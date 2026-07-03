using Dashboard.Api.Pricing;

namespace Dashboard.Api.Tests;

public class PriceTableTests
{
    [Fact]
    public void TryGet_KnownModel_ReturnsRates()
    {
        var table = new PriceTable();

        var price = table.TryGet("claude-opus-4-8");

        Assert.NotNull(price);
        Assert.Equal(5.00m, price!.Input);
        Assert.Equal(25.00m, price.Output);
        Assert.Equal(6.25m, price.CacheWrite);
        Assert.Equal(0.50m, price.CacheRead);
    }

    [Fact]
    public void TryGet_UnknownModel_ReturnsNull()
    {
        var table = new PriceTable();

        var price = table.TryGet("claude-experimental-x");

        Assert.Null(price);
    }
}
