using Evidenciador.Core.Requests;
using Evidenciador.Core.Models;

namespace Evidenciador.Core.Abstractions;

public interface IPullRequestDiffCollector
{
    Task<PullRequestEvidence> CollectAsync(PullRequestEvidenceRequest request, CancellationToken cancellationToken);
}
