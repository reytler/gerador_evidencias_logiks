namespace Evidenciador.Core.Models;

public sealed record PullRequestEvidence(
    string? PullRequestTitle,
    Uri PullRequestUrl,
    Uri FilesUrl,
    DateTimeOffset CollectedAt,
    IReadOnlyList<FileDiffEvidence> Files);
