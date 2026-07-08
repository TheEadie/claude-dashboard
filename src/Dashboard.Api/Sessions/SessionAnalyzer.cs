using Dashboard.Api.Models;
using Dashboard.Api.Pricing;
using Dashboard.Api.Transcripts;

namespace Dashboard.Api.Sessions;

internal static class SessionAnalyzer
{
    private const string SyntheticModel = "<synthetic>";

    public static SessionSummary Analyze(string sessionId, IReadOnlyList<TranscriptLine> lines, IPriceTable prices)
    {
        // Sub-agent/sidechain records are out of scope for the main session —
        // exclude them from every computation below. (Sub-agents themselves
        // are analyzed by AnalyzeSubAgent, which does NOT filter on
        // IsSidechain — a sub-agent's own transcript is entirely sidechain
        // records.)
        var mainLine = lines.Where(l => !l.IsSidechain).ToList();

        var project = mainLine
            .Select(l => l.Cwd)
            .FirstOrDefault(cwd => !string.IsNullOrEmpty(cwd));
        project = string.IsNullOrEmpty(project) ? string.Empty : Path.GetFileName(project.TrimEnd('/'));

        // Use the first AI title so the session name reflects its original
        // intent rather than whatever the last operation happened to be.
        var title = mainLine.Select(l => l.AiTitle).FirstOrDefault(t => !string.IsNullOrEmpty(t)) ?? sessionId;

        var (tokens, models, unpricedModels, costUsd) = Aggregate(mainLine, prices);
        var (startedAt, endedAt, durationMs) = TimeRange(mainLine);

        return new SessionSummary(
            sessionId,
            project,
            title,
            startedAt,
            endedAt,
            durationMs,
            models,
            unpricedModels,
            tokens,
            costUsd);
    }

    /// <summary>
    /// Analyzes a sub-agent from its OWN transcript. Unlike a main session, a
    /// sub-agent transcript is entirely sidechain records — do NOT filter on
    /// IsSidechain here; aggregate over all lines.
    /// </summary>
    public static SubAgentSpan AnalyzeSubAgent(
        string agentId, string role, IReadOnlyList<TranscriptLine> lines, IPriceTable prices)
    {
        var (tokens, models, unpriced, cost) = Aggregate(lines, prices);
        var (startedAt, endedAt, durationMs) = TimeRange(lines);
        var contextWindow = ContextWindow(lines);
        return new SubAgentSpan(
            agentId, role, false, startedAt, endedAt, durationMs,
            models, unpriced, tokens, cost, contextWindow);
    }

    /// <summary>Main session: sidechain records are excluded (mirrors Analyze).</summary>
    public static IReadOnlyList<ContextWindowTurn> MainContextWindow(IReadOnlyList<TranscriptLine> lines) =>
        ContextWindow(lines.Where(l => !l.IsSidechain));

    private static IReadOnlyList<ContextWindowTurn> ContextWindow(IEnumerable<TranscriptLine> lines) =>
        SelectTurns(lines)
            .Where(m => m.Usage is not null)
            .Select(m => new ContextWindowTurn(
                m.Usage!.InputTokens + m.Usage.CacheReadInputTokens + m.Usage.CacheCreationInputTokens,
                m.Model))
            .ToList();

