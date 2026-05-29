using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Evidenciador.Core.Abstractions;
using Evidenciador.Core.Models;
using Evidenciador.Core.Requests;
using Evidenciador.Core.Utils;
using Evidenciador.Infra.Playwright;
using Microsoft.Extensions.Logging;

namespace Evidenciador.Cli;

public sealed class EvidenceOrchestrator : IEvidenceOrchestrator
{
    private readonly IRedmineIssueCollector _redmineCollector;
    private readonly IGitHubLoginService _githubLoginService;
    private readonly IRedminePrExtractor _prExtractor;
    private readonly IPullRequestDiffCollectorFactory _collectorFactory;
    private readonly IWordTemplateService _wordTemplateService;
    private readonly ILogger<EvidenceOrchestrator> _logger;
    private readonly IGogsLoginService? _gogsLoginService;

    public EvidenceOrchestrator(
        IRedmineIssueCollector redmineCollector,
        IGitHubLoginService githubLoginService,
        IRedminePrExtractor prExtractor,
        IPullRequestDiffCollectorFactory collectorFactory,
        IWordTemplateService wordTemplateService,
        ILogger<EvidenceOrchestrator> logger,
        IGogsLoginService? gogsLoginService = null)
    {
        _redmineCollector = redmineCollector;
        _githubLoginService = githubLoginService;
        _prExtractor = prExtractor;
        _collectorFactory = collectorFactory;
        _wordTemplateService = wordTemplateService;
        _logger = logger;
        _gogsLoginService = gogsLoginService;
    }

