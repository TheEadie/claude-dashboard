using Microsoft.Extensions.Configuration;

namespace Dashboard.Api.Transcripts;

internal sealed class TranscriptLocator : ITranscriptLocator
{
    private readonly string _claudeRoot;

    public TranscriptLocator(IConfiguration configuration)
    {
        _claudeRoot = configuration["ClaudeDashboard:ClaudeRoot"]
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude");
    }

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

        var fileName = sessionId + ".jsonl";
        return Directory.EnumerateFiles(projectsDir, fileName, SearchOption.AllDirectories).FirstOrDefault();
    }
}
