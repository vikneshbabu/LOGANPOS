using ApexPOS.Data;
using ApexPOS.Services;
using ApexPOS.Wpf.ViewModels;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ApexPOS.Tests;

/// <summary>
/// Enterprise dependency injection validation tests.
/// Verifies that all service contracts, concrete classes, and ViewModels
/// resolve successfully from the IoC container without runtime activation failures.
/// </summary>
public class DependencyInjectionContainerTests
{
    [Theory]
    [InlineData(typeof(IAuthService))]
    [InlineData(typeof(AuthService))]
    [InlineData(typeof(IProductService))]
    [InlineData(typeof(ProductService))]
    [InlineData(typeof(ICategoryService))]
    [InlineData(typeof(CategoryService))]
    [InlineData(typeof(ICustomerService))]
    [InlineData(typeof(CustomerService))]
    [InlineData(typeof(IOrderService))]
    [InlineData(typeof(OrderService))]
    [InlineData(typeof(IDashboardService))]
    [InlineData(typeof(DashboardService))]
    [InlineData(typeof(IReceiptService))]
    [InlineData(typeof(ReceiptService))]
    [InlineData(typeof(ISettingsService))]
    [InlineData(typeof(SettingsService))]
    [InlineData(typeof(IEmailService))]
    [InlineData(typeof(EmailService))]
    [InlineData(typeof(PurchaseService))]
    [InlineData(typeof(PurchaseRateLogService))]
    [InlineData(typeof(LoginViewModel))]
    [InlineData(typeof(DashboardViewModel))]
    [InlineData(typeof(RegisterViewModel))]
    [InlineData(typeof(InventoryViewModel))]
    [InlineData(typeof(CustomersViewModel))]
    [InlineData(typeof(OrdersViewModel))]
    [InlineData(typeof(CategoriesViewModel))]
    [InlineData(typeof(PurchasesViewModel))]
    [InlineData(typeof(SettingsViewModel))]
    [InlineData(typeof(PurchaseRateLogbookViewModel))]
    [InlineData(typeof(MainViewModel))]
    public void ServiceContainer_ResolvesRegisteredType_Successfully(Type serviceType)
    {
        // Arrange
        var services = new ServiceCollection();

        services.AddDbContext<ApexDbContext>(options =>
            options.UseSqlite("DataSource=:memory:"), ServiceLifetime.Transient);

        // Services
        services.AddTransient<IAuthService, AuthService>();
        services.AddTransient<AuthService>();

        services.AddTransient<IProductService, ProductService>();
        services.AddTransient<ProductService>();

        services.AddTransient<ICategoryService, CategoryService>();
        services.AddTransient<CategoryService>();

        services.AddTransient<ICustomerService, CustomerService>();
        services.AddTransient<CustomerService>();

        services.AddTransient<IOrderService, OrderService>();
        services.AddTransient<OrderService>();

        services.AddTransient<IDashboardService, DashboardService>();
        services.AddTransient<DashboardService>();

        services.AddTransient<IReceiptService, ReceiptService>();
        services.AddTransient<ReceiptService>();

        services.AddTransient<ISettingsService, SettingsService>();
        services.AddTransient<SettingsService>();

        services.AddTransient<IEmailService, EmailService>();
        services.AddTransient<EmailService>();

        services.AddTransient<PurchaseService>();
        services.AddTransient<PurchaseRateLogService>();

        // ViewModels
        services.AddTransient<LoginViewModel>();
        services.AddTransient<DashboardViewModel>();
        services.AddTransient<RegisterViewModel>();
        services.AddTransient<InventoryViewModel>();
        services.AddTransient<CustomersViewModel>();
        services.AddTransient<OrdersViewModel>();
        services.AddTransient<CategoriesViewModel>();
        services.AddTransient<PurchasesViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<PurchaseRateLogbookViewModel>();
        services.AddSingleton<MainViewModel>(sp => new MainViewModel(
            () => sp.GetRequiredService<DashboardViewModel>(),
            () => sp.GetRequiredService<RegisterViewModel>(),
            () => sp.GetRequiredService<InventoryViewModel>(),
            () => sp.GetRequiredService<CustomersViewModel>(),
            () => sp.GetRequiredService<OrdersViewModel>(),
            () => sp.GetRequiredService<CategoriesViewModel>(),
            () => sp.GetRequiredService<PurchasesViewModel>(),
            () => sp.GetRequiredService<SettingsViewModel>(),
            () => sp.GetRequiredService<PurchaseRateLogbookViewModel>()
        ));

        var provider = services.BuildServiceProvider();

        // Act
        var resolvedInstance = provider.GetService(serviceType);

        // Assert
        resolvedInstance.Should().NotBeNull($"Service container must resolve {serviceType.Name}");
    }
}
