using System.Text;
using ApexPOS.Domain.Entities;
using ApexPOS.Domain.Enums;

namespace ApexPOS.Services;

public class ReceiptService : IReceiptService
{
    private readonly ISettingsService? _settingsService;

    public ReceiptService(ISettingsService? settingsService = null)
    {
        _settingsService = settingsService;
    }

    public string GenerateReceiptText(Order order)
    {
        var storeName = "Govindarajan store";
        var storeAddress = "bazzar street sirkali";
        var storePhone = "tel - 9487848939";
        var footerMsg = "Thank you for shopping with us! Please visit again.";
        var currency = "₹";

        if (_settingsService != null)
        {
            var p = _settingsService.GetProfile();
            if (!string.IsNullOrWhiteSpace(p.StoreName)) storeName = p.StoreName;
            if (!string.IsNullOrWhiteSpace(p.StoreAddress)) storeAddress = p.StoreAddress;
            if (!string.IsNullOrWhiteSpace(p.StorePhone)) storePhone = p.StorePhone.StartsWith("tel") ? p.StorePhone : $"tel - {p.StorePhone}";
            if (!string.IsNullOrWhiteSpace(p.ReceiptFooter)) footerMsg = p.ReceiptFooter;
            if (!string.IsNullOrWhiteSpace(p.CurrencySymbol)) currency = p.CurrencySymbol;
        }

        var sb = new StringBuilder();
        sb.AppendLine("========================================");
        sb.AppendLine(CenterText(storeName, 40));
        sb.AppendLine(CenterText(storeAddress, 40));
        sb.AppendLine(CenterText(storePhone, 40));
        sb.AppendLine("========================================");
        sb.AppendLine($"Invoice: {order.InvoiceNumber}");
        sb.AppendLine($"Date:    {order.CreatedAt:yyyy-MM-dd HH:mm:ss}");
        if (order.Cashier != null)
            sb.AppendLine($"Cashier: {order.Cashier.FullName} (#{order.CashierId})");
        if (!string.IsNullOrWhiteSpace(order.SalesPersonName))
            sb.AppendLine($"Sales Person: {order.SalesPersonName}");
        if (order.SalesPersonLoyaltyPointsEarned > 0)
            sb.AppendLine($"Staff Pts Earned: +{order.SalesPersonLoyaltyPointsEarned:F0} pts");
        if (order.Customer != null)
            sb.AppendLine($"Customer: {order.Customer.Name} ({order.Customer.Phone})");
        sb.AppendLine("----------------------------------------");
        sb.AppendLine(string.Format("{0,-20} {1,4} {2,6} {3,7}", "ITEM", "QTY", "PRICE", "TOTAL"));
        sb.AppendLine("----------------------------------------");
        foreach (var item in order.Items)
        {
            var lineTotal = item.TotalPrice;
            var name = item.ProductName.Length > 20 ? item.ProductName[..17] + "..." : item.ProductName;
            sb.AppendLine(string.Format("{0,-20} {1,4} {2,6:F2} {3,7:F2}", name, item.Quantity, item.UnitPrice, lineTotal));
            if (item.DiscountAmount > 0)
            {
                sb.AppendLine($"  (Disc: -{currency}{item.DiscountAmount:F2})");
            }
        }
        sb.AppendLine("----------------------------------------");
        sb.AppendLine(string.Format("{0,-28} {1,11}", "Subtotal:", $"{currency}{order.SubTotal:F2}"));
        if (order.DiscountTotal > 0)
            sb.AppendLine(string.Format("{0,-28} {1,11}", "Order Discount:", $"-{currency}{order.DiscountTotal:F2}"));
        sb.AppendLine("========================================");
        sb.AppendLine(string.Format("{0,-28} {1,11}", "GRAND TOTAL:", $"{currency}{order.GrandTotal:F2}"));
        sb.AppendLine("========================================");

        if (order.Payments != null && order.Payments.Any())
        {
            sb.AppendLine("PAYMENT DETAILS:");
            foreach (var p in order.Payments)
            {
                var methodLabel = p.PaymentMethod == PaymentMethod.StoreCredit ? "Added to Debt:" : $"{p.PaymentMethod}:";
                sb.AppendLine(string.Format("  {0,-15} {1,10}  (Ref: {2})", methodLabel, $"{currency}{p.AmountPaid:F2}", p.ReferenceNo ?? "N/A"));
                if (p.ChangeGiven > 0)
                    sb.AppendLine(string.Format("  {0,-15} {1,10}", "Change Given:", $"{currency}{p.ChangeGiven:F2}"));
            }
            if (order.Customer != null)
            {
                sb.AppendLine(string.Format("  {0,-15} {1,10}", "Customer Debt:", $"{currency}{order.Customer.StoreCredit:F2}"));
            }
        }
        sb.AppendLine("========================================");
        sb.AppendLine(CenterText(footerMsg, 40));
        sb.AppendLine(CenterText("Please Keep Your Receipt", 40));
        sb.AppendLine("========================================");
        return sb.ToString();
    }

