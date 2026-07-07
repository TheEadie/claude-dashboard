using Dashboard.Api.Transcripts;
using Microsoft.Extensions.Configuration;

namespace Dashboard.Api.Tests;

public class TranscriptLocatorTests
{
    private static ITranscriptLocator CreateLocator()
    {
        var claudeRoot = Path.Combine(AppContext.BaseDirectory, "Fixtures");
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ClaudeDashboard:ClaudeRoot"] = claudeRoot,
            })
            .Build();
        return new TranscriptLocator(config);
    }

    [Fact]
    public void Locate_KnownSessionId_ReturnsPathUnderProjects()
    {
        var locator = CreateLocator();

        var path = locator.Locate("valid-single-model");

        Assert.NotNull(path);
        Assert.True(File.Exists(path));
        Assert.EndsWith("valid-single-model.jsonl", path);
    }

    [Fact]
    public void Locate_UnknownSessionId_ReturnsNull()
    {
        var locator = CreateLocator();

        var path = locator.Locate("no-such-session");

        Assert.Null(path);
    }

    [Theory]
    [InlineData("../secrets")]
    [InlineData("foo/bar")]
    [InlineData("foo\\bar")]
    public void Locate_PathTraversalAttempt_ReturnsNull(string sessionId)
    {
        var locator = CreateLocator();

        var path = locator.Locate(sessionId);

        Assert.Null(path);
    }

    [Fact]
    public void DiscoverSessions_ReturnsTopLevelSessionsExcludingSubagents()
    {
        var locator = CreateLocator();

        var discovered = locator.DiscoverSessions();
        var ids = discovered.Select(d => d.SessionId).ToList();

        Assert.Contains("valid-single-model", ids);
        Assert.Contains("multi-model", ids);
        Assert.Contains("second-project-session", ids);
        Assert.DoesNotContain("agent-1", ids);

        foreach (var d in discovered)
        {
            Assert.EndsWith(".jsonl", d.FilePath);
            Assert.True(File.Exists(d.FilePath));
        }
    }

    [Fact]
    public void DiscoverSessions_MissingProjectsDir_ReturnsEmpty()
    {
        var emptyRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(emptyRoot);
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ClaudeDashboard:ClaudeRoot"] = emptyRoot,
            })
            .Build();
        var locator = new TranscriptLocator(config);

        var discovered = locator.DiscoverSessions();

        Assert.Empty(discovered);
    }

    [Fact]
    public void DiscoverSubAgents_SessionWithSubAgents_ReturnsOrderedEntriesWithMetaPaths()
    {
        var locator = CreateLocator();
        var sessionPath = locator.Locate("sub-parent");

        var discovered = locator.DiscoverSubAgents(sessionPath!);

        Assert.Equal(2, discovered.Count);
        Assert.Equal(["agent-1", "agent-2"], discovered.Select(d => d.AgentId));

        var agent1 = discovered.Single(d => d.AgentId == "agent-1");
        Assert.NotNull(agent1.MetaPath);
        Assert.True(File.Exists(agent1.MetaPath));
        Assert.True(File.Exists(agent1.TranscriptPath));

        var agent2 = discovered.Single(d => d.AgentId == "agent-2");
        Assert.Null(agent2.MetaPath);
        Assert.True(File.Exists(agent2.TranscriptPath));
    }

    [Fact]
    public void DiscoverSubAgents_SessionWithoutSubAgentsDir_ReturnsEmpty()
    {
        var locator = CreateLocator();
        var sessionPath = locator.Locate("valid-single-model");

        var discovered = locator.DiscoverSubAgents(sessionPath!);

        Assert.Empty(discovered);
    }
}
