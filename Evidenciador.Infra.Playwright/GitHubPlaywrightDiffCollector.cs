using System.Globalization;
using System.Diagnostics;
using Evidenciador.Core.Abstractions;
using Evidenciador.Core.Models;
using Evidenciador.Core.Requests;
using Evidenciador.Core.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

namespace Evidenciador.Infra.Playwright;

public sealed class GitHubPlaywrightDiffCollector(ILogger<GitHubPlaywrightDiffCollector> logger) : IPullRequestDiffCollector
{
    private readonly ILogger<GitHubPlaywrightDiffCollector> _logger = logger;

    public async Task<PullRequestEvidence> CollectAsync(PullRequestEvidenceRequest request, CancellationToken cancellationToken)
    {
        var filesUrl = GitHubPrUrlNormalizer.NormalizeToFiles(request.PullRequestUrl);

        DirectoryInfo? diagDir = null;
        if (!string.IsNullOrWhiteSpace(request.DiagnosticsDir))
        {
            diagDir = Directory.CreateDirectory(request.DiagnosticsDir);
        }

        using var playwright = await Microsoft.Playwright.Playwright.CreateAsync();

        IBrowser? browser = null;
        IBrowserContext? context = null;
        IPage? page = null;

        try
        {
            browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = request.Headless,
            });

            var contextOptions = new BrowserNewContextOptions
            {
                ViewportSize = new ViewportSize { Width = 1440, Height = 900 },
            };

            if (!string.IsNullOrWhiteSpace(request.StorageStatePath) && File.Exists(request.StorageStatePath))
            {
                contextOptions.StorageStatePath = request.StorageStatePath;
                _logger.LogInformation("Using GitHub storage state: {Path}", request.StorageStatePath);
            }

            context = await browser.NewContextAsync(contextOptions);

            page = await context.NewPageAsync();
            page.SetDefaultTimeout(30000);

