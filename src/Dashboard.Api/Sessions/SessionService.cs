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

        var lines = parser.Parse(path);
        return SessionAnalyzer.Analyze(sessionId, lines, prices);
    }
}
