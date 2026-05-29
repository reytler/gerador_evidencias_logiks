namespace Evidenciador.Core.Models;

public sealed record FileDiffEvidence(
    string Path,
    string? ChangeType,
    bool IsBinary,
    bool IsTruncated,
    IReadOnlyList<DiffLineEvidence> Lines);
