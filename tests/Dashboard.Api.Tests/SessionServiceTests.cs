using Dashboard.Api.Pricing;
using Dashboard.Api.Sessions;
using Dashboard.Api.Transcripts;

namespace Dashboard.Api.Tests;

public class SessionServiceTests
{
    private sealed class StubLocator(IReadOnlyList<DiscoveredTranscript> discovered) : ITranscriptLocator
    {
        public string? Locate(string sessionId) => null;

        public IReadOnlyList<DiscoveredTranscript> DiscoverSessions() => discovered;
    }

    private sealed class StubParser(
        Dictionary<string, IReadOnlyList<TranscriptLine>> byPath,
        HashSet<string> unreadablePaths) : ITranscriptParser
    {
        public IReadOnlyList<TranscriptLine> Parse(string filePath)
        {
            if (unreadablePaths.Contains(filePath))
            {
                throw new IOException($"simulated unreadable file: {filePath}");
            }

            return byPath[filePath];
        }
    }

    private static TranscriptLine Line(string sessionId, DateTimeOffset? timestamp, string messageId, string model)
        => new(
            Type: "assistant",
            Cwd: "/home/user/code/sample-project",
            SessionId: sessionId,
            Timestamp: timestamp,
            IsSidechain: false,
            AiTitle: null,
            Message: new AssistantMessage(messageId, model, new Usage(100, 50, 0, 0)));

    [Fact]
    public void GetAllSessions_OrdersByStartedNewestFirst_TimestamplessLast()
    {
        var older = new DiscoveredTranscript("older", "sample-project", "/fixtures/older.jsonl");
        var newer = new DiscoveredTranscript("newer", "sample-project", "/fixtures/newer.jsonl");
        var noTimestamp = new DiscoveredTranscript("no-timestamp", "sample-project", "/fixtures/no-timestamp.jsonl");

        var byPath = new Dictionary<string, IReadOnlyList<TranscriptLine>>
        {
            [older.FilePath] = [Line("older", new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), "m1", "claude-opus-4-8")],
            [newer.FilePath] = [Line("newer", new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero), "m2", "claude-opus-4-8")],
            [noTimestamp.FilePath] = [Line("no-timestamp", null, "m3", "claude-opus-4-8")],
        };

        var service = new SessionService(
            new StubLocator([older, newer, noTimestamp]),
            new StubParser(byPath, []),
            new PriceTable());

        var result = service.GetAllSessions();

        Assert.Equal(3, result.Count);
        Assert.Equal("newer", result[0].SessionId);
        Assert.Equal("older", result[1].SessionId);
        Assert.Equal("no-timestamp", result[2].SessionId);
    }

    [Fact]
    public void GetAllSessions_UnreadableTranscript_ReturnsFailedEntry()
    {
        var good = new DiscoveredTranscript("good", "sample-project", "/fixtures/good.jsonl");
        var bad = new DiscoveredTranscript("bad", "other-project", "/fixtures/bad.jsonl");

        var byPath = new Dictionary<string, IReadOnlyList<TranscriptLine>>
        {
            [good.FilePath] = [Line("good", new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), "m1", "claude-opus-4-8")],
        };

        var service = new SessionService(
            new StubLocator([good, bad]),
            new StubParser(byPath, [bad.FilePath]),
            new PriceTable());

        var result = service.GetAllSessions();

        Assert.Equal(2, result.Count);

        var goodItem = Assert.Single(result, i => i.SessionId == "good");
        Assert.False(goodItem.Failed);
        Assert.NotNull(goodItem.Summary);

        var badItem = Assert.Single(result, i => i.SessionId == "bad");
        Assert.True(badItem.Failed);
        Assert.Null(badItem.Summary);
        Assert.Equal("other-project", badItem.Project);

        // Failed row (no usable timestamp) must sort after the timestamped row.
        Assert.Equal("good", result[0].SessionId);
        Assert.Equal("bad", result[1].SessionId);
    }
}
