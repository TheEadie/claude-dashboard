namespace Dashboard.Api.Transcripts;

internal interface ITranscriptParser
{
    /// <summary>
    /// Reads a transcript JSONL file line-by-line, skipping any line that
    /// fails to deserialize. Never writes to the file.
    /// </summary>
    IReadOnlyList<TranscriptLine> Parse(string filePath);
}
