using Dashboard.Api.Pricing;
using Dashboard.Api.Sessions;
using Dashboard.Api.Transcripts;

namespace Dashboard.Api.Tests;

public class SessionServiceTests
{
    private sealed class StubLocator(
        IReadOnlyList<DiscoveredTranscript> discovered,
        Dictionary<string, string?>? locateByIdOverride = null,
        Dictionary<string, IReadOnlyList<DiscoveredSubAgent>>? subAgentsByPath = null) : ITranscriptLocator
    {
        public string? Locate(string sessionId) =>
            locateByIdOverride is not null && locateByIdOverride.TryGetValue(sessionId, out var path) ? path : null;

        public IReadOnlyList<DiscoveredTranscript> DiscoverSessions() => discovered;

        public IReadOnlyList<DiscoveredSubAgent> DiscoverSubAgents(string sessionFilePath) =>
            subAgentsByPath is not null && subAgentsByPath.TryGetValue(sessionFilePath, out var subs)
                ? subs
                : Array.Empty<DiscoveredSubAgent>();
    }

    private sealed class StubParser(
        Dictionary<string, IReadOnlyList<TranscriptLine>> byPath,
        Dictionary<string, SubAgentMeta?>? metaByPath = null) : ITranscriptParser
    {
        public IReadOnlyList<TranscriptLine> Parse(string filePath) =>
            byPath.TryGetValue(filePath, out var lines)
                ? lines
                : throw new IOException($"simulated unreadable file: {filePath}");

        public SubAgentMeta? ParseMeta(string? path) =>
            path is not null && metaByPath is not null && metaByPath.TryGetValue(path, out var meta) ? meta : null;
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
            new StubParser(byPath),
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
            new StubParser(byPath),
            new PriceTable());

        var result = service.GetAllSessions();

        Assert.Equal(2, result.Count);

        var goodItem = Assert.Single(result, i => i.SessionId == "good");
        Assert.False(goodItem.Failed);
        Assert.NotNull(goodItem.Summary);
        Assert.NotNull(goodItem.Combined);

        var badItem = Assert.Single(result, i => i.SessionId == "bad");
        Assert.True(badItem.Failed);
        Assert.Null(badItem.Summary);
        Assert.Null(badItem.Combined);
        Assert.Equal("other-project", badItem.Project);

