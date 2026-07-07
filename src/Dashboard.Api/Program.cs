using Dashboard.Api.Pricing;
using Dashboard.Api.Sessions;
using Dashboard.Api.Transcripts;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<ITranscriptLocator, TranscriptLocator>();
builder.Services.AddSingleton<ITranscriptParser, TranscriptParser>();
builder.Services.AddSingleton<IPriceTable, PriceTable>();
builder.Services.AddSingleton<ISessionService, SessionService>();

var app = builder.Build();

app.UseStaticFiles();

app.MapGet("/api/sessions", (ISessionService sessionService) =>
    Results.Ok(sessionService.GetAllSessions()));

app.MapGet("/api/sessions/{sessionId}", (string sessionId, ISessionService sessionService) =>
{
    var summary = sessionService.GetSession(sessionId);
    return summary is null
        ? Results.NotFound(new { error = "no session with that id was found", sessionId })
        : Results.Ok(summary);
});

app.MapFallbackToFile("index.html");

app.Run();

// Exposed so WebApplicationFactory<Program> can be used from integration tests.
public partial class Program;
