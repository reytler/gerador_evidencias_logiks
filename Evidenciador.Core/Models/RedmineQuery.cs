namespace Evidenciador.Core.Models;

public sealed record RedmineQuery(
    string? ProjectId = null,
    string? TrackerId = null,
    string? StatusId = null,
    string? AssignedToId = null,
    bool AssignedToMe = false);