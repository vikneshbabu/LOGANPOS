using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ApexPOS.Domain.Entities;
using ApexPOS.Services;

namespace ApexPOS.Wpf.ViewModels;

public partial class InventoryViewModel : ObservableObject
{
    private readonly ProductService _productService;
    private readonly CategoryService _categoryService;
    private readonly PurchaseService _purchaseService;

    [ObservableProperty] private string _searchQuery = "";
    [ObservableProperty] private Product? _selectedProduct;
    [ObservableProperty] private bool _showEditPanel;
    [ObservableProperty] private bool _showStockAdjust;
    [ObservableProperty] private int _adjustQuantity;
    [ObservableProperty] private string _adjustReason = "";

    // Filters
    [ObservableProperty] private int? _filterSupplierId;
    [ObservableProperty] private int? _filterCategoryId;

    // Edit form
    [ObservableProperty] private string _editName = "";
    [ObservableProperty] private string _editSku = "";
    [ObservableProperty] private string _editBarcode = "";
    [ObservableProperty] private decimal _editPurchasePrice;
    [ObservableProperty] private decimal _editCostPrice;
    [ObservableProperty] private decimal _editPurchaseRate;
    [ObservableProperty] private decimal _editSellingPrice;
    [ObservableProperty] private decimal _editMRP;
    [ObservableProperty] private decimal _editProfitPerUnit;
    [ObservableProperty] private decimal _editMarginPercent;
    [ObservableProperty] private decimal _editMarkupPercent;
    [ObservableProperty] private decimal _editDiscountFromMRP;
    [ObservableProperty] private int _editStockQuantity;
    [ObservableProperty] private int _editReorderLevel;
    [ObservableProperty] private decimal _editTaxRate;
    [ObservableProperty] private int? _editSupplierId;
    [ObservableProperty] private int _editCategoryId;
    [ObservableProperty] private string _editUnit = "piece";
    [ObservableProperty] private bool _isNewProduct;
    [ObservableProperty] private string _editPanelTitle = "Add New Product";
    [ObservableProperty] private bool _showTrashModal;
    [ObservableProperty] private string _trashStatusMessage = "";
    [ObservableProperty] private int _deletedCount;
    [ObservableProperty] private int _totalStockQuantity;
    [ObservableProperty] private decimal _totalInventoryCost;
    [ObservableProperty] private decimal _totalRetailValue;
    [ObservableProperty] private decimal _totalProjectedProfit;
    [ObservableProperty] private decimal _overallProfitMargin;
    [ObservableProperty] private string _exportStatusMessage = "";

    public int CurrentUserId { get; set; }

    public ObservableCollection<Product> Products { get; } = new();
    public ObservableCollection<Category> Categories { get; } = new();
    public ObservableCollection<Category> FilteredListCategories { get; } = new();
    public ObservableCollection<Category> FilteredFormCategories { get; } = new();
    public ObservableCollection<Supplier> Suppliers { get; } = new();
    public ObservableCollection<string> AvailableUnits { get; } = new();
    public ObservableCollection<Product> LowStockProducts { get; } = new();
    public ObservableCollection<Product> DeletedProducts { get; } = new();
    public ObservableCollection<AuditLog> DeleteLogs { get; } = new();

    private static readonly string[] StandardUnits = new[]
    {
        "piece", "kg", "gm", "mg", "lit", "mL",
        "packet", "box", "pouch",
        "pack of 5", "pack of 10", "pack of 12", "pack of 20", "pack of 30"
    };

    public InventoryViewModel(ProductService productService, CategoryService categoryService, PurchaseService purchaseService)
    {
        _productService = productService;
        _categoryService = categoryService;
        _purchaseService = purchaseService;
    }

    public void LoadData()
    {
        Suppliers.Clear();
        foreach (var s in _purchaseService.GetAllSuppliers())
            Suppliers.Add(s);

        Categories.Clear();
        foreach (var cat in _categoryService.GetAll())
            Categories.Add(cat);

        AvailableUnits.Clear();
        foreach (var u in StandardUnits)
            AvailableUnits.Add(u);

        UpdateFilterCategories();
        RefreshProducts();
        RefreshLowStock();
        RefreshDeletedCount();
    }

    private void UpdateFilterCategories()
    {
        FilteredListCategories.Clear();
        var cats = FilterSupplierId.HasValue
            ? Categories.Where(c => c.SupplierId == FilterSupplierId.Value)
            : Categories;

        foreach (var c in cats)
            FilteredListCategories.Add(c);

        if (FilterCategoryId.HasValue && !FilteredListCategories.Any(c => c.Id == FilterCategoryId.Value))
        {
            FilterCategoryId = null;
        }
    }

