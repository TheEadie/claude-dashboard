using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace Dashboard.Api.Tests;

public class SessionsEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public SessionsEndpointTests(WebApplicationFactory<Program> factory)
    {
        var claudeRoot = Path.Combine(AppContext.BaseDirectory, "Fixtures");
        var configured = factory.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, config) =>
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ClaudeDashboard:ClaudeRoot"] = claudeRoot,
                })));
        _client = configured.CreateClient();
    }

    [Fact]
    public async Task GetSession_KnownId_Returns200WithSummary()
    {
        var response = await _client.GetAsync("/api/sessions/valid-single-model");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("valid-single-model", body.GetProperty("sessionId").GetString());
        Assert.Equal("sample-project", body.GetProperty("project").GetString());
        Assert.Equal(5600, body.GetProperty("tokens").GetProperty("total").GetInt64());
    }

    [Fact]
    public async Task GetSession_UnknownId_Returns404WithError()
    {
        var response = await _client.GetAsync("/api/sessions/no-such-session");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("no session with that id was found", body.GetProperty("error").GetString());
        Assert.Equal("no-such-session", body.GetProperty("sessionId").GetString());
    }
}
