using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ApexPOS.Data;
using ApexPOS.Domain.Entities;
using ApexPOS.Domain.Enums;
using ApexPOS.Services;
using FluentAssertions;
using Xunit;

namespace ApexPOS.Tests;

/// <summary>
/// Comprehensive enterprise unit and integration tests for ProductService.
/// Covers Happy Paths, Fuzzy Typo Matching, Acronyms, Edge Cases (Null/Empty/Whitespace),
/// Cross-Selling Suggestions, Quick Add Validations, and Exception Boundaries.
/// </summary>
public class ProductServiceEnterpriseTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ApexDbContext _db;
    private readonly ProductService _productService;

    private const int ValidUserId = 1;
    private const decimal ValidPrice = 180.00m;
    private const decimal ValidCost = 70.00m;
    private const int ValidStock = 50;

    public ProductServiceEnterpriseTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<ApexDbContext>()
            .UseSqlite(_connection)
            .Options;

        _db = new ApexDbContext(options);
        _db.Database.EnsureCreated();
        DbSeeder.Seed(_db);

        var coffeeCat = _db.Categories.First(c => c.IsActive);
        _db.Products.AddRange(
            new Product { Name = "Espresso", SKU = "COF-001", SellingPrice = 90m, CategoryId = coffeeCat.Id, IsActive = true, StockQuantity = 50 },
            new Product { Name = "Cappuccino", SKU = "COF-002", SellingPrice = 120m, CategoryId = coffeeCat.Id, IsActive = true, StockQuantity = 50 }
        );
        _db.SaveChanges();

        _productService = new ProductService(_db);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    #region Adaptive Search & Fuzzy Matching Tests

    [Fact]
    public void Search_ExactProductName_ReturnsMatchingProductWithTopPriority()
    {
        // Arrange
        const string query = "Espresso";

        // Act
        var results = _productService.Search(query);

        // Assert
        results.Should().NotBeEmpty();
        results.First().Name.Should().Be("Espresso");
    }

    [Fact]
    public void Search_DirectBarcodeOrSkuMatch_ReturnsExactItemImmediately()
    {
        // Arrange
        var product = _db.Products.First(p => p.IsActive);
        string exactSku = product.SKU;

        // Act
        var results = _productService.Search(exactSku);

        // Assert
        results.Should().ContainSingle();
        results.First().SKU.Should().Be(exactSku);
    }

    [Fact]
    public void Search_SingleCharacterTypo_MatchesProductGracefullyViaLevenshtein()
    {
        // Arrange - "Espreso" has 1 missing 's' from "Espresso"
        const string typoQuery = "Espreso";

        // Act
        var results = _productService.Search(typoQuery);

        // Assert
        results.Should().NotBeEmpty();
        results.Should().Contain(p => p.Name == "Espresso");
    }

    [Fact]
    public void Search_TwoCharacterTypo_MatchesTargetProduct()
    {
        // Arrange - "Capucino" has missing 'p' and missing 'c' from "Cappuccino"
        const string typoQuery = "Capucino";

        // Act
        var results = _productService.Search(typoQuery);

        // Assert
        results.Should().NotBeEmpty();
        results.Should().Contain(p => p.Name == "Cappuccino");
    }

    [Fact]
    public void Search_AcronymQuery_MatchesFullMultiWordTitle()
    {
        // Arrange - Add multi-word product "Cold Brew Coffee"
        var category = _db.Categories.First(c => c.IsActive);
        var multiWordProduct = new Product
        {
            Name = "Cold Brew Coffee",
            SKU = "BEV-CBC",
            Barcode = "99001122",
            SellingPrice = 140.00m,
            CostPrice = 50.00m,
            StockQuantity = 30,
            CategoryId = category.Id,
            IsActive = true
        };
        _db.Products.Add(multiWordProduct);
        _db.SaveChanges();

        // Act - Query by acronym "CBC"
        var results = _productService.Search("CBC");

        // Assert
        results.Should().NotBeEmpty();
        results.Should().Contain(p => p.Name == "Cold Brew Coffee");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Search_NullOrEmptyOrWhitespaceQuery_ReturnsAllActiveProductsSafely(string? invalidQuery)
    {
        // Arrange
        int totalActive = _db.Products.Count(p => p.IsActive);

        // Act
        var results = _productService.Search(invalidQuery!);

        // Assert
        results.Should().HaveCount(totalActive, "null or empty search query must return full catalog gracefully");
    }

    [Fact]
    public void Search_CompletelyUnrelatedQuery_ReturnsEmptyListWithoutCrashing()
    {
        // Arrange
        const string gibberish = "xyz987qzw_nonexistent";

        // Act
        var results = _productService.Search(gibberish);

        // Assert
        results.Should().BeEmpty();
    }

    #endregion

    #region Cross-Selling Recommendations Tests

    [Fact]
    public void GetCrossSellingSuggestions_EmptyCartList_ReturnsEmptyCollection()
    {
        // Arrange
        var emptyCart = new List<int>();

        // Act
        var suggestions = _productService.GetCrossSellingSuggestions(emptyCart);

        // Assert
        suggestions.Should().BeEmpty();
    }

    [Fact]
    public void GetCrossSellingSuggestions_NullCartList_ReturnsEmptyCollection()
    {
        // Arrange
        List<int>? nullCart = null;

        // Act
        var suggestions = _productService.GetCrossSellingSuggestions(nullCart!);

        // Assert
        suggestions.Should().BeEmpty();
    }

    [Fact]
    public void GetCrossSellingSuggestions_WhenCartHasItems_SuggestsRelevantActiveProducts()
    {
        // Arrange - Create historical order containing Product A and Product B
        var products = _db.Products.Where(p => p.IsActive).Take(3).ToList();
        var productA = products[0];
        var productB = products[1];

        var order = new Order
        {
            InvoiceNumber = "HIST-001",
            CashierId = ValidUserId,
            OrderStatus = OrderStatus.Completed,
            PaymentStatus = PaymentStatus.Paid,
            CreatedAt = DateTime.UtcNow.AddDays(-1)
        };
        _db.Orders.Add(order);
        _db.SaveChanges();

        _db.OrderItems.AddRange(
            new OrderItem { OrderId = order.Id, ProductId = productA.Id, ProductName = productA.Name, Quantity = 1, UnitPrice = productA.SellingPrice },
            new OrderItem { OrderId = order.Id, ProductId = productB.Id, ProductName = productB.Name, Quantity = 1, UnitPrice = productB.SellingPrice }
        );
        _db.SaveChanges();

        // Act - Customer now has Product A in active cart
        var suggestions = _productService.GetCrossSellingSuggestions(new List<int> { productA.Id }, maxResults: 3);

        // Assert
        suggestions.Should().NotBeEmpty();
        suggestions.Should().Contain(p => p.Id == productB.Id);
    }

    #endregion

    #region QuickAdd Product & Validation Boundaries

    [Fact]
    public void CreateQuickProduct_WithValidInputs_PersistsProductAndEmitsAuditLog()
    {
        // Arrange
        const string productName = "Artisan Sourdough Bun";
        var category = _db.Categories.First(c => c.IsActive);

        // Act
        var created = _productService.CreateQuickProduct(productName, ValidPrice, ValidCost, category.Id, ValidStock, ValidUserId);

        // Assert
        created.Should().NotBeNull();
        created.Id.Should().BeGreaterThan(0);
        created.Name.Should().Be(productName);
        created.SellingPrice.Should().Be(ValidPrice);
        created.StockQuantity.Should().Be(ValidStock);
        created.IsActive.Should().BeTrue();

        // Verify Audit Log is generated
        var audit = _db.AuditLogs.FirstOrDefault(a => a.Action == "QUICK_ADD_PRODUCT" && a.Details.Contains(productName));
        audit.Should().NotBeNull("auditing is mandatory for quick manual product creation");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void CreateQuickProduct_NullOrEmptyName_ThrowsArgumentNullException(string? invalidName)
    {
        // Arrange
        var category = _db.Categories.First(c => c.IsActive);

        // Act
        Action act = () => _productService.CreateQuickProduct(invalidName!, ValidPrice, ValidCost, category.Id, ValidStock, ValidUserId);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("name");
    }

    [Fact]
    public void CreateQuickProduct_NegativeSellingPrice_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        const decimal negativePrice = -50.00m;
        var category = _db.Categories.First(c => c.IsActive);

        // Act
        Action act = () => _productService.CreateQuickProduct("Invalid Price Item", negativePrice, ValidCost, category.Id, ValidStock, ValidUserId);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("sellingPrice");
    }

    [Fact]
    public void CreateQuickProduct_NegativeCostPrice_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        const decimal negativeCost = -20.00m;
        var category = _db.Categories.First(c => c.IsActive);

        // Act
        Action act = () => _productService.CreateQuickProduct("Invalid Cost Item", ValidPrice, negativeCost, category.Id, ValidStock, ValidUserId);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("costPrice");
    }

    #endregion

    #region Soft Delete & Restore Lifecycle

    [Fact]
    public void Delete_ActiveProduct_SetsIsActiveFalseAndAuditsAction()
    {
        // Arrange
        var product = _db.Products.First(p => p.IsActive);
        int productId = product.Id;

        // Act
        _productService.Delete(productId, ValidUserId, "Damaged stock discontinued");

        // Assert
        var deleted = _db.Products.AsNoTracking().First(p => p.Id == productId);
        deleted.IsActive.Should().BeFalse();

        var audit = _db.AuditLogs.FirstOrDefault(a => a.Action == "DELETE_PRODUCT" && a.Details.Contains(product.SKU));
        audit.Should().NotBeNull();
    }

    [Fact]
    public void Restore_DeletedProduct_RestoresActiveStatusAndAudits()
    {
        // Arrange
        var product = _db.Products.First(p => p.IsActive);
        int productId = product.Id;
        _productService.Delete(productId, ValidUserId);

        // Act
        _productService.Restore(productId, ValidUserId);

        // Assert
        var restored = _db.Products.AsNoTracking().First(p => p.Id == productId);
        restored.IsActive.Should().BeTrue();

        var audit = _db.AuditLogs.FirstOrDefault(a => a.Action == "RESTORE_PRODUCT" && a.Details.Contains(product.SKU));
        audit.Should().NotBeNull();
    }

    #endregion
}