    private void UpdateFormCategories()
    {
        FilteredFormCategories.Clear();
        var cats = EditSupplierId.HasValue
            ? Categories.Where(c => c.SupplierId == EditSupplierId.Value)
            : Categories;

        foreach (var c in cats)
            FilteredFormCategories.Add(c);

        if (!FilteredFormCategories.Any(c => c.Id == EditCategoryId))
        {
            EditCategoryId = FilteredFormCategories.FirstOrDefault()?.Id ?? 0;
        }
    }

    partial void OnFilterSupplierIdChanged(int? value)
    {
        UpdateFilterCategories();
        RefreshProducts();
    }

    partial void OnFilterCategoryIdChanged(int? value)
    {
        RefreshProducts();
    }

    partial void OnEditSupplierIdChanged(int? value)
    {
        UpdateFormCategories();
    }

    partial void OnEditCategoryIdChanged(int value)
    {
        var cat = Categories.FirstOrDefault(c => c.Id == value);
        if (cat?.SupplierId != null && EditSupplierId != cat.SupplierId)
        {
            EditSupplierId = cat.SupplierId;
        }
    }

    partial void OnEditPurchasePriceChanged(decimal value)
    {
        if (EditCostPrice != value) EditCostPrice = value;
        if (EditPurchaseRate != value) EditPurchaseRate = value;
        UpdateProfitCalculation();
    }

    partial void OnEditPurchaseRateChanged(decimal value)
    {
        if (EditPurchasePrice != value) EditPurchasePrice = value;
        if (EditCostPrice != value) EditCostPrice = value;
        UpdateProfitCalculation();
    }

    partial void OnEditCostPriceChanged(decimal value)
    {
        if (EditPurchasePrice != value) EditPurchasePrice = value;
        if (EditPurchaseRate != value) EditPurchaseRate = value;
        UpdateProfitCalculation();
    }

    partial void OnEditSellingPriceChanged(decimal value)
    {
        if (EditMRP == 0 && value > 0)
        {
            EditMRP = value;
        }
        UpdateProfitCalculation();
    }

    partial void OnEditMRPChanged(decimal value)
    {
        if (EditSellingPrice == 0 && value > 0)
        {
            EditSellingPrice = value;
        }
        UpdateProfitCalculation();
    }

    private void UpdateProfitCalculation()
    {
        // Margin / Profit per unit = Selling Price - Purchase Price
        var buyPrice = EditPurchasePrice > 0 ? EditPurchasePrice : (EditPurchaseRate > 0 ? EditPurchaseRate : EditCostPrice);
        EditProfitPerUnit = EditSellingPrice - buyPrice;
        EditMarginPercent = EditSellingPrice > 0
            ? Math.Round(((EditSellingPrice - buyPrice) / EditSellingPrice) * 100, 1)
            : 0;
        EditMarkupPercent = buyPrice > 0
            ? Math.Round(((EditSellingPrice - buyPrice) / buyPrice) * 100, 1)
            : 0;
        EditDiscountFromMRP = EditMRP > EditSellingPrice && EditMRP > 0
            ? Math.Round(((EditMRP - EditSellingPrice) / EditMRP) * 100, 1)
            : 0;
    }

    private void RefreshProducts()
    {
        Products.Clear();
        var query = string.IsNullOrWhiteSpace(SearchQuery)
            ? _productService.GetAll()
            : _productService.Search(SearchQuery);

        if (FilterSupplierId.HasValue)
        {
            query = query.Where(p => p.SupplierId == FilterSupplierId.Value ||
                                     p.Category?.SupplierId == FilterSupplierId.Value).ToList();
        }

        if (FilterCategoryId.HasValue)
        {
            query = query.Where(p => p.CategoryId == FilterCategoryId.Value).ToList();
        }

        foreach (var p in query)
            Products.Add(p);

        RecalculateValuation();
    }

    private void RefreshLowStock()
    {
        LowStockProducts.Clear();
        foreach (var p in _productService.GetLowStock())
            LowStockProducts.Add(p);
    }

    [RelayCommand]
    public void Search()
    {
        RefreshProducts();
    }

    [RelayCommand]
    public void ResetFilters()
    {
        SearchQuery = "";
        FilterSupplierId = null;
        FilterCategoryId = null;
        RefreshProducts();
    }

