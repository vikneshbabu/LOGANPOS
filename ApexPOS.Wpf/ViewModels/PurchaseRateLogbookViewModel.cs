using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ApexPOS.Domain.Entities;
using ApexPOS.Services;

namespace ApexPOS.Wpf.ViewModels;

public partial class PurchaseRateLogbookViewModel : ObservableObject
{
    private readonly PurchaseRateLogService _logService;
    private readonly ProductService _productService;
    private readonly PurchaseService _purchaseService;

    // Filters
    [ObservableProperty] private string _searchQuery = "";
    [ObservableProperty] private int? _selectedProductId;
    [ObservableProperty] private int? _selectedSupplierId;
    [ObservableProperty] private string _selectedDateFilter = "All Time";
    [ObservableProperty] private DateTime? _startDate;
    [ObservableProperty] private DateTime? _endDate;
    [ObservableProperty] private bool _isCustomDateRange;
    [ObservableProperty] private string _statusMessage = "";

    // KPI Metrics
    [ObservableProperty] private int _totalRevisions;
    [ObservableProperty] private int _uniqueProductsTracked;
    [ObservableProperty] private int _rateIncreases;
    [ObservableProperty] private int _rateDecreases;

    // Manual Rate Log Modal
    [ObservableProperty] private bool _showNewRateModal;
    [ObservableProperty] private Product? _formProduct;
    [ObservableProperty] private DateTime _formEffectiveDate = DateTime.Today;
    [ObservableProperty] private decimal _formOldCost;
    [ObservableProperty] private decimal _formNewCost;
    [ObservableProperty] private decimal _formSellingPrice;
    [ObservableProperty] private decimal _formMRP;
    [ObservableProperty] private int? _formSupplierId;
    [ObservableProperty] private string _formSource = "Manual Supplier Invoice";
    [ObservableProperty] private string _formNotes = "";

    public decimal FormDifference => FormNewCost - FormOldCost;
    public decimal FormProfit => FormSellingPrice - FormNewCost;
    public decimal FormMargin => FormSellingPrice > 0 ? Math.Round(((FormSellingPrice - FormNewCost) / FormSellingPrice) * 100, 1) : 0;
    public decimal FormDiscount => FormMRP > FormSellingPrice && FormMRP > 0 ? Math.Round(((FormMRP - FormSellingPrice) / FormMRP) * 100, 1) : 0;

    public int CurrentUserId { get; set; } = 1;
    public string CurrentUserName { get; set; } = "Admin";

    public ObservableCollection<PurchaseRateLog> Logs { get; } = new();
    public ObservableCollection<Product> ProductsList { get; } = new();
    public ObservableCollection<Supplier> SuppliersList { get; } = new();
    public ObservableCollection<string> DateFilterOptions { get; } = new()
    {
        "All Time", "Today", "Last 7 Days", "Last 30 Days", "This Month", "Custom Date"
    };

    public PurchaseRateLogbookViewModel(
        PurchaseRateLogService logService,
        ProductService productService,
        PurchaseService purchaseService)
    {
        _logService = logService;
        _productService = productService;
        _purchaseService = purchaseService;
    }

    public void LoadData()
    {
        ProductsList.Clear();
        foreach (var p in _productService.GetAll().OrderBy(p => p.Name))
            ProductsList.Add(p);

        SuppliersList.Clear();
        foreach (var s in _purchaseService.GetAllSuppliers())
            SuppliersList.Add(s);

        RefreshLogs();
    }

    public void RefreshLogs()
    {
        DateTime? filterStart = null;
        DateTime? filterEnd = null;
        var today = DateTime.Today;

        switch (SelectedDateFilter)
        {
            case "Today":
                filterStart = today;
                filterEnd = today;
                IsCustomDateRange = false;
                break;
            case "Last 7 Days":
                filterStart = today.AddDays(-7);
                filterEnd = today;
                IsCustomDateRange = false;
                break;
            case "Last 30 Days":
                filterStart = today.AddDays(-30);
                filterEnd = today;
                IsCustomDateRange = false;
                break;
            case "This Month":
                filterStart = new DateTime(today.Year, today.Month, 1);
                filterEnd = today;
                IsCustomDateRange = false;
                break;
            case "Custom Date":
                IsCustomDateRange = true;
                filterStart = StartDate;
                filterEnd = EndDate;
                break;
            default:
                IsCustomDateRange = false;
                break;
        }

        Logs.Clear();
        var data = _logService.GetAllLogs(
            SelectedProductId,
            SelectedSupplierId,
            filterStart,
            filterEnd,
            SearchQuery);

        foreach (var log in data)
            Logs.Add(log);

        UpdateSummaryKpis();
    }

