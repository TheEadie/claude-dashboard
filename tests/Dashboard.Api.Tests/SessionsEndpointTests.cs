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

    [Fact]
    public async Task GetSessions_ReturnsOkWithDiscoveredSessions()
    {
        var response = await _client.GetAsync("/api/sessions");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var ids = body.EnumerateArray().Select(e => e.GetProperty("sessionId").GetString()).ToList();

        Assert.Contains("valid-single-model", ids);
        Assert.Contains("multi-model", ids);
        Assert.Contains("unpriced-model", ids);
        Assert.Contains("malformed", ids);
        Assert.Contains("synthetic-and-sidechain", ids);
        Assert.Contains("second-project-session", ids);
        Assert.DoesNotContain("agent-1", ids);
    }

    [Fact]
    public async Task GetSessions_MalformedTranscript_IsSuccessfulRow()
    {
        var response = await _client.GetAsync("/api/sessions");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        var malformed = body.EnumerateArray()
            .Single(e => e.GetProperty("sessionId").GetString() == "malformed");

        Assert.False(malformed.GetProperty("failed").GetBoolean());
        var summary = malformed.GetProperty("summary");
        Assert.NotEqual(JsonValueKind.Null, summary.ValueKind);
        Assert.True(summary.GetProperty("tokens").GetProperty("total").GetInt64() > 0);
    }

    [Fact]
    public async Task GetSessions_OrdersNewestFirst()
    {
        var response = await _client.GetAsync("/api/sessions");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var ids = body.EnumerateArray().Select(e => e.GetProperty("sessionId").GetString()).ToList();

        var expectedOrder = new[]
        {
            "synthetic-and-sidechain",
            "malformed",
            "unpriced-model",
            "multi-model",
            "valid-single-model",
        };

        var indices = expectedOrder.Select(id => ids.IndexOf(id)).ToList();
        Assert.True(indices.SequenceEqual(indices.OrderBy(i => i)), "expected newest-first relative ordering");
    }

    [Fact]
    public async Task GetSessions_EmptyProjects_ReturnsOkEmptyList()
    {
        var emptyRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(emptyRoot);

        var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, config) =>
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ClaudeDashboard:ClaudeRoot"] = emptyRoot,
                })));
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/sessions");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(JsonValueKind.Array, body.ValueKind);
        Assert.Empty(body.EnumerateArray());
    }
}
