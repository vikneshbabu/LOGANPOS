using ApexPOS.Domain.Enums;

namespace ApexPOS.Domain.Entities;

public class InventoryTransaction
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public InventoryTransactionType Type { get; set; }
    public int QuantityChanged { get; set; }
    public int PreStock { get; set; }
    public int PostStock { get; set; }
    public string Reason { get; set; } = string.Empty;
    public int UserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public Product Product { get; set; } = null!;
    public User User { get; set; } = null!;
}
