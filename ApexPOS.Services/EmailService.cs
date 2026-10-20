using System.Net;
using System.Net.Mail;
using ApexPOS.Data;
using ApexPOS.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace ApexPOS.Services;

public class EmailReportSettings
{
    public string SmtpHost { get; set; } = "smtp.gmail.com";
    public int SmtpPort { get; set; } = 587;
    public bool EnableSsl { get; set; } = true;
    public string SenderEmail { get; set; } = "";
    public string SenderPassword { get; set; } = "";
    public string RecipientEmail { get; set; } = "";
    public bool EnableDailyReport { get; set; } = false;
    public string DailyReportTime { get; set; } = "21:00";
    public string LastReportSentDate { get; set; } = "";
}

public class DailyReportSummary
{
    public DateTime ReportDate { get; set; } = DateTime.Today;
    public decimal TotalSales { get; set; }
    public decimal TotalProfit { get; set; }
    public int TotalOrders { get; set; }
    public int TotalActiveProducts { get; set; }
    public int TotalStockUnits { get; set; }
    public int LowStockCount { get; set; }
    public List<LowStockReportItem> LowStockItems { get; set; } = new();
    public decimal CashSales { get; set; }
    public decimal CardSales { get; set; }
    public decimal DebtSales { get; set; }
}

public class LowStockReportItem
{
    public string Name { get; set; } = "";
    public string SKU { get; set; } = "";
    public int CurrentStock { get; set; }
    public int ReorderLevel { get; set; }
}

public class EmailService : IEmailService
{
    private readonly ApexDbContext _db;
    private readonly ISettingsService _settingsService;

    public EmailService(ApexDbContext db, ISettingsService settingsService)
    {
        _db = db;
        _settingsService = settingsService;
    }

    public EmailReportSettings GetSettings()
    {
        var s = new EmailReportSettings();
        var host = _settingsService.Get("Email_SmtpHost");
        if (host != null) s.SmtpHost = host;

        var portStr = _settingsService.Get("Email_SmtpPort");
        if (int.TryParse(portStr, out var port)) s.SmtpPort = port;

        var sslStr = _settingsService.Get("Email_EnableSsl");
        if (bool.TryParse(sslStr, out var ssl)) s.EnableSsl = ssl;

        var sender = _settingsService.Get("Email_SenderEmail");
        if (!string.IsNullOrWhiteSpace(sender)) s.SenderEmail = sender;

        var pass = _settingsService.Get("Email_SenderPassword");
        if (!string.IsNullOrWhiteSpace(pass)) s.SenderPassword = pass;

        var recip = _settingsService.Get("Email_RecipientEmail");
        if (!string.IsNullOrWhiteSpace(recip)) s.RecipientEmail = recip;

        var dailyStr = _settingsService.Get("Email_EnableDailyReport");
        if (bool.TryParse(dailyStr, out var daily)) s.EnableDailyReport = daily;

        var time = _settingsService.Get("Email_DailyReportTime");
        if (!string.IsNullOrWhiteSpace(time)) s.DailyReportTime = time;

        var last = _settingsService.Get("Email_LastReportSentDate");
        if (!string.IsNullOrWhiteSpace(last)) s.LastReportSentDate = last;

        return s;
    }

    public void SaveSettings(EmailReportSettings s)
    {
        _settingsService.Set("Email_SmtpHost", s.SmtpHost.Trim());
        _settingsService.Set("Email_SmtpPort", s.SmtpPort.ToString());
        _settingsService.Set("Email_EnableSsl", s.EnableSsl.ToString());
        _settingsService.Set("Email_SenderEmail", s.SenderEmail.Trim());
        _settingsService.Set("Email_SenderPassword", s.SenderPassword.Trim());
        _settingsService.Set("Email_RecipientEmail", s.RecipientEmail.Trim());
        _settingsService.Set("Email_EnableDailyReport", s.EnableDailyReport.ToString());
        _settingsService.Set("Email_DailyReportTime", s.DailyReportTime.Trim());
        _settingsService.Set("Email_LastReportSentDate", s.LastReportSentDate.Trim());
        _db.SaveChanges();
    }

