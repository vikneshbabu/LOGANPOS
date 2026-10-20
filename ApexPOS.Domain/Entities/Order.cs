using ApexPOS.Domain.Enums;

namespace ApexPOS.Domain.Entities;

public class Order
{
    public int Id { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public int CashierId { get; set; }
    public int? CustomerId { get; set; }
    public int? SalesPersonId { get; set; }
    public string? SalesPersonName { get; set; }
    public decimal SalesPersonLoyaltyPointsEarned { get; set; } = 0.0m;
    public decimal SubTotal { get; set; }
    public decimal TaxTotal { get; set; }
    public decimal DiscountTotal { get; set; }
    public decimal GrandTotal { get; set; }
    public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.Pending;
    public OrderStatus OrderStatus { get; set; } = OrderStatus.Active;
    public string? HoldNotes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }

    // Navigation
    public User Cashier { get; set; } = null!;
    public User? SalesPerson { get; set; }
    public Customer? Customer { get; set; }
    public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();

    // Computed
    public decimal TotalPaid => Payments?.Sum(p => p.AmountPaid) ?? 0;
    public decimal BalanceDue => GrandTotal - TotalPaid;
}
