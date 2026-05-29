namespace Evidenciador.Infra.Playwright;

public static class RedmineSelectors
{
    public const string UsernameInput = "#username";
    public const string PasswordInput = "#password";
    public const string LoginButton = "input[name=login]";
    public const string IssuesTable = "table.issues";
    public const string IssueLinkInRow = "td.subject a";
    public const string NextPageLink = "a[rel=next]";
    public const string Subject = ".subject h3";
    public const string Project = "#project-jump";
    public const string AssignedTo = ".assigned-to > *:nth-child(2)";
    public const string Tracker = ".list_cf.cf_21.attribute > *:nth-child(2)";
    public const string Description = ".description > *:nth-child(3)";
    public const string Status = "#status_id";
}