using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ApexPOS.Domain.Entities;
using ApexPOS.Domain.Enums;
using ApexPOS.Services;

namespace ApexPOS.Wpf.ViewModels;

public partial class PurchasesViewModel : ObservableObject
{
    private readonly PurchaseService _purchaseService;
    private readonly ProductService _productService;

    [ObservableProperty] private int _selectedTabIndex = 0; // 0 = Distributors, 1 = Purchase Orders
    [ObservableProperty] private string _searchQuery = "";
    [ObservableProperty] private string _statusMessage = "";

    // Distributor Form Modal
    [ObservableProperty] private bool _showSupplierModal;
    [ObservableProperty] private bool _isNewSupplier;
    [ObservableProperty] private Supplier? _selectedSupplier;
    [ObservableProperty] private string _editName = "";
    [ObservableProperty] private string _editContactPerson = "";
    [ObservableProperty] private string _editPhone = "";
    [ObservableProperty] private string _editEmail = "";
    [ObservableProperty] private string _editAddress = "";
    [ObservableProperty] private string _editHandledProducts = "";
    [ObservableProperty] private string _editNotes = "";

    // New Purchase Order Modal
    [ObservableProperty] private bool _showNewPoModal;
    [ObservableProperty] private Supplier? _poSelectedSupplier;
    [ObservableProperty] private Product? _poSelectedProduct;
    [ObservableProperty] private int _poQuantity = 10;
    [ObservableProperty] private decimal _poUnitCost = 0;
    [ObservableProperty] private string _poNotes = "";

    public int CurrentUserId { get; set; }

    public ObservableCollection<Supplier> Suppliers { get; } = new();
    public ObservableCollection<PurchaseOrder> PurchaseOrders { get; } = new();
    public ObservableCollection<Product> AvailableProducts { get; } = new();

    public PurchasesViewModel(PurchaseService purchaseService, ProductService productService)
    {
        _purchaseService = purchaseService;
        _productService = productService;
    }

    public void LoadData()
    {
        RefreshSuppliers();
        RefreshPurchaseOrders();
        AvailableProducts.Clear();
        foreach (var p in _productService.GetAll())
            AvailableProducts.Add(p);
    }

    private void RefreshSuppliers()
    {
        Suppliers.Clear();
        var list = _purchaseService.GetAllSuppliers();
        if (!string.IsNullOrWhiteSpace(SearchQuery))
        {
            var q = SearchQuery.ToLower();
            list = list.Where(s => s.Name.ToLower().Contains(q) ||
                                   s.Address.ToLower().Contains(q) ||
                                   s.HandledProducts.ToLower().Contains(q) ||
                                   s.Phone.Contains(q)).ToList();
        }

        foreach (var s in list)
            Suppliers.Add(s);
    }

    private void RefreshPurchaseOrders()
    {
        PurchaseOrders.Clear();
        foreach (var po in _purchaseService.GetAllPurchaseOrders())
            PurchaseOrders.Add(po);
    }

    [RelayCommand]
    public void Search()
    {
        RefreshSuppliers();
    }

    partial void OnSearchQueryChanged(string value)
    {
        RefreshSuppliers();
    }

    // --- Supplier Commands ---
    [RelayCommand]
    public void NewSupplier()
    {
        IsNewSupplier = true;
        EditName = "";
        EditContactPerson = "";
        EditPhone = "";
        EditEmail = "";
        EditAddress = "";
        EditHandledProducts = "";
        EditNotes = "";
        ShowSupplierModal = true;
        StatusMessage = "";
    }

    [RelayCommand]
    public void EditSupplier(Supplier supplier)
    {
        SelectedSupplier = supplier;
        IsNewSupplier = false;
        EditName = supplier.Name;
        EditContactPerson = supplier.ContactPerson;
        EditPhone = supplier.Phone;
        EditEmail = supplier.Email;
        EditAddress = supplier.Address;
        EditHandledProducts = supplier.HandledProducts;
        EditNotes = supplier.Notes;
        ShowSupplierModal = true;
        StatusMessage = "";
    }

    [RelayCommand]
    public void SaveSupplier()
    {
        if (string.IsNullOrWhiteSpace(EditName))
        {
            StatusMessage = "Distributor name is required.";
            return;
        }

        var s = IsNewSupplier ? new Supplier() : SelectedSupplier!;
        s.Name = EditName.Trim();
        s.ContactPerson = EditContactPerson.Trim();
        s.Phone = EditPhone.Trim();
        s.Email = EditEmail.Trim();
        s.Address = EditAddress.Trim();
        s.HandledProducts = EditHandledProducts.Trim();
        s.Notes = EditNotes.Trim();
        s.IsActive = true;

        _purchaseService.SaveSupplier(s);
        ShowSupplierModal = false;
        RefreshSuppliers();
        StatusMessage = $"Distributor '{s.Name}' saved successfully.";
    }

    [RelayCommand]
    public void DeleteSupplier(Supplier supplier)
    {
        _purchaseService.DeleteSupplier(supplier.Id);
        RefreshSuppliers();
        StatusMessage = $"Distributor '{supplier.Name}' removed.";
    }

    [RelayCommand]
    public void CloseSupplierModal()
    {
        ShowSupplierModal = false;
    }

    // --- Purchase Order Commands ---
    [RelayCommand]
    public void OpenNewPoModal()
    {
        PoSelectedSupplier = Suppliers.FirstOrDefault();
        PoSelectedProduct = AvailableProducts.FirstOrDefault();
        PoQuantity = 10;
        PoUnitCost = PoSelectedProduct?.CostPrice ?? 0;
        PoNotes = "";
        ShowNewPoModal = true;
        StatusMessage = "";
    }

    partial void OnPoSelectedProductChanged(Product? value)
    {
        if (value != null)
            PoUnitCost = value.CostPrice;
    }

    [RelayCommand]
    public void CreatePurchaseOrder()
    {
        if (PoSelectedSupplier == null)
        {
            StatusMessage = "Please select a distributor.";
            return;
        }
        if (PoSelectedProduct == null || PoQuantity <= 0)
        {
            StatusMessage = "Please select a valid product and quantity.";
            return;
        }

        var items = new List<PurchaseOrderItem>
        {
            new PurchaseOrderItem
            {
                ProductId = PoSelectedProduct.Id,
                ProductName = PoSelectedProduct.Name,
                Quantity = PoQuantity,
                UnitCost = PoUnitCost
            }
        };

        var po = _purchaseService.CreatePurchaseOrder(PoSelectedSupplier.Id, PoNotes, items);
        ShowNewPoModal = false;
        RefreshPurchaseOrders();
        StatusMessage = $"Created Purchase Order {po.PONumber} for {PoSelectedSupplier.Name}.";
    }

    [RelayCommand]
    public void ReceiveOrder(PurchaseOrder po)
    {
        if (po.Status == PurchaseOrderStatus.Received) return;

        _purchaseService.ReceivePurchaseOrder(po.Id, CurrentUserId);
        RefreshPurchaseOrders();
        StatusMessage = $"PO {po.PONumber} received! Inventory stock updated.";
    }

    [RelayCommand]
    public void ClosePoModal()
    {
        ShowNewPoModal = false;
    }
}
