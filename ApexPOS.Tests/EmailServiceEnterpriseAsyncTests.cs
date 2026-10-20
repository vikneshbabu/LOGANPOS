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
/// Comprehensive enterprise asynchronous unit and integration tests for EmailService.
/// Validates Daily Summary calculations, HTML/PlainText report formatting,
/// and asynchronous dispatch validation handling using public async Task.
/// </summary>
public class EmailServiceEnterpriseAsyncTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ApexDbContext _db;
    private readonly SettingsService _settingsService;
    private readonly EmailService _emailService;

    public EmailServiceEnterpriseAsyncTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<ApexDbContext>()
            .UseSqlite(_connection)
            .Options;

        _db = new ApexDbContext(options);
        _db.Database.EnsureCreated();
        DbSeeder.Seed(_db);

        _settingsService = new SettingsService(_db);
        _emailService = new EmailService(_db, _settingsService);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    #region Daily Summary Calculations Tests

    [Fact]
    public void BuildDailySummary_WithCompletedOrders_CalculatesSalesAndProfitsAccurately()
    {
        // Arrange - Create completed order today
        var product = _db.Products.First(p => p.IsActive);
        const int qty = 2;
        decimal unitPrice = product.SellingPrice;
        decimal unitCost = product.CostPrice;
        decimal expectedProfit = (unitPrice - unitCost) * qty;

        var order = new Order
        {
            InvoiceNumber = "MAIL-TEST-001",
            CashierId = 1,
            OrderStatus = OrderStatus.Completed,
            PaymentStatus = PaymentStatus.Paid,
            SubTotal = unitPrice * qty,
            GrandTotal = unitPrice * qty,
            CreatedAt = DateTime.UtcNow
        };
        _db.Orders.Add(order);
        _db.SaveChanges();

        _db.OrderItems.Add(new OrderItem
        {
            OrderId = order.Id,
            ProductId = product.Id,
            ProductName = product.Name,
            Quantity = qty,
            UnitPrice = unitPrice,
            UnitCost = unitCost
        });

        _db.Payments.Add(new Payment
        {
            OrderId = order.Id,
            PaymentMethod = PaymentMethod.Cash,
            AmountPaid = unitPrice * qty
        });
        _db.SaveChanges();

        // Act
        var summary = _emailService.BuildDailySummary(DateTime.UtcNow.Date);

        // Assert
        summary.Should().NotBeNull();
        summary.TotalSales.Should().BeGreaterOrEqualTo(unitPrice * qty);
        summary.TotalProfit.Should().BeGreaterOrEqualTo(expectedProfit);
        summary.TotalOrders.Should().BeGreaterThan(0);
        summary.CashSales.Should().BeGreaterOrEqualTo(unitPrice * qty);
    }

    [Fact]
    public void GenerateReportEmail_WithSummary_FormatsHtmlAndPlainTextWithStoreNameAndRupee()
    {
        // Arrange
        var summary = _emailService.BuildDailySummary(DateTime.UtcNow.Date);

        // Act
        var (subject, htmlBody, plainText) = _emailService.GenerateReportEmail(summary);

        // Assert
        subject.Should().NotBeNullOrWhiteSpace();
        subject.Should().Contain("Daily Business Summary");

        htmlBody.Should().Contain("ApexPOS");
        htmlBody.Should().Contain("Payment Breakdown");
        htmlBody.Should().Contain("₹");

        plainText.Should().Contain("DAILY REPORT");
        plainText.Should().Contain("Total Sales");
    }

    #endregion

    #region Asynchronous Dispatch Validation Tests (public async Task)

    [Fact]
    public async Task SendDailyReportAsync_MissingSmtpHost_ReturnsFailureResultAsync()
    {
        // Arrange - Clear SMTP host
        _settingsService.Set("Email_SmtpHost", "");
        _settingsService.Set("Email_SenderEmail", "store@example.com");
        _settingsService.Set("Email_SenderPassword", "app-password");
        _settingsService.Set("Email_RecipientEmail", "owner@example.com");

        // Act
        var (success, message) = await _emailService.SendDailyReportAsync();

        // Assert
        success.Should().BeFalse();
        message.Should().Contain("SMTP Host is not configured");
    }

    [Fact]
    public async Task SendDailyReportAsync_MissingSenderEmail_ReturnsFailureResultAsync()
    {
        // Arrange - Clear sender email
        _settingsService.Set("Email_SmtpHost", "smtp.gmail.com");
        _settingsService.Set("Email_SenderEmail", "");
        _settingsService.Set("Email_SenderPassword", "app-password");
        _settingsService.Set("Email_RecipientEmail", "owner@example.com");

        // Act
        var (success, message) = await _emailService.SendDailyReportAsync();

        // Assert
        success.Should().BeFalse();
        message.Should().Contain("Sender Email address is not configured");
    }

    [Fact]
    public async Task SendDailyReportAsync_MissingRecipientEmail_ReturnsFailureResultAsync()
    {
        // Arrange - Clear recipient email
        _settingsService.Set("Email_SmtpHost", "smtp.gmail.com");
        _settingsService.Set("Email_SenderEmail", "store@example.com");
        _settingsService.Set("Email_SenderPassword", "app-password");
        _settingsService.Set("Email_RecipientEmail", "");

        // Act
        var (success, message) = await _emailService.SendDailyReportAsync();

        // Assert
        success.Should().BeFalse();
        message.Should().Contain("Recipient Email address is not configured");
    }

    #endregion
}
