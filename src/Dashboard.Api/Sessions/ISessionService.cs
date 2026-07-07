using Dashboard.Api.Models;

namespace Dashboard.Api.Sessions;

internal interface ISessionService
{
    /// <summary>
    /// Locates, parses, and analyzes the given session and its sub-agents. Returns
    /// null when no main transcript exists / is readable (drives the 404).
    /// </summary>
    SessionTrace? GetTrace(string sessionId);

    /// <summary>
    /// Discovers, parses, and analyzes every top-level session transcript and
    /// returns them ordered by Started (most recent first); rows with no usable
    /// timestamp (failed reads, or sessions whose transcript yielded no
    /// timestamp) come last in a stable order. A transcript that cannot be read
    /// is returned as a failed entry rather than dropped or throwing.
    /// </summary>
    IReadOnlyList<SessionListItem> GetAllSessions();
}
