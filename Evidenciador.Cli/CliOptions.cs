namespace Evidenciador.Cli;

public sealed record CliOptions(
    OperationMode Mode,
    Uri? PullRequestUrl,
    string? Out,
    string? OutDir,
    bool? Headless,
    string? DiagnosticsDir,
    string? RedmineUrl,
    string? RedmineLogin,
    string? RedminePassword,
    string? RedmineQuery,
    string? IssueId,
    string? TemplatePath)
{
    public static CliOptions Default => new(
        Mode: OperationMode.SinglePr,
        PullRequestUrl: null,
        Out: null,
        OutDir: null,
        Headless: null,
        DiagnosticsDir: null,
        RedmineUrl: null,
        RedmineLogin: null,
        RedminePassword: null,
        RedmineQuery: null,
        IssueId: null,
        TemplatePath: null);
}

public enum OperationMode
{
    SinglePr,
    RedmineQuery,
    RedmineIssue
}