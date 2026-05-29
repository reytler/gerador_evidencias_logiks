using Evidenciador.Core.Requests;

namespace Evidenciador.Core.Abstractions;

public interface IGogsLoginService
{
    bool IsLoggedIn { get; }
    Task LoginAsync(GogsCredentials credentials, bool headless, string? diagnosticsDir, CancellationToken ct);
    Task EnsureLoggedInAsync(GogsCredentials credentials, bool headless, string? diagnosticsDir, CancellationToken ct);
}