    private void UpdateSummaryKpis()
    {
        TotalRevisions = Logs.Count;
        UniqueProductsTracked = Logs.Select(l => l.ProductId).Distinct().Count();
        RateIncreases = Logs.Count(l => l.NewCostPrice > l.OldCostPrice);
        RateDecreases = Logs.Count(l => l.NewCostPrice < l.OldCostPrice);
    }

    partial void OnSearchQueryChanged(string value) => RefreshLogs();
    partial void OnSelectedProductIdChanged(int? value) => RefreshLogs();
    partial void OnSelectedSupplierIdChanged(int? value) => RefreshLogs();

    partial void OnSelectedDateFilterChanged(string value)
    {
        if (value == "Custom Date")
        {
            IsCustomDateRange = true;
            if (!StartDate.HasValue) StartDate = DateTime.Today.AddDays(-30);
            if (!EndDate.HasValue) EndDate = DateTime.Today;
        }
        else
        {
            IsCustomDateRange = false;
        }
        RefreshLogs();
    }

    partial void OnStartDateChanged(DateTime? value)
    {
        if (IsCustomDateRange) RefreshLogs();
    }

    partial void OnEndDateChanged(DateTime? value)
    {
        if (IsCustomDateRange) RefreshLogs();
    }

    partial void OnFormProductChanged(Product? value)
    {
        if (value != null)
        {
            FormOldCost = value.CostPrice;
            FormNewCost = value.CostPrice;
            FormSellingPrice = value.SellingPrice;
            FormMRP = value.MRP > 0 ? value.MRP : value.SellingPrice;
            FormSupplierId = value.SupplierId ?? value.Category?.SupplierId;
            NotifyFormCalculations();
        }
    }

    partial void OnFormNewCostChanged(decimal value)
    {
        NotifyFormCalculations();
    }

    partial void OnFormSellingPriceChanged(decimal value)
    {
        if (FormMRP == 0 && value > 0)
        {
            FormMRP = value;
        }
        NotifyFormCalculations();
    }

    partial void OnFormMRPChanged(decimal value)
    {
        if (FormSellingPrice == 0 && value > 0)
        {
            FormSellingPrice = value;
        }
        NotifyFormCalculations();
    }

    private void NotifyFormCalculations()
    {
        OnPropertyChanged(nameof(FormDifference));
        OnPropertyChanged(nameof(FormProfit));
        OnPropertyChanged(nameof(FormMargin));
        OnPropertyChanged(nameof(FormDiscount));
    }

    [RelayCommand]
    public void ResetFilters()
    {
        SearchQuery = "";
        SelectedProductId = null;
        SelectedSupplierId = null;
        SelectedDateFilter = "All Time";
        StartDate = null;
        EndDate = null;
        IsCustomDateRange = false;
        RefreshLogs();
    }

    [RelayCommand]
    public void OpenNewRateModal()
    {
        FormEffectiveDate = DateTime.Today;
        FormProduct = ProductsList.FirstOrDefault();
        FormSource = "Supplier Invoice Revision";
        FormNotes = "";
        ShowNewRateModal = true;
        StatusMessage = "";
    }

    [RelayCommand]
    public void CloseNewRateModal()
    {
        ShowNewRateModal = false;
    }

