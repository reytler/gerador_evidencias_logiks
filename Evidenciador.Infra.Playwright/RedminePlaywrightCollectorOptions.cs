namespace Evidenciador.Infra.Playwright;

public sealed class RedminePlaywrightCollectorOptions
{
    public string BaseUrl { get; init; } = string.Empty;
    public string LoginUrl { get; init; } = "/login";
    public string IssuesUrl { get; init; } = "/issues";
    public bool Headless { get; init; } = true;
    public int Timeout { get; init; } = 30000;
    public int SlowMo { get; init; } = 0;
    public int RetryCount { get; init; } = 3;
    public int RetryDelayMs { get; init; } = 2000;
}