namespace Evidenciador.Core.Utils;

public static class GitHubPrUrlNormalizer
{
    public static Uri NormalizeToFiles(Uri prUrl)
    {
        if (!string.Equals(prUrl.Host, "github.com", StringComparison.OrdinalIgnoreCase))
            return prUrl;

        // Expected forms:
        // - https://github.com/{owner}/{repo}/pull/{n}
        // - https://github.com/{owner}/{repo}/pull/{n}/files
        // - https://github.com/{owner}/{repo}/pull/{n}/commits (normalize to /files)
        var path = prUrl.AbsolutePath.TrimEnd('/');
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length < 4) return prUrl;

        // owner/repo/pull/123[/...]
        if (!string.Equals(segments[2], "pull", StringComparison.OrdinalIgnoreCase))
            return prUrl;

        // If already in /files, keep the URL as-is (preserve query/fragment).
        if (segments.Length >= 5 && string.Equals(segments[4], "files", StringComparison.OrdinalIgnoreCase))
            return prUrl;

        var owner = segments[0];
        var repo = segments[1];
        var prNumber = segments[3];
        var newPath = $"/{owner}/{repo}/pull/{prNumber}/files";

        var builder = new UriBuilder(prUrl)
        {
            Path = newPath,
            // Preserve any original query params (e.g., ?w=1) and fragment.
            Query = prUrl.Query.TrimStart('?'),
            Fragment = prUrl.Fragment.TrimStart('#'),
        };
        return builder.Uri;
    }
}
