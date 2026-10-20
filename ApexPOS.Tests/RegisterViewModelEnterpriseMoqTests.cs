using ApexPOS.Domain.Entities;
using ApexPOS.Domain.Enums;
using ApexPOS.Services;
using ApexPOS.Wpf.ViewModels;
using FluentAssertions;
using Moq;
using Xunit;

namespace ApexPOS.Tests;

/// <summary>
/// Enterprise-grade pure unit tests for RegisterViewModel using Moq for dependency isolation
/// and FluentAssertions for expressive assertions.
/// Follows Arrange-Act-Assert (AAA) pattern strictly.
/// </summary>
public class RegisterViewModelEnterpriseMoqTests
{
    private readonly Mock<IProductService> _mockProductService;
    private readonly Mock<ICategoryService> _mockCategoryService;
    private readonly Mock<IOrderService> _mockOrderService;
    private readonly Mock<ICustomerService> _mockCustomerService;
    private readonly Mock<IReceiptService> _mockReceiptService;
    private readonly Mock<ISettingsService> _mockSettingsService;
    private readonly Mock<IAuthService> _mockAuthService;

    private const int TestCashierId = 1;

    public RegisterViewModelEnterpriseMoqTests()
    {
        _mockProductService = new Mock<IProductService>();
        _mockCategoryService = new Mock<ICategoryService>();
        _mockOrderService = new Mock<IOrderService>();
        _mockCustomerService = new Mock<ICustomerService>();
        _mockReceiptService = new Mock<IReceiptService>();
        _mockSettingsService = new Mock<ISettingsService>();
        _mockAuthService = new Mock<IAuthService>();

        // Default mock behaviors
        _mockSettingsService.Setup(s => s.GetLoyaltySettings()).Returns(new LoyaltySettings { EnableLoyalty = false });
        _mockSettingsService.Setup(s => s.GetProfile()).Returns(new StoreProfile());
        _mockProductService.Setup(p => p.GetAll()).Returns(new List<Product>());
        _mockProductService.Setup(p => p.GetCrossSellingSuggestions(It.IsAny<List<int>>(), It.IsAny<int>())).Returns(new List<Product>());
        _mockCategoryService.Setup(c => c.GetAll()).Returns(new List<Category>());
        _mockCustomerService.Setup(c => c.GetAll()).Returns(new List<Customer>());
        _mockOrderService.Setup(o => o.GetHeldOrders()).Returns(new List<Order>());
        _mockReceiptService.Setup(r => r.GenerateReceiptText(It.IsAny<Order>())).Returns("Sample Receipt Text");
    }

    private RegisterViewModel CreateViewModel()
    {
        return new RegisterViewModel(
            _mockProductService.Object,
            _mockCategoryService.Object,
            _mockOrderService.Object,
            _mockCustomerService.Object,
            _mockReceiptService.Object,
            _mockSettingsService.Object,
            _mockAuthService.Object)
        {
            CashierId = TestCashierId
        };
    }

    #region Cart Operations & Totals Tests

    [Fact]
    public void AddToCart_WhenProductSelected_AppendsItemAndRecalculatesTotals()
    {
        // Arrange
        var viewModel = CreateViewModel();
        const decimal productPrice = 150.00m;
        var product = new Product
        {
            Id = 101,
            SKU = "BEV-001",
            Name = "Signature Latte",
            SellingPrice = productPrice,
            CostPrice = 60.00m,
            StockQuantity = 20,
            IsActive = true
        };

        // Act
        viewModel.AddToCart(product);

        // Assert
        viewModel.CartItems.Should().ContainSingle();
        var item = viewModel.CartItems.First();
        item.ProductId.Should().Be(product.Id);
        item.Quantity.Should().Be(1);
        viewModel.CartGrandTotal.Should().Be(productPrice);
        viewModel.CartTaxTotal.Should().Be(0m, "no tax should be charged");
        viewModel.CartItemCount.Should().Be(1);
    }

    [Fact]
    public void AddToCart_SameProductMultipleTimes_IncrementsQuantityRatherThanDuplicateLine()
    {
        // Arrange
        var viewModel = CreateViewModel();
        var product = new Product
        {
            Id = 102,
            SKU = "SNK-001",
            Name = "Cheese Sandwich",
            SellingPrice = 120.00m,
            IsActive = true
        };

        // Act
        viewModel.AddToCart(product);
        viewModel.AddToCart(product);

        // Assert
        viewModel.CartItems.Should().ContainSingle();
        viewModel.CartItems.First().Quantity.Should().Be(2);
        viewModel.CartGrandTotal.Should().Be(240.00m);
    }

    #endregion

    #region Undo Cart Action Stack Tests

    [Fact]
    public void UndoLastItem_WhenCartHasItems_RemovesLastAddedItemViaHistoryStack()
    {
        // Arrange
        var viewModel = CreateViewModel();
        var productA = new Product { Id = 1, SKU = "A", Name = "Item A", SellingPrice = 50.00m, IsActive = true };
        var productB = new Product { Id = 2, SKU = "B", Name = "Item B", SellingPrice = 80.00m, IsActive = true };

        viewModel.AddToCart(productA);
        viewModel.AddToCart(productB);

        // Act - Undo last added item (Product B)
        viewModel.UndoLastItem();

        // Assert
        viewModel.CartItems.Should().ContainSingle();
        viewModel.CartItems.First().ProductId.Should().Be(productA.Id);
        viewModel.CartGrandTotal.Should().Be(50.00m);
    }

