namespace Dashboard.Api.Transcripts;

internal sealed record DiscoveredTranscript(string SessionId, string ProjectDirName, string FilePath);

internal interface ITranscriptLocator
{
    /// <summary>
    /// Finds the transcript file for the given session id under
    /// {ClaudeRoot}/projects/*/{sessionId}.jsonl. Returns null if the
    /// session id is invalid or no matching file exists.
    /// </summary>
    string? Locate(string sessionId);

    /// <summary>
    /// Discovers all top-level session transcripts at
    /// {ClaudeRoot}/projects/{project}/{session}.jsonl. Sub-agent transcripts
    /// nested under {session}/subagents/ are excluded. Returns an empty list if
    /// the projects directory is missing or contains no session transcripts.
    /// </summary>
    IReadOnlyList<DiscoveredTranscript> DiscoverSessions();
}
