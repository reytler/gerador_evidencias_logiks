namespace Evidenciador.Core.Models;

public enum DiffLineKind
{
    Context = 0,
    Addition = 1,
    Deletion = 2,
    HunkHeader = 3,
    Placeholder = 4,
}
