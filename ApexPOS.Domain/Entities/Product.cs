namespace ApexPOS.Domain.Entities;

public class Product
{
    public int Id { get; set; }
    public string SKU { get; set; } = string.Empty;
    public string Barcode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int CategoryId { get; set; }
    public decimal CostPrice { get; set; }
    public decimal SellingPrice { get; set; }
    public decimal MRP { get; set; }
    public int StockQuantity { get; set; }
    public int ReorderLevel { get; set; } = 10;
    public decimal TaxRate { get; set; } = 0.0m;
    public string Unit { get; set; } = "piece";
    public int? SupplierId { get; set; } // Associated Distributor / Supplier
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public Category Category { get; set; } = null!;
    public Supplier? Supplier { get; set; }
    public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
    public ICollection<InventoryTransaction> InventoryTransactions { get; set; } = new List<InventoryTransaction>();
    public ICollection<PurchaseRateLog> PurchaseRateLogs { get; set; } = new List<PurchaseRateLog>();

    // Computed / Helper
    public string DistributorName => Supplier?.Name ?? Category?.Supplier?.Name ?? "—";

    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public decimal PurchasePrice
    {
        get => CostPrice;
        set => CostPrice = value;
    }

    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public decimal PurchaseRate
    {
        get => CostPrice;
        set => CostPrice = value;
    }

    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public decimal EffectiveMRP => MRP > 0 ? MRP : SellingPrice;

    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public decimal TotalStockCost => StockQuantity * CostPrice;

    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public decimal TotalRetailValue => StockQuantity * SellingPrice;

    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public decimal TotalProjectedProfit => TotalRetailValue - TotalStockCost;

    // Computed
    public bool IsLowStock => StockQuantity <= ReorderLevel;
    public decimal ProfitPerUnit => SellingPrice - CostPrice;
    public decimal Margin => SellingPrice > 0 ? ((SellingPrice - CostPrice) / SellingPrice) * 100 : 0;

    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public decimal MarginPercent => Margin;

    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public decimal MarginAmount => SellingPrice - CostPrice;
}

