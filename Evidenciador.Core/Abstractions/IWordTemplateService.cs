using Evidenciador.Core.Models;

namespace Evidenciador.Core.Abstractions;

public interface IWordTemplateService
{
    Task<string> GenerateAsync(RedmineIssueData issueData, string templatePath, string outputDir, CancellationToken ct);
}