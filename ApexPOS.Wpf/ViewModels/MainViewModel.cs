using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ApexPOS.Domain.Entities;

namespace ApexPOS.Wpf.ViewModels;

public partial class MainViewModel : ObservableObject
{
    [ObservableProperty]
    private ObservableObject? _currentView;

    [ObservableProperty]
    private string _currentViewName = "Dashboard";

    [ObservableProperty]
    private string _loggedInUser = "";

    [ObservableProperty]
    private string _loggedInRole = "";

    [ObservableProperty]
    private int _loggedInUserId;

    [ObservableProperty]
    private bool _isLoggedIn;

    [ObservableProperty]
    private bool _canViewDashboard = true;

    [ObservableProperty]
    private bool _canViewRegister = true;

    [ObservableProperty]
    private bool _canViewInventory = true;

    [ObservableProperty]
    private bool _canViewCategories = true;

    [ObservableProperty]
    private bool _canViewCustomers = true;

    [ObservableProperty]
    private bool _canViewPurchases = true;

    [ObservableProperty]
    private bool _canViewOrders = true;

    [ObservableProperty]
    private bool _canViewSettings = true;

    [ObservableProperty]
    private bool _canViewPurchaseLogbook = true;

    public string CurrentDate => DateTime.Now.ToString("dddd, MMMM d, yyyy");

    private readonly Func<DashboardViewModel> _dashboardFactory;
    private readonly Func<RegisterViewModel> _registerFactory;
    private readonly Func<InventoryViewModel> _inventoryFactory;
    private readonly Func<CustomersViewModel> _customersFactory;
    private readonly Func<OrdersViewModel> _ordersFactory;
    private readonly Func<CategoriesViewModel> _categoriesFactory;
    private readonly Func<PurchasesViewModel> _purchasesFactory;
    private readonly Func<SettingsViewModel> _settingsFactory;
    private readonly Func<PurchaseRateLogbookViewModel> _purchaseRateLogbookFactory;

    public MainViewModel(
        Func<DashboardViewModel> dashboardFactory,
        Func<RegisterViewModel> registerFactory,
        Func<InventoryViewModel> inventoryFactory,
        Func<CustomersViewModel> customersFactory,
        Func<OrdersViewModel> ordersFactory,
        Func<CategoriesViewModel> categoriesFactory,
        Func<PurchasesViewModel> purchasesFactory,
        Func<SettingsViewModel> settingsFactory,
        Func<PurchaseRateLogbookViewModel>? purchaseRateLogbookFactory = null)
    {
        _dashboardFactory = dashboardFactory;
        _registerFactory = registerFactory;
        _inventoryFactory = inventoryFactory;
        _customersFactory = customersFactory;
        _ordersFactory = ordersFactory;
        _categoriesFactory = categoriesFactory;
        _purchasesFactory = purchasesFactory;
        _settingsFactory = settingsFactory;
        _purchaseRateLogbookFactory = purchaseRateLogbookFactory ?? (() => null!);
    }

    [RelayCommand]
    public void NavigateTo(string viewName)
    {
        if (!IsViewAllowed(viewName))
        {
            viewName = GetDefaultLandingView();
        }

        CurrentViewName = viewName;
        CurrentView = viewName switch
        {
            "Dashboard" => _dashboardFactory(),
            "Register" => _registerFactory(),
            "Inventory" => _inventoryFactory(),
            "Customers" => _customersFactory(),
            "Orders" => _ordersFactory(),
            "Categories" => _categoriesFactory(),
            "Purchases" => _purchasesFactory(),
            "Settings" => _settingsFactory(),
            "PurchaseLogbook" => _purchaseRateLogbookFactory(),
            _ => _dashboardFactory()
        };

        // Trigger data load on navigation and assign logged-in user
        if (CurrentView is DashboardViewModel dashVm)
        {
            dashVm.LoadData();
        }
        else if (CurrentView is RegisterViewModel regVm)
        {
            regVm.CashierId = LoggedInUserId;
            regVm.LoadData();
        }
        else if (CurrentView is InventoryViewModel invVm)
        {
            invVm.CurrentUserId = LoggedInUserId;
            invVm.LoadData();
        }
        else if (CurrentView is CustomersViewModel custVm)
        {
            custVm.LoadData();
        }
        else if (CurrentView is OrdersViewModel ordVm)
        {
            ordVm.CurrentUserId = LoggedInUserId;
            ordVm.OnResumeOrder = ResumeHeldOrder;
            ordVm.LoadData();
        }
        else if (CurrentView is CategoriesViewModel catVm)
        {
            catVm.LoadData();
        }
        else if (CurrentView is PurchasesViewModel purVm)
        {
            purVm.LoadData();
        }
        else if (CurrentView is SettingsViewModel setVm)
        {
            setVm.CurrentUserId = LoggedInUserId;
            setVm.LoadData();
        }
        else if (CurrentView is PurchaseRateLogbookViewModel logVm)
        {
            logVm.CurrentUserId = LoggedInUserId;
            logVm.CurrentUserName = LoggedInUser;
            logVm.LoadData();
        }
    }

