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
        var session = body.GetProperty("session");
        Assert.Equal("valid-single-model", session.GetProperty("sessionId").GetString());
        Assert.Equal("sample-project", session.GetProperty("project").GetString());
        Assert.Equal(5600, session.GetProperty("tokens").GetProperty("total").GetInt64());
        Assert.Empty(body.GetProperty("subAgents").EnumerateArray());
        Assert.Equal(5600, body.GetProperty("combined").GetProperty("tokens").GetProperty("total").GetInt64());
    }

    [Fact]
    public async Task GetSession_SessionWithSubAgents_ReturnsSubAgentsAndCombinedTotals()
    {
        var response = await _client.GetAsync("/api/sessions/sub-parent");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var session = body.GetProperty("session");
        Assert.Equal("sub-parent", session.GetProperty("sessionId").GetString());

        var subAgents = body.GetProperty("subAgents").EnumerateArray().ToList();
        Assert.Equal(2, subAgents.Count);
        var roles = subAgents.Select(s => s.GetProperty("role").GetString()).ToList();
        Assert.Contains("code-reviewer", roles);
        Assert.Contains("agent-2", roles);

        var agent1 = subAgents.Single(s => s.GetProperty("agentId").GetString() == "agent-1");
        var models = agent1.GetProperty("models").EnumerateArray().Select(m => m.GetString()).ToList();
        Assert.Equal(["claude-opus-4-8"], models);
        Assert.Equal(150, agent1.GetProperty("tokens").GetProperty("total").GetInt64());

        var combined = body.GetProperty("combined");
        Assert.Equal(1950, combined.GetProperty("tokens").GetProperty("total").GetInt64());
        var combinedModels = combined.GetProperty("models").EnumerateArray().Select(m => m.GetString()).ToList();
        Assert.Contains("claude-opus-4-8", combinedModels);
        Assert.Contains("claude-sonnet-4-6", combinedModels);
    }

    [Fact]
    public async Task GetSession_SubAgentUsesUnpricedModel_CombinedUnpricedModelsIncludesIt()
    {
        var response = await _client.GetAsync("/api/sessions/sub-with-unpriced-model");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        // The main session itself uses only a priced model.
        var session = body.GetProperty("session");
        Assert.Empty(session.GetProperty("unpricedModels").EnumerateArray());

        // The sub-agent alone contributes the unpriced model.
        var subAgent = Assert.Single(body.GetProperty("subAgents").EnumerateArray());
        var subAgentUnpriced = subAgent.GetProperty("unpricedModels").EnumerateArray()
            .Select(m => m.GetString()).ToList();
        Assert.Equal(["claude-experimental-x"], subAgentUnpriced);
        Assert.Equal(5000, subAgent.GetProperty("durationMs").GetInt64());

        // Combined must union in the sub-agent's unpriced model even though the
        // main session has none of its own — this is what drives the session's
        // understated-cost marker (SPA derives it from Combined.UnpricedModels).
        var combinedUnpriced = body.GetProperty("combined").GetProperty("unpricedModels").EnumerateArray()
            .Select(m => m.GetString()).ToList();
        Assert.Contains("claude-experimental-x", combinedUnpriced);
    }

    [Fact]
    public async Task GetSessions_SubAgentUsesUnpricedModel_RowCombinedUnpricedModelsIncludesIt()
    {
        var response = await _client.GetAsync("/api/sessions");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        var row = body.EnumerateArray()
            .Single(e => e.GetProperty("sessionId").GetString() == "sub-with-unpriced-model");

        // The row's own summary has no unpriced models — only the sub-agent does.
        var summaryUnpriced = row.GetProperty("summary").GetProperty("unpricedModels").EnumerateArray()
            .Select(m => m.GetString()).ToList();
        Assert.Empty(summaryUnpriced);

        // The row's Combined must surface the sub-agent's unpriced model, which is
        // what the session-list understated-cost marker keys off.
        var combinedUnpriced = row.GetProperty("combined").GetProperty("unpricedModels").EnumerateArray()
            .Select(m => m.GetString()).ToList();
        Assert.Contains("claude-experimental-x", combinedUnpriced);
    }

    [Fact]
    public async Task GetSession_ExposesContextWindowForMainSession()
    {
        var response = await _client.GetAsync("/api/sessions/valid-single-model");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var contextWindow = body.GetProperty("contextWindow").EnumerateArray().ToList();
        Assert.Equal(3, contextWindow.Count);
        Assert.Equal(1500, contextWindow[0].GetProperty("tokens").GetInt64());
        Assert.Equal("claude-opus-4-8", contextWindow[0].GetProperty("model").GetString());
    }

    [Fact]
    public async Task GetSession_ExposesContextWindowForSubAgent()
    {
        var response = await _client.GetAsync("/api/sessions/sub-parent");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var agent1 = body.GetProperty("subAgents").EnumerateArray()
            .Single(s => s.GetProperty("agentId").GetString() == "agent-1");
        var contextWindow = agent1.GetProperty("contextWindow").EnumerateArray().ToList();
        Assert.Single(contextWindow);
        Assert.Equal(100, contextWindow[0].GetProperty("tokens").GetInt64());
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
        Assert.Contains("sub-parent", ids);
        Assert.DoesNotContain("agent-1", ids);
    }

    [Fact]
    public async Task GetSessions_SessionWithSubAgents_RowHasCombinedTotalsAndSummary()
    {
        var response = await _client.GetAsync("/api/sessions");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        var subParent = body.EnumerateArray()
            .Single(e => e.GetProperty("sessionId").GetString() == "sub-parent");

        Assert.NotEqual(JsonValueKind.Null, subParent.GetProperty("summary").ValueKind);
        var combined = subParent.GetProperty("combined");
        Assert.Equal(1950, combined.GetProperty("tokens").GetProperty("total").GetInt64());
        var combinedModels = combined.GetProperty("models").EnumerateArray().Select(m => m.GetString()).ToList();
        Assert.Contains("claude-opus-4-8", combinedModels);
        Assert.Contains("claude-sonnet-4-6", combinedModels);
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
