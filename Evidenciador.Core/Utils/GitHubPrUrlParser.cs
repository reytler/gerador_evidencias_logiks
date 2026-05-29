namespace Evidenciador.Core.Utils;

public sealed record GitHubPrInfo(string Owner, string Repo, int Number);

public static class GitHubPrUrlParser
{
    public static bool TryParse(Uri prUrl, out GitHubPrInfo info)
    {
        info = default!;

        if (!string.Equals(prUrl.Host, "github.com", StringComparison.OrdinalIgnoreCase))
            return false;

        var path = prUrl.AbsolutePath.TrimEnd('/');
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length < 4) return false;

        // owner/repo/pull/123[/...]
        if (!string.Equals(segments[2], "pull", StringComparison.OrdinalIgnoreCase))
            return false;

        var owner = segments[0];
        var repo = segments[1];
        if (!int.TryParse(segments[3], out var n)) return false;

        info = new GitHubPrInfo(owner, repo, n);
        return true;
    }

    public static Uri NormalizeToPullRoot(Uri prUrl)
    {
        if (!TryParse(prUrl, out var info)) return prUrl;
        return new UriBuilder(prUrl)
        {
            Path = $"/{info.Owner}/{info.Repo}/pull/{info.Number}",
            Query = string.Empty,
            Fragment = string.Empty,
        }.Uri;
    }
}
