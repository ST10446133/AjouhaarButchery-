using AjouhaarSalesStockWebsite.Models;

namespace AjouhaarSalesStockWebsite.Tests;

public class ProductTests
{
    [Fact]
    public void IsLowStock_IsTrue_WhenQuantityEqualsReorderLevel()
    {
        Product product = new()
        {
            IsActive = true,
            QuantityOnHand = 5m,
            ReorderLevel = 5m
        };

        Assert.True(product.IsLowStock);
    }

    [Fact]
    public void IsLowStock_IsFalse_WhenQuantityIsAboveReorderLevel()
    {
        Product product = new()
        {
            IsActive = true,
            QuantityOnHand = 5.1m,
            ReorderLevel = 5m
        };

        Assert.False(product.IsLowStock);
    }

    [Fact]
    public void IsLowStock_IsFalse_ForInactiveProduct()
    {
        Product product = new()
        {
            IsActive = false,
            QuantityOnHand = 0m,
            ReorderLevel = 5m
        };

        Assert.False(product.IsLowStock);
    }
}
