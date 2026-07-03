using Dashboard.Api.Models;

namespace Dashboard.Api.Sessions;

internal interface ISessionService
{
    /// <summary>
    /// Locates, parses, and analyzes the given session. Returns null when no
    /// transcript exists for the session id (drives the 404 response).
    /// </summary>
    SessionSummary? GetSession(string sessionId);
}
