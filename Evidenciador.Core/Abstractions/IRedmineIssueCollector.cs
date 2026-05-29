using System.Collections.Generic;

namespace Evidenciador.Core.Abstractions;

public interface IRedmineIssueCollector
{
    Task LoginAsync(Evidenciador.Core.Requests.RedmineCredentials credentials, bool headless, CancellationToken ct);
    Task<Models.RedmineIssue?> CollectAsync(string issueIdOrUrl, CancellationToken ct);
    IAsyncEnumerable<Models.RedmineIssue> StreamIssuesAsync(Models.RedmineQuery query, CancellationToken ct);
    Task<string?> CaptureScreenshotAsync(string issueUrl, string outputDir, CancellationToken ct);
    Task<IReadOnlyList<Uri>> ExtractPrUrlsFromPageAsync(CancellationToken ct);
}