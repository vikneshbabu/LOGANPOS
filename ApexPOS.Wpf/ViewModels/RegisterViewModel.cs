using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ApexPOS.Domain.Entities;
using ApexPOS.Domain.Enums;
using ApexPOS.Services;

namespace ApexPOS.Wpf.ViewModels;

public partial class CartItem : ObservableObject
{
    [ObservableProperty] private int _productId;
    [ObservableProperty] private string _sku = "";
    [ObservableProperty] private string _productName = "";
    [ObservableProperty] private decimal _unitPrice;
    [ObservableProperty] private decimal _unitCost;
    [ObservableProperty] private string _unit = "piece";
    [ObservableProperty] private string _categoryIcon = "📦";
    [ObservableProperty] private int _quantity = 1;
    [ObservableProperty] private decimal _taxRate;
    [ObservableProperty] private decimal _discountAmount;

    public decimal LineSubtotal { get => UnitPrice * Quantity; set { } }
    public decimal LineTax { get => 0m; set { } }
    public decimal LineTotal { get => Math.Max(0m, LineSubtotal - DiscountAmount); set { } }

    partial void OnQuantityChanged(int value)
    {
        if (value < 1)
        {
            Quantity = 1;
            return;
        }
        NotifyTotals();
    }

    public void NotifyTotals()
    {
        OnPropertyChanged(nameof(LineSubtotal));
        OnPropertyChanged(nameof(LineTax));
        OnPropertyChanged(nameof(LineTotal));
    }
}

public class CartAction
{
    public enum ActionKind { AddItem, ChangeQty, RemoveItem }
    public ActionKind Kind { get; set; }
    public CartItem Item { get; set; } = null!;
    public int PreviousQty { get; set; }
    public int NewQty { get; set; }
}

public partial class RegisterViewModel : ObservableObject
{
    private readonly IProductService _productService;
    private readonly ICategoryService _categoryService;
    private readonly IOrderService _orderService;
    private readonly ICustomerService _customerService;
    private readonly IReceiptService _receiptService;
    private readonly ISettingsService? _settingsService;
    private readonly IAuthService? _authService;

    [ObservableProperty] private string _searchQuery = "";
    [ObservableProperty] private int? _selectedCategoryId;
    [ObservableProperty] private CartItem? _selectedCartItem;
    [ObservableProperty] private decimal _cartSubTotal;
    [ObservableProperty] private decimal _cartTaxTotal;
    [ObservableProperty] private decimal _cartDiscountTotal;
    [ObservableProperty] private decimal _cartGrandTotal;
    [ObservableProperty] private int _cartItemCount;

    // Payment
    [ObservableProperty] private bool _showPaymentPanel;
    [ObservableProperty] private decimal _paymentAmount;
    [ObservableProperty] private decimal _changeAmount;
    [ObservableProperty] private PaymentMethod _selectedPaymentMethod = PaymentMethod.Cash;
    [ObservableProperty] private string _paymentStatus = "";

    // Receipt Modal
    [ObservableProperty] private bool _showReceiptModal;
    [ObservableProperty] private string _currentReceiptText = "";
    [ObservableProperty] private string _completedInvoiceNumber = "";
    [ObservableProperty] private string _printConfirmation = "";

    // Sales Person
    [ObservableProperty] private User? _selectedSalesPerson;
    [ObservableProperty] private string _salesPersonSearchText = "";
    [ObservableProperty] private bool _isSalesPersonDropdownOpen;
    private bool _suppressSalesPersonSearchDropdown;

    public ObservableCollection<User> SalesPersons { get; } = new();
    public ObservableCollection<User> FilteredSalesPersons { get; } = new();

    public decimal SalesPersonPointsEarnedThisSale
    {
        get
        {
            var loyalty = _settingsService?.GetLoyaltySettings() ?? new LoyaltySettings();
            decimal rate = (loyalty != null && loyalty.AmountPerPoint > 0) ? loyalty.AmountPerPoint : 100m;
            return CartGrandTotal > 0 ? Math.Floor(CartGrandTotal / rate) : 0m;
        }
        set { }
    }

    // Customer
    [ObservableProperty] private Customer? _selectedCustomer;
    [ObservableProperty] private string _customerSearchText = "";
    [ObservableProperty] private bool _isCustomerDropdownOpen;
    [ObservableProperty] private bool _showQuickAddCustomerModal;
    [ObservableProperty] private string _quickCustomerName = "";
    [ObservableProperty] private string _quickCustomerPhone = "";
    [ObservableProperty] private decimal _quickCustomerCredit;
    [ObservableProperty] private string _quickCustomerMessage = "";

    // Customer Debt Application Dialog
    [ObservableProperty] private bool _showCreditDialog;
    [ObservableProperty] private decimal _creditAmountToApply;
    [ObservableProperty] private decimal _appliedStoreCredit;
    [ObservableProperty] private string _creditDialogErrorMessage = "";

    // Manual / Quick Product Addition
    [ObservableProperty] private bool _showQuickProductModal;
    [ObservableProperty] private string _quickProductName = "";
    [ObservableProperty] private decimal _quickProductPrice;
    [ObservableProperty] private decimal _quickProductCostPrice;
    [ObservableProperty] private int _quickProductQty = 1;
    [ObservableProperty] private int _quickProductCategoryId;
    [ObservableProperty] private string _quickProductStatus = "";

