using System.Text.RegularExpressions;
using Evidenciador.Core.Abstractions;
using Evidenciador.Core.Models;
using Evidenciador.Core.Requests;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

namespace Evidenciador.Infra.Playwright;

public sealed class RedminePlaywrightCollector : IRedmineIssueCollector, IAsyncDisposable
{
    private readonly RedminePlaywrightCollectorOptions _options;
    private readonly ILogger<RedminePlaywrightCollector> _logger;
    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private IPage? _page;
    private bool _isLoggedIn;

    public RedminePlaywrightCollector(
        RedminePlaywrightCollectorOptions options,
        ILogger<RedminePlaywrightCollector> logger)
    {
        _options = options;
        _logger = logger;
    }

    public async Task LoginAsync(RedmineCredentials credentials, bool headless, CancellationToken ct)
    {
        if (_browser != null)
        {
            _logger.LogInformation("Browser já está inicializado");
            return;
        }

        _playwright = await Microsoft.Playwright.Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = headless,
            SlowMo = _options.SlowMo
        });

        var context = await _browser.NewContextAsync(new BrowserNewContextOptions
        {
            ViewportSize = new ViewportSize { Width = 1440, Height = 900 }
        });

        _page = await context.NewPageAsync();
        _page.SetDefaultTimeout(_options.Timeout);

        await DoLoginAsync(credentials, ct);
    }

    private async Task DoLoginAsync(RedmineCredentials credentials, CancellationToken ct)
    {
        for (int attempt = 1; attempt <= _options.RetryCount; attempt++)
        {
            try
            {
                _logger.LogInformation("Tentativa de login no Redmine {Attempt}/{Max}", attempt, _options.RetryCount);
                
                var loginUrl = BuildUrl(_options.LoginUrl);
                await _page!.GotoAsync(loginUrl, new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded });
                await _page.WaitForSelectorAsync(RedmineSelectors.UsernameInput);

                await _page.FillAsync(RedmineSelectors.UsernameInput, credentials.Username);
                await _page.FillAsync(RedmineSelectors.PasswordInput, credentials.Password);
                await _page.ClickAsync(RedmineSelectors.LoginButton);

                await _page.WaitForURLAsync(url => !url.Contains("/login", StringComparison.OrdinalIgnoreCase), 
                    new PageWaitForURLOptions { Timeout = _options.Timeout });

                _isLoggedIn = true;
                _logger.LogInformation("Login no Redmine realizado com sucesso");
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Falha no login, tentativa {Attempt}", attempt);
                if (attempt == _options.RetryCount) throw;
                await Task.Delay(_options.RetryDelayMs, ct);
            }
        }
    }

    public async Task<RedmineIssue?> CollectAsync(string issueIdOrUrl, CancellationToken ct)
    {
        EnsureLoggedIn();

        var url = issueIdOrUrl.StartsWith("http") 
            ? issueIdOrUrl 
            : BuildUrl($"/issues/{issueIdOrUrl}");

        _logger.LogInformation("Coletando issue: {Url}", url);
        await _page!.GotoAsync(url, new PageGotoOptions { WaitUntil = WaitUntilState.Load });
        await _page.WaitForSelectorAsync(RedmineSelectors.Subject);

        return await ExtractIssueDataAsync(url, ct);
    }

    public async IAsyncEnumerable<RedmineIssue> StreamIssuesAsync(RedmineQuery query, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        EnsureLoggedIn();

        var issuesUrl = BuildUrl(BuildIssuesQuery(query));
        _logger.LogInformation("Navigating to issues: {Url}", issuesUrl);

        while (!string.IsNullOrEmpty(issuesUrl))
        {
            await _page!.GotoAsync(issuesUrl, new PageGotoOptions { WaitUntil = WaitUntilState.Load });
            await _page.WaitForSelectorAsync(RedmineSelectors.IssuesTable);

            var issueLinks = await _page.EvalOnSelectorAllAsync<string[]>(
                RedmineSelectors.IssueLinkInRow,
                "els => els.map(e => e.href)");

            _logger.LogInformation("Encontradas {Count} issues na página", issueLinks.Length);

            foreach (var link in issueLinks)
            {
                ct.ThrowIfCancellationRequested();
                
                var issue = await CollectAsync(link, ct);
                if (issue != null)
                    yield return issue;

                await _page.GotoAsync(issuesUrl, new PageGotoOptions { WaitUntil = WaitUntilState.Load });
                await _page.WaitForSelectorAsync(RedmineSelectors.IssuesTable);
            }

            var nextLink = await _page.QuerySelectorAsync(RedmineSelectors.NextPageLink);
            issuesUrl = nextLink != null 
                ? await nextLink.GetAttributeAsync("href") 
                : null;

            if (!string.IsNullOrEmpty(issuesUrl) && !issuesUrl.StartsWith("http"))
                issuesUrl = BuildUrl(issuesUrl);
        }
    }

    private async Task<RedmineIssue?> ExtractIssueDataAsync(string url, CancellationToken ct)
    {
        try
        {
            var issueId = ExtractIssueId(url);
            
            var data = new RedmineIssue(
                IssueId: issueId,
                Subject: await GetTextSafeAsync(RedmineSelectors.Subject),
                Description: await GetTextSafeAsync(RedmineSelectors.Description),
                ProjectName: await GetTextSafeAsync(RedmineSelectors.Project),
                TrackerName: await GetTextSafeAsync(RedmineSelectors.Tracker),
                StatusName: await GetTextSafeAsync(RedmineSelectors.Status),
                AssignedToName: await GetTextSafeAsync(RedmineSelectors.AssignedTo),
                Url: url);

            _logger.LogInformation("Issue {IssueId} coletada: {Subject}", issueId, data.Subject);
            return data;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao coletar issue: {Url}", url);
            return null;
        }
    }

    private void EnsureLoggedIn()
    {
        if (_page == null || !_isLoggedIn)
            throw new InvalidOperationException("Você precisa chamar LoginAsync primeiro");
    }

    private string BuildUrl(string path)
    {
        var baseUrl = _options.BaseUrl.TrimEnd('/');
        path = path.TrimStart('/');
        return $"{baseUrl}/{path}";
    }

    private string BuildIssuesQuery(RedmineQuery query)
    {
        var url = _options.IssuesUrl;
        var separator = url.Contains('?') ? "&" : "?";
        
        var filters = new List<string>();
        
        if (!string.IsNullOrEmpty(query.ProjectId))
            filters.Add($"project_id={query.ProjectId}");
        
        if (!string.IsNullOrEmpty(query.TrackerId))
            filters.Add($"tracker_id={query.TrackerId}");
        
        if (!string.IsNullOrEmpty(query.StatusId))
            filters.Add($"status_id={query.StatusId}");
        
        if (!string.IsNullOrEmpty(query.AssignedToId))
            filters.Add($"assigned_to_id={query.AssignedToId}");
        
        if (query.AssignedToMe == true)
            filters.Add($"assigned_to_id=me");

        if (filters.Count > 0)
            url += separator + string.Join("&", filters);

        return url;
    }

    private static string ExtractIssueId(string url)
    {
        var match = Regex.Match(url, @"/issues/(\d+)");
        return match.Success ? match.Groups[1].Value : "unknown";
    }

    private async Task<string> GetTextSafeAsync(string selector)
    {
        try
        {
            var element = await _page!.QuerySelectorAsync(selector);
            return element != null
                ? (await element.InnerTextAsync()).Replace("\r\n", "\n").Replace("\r", "\n").Trim()
                : string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_page != null)
        {
            try { await _page.CloseAsync(); } catch { }
        }
        
        if (_browser != null)
        {
            try { await _browser.CloseAsync(); } catch { }
        }

        _playwright?.Dispose();
    }

    public async Task<string?> CaptureScreenshotAsync(string issueUrl, string outputDir, CancellationToken ct)
    {
        EnsureLoggedIn();

        try
        {
            Directory.CreateDirectory(outputDir);
            var safeName = $"issue_{ExtractIssueId(issueUrl)}_{DateTime.Now:yyyyMMddHHmmss}.png";
            var screenshotPath = Path.Combine(outputDir, safeName);

            await _page!.GotoAsync(issueUrl, new PageGotoOptions { WaitUntil = WaitUntilState.Load });
            await _page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);
            
            await _page.ScreenshotAsync(new PageScreenshotOptions
            {
                Path = screenshotPath,
                FullPage = true
            });

            _logger.LogInformation("Screenshot capturado: {Path}", screenshotPath);
            return screenshotPath;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao capturar screenshot da issue: {Url}", issueUrl);
            return null;
        }
    }

    public async Task<IReadOnlyList<Uri>> ExtractPrUrlsFromPageAsync(CancellationToken ct)
    {
        EnsureLoggedIn();

        try
        {
            _logger.LogInformation("Escaneando pagina da issue em busca de PRs...");

            var links = await _page!.EvalOnSelectorAllAsync<string[]>(
                "a[href]",
                "els => els.map(e => e.href).filter(h => h.includes('/pull/'))");

            var urls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var link in links)
            {
                var cleanUrl = link.TrimEnd('/');
                if (!cleanUrl.Contains("/pull/")) continue;
                urls.Add(cleanUrl);
            }

            var result = urls.Select(u => new Uri(u)).ToList();
            _logger.LogInformation("Encontrados {Count} link(s) de PR na pagina", result.Count);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao extrair PRs da pagina");
            return [];
        }
    }
}