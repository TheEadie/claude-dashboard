namespace Dashboard.Api.Transcripts;

internal interface ITranscriptLocator
{
    /// <summary>
    /// Finds the transcript file for the given session id under
    /// {ClaudeRoot}/projects/*/{sessionId}.jsonl. Returns null if the
    /// session id is invalid or no matching file exists.
    /// </summary>
    string? Locate(string sessionId);
}
