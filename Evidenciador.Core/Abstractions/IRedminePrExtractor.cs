using System.Text.RegularExpressions;

namespace Evidenciador.Core.Abstractions;

public interface IRedminePrExtractor
{
    IReadOnlyList<Uri> ExtractPrUrlsFromDescription(string description);
}

public sealed class RedminePrExtractor : IRedminePrExtractor
{
    private static readonly Regex GithubPrRegex = new(
        @"https://github\.com/[^\s/]+/[^\s/]+/pull/\d+",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public IReadOnlyList<Uri> ExtractPrUrlsFromDescription(string description)
    {
        if (string.IsNullOrWhiteSpace(description))
            return Array.Empty<Uri>();

        var matches = GithubPrRegex.Matches(description);
        var urls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (Match match in matches)
        {
            urls.Add(match.Value.TrimEnd('/'));
        }

        return urls.Select(u => new Uri(u)).ToList();
    }
}