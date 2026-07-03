using System.Text.Json.Serialization;

namespace Dashboard.Api.Transcripts;

/// <summary>
/// A single JSONL line from a ~/.claude transcript file. Deliberately a single
/// nullable-rich shape covering the union of record "type"s we care about
/// (assistant / ai-title / others carrying cwd, timestamp, isSidechain), rather
/// than a polymorphic hierarchy — the transcript format is not versioned or
/// documented, so a permissive shape is the least brittle option.
/// </summary>
internal sealed record TranscriptLine(
    [property: JsonPropertyName("type")] string? Type,
    [property: JsonPropertyName("cwd")] string? Cwd,
    [property: JsonPropertyName("sessionId")] string? SessionId,
    [property: JsonPropertyName("timestamp")] DateTimeOffset? Timestamp,
    [property: JsonPropertyName("isSidechain")] bool IsSidechain,
    [property: JsonPropertyName("aiTitle")] string? AiTitle,
    [property: JsonPropertyName("message")] AssistantMessage? Message);

internal sealed record AssistantMessage(
    [property: JsonPropertyName("id")] string? Id,
    [property: JsonPropertyName("model")] string? Model,
    [property: JsonPropertyName("usage")] Usage? Usage);

internal sealed record Usage(
    [property: JsonPropertyName("input_tokens")] long InputTokens,
    [property: JsonPropertyName("output_tokens")] long OutputTokens,
    [property: JsonPropertyName("cache_creation_input_tokens")] long CacheCreationInputTokens,
    [property: JsonPropertyName("cache_read_input_tokens")] long CacheReadInputTokens);
