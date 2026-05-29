using Evidenciador.Core.Abstractions;
using Evidenciador.Core.Requests;
using Evidenciador.Core.Utils;

namespace Evidenciador.Infra.Playwright;

public interface IPullRequestDiffCollectorFactory
{
    IPullRequestDiffCollector GetCollector(Uri prUrl);
}

public sealed class PullRequestDiffCollectorFactory : IPullRequestDiffCollectorFactory
{
    private readonly GitHubPlaywrightDiffCollector _githubCollector;
    private readonly GogsPlaywrightDiffCollector? _gogsCollector;

    public PullRequestDiffCollectorFactory(
        GitHubPlaywrightDiffCollector githubCollector,
        GogsPlaywrightDiffCollector? gogsCollector = null)
    {
        _githubCollector = githubCollector;
        _gogsCollector = gogsCollector;
    }

    public IPullRequestDiffCollector GetCollector(Uri prUrl)
    {
        if (_gogsCollector != null && GogsPrUrlParser.IsGogsUrl(prUrl))
            return _gogsCollector;

        return _githubCollector;
    }
}