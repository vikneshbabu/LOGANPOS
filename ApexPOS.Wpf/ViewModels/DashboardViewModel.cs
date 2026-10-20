using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ApexPOS.Domain.Entities;
using ApexPOS.Domain.Enums;
using ApexPOS.Services;

namespace ApexPOS.Wpf.ViewModels;

public partial class DashboardViewModel : ObservableObject
{
    private readonly DashboardService _dashboardService;
    private readonly ProductService _productService;
    private readonly OrderService? _orderService;
    private readonly SettingsService? _settingsService;

    [ObservableProperty] private decimal _todaySales;
    [ObservableProperty] private decimal _todayProfit;
    [ObservableProperty] private int _todayTransactions;
    [ObservableProperty] private int _lowStockCount;
    [ObservableProperty] private string _downloadStatusMessage = "";

    public ObservableCollection<ChartDataPoint> SalesChartData { get; } = new();
    public ObservableCollection<TopProductItem> TopProducts { get; } = new();
    public ObservableCollection<CategorySalesItem> CategorySales { get; } = new();
    public ObservableCollection<Order> RecentSalesOrders { get; } = new();

    public DashboardViewModel(DashboardService dashboardService, ProductService productService,
        OrderService? orderService = null, SettingsService? settingsService = null)
    {
        _dashboardService = dashboardService;
        _productService = productService;
        _orderService = orderService;
        _settingsService = settingsService;
    }

    public void LoadData()
    {
        TodaySales = _dashboardService.GetTodaySales();
        TodayProfit = _dashboardService.GetTodayProfit();
        TodayTransactions = _dashboardService.GetTodayTransactionCount();
        LowStockCount = _dashboardService.GetLowStockCount();
        DownloadStatusMessage = "";

        // 7-day sales chart
        SalesChartData.Clear();
        var sales7Days = _dashboardService.GetSalesLast7Days();
        decimal maxSale = sales7Days.Values.Count > 0 ? sales7Days.Values.Max() : 0;

        foreach (var (day, amount) in sales7Days)
        {
            double val = (double)amount;
            double barHeight = maxSale > 0 ? Math.Max(20, (val / (double)maxSale) * 120) : 20;
            SalesChartData.Add(new ChartDataPoint(day, val, barHeight));
        }

        // Top products
        TopProducts.Clear();
        int rank = 1;
        foreach (var (name, qty) in _dashboardService.GetTopSellingProducts(5))
        {
            TopProducts.Add(new TopProductItem(rank++, name, qty));
        }

        // Category breakdown
        CategorySales.Clear();
        foreach (var (cat, total) in _dashboardService.GetSalesByCategory())
        {
            CategorySales.Add(new CategorySalesItem(cat, total));
        }

        // Recent Completed Orders
        RecentSalesOrders.Clear();
        if (_orderService != null)
        {
            var recent = _orderService.GetAllOrders(OrderStatus.Completed)
                .OrderByDescending(o => o.CreatedAt)
                .Take(15);
            foreach (var order in recent)
                RecentSalesOrders.Add(order);
        }
    }

    [RelayCommand]
    public void DownloadSalesData()
    {
        if (_orderService == null)
        {
            DownloadStatusMessage = "⚠️ Order service is unavailable.";
            return;
        }

        try
        {
            var orders = _orderService.GetAllOrders(null);
            if (orders.Count == 0)
            {
                DownloadStatusMessage = "⚠️ No sales records available to download.";
                return;
            }

            var profile = _settingsService?.GetProfile();
            var csv = _orderService.GenerateSalesReportCsv(orders, null, null, profile);

            var saveDialog = new Microsoft.Win32.SaveFileDialog
            {
                FileName = $"ApexPOS_Sales_Data_{DateTime.Now:yyyyMMdd_HHmmss}.csv",
                DefaultExt = ".csv",
                Filter = "CSV (Comma-delimited) (*.csv)|*.csv|All Files (*.*)|*.*",
                Title = "Download Sales & Transactions Data"
            };

            if (saveDialog.ShowDialog() == true)
            {
                System.IO.File.WriteAllText(saveDialog.FileName, csv, System.Text.Encoding.UTF8);
                DownloadStatusMessage = $"📊 Sales data exported successfully: {System.IO.Path.GetFileName(saveDialog.FileName)}";
            }
        }
        catch (Exception ex)
        {
            DownloadStatusMessage = $"❌ Export failed: {ex.Message}";
        }
    }
}

public record ChartDataPoint(string Label, double Value, double BarHeight);
public record TopProductItem(int Rank, string ProductName, int QuantitySold);
public record CategorySalesItem(string CategoryName, decimal TotalSales);
