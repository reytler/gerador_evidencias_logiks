namespace Evidenciador.Core.Models;

public sealed record RedmineIssue(
    string IssueId,
    string Subject,
    string? Description,
    string? ProjectName,
    string? TrackerName,
    string? StatusName,
    string? AssignedToName,
    string Url);