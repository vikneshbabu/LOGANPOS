using ApexPOS.Data;
using ApexPOS.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ApexPOS.Services;

public class PurchaseRateLogSummary
{
    public int TotalRevisions { get; set; }
    public int UniqueProductsTracked { get; set; }
    public int RateIncreases { get; set; }
    public int RateDecreases { get; set; }
    public DateTime? LatestRevisionDate { get; set; }
}

public class PurchaseRateLogService
{
    private readonly ApexDbContext _db;

    public PurchaseRateLogService(ApexDbContext db)
    {
        _db = db;
    }

    public List<PurchaseRateLog> GetAllLogs(
        int? productId = null,
        int? supplierId = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        string? searchQuery = null)
    {
        var query = _db.PurchaseRateLogs
            .Include(r => r.Product)
            .Include(r => r.Supplier)
            .AsQueryable();

        if (productId.HasValue && productId.Value > 0)
        {
            query = query.Where(r => r.ProductId == productId.Value);
        }

        if (supplierId.HasValue && supplierId.Value > 0)
        {
            query = query.Where(r => r.SupplierId == supplierId.Value);
        }

        if (startDate.HasValue)
        {
            var start = startDate.Value.Date;
            query = query.Where(r => r.EffectiveDate >= start);
        }

        if (endDate.HasValue)
        {
            var end = endDate.Value.Date.AddDays(1).AddTicks(-1);
            query = query.Where(r => r.EffectiveDate <= end);
        }

        if (!string.IsNullOrWhiteSpace(searchQuery))
        {
            var q = searchQuery.Trim().ToLower();
            query = query.Where(r =>
                r.ProductName.ToLower().Contains(q) ||
                r.SKU.ToLower().Contains(q) ||
                r.Barcode.ToLower().Contains(q) ||
                r.SupplierName.ToLower().Contains(q) ||
                r.Source.ToLower().Contains(q) ||
                r.Notes.ToLower().Contains(q));
        }

        return query.OrderByDescending(r => r.EffectiveDate)
                    .ThenByDescending(r => r.Id)
                    .ToList();
    }

    public List<PurchaseRateLog> GetProductHistory(int productId)
    {
        return _db.PurchaseRateLogs
            .Where(r => r.ProductId == productId)
            .OrderByDescending(r => r.EffectiveDate)
            .ThenByDescending(r => r.Id)
            .ToList();
    }

    public PurchaseRateLog LogRateChange(
        int productId,
        decimal newCostPrice,
        decimal? sellingPrice = null,
        decimal? mrp = null,
        int? supplierId = null,
        string source = "Manual Entry",
        string notes = "",
        DateTime? effectiveDate = null,
        int? userId = null,
        string? userName = null)
    {
        var product = _db.Products.Include(p => p.Supplier).FirstOrDefault(p => p.Id == productId);
        if (product == null)
            throw new InvalidOperationException($"Product with ID {productId} not found.");

        decimal oldCost = product.CostPrice;
        decimal currentSelling = sellingPrice ?? product.SellingPrice;
        decimal currentMRP = mrp ?? (product.MRP > 0 ? product.MRP : currentSelling);

        var supplier = supplierId.HasValue
            ? _db.Suppliers.Find(supplierId.Value)
            : product.Supplier;

        var log = new PurchaseRateLog
        {
            ProductId = product.Id,
            ProductName = product.Name,
            SKU = product.SKU,
            Barcode = product.Barcode,
            OldCostPrice = oldCost,
            NewCostPrice = newCostPrice,
            SellingPrice = currentSelling,
            MRP = currentMRP,
            SupplierId = supplier?.Id ?? product.SupplierId,
            SupplierName = supplier?.Name ?? product.DistributorName,
            Source = string.IsNullOrWhiteSpace(source) ? "Manual Entry" : source.Trim(),
            Notes = notes?.Trim() ?? string.Empty,
            EffectiveDate = effectiveDate ?? DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UserId = userId,
            UserName = string.IsNullOrWhiteSpace(userName) ? "Staff" : userName.Trim()
        };

        // Update product current cost price, selling price, and MRP if specified
        product.CostPrice = newCostPrice;
        if (sellingPrice.HasValue && sellingPrice.Value > 0)
        {
            product.SellingPrice = sellingPrice.Value;
        }
        if (mrp.HasValue && mrp.Value > 0)
        {
            product.MRP = mrp.Value;
        }
        if (supplier != null)
        {
            product.SupplierId = supplier.Id;
        }

        _db.PurchaseRateLogs.Add(log);
        _db.SaveChanges();

        return log;
    }

    public PurchaseRateLogSummary GetSummaryStats()
    {
        var logs = _db.PurchaseRateLogs.ToList();
        return new PurchaseRateLogSummary
        {
            TotalRevisions = logs.Count,
            UniqueProductsTracked = logs.Select(l => l.ProductId).Distinct().Count(),
            RateIncreases = logs.Count(l => l.NewCostPrice > l.OldCostPrice),
            RateDecreases = logs.Count(l => l.NewCostPrice < l.OldCostPrice),
            LatestRevisionDate = logs.OrderByDescending(l => l.EffectiveDate).FirstOrDefault()?.EffectiveDate
        };
    }
}
