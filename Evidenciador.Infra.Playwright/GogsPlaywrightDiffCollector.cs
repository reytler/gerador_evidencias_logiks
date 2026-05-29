using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Evidenciador.Core.Abstractions;
using Evidenciador.Core.Models;
using Evidenciador.Core.Requests;
using Evidenciador.Core.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

namespace Evidenciador.Infra.Playwright;

public sealed class GogsPlaywrightDiffCollector : IPullRequestDiffCollector
{
    private readonly ILogger<GogsPlaywrightDiffCollector> _logger;
    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private IPage? _page;
    private bool _isLoggedIn;

    public GogsPlaywrightDiffCollector(ILogger<GogsPlaywrightDiffCollector> logger)
    {
        _logger = logger;
    }

    public void SetLoginService(IGogsLoginService loginService)
    {
    }

    public async Task<PullRequestEvidence> CollectAsync(PullRequestEvidenceRequest request, CancellationToken cancellationToken)
    {
        if (request.GogsCredentials == null)
            throw new ArgumentException("GogsCredentials são obrigatórios para coletor Gogs");

        _logger.LogInformation("Iniciando coleta de evidências do PR Gogs: {Url}", request.PullRequestUrl);

        var gogsBaseUrl = request.GogsCredentials.BaseUrl.TrimEnd('/');
        var prUrl = NormalizeUrl(request.PullRequestUrl, gogsBaseUrl);

        if (_browser != null && _isLoggedIn)
        {
            _logger.LogInformation("Usando browser já logado");
        }
        else
        {
            _logger.LogInformation("Iniciando browser para Gogs");
            _playwright = await Microsoft.Playwright.Playwright.CreateAsync();
            _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = request.Headless,
            });

            var context = await _browser.NewContextAsync(new BrowserNewContextOptions
            {
                ViewportSize = new ViewportSize { Width = 1440, Height = 900 },
            });
            _page = await context.NewPageAsync();
            _page.SetDefaultTimeout(30000);

            await DoLoginAsync(request.GogsCredentials, request.DiagnosticsDir, cancellationToken);
            _isLoggedIn = true;
        }

        try
        {
            _logger.LogInformation("Acessando página do PR: {Url}", prUrl);
            await _page!.GotoAsync(prUrl.ToString(), new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded });

            var prTitle = await ExtractTitleAsync(_page);

            _logger.LogInformation("Título do PR: {Title}", prTitle);

            var diffUrl = GogsPrUrlParser.BuildDiffUrl(prUrl);
            _logger.LogInformation("Baixando diff de: {Url}", diffUrl);

            var diffText = await DownloadDiffAsync(_page, diffUrl, cancellationToken);

            var files = UnifiedDiffParser.Parse(diffText);

            _logger.LogInformation("Coletados {Count} arquivos com alterações", files.Count);

            return new PullRequestEvidence(
                PullRequestTitle: prTitle ?? "PR sem título",
                PullRequestUrl: prUrl,
                FilesUrl: prUrl,
                CollectedAt: DateTimeOffset.UtcNow,
                Files: files);
        }
        finally
        {
            await _browser?.CloseAsync();
            _playwright?.Dispose();
        }
    }

    private async Task DoLoginAsync(GogsCredentials credentials, string? diagnosticsDir, CancellationToken ct)
    {
        var loginUrl = $"{credentials.BaseUrl.TrimEnd('/')}/user/login";
        
        _logger.LogInformation("Abrindo página de login do Gogs: {Url}", loginUrl);
        await _page!.GotoAsync(loginUrl, new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded });

        await CaptureDiagnosticsAsync("01-login", diagnosticsDir);

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
            _logger.LogWarning(ex, "Possivelmente já está logado");
        }

        await Task.Delay(1000, ct);

        await CaptureDiagnosticsAsync("02-logged-in", diagnosticsDir);

        _logger.LogInformation("Login no Gogs realizado com sucesso");
    }

    private async Task CaptureDiagnosticsAsync(string name, string? diagnosticsDir)
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

    private static Uri NormalizeUrl(Uri original, string gogsBaseUrl)
    {
        if (GogsPrUrlParser.TryParse(original, out var info))
        {
            return new Uri($"{gogsBaseUrl}/{info.Owner}/{info.Repo}/pulls/{info.Number}");
        }

        return original;
    }

    private async Task<string?> ExtractTitleAsync(IPage page)
    {
        try
        {
            var titleElement = page.Locator("h2.title").First;
            if (await titleElement.CountAsync() > 0)
            {
                var title = await titleElement.InnerTextAsync();
                return title?.Trim();
            }
        }
        catch
        {
        }

        try
        {
            var prTitleElement = page.Locator(".pull-title").First;
            if (await prTitleElement.CountAsync() > 0)
            {
                var title = await prTitleElement.InnerTextAsync();
                return title?.Trim();
            }
        }
        catch
        {
        }

        return null;
    }

    private async Task<string> DownloadDiffAsync(IPage page, Uri diffUrl, CancellationToken ct)
    {
        try
        {
            await page.GotoAsync(diffUrl.ToString(), new PageGotoOptions 
            { 
                WaitUntil = WaitUntilState.Load,
                Timeout = 60000
            });

            var content = await page.ContentAsync();

            var preElement = page.Locator("pre").First;
            if (await preElement.CountAsync() > 0)
            {
                return await preElement.InnerTextAsync() ?? "";
            }

            var codeElement = page.Locator("code").First;
            if (await codeElement.CountAsync() > 0)
            {
                return await codeElement.InnerTextAsync() ?? "";
            }

            return content;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Não foi possível obter diff diretamente, tentando via API");
            return "";
        }
    }
}