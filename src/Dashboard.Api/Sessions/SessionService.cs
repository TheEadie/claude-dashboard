using Dashboard.Api.Models;
using Dashboard.Api.Pricing;
using Dashboard.Api.Transcripts;

namespace Dashboard.Api.Sessions;

internal sealed class SessionService(
    ITranscriptLocator locator,
    ITranscriptParser parser,
    IPriceTable prices) : ISessionService
{
    public SessionSummary? GetSession(string sessionId)
    {
        var path = locator.Locate(sessionId);
        if (path is null)
        {
            return null;
        }

        try
        {
            var lines = parser.Parse(path);
            return SessionAnalyzer.Analyze(sessionId, lines, prices);
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
                items.Add(new SessionListItem(d.SessionId, summary.Project, false, summary));
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                items.Add(new SessionListItem(d.SessionId, d.ProjectDirName, true, null));
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
}
