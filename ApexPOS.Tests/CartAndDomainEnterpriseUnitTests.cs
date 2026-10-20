using ApexPOS.Domain.Entities;
using ApexPOS.Domain.Enums;
using ApexPOS.Services;
using ApexPOS.Wpf.ViewModels;
using FluentAssertions;
using Xunit;

namespace ApexPOS.Tests;

/// <summary>
/// Enterprise-grade unit tests for Cart and Domain models.
/// Uses FluentAssertions for expressive assertions and strictly follows Arrange-Act-Assert (AAA).
/// </summary>
public class CartAndDomainEnterpriseUnitTests
{
    private const decimal StandardPrice = 120.00m;
    private const decimal StandardCost = 45.00m;
    private const int StandardQuantity = 3;
    private const decimal StandardDiscount = 20.00m;

    #region CartItem Calculations & Boundary Tests

    [Fact]
    public void LineSubtotal_ValidQuantityAndUnitPrice_CalculatesExpectedSubtotal()
    {
        // Arrange
        var item = new CartItem
        {
            ProductId = 101,
            Sku = "BEV-001",
            ProductName = "Masala Chai",
            UnitPrice = StandardPrice,
            UnitCost = StandardCost,
            Quantity = StandardQuantity,
            DiscountAmount = 0m
        };

        // Act
        decimal subtotal = item.LineSubtotal;

        // Assert
        subtotal.Should().Be(StandardPrice * StandardQuantity);
    }

    [Fact]
    public void LineTotal_WithValidDiscount_DeductsDiscountAccurately()
    {
        // Arrange
        var item = new CartItem
        {
            ProductId = 102,
            Sku = "BEV-002",
            ProductName = "Filter Coffee",
            UnitPrice = StandardPrice,
            UnitCost = StandardCost,
            Quantity = StandardQuantity,
            DiscountAmount = StandardDiscount
        };

        // Act
        decimal lineTotal = item.LineTotal;

        // Assert
        decimal expectedTotal = (StandardPrice * StandardQuantity) - StandardDiscount;
        lineTotal.Should().Be(expectedTotal);
    }

    [Fact]
    public void LineTax_UnderAllConditions_IsAlwaysZeroAsPerNoTaxRequirement()
    {
        // Arrange
        var item = new CartItem
        {
            ProductId = 103,
            Sku = "SNK-001",
            ProductName = "Samosa Platter",
            UnitPrice = 250.00m,
            UnitCost = 80.00m,
            Quantity = 4,
            TaxRate = 0.18m, // Even if tax rate is provided in legacy payload
            DiscountAmount = 10.00m
        };

        // Act
        decimal tax = item.LineTax;

        // Assert
        tax.Should().Be(0m, "zero-tax policy must be enforced across all line items");
    }

    [Fact]
    public void LineTotal_DiscountExceedingSubtotal_ClampsToZeroWithoutNegativeTotal()
    {
        // Arrange
        const decimal smallUnitPrice = 50.00m;
        const int singleQuantity = 1;
        const decimal excessiveDiscount = 80.00m; // Exceeds 50.00 subtotal

        var item = new CartItem
        {
            ProductId = 104,
            Sku = "SNK-002",
            ProductName = "Biscuit Pack",
            UnitPrice = smallUnitPrice,
            Quantity = singleQuantity,
            DiscountAmount = excessiveDiscount
        };

        // Act
        decimal lineTotal = item.LineTotal;

        // Assert
        lineTotal.Should().Be(0m, "line total should not be negative when discount exceeds subtotal");
    }

    [Fact]
    public void Quantity_ZeroOrNegativeBoundary_AutomaticallyEnforcesMinimumOfOne()
    {
        // Arrange
        var item = new CartItem
        {
            ProductId = 105,
            Sku = "BEV-003",
            ProductName = "Cold Drink",
            UnitPrice = 40.00m,
            Quantity = 0
        };

        // Act & Assert - Should clamp to 1
        item.Quantity.Should().Be(1, "minimum quantity for a line item is 1");
        item.LineSubtotal.Should().Be(40.00m);
    }

    [Fact]
    public void LineSubtotal_LargeNumericBoundary_CalculatesAccuratelyWithoutOverflow()
    {
        // Arrange
        const decimal highUnitPrice = 99999.99m;
        const int highQuantity = 10000;

        var item = new CartItem
        {
            ProductId = 106,
            Sku = "BULK-001",
            ProductName = "Bulk Wholesale Item",
            UnitPrice = highUnitPrice,
            Quantity = highQuantity
        };

        // Act
        decimal subtotal = item.LineSubtotal;

        // Assert
        subtotal.Should().Be(highUnitPrice * highQuantity);
    }

    #endregion

    #region Customer StoreCredit & Loyalty Calculations

    [Fact]
    public void Customer_AddLoyaltyPoints_IncrementsTotalAccurately()
    {
        // Arrange
        const decimal initialPoints = 15.5m;
        const decimal pointsToAdd = 10.0m;
        var customer = new Customer
        {
            Id = 1,
            Name = "Ramesh Kumar",
            LoyaltyPoints = initialPoints
        };

        // Act
        customer.LoyaltyPoints += pointsToAdd;

        // Assert
        customer.LoyaltyPoints.Should().Be(initialPoints + pointsToAdd);
    }

    [Fact]
    public void Customer_StoreCreditDebt_TracksOutstandingBalanceAccurately()
    {
        // Arrange
        const decimal initialDebt = 150.00m;
        const decimal additionalDebt = 50.00m;
        var customer = new Customer
        {
            Id = 2,
            Name = "Suresh Patel",
            StoreCredit = initialDebt
        };

        // Act
        customer.StoreCredit += additionalDebt;

        // Assert
        customer.StoreCredit.Should().Be(200.00m);
    }

    #endregion

    #region StoreProfile & LoyaltySettings Defaults

    [Fact]
    public void StoreProfile_DefaultConfiguration_UsesRupeeSymbolAndZeroTax()
    {
        // Arrange & Act
        var profile = new StoreProfile();

        // Assert
        profile.CurrencySymbol.Should().Be("₹");
        profile.TaxRate.Should().Be(0.0m);
        profile.StoreName.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void LoyaltySettings_DefaultValues_ConfiguresRupeeRatioAccurately()
    {
        // Arrange & Act
        var loyalty = new LoyaltySettings();

        // Assert
        loyalty.AmountPerPoint.Should().Be(250.00m, "default rule specifies ₹250 purchase earns 1 point");
        loyalty.PointRedeemValue.Should().Be(1.00m);
        loyalty.EnableLoyalty.Should().BeTrue();
    }

    #endregion
}
