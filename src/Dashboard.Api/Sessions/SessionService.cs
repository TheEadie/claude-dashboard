using Dashboard.Api.Models;
using Dashboard.Api.Pricing;
using Dashboard.Api.Transcripts;

namespace Dashboard.Api.Sessions;

internal sealed class SessionService(
    ITranscriptLocator locator,
    ITranscriptParser parser,
    IPriceTable prices) : ISessionService
{
    public SessionTrace? GetTrace(string sessionId)
    {
        var path = locator.Locate(sessionId);
        if (path is null)
        {
            return null;
        }

        try
        {
            var lines = parser.Parse(path);
            var main = SessionAnalyzer.Analyze(sessionId, lines, prices);
            var contextWindow = SessionAnalyzer.MainContextWindow(lines);
            var subs = AnalyzeSubAgents(path);
            var combined = SessionAnalyzer.Combine(main, subs);
            return new SessionTrace(main, contextWindow, subs, combined);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // The transcript was located but is unreadable (locked, deleted, or
            // permission-denied between Locate and Parse). Degrade to "not found"
            // (404) rather than surfacing an unhandled 500 — this mirrors how
            // GetAllSessions turns the same condition into a graceful failed row.
            return null;
        }
    }

    public IReadOnlyList<SessionListItem> GetAllSessions()
    {
        var discovered = locator.DiscoverSessions();
        var items = new List<SessionListItem>(discovered.Count);

        foreach (var d in discovered)
        {
            try
            {
                var lines = parser.Parse(d.FilePath);
                var summary = SessionAnalyzer.Analyze(d.SessionId, lines, prices);
                var subs = AnalyzeSubAgents(d.FilePath);
                var combined = SessionAnalyzer.Combine(summary, subs);
                items.Add(new SessionListItem(d.SessionId, summary.Project, false, summary, combined));
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                items.Add(new SessionListItem(d.SessionId, d.ProjectDirName, true, null, null));
            }
        }

        // Timestamped rows first, newest Started first; everything without a
        // usable Started timestamp (failed, or no timestamp) last, in stable
        // (discovery) order. LINQ OrderBy is a stable sort in .NET.
        return items
            .OrderByDescending(i => i.Summary?.StartedAt is not null)
            .ThenByDescending(i => i.Summary?.StartedAt ?? default)
            .ToList();
    }

    private IReadOnlyList<SubAgentSpan> AnalyzeSubAgents(string sessionFilePath)
    {
        IReadOnlyList<DiscoveredSubAgent> discovered;
        try
        {
            discovered = locator.DiscoverSubAgents(sessionFilePath);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // The subagents directory exists but could not be enumerated
            // (permission-denied, broken symlink, or transient IO). A readable
            // main session must still render, so degrade to "no sub-agents"
            // rather than failing the whole trace/list row.
            return Array.Empty<SubAgentSpan>();
        }

        var spans = new List<SubAgentSpan>(discovered.Count);

        foreach (var d in discovered)
        {
            var meta = parser.ParseMeta(d.MetaPath);
            var role = string.IsNullOrWhiteSpace(meta?.AgentType) ? d.AgentId : meta.AgentType!;

            try
            {
                var lines = parser.Parse(d.TranscriptPath);
                spans.Add(SessionAnalyzer.AnalyzeSubAgent(d.AgentId, role, lines, prices));
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                spans.Add(new SubAgentSpan(
                    d.AgentId, role, true, null, null, 0,
                    Array.Empty<string>(), Array.Empty<string>(), null, null,
                    Array.Empty<ContextWindowTurn>()));
            }
        }

        return spans;
    }
}
