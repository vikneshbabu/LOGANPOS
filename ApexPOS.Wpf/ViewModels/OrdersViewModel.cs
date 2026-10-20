using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ApexPOS.Domain.Entities;
using ApexPOS.Domain.Enums;
using ApexPOS.Services;

namespace ApexPOS.Wpf.ViewModels;

public partial class OrdersViewModel : ObservableObject
{
    private readonly OrderService _orderService;
    private readonly ReceiptService _receiptService;
    private readonly SettingsService? _settingsService;

    [ObservableProperty] private string _searchQuery = "";
    [ObservableProperty] private OrderStatus? _selectedStatusFilter;
    [ObservableProperty] private Order? _selectedOrder;
    [ObservableProperty] private bool _showReceiptModal;
    [ObservableProperty] private string _receiptText = "";
    [ObservableProperty] private string _statusMessage = "";

    // Date Range & Report Properties
    [ObservableProperty] private DateTime? _startDate;
    [ObservableProperty] private DateTime? _endDate;
    [ObservableProperty] private string _selectedDatePreset = "All";

    // Invoice Modal Properties
    [ObservableProperty] private bool _showInvoiceModal;
    [ObservableProperty] private Order? _invoiceOrder;

    public int CurrentUserId { get; set; }
    public Action<Order>? OnResumeOrder { get; set; }

    public ObservableCollection<Order> Orders { get; } = new();

    public OrdersViewModel(OrderService orderService, ReceiptService receiptService, SettingsService? settingsService = null)
    {
        _orderService = orderService;
        _receiptService = receiptService;
        _settingsService = settingsService;
    }

    public void LoadData()
    {
        RefreshOrders();
    }

    private void RefreshOrders()
    {
        Orders.Clear();
        var list = _orderService.GetAllOrders(SelectedStatusFilter, SearchQuery, StartDate, EndDate);
        foreach (var o in list)
            Orders.Add(o);

        if (SelectedOrder != null)
        {
            SelectedOrder = Orders.FirstOrDefault(o => o.Id == SelectedOrder.Id);
        }
    }

    [RelayCommand]
    public void FilterByStatus(string? statusStr)
    {
        if (string.IsNullOrEmpty(statusStr) || statusStr == "All")
            SelectedStatusFilter = null;
        else if (Enum.TryParse<OrderStatus>(statusStr, out var status))
            SelectedStatusFilter = status;

        RefreshOrders();
    }

    [RelayCommand]
    public void Search()
    {
        RefreshOrders();
    }

    [RelayCommand]
    public void SetDatePreset(string preset)
    {
        SelectedDatePreset = preset;
        var today = DateTime.Today;

        switch (preset)
        {
            case "Today":
                StartDate = today;
                EndDate = today;
                break;
            case "Yesterday":
                StartDate = today.AddDays(-1);
                EndDate = today.AddDays(-1);
                break;
            case "ThisWeek":
                int diff = (7 + (today.DayOfWeek - DayOfWeek.Monday)) % 7;
                StartDate = today.AddDays(-1 * diff);
                EndDate = today;
                break;
            case "ThisMonth":
                StartDate = new DateTime(today.Year, today.Month, 1);
                EndDate = today;
                break;
            case "All":
            default:
                StartDate = null;
                EndDate = null;
                SelectedDatePreset = "All";
                break;
        }

        RefreshOrders();
    }

    [RelayCommand]
    public void ApplyDateFilter()
    {
        SelectedDatePreset = "Custom";
        RefreshOrders();
    }

    [RelayCommand]
    public void ClearDateFilter()
    {
        SetDatePreset("All");
    }