    [Fact]
    public void UndoLastItem_WhenCartIsEmpty_DoesNotThrowAndMaintainsStableState()
    {
        // Arrange
        var viewModel = CreateViewModel();

        // Act
        Action act = () => viewModel.UndoLastItem();

        // Assert
        act.Should().NotThrow();
        viewModel.CartItems.Should().BeEmpty();
        viewModel.CartGrandTotal.Should().Be(0m);
    }

    #endregion

    #region Parking & Resuming Held Orders Tests

    [Fact]
    public void ParkCurrentOrder_WithCartItems_InvokesHoldOrderAndClearsCart()
    {
        // Arrange
        var viewModel = CreateViewModel();
        var product = new Product { Id = 5, SKU = "P5", Name = "Product 5", SellingPrice = 200.00m, IsActive = true };
        viewModel.AddToCart(product);

        var createdOrder = new Order { Id = 42, InvoiceNumber = "INV-000042", GrandTotal = 200.00m };
        _mockOrderService
            .Setup(o => o.CreateOrder(It.IsAny<int>(), It.IsAny<int?>(), It.IsAny<List<OrderItem>>(), It.IsAny<decimal>()))
            .Returns(createdOrder);

        // Act
        viewModel.ParkCurrentOrder();

        // Assert
        _mockOrderService.Verify(o => o.CreateOrder(TestCashierId, It.IsAny<int?>(), It.IsAny<List<OrderItem>>(), It.IsAny<decimal>()), Times.Once);
        _mockOrderService.Verify(o => o.HoldOrder(createdOrder.Id, It.IsAny<string>()), Times.Once);
        viewModel.CartItems.Should().BeEmpty("cart must be cleared so cashier can serve the next customer");
    }

    [Fact]
    public void ResumeHeldOrderFromRegister_WhenSelected_InvokesResumeOrderAndLoadsItemsToCart()
    {
        // Arrange
        var viewModel = CreateViewModel();
        var heldOrder = new Order
        {
            Id = 55,
            InvoiceNumber = "INV-000055",
            OrderStatus = OrderStatus.OnHold,
            Items = new List<OrderItem>
            {
                new OrderItem { ProductId = 10, SKU = "P10", ProductName = "Item 10", Quantity = 2, UnitPrice = 75.00m }
            }
        };

        // Act
        viewModel.ResumeHeldOrderFromRegister(heldOrder);

        // Assert
        _mockOrderService.Verify(o => o.ResumeOrder(heldOrder.Id), Times.Once);
        viewModel.CartItems.Should().ContainSingle();
        viewModel.CartItems.First().ProductId.Should().Be(10);
        viewModel.CartItems.First().Quantity.Should().Be(2);
        viewModel.CartGrandTotal.Should().Be(150.00m);
    }

    #endregion

    #region Partial Payment with Debt vs Without Customer Tests

    [Fact]
    public void ProcessPayment_UnderpaymentWithSelectedCustomer_InvokesCompletePaymentWithDebt()
    {
        // Arrange - ₹200.00 bill, ₹150.00 paid, ₹50.00 debt
        var viewModel = CreateViewModel();
        var product = new Product { Id = 20, SKU = "P20", Name = "Coffee Beans", SellingPrice = 200.00m, IsActive = true };
        viewModel.AddToCart(product);

        var customer = new Customer { Id = 7, Name = "Venkatesh", StoreCredit = 0m };
        viewModel.SelectCustomer(customer);
        viewModel.PaymentAmount = 150.00m; // Cashier tenders ₹150

        var createdOrder = new Order { Id = 88, InvoiceNumber = "INV-000088", GrandTotal = 200.00m };
        _mockOrderService
            .Setup(o => o.CreateOrder(It.IsAny<int>(), It.IsAny<int?>(), It.IsAny<List<OrderItem>>(), It.IsAny<decimal>()))
            .Returns(createdOrder);
        _mockCustomerService.Setup(c => c.GetById(customer.Id)).Returns(customer);

        // Act
        viewModel.ProcessPayment();

        // Assert
        _mockOrderService.Verify(o => o.CompletePaymentWithDebt(createdOrder.Id, PaymentMethod.Cash, 150.00m, 50.00m), Times.Once);
        _mockCustomerService.Verify(c => c.AddCredit(customer.Id, 50.00m), Times.Once);
    }