    public string GenerateInvoiceHtml(Order order)
    {
        var storeName = "Govindarajan store";
        var storeAddress = "bazzar street sirkali";
        var storePhone = "9487848939";
        var footerMsg = "Thank you for your business! Please visit again.";
        var currency = "₹";

        if (_settingsService != null)
        {
            var p = _settingsService.GetProfile();
            if (!string.IsNullOrWhiteSpace(p.StoreName)) storeName = p.StoreName;
            if (!string.IsNullOrWhiteSpace(p.StoreAddress)) storeAddress = p.StoreAddress;
            if (!string.IsNullOrWhiteSpace(p.StorePhone)) storePhone = p.StorePhone;
            if (!string.IsNullOrWhiteSpace(p.ReceiptFooter)) footerMsg = p.ReceiptFooter;
            if (!string.IsNullOrWhiteSpace(p.CurrencySymbol)) currency = p.CurrencySymbol;
        }

        var customerName = order.Customer?.Name ?? "Walk-in Customer";
        var customerPhone = order.Customer?.Phone ?? "N/A";
        var cashierName = order.Cashier?.FullName ?? $"User #{order.CashierId}";
        var statusColor = order.OrderStatus switch
        {
            Domain.Enums.OrderStatus.Completed => "#10B981",
            Domain.Enums.OrderStatus.OnHold => "#F59E0B",
            Domain.Enums.OrderStatus.Voided => "#EF4444",
            _ => "#00A8E8"
        };
        var statusBg = order.OrderStatus switch
        {
            Domain.Enums.OrderStatus.Completed => "#D1FAE5",
            Domain.Enums.OrderStatus.OnHold => "#FEF3C7",
            Domain.Enums.OrderStatus.Voided => "#FEE2E2",
            _ => "#E0F2FE"
        };

        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang=\"en\">");
        sb.AppendLine("<head>");
        sb.AppendLine("    <meta charset=\"UTF-8\">");
        sb.AppendLine("    <meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
        sb.AppendLine($"    <title>Invoice - {order.InvoiceNumber}</title>");
        sb.AppendLine("    <style>");
        sb.AppendLine("        * { box-sizing: border-box; margin: 0; padding: 0; }");
        sb.AppendLine("        body { font-family: 'Segoe UI', -apple-system, BlinkMacSystemFont, Roboto, sans-serif; color: #1A1A40; background: #F8FAFC; padding: 30px; }");
        sb.AppendLine("        .invoice-card { max-width: 800px; margin: 0 auto; background: #FFFFFF; border-radius: 12px; box-shadow: 0 4px 20px rgba(0,0,0,0.06); border: 1px solid #E2E8F0; overflow: hidden; }");
        sb.AppendLine("        .header { background: #1A1A40; color: #FFFFFF; padding: 32px 40px; display: flex; justify-content: space-between; align-items: flex-start; }");
        sb.AppendLine("        .store-info h1 { font-size: 26px; font-weight: 700; margin-bottom: 6px; letter-spacing: -0.5px; }");
        sb.AppendLine("        .store-info p { font-size: 13px; color: #94A3B8; line-height: 1.5; }");
        sb.AppendLine("        .invoice-meta { text-align: right; }");
        sb.AppendLine("        .invoice-meta .title { font-size: 20px; font-weight: 700; color: #00A8E8; letter-spacing: 1px; text-transform: uppercase; margin-bottom: 8px; }");
        sb.AppendLine($"        .status-badge {{ display: inline-block; padding: 4px 12px; border-radius: 20px; font-size: 12px; font-weight: 700; text-transform: uppercase; background: {statusBg}; color: {statusColor}; }}");
        sb.AppendLine("        .meta-grid { display: grid; grid-template-columns: 1fr 1fr; gap: 24px; padding: 28px 40px; background: #F8FAFC; border-bottom: 1px solid #E2E8F0; font-size: 13px; }");
        sb.AppendLine("        .meta-block h4 { font-size: 11px; text-transform: uppercase; letter-spacing: 0.8px; color: #64748B; margin-bottom: 8px; font-weight: 600; }");
        sb.AppendLine("        .meta-block p { color: #1E293B; margin-bottom: 4px; }");
        sb.AppendLine("        .meta-block p strong { color: #0F172A; }");
        sb.AppendLine("        .items-section { padding: 28px 40px; }");
        sb.AppendLine("        table { width: 100%; border-collapse: collapse; text-align: left; }");
        sb.AppendLine("        th { font-size: 12px; font-weight: 600; color: #64748B; text-transform: uppercase; letter-spacing: 0.5px; border-bottom: 2px solid #E2E8F0; padding: 10px 12px; }");
        sb.AppendLine("        td { padding: 12px; border-bottom: 1px solid #F1F5F9; font-size: 13px; color: #334155; }");
        sb.AppendLine("        tr:last-child td { border-bottom: none; }");
        sb.AppendLine("        .text-right { text-align: right; }");
        sb.AppendLine("        .text-center { text-align: center; }");
        sb.AppendLine("        .totals-section { display: flex; justify-content: flex-end; padding: 0 40px 28px 40px; }");
        sb.AppendLine("        .totals-box { width: 320px; }");
        sb.AppendLine("        .totals-row { display: flex; justify-content: space-between; padding: 6px 0; font-size: 13px; color: #64748B; }");
        sb.AppendLine("        .totals-row.grand { border-top: 2px solid #E2E8F0; margin-top: 8px; padding-top: 12px; font-size: 18px; font-weight: 700; color: #1A1A40; }");
        sb.AppendLine("        .totals-row.grand span:last-child { color: #00A8E8; }");
        sb.AppendLine("        .payments-section { margin: 0 40px 28px 40px; padding: 16px; background: #F8FAFC; border-radius: 8px; border: 1px solid #E2E8F0; font-size: 12px; color: #64748B; }");
        sb.AppendLine("        .payments-section h5 { font-size: 11px; text-transform: uppercase; color: #475569; margin-bottom: 6px; font-weight: 600; }");
        sb.AppendLine("        .footer { padding: 24px 40px; text-align: center; background: #F8FAFC; border-top: 1px solid #E2E8F0; font-size: 12px; color: #64748B; }");
        sb.AppendLine("        .action-bar { max-width: 800px; margin: 20px auto 0 auto; display: flex; justify-content: flex-end; gap: 12px; }");
        sb.AppendLine("        .btn { padding: 10px 20px; font-size: 14px; font-weight: 600; border-radius: 8px; cursor: pointer; border: none; transition: 0.2s; }");
        sb.AppendLine("        .btn-primary { background: #00A8E8; color: #FFFFFF; }");
        sb.AppendLine("        .btn-primary:hover { background: #0090c7; }");
        sb.AppendLine("        @media print {");
        sb.AppendLine("            body { background: #FFFFFF; padding: 0; }");
        sb.AppendLine("            .invoice-card { box-shadow: none; border: none; border-radius: 0; }");
        sb.AppendLine("            .action-bar { display: none; }");
        sb.AppendLine("        }");
        sb.AppendLine("    </style>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");
        sb.AppendLine("    <div class=\"action-bar\">");
        sb.AppendLine("        <button class=\"btn btn-primary\" onclick=\"window.print()\">🖨️ Print / Save as PDF</button>");
        sb.AppendLine("    </div>");
        sb.AppendLine("    <div class=\"invoice-card\">");
        sb.AppendLine("        <div class=\"header\">");
        sb.AppendLine("            <div class=\"store-info\">");
        sb.AppendLine($"                <h1>{System.Net.WebUtility.HtmlEncode(storeName)}</h1>");
        sb.AppendLine($"                <p>{System.Net.WebUtility.HtmlEncode(storeAddress)}</p>");
        sb.AppendLine($"                <p>Phone: {System.Net.WebUtility.HtmlEncode(storePhone)}</p>");
        sb.AppendLine("            </div>");
        sb.AppendLine("            <div class=\"invoice-meta\">");
        sb.AppendLine("                <div class=\"title\">INVOICE</div>");
        sb.AppendLine($"                <span class=\"status-badge\">{order.OrderStatus}</span>");
        sb.AppendLine("            </div>");
        sb.AppendLine("        </div>");
        sb.AppendLine("        <div class=\"meta-grid\">");
        sb.AppendLine("            <div class=\"meta-block\">");
        sb.AppendLine("                <h4>Invoice Details</h4>");
        sb.AppendLine($"                <p><strong>Invoice No:</strong> {order.InvoiceNumber}</p>");
        sb.AppendLine($"                <p><strong>Date & Time:</strong> {order.CreatedAt:yyyy-MM-dd HH:mm:ss}</p>");
        sb.AppendLine($"                <p><strong>Cashier:</strong> {System.Net.WebUtility.HtmlEncode(cashierName)}</p>");
        if (!string.IsNullOrWhiteSpace(order.SalesPersonName))
            sb.AppendLine($"                <p><strong>Sales Person:</strong> {System.Net.WebUtility.HtmlEncode(order.SalesPersonName)}</p>");
        if (order.SalesPersonLoyaltyPointsEarned > 0)
            sb.AppendLine($"                <p><strong>Staff Points Earned:</strong> +{order.SalesPersonLoyaltyPointsEarned:F0} pts</p>");
        sb.AppendLine($"                <p><strong>Payment Status:</strong> {order.PaymentStatus}</p>");
        sb.AppendLine("            </div>");
        sb.AppendLine("            <div class=\"meta-block\">");
        sb.AppendLine("                <h4>Billed To</h4>");
        sb.AppendLine($"                <p><strong>Customer:</strong> {System.Net.WebUtility.HtmlEncode(customerName)}</p>");
        sb.AppendLine($"                <p><strong>Phone:</strong> {System.Net.WebUtility.HtmlEncode(customerPhone)}</p>");
        if (order.Customer != null && order.Customer.LoyaltyPoints > 0)
            sb.AppendLine($"                <p><strong>Loyalty Points:</strong> {order.Customer.LoyaltyPoints:F0}</p>");
        sb.AppendLine("            </div>");
        sb.AppendLine("        </div>");
        sb.AppendLine("        <div class=\"items-section\">");
        sb.AppendLine("            <table>");
        sb.AppendLine("                <thead>");
        sb.AppendLine("                    <tr>");
        sb.AppendLine("                        <th style=\"width: 40px;\">#</th>");
        sb.AppendLine("                        <th>Item Description</th>");
        sb.AppendLine("                        <th style=\"width: 100px;\">SKU</th>");
        sb.AppendLine("                        <th class=\"text-center\" style=\"width: 60px;\">Qty</th>");
        sb.AppendLine("                        <th class=\"text-right\" style=\"width: 90px;\">Price</th>");
        sb.AppendLine("                        <th class=\"text-right\" style=\"width: 80px;\">Discount</th>");
        sb.AppendLine("                        <th class=\"text-right\" style=\"width: 100px;\">Total</th>");
        sb.AppendLine("                    </tr>");
        sb.AppendLine("                </thead>");
        sb.AppendLine("                <tbody>");
        int idx = 1;
        foreach (var item in order.Items)
        {
            sb.AppendLine("                    <tr>");
            sb.AppendLine($"                        <td>{idx++}</td>");
            sb.AppendLine($"                        <td><strong>{System.Net.WebUtility.HtmlEncode(item.ProductName)}</strong></td>");
            sb.AppendLine($"                        <td>{System.Net.WebUtility.HtmlEncode(item.SKU)}</td>");
            sb.AppendLine($"                        <td class=\"text-center\">{item.Quantity}</td>");
            sb.AppendLine($"                        <td class=\"text-right\">{currency}{item.UnitPrice:F2}</td>");
            sb.AppendLine($"                        <td class=\"text-right\">{(item.DiscountAmount > 0 ? $"-{currency}{item.DiscountAmount:F2}" : "-")}</td>");
            sb.AppendLine($"                        <td class=\"text-right\"><strong>{currency}{item.TotalPrice:F2}</strong></td>");
            sb.AppendLine("                    </tr>");
        }
        sb.AppendLine("                </tbody>");
        sb.AppendLine("            </table>");
        sb.AppendLine("        </div>");
        sb.AppendLine("        <div class=\"totals-section\">");
        sb.AppendLine("            <div class=\"totals-box\">");
        sb.AppendLine("                <div class=\"totals-row\">");
        sb.AppendLine($"                    <span>Subtotal</span><span>{currency}{order.SubTotal:F2}</span>");
        sb.AppendLine("                </div>");
        if (order.DiscountTotal > 0)
        {
            sb.AppendLine("                <div class=\"totals-row\">");
            sb.AppendLine($"                    <span>Order Discount</span><span style=\"color: #EF4444;\">-{currency}{order.DiscountTotal:F2}</span>");
            sb.AppendLine("                </div>");
        }
        sb.AppendLine("                <div class=\"totals-row grand\">");
        sb.AppendLine($"                    <span>Grand Total</span><span>{currency}{order.GrandTotal:F2}</span>");
        sb.AppendLine("                </div>");
        sb.AppendLine("            </div>");
        sb.AppendLine("        </div>");

        if (order.Payments != null && order.Payments.Any())
        {
            sb.AppendLine("        <div class=\"payments-section\">");
            sb.AppendLine("            <h5>Payment Information</h5>");
            foreach (var p in order.Payments)
            {
                var methodLabel = p.PaymentMethod == PaymentMethod.StoreCredit ? "Added to Customer Debt" : p.PaymentMethod.ToString();
                sb.AppendLine($"            <p>{methodLabel}: <strong>{currency}{p.AmountPaid:F2}</strong> {(string.IsNullOrEmpty(p.ReferenceNo) ? "" : $"(Ref: {p.ReferenceNo})")} {(p.ChangeGiven > 0 ? $" | Change Given: {currency}{p.ChangeGiven:F2}" : "")}</p>");
            }
            if (order.Customer != null)
            {
                sb.AppendLine($"            <p>Customer Total Debt Balance: <strong>{currency}{order.Customer.StoreCredit:F2}</strong></p>");
            }
            sb.AppendLine("        </div>");
        }

        sb.AppendLine("        <div class=\"footer\">");
        sb.AppendLine($"            <p>{System.Net.WebUtility.HtmlEncode(footerMsg)}</p>");
        sb.AppendLine($"            <p style=\"margin-top: 4px; font-size: 11px; color: #94A3B8;\">Generated by ApexPOS &bull; {DateTime.Now:yyyy-MM-dd HH:mm:ss}</p>");
        sb.AppendLine("        </div>");
        sb.AppendLine("    </div>");
        sb.AppendLine("</body>");
        sb.AppendLine("</html>");

        return sb.ToString();
    }