    [RelayCommand]
    public void SaveNewRateChange()
    {
        if (FormProduct == null)
        {
            StatusMessage = "❌ Please select a product.";
            return;
        }

        if (FormNewCost <= 0)
        {
            StatusMessage = "❌ Please enter a valid new purchase rate.";
            return;
        }

        try
        {
            var log = _logService.LogRateChange(
                FormProduct.Id,
                FormNewCost,
                FormSellingPrice > 0 ? FormSellingPrice : null,
                FormMRP > 0 ? FormMRP : null,
                FormSupplierId,
                FormSource,
                FormNotes,
                FormEffectiveDate,
                CurrentUserId,
                CurrentUserName);

            ShowNewRateModal = false;
            RefreshLogs();

            decimal diff = log.NewCostPrice - log.OldCostPrice;
            string trend = diff > 0 ? $"+Rs.{diff:F2} (Increased)" : diff < 0 ? $"-Rs.{Math.Abs(diff):F2} (Reduced)" : "Unchanged";
            StatusMessage = $"✅ Recorded purchase rate for '{log.ProductName}': Rs.{log.NewCostPrice:F2} ({trend}) on {FormEffectiveDate:dd-MMM-yyyy}.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"❌ Error recording rate change: {ex.Message}";
        }
    }

    [RelayCommand]
    public void ExportCsv()
    {
        try
        {
            var sfd = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Export Purchase Rate Logbook",
                Filter = "CSV Spreadsheet (*.csv)|*.csv|All Files (*.*)|*.*",
                FileName = $"Purchase_Rate_Logbook_{DateTime.Now:yyyyMMdd_HHmm}.csv"
            };

            if (sfd.ShowDialog() != true) return;

            var sb = new StringBuilder();
            sb.AppendLine("==========================================================================================");
            sb.AppendLine("                     ApexPOS - Product Purchase Rate Logbook History                       ");
            sb.AppendLine("==========================================================================================");
            sb.AppendLine($"Report Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"Total Logged Rate Changes: {Logs.Count}");
            sb.AppendLine($"Unique Products Tracked: {UniqueProductsTracked}");
            sb.AppendLine($"Rate Increases: {RateIncreases} | Rate Decreases: {RateDecreases}");
            sb.AppendLine("==========================================================================================");
            sb.AppendLine();
            sb.AppendLine("Log ID,Effective Date,SKU,Barcode,Product Name,Distributor,Old Purchase Price (Rs.),New Purchase Price (Rs.),Rate Change (Rs.),Change %,Selling Price (Rs.),MRP (Rs.),Margin (Rs.),Margin %,Source / PO,Notes,Recorded By");

            foreach (var log in Logs)
            {
                decimal diff = log.RateDifference;
                string diffStr = (diff >= 0 ? "+" : "") + diff.ToString("F2");
                string pctStr = (log.PercentageChange >= 0 ? "+" : "") + log.PercentageChange.ToString("F1") + "%";
                decimal marginAmt = log.MarginAmount;
                string marginStr = log.ProfitMargin.ToString("F1") + "%";

                sb.AppendLine(string.Join(",",
                    log.Id,
                    log.EffectiveDate.ToString("yyyy-MM-dd"),
                    EscapeCsv(log.SKU),
                    EscapeCsv(log.Barcode),
                    EscapeCsv(log.ProductName),
                    EscapeCsv(log.SupplierName),
                    log.OldCostPrice.ToString("F2"),
                    log.NewCostPrice.ToString("F2"),
                    diffStr,
                    pctStr,
                    log.SellingPrice.ToString("F2"),
                    log.EffectiveMRP.ToString("F2"),
                    marginAmt.ToString("F2"),
                    marginStr,
                    EscapeCsv(log.Source),
                    EscapeCsv(log.Notes),
                    EscapeCsv(log.UserName)
                ));
            }

            File.WriteAllText(sfd.FileName, sb.ToString(), Encoding.UTF8);
            StatusMessage = $"✅ Exported {Logs.Count} purchase rate records to {Path.GetFileName(sfd.FileName)}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"❌ Export failed: {ex.Message}";
        }
    }

    private static string EscapeCsv(string? val)
    {
        if (string.IsNullOrEmpty(val)) return "\"\"";
        return $"\"{val.Replace("\"", "\"\"")}\"";
    }
}
