using Octokit;

namespace Synentra.Infrastructure.Semantic.Providers.InternalBert;

public interface IGitHubReleaseClient
{
    Task<Release> GetLatestReleaseAsync(string owner, string repo);
}
