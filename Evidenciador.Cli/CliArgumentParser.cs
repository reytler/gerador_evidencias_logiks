using Microsoft.Extensions.Configuration;

namespace Evidenciador.Cli;

public static class CliArgumentParser
{
    public static CliOptions Parse(string[] args, IConfiguration? configuration = null)
    {
        string? prUrl = null;
        string? outValue = null;
        string? outDir = null;
        bool? headless = null;
        string? diagnosticsDir = null;
        string? redmineUrl = null;
        string? redmineLogin = null;
        string? redminePassword = null;
        string? redmineQuery = null;
        string? issueId = null;
        string? templatePath = null;

        for (var i = 0; i < args.Length; i++)
        {
            var a = args[i];

            if (a.StartsWith("--pr-url", StringComparison.OrdinalIgnoreCase))
            {
                prUrl = GetValue(args, ref i);
                continue;
            }

            if (a.StartsWith("--out-dir", StringComparison.OrdinalIgnoreCase))
            {
                outDir = GetValue(args, ref i);
                continue;
            }

            if (a.StartsWith("--out", StringComparison.OrdinalIgnoreCase))
            {
                outValue = GetValue(args, ref i);
                continue;
            }

            if (a.StartsWith("--headless", StringComparison.OrdinalIgnoreCase))
            {
                var v = GetValueOrImplicitTrue(args, ref i);
                headless = ParseBool(v, defaultValue: true);
                continue;
            }

            if (a.StartsWith("--diagnostics-dir", StringComparison.OrdinalIgnoreCase))
            {
                diagnosticsDir = GetValue(args, ref i);
                continue;
            }

            if (a.StartsWith("--redmine-url", StringComparison.OrdinalIgnoreCase))
            {
                redmineUrl = GetValue(args, ref i);
                continue;
            }

            if (a.StartsWith("--redmine-login", StringComparison.OrdinalIgnoreCase))
            {
                redmineLogin = GetValue(args, ref i);
                continue;
            }

            if (a.StartsWith("--redmine-password", StringComparison.OrdinalIgnoreCase))
            {
                redminePassword = GetValue(args, ref i);
                continue;
            }

            if (a.StartsWith("--redmine-query", StringComparison.OrdinalIgnoreCase))
            {
                redmineQuery = GetValue(args, ref i);
                continue;
            }

            if (a.StartsWith("--issue-id", StringComparison.OrdinalIgnoreCase))
            {
                issueId = GetValue(args, ref i);
                continue;
            }

            if (a.StartsWith("--template-path", StringComparison.OrdinalIgnoreCase))
            {
                templatePath = GetValue(args, ref i);
                continue;
            }

            if (a == "--help" || a == "-h")
            {
                PrintHelp();
                Environment.Exit(0);
            }
        }

        var mode = DetermineMode(prUrl, redmineUrl, issueId, configuration);

        Uri? prUri = null;
        if (!string.IsNullOrWhiteSpace(prUrl))
        {
            if (!Uri.TryCreate(prUrl, UriKind.Absolute, out prUri))
                throw new ArgumentException("Invalid --pr-url. Expected an absolute URL.");
        }

        return new CliOptions(
            Mode: mode,
            PullRequestUrl: prUri,
            Out: outValue,
            OutDir: outDir,
            Headless: headless,
            DiagnosticsDir: diagnosticsDir,
            RedmineUrl: redmineUrl,
            RedmineLogin: redmineLogin,
            RedminePassword: redminePassword,
            RedmineQuery: redmineQuery,
            IssueId: issueId,
            TemplatePath: templatePath);
    }

    private static OperationMode DetermineMode(string? prUrl, string? redmineUrl, string? issueId, IConfiguration? config)
    {
        if (!string.IsNullOrEmpty(redmineUrl) && !string.IsNullOrEmpty(issueId))
            return OperationMode.RedmineIssue;
        
        if (!string.IsNullOrEmpty(redmineUrl))
            return OperationMode.RedmineQuery;

        if (config != null)
        {
            var defaultMode = config["Evidenciador:Mode"];
            var configuredRedmineUrl = config["Redmine:BaseUrl"];
            
            if (string.Equals(defaultMode, "RedmineQuery", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(configuredRedmineUrl))
                return OperationMode.RedmineQuery;

            if (string.Equals(defaultMode, "RedmineIssue", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(configuredRedmineUrl))
                return OperationMode.RedmineIssue;
        }
        
        return OperationMode.SinglePr;
    }

    private static void PrintHelp()
    {
        Console.WriteLine("""
            Evidenciador - Gerador de Evidências
            
            Usage:
              dotnet run --project Evidenciador.Cli -- [options]

            Options:
              --pr-url <url>              URL do PR do GitHub (modo único)
              --out <path>                Arquivo de saída .docx
              --out-dir <dir>             Diretório de saída
              --headless [true|false]     Executar navegador em modo headless (padrão: true)
              --diagnostics-dir <dir>     Diretório para diagnósticos

              Modo Redmine:
              --redmine-url <url>         URL base do Redmine
              --redmine-login <user>      Login do Redmine
              --redmine-password <pass>   Senha do Redmine
              --redmine-query <query>     Query do Redmine (ex: assigned_to_id=me)
              --issue-id <id>             ID da issue específica
              --template-path <path>      Caminho do template .docx

            Environment Variables:
              GITHUB_USERNAME            Usuário do GitHub
              GITHUB_PASSWORD             Senha do GitHub
              REDMINE_USERNAME            Usuário do Redmine (alternativo)
              REDMINE_PASSWORD           Senha do Redmine (alternativo)
            
            Config (appsettings.json):
              Evidenciador:Mode           Modo default: SinglePr, RedmineQuery, RedmineIssue
              Evidenciador:RedmineQuery   Query default para modo RedmineQuery
            """);
    }

    private static string GetValue(string[] args, ref int i)
    {
        var current = args[i];
        var eq = current.IndexOf('=');
        if (eq >= 0)
            return current[(eq + 1)..];

        if (i + 1 >= args.Length)
            throw new ArgumentException($"Missing value for {current}");

        i++;
        return args[i];
    }

    private static string GetValueOrImplicitTrue(string[] args, ref int i)
    {
        var current = args[i];
        var eq = current.IndexOf('=');
        if (eq >= 0)
            return current[(eq + 1)..];

        if (i + 1 >= args.Length || args[i + 1].StartsWith("--", StringComparison.Ordinal))
            return "true";

        i++;
        return args[i];
    }

    private static bool ParseBool(string? value, bool defaultValue)
    {
        if (string.IsNullOrWhiteSpace(value)) return defaultValue;
        if (bool.TryParse(value, out var b)) return b;
        if (value is "0") return false;
        if (value is "1") return true;
        return defaultValue;
    }
}