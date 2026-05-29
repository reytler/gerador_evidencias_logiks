using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Evidenciador.Core.Models;
using Evidenciador.Core.Requests;

namespace Evidenciador.Core.Abstractions;

public interface IEvidenceOrchestrator
{
    Task<ValidateResult<List<DocumentoGerado>>> ExecuteAsync(
        OrchestratorRequest request, 
        CancellationToken ct);
}

public sealed record OrchestratorRequest(
    RedmineCredentials RedmineCredentials,
    GitHubCredentials GitHubCredentials,
    GogsCredentials? GogsCredentials,
    string RedmineUrl,
    string TemplatePath,
    string OutputDir,
    bool Headless = true,
    bool GeneratePdf = false,
    string? DiagnosticsDir = null,
    RedmineQuery? Query = null,
    string? SpecificIssueId = null);

public class ValidateResult<T>
{
    public bool Success => Data != null;
    public List<string> Messages { get; } = [];
    public T? Data { get; set; }
    
    public void AddMessage(string msg) => Messages.Add(msg);
}