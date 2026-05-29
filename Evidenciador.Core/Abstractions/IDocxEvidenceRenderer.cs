using Evidenciador.Core.Models;

namespace Evidenciador.Core.Abstractions;

public interface IDocxEvidenceRenderer
{
    Task RenderAsync(PullRequestEvidence evidence, string outputPath, CancellationToken cancellationToken);
}
