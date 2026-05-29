using System.Text.RegularExpressions;

namespace Evidenciador.Core.Utils;

public sealed record GogsPrInfo(string Owner, string Repo, int Number, string BaseUrl);

public static class GogsPrUrlParser
{
    private static readonly Regex GogsPrRegex = new(
        @"/([^/]+)/([^/]+)/pulls?/(\d+)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static bool IsGogsUrl(Uri url)
    {
        if (url.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase))
            return false;
        
        return url.AbsolutePath.Contains("/pull/", StringComparison.OrdinalIgnoreCase) ||
               url.AbsolutePath.Contains("/pulls/", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsGitHubUrl(Uri url)
    {
        return url.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase) &&
               url.AbsolutePath.Contains("/pull/", StringComparison.OrdinalIgnoreCase);
    }

    public static bool TryParse(Uri prUrl, out GogsPrInfo info)
    {
        info = default!;
        
        if (!IsGogsUrl(prUrl))
            return false;

        var path = prUrl.AbsolutePath.TrimEnd('/');
        var match = GogsPrRegex.Match(path);
        
        if (!match.Success)
            return false;

        var owner = match.Groups[1].Value;
        var repo = match.Groups[2].Value.TrimEnd('.').TrimEnd("git".ToCharArray());
        
        if (!int.TryParse(match.Groups[3].Value, out var number))
            return false;

        info = new GogsPrInfo(owner, repo, number, prUrl.GetLeftPart(UriPartial.Authority));
        return true;
    }

    public static Uri NormalizeToPullRoot(Uri prUrl)
    {
        if (!TryParse(prUrl, out var info))
            return prUrl;

        return new UriBuilder(prUrl)
        {
            Path = $"/{info.Owner}/{info.Repo}/pulls/{info.Number}",
            Query = string.Empty,
            Fragment = string.Empty,
        }.Uri;
    }

    public static Uri BuildDiffUrl(Uri prUrl)
    {
        if (TryParse(prUrl, out var info))
            return new Uri($"{info.BaseUrl}/{info.Owner}/{info.Repo}/pulls/{info.Number}.diff");

        var diffUrl = prUrl.ToString().TrimEnd('/');
        if (!diffUrl.EndsWith(".diff", StringComparison.OrdinalIgnoreCase))
            diffUrl += ".diff";
        
        return new Uri(diffUrl);
    }
}