        // Failed row (no usable timestamp) must sort after the timestamped row.
        Assert.Equal("good", result[0].SessionId);
        Assert.Equal("bad", result[1].SessionId);
    }

    [Fact]
    public void GetTrace_SessionWithTwoSubAgents_CombinesMainAndSubs()
    {
        const string mainPath = "/fixtures/main.jsonl";
        const string sub1Path = "/fixtures/agent-1.jsonl";
        const string sub2Path = "/fixtures/agent-2.jsonl";

        var byPath = new Dictionary<string, IReadOnlyList<TranscriptLine>>
        {
            [mainPath] = [Line("main", new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), "m1", "claude-opus-4-8")],
            [sub1Path] = [Line("agent-1", new DateTimeOffset(2026, 1, 1, 0, 1, 0, TimeSpan.Zero), "s1", "claude-opus-4-8")],
            [sub2Path] = [Line("agent-2", new DateTimeOffset(2026, 1, 1, 0, 2, 0, TimeSpan.Zero), "s2", "claude-sonnet-4-6")],
        };
        var subAgentsByPath = new Dictionary<string, IReadOnlyList<DiscoveredSubAgent>>
        {
            [mainPath] =
            [
                new DiscoveredSubAgent("agent-1", sub1Path, "/fixtures/agent-1.meta.json"),
                new DiscoveredSubAgent("agent-2", sub2Path, null),
            ],
        };
        var metaByPath = new Dictionary<string, SubAgentMeta?>
        {
            ["/fixtures/agent-1.meta.json"] = new SubAgentMeta("code-reviewer"),
        };

        var service = new SessionService(
            new StubLocator([], new Dictionary<string, string?> { ["main"] = mainPath }, subAgentsByPath),
            new StubParser(byPath, metaByPath),
            new PriceTable());

        var trace = service.GetTrace("main");

        Assert.NotNull(trace);
        Assert.Equal("main", trace.Session.SessionId);
        Assert.Equal(2, trace.SubAgents.Count);

        var sub1 = trace.SubAgents.Single(s => s.AgentId == "agent-1");
        Assert.Equal("code-reviewer", sub1.Role);
        var sub2 = trace.SubAgents.Single(s => s.AgentId == "agent-2");
        Assert.Equal("agent-2", sub2.Role);

        Assert.Equal(
            trace.Session.Tokens.Total + sub1.Tokens!.Total + sub2.Tokens!.Total,
            trace.Combined.Tokens.Total);
        Assert.Equal(
            trace.Session.CostUsd + sub1.CostUsd! + sub2.CostUsd!,
            trace.Combined.CostUsd);
    }

    [Fact]
    public void GetTrace_SessionWithNoSubAgents_CombinedEqualsMain()
    {
        const string mainPath = "/fixtures/main.jsonl";
        var byPath = new Dictionary<string, IReadOnlyList<TranscriptLine>>
        {
            [mainPath] = [Line("main", new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), "m1", "claude-opus-4-8")],
        };

        var service = new SessionService(
            new StubLocator([], new Dictionary<string, string?> { ["main"] = mainPath }),
            new StubParser(byPath),
            new PriceTable());

        var trace = service.GetTrace("main");

        Assert.NotNull(trace);
        Assert.Empty(trace.SubAgents);
        Assert.Equal(trace.Session.Tokens.Total, trace.Combined.Tokens.Total);
        Assert.Equal(trace.Session.CostUsd, trace.Combined.CostUsd);
        Assert.Equal(trace.Session.Models, trace.Combined.Models);
    }

    [Fact]
    public void GetTrace_SubAgentUsesUnpricedModel_CombinedUnpricedModelsIncludesItAndSpanExposesDuration()
    {
        const string mainPath = "/fixtures/main.jsonl";
        const string subPath = "/fixtures/agent-1.jsonl";

        var byPath = new Dictionary<string, IReadOnlyList<TranscriptLine>>
        {
            [mainPath] = [Line("main", new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), "m1", "claude-opus-4-8")],
            [subPath] =
            [
                Line("agent-1", new DateTimeOffset(2026, 1, 1, 0, 1, 0, TimeSpan.Zero), "s1", "claude-experimental-x"),
                Line("agent-1", new DateTimeOffset(2026, 1, 1, 0, 1, 5, TimeSpan.Zero), "s2", "claude-experimental-x"),
            ],
        };
        var subAgentsByPath = new Dictionary<string, IReadOnlyList<DiscoveredSubAgent>>
        {
            [mainPath] = [new DiscoveredSubAgent("agent-1", subPath, null)],
        };

        var service = new SessionService(
            new StubLocator([], new Dictionary<string, string?> { ["main"] = mainPath }, subAgentsByPath),
            new StubParser(byPath),
            new PriceTable());

        var trace = service.GetTrace("main");

        Assert.NotNull(trace);
        var sub = Assert.Single(trace.SubAgents);
        Assert.Equal(["claude-experimental-x"], sub.UnpricedModels);
        Assert.Equal(5000, sub.DurationMs);

        // The main session has no unpriced model of its own — only the sub-agent
        // does — yet Combined must still union it in.
        Assert.Empty(trace.Session.UnpricedModels);
        Assert.Contains("claude-experimental-x", trace.Combined.UnpricedModels);
    }

    [Fact]
    public void GetTrace_UnreadableSubAgentTranscript_IsFailedAndExcludedFromCombined()
    {
        const string mainPath = "/fixtures/main.jsonl";
        const string subPath = "/fixtures/agent-1.jsonl";

        var byPath = new Dictionary<string, IReadOnlyList<TranscriptLine>>
        {
            [mainPath] = [Line("main", new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), "m1", "claude-opus-4-8")],
        };
        var subAgentsByPath = new Dictionary<string, IReadOnlyList<DiscoveredSubAgent>>
        {
            [mainPath] = [new DiscoveredSubAgent("agent-1", subPath, null)],
        };

        var service = new SessionService(
            new StubLocator([], new Dictionary<string, string?> { ["main"] = mainPath }, subAgentsByPath),
            new StubParser(byPath),
            new PriceTable());

        var trace = service.GetTrace("main");

        Assert.NotNull(trace);
        var failedSpan = Assert.Single(trace.SubAgents);
        Assert.True(failedSpan.Failed);
        Assert.Null(failedSpan.Tokens);
        Assert.Null(failedSpan.CostUsd);
        Assert.Equal("agent-1", failedSpan.Role);

        // Excluded from combined -> combined equals main-only.
        Assert.Equal(trace.Session.Tokens.Total, trace.Combined.Tokens.Total);
        Assert.Equal(trace.Session.CostUsd, trace.Combined.CostUsd);
    }

    [Fact]
    public void GetTrace_SubAgentMissingMeta_RoleFallsBackToAgentId()
    {
        const string mainPath = "/fixtures/main.jsonl";
        const string subPath = "/fixtures/agent-1.jsonl";

        var byPath = new Dictionary<string, IReadOnlyList<TranscriptLine>>
        {
            [mainPath] = [Line("main", new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), "m1", "claude-opus-4-8")],
            [subPath] = [Line("agent-1", new DateTimeOffset(2026, 1, 1, 0, 1, 0, TimeSpan.Zero), "s1", "claude-opus-4-8")],
        };
        var subAgentsByPath = new Dictionary<string, IReadOnlyList<DiscoveredSubAgent>>
        {
            [mainPath] = [new DiscoveredSubAgent("agent-1", subPath, null)],
        };

        var service = new SessionService(
            new StubLocator([], new Dictionary<string, string?> { ["main"] = mainPath }, subAgentsByPath),
            new StubParser(byPath),
            new PriceTable());

        var trace = service.GetTrace("main");

        Assert.NotNull(trace);
        var span = Assert.Single(trace.SubAgents);
        Assert.Equal("agent-1", span.Role);
    }

    [Fact]
    public void GetAllSessions_SessionWithSubAgents_RowCombinedReflectsMainPlusSubs()
    {
        const string mainPath = "/fixtures/main.jsonl";
        const string sub1Path = "/fixtures/agent-1.jsonl";
        const string sub2Path = "/fixtures/agent-2-failed.jsonl";

        var main = new DiscoveredTranscript("main", "sample-project", mainPath);
        var byPath = new Dictionary<string, IReadOnlyList<TranscriptLine>>
        {
            [mainPath] = [Line("main", new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), "m1", "claude-opus-4-8")],
            [sub1Path] = [Line("agent-1", new DateTimeOffset(2026, 1, 1, 0, 1, 0, TimeSpan.Zero), "s1", "claude-opus-4-8")],
        };
        var subAgentsByPath = new Dictionary<string, IReadOnlyList<DiscoveredSubAgent>>
        {
            [mainPath] =
            [
                new DiscoveredSubAgent("agent-1", sub1Path, null),
                new DiscoveredSubAgent("agent-2", sub2Path, null),
            ],
        };

        var service = new SessionService(
            new StubLocator([main], null, subAgentsByPath),
            new StubParser(byPath),
            new PriceTable());

        var result = service.GetAllSessions();

        var row = Assert.Single(result);
        Assert.NotNull(row.Combined);
        Assert.Equal(row.Summary!.Tokens.Total + 150, row.Combined!.Tokens.Total);
    }
}