    [Fact]
    public void ProcessPayment_UnderpaymentWithoutCustomer_BlocksCheckoutAndSetsWarningMessage()
    {
        // Arrange - ₹200.00 bill, ₹150.00 tendered, NO customer selected
        var viewModel = CreateViewModel();
        var product = new Product { Id = 21, SKU = "P21", Name = "Tea Leaves", SellingPrice = 200.00m, IsActive = true };
        viewModel.AddToCart(product);
        viewModel.PaymentAmount = 150.00m; // Less than Grand Total

        // Act
        viewModel.ProcessPayment();

        // Assert
        _mockOrderService.Verify(o => o.CreateOrder(It.IsAny<int>(), It.IsAny<int?>(), It.IsAny<List<OrderItem>>(), It.IsAny<decimal>()), Times.Never);
        viewModel.PaymentStatus.Should().Contain("requires selecting a customer");
    }

    #endregion

    #region Void Cart & Audit Logging Tests

    [Fact]
    public void ConfirmVoid_WithActiveCart_InvokesLogVoidAuditAndClearsCart()
    {
        // Arrange
        var viewModel = CreateViewModel();
        var productA = new Product { Id = 1, SKU = "A", Name = "Item A", SellingPrice = 100.00m, IsActive = true };
        var productB = new Product { Id = 2, SKU = "B", Name = "Item B", SellingPrice = 150.00m, IsActive = true };
        viewModel.AddToCart(productA);
        viewModel.AddToCart(productB);

        viewModel.OpenVoidModal();
        viewModel.VoidReason = "Counter cancelled by supervisor";

        // Act
        viewModel.ConfirmVoid();

        // Assert
        _mockOrderService.Verify(o => o.LogVoidAudit(
            TestCashierId,
            2,
            250.00m,
            "Counter cancelled by supervisor"), Times.Once);

        viewModel.CartItems.Should().BeEmpty();
        viewModel.ShowVoidModal.Should().BeFalse();
    }

    #endregion

    #region Sales Person & Staff Loyalty Points Tests

    [Fact]
    public void SelectSalesPerson_UpdatesProperties_And_CalculatesStaffPoints()
    {
        // Arrange
        var viewModel = CreateViewModel();
        var salesPerson = new User
        {
            Id = 9,
            Username = "ravi",
            FullName = "Ravi Kumar",
            Role = UserRole.SalesPerson,
            LoyaltyPoints = 40.0m
        };

        // Act
        viewModel.SelectSalesPerson(salesPerson);
        viewModel.AddToCart(new Product { Id = 101, SKU = "P101", Name = "Watch", SellingPrice = 500.00m, IsActive = true });

        // Assert
        viewModel.SelectedSalesPerson.Should().NotBeNull();
        viewModel.SelectedSalesPerson!.FullName.Should().Be("Ravi Kumar");
        viewModel.SalesPersonSearchText.Should().Be("Ravi Kumar");
        viewModel.IsSalesPersonDropdownOpen.Should().BeFalse();
        // 500 / 250 default rate = 2 pts
        viewModel.SalesPersonPointsEarnedThisSale.Should().Be(2.0m);
    }

    [Fact]
    public void ProcessPayment_WithSalesPerson_AttributesOrderAndAwardsLoyaltyPoints()
    {
        // Arrange
        var viewModel = CreateViewModel();
        var salesPerson = new User
        {
            Id = 12,
            Username = "sales2",
            FullName = "Priya Sharma",
            Role = UserRole.SalesPerson,
            LoyaltyPoints = 20.0m
        };
        viewModel.SelectSalesPerson(salesPerson);

        var product = new Product { Id = 50, SKU = "P50", Name = "Headphones", SellingPrice = 300.00m, IsActive = true };
        viewModel.AddToCart(product);
        viewModel.PaymentAmount = 300.00m;

        // 300 / 250 = 1.0 point
        var createdOrder = new Order
        {
            Id = 99,
            InvoiceNumber = "INV-000099",
            GrandTotal = 300.00m,
            SalesPersonId = 12,
            SalesPersonName = "Priya Sharma",
            SalesPersonLoyaltyPointsEarned = 1.0m
        };

        _mockOrderService
            .Setup(o => o.CreateOrder(
                It.IsAny<int>(),
                It.IsAny<int?>(),
                It.IsAny<List<OrderItem>>(),
                It.IsAny<decimal>(),
                12,
                "Priya Sharma",
                1.0m))
            .Returns(createdOrder);

        // Act
        viewModel.ProcessPayment();

        // Assert
        _mockOrderService.Verify(o => o.CreateOrder(
            TestCashierId,
            null,
            It.IsAny<List<OrderItem>>(),
            0m,
            12,
            "Priya Sharma",
            1.0m), Times.Once);

        _mockAuthService.Verify(a => a.AddLoyaltyPoints(12, 1.0m), Times.Once);
        salesPerson.LoyaltyPoints.Should().Be(21.0m);
    }

    [Fact]
    public void ClearSalesPerson_ResetsSelectionAndSearchText()
    {
        // Arrange
        var viewModel = CreateViewModel();
        var salesPerson = new User { Id = 5, FullName = "Staff User", Role = UserRole.SalesPerson };
        viewModel.SelectSalesPerson(salesPerson);

        // Act
        viewModel.ClearSalesPerson();

        // Assert
        viewModel.SelectedSalesPerson.Should().BeNull();
        viewModel.SalesPersonSearchText.Should().BeEmpty();
    }

    #endregion
}
