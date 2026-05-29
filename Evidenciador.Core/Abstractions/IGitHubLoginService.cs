namespace Evidenciador.Core.Abstractions;

public interface IGitHubLoginService
{
    Task LoginAsync(Requests.GitHubCredentials credentials, bool headless, string? diagnosticsDir, CancellationToken ct);
    Task EnsureLoggedInAsync(Requests.GitHubCredentials credentials, bool headless, string? diagnosticsDir, CancellationToken ct);
    Task<string?> TryExportStorageStateAsync(string? diagnosticsDir, CancellationToken ct);
    bool IsLoggedIn { get; }
}
