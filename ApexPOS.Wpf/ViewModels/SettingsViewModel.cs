using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ApexPOS.Domain.Entities;
using ApexPOS.Domain.Enums;
using ApexPOS.Services;

namespace ApexPOS.Wpf.ViewModels;

public class RolePermissionsState
{
    // Transactions Group
    public bool ProcessSales { get; set; } = true;
    public bool IssueRefunds { get; set; } = true;
    public bool VoidTransaction { get; set; } = true;
    public bool ManagerOverride { get; set; } = true;

    // User Management Group
    public bool AddEditUsers { get; set; } = true;
    public bool ResetPasswords { get; set; } = true;
    public bool ChangeRoles { get; set; } = true;

    // Inventory Group
    public bool AddEditProducts { get; set; } = true;
    public bool ViewStock { get; set; } = true;
    public bool AdjustStockLevels { get; set; } = true;
    public bool PrintBarcodes { get; set; } = true;

    // Reporting Group
    public bool ViewSalesReports { get; set; } = true;
    public bool ViewInventoryReports { get; set; } = true;
    public bool DailySummaryZReport { get; set; } = true;

    // Settings & Config Group 1
    public bool EditPosRules1 { get; set; } = true;
    public bool HardwareSetup1 { get; set; } = true;
    public bool ReceiptCustomization1 { get; set; } = true;

    // Settings & Config Group 2
    public bool EditPosRules2 { get; set; } = true;
    public bool HardwareSetup2 { get; set; } = true;
    public bool ReceiptCustomization2 { get; set; } = true;
}

public partial class SettingsViewModel : ObservableObject
{
    private readonly SettingsService _settingsService;
    private readonly AuthService? _authService;
    private readonly EmailService? _emailService;
    private readonly Dictionary<string, RolePermissionsState> _rolePermissionsMap = new();

    // User Accounts Management
    public int CurrentUserId { get; set; } = 1;
    public ObservableCollection<User> UsersList { get; } = new();

    public ObservableCollection<string> AvailableRoles { get; } = new()
    {
        "Cashier",
        "Sales Person",
        "Stock Maintainer",
        "Admin",
        "Manager"
    };

    [ObservableProperty] private bool _showUserModal;
    [ObservableProperty] private string _newUsername = "";
    [ObservableProperty] private string _newFullName = "";
    [ObservableProperty] private string _newUserRole = "Cashier";
    [ObservableProperty] private string _newPassword = "";
    [ObservableProperty] private string _newPinCode = "";
    [ObservableProperty] private string _userStatusMessage = "";
    [ObservableProperty] private bool _isUserSuccess;

    // Loyalty Program Settings
    [ObservableProperty] private decimal _loyaltyAmountPerPoint = 250.00m;
    [ObservableProperty] private decimal _loyaltyPointRedeemValue = 1.00m;
    [ObservableProperty] private bool _enableLoyaltyProgram = true;
    [ObservableProperty] private string _loyaltyStatusMessage = "";

    // Daily Email Report Settings
    [ObservableProperty] private string _smtpHost = "smtp.gmail.com";
    [ObservableProperty] private int _smtpPort = 587;
    [ObservableProperty] private bool _enableSsl = true;
    [ObservableProperty] private string _senderEmail = "";
    [ObservableProperty] private string _senderPassword = "";
    [ObservableProperty] private string _recipientEmail = "";
    [ObservableProperty] private bool _enableDailyReport = false;
    [ObservableProperty] private string _dailyReportTime = "21:00";
    [ObservableProperty] private string _emailStatusMessage = "";
    [ObservableProperty] private bool _isSendingEmail;

    // Report Summary Preview
    [ObservableProperty] private decimal _previewTodaySales;
    [ObservableProperty] private decimal _previewTodayProfit;
    [ObservableProperty] private int _previewTodayOrders;
    [ObservableProperty] private int _previewStockUnits;
    [ObservableProperty] private int _previewLowStockCount;

    // Navigation state
    [ObservableProperty] private string _activeNavItem = "Role Permissions";

    // Role Selection
    public ObservableCollection<string> Roles { get; } = new()
    {
        "Store Manager",
        "Cashier",
        "Supervisor",
        "Admin"
    };

    [ObservableProperty] private string _selectedRole = "Store Manager";