    [RelayCommand]
    public void DownloadSalesReport()
    {
        try
        {
            if (Orders.Count == 0)
            {
                StatusMessage = "⚠️ No orders available in the selected range to download a report.";
                return;
            }

            var profile = _settingsService?.GetProfile();
            var csv = _orderService.GenerateSalesReportCsv(Orders, StartDate, EndDate, profile);

            var startStr = StartDate.HasValue ? StartDate.Value.ToString("yyyyMMdd") : "Start";
            var endStr = EndDate.HasValue ? EndDate.Value.ToString("yyyyMMdd") : "Present";
            var defaultName = $"SalesReport_{startStr}_to_{endStr}.csv";

            var saveDialog = new Microsoft.Win32.SaveFileDialog
            {
                FileName = defaultName,
                DefaultExt = ".csv",
                Filter = "CSV (Comma-delimited) (*.csv)|*.csv|All Files (*.*)|*.*",
                Title = "Download Sales & Orders Report"
            };

            if (saveDialog.ShowDialog() == true)
            {
                System.IO.File.WriteAllText(saveDialog.FileName, csv, System.Text.Encoding.UTF8);
                StatusMessage = $"📊 Report saved successfully: {System.IO.Path.GetFileName(saveDialog.FileName)}";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Report export notice: {ex.Message}";
        }
    }

    [RelayCommand]
    public void SelectOrder(Order order)
    {
        SelectedOrder = _orderService.GetOrderWithDetails(order.Id) ?? order;
    }

    [RelayCommand]
    public void ResumeOrder(Order order)
    {
        if (order.OrderStatus != OrderStatus.OnHold) return;

        var fullOrder = _orderService.GetOrderWithDetails(order.Id) ?? order;
        _orderService.ResumeOrder(order.Id);
        OnResumeOrder?.Invoke(fullOrder);
    }

    [RelayCommand]
    public void VoidOrder(Order order)
    {
        if (order.OrderStatus == OrderStatus.Voided) return;

        _orderService.VoidOrder(order.Id, CurrentUserId);
        StatusMessage = $"Order {order.InvoiceNumber} has been voided and inventory restored.";
        RefreshOrders();
        if (SelectedOrder?.Id == order.Id)
        {
            SelectedOrder = _orderService.GetOrderWithDetails(order.Id);
        }
    }

    [RelayCommand]
    public void ViewReceipt(Order order)
    {
        var fullOrder = _orderService.GetOrderWithDetails(order.Id) ?? order;
        ReceiptText = _receiptService.GenerateReceiptText(fullOrder);
        ShowReceiptModal = true;
    }

    [RelayCommand]
    public void CloseReceiptModal()
    {
        ShowReceiptModal = false;
        ReceiptText = "";
    }

    [RelayCommand]
    public void ViewInvoice(Order? order = null)
    {
        var target = order ?? SelectedOrder;
        if (target == null) return;

        InvoiceOrder = _orderService.GetOrderWithDetails(target.Id) ?? target;
        ShowInvoiceModal = true;
    }

    [RelayCommand]
    public void CloseInvoiceModal()
    {
        ShowInvoiceModal = false;
        InvoiceOrder = null;
    }

    [RelayCommand]
    public void DownloadInvoice(Order? order = null)
    {
        try
        {
            var target = order ?? InvoiceOrder ?? SelectedOrder;
            if (target == null)
            {
                StatusMessage = "⚠️ Please select an order to download its invoice.";
                return;
            }

            var fullOrder = _orderService.GetOrderWithDetails(target.Id) ?? target;
            var safeInv = fullOrder.InvoiceNumber.Replace("/", "-").Replace("\\", "-");
            var statusStr = fullOrder.OrderStatus.ToString();

            var saveDialog = new Microsoft.Win32.SaveFileDialog
            {
                FileName = $"Invoice_{safeInv}_{statusStr}.html",
                DefaultExt = ".html",
                Filter = "HTML Invoice (*.html)|*.html|Text Invoice (*.txt)|*.txt|All Files (*.*)|*.*",
                Title = "Download Completed Invoice"
            };

            if (saveDialog.ShowDialog() == true)
            {
                var ext = System.IO.Path.GetExtension(saveDialog.FileName).ToLowerInvariant();
                string content = ext == ".txt"
                    ? _receiptService.GenerateInvoiceText(fullOrder)
                    : _receiptService.GenerateInvoiceHtml(fullOrder);

                System.IO.File.WriteAllText(saveDialog.FileName, content, System.Text.Encoding.UTF8);
                StatusMessage = $"💾 Invoice saved successfully: {System.IO.Path.GetFileName(saveDialog.FileName)}";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Invoice export notice: {ex.Message}";
        }
    }

    [RelayCommand]
    public void PrintInvoice(Order? order = null)
    {
        try
        {
            var target = order ?? InvoiceOrder ?? SelectedOrder;
            if (target == null) return;

            var fullOrder = _orderService.GetOrderWithDetails(target.Id) ?? target;
            var printDialog = new System.Windows.Controls.PrintDialog();
            if (printDialog.ShowDialog() == true)
            {
                var invoiceText = _receiptService.GenerateInvoiceText(fullOrder);
                var textBlock = new System.Windows.Controls.TextBlock
                {
                    Text = invoiceText,
                    FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                    FontSize = 10,
                    Margin = new System.Windows.Thickness(25)
                };
                printDialog.PrintVisual(textBlock, $"Invoice_{fullOrder.InvoiceNumber}");
                StatusMessage = $"🖨️ Invoice {fullOrder.InvoiceNumber} sent to printer!";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Print notice: {ex.Message}";
        }
    }

    [RelayCommand]
    public void PrintReceipt()
    {
        try
        {
            var printDialog = new System.Windows.Controls.PrintDialog();
            if (printDialog.ShowDialog() == true)
            {
                var textBlock = new System.Windows.Controls.TextBlock
                {
                    Text = ReceiptText,
                    FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                    FontSize = 10,
                    Margin = new System.Windows.Thickness(15)
                };
                printDialog.PrintVisual(textBlock, $"Receipt_{SelectedOrder?.InvoiceNumber ?? "Order"}");
                StatusMessage = "🖨️ Receipt sent to printer successfully!";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Print notice: {ex.Message}";
        }
    }

    [RelayCommand]
    public void DownloadReceipt()
    {
        try
        {
            var saveDialog = new Microsoft.Win32.SaveFileDialog
            {
                FileName = $"Receipt_{SelectedOrder?.InvoiceNumber ?? "Order"}.txt",
                DefaultExt = ".txt",
                Filter = "Text Documents (*.txt)|*.txt|All Files (*.*)|*.*"
            };
            if (saveDialog.ShowDialog() == true)
            {
                System.IO.File.WriteAllText(saveDialog.FileName, ReceiptText);
                StatusMessage = $"💾 Receipt saved: {System.IO.Path.GetFileName(saveDialog.FileName)}";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Save error: {ex.Message}";
        }
    }
}
