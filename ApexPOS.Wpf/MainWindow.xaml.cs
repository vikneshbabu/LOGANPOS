using System.Windows;
using ApexPOS.Wpf.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace ApexPOS.Wpf;

public partial class MainWindow : Window
{
    public MainViewModel MainVm { get; }
    public LoginViewModel LoginVm { get; }

    public MainWindow(MainViewModel mainVm, LoginViewModel loginVm)
    {
        InitializeComponent();
        MainVm = mainVm;
        LoginVm = loginVm;

        LoginVm.OnLoginSuccess = user =>
        {
            MainVm.SetLoggedInUser(user.FullName, user.Role.ToString(), user.Id);
        };

        DataContext = this;
    }

    private void OnPasswordLogin_Click(object sender, RoutedEventArgs e)
    {
        LoginVm.LoginWithPassword(PasswordInput.Password);
    }
}