    // Transactions Group Toggles
    [ObservableProperty] private bool _processSales = true;
    [ObservableProperty] private bool _issueRefunds = true;
    [ObservableProperty] private bool _voidTransaction = true;
    [ObservableProperty] private bool _managerOverride = true;

    // User Management Group Toggles
    [ObservableProperty] private bool _addEditUsers = true;
    [ObservableProperty] private bool _resetPasswords = true;
    [ObservableProperty] private bool _changeRoles = true;

    // Inventory Group Toggles
    [ObservableProperty] private bool _addEditProducts = true;
    [ObservableProperty] private bool _viewStock = true;
    [ObservableProperty] private bool _adjustStockLevels = true;
    [ObservableProperty] private bool _printBarcodes = true;

    // Reporting Group Toggles
    [ObservableProperty] private bool _viewSalesReports = true;
    [ObservableProperty] private bool _viewInventoryReports = true;
    [ObservableProperty] private bool _dailySummaryZReport = true;

    // Settings & Config Group 1 Toggles
    [ObservableProperty] private bool _editPosRules1 = true;
    [ObservableProperty] private bool _hardwareSetup1 = true;
    [ObservableProperty] private bool _receiptCustomization1 = true;

    // Settings & Config Group 2 Toggles
    [ObservableProperty] private bool _editPosRules2 = true;
    [ObservableProperty] private bool _hardwareSetup2 = true;
    [ObservableProperty] private bool _receiptCustomization2 = true;

    // Toast Notification
    [ObservableProperty] private bool _showToast;
    [ObservableProperty] private string _toastMessage = "";

    // Legacy / General Store Profile Settings
    [ObservableProperty] private string _storeName = "Loganx Store";
    [ObservableProperty] private string _storeAddress = "Bazaar Street, Sirkali";
    [ObservableProperty] private string _storePhone = "9487848939";
    [ObservableProperty] private decimal _taxRate = 0.0m;
    [ObservableProperty] private string _currencySymbol = "₹";
    [ObservableProperty] private string _receiptFooter = "Thank you for shopping at Loganx POS! Please visit again.";
    [ObservableProperty] private string _statusMessage = "";

    public SettingsViewModel(SettingsService settingsService, AuthService? authService = null, EmailService? emailService = null)
    {
        _settingsService = settingsService;
        _authService = authService;
        _emailService = emailService;
        InitializeRolePresets();
        LoadRolePermissions(SelectedRole);
    }

    private void InitializeRolePresets()
    {
        // Store Manager: All ON by default as requested
        _rolePermissionsMap["Store Manager"] = new RolePermissionsState
        {
            ProcessSales = true,
            IssueRefunds = true,
            VoidTransaction = true,
            ManagerOverride = true,
            AddEditUsers = true,
            ResetPasswords = true,
            ChangeRoles = true,
            AddEditProducts = true,
            ViewStock = true,
            AdjustStockLevels = true,
            PrintBarcodes = true,
            ViewSalesReports = true,
            ViewInventoryReports = true,
            DailySummaryZReport = true,
            EditPosRules1 = true,
            HardwareSetup1 = true,
            ReceiptCustomization1 = true,
            EditPosRules2 = true,
            HardwareSetup2 = true,
            ReceiptCustomization2 = true
        };

        // Cashier: Standard retail checkout permissions
        _rolePermissionsMap["Cashier"] = new RolePermissionsState
        {
            ProcessSales = true,
            IssueRefunds = false,
            VoidTransaction = false,
            ManagerOverride = false,
            AddEditUsers = false,
            ResetPasswords = false,
            ChangeRoles = false,
            AddEditProducts = false,
            ViewStock = true,
            AdjustStockLevels = false,
            PrintBarcodes = true,
            ViewSalesReports = false,
            ViewInventoryReports = false,
            DailySummaryZReport = true,
            EditPosRules1 = false,
            HardwareSetup1 = false,
            ReceiptCustomization1 = false,
            EditPosRules2 = false,
            HardwareSetup2 = false,
            ReceiptCustomization2 = false
        };

        // Supervisor: Higher operational capabilities
        _rolePermissionsMap["Supervisor"] = new RolePermissionsState
        {
            ProcessSales = true,
            IssueRefunds = true,
            VoidTransaction = true,
            ManagerOverride = true,
            AddEditUsers = false,
            ResetPasswords = true,
            ChangeRoles = false,
            AddEditProducts = true,
            ViewStock = true,
            AdjustStockLevels = true,
            PrintBarcodes = true,
            ViewSalesReports = true,
            ViewInventoryReports = true,
            DailySummaryZReport = true,
            EditPosRules1 = true,
            HardwareSetup1 = false,
            ReceiptCustomization1 = false,
            EditPosRules2 = true,
            HardwareSetup2 = false,
            ReceiptCustomization2 = false
        };

        // Admin: Full unrestricted access
        _rolePermissionsMap["Admin"] = new RolePermissionsState
        {
            ProcessSales = true,
            IssueRefunds = true,
            VoidTransaction = true,
            ManagerOverride = true,
            AddEditUsers = true,
            ResetPasswords = true,
            ChangeRoles = true,
            AddEditProducts = true,
            ViewStock = true,
            AdjustStockLevels = true,
            PrintBarcodes = true,
            ViewSalesReports = true,
            ViewInventoryReports = true,
            DailySummaryZReport = true,
            EditPosRules1 = true,
            HardwareSetup1 = true,
            ReceiptCustomization1 = true,
            EditPosRules2 = true,
            HardwareSetup2 = true,
            ReceiptCustomization2 = true
        };
    }

