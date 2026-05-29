namespace Evidenciador.Core.Models;

public sealed record DiffLineEvidence(
    DiffLineKind Kind,
    int? OldLineNumber,
    int? NewLineNumber,
    string Text);
