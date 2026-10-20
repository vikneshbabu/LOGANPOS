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
/// Comprehensive enterprise unit and integration test suite for OrderService.
/// Validates invoice numbering, order creation with stock deductions, partial tender with customer debt,
/// held orders multi-tab lifecycle, stock restoration upon void/cancel, and audit logging.
/// </summary>
public class OrderServiceAndDebtEnterpriseTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ApexDbContext _db;
    private readonly OrderService _orderService;

    private const int ValidCashierId = 1;
    private const decimal OrderDiscount = 10.00m;

    public OrderServiceAndDebtEnterpriseTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<ApexDbContext>()
            .UseSqlite(_connection)
            .Options;

        _db = new ApexDbContext(options);
        _db.Database.EnsureCreated();
        DbSeeder.Seed(_db);

        _orderService = new OrderService(_db);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    #region Invoice Number Generation Tests

    [Fact]
    public void GenerateInvoiceNumber_WhenInvokedSequentially_ProducesFormattedSequentialInvoices()
    {
        // Arrange & Act
        var invoice1 = _orderService.GenerateInvoiceNumber();
        var invoice2 = _orderService.GenerateInvoiceNumber();

        // Assert
        invoice1.Should().StartWith("INV-");
        invoice2.Should().StartWith("INV-");
        invoice1.Should().NotBe(invoice2, "each invoice number must be unique in sequence");
    }

    #endregion

    #region Order Creation & Stock Deduction Tests

    [Fact]
    public void CreateOrder_WithValidItems_CalculatesTotalsAndDeductsStockAccurately()
    {
        // Arrange
        var product = _db.Products.First(p => p.IsActive && p.StockQuantity >= 10);
        int initialStock = product.StockQuantity;
        const int orderQuantity = 2;
        decimal unitPrice = product.SellingPrice;

        var items = new List<OrderItem>
        {
            new OrderItem
            {
                ProductId = product.Id,
                ProductName = product.Name,
                Quantity = orderQuantity,
                UnitPrice = unitPrice,
                UnitCost = product.CostPrice,
                DiscountAmount = 0m
            }
        };

        // Act
        var order = _orderService.CreateOrder(ValidCashierId, customerId: null, items, OrderDiscount);

        // Assert
        order.Should().NotBeNull();
        order.Id.Should().BeGreaterThan(0);
        order.SubTotal.Should().Be(unitPrice * orderQuantity);
        order.TaxTotal.Should().Be(0m, "zero-tax policy must be adhered to");
        order.DiscountTotal.Should().Be(OrderDiscount);
        order.GrandTotal.Should().Be((unitPrice * orderQuantity) - OrderDiscount);
        order.OrderStatus.Should().Be(OrderStatus.Active);
        order.PaymentStatus.Should().Be(PaymentStatus.Pending);

        // Verify stock deduction in database
        var updatedProduct = _db.Products.AsNoTracking().First(p => p.Id == product.Id);
        updatedProduct.StockQuantity.Should().Be(initialStock - orderQuantity);
    }

    [Fact]
    public void CreateOrder_NullItemsList_ThrowsArgumentNullException()
    {
        // Arrange
        List<OrderItem>? nullItems = null;

        // Act
        Action act = () => _orderService.CreateOrder(ValidCashierId, customerId: null, nullItems!, OrderDiscount);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("items");
    }

    [Fact]
    public void CreateOrder_EmptyItemsList_ThrowsInvalidOperationException()
    {
        // Arrange
        var emptyItems = new List<OrderItem>();

        // Act
        Action act = () => _orderService.CreateOrder(ValidCashierId, customerId: null, emptyItems, OrderDiscount);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*zero items*");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void CreateOrder_NonPositiveCashierId_ThrowsArgumentOutOfRangeException(int invalidCashierId)
    {
        // Arrange
        var items = new List<OrderItem>
        {
            new OrderItem { ProductId = 1, ProductName = "Item", Quantity = 1, UnitPrice = 100m }
        };

        // Act
        Action act = () => _orderService.CreateOrder(invalidCashierId, customerId: null, items, 0m);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("cashierId");
    }

    #endregion

    #region Payment & Debt Handling Tests (Rupee ₹200 Total, Tender ₹150, Debt ₹50)

    [Fact]
    public void CompletePayment_FullTenderAmount_MarksOrderCompletedAndPaid()
    {
        // Arrange - Create Order of ₹200.00
        var product = _db.Products.First(p => p.IsActive);
        const decimal totalAmount = 200.00m;
        var items = new List<OrderItem>
        {
            new OrderItem { ProductId = product.Id, ProductName = product.Name, Quantity = 1, UnitPrice = totalAmount }
        };
        var order = _orderService.CreateOrder(ValidCashierId, customerId: null, items, 0m);

        // Act
        _orderService.CompletePayment(order.Id, PaymentMethod.Cash, totalAmount);

        // Assert
        var updatedOrder = _db.Orders.Include(o => o.Payments).First(o => o.Id == order.Id);
        updatedOrder.PaymentStatus.Should().Be(PaymentStatus.Paid);
        updatedOrder.OrderStatus.Should().Be(OrderStatus.Completed);
        updatedOrder.Payments.Should().ContainSingle();
        updatedOrder.Payments.First().AmountPaid.Should().Be(totalAmount);
        updatedOrder.Payments.First().ChangeGiven.Should().Be(0m);
    }

    [Fact]
    public void CompletePaymentWithDebt_UnderpaidCashWithRemainingDebt_RecordsTenderAndDebtPaymentsAndCompletesOrder()
    {
        // Arrange - Purchase amount ₹200.00, customer pays ₹150.00, ₹50.00 added to debt
        var customer = _db.Customers.First(c => c.IsActive);
        var product = _db.Products.First(p => p.IsActive);
        const decimal billTotal = 200.00m;
        const decimal tenderedCash = 150.00m;
        const decimal remainingDebt = 50.00m;

        var items = new List<OrderItem>
        {
            new OrderItem { ProductId = product.Id, ProductName = product.Name, Quantity = 1, UnitPrice = billTotal }
        };
        var order = _orderService.CreateOrder(ValidCashierId, customer.Id, items, 0m);

        // Act - Tender ₹150 cash, record ₹50 to customer debt
        _orderService.CompletePaymentWithDebt(order.Id, PaymentMethod.Cash, tenderedCash, remainingDebt);

        // Assert
        var completedOrder = _db.Orders.Include(o => o.Payments).First(o => o.Id == order.Id);
        completedOrder.OrderStatus.Should().Be(OrderStatus.Completed);
        completedOrder.PaymentStatus.Should().Be(PaymentStatus.Paid);

        // Verify two payment entries: One for Cash tender, One for Store Credit / Debt
        completedOrder.Payments.Should().HaveCount(2);

        var cashPayment = completedOrder.Payments.First(p => p.PaymentMethod == PaymentMethod.Cash);
        cashPayment.AmountPaid.Should().Be(tenderedCash);

        var debtPayment = completedOrder.Payments.First(p => p.PaymentMethod == PaymentMethod.StoreCredit);
        debtPayment.AmountPaid.Should().Be(remainingDebt);
        debtPayment.ReferenceNo.Should().Be("ADDED_TO_CUSTOMER_DEBT");
    }

    #endregion

    #region Held Orders Multi-Tab Lifecycle & Stock Restoration Tests

    [Fact]
    public void HoldOrder_ActiveOrder_TransitionsStatusToOnHoldWithNotes()
    {
        // Arrange
        var product = _db.Products.First(p => p.IsActive);
        var items = new List<OrderItem>
        {
            new OrderItem { ProductId = product.Id, ProductName = product.Name, Quantity = 1, UnitPrice = product.SellingPrice }
        };
        var order = _orderService.CreateOrder(ValidCashierId, customerId: null, items, 0m);
        const string holdNotes = "Customer stepped out to ATM";

        // Act
        _orderService.HoldOrder(order.Id, holdNotes);

        // Assert
        var heldOrder = _db.Orders.First(o => o.Id == order.Id);
        heldOrder.OrderStatus.Should().Be(OrderStatus.OnHold);
        heldOrder.HoldNotes.Should().Be(holdNotes);

        var heldList = _orderService.GetHeldOrders();
        heldList.Should().Contain(o => o.Id == order.Id);
    }

    [Fact]
    public void ResumeOrder_HeldOrder_RestoresStatusToActiveAndClearsHoldNotes()
    {
        // Arrange
        var product = _db.Products.First(p => p.IsActive);
        var items = new List<OrderItem>
        {
            new OrderItem { ProductId = product.Id, ProductName = product.Name, Quantity = 1, UnitPrice = product.SellingPrice }
        };
        var order = _orderService.CreateOrder(ValidCashierId, customerId: null, items, 0m);
        _orderService.HoldOrder(order.Id, "Parking cart");

        // Act
        _orderService.ResumeOrder(order.Id);

        // Assert
        var resumedOrder = _db.Orders.First(o => o.Id == order.Id);
        resumedOrder.OrderStatus.Should().Be(OrderStatus.Active);
        resumedOrder.HoldNotes.Should().BeNull();
    }

    [Fact]
    public void CancelHeldOrder_HeldOrder_RestoresStockQuantityAndEmitsAuditLog()
    {
        // Arrange
        var product = _db.Products.First(p => p.IsActive);
        int initialStock = product.StockQuantity;
        const int qty = 3;

        var items = new List<OrderItem>
        {
            new OrderItem { ProductId = product.Id, ProductName = product.Name, Quantity = qty, UnitPrice = product.SellingPrice }
        };
        var order = _orderService.CreateOrder(ValidCashierId, customerId: null, items, 0m);
        _orderService.HoldOrder(order.Id, "Temporary hold");

        // Act
        _orderService.CancelHeldOrder(order.Id, ValidCashierId);

        // Assert
        var cancelledOrder = _db.Orders.First(o => o.Id == order.Id);
        cancelledOrder.OrderStatus.Should().Be(OrderStatus.Voided);

        // Verify stock is restored back to original level
        var productAfterCancel = _db.Products.AsNoTracking().First(p => p.Id == product.Id);
        productAfterCancel.StockQuantity.Should().Be(initialStock, "cancelled held order must return reserved stock to inventory");

        // Verify audit log
        var audit = _db.AuditLogs.FirstOrDefault(a => a.Action == "CANCEL_HELD_ORDER" && a.Details.Contains(order.InvoiceNumber));
        audit.Should().NotBeNull();
    }

    [Fact]
    public void LogVoidAudit_ActiveCartVoided_WritesAuditEntryWithCorrectItemCountAndTotal()
    {
        // Arrange
        const int itemCount = 4;
        const decimal cartTotal = 480.00m;
        const string voidReason = "Customer changed mind at billing counter";

        // Act
        _orderService.LogVoidAudit(ValidCashierId, itemCount, cartTotal, voidReason);

        // Assert
        var audit = _db.AuditLogs.FirstOrDefault(a => a.Action == "VOID_BILL" && a.Details.Contains("4 items") && a.Details.Contains("480.00"));
        audit.Should().NotBeNull();
        audit!.Details.Should().Contain(voidReason);
    }

    #endregion
}
