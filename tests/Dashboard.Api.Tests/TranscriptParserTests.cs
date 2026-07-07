using Dashboard.Api.Transcripts;

namespace Dashboard.Api.Tests;

public class TranscriptParserTests
{
    private static string FixturePath(string name) =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "projects", "sample-project", name);

    [Fact]
    public void Parse_ReturnsOneLinePerValidJsonLine()
    {
        var parser = new TranscriptParser();

        var lines = parser.Parse(FixturePath("valid-single-model.jsonl"));

        Assert.Equal(6, lines.Count);
        var firstAssistant = lines.First(l => l.Type == "assistant");
        Assert.Equal("claude-opus-4-8", firstAssistant.Message?.Model);
        Assert.Equal(1000, firstAssistant.Message?.Usage?.InputTokens);
    }

    [Fact]
    public void Parse_SkipsMalformedLinesAndReturnsTheRest()
    {
        var parser = new TranscriptParser();

        var lines = parser.Parse(FixturePath("malformed.jsonl"));

        // File has 1 system + 2 assistant valid lines, plus 2 malformed lines
        // interleaved that must be skipped without throwing.
        Assert.Equal(3, lines.Count);
        Assert.Equal(
            ["bad_0001", "bad_0003"],
            lines.Where(l => l.Type == "assistant").Select(l => l.Message!.Id));
    }

    [Fact]
    public void ParseMeta_KnownMetaFile_ReturnsAgentType()
    {
        var parser = new TranscriptParser();
        var path = Path.Combine(
            AppContext.BaseDirectory, "Fixtures", "projects", "sample-project",
            "sub-parent", "subagents", "agent-1.meta.json");

        var meta = parser.ParseMeta(path);

        Assert.NotNull(meta);
        Assert.Equal("code-reviewer", meta.AgentType);
    }

    [Fact]
    public void ParseMeta_NullPath_ReturnsNull()
    {
        var parser = new TranscriptParser();

        Assert.Null(parser.ParseMeta(null));
    }

    [Fact]
    public void ParseMeta_NonexistentPath_ReturnsNull()
    {
        var parser = new TranscriptParser();

        Assert.Null(parser.ParseMeta(FixturePath("does-not-exist.meta.json")));
    }
}
