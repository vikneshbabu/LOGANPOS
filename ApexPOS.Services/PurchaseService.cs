using ApexPOS.Data;
using ApexPOS.Domain.Entities;
using ApexPOS.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace ApexPOS.Services;

public class PurchaseService
{
    private readonly ApexDbContext _db;

    public PurchaseService(ApexDbContext db) => _db = db;

    // --- Suppliers / Distributors ---
    public List<Supplier> GetAllSuppliers() =>
        _db.Suppliers.Where(s => s.IsActive).OrderBy(s => s.Name).ToList();

    public Supplier? GetSupplier(int id) =>
        _db.Suppliers.Include(s => s.PurchaseOrders).FirstOrDefault(s => s.Id == id);

    public void SaveSupplier(Supplier supplier)
    {
        if (supplier.Id == 0)
            _db.Suppliers.Add(supplier);
        else
            _db.Suppliers.Update(supplier);
        _db.SaveChanges();
    }

    public void DeleteSupplier(int id)
    {
        var supplier = _db.Suppliers.Find(id);
        if (supplier != null)
        {
            supplier.IsActive = false;
            _db.SaveChanges();
        }
    }

    // --- Purchase Orders ---
    public List<PurchaseOrder> GetAllPurchaseOrders() =>
        _db.PurchaseOrders
            .Include(p => p.Supplier)
            .Include(p => p.Items)
            .OrderByDescending(p => p.CreatedAt)
            .ToList();

    public PurchaseOrder? GetPurchaseOrder(int id) =>
        _db.PurchaseOrders
            .Include(p => p.Supplier)
            .Include(p => p.Items)
            .FirstOrDefault(p => p.Id == id);

    public PurchaseOrder CreatePurchaseOrder(int supplierId, string notes, List<PurchaseOrderItem> items)
    {
        int count = _db.PurchaseOrders.Count() + 1;
        var po = new PurchaseOrder
        {
            PONumber = $"PO-{DateTime.UtcNow:yyyyMM}-{count:D4}",
            SupplierId = supplierId,
            Notes = notes,
            TotalAmount = items.Sum(i => i.Quantity * i.UnitCost),
            Status = PurchaseOrderStatus.Ordered,
            CreatedAt = DateTime.UtcNow
        };

        _db.PurchaseOrders.Add(po);
        _db.SaveChanges();

        foreach (var item in items)
        {
            item.PurchaseOrderId = po.Id;
            _db.PurchaseOrderItems.Add(item);
        }
        _db.SaveChanges();

        return po;
    }

    public void ReceivePurchaseOrder(int poId, int userId)
    {
        var po = _db.PurchaseOrders.Include(p => p.Items).FirstOrDefault(p => p.Id == poId);
        if (po == null || po.Status == PurchaseOrderStatus.Received) return;

        po.Status = PurchaseOrderStatus.Received;
        po.ReceivedAt = DateTime.UtcNow;

        // Auto-increment stock
        foreach (var item in po.Items)
        {
            var product = _db.Products.Find(item.ProductId);
            if (product != null)
            {
                var preStock = product.StockQuantity;
                product.StockQuantity += item.Quantity;

                decimal oldCost = product.CostPrice;
                if (item.UnitCost > 0)
                {
                    product.CostPrice = item.UnitCost;
                }

                _db.InventoryTransactions.Add(new InventoryTransaction
                {
                    ProductId = product.Id,
                    Type = InventoryTransactionType.Restock,
                    QuantityChanged = item.Quantity,
                    PreStock = preStock,
                    PostStock = product.StockQuantity,
                    Reason = $"Received from Distributor via PO {po.PONumber}",
                    UserId = userId
                });

                if (item.UnitCost > 0)
                {
                    _db.PurchaseRateLogs.Add(new PurchaseRateLog
                    {
                        ProductId = product.Id,
                        ProductName = product.Name,
                        SKU = product.SKU,
                        Barcode = product.Barcode,
                        OldCostPrice = oldCost,
                        NewCostPrice = item.UnitCost,
                        SellingPrice = product.SellingPrice,
                        SupplierId = po.SupplierId,
                        SupplierName = po.Supplier?.Name ?? product.DistributorName,
                        Source = $"PO {po.PONumber}",
                        Notes = $"Received {item.Quantity} {product.Unit} @ ₹{item.UnitCost:F2}/unit",
                        EffectiveDate = po.ReceivedAt ?? DateTime.UtcNow,
                        CreatedAt = DateTime.UtcNow,
                        UserId = userId
                    });
                }
            }
        }

        _db.SaveChanges();
    }
}
