namespace Evidenciador.Core.Requests;

public sealed record PullRequestEvidenceRequest(
    Uri PullRequestUrl,
    GitHubCredentials? GitHubCredentials,
    GogsCredentials? GogsCredentials,
    bool Headless,
    string? DiagnosticsDir,
    string? StorageStatePath = null);
