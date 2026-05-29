using System.Globalization;
using Evidenciador.Core.Abstractions;
using Evidenciador.Core.Requests;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

namespace Evidenciador.Infra.Playwright;

public sealed class GogsLoginService : IGogsLoginService
{
    private readonly ILogger<GogsLoginService> _logger;
    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private IPage? _page;
    private bool _isLoggedIn;
    private string? _gogsBaseUrl;

    public GogsLoginService(ILogger<GogsLoginService> logger)
    {
        _logger = logger;
    }

    public bool IsLoggedIn => _isLoggedIn;

    public async Task LoginAsync(GogsCredentials credentials, bool headless, string? diagnosticsDir, CancellationToken ct)
    {
        if (_browser != null)
        {
            _logger.LogInformation("Browser já está inicializado");
            return;
        }

        _gogsBaseUrl = credentials.BaseUrl.TrimEnd('/');
        var loginUrl = $"{_gogsBaseUrl}/user/login";

        _playwright = await Microsoft.Playwright.Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = headless,
        });

        var context = await _browser.NewContextAsync(new BrowserNewContextOptions
        {
            ViewportSize = new ViewportSize { Width = 1440, Height = 900 },
        });

        _page = await context.NewPageAsync();
        _page.SetDefaultTimeout(30000);

        await DoLoginAsync(credentials, loginUrl, diagnosticsDir, ct);
    }

    private async Task DoLoginAsync(GogsCredentials credentials, string loginUrl, string? diagnosticsDir, CancellationToken ct)
    {
        _logger.LogInformation("Abrindo página de login do Gogs: {Url}", loginUrl);
        await _page!.GotoAsync(loginUrl, new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded });

        await MaybeCaptureDiagnosticsAsync("01-login", diagnosticsDir);

        _logger.LogInformation("Digitando credenciais (username length={Length})", credentials.Username.Length);
        
        var userNameInput = _page.Locator("input[name='user_name']").First;
        var passwordInput = _page.Locator("input[name='password']").First;
        var submitButton = _page.Locator("button[type='submit']").First;

        await userNameInput.ClickAsync(new LocatorClickOptions { Timeout = 30000 });
        await userNameInput.TypeAsync(credentials.Username, new LocatorTypeOptions { Delay = 35 });
        
        await passwordInput.ClickAsync();
        await passwordInput.TypeAsync(credentials.Password, new LocatorTypeOptions { Delay = 35 });

        await submitButton.ClickAsync(new LocatorClickOptions { Timeout = 30000 });

        _logger.LogInformation("Validando login");
        
        try
        {
            await _page.WaitForURLAsync(url => !url.Contains("/user/login", StringComparison.OrdinalIgnoreCase), new PageWaitForURLOptions { Timeout = 30000 });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "可能已经登录或者URL没有改变");
        }

        await WaitForLoginSuccessAsync(30000, ct);

        await MaybeCaptureDiagnosticsAsync("02-logged-in", diagnosticsDir);

        _isLoggedIn = true;
        _logger.LogInformation("Login no Gogs realizado com sucesso");
    }

    public async Task EnsureLoggedInAsync(GogsCredentials credentials, bool headless, string? diagnosticsDir, CancellationToken ct)
    {
        if (_isLoggedIn && _page != null)
            return;

        await LoginAsync(credentials, headless, diagnosticsDir, ct);
    }

    private async Task WaitForLoginSuccessAsync(int timeoutMs, CancellationToken ct)
    {
        var started = DateTime.UtcNow;

        while ((DateTime.UtcNow - started).TotalMilliseconds < timeoutMs)
        {
            ct.ThrowIfCancellationRequested();

            var userMenu = _page!.Locator("a[href*='/user/']").First;
            if (await userMenu.CountAsync() > 0)
                return;

            await Task.Delay(250, ct);
        }

        _logger.LogWarning("Timeout esperando elemento de login do Gogs, continuando mesmo assim");
    }

    private async Task MaybeCaptureDiagnosticsAsync(string name, string? diagnosticsDir)
    {
        if (diagnosticsDir == null || _page == null) return;

        try
        {
            var dir = System.IO.Directory.CreateDirectory(diagnosticsDir);
            var safeName = string.Concat(name.Select(ch => char.IsLetterOrDigit(ch) ? ch : '-'));
            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
            var path = System.IO.Path.Combine(dir.FullName, $"{timestamp}-gogs-{safeName}.png");
            await _page.ScreenshotAsync(new PageScreenshotOptions { Path = path, FullPage = true });
        }
        catch
        {
        }
    }
}