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
}
