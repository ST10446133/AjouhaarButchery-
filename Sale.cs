namespace AjouhaarSalesStockWebsite.Models;

public class Sale
{
    public int SaleID { get; set; }
    public string ReceiptNumber { get; set; } = string.Empty;
    public string CustomerName { get; set; } = "Walk-in Customer";
    public string CustomerContact { get; set; } = "N/A";
    public string Cashier { get; set; } = string.Empty;
    public DateTime SaleDate { get; set; } = DateTime.Now;
    public string PaymentMethod { get; set; } = "Cash";
    public List<SaleItem> Items { get; set; } = new();
    public decimal TotalAmount => Items.Sum(item => item.LineTotal);
}
