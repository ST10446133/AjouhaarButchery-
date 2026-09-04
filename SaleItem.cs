namespace AjouhaarSalesStockWebsite.Models;

public class SaleItem
{
    public int ProductID { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public decimal QuantitySold { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal => QuantitySold * UnitPrice;
}
