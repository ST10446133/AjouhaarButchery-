namespace AjouhaarSalesStockWebsite.Models;

public class Product
{
    public int ProductID { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public decimal QuantityOnHand { get; set; }
    public decimal ReorderLevel { get; set; }
    public bool IsActive { get; set; } = true;
    public DateOnly LastUpdated { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    public bool IsLowStock => IsActive && QuantityOnHand <= ReorderLevel;
}
