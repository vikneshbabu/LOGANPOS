using ApexPOS.Domain.Entities;
using ApexPOS.Domain.Enums;

namespace ApexPOS.Services;

public interface IAuthService
{
    User? Login(string username, string password);
    User? LoginByPin(string pin);
    List<User> GetAllUsers();
    User CreateUser(string username, string fullName, UserRole role, string password, string pinCode, int currentUserId = 1);
    void UpdateUser(int id, string fullName, UserRole role, string? newPassword, string? newPin, bool isActive, int currentUserId = 1);
    void DeleteUser(int id, int currentUserId = 1);
    void AddLoyaltyPoints(int userId, decimal points);
    List<User> GetSalesPersons();
}

public interface IProductService
{
    List<Product> GetAll();
    List<Product> GetByCategory(int categoryId);
    List<Product> GetBySupplier(int supplierId);
    List<Product> Search(string query);
    List<Product> AdaptiveSearch(string query);
    List<Product> GetCrossSellingSuggestions(List<int> cartProductIds, int maxResults = 3);
    Product? GetById(int id);
    Product? GetByBarcode(string barcode);
    void Save(Product product);
    void AdjustStock(int productId, int quantityChange, string reason, int userId);
    void Delete(int id, int userId = 1, string reason = "Deleted by user");
    void Restore(int id, int userId = 1);
    List<Product> GetDeletedProducts();
    Product CreateQuickProduct(string name, decimal sellingPrice, decimal costPrice, int categoryId, int initialStock = 100, int userId = 1);
}

public interface ICategoryService
{
    List<Category> GetAll();
    List<Category> GetDeletedCategories();
    List<Category> GetBySupplier(int supplierId);
    void Save(Category category);
    void Delete(int id, int userId = 1);
    void Restore(int id, int userId = 1);
}

public interface ICustomerService
{
    List<Customer> GetAll();
    Customer? GetById(int id);
    List<Customer> Search(string query);
    void Save(Customer customer);
    void AddLoyaltyPoints(int customerId, decimal points);
    void DeductCredit(int customerId, decimal amount);
    void AddCredit(int customerId, decimal amount);
}

public interface IOrderService
{
    string GenerateInvoiceNumber();
    Order CreateOrder(int cashierId, int? customerId, List<OrderItem> items, decimal discountTotal);
    Order CreateOrder(int cashierId, int? customerId, List<OrderItem> items, decimal discountTotal, int? salesPersonId, string? salesPersonName, decimal salesPersonPoints = 0);
    void AddDebtToOrder(int orderId, decimal debtAmount);
    void CompletePayment(int orderId, PaymentMethod method, decimal amountPaid);
    void CompletePaymentWithDebt(int orderId, PaymentMethod method, decimal amountPaid, decimal debtAmount);
    void HoldOrder(int orderId, string notes);
    List<Order> GetHeldOrders();
    void ResumeOrder(int orderId);
    void CancelHeldOrder(int orderId, int userId = 1);
    void LogVoidAudit(int userId, int itemCount, decimal amount, string reason);
    void VoidOrder(int orderId, int userId);
    List<Order> GetAllOrders(OrderStatus? status = null, string? search = null, DateTime? fromDate = null, DateTime? toDate = null);
    Order? GetOrderById(int id);
    Order? GetOrderWithDetails(int orderId);
}

public interface IDashboardService
{
    decimal GetTodaySales();
    decimal GetTodayProfit();
    int GetTodayTransactionCount();
    int GetLowStockCount();
    Dictionary<string, decimal> GetSalesLast7Days();
    List<(string ProductName, int QuantitySold)> GetTopSellingProducts(int count = 5);
    List<(string CategoryName, decimal Total)> GetSalesByCategory();
}

public interface ISettingsService
{
    StoreProfile GetProfile();
    void SaveProfile(StoreProfile profile);
    string? Get(string key);
    void Set(string key, string value);
    LoyaltySettings GetLoyaltySettings();
    void SaveLoyaltySettings(LoyaltySettings settings);
}

public interface IEmailService
{
    EmailReportSettings GetSettings();
    void SaveSettings(EmailReportSettings s);
    DailyReportSummary BuildDailySummary(DateTime? targetDate = null);
    (string Subject, string HtmlBody, string PlainText) GenerateReportEmail(DailyReportSummary summary);
    Task<(bool Success, string Message)> SendDailyReportAsync(string? overrideRecipient = null);
}

public interface IReceiptService
{
    string GenerateReceiptText(Order order);
}
