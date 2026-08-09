using System.Reflection;
using FluentAssertions;
using Octokit;
using Synentra.Infrastructure.Semantic.Providers.InternalBert;

namespace Synentra.Infrastructure.UnitTests.Semantic;

public class GitHubReleaseClientTests
{
    [Fact]
    public void Constructor_Creates_GitHubClient()
    {
        // Act
        var client = new GitHubReleaseClient();

        // Assert - private field _client is initialized
        var field = typeof(GitHubReleaseClient).GetField("_client", BindingFlags.NonPublic | BindingFlags.Instance);
        field.Should().NotBeNull();
        var value = field!.GetValue(client);
        value.Should().NotBeNull().And.BeOfType<GitHubClient>();
    }

    [Fact]
    public async Task GetLatestReleaseAsync_Throws_When_Repository_Not_Found_Or_Network_Issue()
    {
        var client = new GitHubReleaseClient();

        // Use a very likely-nonexistent repo name to trigger a failure from Octokit
        var owner = "this-owner-does-not-exist-for-tests-" + Guid.NewGuid().ToString("N");
        var repo = "nonexistent-repo-" + Guid.NewGuid().ToString("N");

        Func<Task> act = async () => await client.GetLatestReleaseAsync(owner, repo);

        // We don't assert a specific exception type because network or API behavior may vary in CI;
        // it's sufficient that the call propagates an exception (no silent swallow).
        await act.Should().ThrowAsync<Exception>();
    }
}
