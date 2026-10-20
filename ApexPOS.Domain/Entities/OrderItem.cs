namespace ApexPOS.Domain.Entities;

public class OrderItem
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public int ProductId { get; set; }
    public string SKU { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public decimal UnitCost { get; set; }
    public int Quantity { get; set; } = 1;
    public decimal DiscountAmount { get; set; }
    public decimal TaxRate { get; set; } = 0.0m;
    public decimal TaxAmount => 0m;
    public decimal TotalPrice => (UnitPrice * Quantity) - DiscountAmount;

    // Navigation
    public Order Order { get; set; } = null!;
    public Product Product { get; set; } = null!;
}