    public DailyReportSummary BuildDailySummary(DateTime? targetDate = null)
    {
        var date = targetDate ?? DateTime.UtcNow.Date;
        var startOfDay = date.Date;
        var endOfDay = startOfDay.AddDays(1);

        var orders = _db.Orders
            .Include(o => o.Items)
            .Include(o => o.Payments)
            .Where(o => o.OrderStatus == OrderStatus.Completed && o.CreatedAt >= startOfDay && o.CreatedAt < endOfDay)
            .ToList();

        var totalSales = orders.Sum(o => o.GrandTotal);
        var totalOrders = orders.Count;

        var items = orders.SelectMany(o => o.Items).ToList();
        var totalProfit = items.Sum(i => (i.UnitPrice - i.UnitCost) * i.Quantity);

        var cashSales = orders.SelectMany(o => o.Payments)
            .Where(p => p.PaymentMethod == PaymentMethod.Cash)
            .Sum(p => p.AmountPaid);

        var cardSales = orders.SelectMany(o => o.Payments)
            .Where(p => p.PaymentMethod == PaymentMethod.Card || p.PaymentMethod == PaymentMethod.DigitalWallet)
            .Sum(p => p.AmountPaid);

        var debtSales = orders.SelectMany(o => o.Payments)
            .Where(p => p.PaymentMethod == PaymentMethod.StoreCredit)
            .Sum(p => p.AmountPaid);

        var activeProducts = _db.Products.Where(p => p.IsActive).ToList();
        var totalStock = activeProducts.Sum(p => p.StockQuantity);
        var lowStockList = activeProducts.Where(p => p.StockQuantity <= p.ReorderLevel).ToList();

        return new DailyReportSummary
        {
            ReportDate = startOfDay,
            TotalSales = totalSales,
            TotalProfit = totalProfit,
            TotalOrders = totalOrders,
            TotalActiveProducts = activeProducts.Count,
            TotalStockUnits = totalStock,
            LowStockCount = lowStockList.Count,
            CashSales = cashSales,
            CardSales = cardSales,
            DebtSales = debtSales,
            LowStockItems = lowStockList.Select(p => new LowStockReportItem
            {
                Name = p.Name,
                SKU = p.SKU,
                CurrentStock = p.StockQuantity,
                ReorderLevel = p.ReorderLevel
            }).ToList()
        };
    }

