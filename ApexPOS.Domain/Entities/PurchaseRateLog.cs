namespace ApexPOS.Domain.Entities;

public class PurchaseRateLog
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string SKU { get; set; } = string.Empty;
    public string Barcode { get; set; } = string.Empty;

    public decimal OldCostPrice { get; set; }
    public decimal NewCostPrice { get; set; }
    public decimal SellingPrice { get; set; } // Selling Price at the time of purchase rate record
    public decimal MRP { get; set; }          // MRP at the time of purchase rate record

    public int? SupplierId { get; set; }
    public string SupplierName { get; set; } = string.Empty;

    public string Source { get; set; } = "Manual Entry"; // e.g. "Purchase Order #PO-...", "Inventory Edit", "Manual Entry"
    public string Notes { get; set; } = string.Empty;

    public DateTime EffectiveDate { get; set; } = DateTime.UtcNow;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public int? UserId { get; set; }
    public string UserName { get; set; } = string.Empty;

    // Navigation (optional, for EF Core relationship)
    public Product? Product { get; set; }
    public Supplier? Supplier { get; set; }

    // Aliases
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public decimal PurchasePrice => NewCostPrice;

    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public decimal OldPurchasePrice => OldCostPrice;

    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public decimal EffectiveMRP => MRP > 0 ? MRP : SellingPrice;

    // Computed Helpers
    public decimal RateDifference => NewCostPrice - OldCostPrice;
    public bool IsRateIncrease => RateDifference > 0;
    public bool IsRateDecrease => RateDifference < 0;
    public decimal PercentageChange => OldCostPrice > 0 ? (RateDifference / OldCostPrice) * 100 : 0;
    
    // Margin calculated based on Selling Price - Purchase Price
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public decimal MarginAmount => SellingPrice - NewCostPrice;
    
    public decimal ProfitMargin => SellingPrice > 0 ? ((SellingPrice - NewCostPrice) / SellingPrice) * 100 : 0;
    
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public decimal MarginPercent => ProfitMargin;

    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public decimal MarkupPercent => NewCostPrice > 0 ? ((SellingPrice - NewCostPrice) / NewCostPrice) * 100 : 0;

    public string RateDifferenceFormatted => RateDifference > 0 ? $"+₹{RateDifference:F2} (▲ {PercentageChange:F1}%)" : RateDifference < 0 ? $"-₹{Math.Abs(RateDifference):F2} (▼ {Math.Abs(PercentageChange):F1}%)" : "— Same Rate";
    public string RateChangeColor => IsRateIncrease ? "#DC2626" : IsRateDecrease ? "#16A34A" : "#64748B";
    public string RateChangeBackground => IsRateIncrease ? "#FEF2F2" : IsRateDecrease ? "#F0FDF4" : "#F1F5F9";
}

