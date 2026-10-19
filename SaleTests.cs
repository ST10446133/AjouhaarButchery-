using AjouhaarSalesStockWebsite.Models;

namespace AjouhaarSalesStockWebsite.Tests;

public class SaleTests
{
    [Fact]
    public void SaleItem_LineTotal_IsQuantityTimesUnitPrice()
    {
        SaleItem item = new()
        {
            QuantitySold = 1.5m,
            UnitPrice = 120m
        };

        Assert.Equal(180m, item.LineTotal);
    }

    [Fact]
    public void Sale_TotalAmount_IsSumOfItemLineTotals()
    {
        Sale sale = new()
        {
            Items =
            [
                new SaleItem { QuantitySold = 1m, UnitPrice = 100m },
                new SaleItem { QuantitySold = 0.5m, UnitPrice = 80m }
            ]
        };

        Assert.Equal(140m, sale.TotalAmount);
    }
}
