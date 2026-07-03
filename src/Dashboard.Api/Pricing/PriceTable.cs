namespace Dashboard.Api.Pricing;

/// <summary>
/// Bundled, built-in per-model price table (USD per 1,000,000 tokens).
/// Configurable overrides are out of scope for this story (#6).
///
/// Cache-write is priced at 1.25x the input rate and cache-read at 0.10x the
/// input rate — the standard Anthropic ephemeral-cache economics convention,
/// applied consistently across models. Rates sourced from the Claude model
/// catalog as of 2026-07-03; cost figures are an estimate, not a billed
/// figure.
/// </summary>
internal sealed class PriceTable : IPriceTable
{
    private static readonly IReadOnlyDictionary<string, ModelPrice> Rates = new Dictionary<string, ModelPrice>
    {
        ["claude-opus-4-8"] = new(5.00m, 25.00m, 6.25m, 0.50m),
        ["claude-opus-4-7"] = new(5.00m, 25.00m, 6.25m, 0.50m),
        ["claude-opus-4-6"] = new(5.00m, 25.00m, 6.25m, 0.50m),
        ["claude-sonnet-5"] = new(3.00m, 15.00m, 3.75m, 0.30m),
        ["claude-sonnet-4-6"] = new(3.00m, 15.00m, 3.75m, 0.30m),
        ["claude-fable-5"] = new(10.00m, 50.00m, 12.50m, 1.00m),
        ["claude-haiku-4-5"] = new(1.00m, 5.00m, 1.25m, 0.10m),
    };

    public ModelPrice? TryGet(string model) => Rates.GetValueOrDefault(model);
}
