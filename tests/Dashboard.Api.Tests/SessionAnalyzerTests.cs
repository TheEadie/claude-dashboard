using Dashboard.Api.Models;
using Dashboard.Api.Pricing;
using Dashboard.Api.Sessions;
using Dashboard.Api.Transcripts;

namespace Dashboard.Api.Tests;

public class SessionAnalyzerTests
{
    private static string FixturePath(string name) =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "projects", "sample-project", name);

    private static IReadOnlyList<TranscriptLine> ParseFixture(string name) =>
        new TranscriptParser().Parse(FixturePath(name));

    [Fact]
    public void Analyze_DuplicateMessageId_CountsTokensOnce()
    {
        var lines = ParseFixture("valid-single-model.jsonl");
        var prices = new PriceTable();

        var summary = SessionAnalyzer.Analyze("valid-single-model", lines, prices);

        // msg_0001 appears twice (identical usage) and must be counted once;
        // msg_0002 and msg_0003 are distinct turns.
        Assert.Equal(3500, summary.Tokens.Input);
        Assert.Equal(1500, summary.Tokens.Output);
        Assert.Equal(200, summary.Tokens.CacheWrite);
        Assert.Equal(400, summary.Tokens.CacheRead);
        Assert.Equal(5600, summary.Tokens.Total);
    }

    [Fact]
    public void Analyze_ValidSession_ComputesMetadata()
    {
        var lines = ParseFixture("valid-single-model.jsonl");
        var prices = new PriceTable();

        var summary = SessionAnalyzer.Analyze("valid-single-model", lines, prices);

        Assert.Equal("sample-project", summary.Project);
        Assert.Equal("Fix the flaky retry test", summary.Title);
        Assert.Equal(DateTimeOffset.Parse("2026-06-26T14:30:50.111Z"), summary.StartedAt);
        Assert.Equal(DateTimeOffset.Parse("2026-06-26T14:40:12.345Z"), summary.EndedAt);
        Assert.Equal(562234, summary.DurationMs);
        Assert.Equal(["claude-opus-4-8"], summary.Models);
    }

    [Fact]
    public void Analyze_MultipleAiTitles_UsesFirstNotLast()
    {
        // Claude Code rewrites the ai-title as a conversation's topic drifts;
        // the title should reflect the session's original intent (first title),
        // not whatever the last operation happened to be.
        var lines = new List<TranscriptLine>
        {
            new("ai-title", "/home/user/code/sample-project", "s", null, false, "Original intent", null),
            new("ai-title", "/home/user/code/sample-project", "s", null, false, "Last operation", null),
        };

        var summary = SessionAnalyzer.Analyze("s", lines, new PriceTable());

        Assert.Equal("Original intent", summary.Title);
    }

    [Fact]
    public void Analyze_NoAiTitle_FallsBackToSessionId()
    {
        var lines = new List<TranscriptLine>
        {
            new("assistant", "/home/user/code/sample-project", "s", null, false, null,
                new AssistantMessage("m1", "claude-opus-4-8", new Usage(100, 50, 0, 0))),
        };

        var summary = SessionAnalyzer.Analyze("fallback-id", lines, new PriceTable());

        Assert.Equal("fallback-id", summary.Title);
    }

    [Fact]
    public void Analyze_MultiModelSession_PricesEachTurnByItsOwnModel()
    {
        var lines = ParseFixture("multi-model.jsonl");
        var prices = new PriceTable();

        var summary = SessionAnalyzer.Analyze("multi-model", lines, prices);

        Assert.Equal(["claude-opus-4-8", "claude-sonnet-4-6"], summary.Models);
        Assert.Empty(summary.UnpricedModels);
        // opus turns: (1000*5 + 400*25)/1e6 + (300*5 + 150*25)/1e6 = 0.015 + 0.00525
        // sonnet turn: (2000*3 + 900*15 + 100*3.75 + 50*0.30)/1e6 = 0.019890... wait computed below
        Assert.Equal(0.04014m, summary.CostUsd);
    }

    [Fact]
    public void Analyze_UnpricedModel_TokensCountedFlaggedZeroCost()
    {
        var lines = ParseFixture("unpriced-model.jsonl");
        var prices = new PriceTable();

        var summary = SessionAnalyzer.Analyze("unpriced-model", lines, prices);

        Assert.Equal(1500, summary.Tokens.Input);
        Assert.Equal(650, summary.Tokens.Output);
        Assert.Contains("claude-experimental-x", summary.UnpricedModels);
        // Only the opus turn contributes cost; the unpriced turn adds $0.
        Assert.Equal((1000m * 5.00m + 400m * 25.00m) / 1_000_000m, summary.CostUsd);
    }

    [Fact]
    public void Analyze_SyntheticAndSidechainTurns_AreExcluded()
    {
        var lines = ParseFixture("synthetic-and-sidechain.jsonl");
        var prices = new PriceTable();

        var summary = SessionAnalyzer.Analyze("synthetic-and-sidechain", lines, prices);

        // Only ss_0001 (1000 input / 400 output) should count; the synthetic
        // turn has zero usage anyway, and the sidechain turn (5000/5000) must
        // be excluded even though it carries a real model and non-zero usage.
        Assert.Equal(1000, summary.Tokens.Input);
        Assert.Equal(400, summary.Tokens.Output);
        Assert.Equal(["claude-opus-4-8"], summary.Models);
    }

    [Fact]
    public void AnalyzeSubAgent_SidechainLines_AreIncludedNotExcluded()
    {
        // Contrast with Analyze: a sub-agent's own transcript is entirely
        // sidechain records, so AnalyzeSubAgent must NOT filter them out.
        var lines = new List<TranscriptLine>
        {
            new("assistant", "/home/user/code/sample-project", "agent-1",
                DateTimeOffset.Parse("2026-06-26T15:01:00.000Z"), true, null,
                new AssistantMessage("a1_0001", "claude-opus-4-8",
                    new Usage(100, 50, 0, 0))),
        };
        var prices = new PriceTable();

        var span = SessionAnalyzer.AnalyzeSubAgent("agent-1", "code-reviewer", lines, prices);

        Assert.Equal("agent-1", span.AgentId);
        Assert.Equal("code-reviewer", span.Role);
        Assert.False(span.Failed);
        Assert.Equal(100, span.Tokens!.Input);
        Assert.Equal(50, span.Tokens.Output);
        Assert.Equal(["claude-opus-4-8"], span.Models);
        Assert.Equal((100m * 5.00m + 50m * 25.00m) / 1_000_000m, span.CostUsd);
        Assert.Equal(DateTimeOffset.Parse("2026-06-26T15:01:00.000Z"), span.StartedAt);
        Assert.Equal(DateTimeOffset.Parse("2026-06-26T15:01:00.000Z"), span.EndedAt);
    }

    [Fact]
    public void Combine_MainPlusTwoReadableSubAgents_SumsAllCategoriesAndUnionsModels()
    {
        var main = SessionAnalyzer.Analyze("main", ParseFixture("valid-single-model.jsonl"), new PriceTable());
        var sub1 = new SubAgentSpan(
            "agent-1", "code-reviewer", false,
            DateTimeOffset.Parse("2026-06-26T15:01:00.000Z"), DateTimeOffset.Parse("2026-06-26T15:01:00.000Z"), 0,
            ["claude-opus-4-8"], [], new TokenBreakdown(100, 50, 0, 0, 150), 0.00175m, []);
        var sub2 = new SubAgentSpan(
            "agent-2", "agent-2", false,
            DateTimeOffset.Parse("2026-07-01T09:02:00.000Z"), DateTimeOffset.Parse("2026-07-01T09:02:00.000Z"), 0,
            ["claude-sonnet-4-6"], [], new TokenBreakdown(200, 100, 0, 0, 300), 0.0021m, []);

        var combined = SessionAnalyzer.Combine(main, [sub1, sub2]);

        Assert.Equal(main.Tokens.Total + 150 + 300, combined.Tokens.Total);
        Assert.Equal(main.CostUsd + 0.00175m + 0.0021m, combined.CostUsd);
        Assert.Contains("claude-opus-4-8", combined.Models);
        Assert.Contains("claude-sonnet-4-6", combined.Models);
    }

    [Fact]
    public void Combine_FailedSubAgentWithNullTokens_ContributesNothing()
    {
        var main = SessionAnalyzer.Analyze("main", ParseFixture("valid-single-model.jsonl"), new PriceTable());
        var failed = new SubAgentSpan("agent-1", "agent-1", true, null, null, 0, [], [], null, null, []);

        var combined = SessionAnalyzer.Combine(main, [failed]);

        Assert.Equal(main.Tokens.Total, combined.Tokens.Total);
        Assert.Equal(main.CostUsd, combined.CostUsd);
    }

    [Fact]
    public void Combine_UndatedButReadableSubAgent_IsIncludedInCombinedTotal()
    {
        var main = SessionAnalyzer.Analyze("main", ParseFixture("valid-single-model.jsonl"), new PriceTable());
        var undated = new SubAgentSpan(
            "agent-1", "agent-1", false, null, null, 0,
            ["claude-opus-4-8"], [], new TokenBreakdown(100, 50, 0, 0, 150), 0.00175m, []);

        var combined = SessionAnalyzer.Combine(main, [undated]);

        Assert.Equal(main.Tokens.Total + 150, combined.Tokens.Total);
        Assert.Equal(main.CostUsd + 0.00175m, combined.CostUsd);
    }

    [Fact]
    public void MainContextWindow_ValidSession_ReturnsOrderedPerTurnTokens()
    {
        var lines = ParseFixture("valid-single-model.jsonl");

        var contextWindow = SessionAnalyzer.MainContextWindow(lines);

        Assert.Equal(3, contextWindow.Count);
        Assert.Equal([1500, 2100, 500], contextWindow.Select(t => t.Tokens).ToList());
        Assert.All(contextWindow, t => Assert.Equal("claude-opus-4-8", t.Model));
    }

    [Fact]
    public void MainContextWindow_ExcludesSyntheticAndSidechain()
    {
        var lines = ParseFixture("synthetic-and-sidechain.jsonl");

        var contextWindow = SessionAnalyzer.MainContextWindow(lines);

        var turn = Assert.Single(contextWindow);
        Assert.Equal(1000, turn.Tokens);
        Assert.Equal("claude-opus-4-8", turn.Model);
    }

    [Fact]
    public void MainContextWindow_MultiModel_CarriesPerTurnModel()
    {
        var lines = ParseFixture("multi-model.jsonl");

        var contextWindow = SessionAnalyzer.MainContextWindow(lines);

        Assert.Equal([1000, 2150, 300], contextWindow.Select(t => t.Tokens).ToList());
        Assert.Equal(
            ["claude-opus-4-8", "claude-sonnet-4-6", "claude-opus-4-8"],
            contextWindow.Select(t => t.Model).ToList());
    }

    [Fact]
    public void AnalyzeSubAgent_PopulatesContextWindow()
    {
        var lines = new List<TranscriptLine>
        {
            new("assistant", "/home/user/code/sample-project", "agent-1",
                DateTimeOffset.Parse("2026-06-26T15:01:00.000Z"), true, null,
                new AssistantMessage("a1_0001", "claude-opus-4-8",
                    new Usage(100, 50, 0, 0))),
        };
        var prices = new PriceTable();

        var span = SessionAnalyzer.AnalyzeSubAgent("agent-1", "code-reviewer", lines, prices);

        var turn = Assert.Single(span.ContextWindow);
        Assert.Equal(100, turn.Tokens);
        Assert.Equal("claude-opus-4-8", turn.Model);
    }
}