    partial void OnSelectedRoleChanged(string value)
    {
        if (string.IsNullOrEmpty(value)) return;
        LoadRolePermissions(value);
    }

    private void LoadRolePermissions(string role)
    {
        if (!_rolePermissionsMap.TryGetValue(role, out var state))
        {
            state = new RolePermissionsState();
            _rolePermissionsMap[role] = state;
        }

        ProcessSales = state.ProcessSales;
        IssueRefunds = state.IssueRefunds;
        VoidTransaction = state.VoidTransaction;
        ManagerOverride = state.ManagerOverride;

        AddEditUsers = state.AddEditUsers;
        ResetPasswords = state.ResetPasswords;
        ChangeRoles = state.ChangeRoles;

        AddEditProducts = state.AddEditProducts;
        ViewStock = state.ViewStock;
        AdjustStockLevels = state.AdjustStockLevels;
        PrintBarcodes = state.PrintBarcodes;

        ViewSalesReports = state.ViewSalesReports;
        ViewInventoryReports = state.ViewInventoryReports;
        DailySummaryZReport = state.DailySummaryZReport;

        EditPosRules1 = state.EditPosRules1;
        HardwareSetup1 = state.HardwareSetup1;
        ReceiptCustomization1 = state.ReceiptCustomization1;

        EditPosRules2 = state.EditPosRules2;
        HardwareSetup2 = state.HardwareSetup2;
        ReceiptCustomization2 = state.ReceiptCustomization2;
    }

    [RelayCommand]
    public void SelectNavItem(string navItem)
    {
        ActiveNavItem = navItem;
    }

