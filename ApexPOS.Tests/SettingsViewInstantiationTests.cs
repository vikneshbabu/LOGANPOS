using System;
using System.Threading;
using System.Windows;
using ApexPOS.Data;
using ApexPOS.Domain.Entities;
using ApexPOS.Domain.Enums;
using ApexPOS.Services;
using ApexPOS.Wpf.ViewModels;
using ApexPOS.Wpf.Views;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ApexPOS.Tests;

public class SettingsViewInstantiationTests
{
    [Fact]
    public void Views_MeasureAndLayoutWithoutException()
    {
        Exception? thrownException = null;

        var thread = new Thread(() =>
        {
            try
            {
                if (Application.Current == null)
                {
                    _ = new Application();
                }

                if (Application.Current!.Resources.MergedDictionaries.Count == 0)
                {
                    var dict = new ResourceDictionary
                    {
                        Source = new Uri("pack://application:,,,/ApexPOS;component/Themes/DarkTheme.xaml", UriKind.Absolute)
                    };
                    Application.Current!.Resources.MergedDictionaries.Add(dict);
                }

                var options = new DbContextOptionsBuilder<ApexDbContext>()
                    .UseSqlite("DataSource=:memory:")
                    .Options;
                var db = new ApexDbContext(options);
                db.Database.OpenConnection();
                db.Database.EnsureCreated();
                DbSeeder.Seed(db);

                var settingsService = new SettingsService(db);
                var authService = new AuthService(db);
                var emailService = new EmailService(db, settingsService);
                var vm = new SettingsViewModel(settingsService, authService, emailService);
                vm.LoadData();

                var view = new SettingsView
                {
                    DataContext = vm
                };

                var window = new Window
                {
                    Width = 1200,
                    Height = 800,
                    Content = view
                };

                // Measure and Arrange initial tab
                window.Measure(new Size(1200, 800));
                window.Arrange(new Rect(0, 0, 1200, 800));
                window.UpdateLayout();

                // Test every tab to force WPF to instantiate and measure every sub-panel's templates
                string[] tabs = new[]
                {
                    "Role Permissions",
                    "User Accounts",
                    "Register Setup",
                    "Printer Config",
                    "Card Terminal",
                    "Inventory Control",
                    "Transaction Rules",
                    "Loyalty Program",
                    "Store Profile",
                    "Hardware Setup",
                    "Receipt Setup",
                    "Email Server",
                    "Settings & Config Group 1",
                    "Settings & Config Group 2"
                };

                foreach (var tab in tabs)
                {
                    vm.ActiveNavItem = tab;
                    window.Measure(new Size(1200, 800));
                    window.Arrange(new Rect(0, 0, 1200, 800));
                    window.UpdateLayout();
                }

                // Also test RegisterView on the same STA thread
                var productService = new ProductService(db);
                var categoryService = new CategoryService(db);
                var orderService = new OrderService(db);
                var customerService = new CustomerService(db);
                var receiptService = new ReceiptService(settingsService);

                var regVm = new RegisterViewModel(
                    productService,
                    categoryService,
                    orderService,
                    customerService,
                    receiptService,
                    settingsService,
                    authService);

                var regView = new RegisterView
                {
                    DataContext = regVm
                };

                window.Content = regView;
                window.Measure(new Size(1200, 800));
                window.Arrange(new Rect(0, 0, 1200, 800));
                window.UpdateLayout();

                // Test sales person dropdown
                regVm.IsSalesPersonDropdownOpen = true;
                window.Measure(new Size(1200, 800));
                window.Arrange(new Rect(0, 0, 1200, 800));
                window.UpdateLayout();

                // Test selected sales person
                var salesPerson = new User
                {
                    Id = 5,
                    Username = "sales_test",
                    FullName = "Sales Test User",
                    Role = UserRole.SalesPerson,
                    LoyaltyPoints = 120m
                };
                regVm.SelectSalesPerson(salesPerson);
                window.Measure(new Size(1200, 800));
                window.Arrange(new Rect(0, 0, 1200, 800));
                window.UpdateLayout();

                // Test payment panel
                regVm.ShowPaymentPanel = true;
                window.Measure(new Size(1200, 800));
                window.Arrange(new Rect(0, 0, 1200, 800));
                window.UpdateLayout();

                // Also test OrdersView (including Invoice preview with SalesPersonName and NullVis)
                var ordersVm = new OrdersViewModel(orderService, receiptService);
                var ordersView = new OrdersView
                {
                    DataContext = ordersVm
                };
                window.Content = ordersView;
                window.Measure(new Size(1200, 800));
                window.Arrange(new Rect(0, 0, 1200, 800));
                window.UpdateLayout();
            }
            catch (Exception ex)
            {
                thrownException = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        thrownException.Should().BeNull(because: thrownException?.ToString() ?? "no exception");
    }
}
