using Dashboard.Api.Models;

namespace Dashboard.Api.Sessions;

internal interface ISessionService
{
    /// <summary>
    /// Locates, parses, and analyzes the given session. Returns null when no
    /// transcript exists for the session id (drives the 404 response).
    /// </summary>
    SessionSummary? GetSession(string sessionId);

    /// <summary>
    /// Discovers, parses, and analyzes every top-level session transcript and
    /// returns them ordered by Started (most recent first); rows with no usable
    /// timestamp (failed reads, or sessions whose transcript yielded no
    /// timestamp) come last in a stable order. A transcript that cannot be read
    /// is returned as a failed entry rather than dropped or throwing.
    /// </summary>
    IReadOnlyList<SessionListItem> GetAllSessions();
}
