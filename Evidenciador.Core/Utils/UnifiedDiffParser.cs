using System.Globalization;
using Evidenciador.Core.Models;

namespace Evidenciador.Core.Utils;

public static class UnifiedDiffParser
{
    public static List<FileDiffEvidence> Parse(string diffText)
    {
        var files = new List<FileDiffEvidence>();

        string? currentPath = null;
        string? changeType = null;
        bool isBinary = false;
        bool isTruncated = false;
        var lines = new List<DiffLineEvidence>();

        var inHunk = false;
        int? oldLine = null;
        int? newLine = null;

        void Flush()
        {
            if (currentPath is null) return;
            if (lines.Count == 0)
                lines.Add(new DiffLineEvidence(DiffLineKind.Placeholder, null, null, "[no diff lines captured]"));

            files.Add(new FileDiffEvidence(
                Path: currentPath,
                ChangeType: changeType,
                IsBinary: isBinary,
                IsTruncated: isTruncated,
                Lines: lines.ToArray()));

            currentPath = null;
            changeType = null;
            isBinary = false;
            isTruncated = false;
            lines = new List<DiffLineEvidence>();
            inHunk = false;
            oldLine = null;
            newLine = null;
        }

        foreach (var rawLine in EnumerateLines(diffText))
        {
            var lineText = rawLine;

            if (lineText.StartsWith("diff --git ", StringComparison.Ordinal))
            {
                Flush();

                // Format: diff --git a/path b/path
                var parts = lineText.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 4)
                {
                    currentPath = StripABPrefix(parts[3]);
                }
                else
                {
                    currentPath = "(unknown-path)";
                }
                continue;
            }

            if (currentPath is null)
                continue;

            if (lineText.StartsWith("new file mode ", StringComparison.OrdinalIgnoreCase))
            {
                changeType = "added";
                continue;
            }

            if (lineText.StartsWith("deleted file mode ", StringComparison.OrdinalIgnoreCase))
            {
                changeType = "deleted";
                continue;
            }

            if (lineText.StartsWith("rename from ", StringComparison.OrdinalIgnoreCase))
            {
                changeType = "renamed";
                continue;
            }

            if (lineText.StartsWith("rename to ", StringComparison.OrdinalIgnoreCase))
            {
                changeType = "renamed";
                var p = lineText["rename to ".Length..].Trim();
                if (!string.IsNullOrWhiteSpace(p))
                    currentPath = p;
                continue;
            }

            if (lineText.StartsWith("Binary files ", StringComparison.OrdinalIgnoreCase) ||
                lineText.StartsWith("GIT binary patch", StringComparison.OrdinalIgnoreCase))
            {
                isBinary = true;
                lines.Add(new DiffLineEvidence(DiffLineKind.Placeholder, null, null, "[binary file - diff not shown]"));
                continue;
            }

            if (lineText.StartsWith("@@", StringComparison.Ordinal))
            {
                inHunk = true;
                ParseHunkHeader(lineText, out oldLine, out newLine);
                lines.Add(new DiffLineEvidence(DiffLineKind.HunkHeader, null, null, lineText));
                continue;
            }

            if (!inHunk)
            {
                // Ignore preamble lines (index/---/+++ etc) for evidence purposes.
                continue;
            }

            if (lineText.StartsWith("\\ No newline at end of file", StringComparison.Ordinal))
            {
                lines.Add(new DiffLineEvidence(DiffLineKind.Placeholder, null, null, "[no newline at end of file]"));
                continue;
            }

            if (lineText.Length == 0)
            {
                // Empty context line within hunk.
                lines.Add(new DiffLineEvidence(DiffLineKind.Context, oldLine, newLine, ""));
                if (oldLine.HasValue) oldLine++;
                if (newLine.HasValue) newLine++;
                continue;
            }

            var prefix = lineText[0];
            var text = lineText.Length > 1 ? lineText[1..] : string.Empty;
            switch (prefix)
            {
                case '+':
                    if (lineText.StartsWith("+++", StringComparison.Ordinal)) break;
                    lines.Add(new DiffLineEvidence(DiffLineKind.Addition, null, newLine, text));
                    if (newLine.HasValue) newLine++;
                    break;
                case '-':
                    if (lineText.StartsWith("---", StringComparison.Ordinal)) break;
                    lines.Add(new DiffLineEvidence(DiffLineKind.Deletion, oldLine, null, text));
                    if (oldLine.HasValue) oldLine++;
                    break;
                case ' ':
                    lines.Add(new DiffLineEvidence(DiffLineKind.Context, oldLine, newLine, text));
                    if (oldLine.HasValue) oldLine++;
                    if (newLine.HasValue) newLine++;
                    break;
                default:
                    // Unexpected line inside a hunk; keep as context to avoid losing info.
                    lines.Add(new DiffLineEvidence(DiffLineKind.Context, oldLine, newLine, lineText));
                    break;
            }
        }

        Flush();
        return files;
    }

    private static IEnumerable<string> EnumerateLines(string text)
    {
        using var sr = new StringReader(text);
        string? line;
        while ((line = sr.ReadLine()) is not null)
            yield return line;
    }

    private static string StripABPrefix(string path)
    {
        // Common forms: a/foo, b/foo
        if (path.Length > 2 && path[1] == '/' && (path[0] == 'a' || path[0] == 'b'))
            return path[2..];
        return path;
    }

    private static void ParseHunkHeader(string header, out int? oldStart, out int? newStart)
    {
        // Example: @@ -12,7 +12,9 @@ optional
        oldStart = null;
        newStart = null;

        var minus = header.IndexOf("-", StringComparison.Ordinal);
        var plus = header.IndexOf("+", StringComparison.Ordinal);
        if (minus < 0 || plus < 0) return;

        var oldPart = header[minus..].Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        var newPart = header[plus..].Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        if (oldPart is null || newPart is null) return;

        oldStart = ParseHunkNumber(oldPart);
        newStart = ParseHunkNumber(newPart);
    }

    private static int? ParseHunkNumber(string part)
    {
        // "-12,7" or "+12" etc
        if (part.Length == 0) return null;
        var s = part;
        if (s[0] == '-' || s[0] == '+') s = s[1..];
        var comma = s.IndexOf(',', StringComparison.Ordinal);
        if (comma > 0) s = s[..comma];
        if (int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n)) return n;
        return null;
    }
}