    [RelayCommand]
    public void SavePermissions()
    {
        var state = new RolePermissionsState
        {
            ProcessSales = ProcessSales,
            IssueRefunds = IssueRefunds,
            VoidTransaction = VoidTransaction,
            ManagerOverride = ManagerOverride,

            AddEditUsers = AddEditUsers,
            ResetPasswords = ResetPasswords,
            ChangeRoles = ChangeRoles,

            AddEditProducts = AddEditProducts,
            ViewStock = ViewStock,
            AdjustStockLevels = AdjustStockLevels,
            PrintBarcodes = PrintBarcodes,

            ViewSalesReports = ViewSalesReports,
            ViewInventoryReports = ViewInventoryReports,
            DailySummaryZReport = DailySummaryZReport,

            EditPosRules1 = EditPosRules1,
            HardwareSetup1 = HardwareSetup1,
            ReceiptCustomization1 = ReceiptCustomization1,

            EditPosRules2 = EditPosRules2,
            HardwareSetup2 = HardwareSetup2,
            ReceiptCustomization2 = ReceiptCustomization2
        };

        _rolePermissionsMap[SelectedRole] = state;

        // Display exact success toast alert requested:
        ToastMessage = "✔ Changes saved. API endpoints updated.";
        ShowToast = true;

        // Auto-dismiss in background after 4 seconds
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(4000);
                ShowToast = false;
            }
            catch
            {
                // Ignore
            }
        });
    }

    [RelayCommand]
    public void CancelPermissions()
    {
        // Revert to saved role permissions
        LoadRolePermissions(SelectedRole);
        ShowToast = false;
        ToastMessage = "";
    }

    [RelayCommand]
    public void DismissToast()
    {
        ShowToast = false;
    }

    public void LoadUsers()
    {
        if (_authService == null) return;
        UsersList.Clear();
        var users = _authService.GetAllUsers();
        foreach (var u in users)
        {
            UsersList.Add(u);
        }
    }

    [RelayCommand]
    public void OpenCreateUserModal()
    {
        NewUsername = "";
        NewFullName = "";
        NewUserRole = "Cashier";
        NewPassword = "";
        NewPinCode = "";
        UserStatusMessage = "";
        ShowUserModal = true;
    }

    [RelayCommand]
    public void CloseCreateUserModal()
    {
        ShowUserModal = false;
        UserStatusMessage = "";
    }

    [RelayCommand]
    public void CreateUser()
    {
        if (_authService == null) return;

        if (string.IsNullOrWhiteSpace(NewUsername))
        {
            UserStatusMessage = "Username is required.";
            IsUserSuccess = false;
            return;
        }

        if (string.IsNullOrWhiteSpace(NewPassword) && string.IsNullOrWhiteSpace(NewPinCode))
        {
            UserStatusMessage = "Password or PIN Code is required.";
            IsUserSuccess = false;
            return;
        }

        var roleEnum = NewUserRole switch
        {
            "Sales Person" => UserRole.SalesPerson,
            "Cashier" => UserRole.Cashier,
            "Stock Maintainer" => UserRole.StockMaintainer,
            "Manager" => UserRole.Manager,
            _ => UserRole.Admin
        };

        try
        {
            var user = _authService.CreateUser(
                NewUsername.Trim(),
                string.IsNullOrWhiteSpace(NewFullName) ? NewUsername.Trim() : NewFullName.Trim(),
                roleEnum,
                NewPassword.Trim(),
                NewPinCode.Trim(),
                CurrentUserId);

            UserStatusMessage = $"User '{user.Username}' created successfully!";
            IsUserSuccess = true;
            LoadUsers();
            ShowUserModal = false;
            ToastMessage = $"✔ User '{NewUsername.Trim()}' created successfully!";
            ShowToast = true;
        }
        catch (Exception ex)
        {
            UserStatusMessage = ex.Message;
            IsUserSuccess = false;
        }
    }

    [RelayCommand]
    public void DeleteUser(User? user)
    {
        if (user == null || _authService == null) return;
        if (user.Id == CurrentUserId)
        {
            ToastMessage = "⚠ You cannot delete your own logged-in account.";
            ShowToast = true;
            return;
        }

        try
        {
            _authService.DeleteUser(user.Id, CurrentUserId);
            ToastMessage = $"✔ User '{user.Username}' deactivated.";
            ShowToast = true;
            LoadUsers();
        }
        catch (Exception ex)
        {
            ToastMessage = $"❌ {ex.Message}";
            ShowToast = true;
        }
    }

    [RelayCommand]
    public void SaveLoyaltySettings()
    {
        if (LoyaltyAmountPerPoint <= 0)
        {
            LoyaltyStatusMessage = "❌ Spending amount per point must be greater than 0.";
            return;
        }

        var loyalty = new LoyaltySettings
        {
            AmountPerPoint = LoyaltyAmountPerPoint,
            PointRedeemValue = LoyaltyPointRedeemValue > 0 ? LoyaltyPointRedeemValue : 1.00m,
            EnableLoyalty = EnableLoyaltyProgram
        };

        _settingsService.SaveLoyaltySettings(loyalty);
        LoyaltyStatusMessage = "✅ Loyalty configuration saved successfully!";
        ToastMessage = "✔ Loyalty program settings updated.";
        ShowToast = true;
    }

    public void LoadData()
    {
        try
        {
            var profile = _settingsService.GetProfile();
            StoreName = profile.StoreName;
            StoreAddress = profile.StoreAddress;
            StorePhone = profile.StorePhone;
            TaxRate = 0.0m;
            CurrencySymbol = profile.CurrencySymbol;
            ReceiptFooter = profile.ReceiptFooter;
        }
        catch (Exception ex)
        {
            App.Log($"SettingsViewModel LoadProfile error: {ex.Message}");
        }

        StatusMessage = "";

        try
        {
            LoadRolePermissions(SelectedRole);
        }
        catch (Exception ex)
        {
            App.Log($"SettingsViewModel LoadRolePermissions error: {ex.Message}");
        }

        try
        {
            LoadUsers();
        }
        catch (Exception ex)
        {
            App.Log($"SettingsViewModel LoadUsers error: {ex.Message}");
        }

        try
        {
            var loyalty = _settingsService.GetLoyaltySettings();
            LoyaltyAmountPerPoint = loyalty.AmountPerPoint;
            LoyaltyPointRedeemValue = loyalty.PointRedeemValue;
            EnableLoyaltyProgram = loyalty.EnableLoyalty;
        }
        catch (Exception ex)
        {
            App.Log($"SettingsViewModel LoadLoyalty error: {ex.Message}");
        }

        LoyaltyStatusMessage = "";

        if (_emailService != null)
        {
            try
            {
                var emailSettings = _emailService.GetSettings();
                SmtpHost = emailSettings.SmtpHost;
                SmtpPort = emailSettings.SmtpPort;
                EnableSsl = emailSettings.EnableSsl;
                SenderEmail = emailSettings.SenderEmail;
                SenderPassword = emailSettings.SenderPassword;
                RecipientEmail = emailSettings.RecipientEmail;
                EnableDailyReport = emailSettings.EnableDailyReport;
                DailyReportTime = emailSettings.DailyReportTime;
                EmailStatusMessage = "";

                try
                {
                    var summary = _emailService.BuildDailySummary();
                    PreviewTodaySales = summary.TotalSales;
                    PreviewTodayProfit = summary.TotalProfit;
                    PreviewTodayOrders = summary.TotalOrders;
                    PreviewStockUnits = summary.TotalStockUnits;
                    PreviewLowStockCount = summary.LowStockCount;
                }
                catch
                {
                    // Ignore calculation errors during preview
                }
            }
            catch (Exception ex)
            {
                App.Log($"SettingsViewModel LoadEmail error: {ex.Message}");
            }
        }
    }

    [RelayCommand]
    public void SaveSettings()
    {
        var profile = new StoreProfile
        {
            StoreName = StoreName.Trim(),
            StoreAddress = StoreAddress.Trim(),
            StorePhone = StorePhone.Trim(),
            TaxRate = 0.0m,
            CurrencySymbol = string.IsNullOrWhiteSpace(CurrencySymbol) ? "₹" : CurrencySymbol.Trim(),
            ReceiptFooter = ReceiptFooter.Trim()
        };

        _settingsService.SaveProfile(profile);
        StatusMessage = "✅ Settings saved successfully! Changes will reflect on receipts and screens.";
    }

    [RelayCommand]
    public void SaveEmailSettings()
    {
        if (_emailService == null) return;
        var s = new EmailReportSettings
        {
            SmtpHost = SmtpHost.Trim(),
            SmtpPort = SmtpPort,
            EnableSsl = EnableSsl,
            SenderEmail = SenderEmail.Trim(),
            SenderPassword = SenderPassword.Trim(),
            RecipientEmail = RecipientEmail.Trim(),
            EnableDailyReport = EnableDailyReport,
            DailyReportTime = string.IsNullOrWhiteSpace(DailyReportTime) ? "21:00" : DailyReportTime.Trim()
        };
        _emailService.SaveSettings(s);
        EmailStatusMessage = "✅ Daily Email Configuration saved successfully!";
    }

    [RelayCommand]
    public async Task SendTestReport()
    {
        if (_emailService == null) return;
        IsSendingEmail = true;
        EmailStatusMessage = "⏳ Sending daily report email...";
        try
        {
            SaveEmailSettings();
            var (success, msg) = await _emailService.SendDailyReportAsync(RecipientEmail);
            EmailStatusMessage = msg;
        }
        catch (Exception ex)
        {
            EmailStatusMessage = $"❌ Error sending report: {ex.Message}";
        }
        finally
        {
            IsSendingEmail = false;
        }
    }
}
