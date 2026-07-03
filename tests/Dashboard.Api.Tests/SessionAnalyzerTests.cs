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
}