    public async Task<ValidateResult<List<DocumentoGerado>>> ExecuteAsync(OrchestratorRequest request, CancellationToken ct)
    {
        var result = new ValidateResult<List<DocumentoGerado>>();
        var documentos = new List<DocumentoGerado>();

        try
        {
            _logger.LogInformation("=== Iniciando fluxo de evidências ===");
            
            var redmineCredsWithUrl = request.RedmineCredentials with { BaseUrl = request.RedmineUrl };
            
            _logger.LogInformation("1. Fazendo login no Redmine...");
            await _redmineCollector.LoginAsync(redmineCredsWithUrl, request.Headless, ct);
            _logger.LogInformation("   Login no Redmine OK");

            _logger.LogInformation("2. Coletando issues...");
            List<RedmineIssue> issues;

            if (!string.IsNullOrEmpty(request.SpecificIssueId))
            {
                var issue = await _redmineCollector.CollectAsync(request.SpecificIssueId, ct);
                issues = issue != null ? [issue] : [];
            }
            else
            {
                var query = request.Query ?? new RedmineQuery(AssignedToMe: true);
                var issueList = new List<RedmineIssue>();
                await foreach (var i in _redmineCollector.StreamIssuesAsync(query, ct).WithCancellation(ct))
                {
                    issueList.Add(i);
                }
                issues = issueList;
            }

            _logger.LogInformation("   Encontradas {Count} issues", issues.Count);

            foreach (var issue in issues)
            {
                ct.ThrowIfCancellationRequested();
                _logger.LogInformation("Processando issue #{IssueId}: {Subject}", issue.IssueId, issue.Subject);

                var documento = await ProcessIssueAsync(issue, request, ct);
                if (documento != null)
                    documentos.Add(documento);
            }

            result.Data = documentos;
            _logger.LogInformation("=== Fluxo concluído. {Count} documentos gerados ===", documentos.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro durante a execução do fluxo");
            result.AddMessage($"ERRO: {ex.Message}");
        }

        return result;
    }

    private async Task<DocumentoGerado?> ProcessIssueAsync(RedmineIssue issue, OrchestratorRequest request, CancellationToken ct)
    {
        var documento = new DocumentoGerado { IssueId = issue.IssueId };

        try
        {
            _logger.LogInformation("   Capturando screenshot da issue...");
            var screenshotPath = await _redmineCollector.CaptureScreenshotAsync(issue.Url, request.OutputDir, ct);
            _logger.LogInformation("   Screenshot: {Path}", screenshotPath ?? "falhou");

            var prUrlsFromPage = await _redmineCollector.ExtractPrUrlsFromPageAsync(ct);
            var prUrlsFromDescription = _prExtractor.ExtractPrUrlsFromDescription(issue.Description ?? "");

            var prUrls = BuildOrderedPrUrlList(prUrlsFromPage, prUrlsFromDescription);
            var evidencias = new List<PullRequestEvidence>();

            if (prUrls.Count > 0)
            {
                _logger.LogInformation("   Encontrados {Count} PRs (page+description)", prUrls.Count);

                var githubOk = true;
                string? storageStatePath = null;
                try
                {
                    _logger.LogInformation("   Fazendo login no GitHub...");
                    await _githubLoginService.EnsureLoggedInAsync(request.GitHubCredentials, request.Headless, request.DiagnosticsDir, ct);
                    _logger.LogInformation("   Login GitHub OK");

                    storageStatePath = await _githubLoginService.TryExportStorageStateAsync(request.DiagnosticsDir, ct);
                    if (!string.IsNullOrWhiteSpace(storageStatePath))
                        _logger.LogInformation("   GitHub storage state pronto para reuse: {Path}", storageStatePath);
                }
                catch (Exception ex)
                {
                    githubOk = false;
                    _logger.LogWarning(ex, "Falha ao autenticar no GitHub (issue #{IssueId})", issue.IssueId);
                    documento.Erros.Add($"GitHub login: {ex.Message}");
                }

                foreach (var prUrl in prUrls)
                {
                    ct.ThrowIfCancellationRequested();

                    if (!githubOk)
                        continue;

                    try
                    {
                        _logger.LogInformation("   Coletando diff do PR: {Url}", prUrl);

                        var diffCollector = _collectorFactory.GetCollector(prUrl);

                        var diffRequest = new PullRequestEvidenceRequest(
                            PullRequestUrl: prUrl,
                            GitHubCredentials: request.GitHubCredentials,
                            GogsCredentials: request.GogsCredentials,
                            Headless: request.Headless,
                            DiagnosticsDir: request.DiagnosticsDir,
                            StorageStatePath: storageStatePath);

                        var evidencia = await diffCollector.CollectAsync(diffRequest, ct);
                        evidencias.Add(evidencia);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Falha ao coletar evidencias do PR {Url} (issue #{IssueId})", prUrl, issue.IssueId);
                        documento.Erros.Add($"PR {prUrl}: {ex.Message}");
                    }
                }
            }
            else
            {
                _logger.LogInformation("   Issue sem PRs, gerando documento básico");
            }

            var issueData = new RedmineIssueData
            {
                IssueId = issue.IssueId,
                NomeProjeto = issue.ProjectName ?? "",
                NomeAtividade = issue.Subject,
                PerfilDesenvolvedor = issue.TrackerName ?? "",
                NomeDesenvolvedor = issue.AssignedToName ?? "",
                UrlPagina = issue.Url,
                Descricao = issue.Description ?? "",
                PrUrls = prUrls,
                Evidencias = evidencias,
                ScreenshotPath = screenshotPath ?? ""
            };

            var wordPath = await _wordTemplateService.GenerateAsync(
                issueData,
                request.TemplatePath,
                request.OutputDir,
                ct);

            documento.WordPath = wordPath;

            _logger.LogInformation("   Issue #{IssueId} concluída", issue.IssueId);
            return documento;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao processar issue #{IssueId}", issue.IssueId);
            documento.Erros.Add(ex.Message);
            return documento;
        }
    }

    private static List<Uri> BuildOrderedPrUrlList(IEnumerable<Uri> pageUrls, IEnumerable<Uri> descriptionUrls)
    {
        var unique = new Dictionary<string, Uri>(StringComparer.OrdinalIgnoreCase);

        static string KeyFor(Uri u) => GitHubPrUrlParser.NormalizeToPullRoot(u).AbsoluteUri.TrimEnd('/');

        foreach (var u in pageUrls)
        {
            unique[KeyFor(u)] = GitHubPrUrlParser.NormalizeToPullRoot(u);
        }

        foreach (var u in descriptionUrls)
        {
            unique[KeyFor(u)] = GitHubPrUrlParser.NormalizeToPullRoot(u);
        }

        var list = unique.Values.ToList();
        list.Sort(GitHubPrUriComparer.Instance);
        return list;
    }

    private sealed class GitHubPrUriComparer : IComparer<Uri>
    {
        public static readonly GitHubPrUriComparer Instance = new();

        public int Compare(Uri? x, Uri? y)
        {
            if (ReferenceEquals(x, y)) return 0;
            if (x is null) return -1;
            if (y is null) return 1;

            var xParsed = GitHubPrUrlParser.TryParse(x, out var xInfo);
            var yParsed = GitHubPrUrlParser.TryParse(y, out var yInfo);

            if (xParsed && yParsed)
            {
                var c = StringComparer.OrdinalIgnoreCase.Compare(xInfo.Owner, yInfo.Owner);
                if (c != 0) return c;

                c = StringComparer.OrdinalIgnoreCase.Compare(xInfo.Repo, yInfo.Repo);
                if (c != 0) return c;

                c = xInfo.Number.CompareTo(yInfo.Number);
                if (c != 0) return c;

                return StringComparer.Ordinal.Compare(x.AbsoluteUri, y.AbsoluteUri);
            }

            if (xParsed != yParsed)
                return xParsed ? -1 : 1;

            return StringComparer.Ordinal.Compare(x.AbsoluteUri, y.AbsoluteUri);
        }
    }
}