    public string GenerateInvoiceText(Order order)
    {
        var storeName = "Govindarajan store";
        var storeAddress = "bazzar street sirkali";
        var storePhone = "tel - 9487848939";
        var footerMsg = "Thank you for your business! Please visit again.";
        var currency = "₹";

        if (_settingsService != null)
        {
            var p = _settingsService.GetProfile();
            if (!string.IsNullOrWhiteSpace(p.StoreName)) storeName = p.StoreName;
            if (!string.IsNullOrWhiteSpace(p.StoreAddress)) storeAddress = p.StoreAddress;
            if (!string.IsNullOrWhiteSpace(p.StorePhone)) storePhone = p.StorePhone;
            if (!string.IsNullOrWhiteSpace(p.ReceiptFooter)) footerMsg = p.ReceiptFooter;
            if (!string.IsNullOrWhiteSpace(p.CurrencySymbol)) currency = p.CurrencySymbol;
        }

        var customerName = order.Customer?.Name ?? "Walk-in";
        var customerPhone = order.Customer?.Phone ?? "N/A";
        var cashier = order.Cashier?.FullName ?? $"User #{order.CashierId}";

        var sb = new StringBuilder();
        sb.AppendLine("================================================================================");
        sb.AppendLine(CenterText(storeName, 80));
        sb.AppendLine(CenterText(storeAddress, 80));
        sb.AppendLine(CenterText($"Phone: {storePhone}", 80));
        sb.AppendLine(CenterText("INVOICE", 80));
        sb.AppendLine("================================================================================");
        sb.AppendLine(string.Format("Invoice Number : {0,-25} Order Status  : {1}", order.InvoiceNumber, order.OrderStatus));
        sb.AppendLine(string.Format("Date & Time    : {0,-25:yyyy-MM-dd HH:mm:ss} Payment Status: {1}", order.CreatedAt, order.PaymentStatus));
        sb.AppendLine(string.Format("Customer Name  : {0,-25} Customer Phone: {1}", customerName, customerPhone));
        sb.AppendLine(string.Format("Cashier        : {0,-25}", cashier));
        sb.AppendLine("--------------------------------------------------------------------------------");
        sb.AppendLine(string.Format("{0,-4} {1,-32} {2,-12} {3,5} {4,10} {5,12}", "#", "ITEM DESCRIPTION", "SKU", "QTY", "PRICE", "TOTAL"));
        sb.AppendLine("--------------------------------------------------------------------------------");
        int idx = 1;
        foreach (var item in order.Items)
        {
            var name = item.ProductName.Length > 32 ? item.ProductName[..29] + "..." : item.ProductName;
            var sku = item.SKU.Length > 12 ? item.SKU[..12] : item.SKU;
            sb.AppendLine(string.Format("{0,-4} {1,-32} {2,-12} {3,5} {4,10:F2} {5,12:F2}", idx++, name, sku, item.Quantity, item.UnitPrice, item.TotalPrice));
            if (item.DiscountAmount > 0)
                sb.AppendLine(string.Format("     (Item Discount: -{0}{1:F2})", currency, item.DiscountAmount));
        }
        sb.AppendLine("--------------------------------------------------------------------------------");
        sb.AppendLine(string.Format("{0,-65} {1,14}", "Subtotal:", $"{currency}{order.SubTotal:F2}"));
        if (order.DiscountTotal > 0)
            sb.AppendLine(string.Format("{0,-65} {1,14}", "Discount Total:", $"-{currency}{order.DiscountTotal:F2}"));
        sb.AppendLine("================================================================================");
        sb.AppendLine(string.Format("{0,-65} {1,14}", "GRAND TOTAL:", $"{currency}{order.GrandTotal:F2}"));
        sb.AppendLine("================================================================================");

        if (order.Payments != null && order.Payments.Any())
        {
            sb.AppendLine("PAYMENT DETAILS:");
            foreach (var p in order.Payments)
            {
                var methodLabel = p.PaymentMethod == PaymentMethod.StoreCredit ? "Added to Debt" : p.PaymentMethod.ToString();
                sb.AppendLine(string.Format("  - {0,-18}: {1,12} (Ref: {2})", methodLabel, $"{currency}{p.AmountPaid:F2}", p.ReferenceNo ?? "N/A"));
                if (p.ChangeGiven > 0)
                    sb.AppendLine(string.Format("    Change Returned  : {0,12}", $"{currency}{p.ChangeGiven:F2}"));
            }
            if (order.Customer != null)
            {
                sb.AppendLine(string.Format("  - {0,-18}: {1,12}", "Customer Debt", $"{currency}{order.Customer.StoreCredit:F2}"));
            }
            sb.AppendLine("--------------------------------------------------------------------------------");
        }

        sb.AppendLine(CenterText(footerMsg, 80));
        sb.AppendLine("================================================================================");
        return sb.ToString();
    }

    private static string CenterText(string text, int width)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        if (text.Length >= width) return text;
        int leftPadding = (width - text.Length) / 2;
        return text.PadLeft(leftPadding + text.Length).PadRight(width);
    }
}
