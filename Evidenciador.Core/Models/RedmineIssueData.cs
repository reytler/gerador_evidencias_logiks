using Evidenciador.Core.Models;

namespace Evidenciador.Core.Models;

public sealed class RedmineIssueData
{
    public string IssueId { get; init; } = string.Empty;
    public string NomeProjeto { get; init; } = string.Empty;
    public string NomeAtividade { get; init; } = string.Empty;
    public string PerfilDesenvolvedor { get; init; } = string.Empty;
    public string NomeDesenvolvedor { get; init; } = string.Empty;
    public string UrlPagina { get; init; } = string.Empty;
    public string Descricao { get; init; } = string.Empty;
    public string ScreenshotPath { get; init; } = string.Empty;
    public List<string> PrScreenshotPaths { get; init; } = [];
    public List<Uri> PrUrls { get; init; } = [];
    public List<PullRequestEvidence> Evidencias { get; init; } = [];
}

public sealed class DocumentoGerado
{
    public string IssueId { get; set; } = string.Empty;
    public string WordPath { get; set; } = string.Empty;
    public string? PdfPath { get; set; }
    public string ScreenshotPath { get; set; } = string.Empty;
    public List<string> Erros { get; set; } = [];
    public bool Sucesso => Erros.Count == 0;
}