    /// <summary>
    /// Combined total = main + every non-failed sub-agent (a failed sub-agent has
    /// Tokens == null and contributes nothing). Models/unpriced are order-preserving
    /// unions starting from the main session.
    /// </summary>
    public static CombinedTotals Combine(SessionSummary main, IReadOnlyList<SubAgentSpan> subAgents)
    {
        long input = main.Tokens.Input, output = main.Tokens.Output;
        long cacheWrite = main.Tokens.CacheWrite, cacheRead = main.Tokens.CacheRead;
        var cost = main.CostUsd;
        var models = new List<string>(main.Models);
        var unpriced = new List<string>(main.UnpricedModels);

        foreach (var s in subAgents)
        {
            if (s.Tokens is null)
            {
                continue;
            }

            input += s.Tokens.Input;
            output += s.Tokens.Output;
            cacheWrite += s.Tokens.CacheWrite;
            cacheRead += s.Tokens.CacheRead;
            cost += s.CostUsd ?? 0m;

            foreach (var m in s.Models)
            {
                if (!models.Contains(m))
                {
                    models.Add(m);
                }
            }

            foreach (var m in s.UnpricedModels)
            {
                if (!unpriced.Contains(m))
                {
                    unpriced.Add(m);
                }
            }
        }

        var tokens = new TokenBreakdown(input, output, cacheWrite, cacheRead,
            input + output + cacheWrite + cacheRead);
        return new CombinedTotals(cost, tokens, models, unpriced);
    }

    /// <summary>
    /// CRITICAL: a single logical assistant turn is emitted as multiple
    /// JSONL lines sharing the same message.id, each repeating an identical
    /// usage block. Group by message.id and keep one usage block per group,
    /// or totals inflate several-fold (verified against real transcripts —
    /// see plan §1.4). "&lt;synthetic&gt;" turns are Claude Code placeholders
    /// with zero usage, not real model turns — exclude them.
    /// </summary>
    private static List<AssistantMessage> SelectTurns(IEnumerable<TranscriptLine> lines) =>
        lines
            .Where(l => l.Type == "assistant" && l.Message is { Id: not null })
            .GroupBy(l => l.Message!.Id)
            .Select(g => g.First().Message!)
            .Where(m => m.Model != SyntheticModel)
            .ToList();

    private static (TokenBreakdown Tokens, List<string> Models, List<string> Unpriced, decimal CostUsd) Aggregate(
        IReadOnlyList<TranscriptLine> lines, IPriceTable prices)
    {
        var turns = SelectTurns(lines);

        long input = 0, output = 0, cacheWrite = 0, cacheRead = 0;
        var models = new List<string>();
        var unpricedModels = new List<string>();
        var costUsd = 0m;

        foreach (var turn in turns)
        {
            var usage = turn.Usage;
            if (usage is not null)
            {
                input += usage.InputTokens;
                output += usage.OutputTokens;
                cacheWrite += usage.CacheCreationInputTokens;
                cacheRead += usage.CacheReadInputTokens;
            }

            var model = turn.Model;
            if (model is null)
            {
                continue;
            }

            if (!models.Contains(model))
            {
                models.Add(model);
            }

            var price = prices.TryGet(model);
            if (price is null)
            {
                if (!unpricedModels.Contains(model))
                {
                    unpricedModels.Add(model);
                }

                continue;
            }

            if (usage is null)
            {
                continue;
            }

            costUsd +=
                (usage.InputTokens * price.Input
                 + usage.OutputTokens * price.Output
                 + usage.CacheCreationInputTokens * price.CacheWrite
                 + usage.CacheReadInputTokens * price.CacheRead)
                / 1_000_000m;
        }

        var tokens = new TokenBreakdown(input, output, cacheWrite, cacheRead, input + output + cacheWrite + cacheRead);
        return (tokens, models, unpricedModels, costUsd);
    }

    private static (DateTimeOffset? StartedAt, DateTimeOffset? EndedAt, long DurationMs) TimeRange(
        IReadOnlyList<TranscriptLine> lines)
    {
        var timestamps = lines.Where(l => l.Timestamp is not null).Select(l => l.Timestamp!.Value).ToList();
        DateTimeOffset? startedAt = timestamps.Count > 0 ? timestamps.Min() : null;
        DateTimeOffset? endedAt = timestamps.Count > 0 ? timestamps.Max() : null;
        var durationMs = startedAt is not null && endedAt is not null
            ? (long)(endedAt.Value - startedAt.Value).TotalMilliseconds
            : 0;
        return (startedAt, endedAt, durationMs);
    }
}