    [RelayCommand]
    public void NewProduct()
    {
        IsNewProduct = true;
        EditPanelTitle = "➕ Add New Product";
        EditName = "";
        EditSku = "";
        EditBarcode = "";
        EditPurchasePrice = 0;
        EditCostPrice = 0;
        EditPurchaseRate = 0;
        EditSellingPrice = 0;
        EditMRP = 0;
        EditStockQuantity = 0;
        EditReorderLevel = 15;
        EditTaxRate = 0.0m;
        EditUnit = "piece";
        EditSupplierId = Suppliers.FirstOrDefault()?.Id;
        UpdateFormCategories();
        EditCategoryId = FilteredFormCategories.FirstOrDefault()?.Id ?? Categories.FirstOrDefault()?.Id ?? 0;
        UpdateProfitCalculation();
        ShowEditPanel = true;
    }

    [RelayCommand]
    public void EditProduct(Product product)
    {
        SelectedProduct = product;
        IsNewProduct = false;
        EditPanelTitle = $"✏️ Edit Product - {product.Name}";
        EditName = product.Name;
        EditSku = product.SKU;
        EditBarcode = product.Barcode;
        EditPurchasePrice = product.CostPrice;
        EditCostPrice = product.CostPrice;
        EditPurchaseRate = product.CostPrice;
        EditSellingPrice = product.SellingPrice;
        EditMRP = product.MRP > 0 ? product.MRP : product.SellingPrice;
        EditStockQuantity = product.StockQuantity;
        EditReorderLevel = product.ReorderLevel;
        EditTaxRate = 0.0m;
        EditUnit = string.IsNullOrWhiteSpace(product.Unit) ? "piece" : product.Unit;
        EditSupplierId = product.SupplierId ?? product.Category?.SupplierId;
        UpdateFormCategories();
        EditCategoryId = product.CategoryId;
        UpdateProfitCalculation();
        ShowEditPanel = true;
    }

    [RelayCommand]
    public void SaveProduct()
    {
        Product product;
        if (IsNewProduct)
        {
            product = new Product();
        }
        else
        {
            product = SelectedProduct!;
        }

        product.Name = EditName;
        product.SKU = EditSku;
        product.Barcode = EditBarcode;
        product.CostPrice = EditPurchasePrice > 0 ? EditPurchasePrice : (EditPurchaseRate > 0 ? EditPurchaseRate : EditCostPrice);
        product.SellingPrice = EditSellingPrice;
        product.MRP = EditMRP > 0 ? EditMRP : EditSellingPrice;
        product.StockQuantity = EditStockQuantity;
        product.ReorderLevel = EditReorderLevel;
        product.TaxRate = 0.0m;
        product.Unit = string.IsNullOrWhiteSpace(EditUnit) ? "piece" : EditUnit.Trim();
        product.SupplierId = EditSupplierId;
        product.CategoryId = EditCategoryId;

        _productService.Save(product);
        ShowEditPanel = false;
        RefreshProducts();
        RefreshLowStock();
    }

    [RelayCommand]
    public void CancelEdit()
    {
        ShowEditPanel = false;
    }

    [RelayCommand]
    public void OpenStockAdjust(Product product)
    {
        SelectedProduct = product;
        AdjustQuantity = 0;
        AdjustReason = "";
        ShowStockAdjust = true;
    }

    [RelayCommand]
    public void ApplyStockAdjust()
    {
        if (SelectedProduct == null || AdjustQuantity == 0) return;
        _productService.AdjustStock(SelectedProduct.Id, AdjustQuantity, AdjustReason, CurrentUserId);
        ShowStockAdjust = false;
        RefreshProducts();
        RefreshLowStock();
    }

    [RelayCommand]
    public void CancelStockAdjust()
    {
        ShowStockAdjust = false;
    }

    public void RefreshDeletedCount()
    {
        DeletedCount = _productService.GetDeletedProducts().Count;
    }

    [RelayCommand]
    public void DeleteProduct(Product product)
    {
        if (product == null) return;
        _productService.Delete(product.Id, CurrentUserId > 0 ? CurrentUserId : 1, "Deleted from inventory table");
        RefreshProducts();
        RefreshLowStock();
        RefreshDeletedCount();
    }

    [RelayCommand]
    public void OpenTrashModal()
    {
        TrashStatusMessage = "";
        RefreshTrash();
        ShowTrashModal = true;
    }

    private void RefreshTrash()
    {
        DeletedProducts.Clear();
        foreach (var p in _productService.GetDeletedProducts())
            DeletedProducts.Add(p);

        DeleteLogs.Clear();
        foreach (var log in _productService.GetDeleteLogs(50))
            DeleteLogs.Add(log);

        DeletedCount = DeletedProducts.Count;
    }

    [RelayCommand]
    public void CloseTrashModal()
    {
        ShowTrashModal = false;
        RefreshProducts();
        RefreshLowStock();
        RefreshDeletedCount();
    }

