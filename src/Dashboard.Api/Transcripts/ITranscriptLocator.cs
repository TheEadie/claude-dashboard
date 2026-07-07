namespace Dashboard.Api.Transcripts;

internal sealed record DiscoveredTranscript(string SessionId, string ProjectDirName, string FilePath);

internal sealed record DiscoveredSubAgent(string AgentId, string TranscriptPath, string? MetaPath);

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

    /// <summary>
    /// Discovers a session's sub-agent transcripts at
    /// {sessionFileDir}/{sessionId}/subagents/agent-*.jsonl (the directory that
    /// sits beside the session's own transcript). MetaPath is the sibling
    /// agent-*.meta.json when it exists, else null. Returns empty when the
    /// subagents directory does not exist. Ordered by AgentId for determinism.
    /// </summary>
    IReadOnlyList<DiscoveredSubAgent> DiscoverSubAgents(string sessionFilePath);
}
