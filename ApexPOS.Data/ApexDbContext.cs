using ApexPOS.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ApexPOS.Data;

public class ApexDbContext : DbContext
{
    public ApexDbContext(DbContextOptions<ApexDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<InventoryTransaction> InventoryTransactions => Set<InventoryTransaction>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<Setting> Settings => Set<Setting>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<PurchaseOrder> PurchaseOrders => Set<PurchaseOrder>();
    public DbSet<PurchaseOrderItem> PurchaseOrderItems => Set<PurchaseOrderItem>();
    public DbSet<PurchaseRateLog> PurchaseRateLogs => Set<PurchaseRateLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Setting has composite key on Key
        modelBuilder.Entity<Setting>().HasKey(s => s.Key);

        // User
        modelBuilder.Entity<User>(e =>
        {
            e.HasIndex(u => u.Username).IsUnique();
            e.Property(u => u.Username).HasMaxLength(50).IsRequired();
            e.Property(u => u.PasswordHash).HasMaxLength(256).IsRequired();
            e.Property(u => u.PinCode).HasMaxLength(10);
            e.Property(u => u.FullName).HasMaxLength(100);
            e.Property(u => u.LoyaltyPoints).HasColumnType("decimal(18,2)").HasDefaultValue(0.0m);
        });

        // Category
        modelBuilder.Entity<Category>(e =>
        {
            e.Property(c => c.Name).HasMaxLength(100).IsRequired();
            e.Property(c => c.Icon).HasMaxLength(10);
            e.HasOne(c => c.Supplier).WithMany().HasForeignKey(c => c.SupplierId).OnDelete(DeleteBehavior.SetNull);
        });

        // Product
        modelBuilder.Entity<Product>(e =>
        {
            e.HasIndex(p => p.SKU).IsUnique();
            e.HasIndex(p => p.Barcode);
            e.Property(p => p.SKU).HasMaxLength(50).IsRequired();
            e.Property(p => p.Barcode).HasMaxLength(50);
            e.Property(p => p.Name).HasMaxLength(200).IsRequired();
            e.Property(p => p.Unit).HasMaxLength(50).HasDefaultValue("piece");
            e.Property(p => p.CostPrice).HasColumnType("decimal(18,2)");
            e.Property(p => p.SellingPrice).HasColumnType("decimal(18,2)");
            e.Property(p => p.MRP).HasColumnType("decimal(18,2)").HasDefaultValue(0.0m);
            e.Property(p => p.TaxRate).HasColumnType("decimal(5,4)");
            e.HasOne(p => p.Category).WithMany(c => c.Products).HasForeignKey(p => p.CategoryId);
            e.HasOne(p => p.Supplier).WithMany().HasForeignKey(p => p.SupplierId).OnDelete(DeleteBehavior.SetNull);
            e.Ignore(p => p.IsLowStock);
            e.Ignore(p => p.Margin);
            e.Ignore(p => p.ProfitPerUnit);
            e.Ignore(p => p.DistributorName);
        });

        // Customer
        modelBuilder.Entity<Customer>(e =>
        {
            e.HasIndex(c => c.Code).IsUnique();
            e.Property(c => c.Code).HasMaxLength(20).IsRequired();
            e.Property(c => c.Name).HasMaxLength(100).IsRequired();
            e.Property(c => c.Phone).HasMaxLength(20);
            e.Property(c => c.Email).HasMaxLength(100);
            e.Property(c => c.LoyaltyPoints).HasColumnType("decimal(18,2)");
            e.Property(c => c.StoreCredit).HasColumnType("decimal(18,2)");
        });

        // Order
        modelBuilder.Entity<Order>(e =>
        {
            e.HasIndex(o => o.InvoiceNumber).IsUnique();
            e.Property(o => o.InvoiceNumber).HasMaxLength(30).IsRequired();
            e.Property(o => o.SubTotal).HasColumnType("decimal(18,2)");
            e.Property(o => o.TaxTotal).HasColumnType("decimal(18,2)");
            e.Property(o => o.DiscountTotal).HasColumnType("decimal(18,2)");
            e.Property(o => o.GrandTotal).HasColumnType("decimal(18,2)");
            e.Property(o => o.SalesPersonName).HasMaxLength(100);
            e.Property(o => o.SalesPersonLoyaltyPointsEarned).HasColumnType("decimal(18,2)").HasDefaultValue(0.0m);
            e.HasOne(o => o.Cashier).WithMany(u => u.Orders).HasForeignKey(o => o.CashierId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(o => o.SalesPerson).WithMany().HasForeignKey(o => o.SalesPersonId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(o => o.Customer).WithMany(c => c.Orders).HasForeignKey(o => o.CustomerId).OnDelete(DeleteBehavior.SetNull);
            e.Ignore(o => o.TotalPaid);
            e.Ignore(o => o.BalanceDue);
        });

        // OrderItem
        modelBuilder.Entity<OrderItem>(e =>
        {
            e.Property(i => i.UnitPrice).HasColumnType("decimal(18,2)");
            e.Property(i => i.UnitCost).HasColumnType("decimal(18,2)");
            e.Property(i => i.DiscountAmount).HasColumnType("decimal(18,2)");
            e.Property(i => i.TaxRate).HasColumnType("decimal(5,4)");
            e.HasOne(i => i.Order).WithMany(o => o.Items).HasForeignKey(i => i.OrderId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(i => i.Product).WithMany(p => p.OrderItems).HasForeignKey(i => i.ProductId).OnDelete(DeleteBehavior.Restrict);
            e.Ignore(i => i.TaxAmount);
            e.Ignore(i => i.TotalPrice);
        });

        // Payment
        modelBuilder.Entity<Payment>(e =>
        {
            e.Property(p => p.AmountPaid).HasColumnType("decimal(18,2)");
            e.Property(p => p.ChangeGiven).HasColumnType("decimal(18,2)");
            e.Property(p => p.ReferenceNo).HasMaxLength(50);
            e.HasOne(p => p.Order).WithMany(o => o.Payments).HasForeignKey(p => p.OrderId).OnDelete(DeleteBehavior.Cascade);
        });

        // InventoryTransaction
        modelBuilder.Entity<InventoryTransaction>(e =>
        {
            e.Property(t => t.Reason).HasMaxLength(500);
            e.HasOne(t => t.Product).WithMany(p => p.InventoryTransactions).HasForeignKey(t => t.ProductId);
            e.HasOne(t => t.User).WithMany().HasForeignKey(t => t.UserId).OnDelete(DeleteBehavior.Restrict);
        });

        // AuditLog
        modelBuilder.Entity<AuditLog>(e =>
        {
            e.Property(a => a.Action).HasMaxLength(100).IsRequired();
            e.Property(a => a.Details).HasMaxLength(1000);
            e.HasOne(a => a.User).WithMany(u => u.AuditLogs).HasForeignKey(a => a.UserId).OnDelete(DeleteBehavior.Restrict);
        });

        // PurchaseRateLog
        modelBuilder.Entity<PurchaseRateLog>(e =>
        {
            e.HasIndex(r => r.ProductId);
            e.HasIndex(r => r.EffectiveDate);
            e.Property(r => r.ProductName).HasMaxLength(200).IsRequired();
            e.Property(r => r.SKU).HasMaxLength(50);
            e.Property(r => r.Barcode).HasMaxLength(50);
            e.Property(r => r.OldCostPrice).HasColumnType("decimal(18,2)");
            e.Property(r => r.NewCostPrice).HasColumnType("decimal(18,2)");
            e.Property(r => r.SellingPrice).HasColumnType("decimal(18,2)");
            e.Property(r => r.MRP).HasColumnType("decimal(18,2)").HasDefaultValue(0.0m);
            e.Property(r => r.SupplierName).HasMaxLength(150);
            e.Property(r => r.Source).HasMaxLength(100);
            e.Property(r => r.Notes).HasMaxLength(500);
            e.Property(r => r.UserName).HasMaxLength(100);
            e.HasOne(r => r.Product).WithMany(p => p.PurchaseRateLogs).HasForeignKey(r => r.ProductId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(r => r.Supplier).WithMany().HasForeignKey(r => r.SupplierId).OnDelete(DeleteBehavior.SetNull);
            e.Ignore(r => r.PurchasePrice);
            e.Ignore(r => r.OldPurchasePrice);
            e.Ignore(r => r.EffectiveMRP);
            e.Ignore(r => r.RateDifference);
            e.Ignore(r => r.IsRateIncrease);
            e.Ignore(r => r.IsRateDecrease);
            e.Ignore(r => r.PercentageChange);
            e.Ignore(r => r.MarginAmount);
            e.Ignore(r => r.ProfitMargin);
            e.Ignore(r => r.MarginPercent);
            e.Ignore(r => r.MarkupPercent);
            e.Ignore(r => r.RateDifferenceFormatted);
            e.Ignore(r => r.RateChangeColor);
            e.Ignore(r => r.RateChangeBackground);
        });
    }
}
