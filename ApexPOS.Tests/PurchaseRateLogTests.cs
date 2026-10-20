using System;
using System.Collections.Generic;
using System.Linq;
using ApexPOS.Data;
using ApexPOS.Domain.Entities;
using ApexPOS.Services;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ApexPOS.Tests;

public class PurchaseRateLogTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ApexDbContext _db;

    public PurchaseRateLogTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<ApexDbContext>()
            .UseSqlite(_connection)
            .Options;

        _db = new ApexDbContext(options);
        _db.Database.EnsureCreated();

        var user = new User { Id = 1, Username = "admin", FullName = "Admin", PasswordHash = "hash", Role = ApexPOS.Domain.Enums.UserRole.Admin };
        var supplier = new Supplier { Id = 1, Name = "Britannia Distributors", ContactPerson = "Ramesh" };
        var category = new Category { Id = 1, Name = "Biscuits & Snacks", SupplierId = 1 };
        var product = new Product
        {
            Id = 1,
            Name = "Oreo Biscuits 120g",
            SKU = "SNK-OREO-01",
            Barcode = "8901234567890",
            CategoryId = 1,
            SupplierId = 1,
            CostPrice = 8.90m,
            SellingPrice = 10.00m,
            StockQuantity = 100,
            ReorderLevel = 15,
            Unit = "packet"
        };

        _db.Users.Add(user);
        _db.Suppliers.Add(supplier);
        _db.Categories.Add(category);
        _db.Products.Add(product);
        _db.SaveChanges();
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public void LogRateChange_CalculatesDifferencesAndMargin_Accurately()
    {
        // Arrange: Oreo biscuits MRP 10, initially cost 8.90, later cost becomes 9.00
        var service = new PurchaseRateLogService(_db);

        // Act: 2 months later purchase rate changes to 9.00
        var effectiveDate = DateTime.Today;
        var log = service.LogRateChange(
            productId: 1,
            newCostPrice: 9.00m,
            sellingPrice: 10.00m,
            supplierId: 1,
            source: "Supplier Price Revision",
            notes: "Vendor increased price by 10 paise",
            effectiveDate: effectiveDate,
            userId: 1,
            userName: "Admin");

        // Assert
        log.Should().NotBeNull();
        log.OldCostPrice.Should().Be(8.90m);
        log.NewCostPrice.Should().Be(9.00m);
        log.SellingPrice.Should().Be(10.00m);
        log.RateDifference.Should().Be(0.10m);
        log.IsRateIncrease.Should().BeTrue();
        log.IsRateDecrease.Should().BeFalse();
        log.ProfitMargin.Should().Be(10.0m); // (10 - 9)/10 = 10%

        // Verify product current cost was updated
        var updatedProduct = _db.Products.Find(1);
        updatedProduct!.CostPrice.Should().Be(9.00m);
    }

    [Fact]
    public void ProductServiceSave_AutomaticallyRecordsPurchaseRateLog_WhenCostPriceChanges()
    {
        // Arrange
        var productService = new ProductService(_db);

        var product = _db.Products.Find(1)!;
        product.CostPrice = 9.20m;

        // Act
        productService.Save(product);

        // Assert
        var logs = _db.PurchaseRateLogs.Where(l => l.ProductId == 1).ToList();
        logs.Should().Contain(l => l.OldCostPrice == 8.90m && l.NewCostPrice == 9.20m);
    }

    [Fact]
    public void ReceivePurchaseOrder_UpdatesProductCostPrice_AndRecordsPurchaseRateLog()
    {
        // Arrange
        var purchaseService = new PurchaseService(_db);

        var items = new List<PurchaseOrderItem>
        {
            new PurchaseOrderItem
            {
                ProductId = 1,
                ProductName = "Oreo Biscuits 120g",
                Quantity = 50,
                UnitCost = 9.10m
            }
        };

        var po = purchaseService.CreatePurchaseOrder(1, "New batch order", items);

        // Act: Receive PO
        purchaseService.ReceivePurchaseOrder(po.Id, userId: 1);

        // Assert
        var updatedProduct = _db.Products.Find(1)!;
        updatedProduct.CostPrice.Should().Be(9.10m);
        updatedProduct.StockQuantity.Should().Be(150); // 100 + 50

        var logs = _db.PurchaseRateLogs.Where(l => l.ProductId == 1).ToList();
        logs.Should().Contain(l => l.NewCostPrice == 9.10m && l.Source.Contains(po.PONumber));
    }

    [Fact]
    public void GetAllLogs_FiltersByDateAndSearchQuery_Correctly()
    {
        // Arrange
        var service = new PurchaseRateLogService(_db);

        service.LogRateChange(1, 9.00m, effectiveDate: DateTime.Today.AddDays(-10), source: "PO-001");
        service.LogRateChange(1, 9.25m, effectiveDate: DateTime.Today.AddDays(-2), source: "PO-002");

        // Act: Filter last 5 days
        var recentLogs = service.GetAllLogs(startDate: DateTime.Today.AddDays(-5), endDate: DateTime.Today);

        // Assert
        recentLogs.Should().HaveCount(1);
        recentLogs.First().NewCostPrice.Should().Be(9.25m);

        // Search by query
        var searched = service.GetAllLogs(searchQuery: "PO-001");
        searched.Should().HaveCount(1);
        searched.First().NewCostPrice.Should().Be(9.00m);
    }
}
