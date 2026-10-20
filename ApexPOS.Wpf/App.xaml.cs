using System.Globalization;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Markup;
using ApexPOS.Data;
using ApexPOS.Services;
using ApexPOS.Wpf.ViewModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ApexPOS.Wpf;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;
    public static readonly string LogFile = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ApexPOS", "startup.log");

    public static void Log(string msg)
    {
        try
        {
            var dir = Path.GetDirectoryName(LogFile);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            File.AppendAllText(LogFile, $"[{DateTime.Now:HH:mm:ss.fff}] {msg}{Environment.NewLine}");
        }
        catch { }
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        Log("App.OnStartup called");
        AppDomain.CurrentDomain.UnhandledException += (s, args) =>
        {
            Log($"Unhandled AppDomain exception: {args.ExceptionObject}");
        };
        DispatcherUnhandledException += (s, args) =>
        {
            Log($"Dispatcher exception: {args.Exception}");
            MessageBox.Show($"ApexPOS Error: {args.Exception.Message}\n\n{args.Exception}", "ApexPOS Startup Error", MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };

        try
        {
            var culture = new CultureInfo("en-IN");
            CultureInfo.DefaultThreadCurrentCulture = culture;
            CultureInfo.DefaultThreadCurrentUICulture = culture;
            Thread.CurrentThread.CurrentCulture = culture;
            Thread.CurrentThread.CurrentUICulture = culture;
            FrameworkElement.LanguageProperty.OverrideMetadata(
                typeof(FrameworkElement),
                new FrameworkPropertyMetadata(XmlLanguage.GetLanguage(culture.IetfLanguageTag)));

            base.OnStartup(e);
            Log("Base OnStartup completed");

            var services = new ServiceCollection();

            // Database
            var dbPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "ApexPOS", "apexpos.db");
            var dir = Path.GetDirectoryName(dbPath)!;
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            Log($"Database path: {dbPath}");

            services.AddDbContext<ApexDbContext>(options =>
                options.UseSqlite($"Data Source={dbPath}"), ServiceLifetime.Transient);

            // Services (Registered with both Interface and Concrete Type for DI and Testing)
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

            // Windows
            services.AddTransient<MainWindow>();

            Services = services.BuildServiceProvider();
            Log("DI Container built successfully");

            // Seed database
            Log("Seeding database...");
            using (var scope = Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApexDbContext>();
                DbSeeder.Seed(db);

                // Ensure all tax rates and tax totals in existing database are zeroed out
                try
                {
                    db.Database.ExecuteSqlRaw("UPDATE Products SET TaxRate = 0 WHERE TaxRate != 0;");
                    db.Database.ExecuteSqlRaw("UPDATE OrderItems SET TaxRate = 0 WHERE TaxRate != 0;");
                    db.Database.ExecuteSqlRaw("UPDATE Settings SET Value = '0.00' WHERE Key = 'TaxRate';");
                }
                catch { }
            }
            Log("Database seeded successfully");

            // Start daily email report background scheduler
            StartDailyEmailTimer();

            // Show main window
            Log("Resolving MainWindow...");
            var mainWindow = Services.GetRequiredService<MainWindow>();
            Log("Showing MainWindow...");
            mainWindow.Show();
            Log("MainWindow shown successfully");
        }
        catch (Exception ex)
        {
            Log($"FATAL OnStartup EXCEPTION: {ex}");
            MessageBox.Show($"Failed to launch ApexPOS:\n{ex.Message}\n\n{ex}", "ApexPOS Startup Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private static System.Threading.Timer? _dailyEmailTimer;

    private void StartDailyEmailTimer()
    {
        _dailyEmailTimer = new System.Threading.Timer(async _ =>
        {
            try
            {
                if (Services == null) return;
                using var scope = Services.CreateScope();
                var emailService = scope.ServiceProvider.GetRequiredService<EmailService>();
                var settings = emailService.GetSettings();

                if (!settings.EnableDailyReport) return;
                if (string.IsNullOrWhiteSpace(settings.RecipientEmail)) return;

                var todayStr = DateTime.Today.ToString("yyyy-MM-dd");
                if (settings.LastReportSentDate == todayStr) return;

                if (TimeSpan.TryParse(settings.DailyReportTime, out var targetTime))
                {
                    var now = DateTime.Now.TimeOfDay;
                    if (now >= targetTime)
                    {
                        await emailService.SendDailyReportAsync();
                    }
                }
            }
            catch
            {
                // Silently ignore background automated report errors
            }
        }, null, TimeSpan.FromSeconds(30), TimeSpan.FromMinutes(1));
    }
}
