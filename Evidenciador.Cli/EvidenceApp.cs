using Evidenciador.Core.Abstractions;
using Evidenciador.Core.Models;
using Evidenciador.Core.Requests;
using Evidenciador.Core.Utils;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Text;

namespace Evidenciador.Cli;

public sealed class EvidenceApp(
    IConfiguration configuration,
    IPullRequestDiffCollector collector,
    IDocxEvidenceRenderer renderer,
    IEvidenceOrchestrator orchestrator,
    ILogger<EvidenceApp> logger)
{
    private readonly IConfiguration _configuration = configuration;
    private readonly IPullRequestDiffCollector _collector = collector;
    private readonly IDocxEvidenceRenderer _renderer = renderer;
    private readonly IEvidenceOrchestrator _orchestrator = orchestrator;
    private readonly ILogger<EvidenceApp> _logger = logger;

    public async Task<int> RunAsync(CliOptions options, CancellationToken cancellationToken)
    {
        return options.Mode switch
        {
            OperationMode.SinglePr => await RunSinglePrAsync(options, cancellationToken),
            OperationMode.RedmineQuery or OperationMode.RedmineIssue => await RunRedmineAsync(options, cancellationToken),
            _ => throw new ArgumentException($"Unknown operation mode: {options.Mode}")
        };
    }

    private async Task<int> RunSinglePrAsync(CliOptions options, CancellationToken cancellationToken)
    {
        var username = _configuration["GITHUB_USERNAME"];
        var password = _configuration["GITHUB_PASSWORD"];

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            _logger.LogError("Missing GitHub credentials. Provide GITHUB_USERNAME and GITHUB_PASSWORD via environment variables or (dev) user-secrets.");
            return 2;
        }

        var prUrl = options.PullRequestUrl ?? TryGetUri(_configuration["Evidenciador:PrUrl"]);
        if (prUrl is null)
        {
            _logger.LogError("Missing PR URL. Provide --pr-url or configure Evidenciador:PrUrl.");
            return 1;
        }

        var headless = options.Headless
            ?? TryParseBool(_configuration["Evidenciador:Headless"])
            ?? true;

        var diagnosticsDir = options.DiagnosticsDir ?? _configuration["Evidenciador:DiagnosticsDir"];

        var outDir = FirstNonEmpty(options.OutDir, _configuration["Evidenciador:OutDir"]);
        var outValue = FirstNonEmpty(options.Out, _configuration["Evidenciador:Out"]);

        if (!string.IsNullOrWhiteSpace(outDir) && !string.IsNullOrWhiteSpace(outValue))
        {
            _logger.LogWarning("Both out-dir and out are set; out-dir will be used.");
        }

        OutputTarget? outputTarget;
        try
        {
            outputTarget = DetermineOutputTarget(outDir, outValue);
        }
        catch (ArgumentException ex)
        {
            _logger.LogError(ex.Message);
            return 1;
        }
        if (outputTarget is null)
        {
            _logger.LogError("Missing output target. Provide --out / --out-dir or configure Evidenciador:Out/Evidenciador:OutDir.");
            return 1;
        }

        _logger.LogInformation("Collecting evidence from PR: {PrUrl}", prUrl);
        _logger.LogInformation("Headless: {Headless}", headless);
        if (!string.IsNullOrWhiteSpace(diagnosticsDir))
            _logger.LogInformation("DiagnosticsDir: {DiagnosticsDir}", diagnosticsDir);

        var request = new PullRequestEvidenceRequest(
            PullRequestUrl: prUrl,
            GitHubCredentials: new GitHubCredentials(username, password),
            GogsCredentials: null,
            Headless: headless,
            DiagnosticsDir: diagnosticsDir);

        var evidence = await _collector.CollectAsync(request, cancellationToken);

        var outputPath = outputTarget.ResolveOutputPath(evidence);
        _logger.LogInformation("Writing DOCX: {Out}", outputPath);
        await _renderer.RenderAsync(evidence, outputPath, cancellationToken);

        _logger.LogInformation("Done");
        return 0;
    }

    private async Task<int> RunRedmineAsync(CliOptions options, CancellationToken cancellationToken)
    {
        var githubUsername = _configuration["GITHUB_USERNAME"];
        var githubPassword = _configuration["GITHUB_PASSWORD"];

        var redmineUsername = options.RedmineLogin 
            ?? _configuration["REDMINE_USERNAME"] 
            ?? _configuration["Redmine:Username"];
        var redminePassword = options.RedminePassword 
            ?? _configuration["REDMINE_PASSWORD"] 
            ?? _configuration["Redmine:Password"];

        GogsCredentials? gogsCreds = null;
        var gogsUrl = _configuration["Gogs:BaseUrl"];
        var gogsUsername = _configuration["Gogs:Username"];
        var gogsPassword = _configuration["Gogs:Password"];

        if (!string.IsNullOrWhiteSpace(gogsUrl) && !string.IsNullOrWhiteSpace(gogsUsername))
        {
            gogsCreds = new GogsCredentials(gogsUsername, gogsPassword ?? "", gogsUrl);
        }

        if (string.IsNullOrWhiteSpace(githubUsername) || string.IsNullOrWhiteSpace(githubPassword))
        {
            if (gogsCreds == null)
            {
                _logger.LogError("Missing GitHub or Gogs credentials. Provide at least one.");
                return 2;
            }
            _logger.LogInformation("GitHub credentials not provided, will use Gogs only");
            githubUsername = "";
            githubPassword = "";
        }

        if (string.IsNullOrWhiteSpace(redmineUsername) || string.IsNullOrWhiteSpace(redminePassword))
        {
            _logger.LogError("Missing Redmine credentials. Provide --redmine-login and --redmine-password or configure via environment variables.");
            return 2;
        }

        var redmineUrl = options.RedmineUrl ?? _configuration["Redmine:BaseUrl"];
        if (string.IsNullOrWhiteSpace(redmineUrl))
        {
            _logger.LogError("Missing Redmine URL. Provide --redmine-url or configure Redmine:BaseUrl.");
            return 1;
        }

        var templatePath = options.TemplatePath ?? _configuration["Evidenciador:TemplatePath"] ?? "TEMPLATE_EVIDENCIA.docx";
        if (!File.Exists(templatePath))
        {
            _logger.LogError("Template not found: {Path}", templatePath);
            return 1;
        }

        var outputDir = options.OutDir ?? _configuration["Evidenciador:OutDir"] ?? ".\\out";
        var headless = options.Headless ?? TryParseBool(_configuration["Evidenciador:Headless"]) ?? true;
        var diagnosticsDir = options.DiagnosticsDir ?? _configuration["Evidenciador:DiagnosticsDir"];

        _logger.LogInformation("=== Modo Redmine ===");
        _logger.LogInformation("Redmine URL: {Url}", redmineUrl);
        _logger.LogInformation("Template: {Path}", templatePath);
        _logger.LogInformation("Output: {Dir}", outputDir);
        _logger.LogInformation("Headless: {Headless}", headless);
        _logger.LogInformation("DiagnosticsDir: {Dir}", diagnosticsDir ?? "none");

        RedmineQuery? query = null;
        if (!string.IsNullOrEmpty(options.RedmineQuery))
            query = ParseRedmineQuery(options.RedmineQuery);
        else if (!string.IsNullOrEmpty(_configuration["Evidenciador:RedmineQuery"]))
            query = ParseRedmineQuery(_configuration["Evidenciador:RedmineQuery"]);

        if (query == null && string.IsNullOrEmpty(options.IssueId) && string.IsNullOrEmpty(_configuration["Evidenciador:IssueId"]))
        {
            _logger.LogError("Missing Redmine query or issue ID. Configure Evidenciador:RedmineQuery or Evidenciador:IssueId.");
            return 1;
        }

        var specificIssueId = options.IssueId ?? _configuration["Evidenciador:IssueId"];

        var request = new OrchestratorRequest(
            RedmineCredentials: new RedmineCredentials(redmineUsername, redminePassword),
            GitHubCredentials: new GitHubCredentials(githubUsername, githubPassword),
            GogsCredentials: gogsCreds,
            RedmineUrl: redmineUrl,
            TemplatePath: templatePath,
            OutputDir: outputDir,
            Headless: headless,
            GeneratePdf: false,
            DiagnosticsDir: diagnosticsDir,
            Query: query,
            SpecificIssueId: specificIssueId);

        var result = await _orchestrator.ExecuteAsync(request, cancellationToken);

        if (result.Data?.Any() == true)
        {
            _logger.LogInformation("{Count} documento(s) gerado(s)", result.Data.Count);
            foreach (var doc in result.Data)
            {
                _logger.LogInformation("  Issue #{IssueId}: {Path}", doc.IssueId, doc.WordPath);
                if (doc.Erros.Any())
                {
                    _logger.LogWarning("  Erros: {Erros}", string.Join("; ", doc.Erros));
                }
            }
        }
        else
        {
            _logger.LogWarning("Nenhum documento gerado");
        }

        return result.Success ? 0 : 1;
    }

    private static RedmineQuery ParseRedmineQuery(string queryString)
    {
        var query = new RedmineQuery();
        
        var parts = queryString.Split('&');
        foreach (var part in parts)
        {
            var kv = part.Split('=');
            if (kv.Length != 2) continue;

            var key = kv[0].Trim();
            var value = kv[1].Trim();

            query = key switch
            {
                "project_id" => query with { ProjectId = value },
                "tracker_id" => query with { TrackerId = value },
                "status_id" => query with { StatusId = value },
                "assigned_to_id" when value == "me" => query with { AssignedToMe = true },
                "assigned_to_id" => query with { AssignedToId = value },
                _ => query
            };
        }

        return query;
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        foreach (var v in values)
        {
            if (!string.IsNullOrWhiteSpace(v)) return v;
        }
        return null;
    }

    private static Uri? TryGetUri(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return Uri.TryCreate(value, UriKind.Absolute, out var u) ? u : null;
    }

    private static bool? TryParseBool(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (bool.TryParse(value, out var b)) return b;
        if (value is "0") return false;
        if (value is "1") return true;
        return null;
    }

    private static OutputTarget? DetermineOutputTarget(string? outDir, string? outValue)
    {
        if (!string.IsNullOrWhiteSpace(outDir))
            return new OutputTarget(directory: outDir, explicitFilePath: null);

        if (string.IsNullOrWhiteSpace(outValue))
            return null;

        if (Path.EndsInDirectorySeparator(outValue) || Directory.Exists(outValue))
            return new OutputTarget(directory: outValue, explicitFilePath: null);

        if (!Path.HasExtension(outValue))
            outValue += ".docx";

        if (!string.Equals(Path.GetExtension(outValue), ".docx", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("--out must be a .docx file path, or a directory path ending with a separator.");

        return new OutputTarget(directory: null, explicitFilePath: outValue);
    }

    private sealed class OutputTarget
    {
        private readonly string? _directory;
        private readonly string? _explicitFilePath;

        public OutputTarget(string? directory, string? explicitFilePath)
        {
            _directory = string.IsNullOrWhiteSpace(directory) ? null : directory;
            _explicitFilePath = string.IsNullOrWhiteSpace(explicitFilePath) ? null : explicitFilePath;
        }

        public string ResolveOutputPath(Evidenciador.Core.Models.PullRequestEvidence evidence)
        {
            if (!string.IsNullOrWhiteSpace(_explicitFilePath))
                return _explicitFilePath!;

            var dir = _directory ?? ".";
            Directory.CreateDirectory(dir);

            var fileName = BuildEvidenceFileName(evidence);
            var path = Path.Combine(dir, fileName);
            return EnsureUnique(path);
        }

        private static string EnsureUnique(string path)
        {
            if (!File.Exists(path)) return path;

            var dir = Path.GetDirectoryName(path) ?? ".";
            var name = Path.GetFileNameWithoutExtension(path);
            var ext = Path.GetExtension(path);

            for (var i = 2; i < 10_000; i++)
            {
                var candidate = Path.Combine(dir, $"{name}-{i}{ext}");
                if (!File.Exists(candidate)) return candidate;
            }

            throw new IOException("Unable to find a unique output filename.");
        }

        private static string BuildEvidenceFileName(Evidenciador.Core.Models.PullRequestEvidence evidence)
        {
            var stamp = evidence.CollectedAt.ToUniversalTime().ToString("yyyyMMdd-HHmmss'Z'", CultureInfo.InvariantCulture);

            string prefix;
            if (GitHubPrUrlParser.TryParse(evidence.PullRequestUrl, out var info))
            {
                prefix = $"{info.Owner}_{info.Repo}_pr{info.Number}_{stamp}";
            }
            else
            {
                prefix = $"pull-request_{stamp}";
            }

            var slug = Slugify(evidence.PullRequestTitle);
            return $"{prefix}_{slug}.docx";
        }

        private static string Slugify(string? title)
        {
            if (string.IsNullOrWhiteSpace(title)) return "untitled";

            var s = title.Trim();
            s = s.Normalize(NormalizationForm.FormD);

            var sb = new StringBuilder(capacity: s.Length);
            foreach (var ch in s)
            {
                var cat = CharUnicodeInfo.GetUnicodeCategory(ch);
                if (cat == UnicodeCategory.NonSpacingMark) continue;

                var c = char.ToLowerInvariant(ch);
                if ((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9'))
                {
                    sb.Append(c);
                    continue;
                }

                sb.Append('-');
            }

            var slug = sb.ToString();
            while (slug.Contains("--", StringComparison.Ordinal)) slug = slug.Replace("--", "-", StringComparison.Ordinal);
            slug = slug.Trim('-').TrimEnd('.', ' ');
            if (slug.Length == 0) slug = "untitled";

            var reserved = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "con","prn","aux","nul",
                "com1","com2","com3","com4","com5","com6","com7","com8","com9",
                "lpt1","lpt2","lpt3","lpt4","lpt5","lpt6","lpt7","lpt8","lpt9",
            };
            if (reserved.Contains(slug)) slug = "pr-" + slug;

            const int max = 100;
            if (slug.Length > max) slug = slug[..max].TrimEnd('-');

            return slug;
        }
    }
}