    public bool IsViewAllowed(string viewName) => viewName switch
    {
        "Dashboard" => CanViewDashboard,
        "Register" => CanViewRegister,
        "Inventory" => CanViewInventory,
        "Categories" => CanViewCategories,
        "Customers" => CanViewCustomers,
        "Purchases" => CanViewPurchases,
        "Orders" => CanViewOrders,
        "Settings" => CanViewSettings,
        "PurchaseLogbook" => CanViewPurchaseLogbook,
        _ => false
    };

    public string GetDefaultLandingView()
    {
        if (CanViewDashboard) return "Dashboard";
        if (CanViewRegister) return "Register";
        if (CanViewInventory) return "Inventory";
        return "Register";
    }

    public void ResumeHeldOrder(Order order)
    {
        var regVm = _registerFactory();
        regVm.CashierId = LoggedInUserId;
        regVm.LoadData();
        regVm.LoadHeldOrder(order);
        CurrentViewName = "Register";
        CurrentView = regVm;
    }

    public void SetLoggedInUser(string name, string role, int userId)
    {
        LoggedInUser = name;
        LoggedInRole = role;
        LoggedInUserId = userId;
        IsLoggedIn = true;

        UpdatePermissions(role);
    }

    private void UpdatePermissions(string role)
    {
        string roleNorm = role?.Trim().ToLowerInvariant() ?? "";
        if (roleNorm.Contains("cashier"))
        {
            // Cashier: Register, Customers, Orders
            CanViewRegister = true;
            CanViewCustomers = true;
            CanViewOrders = true;

            CanViewDashboard = false;
            CanViewInventory = false;
            CanViewCategories = false;
            CanViewPurchases = false;
            CanViewSettings = false;
            CanViewPurchaseLogbook = false;

            NavigateTo("Register");
        }
        else if (roleNorm.Contains("stock") || roleNorm.Contains("maintainer"))
        {
            // Stock Maintainer: Inventory, Categories, Purchases, Dashboard, PurchaseLogbook
            CanViewInventory = true;
            CanViewCategories = true;
            CanViewPurchases = true;
            CanViewDashboard = true;
            CanViewPurchaseLogbook = true;

            CanViewRegister = false;
            CanViewCustomers = false;
            CanViewOrders = false;
            CanViewSettings = false;

            NavigateTo("Inventory");
        }
        else // Admin or Manager
        {
            CanViewDashboard = true;
            CanViewRegister = true;
            CanViewInventory = true;
            CanViewCategories = true;
            CanViewCustomers = true;
            CanViewPurchases = true;
            CanViewOrders = true;
            CanViewSettings = true;
            CanViewPurchaseLogbook = true;

            NavigateTo("Dashboard");
        }
    }

    [RelayCommand]
    public void Logout()
    {
        IsLoggedIn = false;
        LoggedInUser = "";
        LoggedInRole = "";
        LoggedInUserId = 0;
        CurrentView = null;

        CanViewDashboard = true;
        CanViewRegister = true;
        CanViewInventory = true;
        CanViewCategories = true;
        CanViewCustomers = true;
        CanViewPurchases = true;
        CanViewOrders = true;
        CanViewSettings = true;
        CanViewPurchaseLogbook = true;
    }
}