    // Held Orders / Multi-Tab
    [ObservableProperty] private bool _showHeldDrawer;
    [ObservableProperty] private int _heldOrdersCount;

    // Void Modal
    [ObservableProperty] private bool _showVoidModal;
    [ObservableProperty] private string _voidReason = "Customer changed mind";
    [ObservableProperty] private string _voidMessage = "";

    // Search and Suggestion State
    [ObservableProperty] private string _fuzzySearchNotice = "";

    private readonly Stack<CartAction> _cartHistory = new();
    private bool _suppressCustomerSearchDropdown;
    private bool _debtPromptHandled;

    public decimal AppliedDebt => AppliedStoreCredit;

    public decimal BillTotalBeforeCredit
    {
        get => Math.Max(0, CartSubTotal - CartDiscountTotal);
        set { }
    }

    public decimal RemainingCustomerCredit
    {
        get => SelectedCustomer != null ? Math.Max(0, SelectedCustomer.StoreCredit - AppliedStoreCredit) : 0;
        set { }
    }

    public decimal RemainingCustomerDebt
    {
        get => RemainingCustomerCredit;
        set { }
    }

    public decimal UnpaidDebtAmount
    {
        get => (PaymentAmount < CartGrandTotal && SelectedCustomer != null)
            ? (CartGrandTotal - PaymentAmount)
            : 0m;
        set { }
    }

    public bool IsPartialPaymentWithDebt => UnpaidDebtAmount > 0;

    public bool IsPartialPaymentWithoutCustomer => (PaymentAmount < CartGrandTotal && SelectedCustomer == null);

    public void NotifyPartialPaymentProperties()
    {
        OnPropertyChanged(nameof(UnpaidDebtAmount));
        OnPropertyChanged(nameof(IsPartialPaymentWithDebt));
        OnPropertyChanged(nameof(IsPartialPaymentWithoutCustomer));
    }

    public decimal PointsEarnedThisSale
    {
        get
        {
            var loyalty = _settingsService?.GetLoyaltySettings() ?? new LoyaltySettings();
            if (!loyalty.EnableLoyalty || loyalty.AmountPerPoint <= 0) return 0m;
            return Math.Floor(CartGrandTotal / loyalty.AmountPerPoint);
        }
        set { }
    }

    public int CashierId { get; set; }

