namespace Evidenciador.Core.Requests;

public sealed record GogsCredentials(
    string Username, 
    string Password,
    string BaseUrl);