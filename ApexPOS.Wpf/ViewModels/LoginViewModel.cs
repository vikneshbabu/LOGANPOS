using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ApexPOS.Services;
using ApexPOS.Domain.Entities;

namespace ApexPOS.Wpf.ViewModels;

public partial class LoginViewModel : ObservableObject
{
    private readonly AuthService _authService;

    [ObservableProperty]
    private string _username = "";

    [ObservableProperty]
    private string _pinCode = "";

    [ObservableProperty]
    private string _errorMessage = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ToggleModeText))]
    private bool _showPinLogin = false;

    public string ToggleModeText => ShowPinLogin ? "Switch to Username / Password" : "Switch to Quick PIN";

    public Action<User>? OnLoginSuccess { get; set; }

    public LoginViewModel(AuthService authService)
    {
        _authService = authService;
    }

    [RelayCommand]
    public void LoginWithPassword(object? passwordBox)
    {
        ErrorMessage = "";
        // Get password from PasswordBox via code-behind bridge
        string password = passwordBox?.ToString() ?? "";
        if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(password))
        {
            ErrorMessage = "Please enter both username and password.";
            return;
        }

        var user = _authService.Login(Username, password);
        if (user != null)
        {
            OnLoginSuccess?.Invoke(user);
        }
        else
        {
            ErrorMessage = "Invalid username or password.";
        }
    }

    [RelayCommand]
    public void LoginWithPin()
    {
        ErrorMessage = "";
        if (string.IsNullOrWhiteSpace(PinCode))
        {
            ErrorMessage = "Please enter your PIN.";
            return;
        }

        var user = _authService.LoginByPin(PinCode);
        if (user != null)
        {
            OnLoginSuccess?.Invoke(user);
        }
        else
        {
            ErrorMessage = "Invalid PIN code.";
        }
        PinCode = "";
    }

    [RelayCommand]
    public void ToggleLoginMode()
    {
        ShowPinLogin = !ShowPinLogin;
        ErrorMessage = "";
    }

    [RelayCommand]
    public void AppendPin(string digit)
    {
        if (PinCode.Length < 6)
            PinCode += digit;
    }

    [RelayCommand]
    public void ClearPin()
    {
        PinCode = "";
    }

    [RelayCommand]
    public void BackspacePin()
    {
        if (PinCode.Length > 0)
            PinCode = PinCode[..^1];
    }
}