    [RelayCommand]
    public void RestoreProduct(Product product)
    {
        if (product == null) return;
        _productService.Restore(product.Id, CurrentUserId > 0 ? CurrentUserId : 1);
        TrashStatusMessage = $"✅ Restored '{product.Name}' back to active inventory!";
        RefreshTrash();
        RefreshProducts();
        RefreshLowStock();
    }

    private void RecalculateValuation()
    {
        TotalStockQuantity = Products.Sum(p => p.StockQuantity);
        TotalInventoryCost = Products.Sum(p => p.StockQuantity * p.CostPrice);
        TotalRetailValue = Products.Sum(p => p.StockQuantity * p.SellingPrice);
        TotalProjectedProfit = TotalRetailValue - TotalInventoryCost;
        OverallProfitMargin = TotalRetailValue > 0
            ? Math.Round((TotalProjectedProfit / TotalRetailValue) * 100, 1)
            : 0;
    }

    [RelayCommand]
    public void ExportInventoryValuation()
    {
        try
        {
            var sfd = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Export Inventory Valuation & Profit Report",
                Filter = "CSV Spreadsheet (*.csv)|*.csv|All Files (*.*)|*.*",
                FileName = $"Inventory_Valuation_Report_{DateTime.Now:yyyyMMdd_HHmm}.csv"
            };

            if (sfd.ShowDialog() != true) return;

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("==========================================================================================");
            sb.AppendLine("          ApexPOS - Inventory Purchase Rate Valuation & Profit Analysis Report             ");
            sb.AppendLine("==========================================================================================");
            sb.AppendLine($"Report Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"Filtered By Distributor: {(FilterSupplierId.HasValue ? (Suppliers.FirstOrDefault(s => s.Id == FilterSupplierId)?.Name ?? "All") : "All")}");
            sb.AppendLine($"Filtered By Category: {(FilterCategoryId.HasValue ? (Categories.FirstOrDefault(c => c.Id == FilterCategoryId)?.Name ?? "All") : "All")}");
            sb.AppendLine($"Total Active Products / SKUs: {Products.Count}");
            sb.AppendLine($"Total Physical Units In Stock: {TotalStockQuantity:N0}");
            sb.AppendLine($"Total Stock Purchase Cost (at Purchase Rate): Rs. {TotalInventoryCost:F2}");
            sb.AppendLine($"Total Stock Retail Selling Value (at MRP): Rs. {TotalRetailValue:F2}");
            sb.AppendLine($"Total Projected Gross Profit: Rs. {TotalProjectedProfit:F2}");
            sb.AppendLine($"Overall Projected Profit Margin: {OverallProfitMargin:F1}%");
            sb.AppendLine("==========================================================================================");
            sb.AppendLine();
            sb.AppendLine("SKU,Barcode,Product Name,Category,Distributor,Stock Units,Unit,Purchase Price (Rs.),Selling Price (Rs.),MRP (Rs.),Total Purchase Cost (Rs.),Total Retail Value (Rs.),Projected Profit (Rs.),Margin (%)");

            foreach (var p in Products)
            {
                decimal stockCost = p.StockQuantity * p.CostPrice;
                decimal retailVal = p.StockQuantity * p.SellingPrice;
                decimal profit = retailVal - stockCost;
                decimal margin = retailVal > 0 ? (profit / retailVal) * 100 : 0;

                sb.AppendLine(string.Join(",",
                    EscapeCsv(p.SKU),
                    EscapeCsv(p.Barcode),
                    EscapeCsv(p.Name),
                    EscapeCsv(p.Category?.Name ?? "—"),
                    EscapeCsv(p.DistributorName),
                    p.StockQuantity,
                    EscapeCsv(p.Unit),
                    p.CostPrice.ToString("F2"),
                    p.SellingPrice.ToString("F2"),
                    p.EffectiveMRP.ToString("F2"),
                    stockCost.ToString("F2"),
                    retailVal.ToString("F2"),
                    profit.ToString("F2"),
                    margin.ToString("F1") + "%"
                ));
            }

            System.IO.File.WriteAllText(sfd.FileName, sb.ToString(), System.Text.Encoding.UTF8);
            ExportStatusMessage = $"✅ Exported valuation report to {System.IO.Path.GetFileName(sfd.FileName)} ({Products.Count} items)";
        }
        catch (Exception ex)
        {
            ExportStatusMessage = $"❌ Export failed: {ex.Message}";
        }
    }

    private static string EscapeCsv(string? val)
    {
        if (string.IsNullOrEmpty(val)) return "\"\"";
        return $"\"{val.Replace("\"", "\"\"")}\"";
    }
}