            // Capture PR title from the PR root (HTML), then download the unified diff.
            var prRoot = GitHubPrUrlParser.NormalizeToPullRoot(request.PullRequestUrl);
            _logger.LogInformation("Opening PR root to capture title: {Url}", prRoot);
            await page.GotoAsync(prRoot.ToString(), new PageGotoOptions { WaitUntil = WaitUntilState.Load });
            await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);

            if (await LooksLikeLoginAsync(page))
            {
                _logger.LogInformation("Not authenticated; performing interactive GitHub login");
                if (request.GitHubCredentials is null)
                    throw new ArgumentException("GitHubCredentials are required for GitHub PR collection.", nameof(request));

                await DoInteractiveLoginAsync(page, request.GitHubCredentials, diagDir, cancellationToken);

                _logger.LogInformation("Re-opening PR root after login: {Url}", prRoot);
                await page.GotoAsync(prRoot.ToString(), new PageGotoOptions { WaitUntil = WaitUntilState.Load });
                await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);
            }

            await MaybeCaptureDiagnosticsAsync(page, diagDir, "03-pr-root");

            var prTitle = await TryExtractPullRequestTitleAsync(page);

            var diffUrl = BuildDiffUrl(request.PullRequestUrl);
            _logger.LogInformation("Downloading PR diff: {Url}", diffUrl);
            var diffResponse = await page.GotoAsync(diffUrl.ToString(), new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded });
            if (diffResponse is null)
                throw new InvalidOperationException("Failed to load PR diff (no response)." );

            var diffText = await diffResponse.TextAsync();
            await MaybeCaptureDiagnosticsAsync(page, diagDir, "04-pr-diff");
            await MaybeWriteTextArtifactAsync(diagDir, "04-pr-diff", ".diff", diffText);

            var files = UnifiedDiffParser.Parse(diffText);
            _logger.LogInformation("Parsed {FileCount} files from .diff", files.Count);

            // Optional fallback if parsing yields nothing.
            if (files.Count == 0)
            {
                _logger.LogWarning("No files parsed from .diff; falling back to DOM scraping");
                _logger.LogInformation("Navigating to PR files view: {Url}", filesUrl);
                await page.GotoAsync(filesUrl.ToString(), new PageGotoOptions { WaitUntil = WaitUntilState.Load });
                await MaybeCaptureDiagnosticsAsync(page, diagDir, "05-pr-files");
                files = await ScrapeFilesAsync(page, cancellationToken);
                _logger.LogInformation("Scraped {FileCount} files", files.Count);
            }

            return new PullRequestEvidence(
                PullRequestTitle: string.IsNullOrWhiteSpace(prTitle) ? null : prTitle,
                PullRequestUrl: request.PullRequestUrl,
                FilesUrl: filesUrl,
                CollectedAt: DateTimeOffset.UtcNow,
                Files: files);
        }
        finally
        {
            if (context is not null)
            {
                try { await context.CloseAsync(); } catch { }
            }

            if (browser is not null)
            {
                try { await browser.CloseAsync(); } catch { }
            }
        }
    }

    private static async Task<bool> LooksLikeLoginAsync(IPage page)
    {
        if (page.Url.Contains("/login", StringComparison.OrdinalIgnoreCase))
            return true;

        var loginField = page.Locator("#login_field").First;
        return await loginField.CountAsync() > 0;
    }

    private async Task DoInteractiveLoginAsync(IPage page, GitHubCredentials credentials, DirectoryInfo? diagDir, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Opening GitHub login page");
        await page.GotoAsync("https://github.com/login", new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded });
        await MaybeCaptureDiagnosticsAsync(page, diagDir, "01-login");

        _logger.LogInformation("Typing credentials (username length={UsernameLength})", credentials.Username.Length);
        await TypeWithDelayAsync(page.Locator("#login_field"), credentials.Username, cancellationToken);
        await TypeWithDelayAsync(page.Locator("#password"), credentials.Password, cancellationToken);

        await page.Locator("input[name='commit']").ClickAsync(new LocatorClickOptions { Timeout = 30000 });

        _logger.LogInformation("Validating login");
        await page.WaitForURLAsync(url => !url.Contains("/login", StringComparison.OrdinalIgnoreCase), new PageWaitForURLOptions { Timeout = 30000 });
        await WaitForAnyAsync(page, new[]
        {
            "summary[aria-label*='View profile']",
            "summary[aria-label*='View profile and more']",
            "img.avatar-user",
            "a[href='/settings/profile']",
        }, timeoutMs: 30000, cancellationToken);

        await MaybeCaptureDiagnosticsAsync(page, diagDir, "02-logged-in");
    }

    private async Task<List<FileDiffEvidence>> ScrapeFilesAsync(IPage page, CancellationToken cancellationToken)
    {
        var fileLoc = page.Locator("div.js-file");
        if (await fileLoc.CountAsync() == 0)
        {
            fileLoc = page.Locator("div.js-diff-entry");
        }

        if (await fileLoc.CountAsync() == 0)
        {
            fileLoc = page.Locator("div[data-details-container-group='file']");
        }

        var count = await fileLoc.CountAsync();
        var results = new List<FileDiffEvidence>(capacity: count);

        for (var i = 0; i < count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var container = fileLoc.Nth(i);

            var path = await ExtractFilePathAsync(container);
            var changeType = await ExtractChangeTypeAsync(container);

            _logger.LogInformation("[{Index}/{Total}] Extracting diff for {Path}", i + 1, count, path);

            var (isBinary, isTruncated) = await DetectBinaryOrTruncatedAsync(container);
            var lines = new List<DiffLineEvidence>();

            if (isBinary)
            {
                lines.Add(new DiffLineEvidence(DiffLineKind.Placeholder, null, null, "[binary file - diff not shown]"));
            }
            else if (isTruncated)
            {
                lines.Add(new DiffLineEvidence(DiffLineKind.Placeholder, null, null, "[diff truncated/too large - content not fully shown by GitHub]"));
            }
            else
            {
                var sw = Stopwatch.StartNew();
                lines.AddRange(await ExtractDiffLinesAsync(container));
                sw.Stop();
                _logger.LogInformation("[{Index}/{Total}] Captured {LineCount} lines in {ElapsedMs}ms", i + 1, count, lines.Count, sw.ElapsedMilliseconds);
                if (lines.Count == 0)
                {
                    // Empty diffs happen (e.g., whitespace-only changes hidden).
                    lines.Add(new DiffLineEvidence(DiffLineKind.Placeholder, null, null, "[no diff lines captured]"));
                }
            }

            results.Add(new FileDiffEvidence(
                Path: path,
                ChangeType: changeType,
                IsBinary: isBinary,
                IsTruncated: isTruncated,
                Lines: lines));
        }

        return results;
    }

    private static async Task<string> ExtractFilePathAsync(ILocator container)
    {
        var path = await container.GetAttributeAsync("data-path");
        if (!string.IsNullOrWhiteSpace(path)) return path.Trim();

        // GitHub header often contains the path in a link.
        var headerLink = container.Locator(".file-header a.Link--primary").First;
        if (await headerLink.CountAsync() > 0)
        {
            var text = (await headerLink.InnerTextAsync()).Trim();
            if (!string.IsNullOrWhiteSpace(text)) return text;
        }

        var headerText = container.Locator(".file-header").First;
        if (await headerText.CountAsync() > 0)
        {
            var text = (await headerText.InnerTextAsync()).Trim();
            if (!string.IsNullOrWhiteSpace(text))
            {
                // Best-effort: first line tends to include the path.
                var firstLine = text.Split('\n', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(firstLine)) return firstLine.Trim();
            }
        }

        return "(unknown-path)";
    }

    private static async Task<string?> ExtractChangeTypeAsync(ILocator container)
    {
        // Best-effort: GitHub sometimes shows a small label like "added"/"deleted".
        var label = container.Locator(".file-header .file-info .text-emphasized").First;
        if (await label.CountAsync() > 0)
        {
            var text = (await label.InnerTextAsync()).Trim();
            if (!string.IsNullOrWhiteSpace(text)) return text;
        }

        var header = container.Locator(".file-header").First;
        if (await header.CountAsync() == 0) return null;
        var headerText = (await header.InnerTextAsync()).ToLowerInvariant();

        if (headerText.Contains("renamed", StringComparison.OrdinalIgnoreCase)) return "renamed";
        if (headerText.Contains("deleted", StringComparison.OrdinalIgnoreCase)) return "deleted";
        if (headerText.Contains("added", StringComparison.OrdinalIgnoreCase)) return "added";
        return null;
    }

    private static async Task<(bool isBinary, bool isTruncated)> DetectBinaryOrTruncatedAsync(ILocator container)
    {
        // Binary indicators
        var binary = container.Locator("text=/binary file/i");
        if (await binary.CountAsync() > 0) return (true, false);

        // Truncation/large diff indicators (best-effort; GitHub wording can vary)
        var tooLarge = container.Locator("text=/too (large|big) to display/i");
        if (await tooLarge.CountAsync() > 0) return (false, true);

        var truncated = container.Locator("text=/diff (is|was) too large/i");
        if (await truncated.CountAsync() > 0) return (false, true);

        var limited = container.Locator("text=/limited to/i");
        if (await limited.CountAsync() > 0) return (false, true);

        return (false, false);
    }

    private static async Task<List<DiffLineEvidence>> ExtractDiffLinesAsync(ILocator container)
    {
        // Fast path: extract all rows in one browser roundtrip.
        try
        {
            var rows = container.Locator("table.diff-table tr");
            var raw = await rows.EvaluateAllAsync<JsDiffLine[]>(@"rows => rows.map(row => {
  const hunk = row.querySelector('td.blob-code-hunk');
  if (hunk) {
    let t = hunk.textContent || '';
    t = t.replace(/\u00a0/g, ' ').replace(/\r/g, '');
    if (t.endsWith('\n')) t = t.slice(0, -1);
    return { kind: 'HunkHeader', oldNum: null, newNum: null, text: t };
  }

  const code = row.querySelector('td.blob-code');
  if (!code) return null;

  const cls = (code.getAttribute('class') || '').toLowerCase();
  let kind = 'Context';
  if (cls.includes('blob-code-addition')) kind = 'Addition';
  else if (cls.includes('blob-code-deletion')) kind = 'Deletion';

  const nums = row.querySelectorAll('td.blob-num');
  const oldNum = nums.length >= 1 ? (nums[0].getAttribute('data-line-number') || null) : null;
  const newNum = nums.length >= 2 ? (nums[1].getAttribute('data-line-number') || null) : null;

  let t = code.textContent || '';
  t = t.replace(/\u00a0/g, ' ').replace(/\r/g, '');
  if (t.endsWith('\n')) t = t.slice(0, -1);
  return { kind, oldNum, newNum, text: t };
}).filter(x => x !== null)");

            var lines = new List<DiffLineEvidence>(capacity: raw.Length);
            foreach (var r in raw)
            {
                var kind = r.Kind switch
                {
                    "Addition" => DiffLineKind.Addition,
                    "Deletion" => DiffLineKind.Deletion,
                    "HunkHeader" => DiffLineKind.HunkHeader,
                    _ => DiffLineKind.Context,
                };

                lines.Add(new DiffLineEvidence(
                    kind,
                    ParseNullableInt(r.OldNum),
                    ParseNullableInt(r.NewNum),
                    string.IsNullOrEmpty(r.Text) ? " " : r.Text));
            }

            return lines;
        }
        catch
        {
            // Fallback (slower) but more tolerant.
            var rows = container.Locator("table.diff-table tr");
            var rowCount = await rows.CountAsync();
            var lines = new List<DiffLineEvidence>(capacity: Math.Max(16, rowCount));

            for (var i = 0; i < rowCount; i++)
            {
                var row = rows.Nth(i);

                var hunk = row.Locator("td.blob-code-hunk");
                if (await hunk.CountAsync() > 0)
                {
                    var text = await GetCellTextPreserveAsync(hunk.First);
                    lines.Add(new DiffLineEvidence(DiffLineKind.HunkHeader, null, null, text));
                    continue;
                }

                var code = row.Locator("td.blob-code");
                if (await code.CountAsync() == 0) continue;

                var codeCell = code.First;
                var cls = (await codeCell.GetAttributeAsync("class")) ?? string.Empty;
                var kind = cls.Contains("blob-code-addition", StringComparison.OrdinalIgnoreCase)
                    ? DiffLineKind.Addition
                    : cls.Contains("blob-code-deletion", StringComparison.OrdinalIgnoreCase)
                        ? DiffLineKind.Deletion
                        : DiffLineKind.Context;

                var nums = row.Locator("td.blob-num");
                int? oldNum = null;
                int? newNum = null;
                if (await nums.CountAsync() >= 1)
                    oldNum = ParseNullableInt(await nums.Nth(0).GetAttributeAsync("data-line-number"));
                if (await nums.CountAsync() >= 2)
                    newNum = ParseNullableInt(await nums.Nth(1).GetAttributeAsync("data-line-number"));

                var textContent = await GetCellTextPreserveAsync(codeCell);
                lines.Add(new DiffLineEvidence(kind, oldNum, newNum, textContent));
            }

            return lines;
        }
    }

    private sealed record JsDiffLine(string Kind, string? OldNum, string? NewNum, string Text);

    private static int? ParseNullableInt(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n)) return n;
        return null;
    }

    private static async Task<string> GetCellTextPreserveAsync(ILocator cell)
    {
        // innerText() can normalize whitespace; textContent is closer to the source.
        var text = await cell.EvaluateAsync<string>("el => el.textContent");
        text = text.Replace("\u00A0", " ").Replace("\r", "");
        if (text.EndsWith("\n", StringComparison.Ordinal))
            text = text[..^1];
        return text;
    }

    private static async Task TypeWithDelayAsync(ILocator locator, string value, CancellationToken cancellationToken)
    {
        await locator.ClickAsync(new LocatorClickOptions { Timeout = 30000 });
        await locator.TypeAsync(value, new LocatorTypeOptions
        {
            Delay = 35,
        });
        cancellationToken.ThrowIfCancellationRequested();
    }

    private static async Task<string?> TryExtractPullRequestTitleAsync(IPage page)
    {
        // Common GitHub PR header.
        var title = await TryGetInnerTextAsync(page, "h1.gh-header-title .js-issue-title");
        if (!string.IsNullOrWhiteSpace(title)) return CleanTitle(title);

        title = await TryGetInnerTextAsync(page, "h1.gh-header-title");
        if (!string.IsNullOrWhiteSpace(title)) return CleanTitle(title);

        // Newer UI experiments.
        title = await TryGetInnerTextAsync(page, "[data-testid='issue-title']");
        if (!string.IsNullOrWhiteSpace(title)) return CleanTitle(title);

        // Resilient fallback: og:title.
        var og = page.Locator("meta[property='og:title']").First;
        if (await og.CountAsync() > 0)
        {
            var content = await og.GetAttributeAsync("content");
            var parsed = ParseOgOrDocumentTitle(content);
            if (!string.IsNullOrWhiteSpace(parsed)) return CleanTitle(parsed);
        }

        // Last resort: document.title.
        try
        {
            var docTitle = await page.TitleAsync();
            var parsed = ParseOgOrDocumentTitle(docTitle);
            if (!string.IsNullOrWhiteSpace(parsed)) return CleanTitle(parsed);
        }
        catch
        {
            // ignore
        }

        return null;
    }

    private static async Task<string?> TryGetInnerTextAsync(IPage page, string selector)
    {
        var loc = page.Locator(selector).First;
        if (await loc.CountAsync() == 0) return null;
        try
        {
            var t = (await loc.InnerTextAsync()).Trim();
            return string.IsNullOrWhiteSpace(t) ? null : t;
        }
        catch
        {
            return null;
        }
    }

    private static string ParseOgOrDocumentTitle(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;

        // Common formats:
        // - "My title · owner/repo"
        // - "My title by user · Pull Request #123 · owner/repo · GitHub"
        var s = raw.Trim();

        var cutTokens = new[]
        {
            "· Pull Request",
            "· Issue",
            "· GitHub",
            "· github.com",
        };

        foreach (var token in cutTokens)
        {
            var idx = s.IndexOf(token, StringComparison.OrdinalIgnoreCase);
            if (idx > 0)
            {
                s = s[..idx].Trim();
                break;
            }
        }

        // If still contains a repo suffix separated by "·", keep only the first segment.
        var dot = s.IndexOf('·');
        if (dot > 0) s = s[..dot].Trim();

        return s;
    }

    private static string CleanTitle(string raw)
    {
        var s = raw.Trim();
        s = s.Replace("\r", "");
        s = string.Join(" ", s.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()));
        while (s.Contains("  ", StringComparison.Ordinal)) s = s.Replace("  ", " ");

        if (s.StartsWith("Draft:", StringComparison.OrdinalIgnoreCase))
            s = s["Draft:".Length..].Trim();

        return s;
    }

    private static Uri BuildDiffUrl(Uri prUrl)
    {
        if (GitHubPrUrlParser.TryParse(prUrl, out var info))
            return new Uri($"https://github.com/{info.Owner}/{info.Repo}/pull/{info.Number}.diff");

        // Best-effort fallback.
        return new Uri(prUrl.ToString().TrimEnd('/') + ".diff");
    }

    private static async Task MaybeWriteTextArtifactAsync(DirectoryInfo? diagDir, string name, string extension, string content)
    {
        if (diagDir is null) return;

        var safeName = string.Concat(name.Select(ch => char.IsLetterOrDigit(ch) ? ch : '-'));
        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        var path = Path.Combine(diagDir.FullName, $"{timestamp}-{safeName}{extension}");

        try
        {
            await File.WriteAllTextAsync(path, content);
        }
        catch
        {
            // ignore
        }
    }

    private async Task MaybeCaptureDiagnosticsAsync(IPage page, DirectoryInfo? diagDir, string name)
    {
        if (diagDir is null) return;

        var safeName = string.Concat(name.Select(ch => char.IsLetterOrDigit(ch) ? ch : '-'));
        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        var prefix = Path.Combine(diagDir.FullName, $"{timestamp}-{safeName}");

        try
        {
            await page.ScreenshotAsync(new PageScreenshotOptions { Path = prefix + ".png", FullPage = true });
        }
        catch
        {
            // ignore
        }

        try
        {
            var html = await page.ContentAsync();
            await File.WriteAllTextAsync(prefix + ".html", html);
        }
        catch
        {
            // ignore
        }
    }

    private static async Task WaitForAnyAsync(IPage page, IEnumerable<string> selectors, int timeoutMs, CancellationToken cancellationToken)
    {
        var started = DateTime.UtcNow;
        var selectorsList = selectors.ToArray();

        while ((DateTime.UtcNow - started).TotalMilliseconds < timeoutMs)
        {
            cancellationToken.ThrowIfCancellationRequested();

            foreach (var selector in selectorsList)
            {
                var loc = page.Locator(selector).First;
                if (await loc.CountAsync() > 0)
                {
                    return;
                }
            }

            await Task.Delay(250, cancellationToken);
        }

        throw new TimeoutException($"Timed out waiting for selectors on GitHub. Current URL: {page.Url}");
    }
}
