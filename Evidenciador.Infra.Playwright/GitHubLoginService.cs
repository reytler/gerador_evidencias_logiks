using System.Globalization;
using Evidenciador.Core.Abstractions;
using Evidenciador.Core.Requests;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

namespace Evidenciador.Infra.Playwright;

public sealed class GitHubLoginService : IGitHubLoginService
{
    private readonly ILogger<GitHubLoginService> _logger;
    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private IBrowserContext? _context;
    private IPage? _page;
    private bool _isLoggedIn;

    public GitHubLoginService(ILogger<GitHubLoginService> logger)
    {
        _logger = logger;
    }

    public bool IsLoggedIn => _isLoggedIn;

    public async Task LoginAsync(GitHubCredentials credentials, bool headless, string? diagnosticsDir, CancellationToken ct)
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
        });

        _context = await _browser.NewContextAsync(new BrowserNewContextOptions
        {
            ViewportSize = new ViewportSize { Width = 1440, Height = 900 },
        });

        _page = await _context.NewPageAsync();
        _page.SetDefaultTimeout(30000);

        await DoLoginAsync(credentials, diagnosticsDir, ct);
    }

    private async Task DoLoginAsync(GitHubCredentials credentials, string? diagnosticsDir, CancellationToken ct)
    {
        _logger.LogInformation("Abrindo GitHub login page");
        await _page!.GotoAsync("https://github.com/login", new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded });

        await MaybeCaptureDiagnosticsAsync("01-login", diagnosticsDir);

        _logger.LogInformation("Digitando credenciais (username length={Length})", credentials.Username.Length);
        await TypeWithDelayAsync(_page.Locator("#login_field"), credentials.Username, ct);
        await TypeWithDelayAsync(_page.Locator("#password"), credentials.Password, ct);

        await _page.Locator("input[name='commit']").ClickAsync(new LocatorClickOptions { Timeout = 30000 });

        _logger.LogInformation("Validando login");
        await _page.WaitForURLAsync(url => !url.Contains("/login", StringComparison.OrdinalIgnoreCase), new PageWaitForURLOptions { Timeout = 30000 });

        await WaitForAnyAsync(new[]
        {
            "summary[aria-label*='View profile']",
            "summary[aria-label*='View profile and more']",
            "img.avatar-user",
            "a[href='/settings/profile']",
        }, 30000, ct);

        await MaybeCaptureDiagnosticsAsync("02-logged-in", diagnosticsDir);

        _isLoggedIn = true;
        _logger.LogInformation("Login no GitHub realizado com sucesso");
    }

    public async Task EnsureLoggedInAsync(GitHubCredentials credentials, bool headless, string? diagnosticsDir, CancellationToken ct)
    {
        if (_isLoggedIn && _page != null)
            return;

        await LoginAsync(credentials, headless, diagnosticsDir, ct);
    }

    public async Task<string?> TryExportStorageStateAsync(string? diagnosticsDir, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        if (!_isLoggedIn || _context == null)
            return null;

        try
        {
            var dir = diagnosticsDir != null
                ? System.IO.Directory.CreateDirectory(diagnosticsDir)
                : System.IO.Directory.CreateDirectory(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "evidenciador"));

            var path = System.IO.Path.Combine(dir.FullName, "github-storage-state.json");

            await _context.StorageStateAsync(new BrowserContextStorageStateOptions
            {
                Path = path,
            });

            _logger.LogInformation("GitHub storage state exportado: {Path}", path);
            return path;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao exportar GitHub storage state");
            return null;
        }
    }

    private static async Task TypeWithDelayAsync(ILocator locator, string value, CancellationToken ct)
    {
        await locator.ClickAsync(new LocatorClickOptions { Timeout = 30000 });
        await locator.TypeAsync(value, new LocatorTypeOptions { Delay = 35 });
        ct.ThrowIfCancellationRequested();
    }

    private async Task WaitForAnyAsync(string[] selectors, int timeoutMs, CancellationToken ct)
    {
        var started = DateTime.UtcNow;

        while ((DateTime.UtcNow - started).TotalMilliseconds < timeoutMs)
        {
            ct.ThrowIfCancellationRequested();

            foreach (var selector in selectors)
            {
                var loc = _page!.Locator(selector).First;
                if (await loc.CountAsync() > 0)
                    return;
            }

            await Task.Delay(250, ct);
        }

        throw new TimeoutException($"Timed out waiting for selectors on GitHub. Current URL: {_page?.Url}");
    }

    private async Task MaybeCaptureDiagnosticsAsync(string name, string? diagnosticsDir)
    {
        if (diagnosticsDir == null || _page == null) return;

        try
        {
            var dir = System.IO.Directory.CreateDirectory(diagnosticsDir);
            var safeName = string.Concat(name.Select(ch => char.IsLetterOrDigit(ch) ? ch : '-'));
            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
            var path = System.IO.Path.Combine(dir.FullName, $"{timestamp}-github-{safeName}.png");
            await _page.ScreenshotAsync(new PageScreenshotOptions { Path = path, FullPage = true });
        }
        catch
        {
        }
    }
}
