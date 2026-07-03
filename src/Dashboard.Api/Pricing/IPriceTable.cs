namespace Dashboard.Api.Pricing;

/// <summary>
/// Per-model USD rates, expressed per 1,000,000 tokens.
/// </summary>
internal sealed record ModelPrice(decimal Input, decimal Output, decimal CacheWrite, decimal CacheRead);

internal interface IPriceTable
{
    ModelPrice? TryGet(string model);
}
