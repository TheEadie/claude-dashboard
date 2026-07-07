namespace Dashboard.Api.Models;

/// <summary>
/// The response shape for GET /api/sessions/{sessionId}. Field names are the
/// star contract from the architecture sticky — do not rename without updating
/// the SPA's src/types.ts in lockstep.
/// </summary>
public sealed record SessionSummary(
    string SessionId,
    string Project,
    string Title,
    DateTimeOffset? StartedAt,
    DateTimeOffset? EndedAt,
    long DurationMs,
    IReadOnlyList<string> Models,
    IReadOnlyList<string> UnpricedModels,
    TokenBreakdown Tokens,
    decimal CostUsd);

public sealed record TokenBreakdown(
    long Input,
    long Output,
    long CacheWrite,
    long CacheRead,
    long Total);

/// <summary>
/// One row of GET /api/sessions. Successful rows carry the full SessionSummary
/// (same shape as the detail endpoint) and the session's Combined totals
/// (main + sub-agents); failed rows carry only the session id and a
/// best-effort project name with Failed = true, Summary = null, Combined = null.
/// Keep field names in lockstep with the SPA's src/types.ts.
/// </summary>
public sealed record SessionListItem(
    string SessionId,
    string Project,
    bool Failed,
    SessionSummary? Summary,
    CombinedTotals? Combined);

/// <summary>
/// One sub-agent span in a session trace. Failed spans carry only AgentId,
/// Role and Failed = true (no stats, no timestamps); undated-but-readable
/// spans carry stats with null timestamps.
/// </summary>
public sealed record SubAgentSpan(
    string AgentId,
    string Role,
    bool Failed,
    DateTimeOffset? StartedAt,
    DateTimeOffset? EndedAt,
    long DurationMs,
    IReadOnlyList<string> Models,
    IReadOnlyList<string> UnpricedModels,
    TokenBreakdown? Tokens,
    decimal? CostUsd);

/// <summary>
/// Combined total = main session + every successfully-analyzed sub-agent.
/// </summary>
public sealed record CombinedTotals(
    decimal CostUsd,
    TokenBreakdown Tokens,
    IReadOnlyList<string> Models,
    IReadOnlyList<string> UnpricedModels);

/// <summary>
/// Response shape for GET /api/sessions/{sessionId}.
/// </summary>
public sealed record SessionTrace(
    SessionSummary Session,
    IReadOnlyList<SubAgentSpan> SubAgents,
    CombinedTotals Combined);
