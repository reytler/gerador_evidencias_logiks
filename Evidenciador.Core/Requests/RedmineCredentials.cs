namespace Evidenciador.Core.Requests;

public sealed record RedmineCredentials(string Username, string Password, string? ApiKey = null, string? BaseUrl = null);