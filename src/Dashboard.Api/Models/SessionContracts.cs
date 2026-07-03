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
