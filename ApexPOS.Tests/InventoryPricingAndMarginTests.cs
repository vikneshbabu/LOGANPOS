using System;
using System.Linq;
using ApexPOS.Data;
using ApexPOS.Domain.Entities;
using ApexPOS.Services;
using ApexPOS.Wpf.ViewModels;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ApexPOS.Tests;

public class InventoryPricingAndMarginTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ApexDbContext _db;
    private readonly ProductService _productService;
    private readonly CategoryService _categoryService;
    private readonly PurchaseService _purchaseService;

    public InventoryPricingAndMarginTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<ApexDbContext>()
            .UseSqlite(_connection)
            .Options;

        _db = new ApexDbContext(options);
        _db.Database.EnsureCreated();

        var supplier = new Supplier { Id = 1, Name = "Parle Distributor" };
        var category = new Category { Id = 1, Name = "Biscuits", SupplierId = 1 };
        var product = new Product
        {
            Id = 1,
            Name = "Parle-G 250g",
            SKU = "BIS-PARLE-01",
            Barcode = "8901234567800",
            CategoryId = 1,
            SupplierId = 1,
            CostPrice = 20.00m,
            SellingPrice = 25.00m,
            MRP = 30.00m,
            StockQuantity = 50,
            Unit = "packet"
        };

        _db.Suppliers.Add(supplier);
        _db.Categories.Add(category);
        _db.Products.Add(product);
        _db.SaveChanges();

        _productService = new ProductService(_db);
        _categoryService = new CategoryService(_db);
        _purchaseService = new PurchaseService(_db);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public void ProductEntity_PricingAndMarginCalculations_AreAccurate()
    {
        // Arrange
        var product = new Product
        {
            CostPrice = 80.00m,
            SellingPrice = 100.00m,
            MRP = 120.00m,
            StockQuantity = 10
        };

        // Assert
        product.PurchasePrice.Should().Be(80.00m);
        product.PurchaseRate.Should().Be(80.00m);
        product.EffectiveMRP.Should().Be(120.00m);
        product.ProfitPerUnit.Should().Be(20.00m); // 100 - 80
        product.Margin.Should().Be(20.0m);         // (20 / 100) * 100
        product.MarginAmount.Should().Be(20.00m);
        product.MarginPercent.Should().Be(20.0m);
        product.TotalStockCost.Should().Be(800.00m);
        product.TotalRetailValue.Should().Be(1000.00m);
        product.TotalProjectedProfit.Should().Be(200.00m);
    }

    [Fact]
    public void InventoryViewModel_MarginCalculation_BasedOnSellingPriceMinusPurchasePrice()
    {
        // Arrange
        var vm = new InventoryViewModel(_productService, _categoryService, _purchaseService);
        vm.LoadData();

        // Act: Start new product
        vm.NewProduct();
        vm.EditPurchasePrice = 8.90m;
        vm.EditSellingPrice = 10.00m;
        vm.EditMRP = 12.00m;

        // Assert
        vm.EditProfitPerUnit.Should().Be(1.10m);       // 10.00 - 8.90 = 1.10
        vm.EditMarginPercent.Should().Be(11.0m);       // (1.10 / 10.00) * 100 = 11.0%
        vm.EditMarkupPercent.Should().Be(12.4m);       // (1.10 / 8.90) * 100 = 12.359... -> 12.4%
        vm.EditDiscountFromMRP.Should().Be(16.7m);     // ((12 - 10) / 12) * 100 = 16.666... -> 16.7%
    }

    [Fact]
    public void InventoryViewModel_AutoDefaults_MRPAndSellingPriceWhenZero()
    {
        // Arrange
        var vm = new InventoryViewModel(_productService, _categoryService, _purchaseService);
        vm.NewProduct();

        // Act 1: Enter MRP first
        vm.EditMRP = 50.00m;

        // Assert: Selling price defaults to MRP
        vm.EditSellingPrice.Should().Be(50.00m);

        // Act 2: Set Purchase Price
        vm.EditPurchasePrice = 40.00m;
        vm.EditProfitPerUnit.Should().Be(10.00m);
        vm.EditMarginPercent.Should().Be(20.0m);
    }

    [Fact]
    public void InventoryViewModel_SaveProduct_PersistsPurchasePriceSellingPriceAndMRP()
    {
        // Arrange
        var vm = new InventoryViewModel(_productService, _categoryService, _purchaseService);
        vm.LoadData();
        vm.NewProduct();

        vm.EditName = "Britannia Good Day 100g";
        vm.EditSku = "BIS-GD-01";
        vm.EditBarcode = "8901234567899";
        vm.EditPurchasePrice = 18.00m;
        vm.EditSellingPrice = 22.00m;
        vm.EditMRP = 25.00m;
        vm.EditStockQuantity = 40;
        vm.EditReorderLevel = 10;
        vm.EditUnit = "packet";
        vm.EditSupplierId = 1;
        vm.EditCategoryId = 1;

        // Act
        vm.SaveProduct();

        // Assert
        var saved = _db.Products.FirstOrDefault(p => p.SKU == "BIS-GD-01");
        saved.Should().NotBeNull();
        saved!.CostPrice.Should().Be(18.00m);
        saved.SellingPrice.Should().Be(22.00m);
        saved.MRP.Should().Be(25.00m);
        saved.EffectiveMRP.Should().Be(25.00m);
        saved.ProfitPerUnit.Should().Be(4.00m);
        saved.Margin.Should().Be(18.18181818181818181818181818m);
    }
}