    public (string Subject, string HtmlBody, string PlainText) GenerateReportEmail(DailyReportSummary summary)
    {
        var profile = _settingsService.GetProfile();
        var storeName = string.IsNullOrWhiteSpace(profile.StoreName) ? "ApexPOS Store" : profile.StoreName;
        var currency = string.IsNullOrWhiteSpace(profile.CurrencySymbol) ? "₹" : profile.CurrencySymbol;
        var dateStr = summary.ReportDate.ToString("dddd, dd MMMM yyyy");

        var subject = $"📊 Daily Business Summary - {storeName} ({summary.ReportDate:yyyy-MM-dd})";

        var sbPlain = new System.Text.StringBuilder();
        sbPlain.AppendLine($"==========================================");
        sbPlain.AppendLine($" DAILY REPORT: {storeName}");
        sbPlain.AppendLine($" Date: {dateStr}");
        sbPlain.AppendLine($"==========================================");
        sbPlain.AppendLine($"💰 Total Sales: {currency}{summary.TotalSales:N2}");
        sbPlain.AppendLine($"📈 Net Profit: {currency}{summary.TotalProfit:N2}");
        sbPlain.AppendLine($"🧾 Total Orders: {summary.TotalOrders}");
        sbPlain.AppendLine($"  - Cash Tendered: {currency}{summary.CashSales:N2}");
        sbPlain.AppendLine($"  - Card/Digital: {currency}{summary.CardSales:N2}");
        sbPlain.AppendLine($"  - Customer Debt Added: {currency}{summary.DebtSales:N2}");
        sbPlain.AppendLine();
        sbPlain.AppendLine($"📦 INVENTORY STATUS");
        sbPlain.AppendLine($"  - Active Product Lines: {summary.TotalActiveProducts}");
        sbPlain.AppendLine($"  - Total Stock Units Remaining: {summary.TotalStockUnits}");
        sbPlain.AppendLine($"  - Low Stock Items: {summary.LowStockCount}");
        if (summary.LowStockItems.Any())
        {
            sbPlain.AppendLine($"\nLOW STOCK REORDER LIST:");
            foreach (var item in summary.LowStockItems)
            {
                sbPlain.AppendLine($"  * {item.Name} (SKU: {item.SKU}) - Current: {item.CurrentStock} | Reorder: {item.ReorderLevel}");
            }
        }
        sbPlain.AppendLine($"\nGenerated automatically by ApexPOS.");

        var lowStockRows = "";
        if (summary.LowStockItems.Any())
        {
            foreach (var item in summary.LowStockItems)
            {
                lowStockRows += $@"
                <tr>
                    <td style='padding:8px 12px;border-bottom:1px solid #E2E8F0;font-size:13px;color:#1E293B;'>{item.Name}</td>
                    <td style='padding:8px 12px;border-bottom:1px solid #E2E8F0;font-size:12px;color:#64748B;'>{item.SKU}</td>
                    <td style='padding:8px 12px;border-bottom:1px solid #E2E8F0;font-size:13px;font-weight:bold;color:#DC2626;text-align:center;'>{item.CurrentStock}</td>
                    <td style='padding:8px 12px;border-bottom:1px solid #E2E8F0;font-size:12px;color:#64748B;text-align:center;'>{item.ReorderLevel}</td>
                </tr>";
            }
        }
        else
        {
            lowStockRows = @"
            <tr>
                <td colspan='4' style='padding:16px;text-align:center;color:#10B981;font-weight:bold;'>
                    ✅ All inventory items are adequately stocked.
                </td>
            </tr>";
        }

        var html = $@"
<!DOCTYPE html>
<html>
<head>
<meta charset='utf-8'/>
<style>
    body {{ font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; background-color: #F8FAFC; margin: 0; padding: 24px; color: #1E293B; }}
    .container {{ max-width: 600px; margin: 0 auto; background: #FFFFFF; border-radius: 12px; overflow: hidden; box-shadow: 0 4px 6px -1px rgba(0,0,0,0.1); border: 1px solid #E2E8F0; }}
    .header {{ background: linear-gradient(135deg, #0284C7 0%, #0369A1 100%); color: #FFFFFF; padding: 28px; text-align: center; }}
    .header h1 {{ margin: 0 0 4px 0; font-size: 22px; font-weight: bold; letter-spacing: -0.5px; }}
    .header p {{ margin: 0; font-size: 13px; opacity: 0.9; color: #E0F2FE; }}
    .content {{ padding: 24px; }}
    .kpi-grid {{ display: table; width: 100%; margin-bottom: 20px; }}
    .kpi-row {{ display: table-row; }}
    .kpi-cell {{ display: table-cell; width: 50%; padding: 10px; }}
    .kpi-card {{ background: #F1F5F9; border-radius: 8px; padding: 16px; text-align: center; border: 1px solid #E2E8F0; }}
    .kpi-title {{ font-size: 11px; font-weight: 600; text-transform: uppercase; letter-spacing: 0.5px; color: #64748B; margin-bottom: 4px; }}
    .kpi-value {{ font-size: 22px; font-weight: bold; }}
    .val-sales {{ color: #0284C7; }}
    .val-profit {{ color: #16A34A; }}
    .val-orders {{ color: #475569; }}
    .val-stock {{ color: #D97706; }}
    .section-title {{ font-size: 14px; font-weight: 700; color: #0F172A; margin: 20px 0 10px 0; text-transform: uppercase; letter-spacing: 0.5px; border-bottom: 2px solid #E2E8F0; padding-bottom: 6px; }}
    table {{ width: 100%; border-collapse: collapse; margin-top: 8px; }}
    th {{ background: #F8FAFC; color: #475569; font-size: 11px; font-weight: 600; text-transform: uppercase; padding: 8px 12px; border-bottom: 2px solid #CBD5E1; text-align: left; }}
    .footer {{ background: #F8FAFC; border-top: 1px solid #E2E8F0; padding: 16px; text-align: center; font-size: 11px; color: #94A3B8; }}
</style>
</head>
<body>
<div class='container'>
    <div class='header'>
        <h1>🏪 {storeName}</h1>
        <p>Daily Sales, Profit &amp; Inventory Report | {dateStr}</p>
    </div>

    <div class='content'>
        <!-- KPI METRICS -->
        <div class='kpi-grid'>
            <div class='kpi-row'>
                <div class='kpi-cell'>
                    <div class='kpi-card'>
                        <div class='kpi-title'>💰 Today's Sales</div>
                        <div class='kpi-value val-sales'>{currency}{summary.TotalSales:N2}</div>
                    </div>
                </div>
                <div class='kpi-cell'>
                    <div class='kpi-card'>
                        <div class='kpi-title'>📈 Net Profit</div>
                        <div class='kpi-value val-profit'>{currency}{summary.TotalProfit:N2}</div>
                    </div>
                </div>
            </div>
            <div class='kpi-row'>
                <div class='kpi-cell'>
                    <div class='kpi-card'>
                        <div class='kpi-title'>🧾 Orders Completed</div>
                        <div class='kpi-value val-orders'>{summary.TotalOrders}</div>
                    </div>
                </div>
                <div class='kpi-cell'>
                    <div class='kpi-card'>
                        <div class='kpi-title'>📦 Total Stock Remaining</div>
                        <div class='kpi-value val-stock'>{summary.TotalStockUnits} units</div>
                    </div>
                </div>
            </div>
        </div>

        <!-- PAYMENT BREAKDOWN -->
        <div class='section-title'>Payment Breakdown</div>
        <table>
            <tr><td style='padding:8px 0;border-bottom:1px solid #E2E8F0;'>💵 Cash Collected</td><td style='padding:8px 0;border-bottom:1px solid #E2E8F0;text-align:right;font-weight:bold;'>{currency}{summary.CashSales:N2}</td></tr>
            <tr><td style='padding:8px 0;border-bottom:1px solid #E2E8F0;'>💳 Card &amp; Digital</td><td style='padding:8px 0;border-bottom:1px solid #E2E8F0;text-align:right;font-weight:bold;'>{currency}{summary.CardSales:N2}</td></tr>
            <tr><td style='padding:8px 0;border-bottom:1px solid #E2E8F0;'>📝 Customer Debt Added</td><td style='padding:8px 0;border-bottom:1px solid #E2E8F0;text-align:right;font-weight:bold;color:#D97706;'>{currency}{summary.DebtSales:N2}</td></tr>
        </table>

        <!-- LOW STOCK ALERT -->
        <div class='section-title' style='margin-top:24px;'>⚠️ Low Stock Alerts ({summary.LowStockCount})</div>
        <table>
            <thead>
                <tr>
                    <th>Product</th>
                    <th>SKU</th>
                    <th style='text-align:center;'>Stock</th>
                    <th style='text-align:center;'>Reorder</th>
                </tr>
            </thead>
            <tbody>
                {lowStockRows}
            </tbody>
        </table>
    </div>

    <div class='footer'>
        Report generated by <strong>ApexPOS</strong> on {DateTime.Now:yyyy-MM-dd HH:mm:ss}.<br/>
        ApexPOS Enterprise Retail Solutions
    </div>
</div>
</body>
</html>";

        return (subject, html, sbPlain.ToString());
    }

    public async Task<(bool Success, string Message)> SendDailyReportAsync(string? overrideRecipient = null)
    {
        var settings = GetSettings();
        var recipient = !string.IsNullOrWhiteSpace(overrideRecipient) ? overrideRecipient : settings.RecipientEmail;

        if (string.IsNullOrWhiteSpace(settings.SmtpHost))
            return (false, "SMTP Host is not configured.");
        if (string.IsNullOrWhiteSpace(settings.SenderEmail))
            return (false, "Sender Email address is not configured.");
        if (string.IsNullOrWhiteSpace(settings.SenderPassword))
            return (false, "Sender Email Password/App Password is not configured.");
        if (string.IsNullOrWhiteSpace(recipient))
            return (false, "Recipient Email address is not configured.");

        try
        {
            var summary = BuildDailySummary();
            var (subject, htmlBody, plainText) = GenerateReportEmail(summary);

            using var client = new SmtpClient(settings.SmtpHost, settings.SmtpPort)
            {
                EnableSsl = settings.EnableSsl,
                Credentials = new NetworkCredential(settings.SenderEmail, settings.SenderPassword),
                Timeout = 15000
            };

            using var message = new MailMessage
            {
                From = new MailAddress(settings.SenderEmail, "ApexPOS Reports"),
                Subject = subject,
                Body = htmlBody,
                IsBodyHtml = true
            };

            // Support comma or semicolon separated recipients
            var emails = recipient.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var email in emails)
            {
                var trimmed = email.Trim();
                if (!string.IsNullOrWhiteSpace(trimmed))
                    message.To.Add(trimmed);
            }

            if (message.To.Count == 0)
                return (false, "No valid recipient email address specified.");

            // Add plain text alternate view
            var plainView = AlternateView.CreateAlternateViewFromString(plainText, null, "text/plain");
            var htmlView = AlternateView.CreateAlternateViewFromString(htmlBody, null, "text/html");
            message.AlternateViews.Add(plainView);
            message.AlternateViews.Add(htmlView);

            await client.SendMailAsync(message);

            // Record last report sent date
            settings.LastReportSentDate = DateTime.Today.ToString("yyyy-MM-dd");
            SaveSettings(settings);

            return (true, $"✅ Daily business report sent successfully to {recipient}!");
        }
        catch (Exception ex)
        {
            return (false, $"❌ Email delivery failed: {ex.Message}");
        }
    }
}