    public ObservableCollection<Product> Products { get; } = new();
    public ObservableCollection<Category> Categories { get; } = new();
    public ObservableCollection<CartItem> CartItems { get; } = new();
    public ObservableCollection<Customer> Customers { get; } = new();
    public ObservableCollection<Customer> FilteredCustomers { get; } = new();
    public ObservableCollection<Order> HeldOrdersList { get; } = new();
    public ObservableCollection<Product> CrossSellingSuggestions { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCrossSellingVisible))]
    private bool _isCrossSellingDismissed;

    public bool IsCrossSellingVisible => !IsCrossSellingDismissed && CrossSellingSuggestions.Count > 0;

    public RegisterViewModel(IProductService productService, ICategoryService categoryService,
        IOrderService orderService, ICustomerService customerService, IReceiptService receiptService,
        ISettingsService? settingsService = null, IAuthService? authService = null)
    {
        _productService = productService;
        _categoryService = categoryService;
        _orderService = orderService;
        _customerService = customerService;
        _receiptService = receiptService;
        _settingsService = settingsService;
        _authService = authService;

        CartItems.CollectionChanged += CartItems_CollectionChanged;
    }

    private void CartItems_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Reset)
        {
            RecalculateCart();
            return;
        }

        if (e.NewItems != null)
        {
            foreach (CartItem item in e.NewItems)
            {
                item.PropertyChanged += CartItem_PropertyChanged;
            }
        }
        if (e.OldItems != null)
        {
            foreach (CartItem item in e.OldItems)
            {
                item.PropertyChanged -= CartItem_PropertyChanged;
            }
        }
        RecalculateCart();
    }

    private void CartItem_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(CartItem.Quantity) ||
            e.PropertyName == nameof(CartItem.UnitPrice) ||
            e.PropertyName == nameof(CartItem.DiscountAmount))
        {
            RecalculateCart();
        }
    }

    public void LoadData()
    {
        Categories.Clear();
        foreach (var cat in _categoryService.GetAll())
            Categories.Add(cat);

        RefreshProducts();

        Customers.Clear();
        foreach (var cust in _customerService.GetAll())
            Customers.Add(cust);

        FilterCustomers();
        LoadSalesPersons();
        LoadHeldOrdersList();
    }

    private void RefreshProducts()
    {
        Products.Clear();
        var products = string.IsNullOrWhiteSpace(SearchQuery)
            ? (SelectedCategoryId.HasValue
                ? _productService.GetByCategory(SelectedCategoryId.Value)
                : _productService.GetAll())
            : _productService.Search(SearchQuery);

        foreach (var p in products)
            Products.Add(p);

        if (!string.IsNullOrWhiteSpace(SearchQuery))
        {
            var trimmed = SearchQuery.Trim();
            if (Products.Count == 0)
            {
                FuzzySearchNotice = $"No products found matching \"{trimmed}\"";
            }
            else
            {
                var first = Products[0];
                bool isDirect = first.Name.Contains(trimmed, StringComparison.OrdinalIgnoreCase) ||
                               first.SKU.Contains(trimmed, StringComparison.OrdinalIgnoreCase) ||
                               (first.Barcode ?? "").Contains(trimmed, StringComparison.OrdinalIgnoreCase);
                if (!isDirect)
                {
                    FuzzySearchNotice = $"💡 Closest match for \"{trimmed}\": {first.Name}";
                }
                else
                {
                    FuzzySearchNotice = "";
                }
            }
        }
        else
        {
            FuzzySearchNotice = "";
        }
    }

    [RelayCommand]
    public void Search()
    {
        SelectedCategoryId = null;
        RefreshProducts();
    }

    partial void OnSearchQueryChanged(string value)
    {
        RefreshProducts();
    }

    [RelayCommand]
    public void FilterByCategory(int? categoryId)
    {
        SelectedCategoryId = categoryId;
        SearchQuery = "";
        RefreshProducts();
    }

    [RelayCommand]
    public void ShowAllProducts()
    {
        SelectedCategoryId = null;
        SearchQuery = "";
        RefreshProducts();
    }

    [RelayCommand]
    public void AddToCart(Product product)
    {
        var icon = product.Category?.Icon;
        if (string.IsNullOrWhiteSpace(icon)) icon = "📦";

        var existing = CartItems.FirstOrDefault(c => c.ProductId == product.Id);
        if (existing != null)
        {
            _cartHistory.Push(new CartAction
            {
                Kind = CartAction.ActionKind.ChangeQty,
                Item = existing,
                PreviousQty = existing.Quantity,
                NewQty = existing.Quantity + 1
            });
            existing.Quantity++;
            existing.NotifyTotals();
        }
        else
        {
            var newItem = new CartItem
            {
                ProductId = product.Id,
                Sku = product.SKU,
                ProductName = product.Name,
                UnitPrice = product.SellingPrice,
                UnitCost = product.CostPrice,
                TaxRate = product.TaxRate,
                Unit = product.Unit,
                CategoryIcon = icon,
                Quantity = 1
            };
            CartItems.Add(newItem);
            _cartHistory.Push(new CartAction
            {
                Kind = CartAction.ActionKind.AddItem,
                Item = newItem,
                PreviousQty = 0,
                NewQty = 1
            });
        }
        RecalculateCart();
    }

    [RelayCommand]
    public void IncreaseQuantity(CartItem item)
    {
        _cartHistory.Push(new CartAction
        {
            Kind = CartAction.ActionKind.ChangeQty,
            Item = item,
            PreviousQty = item.Quantity,
            NewQty = item.Quantity + 1
        });
        item.Quantity++;
        item.NotifyTotals();
        RecalculateCart();
    }

    [RelayCommand]
    public void DecreaseQuantity(CartItem item)
    {
        if (item.Quantity > 1)
        {
            _cartHistory.Push(new CartAction
            {
                Kind = CartAction.ActionKind.ChangeQty,
                Item = item,
                PreviousQty = item.Quantity,
                NewQty = item.Quantity - 1
            });
            item.Quantity--;
            item.NotifyTotals();
        }
        else
        {
            _cartHistory.Push(new CartAction
            {
                Kind = CartAction.ActionKind.RemoveItem,
                Item = item,
                PreviousQty = 1,
                NewQty = 0
            });
            CartItems.Remove(item);
        }
        RecalculateCart();
    }

    [RelayCommand]
    public void RemoveFromCart(CartItem item)
    {
        _cartHistory.Push(new CartAction
        {
            Kind = CartAction.ActionKind.RemoveItem,
            Item = item,
            PreviousQty = item.Quantity,
            NewQty = 0
        });
        CartItems.Remove(item);
        RecalculateCart();
    }

    [RelayCommand]
    public void OpenQuickProduct()
    {
        QuickProductName = "";
        QuickProductPrice = 0;
        QuickProductCostPrice = 0;
        QuickProductQty = 1;
        QuickProductCategoryId = Categories.FirstOrDefault()?.Id ?? 0;
        QuickProductStatus = "";
        ShowQuickProductModal = true;
    }

    [RelayCommand]
    public void CloseQuickProduct()
    {
        ShowQuickProductModal = false;
        QuickProductStatus = "";
    }

    [RelayCommand]
    public void SaveQuickProduct()
    {
        if (string.IsNullOrWhiteSpace(QuickProductName))
        {
            QuickProductStatus = "⚠️ Product Name is required.";
            return;
        }

        if (QuickProductPrice <= 0)
        {
            QuickProductStatus = "⚠️ Selling Price must be greater than 0.";
            return;
        }

        var qty = QuickProductQty > 0 ? QuickProductQty : 1;
        var product = _productService.CreateQuickProduct(
            QuickProductName.Trim(),
            QuickProductPrice,
            QuickProductCostPrice,
            QuickProductCategoryId,
            initialStock: Math.Max(100, qty),
            userId: CashierId > 0 ? CashierId : 1);

        // Refresh product catalog on POS screen
        RefreshProducts();

        // Automatically add to current cart
        var existing = CartItems.FirstOrDefault(c => c.ProductId == product.Id);
        if (existing != null)
        {
            _cartHistory.Push(new CartAction
            {
                Kind = CartAction.ActionKind.ChangeQty,
                Item = existing,
                PreviousQty = existing.Quantity,
                NewQty = existing.Quantity + qty
            });
            existing.Quantity += qty;
            existing.NotifyTotals();
        }
        else
        {
            var newItem = new CartItem
            {
                ProductId = product.Id,
                ProductName = product.Name,
                UnitPrice = product.SellingPrice,
                UnitCost = product.CostPrice,
                Quantity = qty,
                Unit = product.Unit,
                Sku = product.SKU,
                CategoryIcon = product.Category?.Icon ?? "📦",
                TaxRate = product.TaxRate
            };
            CartItems.Add(newItem);
            _cartHistory.Push(new CartAction
            {
                Kind = CartAction.ActionKind.AddItem,
                Item = newItem,
                PreviousQty = 0,
                NewQty = qty
            });
        }
        RecalculateCart();

        PaymentStatus = $"✅ Added '{product.Name}' to cart & catalog. Inventory details can be updated in Inventory page.";
        ShowQuickProductModal = false;
    }

    [RelayCommand]
    public void ClearCart()
    {
        _cartHistory.Clear();
        CartItems.Clear();
        SelectedCustomer = null;
        AppliedStoreCredit = 0;
        _debtPromptHandled = false;
        _suppressCustomerSearchDropdown = true;
        CustomerSearchText = "";
        _suppressCustomerSearchDropdown = false;
        RecalculateCart();
    }

    private void RecalculateCart()
    {
        CartSubTotal = CartItems.Sum(i => i.LineSubtotal);
        CartTaxTotal = 0m;
        decimal billBeforeDebt = Math.Max(0, CartSubTotal - CartDiscountTotal);

        if (CartItems.Count == 0)
        {
            AppliedStoreCredit = 0;
            _debtPromptHandled = false;
            _cartHistory.Clear();
            IsCrossSellingDismissed = false;
        }

        CartGrandTotal = billBeforeDebt + AppliedStoreCredit;
        CartItemCount = CartItems.Sum(i => i.Quantity);

        // Update Cross-Selling Suggestions based on current cart products
        CrossSellingSuggestions.Clear();
        if (CartItems.Count > 0)
        {
            var cartProductIds = CartItems.Select(c => c.ProductId).Distinct().ToList();
            var suggestions = _productService.GetCrossSellingSuggestions(cartProductIds, 3);
            if (suggestions != null)
            {
                foreach (var s in suggestions)
                {
                    CrossSellingSuggestions.Add(s);
                }
            }
        }
        OnPropertyChanged(nameof(IsCrossSellingVisible));

        OnPropertyChanged(nameof(PointsEarnedThisSale));
        OnPropertyChanged(nameof(SalesPersonPointsEarnedThisSale));
        OnPropertyChanged(nameof(BillTotalBeforeCredit));
        OnPropertyChanged(nameof(RemainingCustomerCredit));
        OnPropertyChanged(nameof(RemainingCustomerDebt));
        OnPropertyChanged(nameof(AppliedDebt));
        NotifyPartialPaymentProperties();
    }

    [RelayCommand]
    public void OpenPayment()
    {
        if (CartItems.Count == 0) return;

        // If customer has debt and cashier hasn't decided yet, prompt with debt dialog
        if (SelectedCustomer != null && SelectedCustomer.StoreCredit > 0 && !_debtPromptHandled)
        {
            CreditAmountToApply = SelectedCustomer.StoreCredit; // default to full debt
            CreditDialogErrorMessage = "";
            ShowCreditDialog = true;
            return;
        }

        ShowPaymentPanel = true;
        PaymentAmount = CartGrandTotal;
        ChangeAmount = 0;
        PaymentStatus = "";
        NotifyPartialPaymentProperties();
    }

    [RelayCommand]
    public void ClosePayment()
    {
        ShowPaymentPanel = false;
    }

    [RelayCommand]
    public void SetQuickCash(object? param)
    {
        if (param != null && decimal.TryParse(param.ToString(), out var amount))
        {
            PaymentAmount = amount;
            ChangeAmount = Math.Max(0, amount - CartGrandTotal);
        }
    }

    partial void OnPaymentAmountChanged(decimal value)
    {
        ChangeAmount = Math.Max(0, value - CartGrandTotal);
        NotifyPartialPaymentProperties();
    }

    [RelayCommand]
    public void ProcessPayment()
    {
        if (CartItems.Count == 0) return;

        // Check for partial payment
        decimal remainingDebt = 0;
        if (PaymentAmount < CartGrandTotal)
        {
            if (SelectedCustomer == null)
            {
                PaymentStatus = "⚠️ Partial payment requires selecting a customer to record the remaining debt.";
                return;
            }
            remainingDebt = CartGrandTotal - PaymentAmount;
        }

        var items = CartItems.Select(c => new OrderItem
        {
            ProductId = c.ProductId,
            SKU = c.Sku,
            ProductName = c.ProductName,
            UnitPrice = c.UnitPrice,
            UnitCost = c.UnitCost,
            Quantity = c.Quantity,
            TaxRate = c.TaxRate,
            DiscountAmount = c.DiscountAmount
        }).ToList();

        decimal spPoints = (SelectedSalesPerson != null) ? SalesPersonPointsEarnedThisSale : 0m;
        var order = SelectedSalesPerson != null
            ? _orderService.CreateOrder(
                CashierId > 0 ? CashierId : 1,
                SelectedCustomer?.Id,
                items,
                CartDiscountTotal,
                SelectedSalesPerson.Id,
                SelectedSalesPerson.FullName,
                spPoints)
            : _orderService.CreateOrder(
                CashierId > 0 ? CashierId : 1,
                SelectedCustomer?.Id,
                items,
                CartDiscountTotal);

        // 1. If past customer debt was added to this bill
        if (SelectedCustomer != null && AppliedStoreCredit > 0)
        {
            _orderService.AddDebtToOrder(order.Id, AppliedStoreCredit);
            _customerService.DeductCredit(SelectedCustomer.Id, AppliedStoreCredit);
            var updated = _customerService.GetById(SelectedCustomer.Id);
            if (updated != null) SelectedCustomer.StoreCredit = updated.StoreCredit;
        }

        // 2. Pay order
        if (remainingDebt > 0 && SelectedCustomer != null)
        {
            _orderService.CompletePaymentWithDebt(order.Id, SelectedPaymentMethod, PaymentAmount, remainingDebt);
            _customerService.AddCredit(SelectedCustomer.Id, remainingDebt);
            var updated = _customerService.GetById(SelectedCustomer.Id);
            if (updated != null) SelectedCustomer.StoreCredit = updated.StoreCredit;

            PaymentStatus = $"✅ Payment Complete | Invoice: {order.InvoiceNumber} | ₹{remainingDebt:F2} added to customer debt";
        }
        else
        {
            _orderService.CompletePayment(order.Id, SelectedPaymentMethod, PaymentAmount);
            PaymentStatus = $"✅ Payment Complete | Invoice: {order.InvoiceNumber}" + (ChangeAmount > 0 ? $" | Change: ₹{ChangeAmount:F2}" : "");
        }

        // Award loyalty points to customer if enabled and earned
        if (SelectedCustomer != null && PointsEarnedThisSale > 0)
        {
            _customerService.AddLoyaltyPoints(SelectedCustomer.Id, PointsEarnedThisSale);
            var updated = _customerService.GetById(SelectedCustomer.Id);
            if (updated != null) SelectedCustomer.LoyaltyPoints = updated.LoyaltyPoints;
        }

        // Award loyalty points to sales person if assigned
        if (SelectedSalesPerson != null && spPoints > 0 && _authService != null)
        {
            _authService.AddLoyaltyPoints(SelectedSalesPerson.Id, spPoints);
            SelectedSalesPerson.LoyaltyPoints += spPoints;
        }

        // Generate receipt
        var orderDetails = _orderService.GetOrderWithDetails(order.Id) ?? order;
        CurrentReceiptText = _receiptService.GenerateReceiptText(orderDetails);
        CompletedInvoiceNumber = order.InvoiceNumber;
        PrintConfirmation = "";
        ShowReceiptModal = true;

        // Clear cart and customer
        CartItems.Clear();
        SelectedCustomer = null;
        AppliedStoreCredit = 0;
        _debtPromptHandled = false;
        _suppressCustomerSearchDropdown = true;
        CustomerSearchText = "";
        _suppressCustomerSearchDropdown = false;
        RecalculateCart();
        ShowPaymentPanel = false;
        RefreshProducts(); // Refresh stock display
    }

    [RelayCommand]
    public void PayWithStoreCredit()
    {
        if (SelectedCustomer != null && SelectedCustomer.StoreCredit > 0)
        {
            OpenCreditDialog();
        }
    }

    partial void OnCustomerSearchTextChanged(string value)
    {
        if (_suppressCustomerSearchDropdown) return;
        FilterCustomers();
        IsCustomerDropdownOpen = true;
    }

    partial void OnIsCustomerDropdownOpenChanged(bool value)
    {
        if (value)
        {
            FilterCustomers();
        }
    }

    private void FilterCustomers()
    {
        FilteredCustomers.Clear();
        var query = CustomerSearchText?.Trim() ?? "";
        var list = string.IsNullOrWhiteSpace(query)
            ? Customers.ToList()
            : Customers.Where(c =>
                c.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                c.Phone.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                c.Code.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();

        foreach (var c in list)
            FilteredCustomers.Add(c);
    }

    [RelayCommand]
    public void SelectCustomer(Customer customer)
    {
        SelectedCustomer = customer;
        _suppressCustomerSearchDropdown = true;
        CustomerSearchText = customer.Name;
        _suppressCustomerSearchDropdown = false;
        IsCustomerDropdownOpen = false;

        _debtPromptHandled = false;
        AppliedStoreCredit = 0;
        RecalculateCart();
        OnPropertyChanged(nameof(RemainingCustomerCredit));
        OnPropertyChanged(nameof(RemainingCustomerDebt));
        NotifyPartialPaymentProperties();
    }

    [RelayCommand]
    public void SelectFirstFilteredCustomer()
    {
        if (FilteredCustomers.Count > 0)
        {
            SelectCustomer(FilteredCustomers[0]);
        }
    }

    [RelayCommand]
    public void ToggleCustomerDropdown()
    {
        IsCustomerDropdownOpen = !IsCustomerDropdownOpen;
        if (IsCustomerDropdownOpen)
        {
            FilterCustomers();
        }
    }

    [RelayCommand]
    public void OpenCreditDialog()
    {
        if (SelectedCustomer == null || SelectedCustomer.StoreCredit <= 0) return;
        CreditAmountToApply = AppliedStoreCredit > 0
            ? AppliedStoreCredit
            : SelectedCustomer.StoreCredit;
        CreditDialogErrorMessage = "";
        ShowCreditDialog = true;
    }

    [RelayCommand]
    public void CloseCreditDialog()
    {
        ShowCreditDialog = false;
        CreditDialogErrorMessage = "";
        _debtPromptHandled = true;
        AppliedStoreCredit = 0;
        RecalculateCart();

        // Proceed to payment modal with items only
        ShowPaymentPanel = true;
        PaymentAmount = CartGrandTotal;
        ChangeAmount = 0;
        PaymentStatus = "";
    }

    [RelayCommand]
    public void DismissCreditDialog()
    {
        ShowCreditDialog = false;
        CreditDialogErrorMessage = "";
    }

    [RelayCommand]
    public void ApplyCreditToBill()
    {
        if (SelectedCustomer == null)
        {
            ShowCreditDialog = false;
            return;
        }

        if (CreditAmountToApply <= 0)
        {
            CreditDialogErrorMessage = "Please enter an amount greater than ₹0.00.";
            return;
        }

        if (CreditAmountToApply > SelectedCustomer.StoreCredit)
        {
            CreditDialogErrorMessage = $"Amount cannot exceed customer debt (₹{SelectedCustomer.StoreCredit:F2}).";
            return;
        }

        AppliedStoreCredit = CreditAmountToApply;
        _debtPromptHandled = true;
        CreditDialogErrorMessage = "";
        ShowCreditDialog = false;
        RecalculateCart();
        OnPropertyChanged(nameof(RemainingCustomerCredit));
        OnPropertyChanged(nameof(RemainingCustomerDebt));

        // Proceed to payment modal with added debt
        ShowPaymentPanel = true;
        PaymentAmount = CartGrandTotal;
        ChangeAmount = 0;
        PaymentStatus = "";
    }

    [RelayCommand]
    public void RemoveAppliedCredit()
    {
        AppliedStoreCredit = 0;
        _debtPromptHandled = true;
        RecalculateCart();
        OnPropertyChanged(nameof(RemainingCustomerCredit));
        OnPropertyChanged(nameof(RemainingCustomerDebt));
        if (ShowPaymentPanel)
        {
            PaymentAmount = CartGrandTotal;
            ChangeAmount = 0;
        }
    }

    [RelayCommand]
    public void SetCreditPreset(object? param)
    {
        if (SelectedCustomer == null || param == null) return;
        string preset = param.ToString() ?? "";

        if (preset == "full_debt" || preset == "all_credit" || preset == "max_bill")
        {
            CreditAmountToApply = SelectedCustomer.StoreCredit;
        }
        else if (decimal.TryParse(preset, out var amt))
        {
            CreditAmountToApply = Math.Min(amt, SelectedCustomer.StoreCredit);
        }
        CreditDialogErrorMessage = "";
    }

    [RelayCommand]
    public void OpenQuickAddCustomer()
    {
        var text = CustomerSearchText?.Trim() ?? "";
        bool isPhone = text.Length > 0 && text.All(ch => char.IsDigit(ch) || ch == '+' || ch == '-' || ch == ' ');
        QuickCustomerName = isPhone ? "" : text;
        QuickCustomerPhone = isPhone ? text : "";
        QuickCustomerCredit = 0;
        QuickCustomerMessage = "";
        ShowQuickAddCustomerModal = true;
    }

    [RelayCommand]
    public void CloseQuickAddCustomer()
    {
        ShowQuickAddCustomerModal = false;
    }

    [RelayCommand]
    public void SaveQuickCustomer()
    {
        if (string.IsNullOrWhiteSpace(QuickCustomerName))
        {
            QuickCustomerMessage = "Customer name is required.";
            return;
        }

        var customer = new Customer
        {
            Name = QuickCustomerName.Trim(),
            Phone = QuickCustomerPhone.Trim(),
            Code = $"CUST-{DateTime.Now:mmss}",
            LoyaltyPoints = 0,
            StoreCredit = QuickCustomerCredit,
            IsActive = true
        };

        _customerService.Save(customer);
        Customers.Add(customer);
        FilteredCustomers.Add(customer);
        SelectedCustomer = customer;
        _suppressCustomerSearchDropdown = true;
        CustomerSearchText = customer.Name;
        _suppressCustomerSearchDropdown = false;
        IsCustomerDropdownOpen = false;
        ShowQuickAddCustomerModal = false;

        _debtPromptHandled = false;
        AppliedStoreCredit = 0;
        RecalculateCart();
        OnPropertyChanged(nameof(RemainingCustomerCredit));
        OnPropertyChanged(nameof(RemainingCustomerDebt));
        NotifyPartialPaymentProperties();
    }

    [RelayCommand]
    public void ClearCustomer()
    {
        SelectedCustomer = null;
        AppliedStoreCredit = 0;
        _debtPromptHandled = false;
        _suppressCustomerSearchDropdown = true;
        CustomerSearchText = "";
        _suppressCustomerSearchDropdown = false;
        IsCustomerDropdownOpen = false;
        FilterCustomers();
        RecalculateCart();
        OnPropertyChanged(nameof(RemainingCustomerCredit));
        OnPropertyChanged(nameof(RemainingCustomerDebt));
        NotifyPartialPaymentProperties();
    }

    partial void OnSalesPersonSearchTextChanged(string value)
    {
        if (_suppressSalesPersonSearchDropdown) return;
        FilterSalesPersons();
        IsSalesPersonDropdownOpen = true;
    }

    partial void OnIsSalesPersonDropdownOpenChanged(bool value)
    {
        if (value) FilterSalesPersons();
    }

    private void FilterSalesPersons()
    {
        FilteredSalesPersons.Clear();
        var query = SalesPersonSearchText?.Trim() ?? "";
        var list = string.IsNullOrWhiteSpace(query)
            ? SalesPersons.ToList()
            : SalesPersons.Where(u =>
                u.FullName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                u.Username.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                u.Role.ToString().Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();

        foreach (var u in list)
            FilteredSalesPersons.Add(u);
    }

    [RelayCommand]
    public void SelectSalesPerson(User? salesPerson)
    {
        SelectedSalesPerson = salesPerson;
        _suppressSalesPersonSearchDropdown = true;
        SalesPersonSearchText = salesPerson?.FullName ?? "";
        _suppressSalesPersonSearchDropdown = false;
        IsSalesPersonDropdownOpen = false;
    }

    [RelayCommand]
    public void SelectFirstFilteredSalesPerson()
    {
        if (FilteredSalesPersons.Count > 0)
        {
            SelectSalesPerson(FilteredSalesPersons[0]);
        }
    }

    [RelayCommand]
    public void ToggleSalesPersonDropdown()
    {
        IsSalesPersonDropdownOpen = !IsSalesPersonDropdownOpen;
        if (IsSalesPersonDropdownOpen)
        {
            FilterSalesPersons();
        }
    }

    [RelayCommand]
    public void ClearSalesPerson()
    {
        SelectedSalesPerson = null;
        _suppressSalesPersonSearchDropdown = true;
        SalesPersonSearchText = "";
        _suppressSalesPersonSearchDropdown = false;
        IsSalesPersonDropdownOpen = false;
    }

    public void LoadSalesPersons()
    {
        SalesPersons.Clear();
        if (_authService != null)
        {
            var list = _authService.GetSalesPersons();
            foreach (var u in list)
                SalesPersons.Add(u);
        }
        FilterSalesPersons();

        if (SelectedSalesPerson == null && CashierId > 0)
        {
            var current = SalesPersons.FirstOrDefault(u => u.Id == CashierId);
            if (current != null)
            {
                SelectSalesPerson(current);
            }
        }
    }

    [RelayCommand]
    public void CloseReceiptModal()
    {
        ShowReceiptModal = false;
        CurrentReceiptText = "";
        PrintConfirmation = "";
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
                    Text = CurrentReceiptText,
                    FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                    FontSize = 10,
                    Margin = new System.Windows.Thickness(15)
                };
                printDialog.PrintVisual(textBlock, $"Receipt_{CompletedInvoiceNumber}");
                PrintConfirmation = "🖨️ Receipt sent to printer successfully!";
            }
        }
        catch (Exception ex)
        {
            PrintConfirmation = $"Print notice: {ex.Message}";
        }
    }

    [RelayCommand]
    public void DownloadReceipt()
    {
        try
        {
            var saveDialog = new Microsoft.Win32.SaveFileDialog
            {
                FileName = $"Receipt_{CompletedInvoiceNumber}.txt",
                DefaultExt = ".txt",
                Filter = "Text Documents (*.txt)|*.txt|All Files (*.*)|*.*"
            };
            if (saveDialog.ShowDialog() == true)
            {
                System.IO.File.WriteAllText(saveDialog.FileName, CurrentReceiptText);
                PrintConfirmation = $"💾 Receipt saved: {System.IO.Path.GetFileName(saveDialog.FileName)}";
            }
        }
        catch (Exception ex)
        {
            PrintConfirmation = $"Save error: {ex.Message}";
        }
    }

    public void LoadHeldOrder(Order order)
    {
        _cartHistory.Clear();
        CartItems.Clear();
        SelectedCustomer = order.Customer;
        CartDiscountTotal = order.DiscountTotal;

        if (order.SalesPerson != null)
        {
            SelectSalesPerson(order.SalesPerson);
        }
        else if (order.SalesPersonId.HasValue)
        {
            var sp = SalesPersons.FirstOrDefault(u => u.Id == order.SalesPersonId.Value);
            if (sp != null) SelectSalesPerson(sp);
        }

        foreach (var item in order.Items)
        {
            var prod = _productService.GetById(item.ProductId);
            CartItems.Add(new CartItem
            {
                ProductId = item.ProductId,
                Sku = item.SKU,
                ProductName = item.ProductName,
                UnitPrice = item.UnitPrice,
                UnitCost = item.UnitCost,
                Quantity = item.Quantity,
                TaxRate = item.TaxRate,
                DiscountAmount = item.DiscountAmount,
                CategoryIcon = prod?.Category?.Icon ?? "📦"
            });
        }

        RecalculateCart();
    }

    [RelayCommand]
    public void UndoLastItem()
    {
        if (_cartHistory.Count == 0)
        {
            PaymentStatus = "⚠️ Nothing to undo.";
            return;
        }

        var action = _cartHistory.Pop();
        switch (action.Kind)
        {
            case CartAction.ActionKind.AddItem:
                CartItems.Remove(action.Item);
                PaymentStatus = $"↩️ Undid adding '{action.Item.ProductName}'.";
                break;

            case CartAction.ActionKind.ChangeQty:
                action.Item.Quantity = action.PreviousQty;
                action.Item.NotifyTotals();
                PaymentStatus = $"↩️ Restored '{action.Item.ProductName}' quantity to {action.PreviousQty}.";
                break;

            case CartAction.ActionKind.RemoveItem:
                action.Item.Quantity = action.PreviousQty;
                if (!CartItems.Contains(action.Item))
                {
                    CartItems.Add(action.Item);
                }
                action.Item.NotifyTotals();
                PaymentStatus = $"↩️ Restored '{action.Item.ProductName}' to cart.";
                break;
        }

        RecalculateCart();
    }

    [RelayCommand]
    public void OpenVoidModal()
    {
        if (CartItems.Count == 0)
        {
            PaymentStatus = "⚠️ Cart is already empty.";
            return;
        }
        VoidReason = "Customer changed mind";
        VoidMessage = "";
        ShowVoidModal = true;
    }

    [RelayCommand]
    public void CloseVoidModal()
    {
        ShowVoidModal = false;
    }

    [RelayCommand]
    public void ConfirmVoid()
    {
        if (CartItems.Count == 0)
        {
            ShowVoidModal = false;
            return;
        }

        var itemCount = CartItemCount;
        var totalAmount = CartGrandTotal;

        _orderService.LogVoidAudit(CashierId > 0 ? CashierId : 1, itemCount, totalAmount, VoidReason);

        _cartHistory.Clear();
        CartItems.Clear();
        SelectedCustomer = null;
        AppliedStoreCredit = 0;
        _debtPromptHandled = false;
        _suppressCustomerSearchDropdown = true;
        CustomerSearchText = "";
        _suppressCustomerSearchDropdown = false;
        RecalculateCart();
        ShowVoidModal = false;
        PaymentStatus = $"🚫 Voided bill ({itemCount} items, ₹{totalAmount:F2}) - Logged to Audit.";
    }

    public void LoadHeldOrdersList()
    {
        HeldOrdersList.Clear();
        var held = _orderService.GetHeldOrders();
        foreach (var o in held)
        {
            HeldOrdersList.Add(o);
        }
        HeldOrdersCount = HeldOrdersList.Count;
    }

    [RelayCommand]
    public void ToggleHeldDrawer()
    {
        ShowHeldDrawer = !ShowHeldDrawer;
        if (ShowHeldDrawer)
        {
            LoadHeldOrdersList();
        }
    }

    [RelayCommand]
    public void CloseHeldDrawer()
    {
        ShowHeldDrawer = false;
    }

    [RelayCommand]
    public void ParkCurrentOrder()
    {
        if (CartItems.Count == 0)
        {
            PaymentStatus = "⚠️ Cannot park an empty cart.";
            return;
        }

        var items = CartItems.Select(c => new OrderItem
        {
            ProductId = c.ProductId,
            SKU = c.Sku,
            ProductName = c.ProductName,
            UnitPrice = c.UnitPrice,
            UnitCost = c.UnitCost,
            Quantity = c.Quantity,
            TaxRate = c.TaxRate,
            DiscountAmount = c.DiscountAmount
        }).ToList();

        var order = SelectedSalesPerson != null
            ? _orderService.CreateOrder(
                CashierId > 0 ? CashierId : 1,
                SelectedCustomer?.Id,
                items,
                CartDiscountTotal,
                SelectedSalesPerson.Id,
                SelectedSalesPerson.FullName,
                0)
            : _orderService.CreateOrder(
                CashierId > 0 ? CashierId : 1,
                SelectedCustomer?.Id,
                items,
                CartDiscountTotal);
        _orderService.HoldOrder(order.Id, $"Parked from register at {DateTime.Now:hh:mm tt}");

        _cartHistory.Clear();
        CartItems.Clear();
        SelectedCustomer = null;
        AppliedStoreCredit = 0;
        _debtPromptHandled = false;
        _suppressCustomerSearchDropdown = true;
        CustomerSearchText = "";
        _suppressCustomerSearchDropdown = false;
        RecalculateCart();
        LoadHeldOrdersList();

        PaymentStatus = $"📌 Order #{order.InvoiceNumber} parked in held tray ({HeldOrdersCount} held).";
    }

    [RelayCommand]
    public void ResumeHeldOrderFromRegister(Order order)
    {
        if (order == null) return;

        if (CartItems.Count > 0)
        {
            // Seamlessly park the current order first so cashier doesn't lose anything
            ParkCurrentOrder();
        }

        _orderService.ResumeOrder(order.Id);
        LoadHeldOrder(order);
        LoadHeldOrdersList();
        ShowHeldDrawer = false;
        PaymentStatus = $"▶ Resumed order #{order.InvoiceNumber} into register.";
    }

    [RelayCommand]
    public void DismissHeldOrderFromRegister(Order order)
    {
        if (order == null) return;
        _orderService.CancelHeldOrder(order.Id, CashierId > 0 ? CashierId : 1);
        LoadHeldOrdersList();
        PaymentStatus = $"🗑 Cancelled held order #{order.InvoiceNumber} and restored stock.";
    }

    [RelayCommand]
    public void AddSuggestedProduct(Product product)
    {
        AddToCart(product);
        PaymentStatus = $"➕ Added suggested item '{product.Name}' to cart.";
    }

    [RelayCommand]
    public void DismissCrossSelling()
    {
        IsCrossSellingDismissed = true;
    }

    [RelayCommand]
    public void HoldOrder()
    {
        ParkCurrentOrder();
    }

    [RelayCommand]
    public void ScanBarcode()
    {
        if (string.IsNullOrWhiteSpace(SearchQuery)) return;

        var product = _productService.GetByBarcode(SearchQuery);
        if (product != null)
        {
            AddToCart(product);
            SearchQuery = "";
        }
    }
}
