using Octokit;

namespace Synentra.Infrastructure.Semantic.Providers.InternalBert;

public class GitHubReleaseClient : IGitHubReleaseClient
{
    private readonly GitHubClient _client;

    public GitHubReleaseClient()
    {
        _client = new GitHubClient(new ProductHeaderValue("Synentra"));
    }

    public async Task<Release> GetLatestReleaseAsync(string owner, string repo)
    {
        return await _client.Repository.Release.GetLatest(owner, repo);
    }
}