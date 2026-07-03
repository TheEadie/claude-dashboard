using Dashboard.Api.Models;
using Dashboard.Api.Pricing;
using Dashboard.Api.Transcripts;

namespace Dashboard.Api.Sessions;

internal static class SessionAnalyzer
{
    private const string SyntheticModel = "<synthetic>";

    public static SessionSummary Analyze(string sessionId, IReadOnlyList<TranscriptLine> lines, IPriceTable prices)
    {
        // Sub-agent/sidechain records are out of scope for this story (#4) —
        // exclude them from every computation below.
        var mainLine = lines.Where(l => !l.IsSidechain).ToList();

        var project = mainLine
            .Select(l => l.Cwd)
            .FirstOrDefault(cwd => !string.IsNullOrEmpty(cwd));
        project = string.IsNullOrEmpty(project) ? string.Empty : Path.GetFileName(project.TrimEnd('/'));

        var timestamps = mainLine.Where(l => l.Timestamp is not null).Select(l => l.Timestamp!.Value).ToList();
        DateTimeOffset? startedAt = timestamps.Count > 0 ? timestamps.Min() : null;
        DateTimeOffset? endedAt = timestamps.Count > 0 ? timestamps.Max() : null;
        var durationMs = startedAt is not null && endedAt is not null
            ? (long)(endedAt.Value - startedAt.Value).TotalMilliseconds
            : 0;

        var title = mainLine.Select(l => l.AiTitle).LastOrDefault(t => !string.IsNullOrEmpty(t)) ?? sessionId;

        // CRITICAL: a single logical assistant turn is emitted as multiple
        // JSONL lines sharing the same message.id, each repeating an
        // identical usage block. Group by message.id and keep one usage
        // block per group, or totals inflate several-fold (verified against
        // real transcripts — see plan §1.4).
        var turns = mainLine
            .Where(l => l.Type == "assistant" && l.Message?.Id is not null)
            .GroupBy(l => l.Message!.Id)
            .Select(g => g.First().Message!)
            // "<synthetic>" turns are Claude Code placeholders with zero
            // usage, not real model turns — exclude them from tokens,
            // models, and cost.
            .Where(m => m.Model != SyntheticModel)
            .ToList();

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
}
