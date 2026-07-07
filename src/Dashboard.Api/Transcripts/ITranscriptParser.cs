namespace Dashboard.Api.Transcripts;

internal interface ITranscriptParser
{
    /// <summary>
    /// Reads a transcript JSONL file line-by-line, skipping any line that
    /// fails to deserialize. Never writes to the file.
    /// </summary>
    IReadOnlyList<TranscriptLine> Parse(string filePath);

    /// <summary>
    /// Reads a sub-agent's agent-*.meta.json. Returns null when path is null, the
    /// file cannot be read, or the JSON is malformed. Never writes to the file.
    /// </summary>
    SubAgentMeta? ParseMeta(string? path);
}
