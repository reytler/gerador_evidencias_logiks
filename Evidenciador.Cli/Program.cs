using Evidenciador.Core.Abstractions;
using Evidenciador.Infra.Docx;
using Evidenciador.Infra.Playwright;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Evidenciador.Cli;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder();

        builder.Configuration.Sources.Clear();
        builder.Configuration
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
            .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: false);

        if (builder.Environment.IsDevelopment())
        {
            builder.Configuration.AddUserSecrets(typeof(Program).Assembly, optional: true);
        }

        builder.Configuration.AddEnvironmentVariables();

        builder.Logging.ClearProviders();
        builder.Logging.AddSimpleConsole(o =>
        {
            o.SingleLine = true;
            o.TimestampFormat = "HH:mm:ss ";
        });
        builder.Logging.SetMinimumLevel(LogLevel.Information);

        CliOptions options;
        try
        {
            options = CliArgumentParser.Parse(args, builder.Configuration);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            Console.Error.WriteLine("Usage: dotnet run --project Evidenciador.Cli -- [--pr-url <url>] [--out <path.docx|dir>] [--out-dir <dir>] [--headless true|false] [--diagnostics-dir <dir>]");
            Console.Error.WriteLine("You can also set defaults in appsettings.json under section 'Evidenciador' (PrUrl/Out/OutDir/Headless/DiagnosticsDir/Mode/RedmineQuery).");
            return 1;
        }

        builder.Services.AddSingleton(options);
        builder.Services.AddSingleton<GitHubPlaywrightDiffCollector>();
        builder.Services.AddSingleton<GogsPlaywrightDiffCollector>();
        builder.Services.AddSingleton<IPullRequestDiffCollector>(sp => sp.GetRequiredService<GitHubPlaywrightDiffCollector>());
        builder.Services.AddSingleton<IPullRequestDiffCollectorFactory, PullRequestDiffCollectorFactory>();
        builder.Services.AddSingleton<IDocxEvidenceRenderer, OpenXmlDocxEvidenceRenderer>();
        
        builder.Services.AddSingleton<RedminePlaywrightCollectorOptions>(sp =>
        {
            var config = sp.GetRequiredService<IConfiguration>();
            return new RedminePlaywrightCollectorOptions
            {
                BaseUrl = config["Redmine:BaseUrl"] ?? "",
                LoginUrl = config["Redmine:LoginUrl"] ?? "/login",
                IssuesUrl = config["Redmine:IssuesUrl"] ?? "/issues",
                Headless = true,
                Timeout = 30000,
                SlowMo = 0,
                RetryCount = 3,
                RetryDelayMs = 2000
            };
        });
        builder.Services.AddSingleton<IRedmineIssueCollector, RedminePlaywrightCollector>();
        builder.Services.AddSingleton<IGitHubLoginService, GitHubLoginService>();
        builder.Services.AddSingleton<IGogsLoginService, GogsLoginService>();
        builder.Services.AddSingleton<IRedminePrExtractor, RedminePrExtractor>();
        builder.Services.AddSingleton<IWordTemplateService, WordTemplateService>();
        builder.Services.AddSingleton<IEvidenceOrchestrator, EvidenceOrchestrator>();
        
        builder.Services.AddSingleton<EvidenceApp>();

        using var host = builder.Build();
        var app = host.Services.GetRequiredService<EvidenceApp>();

        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        return await app.RunAsync(options, cts.Token);
    }
}