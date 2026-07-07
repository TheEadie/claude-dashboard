namespace Dashboard.Api.Transcripts;

internal sealed class TranscriptLocator(IConfiguration configuration) : ITranscriptLocator
{
    private readonly string _claudeRoot = configuration["ClaudeDashboard:ClaudeRoot"]
        ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude");

    public string? Locate(string sessionId)
    {
        // Guard against path traversal: a session id is always a bare file
        // name (e.g. a GUID) — never a path.
        if (string.IsNullOrWhiteSpace(sessionId)
            || sessionId.Contains('/')
            || sessionId.Contains('\\')
            || sessionId.Contains(".."))
        {
            return null;
        }

        var projectsDir = Path.Combine(_claudeRoot, "projects");
        if (!Directory.Exists(projectsDir))
        {
            return null;
        }

        // Probe each project directory for an exact file-name match. This mirrors
        // DiscoverSessions' TopDirectoryOnly semantics (so a sub-agent transcript
        // nested under <session>/subagents/ is NOT resolvable here, matching what
        // the session list exposes) and, by using Path.Combine + File.Exists rather
        // than passing sessionId as a search pattern, avoids interpreting glob
        // metacharacters (* and ?) in the id as wildcards.
        var fileName = sessionId + ".jsonl";
        foreach (var projectDir in Directory.EnumerateDirectories(projectsDir))
        {
            var candidate = Path.Combine(projectDir, fileName);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    public IReadOnlyList<DiscoveredTranscript> DiscoverSessions()
    {
        var projectsDir = Path.Combine(_claudeRoot, "projects");
        if (!Directory.Exists(projectsDir))
        {
            return Array.Empty<DiscoveredTranscript>();
        }

        var result = new List<DiscoveredTranscript>();

        foreach (var projectDir in Directory.EnumerateDirectories(projectsDir))
        {
            // TopDirectoryOnly is what excludes sub-agent transcripts nested
            // under <session>/subagents/agent-*.jsonl — do not switch to
            // AllDirectories here.
            foreach (var file in Directory.EnumerateFiles(projectDir, "*.jsonl", SearchOption.TopDirectoryOnly))
            {
                result.Add(new DiscoveredTranscript(
                    Path.GetFileNameWithoutExtension(file),
                    Path.GetFileName(projectDir),
                    file));
            }
        }

        return result;
    }

    public IReadOnlyList<DiscoveredSubAgent> DiscoverSubAgents(string sessionFilePath)
    {
        // Not user input: sessionFilePath is always a path already returned by
        // Locate/DiscoverSessions, so no path-traversal guard is needed here.
        var subagentsDir = Path.Combine(
            Path.GetDirectoryName(sessionFilePath)!,
            Path.GetFileNameWithoutExtension(sessionFilePath),
            "subagents");

        if (!Directory.Exists(subagentsDir))
        {
            return Array.Empty<DiscoveredSubAgent>();
        }

        var result = new List<DiscoveredSubAgent>();

        foreach (var transcriptPath in Directory.EnumerateFiles(subagentsDir, "agent-*.jsonl", SearchOption.TopDirectoryOnly))
        {
            var agentId = Path.GetFileNameWithoutExtension(transcriptPath);
            var metaCandidate = transcriptPath[..^".jsonl".Length] + ".meta.json";
            var metaPath = File.Exists(metaCandidate) ? metaCandidate : null;
            result.Add(new DiscoveredSubAgent(agentId, transcriptPath, metaPath));
        }

        return result.OrderBy(a => a.AgentId, StringComparer.Ordinal).ToList();
    }
}
