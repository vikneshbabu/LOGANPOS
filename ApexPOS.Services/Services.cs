using ApexPOS.Data;
using ApexPOS.Domain.Entities;
using ApexPOS.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace ApexPOS.Services;

public class AuthService : IAuthService
{
    private readonly ApexDbContext _db;

    public AuthService(ApexDbContext db) => _db = db;

    public User? Login(string username, string password)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password)) return null;
        var user = _db.Users.FirstOrDefault(u => u.Username == username && u.IsActive);
        if (user == null) return null;
        return DbSeeder.VerifyPassword(password, user.PasswordHash) ? user : null;
    }

    public User? LoginByPin(string pin)
    {
        if (string.IsNullOrWhiteSpace(pin)) return null;
        return _db.Users.FirstOrDefault(u => u.PinCode == pin && u.IsActive);
    }

    public List<User> GetAllUsers()
    {
        return _db.Users
            .OrderBy(u => u.Role)
            .ThenBy(u => u.Username)
            .ToList();
    }

    public User CreateUser(string username, string fullName, UserRole role, string password, string pinCode, int currentUserId = 1)
    {
        if (string.IsNullOrWhiteSpace(username))
            throw new ArgumentNullException(nameof(username), "Username cannot be null or empty.");

        var trimmedUser = username.Trim();
        if (_db.Users.Any(u => u.Username.ToLower() == trimmedUser.ToLower()))
        {
            throw new InvalidOperationException($"Username '{trimmedUser}' already exists.");
        }

        var pass = string.IsNullOrWhiteSpace(password) ? (string.IsNullOrWhiteSpace(pinCode) ? "1234" : pinCode) : password;
        var pin = string.IsNullOrWhiteSpace(pinCode) ? "0000" : pinCode.Trim();

        var user = new User
        {
            Username = trimmedUser,
            FullName = string.IsNullOrWhiteSpace(fullName) ? trimmedUser : fullName.Trim(),
            Role = role,
            PasswordHash = DbSeeder.HashPassword(pass),
            PinCode = pin,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _db.Users.Add(user);

        var validUserId = _db.Users.Any(u => u.Id == currentUserId) ? currentUserId : 1;
        _db.AuditLogs.Add(new AuditLog
        {
            UserId = validUserId,
            Action = "CREATE_USER",
            Details = $"Created user '{user.Username}' ({user.FullName}) with role '{role}'",
            Timestamp = DateTime.UtcNow
        });

        _db.SaveChanges();
        return user;
    }

    public void UpdateUser(int id, string fullName, UserRole role, string? newPassword, string? newPin, bool isActive, int currentUserId = 1)
    {
        var user = _db.Users.Find(id);
        if (user == null) return;

        user.FullName = fullName.Trim();
        user.Role = role;
        user.IsActive = isActive;

        if (!string.IsNullOrWhiteSpace(newPassword))
        {
            user.PasswordHash = DbSeeder.HashPassword(newPassword);
        }

        if (!string.IsNullOrWhiteSpace(newPin))
        {
            user.PinCode = newPin.Trim();
        }

        var validUserId = _db.Users.Any(u => u.Id == currentUserId) ? currentUserId : 1;
        _db.AuditLogs.Add(new AuditLog
        {
            UserId = validUserId,
            Action = "UPDATE_USER",
            Details = $"Updated user '{user.Username}' ({user.FullName}) Role: {role}, Active: {isActive}",
            Timestamp = DateTime.UtcNow
        });

        _db.SaveChanges();
    }

    public void DeleteUser(int id, int currentUserId = 1)
    {
        var user = _db.Users.Find(id);
        if (user == null) return;

        user.IsActive = false;

        var validUserId = _db.Users.Any(u => u.Id == currentUserId) ? currentUserId : 1;
        _db.AuditLogs.Add(new AuditLog
        {
            UserId = validUserId,
            Action = "DELETE_USER",
            Details = $"Deactivated user '{user.Username}' ({user.FullName})",
            Timestamp = DateTime.UtcNow
        });

        _db.SaveChanges();
    }

    public void AddLoyaltyPoints(int userId, decimal points)
    {
        var user = _db.Users.Find(userId);
        if (user != null)
        {
            user.LoyaltyPoints += points;
            _db.SaveChanges();
        }
    }

    public List<User> GetSalesPersons()
    {
        return _db.Users
            .Where(u => u.IsActive)
            .OrderBy(u => u.FullName)
            .ToList();
    }
}

public class ProductService : IProductService
{
    private readonly ApexDbContext _db;

    public ProductService(ApexDbContext db) => _db = db;

    public List<Product> GetAll() => _db.Products
        .Include(p => p.Category).ThenInclude(c => c.Supplier)
        .Include(p => p.Supplier)
        .Where(p => p.IsActive)
        .OrderBy(p => p.Name)
        .ToList();

    public List<Product> GetByCategory(int categoryId) =>
        _db.Products
            .Include(p => p.Category).ThenInclude(c => c.Supplier)
            .Include(p => p.Supplier)
            .Where(p => p.CategoryId == categoryId && p.IsActive)
            .OrderBy(p => p.Name)
            .ToList();

    public List<Product> GetBySupplier(int supplierId) =>
        _db.Products
            .Include(p => p.Category).ThenInclude(c => c.Supplier)
            .Include(p => p.Supplier)
            .Where(p => (p.SupplierId == supplierId || p.Category.SupplierId == supplierId) && p.IsActive)
            .OrderBy(p => p.Name)
            .ToList();

    public Product? GetById(int id) => _db.Products
        .Include(p => p.Category).ThenInclude(c => c.Supplier)
        .Include(p => p.Supplier)
        .FirstOrDefault(p => p.Id == id);

    public Product? GetByBarcode(string barcode) =>
        _db.Products
            .Include(p => p.Category).ThenInclude(c => c.Supplier)
            .Include(p => p.Supplier)
            .FirstOrDefault(p => p.Barcode == barcode && p.IsActive);

    public List<Product> Search(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return GetAll();

        var trimmed = query.Trim();
        var all = GetAll();

        // 1. Direct SKU or Barcode exact match
        var exactBarcodeOrSku = all.FirstOrDefault(p =>
            string.Equals(p.Barcode, trimmed, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(p.SKU, trimmed, StringComparison.OrdinalIgnoreCase));
        if (exactBarcodeOrSku != null)
        {
            return new List<Product> { exactBarcodeOrSku };
        }

        var terms = trimmed.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);

        var scored = new List<(Product Product, int Score)>();

        foreach (var p in all)
        {
            int score = CalculateMatchScore(p, trimmed, terms);
            if (score > 0)
            {
                scored.Add((p, score));
            }
        }

        return scored
            .OrderByDescending(x => x.Score)
            .ThenByDescending(x => x.Product.StockQuantity > 0)
            .ThenBy(x => x.Product.Name)
            .Select(x => x.Product)
            .ToList();
    }

    public List<Product> AdaptiveSearch(string query) => Search(query);

    private static int CalculateMatchScore(Product p, string fullQuery, string[] terms)
    {
        string name = p.Name.ToLowerInvariant();
        string sku = p.SKU.ToLowerInvariant();
        string barcode = (p.Barcode ?? "").ToLowerInvariant();
        string catName = (p.Category?.Name ?? "").ToLowerInvariant();
        string unit = (p.Unit ?? "").ToLowerInvariant();

        // Exact full name match
        if (name == fullQuery) return 1000;
        if (sku == fullQuery || barcode == fullQuery) return 900;

        int score = 0;

        // Substring matches
        if (name.Contains(fullQuery)) score += 400;
        if (sku.Contains(fullQuery)) score += 300;
        if (catName.Contains(fullQuery)) score += 150;

        // Acronym match (e.g. "CM" -> Caramel Macchiato, "EC" -> Espresso Con Panna)
        var nameWords = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (nameWords.Length > 1)
        {
            var acronym = new string(nameWords.Select(w => w[0]).ToArray());
            if (acronym.Equals(fullQuery, StringComparison.OrdinalIgnoreCase))
            {
                score += 250;
            }
        }

        // Multi-term substring matching
        bool allTermsMatched = true;
        foreach (var term in terms)
        {
            bool termMatched = false;

            if (name.Contains(term) || sku.Contains(term) || barcode.Contains(term) || catName.Contains(term))
            {
                termMatched = true;
                score += 100;
            }
            else
            {
                // Fuzzy term matching against individual name words and category
                foreach (var w in nameWords)
                {
                    if (IsFuzzyMatch(term, w))
                    {
                        termMatched = true;
                        score += 60;
                        break;
                    }
                }

                if (!termMatched && IsFuzzyMatch(term, catName))
                {
                    termMatched = true;
                    score += 40;
                }
            }

            if (!termMatched)
            {
                allTermsMatched = false;
            }
        }

        if (allTermsMatched) score += 150;

        return score;
    }

    private static bool IsFuzzyMatch(string queryTerm, string targetWord)
    {
        if (string.IsNullOrEmpty(queryTerm) || string.IsNullOrEmpty(targetWord)) return false;

        // Exact or prefix match
        if (targetWord.StartsWith(queryTerm, StringComparison.OrdinalIgnoreCase)) return true;
        if (queryTerm.StartsWith(targetWord, StringComparison.OrdinalIgnoreCase)) return true;

        int qLen = queryTerm.Length;
        int tLen = targetWord.Length;

        // Allow typos based on term length
        int maxAllowedDist = qLen switch
        {
            <= 3 => 0,
            <= 6 => 1,
            _ => 2
        };

        if (Math.Abs(qLen - tLen) > maxAllowedDist) return false;

        int dist = ComputeLevenshteinDistance(queryTerm, targetWord);
        return dist <= maxAllowedDist;
    }

    public static int ComputeLevenshteinDistance(string s, string t)
    {
        if (string.IsNullOrEmpty(s)) return t?.Length ?? 0;
        if (string.IsNullOrEmpty(t)) return s.Length;

        int n = s.Length;
        int m = t.Length;
        int[,] d = new int[n + 1, m + 1];

        for (int i = 0; i <= n; d[i, 0] = i++) { }
        for (int j = 0; j <= m; d[0, j] = j++) { }

        for (int i = 1; i <= n; i++)
        {
            for (int j = 1; j <= m; j++)
            {
                int cost = (t[j - 1] == s[i - 1]) ? 0 : 1;
                d[i, j] = Math.Min(
                    Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                    d[i - 1, j - 1] + cost);
            }
        }
        return d[n, m];
    }

    public List<Product> GetCrossSellingSuggestions(List<int> cartProductIds, int maxResults = 3)
    {
        if (cartProductIds == null || cartProductIds.Count == 0)
            return new List<Product>();

        var allActive = GetAll();
        var cartIdSet = new HashSet<int>(cartProductIds);

        // 1. Check historical co-occurrence in OrderItems
        var coOccurredProductIds = _db.OrderItems
            .Where(oi => cartIdSet.Contains(oi.ProductId))
            .Select(oi => oi.OrderId)
            .Distinct()
            .SelectMany(orderId => _db.OrderItems
                .Where(oi => oi.OrderId == orderId && !cartIdSet.Contains(oi.ProductId))
                .Select(oi => oi.ProductId))
            .GroupBy(id => id)
            .OrderByDescending(g => g.Count())
            .Select(g => g.Key)
            .Take(maxResults)
            .ToList();

        var suggestions = allActive.Where(p => coOccurredProductIds.Contains(p.Id)).ToList();
        if (suggestions.Count >= maxResults)
            return suggestions;

        // 2. Complementary Category pairing fallback
        // Check categories of cart items
        var cartCategories = allActive
            .Where(p => cartIdSet.Contains(p.Id))
            .Select(p => p.Category?.Name?.ToLowerInvariant() ?? "")
            .ToList();

        bool hasBeverage = cartCategories.Any(c => c.Contains("coffee") || c.Contains("beverage") || c.Contains("drink") || c.Contains("tea"));
        bool hasBakery = cartCategories.Any(c => c.Contains("bakery") || c.Contains("pastry") || c.Contains("snack") || c.Contains("food"));

        IEnumerable<Product> candidates;
        if (hasBeverage && !hasBakery)
        {
            // Suggest bakery / snacks
            candidates = allActive.Where(p => !cartIdSet.Contains(p.Id) &&
                ((p.Category?.Name ?? "").ToLowerInvariant().Contains("bakery") ||
                 (p.Category?.Name ?? "").ToLowerInvariant().Contains("pastry") ||
                 (p.Category?.Name ?? "").ToLowerInvariant().Contains("snack")));
        }
        else if (hasBakery && !hasBeverage)
        {
            // Suggest beverages / coffee
            candidates = allActive.Where(p => !cartIdSet.Contains(p.Id) &&
                ((p.Category?.Name ?? "").ToLowerInvariant().Contains("coffee") ||
                 (p.Category?.Name ?? "").ToLowerInvariant().Contains("beverage") ||
                 (p.Category?.Name ?? "").ToLowerInvariant().Contains("drink")));
        }
        else
        {
            // Suggest top products from other categories not yet in cart
            candidates = allActive.Where(p => !cartIdSet.Contains(p.Id));
        }

        foreach (var c in candidates)
        {
            if (!suggestions.Any(s => s.Id == c.Id))
            {
                suggestions.Add(c);
                if (suggestions.Count >= maxResults) break;
            }
        }

        return suggestions;
    }

    public List<Product> GetLowStock() =>
        _db.Products
            .Include(p => p.Category).ThenInclude(c => c.Supplier)
            .Include(p => p.Supplier)
            .Where(p => p.IsActive && p.StockQuantity <= p.ReorderLevel)
            .ToList();

    public void Save(Product product)
    {
        if (product.Id == 0)
        {
            _db.Products.Add(product);
            _db.SaveChanges();

            try
            {
                _db.PurchaseRateLogs.Add(new PurchaseRateLog
                {
                    ProductId = product.Id,
                    ProductName = product.Name,
                    SKU = product.SKU,
                    Barcode = product.Barcode,
                    OldCostPrice = product.CostPrice,
                    NewCostPrice = product.CostPrice,
                    SellingPrice = product.SellingPrice,
                    SupplierId = product.SupplierId,
                    SupplierName = product.DistributorName,
                    Source = "New Product Creation",
                    Notes = "Initial product cost price registered",
                    EffectiveDate = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow
                });
                _db.SaveChanges();
            }
            catch { }
        }
        else
        {
            var existing = _db.Products.AsNoTracking().FirstOrDefault(p => p.Id == product.Id);
            decimal oldCost = existing?.CostPrice ?? product.CostPrice;

            _db.Products.Update(product);
            _db.SaveChanges();

            if (existing != null && existing.CostPrice != product.CostPrice)
            {
                try
                {
                    _db.PurchaseRateLogs.Add(new PurchaseRateLog
                    {
                        ProductId = product.Id,
                        ProductName = product.Name,
                        SKU = product.SKU,
                        Barcode = product.Barcode,
                        OldCostPrice = oldCost,
                        NewCostPrice = product.CostPrice,
                        SellingPrice = product.SellingPrice,
                        SupplierId = product.SupplierId,
                        SupplierName = product.DistributorName,
                        Source = "Inventory Price Update",
                        Notes = $"Cost revised from ₹{oldCost:F2} to ₹{product.CostPrice:F2}",
                        EffectiveDate = DateTime.UtcNow,
                        CreatedAt = DateTime.UtcNow
                    });
                    _db.SaveChanges();
                }
                catch { }
            }
        }
    }

    public void AdjustStock(int productId, int quantityChange, string reason, int userId)
    {
        var product = _db.Products.Find(productId);
        if (product == null) return;

        var preStock = product.StockQuantity;
        product.StockQuantity += quantityChange;

        _db.InventoryTransactions.Add(new InventoryTransaction
        {
            ProductId = productId,
            Type = quantityChange > 0 ? InventoryTransactionType.Restock : InventoryTransactionType.Adjustment,
            QuantityChanged = quantityChange,
            PreStock = preStock,
            PostStock = product.StockQuantity,
            Reason = reason,
            UserId = userId
        });
        _db.SaveChanges();
    }

    public void Delete(int id, int userId = 1, string reason = "Deleted by user")
    {
        var product = _db.Products.Find(id);
        if (product == null || !product.IsActive) return;

        product.IsActive = false;

        var validUserId = _db.Users.Any(u => u.Id == userId)
            ? userId
            : (_db.Users.Select(u => u.Id).FirstOrDefault());
        if (validUserId > 0)
        {
            _db.AuditLogs.Add(new AuditLog
            {
                UserId = validUserId,
                Action = "DELETE_PRODUCT",
                Details = $"Product '{product.Name}' (SKU: {product.SKU}, ID: {product.Id}) deleted. Reason: {reason}",
                Timestamp = DateTime.UtcNow
            });
        }
        _db.SaveChanges();
    }

    public void Restore(int id, int userId = 1)
    {
        var product = _db.Products.Find(id);
        if (product == null || product.IsActive) return;

        product.IsActive = true;

        var validUserId = _db.Users.Any(u => u.Id == userId)
            ? userId
            : (_db.Users.Select(u => u.Id).FirstOrDefault());
        if (validUserId > 0)
        {
            _db.AuditLogs.Add(new AuditLog
            {
                UserId = validUserId,
                Action = "RESTORE_PRODUCT",
                Details = $"Product '{product.Name}' (SKU: {product.SKU}, ID: {product.Id}) restored to active inventory.",
                Timestamp = DateTime.UtcNow
            });
        }
        _db.SaveChanges();
    }

    public List<Product> GetDeletedProducts() => _db.Products
        .Include(p => p.Category).ThenInclude(c => c.Supplier)
        .Include(p => p.Supplier)
        .Where(p => !p.IsActive)
        .OrderByDescending(p => p.Id)
        .ToList();

    public List<AuditLog> GetDeleteLogs(int limit = 50) => _db.AuditLogs
        .Include(a => a.User)
        .Where(a => a.Action.StartsWith("DELETE_") || a.Action.StartsWith("RESTORE_"))
        .OrderByDescending(a => a.Timestamp)
        .Take(limit)
        .ToList();

    public Product CreateQuickProduct(string name, decimal sellingPrice, decimal costPrice, int categoryId, int initialStock = 100, int userId = 1)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentNullException(nameof(name), "Product name cannot be empty.");
        if (sellingPrice < 0)
            throw new ArgumentOutOfRangeException(nameof(sellingPrice), "Selling price cannot be negative.");
        if (costPrice < 0)
            throw new ArgumentOutOfRangeException(nameof(costPrice), "Cost price cannot be negative.");

        var timestamp = DateTime.UtcNow.ToString("yyMMddHHmmss");
        var sku = $"MAN-{timestamp}";

        // If category is not specified, get first available category
        if (categoryId <= 0)
        {
            categoryId = _db.Categories.Where(c => c.IsActive).Select(c => c.Id).FirstOrDefault();
            if (categoryId <= 0)
            {
                var defaultCat = new Category { Name = "General", Icon = "📦", IsActive = true, SortOrder = 1 };
                _db.Categories.Add(defaultCat);
                _db.SaveChanges();
                categoryId = defaultCat.Id;
            }
        }

        var product = new Product
        {
            Name = name.Trim(),
            SKU = sku,
            Barcode = sku,
            SellingPrice = sellingPrice,
            CostPrice = costPrice,
            StockQuantity = initialStock,
            ReorderLevel = 10,
            CategoryId = categoryId,
            Unit = "piece",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _db.Products.Add(product);

        var validUserId = _db.Users.Any(u => u.Id == userId)
            ? userId
            : (_db.Users.Select(u => u.Id).FirstOrDefault());
        if (validUserId > 0)
        {
            _db.AuditLogs.Add(new AuditLog
            {
                UserId = validUserId,
                Action = "QUICK_ADD_PRODUCT",
                Details = $"Manual product '{product.Name}' (SKU: {product.SKU}, Price: ₹{sellingPrice:F2}) added from Register.",
                Timestamp = DateTime.UtcNow
            });
        }

        _db.SaveChanges();
        return product;
    }
}

public class CategoryService : ICategoryService
{
    private readonly ApexDbContext _db;

    public CategoryService(ApexDbContext db) => _db = db;

    public List<Category> GetAll() => _db.Categories
        .Include(c => c.Supplier)
        .Where(c => c.IsActive)
        .OrderBy(c => c.SortOrder)
        .ToList();

    public List<Category> GetDeletedCategories() => _db.Categories
        .Include(c => c.Supplier)
        .Where(c => !c.IsActive)
        .OrderBy(c => c.Name)
        .ToList();

    public List<Category> GetBySupplier(int supplierId) => _db.Categories
        .Include(c => c.Supplier)
        .Where(c => c.SupplierId == supplierId && c.IsActive)
        .OrderBy(c => c.SortOrder)
        .ToList();

    public void Save(Category category)
    {
        if (category == null) throw new ArgumentNullException(nameof(category));
        if (string.IsNullOrWhiteSpace(category.Name)) throw new ArgumentException("Category name cannot be empty.", nameof(category));

        if (category.Id == 0)
            _db.Categories.Add(category);
        else
            _db.Categories.Update(category);
        _db.SaveChanges();
    }

    public void Delete(int id, int userId = 1)
    {
        var cat = _db.Categories.Find(id);
        if (cat != null && cat.IsActive)
        {
            cat.IsActive = false;
            var validUserId = _db.Users.Any(u => u.Id == userId)
                ? userId
                : (_db.Users.Select(u => u.Id).FirstOrDefault());
            if (validUserId > 0)
            {
                _db.AuditLogs.Add(new AuditLog
                {
                    UserId = validUserId,
                    Action = "DELETE_CATEGORY",
                    Details = $"Category '{cat.Name}' (ID: {cat.Id}) deleted.",
                    Timestamp = DateTime.UtcNow
                });
            }
            _db.SaveChanges();
        }
    }

    public void Restore(int id, int userId = 1)
    {
        var cat = _db.Categories.Find(id);
        if (cat != null && !cat.IsActive)
        {
            cat.IsActive = true;
            var validUserId = _db.Users.Any(u => u.Id == userId)
                ? userId
                : (_db.Users.Select(u => u.Id).FirstOrDefault());
            if (validUserId > 0)
            {
                _db.AuditLogs.Add(new AuditLog
                {
                    UserId = validUserId,
                    Action = "RESTORE_CATEGORY",
                    Details = $"Category '{cat.Name}' (ID: {cat.Id}) restored.",
                    Timestamp = DateTime.UtcNow
                });
            }
            _db.SaveChanges();
        }
    }
}

public class CustomerService : ICustomerService
{
    private readonly ApexDbContext _db;

    public CustomerService(ApexDbContext db) => _db = db;

    public List<Customer> GetAll() => _db.Customers.Where(c => c.IsActive).OrderBy(c => c.Name).ToList();

    public Customer? GetById(int id) => _db.Customers.Find(id);

    public List<Customer> Search(string query) =>
        _db.Customers.Where(c => c.IsActive && (c.Name.Contains(query) || c.Code.Contains(query) || c.Phone.Contains(query)))
            .OrderBy(c => c.Name).ToList();

    public void Save(Customer customer)
    {
        if (customer == null) throw new ArgumentNullException(nameof(customer));
        if (string.IsNullOrWhiteSpace(customer.Name)) throw new ArgumentException("Customer name cannot be empty.", nameof(customer));

        if (customer.Id == 0)
            _db.Customers.Add(customer);
        else
            _db.Customers.Update(customer);
        _db.SaveChanges();
    }

    public void AddLoyaltyPoints(int customerId, decimal points)
    {
        var customer = _db.Customers.Find(customerId);
        if (customer != null)
        {
            customer.LoyaltyPoints += points;
            _db.SaveChanges();
        }
    }

    public void DeductCredit(int customerId, decimal amount)
    {
        var customer = _db.Customers.Find(customerId);
        if (customer != null && customer.StoreCredit >= amount)
        {
            customer.StoreCredit -= amount;
            _db.SaveChanges();
        }
    }

    public void AddCredit(int customerId, decimal amount)
    {
        var customer = _db.Customers.Find(customerId);
        if (customer != null)
        {
            customer.StoreCredit += amount;
            _db.SaveChanges();
        }
    }
}

public class OrderService : IOrderService
{
    private readonly ApexDbContext _db;

    public OrderService(ApexDbContext db) => _db = db;

    private static int _invoiceCounter = 0;

    public string GenerateInvoiceNumber()
    {
        if (_invoiceCounter == 0)
        {
            var lastOrder = _db.Orders.OrderByDescending(o => o.Id).FirstOrDefault();
            if (lastOrder != null && lastOrder.InvoiceNumber.StartsWith("INV-"))
            {
                int.TryParse(lastOrder.InvoiceNumber.Replace("INV-", ""), out _invoiceCounter);
            }
        }
        _invoiceCounter++;
        return $"INV-{_invoiceCounter:D6}";
    }

    public Order CreateOrder(int cashierId, int? customerId, List<OrderItem> items, decimal discountTotal)
    {
        return CreateOrder(cashierId, customerId, items, discountTotal, null, null, 0);
    }

    public Order CreateOrder(int cashierId, int? customerId, List<OrderItem> items, decimal discountTotal, int? salesPersonId, string? salesPersonName, decimal salesPersonPoints = 0)
    {
        if (cashierId <= 0)
            throw new ArgumentOutOfRangeException(nameof(cashierId), "Cashier ID must be positive.");
        if (items == null)
            throw new ArgumentNullException(nameof(items), "Order items list cannot be null.");
        if (!items.Any())
            throw new InvalidOperationException("Cannot create an order with zero items.");

        decimal subTotal = items.Sum(i => i.UnitPrice * i.Quantity - i.DiscountAmount);

        var order = new Order
        {
            InvoiceNumber = GenerateInvoiceNumber(),
            CashierId = cashierId,
            CustomerId = customerId,
            SalesPersonId = salesPersonId,
            SalesPersonName = salesPersonName,
            SalesPersonLoyaltyPointsEarned = salesPersonPoints,
            SubTotal = subTotal,
            TaxTotal = 0m,
            DiscountTotal = discountTotal,
            GrandTotal = subTotal - discountTotal,
            PaymentStatus = PaymentStatus.Pending,
            OrderStatus = OrderStatus.Active
        };

        _db.Orders.Add(order);
        _db.SaveChanges();

        foreach (var item in items)
        {
            item.OrderId = order.Id;
            _db.OrderItems.Add(item);

            // Deduct stock
            var product = _db.Products.Find(item.ProductId);
            if (product != null)
            {
                product.StockQuantity -= item.Quantity;
            }
        }
        _db.SaveChanges();

        return order;
    }

    public void AddDebtToOrder(int orderId, decimal debtAmount)
    {
        var order = _db.Orders.Find(orderId);
        if (order != null)
        {
            order.GrandTotal += debtAmount;
            _db.SaveChanges();
        }
    }

    public void CompletePayment(int orderId, PaymentMethod method, decimal amountPaid)
    {
        var order = _db.Orders.Include(o => o.Payments).FirstOrDefault(o => o.Id == orderId);
        if (order == null) return;

        var totalPaid = order.Payments.Sum(p => p.AmountPaid) + amountPaid;
        decimal change = Math.Max(0, totalPaid - order.GrandTotal);

        _db.Payments.Add(new Payment
        {
            OrderId = orderId,
            PaymentMethod = method,
            AmountPaid = amountPaid,
            ChangeGiven = change
        });

        if (totalPaid >= order.GrandTotal)
        {
            order.PaymentStatus = PaymentStatus.Paid;
            order.OrderStatus = OrderStatus.Completed;
            order.CompletedAt = DateTime.UtcNow;
        }
        else
        {
            order.PaymentStatus = PaymentStatus.PartiallyPaid;
        }

        _db.SaveChanges();
    }

    public void CompletePaymentWithDebt(int orderId, PaymentMethod method, decimal amountPaid, decimal debtAmount)
    {
        var order = _db.Orders.Include(o => o.Payments).FirstOrDefault(o => o.Id == orderId);
        if (order == null) return;

        // 1. Record the tendered payment (Cash, Card, etc.)
        if (amountPaid > 0)
        {
            _db.Payments.Add(new Payment
            {
                OrderId = orderId,
                PaymentMethod = method,
                AmountPaid = amountPaid,
                ChangeGiven = 0,
                ReferenceNo = "PARTIAL_PAYMENT"
            });
        }

        // 2. Record the unpaid balance added to customer debt
        if (debtAmount > 0)
        {
            _db.Payments.Add(new Payment
            {
                OrderId = orderId,
                PaymentMethod = PaymentMethod.StoreCredit,
                AmountPaid = debtAmount,
                ChangeGiven = 0,
                ReferenceNo = "ADDED_TO_CUSTOMER_DEBT"
            });
        }

        order.PaymentStatus = PaymentStatus.Paid;
        order.OrderStatus = OrderStatus.Completed;
        order.CompletedAt = DateTime.UtcNow;

        _db.SaveChanges();
    }

    public void HoldOrder(int orderId, string notes)
    {
        var order = _db.Orders.Find(orderId);
        if (order == null) return;
        order.OrderStatus = OrderStatus.OnHold;
        order.HoldNotes = notes;
        _db.SaveChanges();
    }

    public List<Order> GetHeldOrders() =>
        _db.Orders.Include(o => o.Items).Include(o => o.Cashier).Include(o => o.Customer)
            .Where(o => o.OrderStatus == OrderStatus.OnHold)
            .OrderByDescending(o => o.CreatedAt).ToList();

    public void ResumeOrder(int orderId)
    {
        var order = _db.Orders.Find(orderId);
        if (order == null) return;
        order.OrderStatus = OrderStatus.Active;
        order.HoldNotes = null;
        _db.SaveChanges();
    }

    public void CancelHeldOrder(int orderId, int userId = 1)
    {
        var order = _db.Orders.Include(o => o.Items).FirstOrDefault(o => o.Id == orderId);
        if (order == null) return;

        // Restore stock deducted when held
        foreach (var item in order.Items)
        {
            var product = _db.Products.Find(item.ProductId);
            if (product != null)
                product.StockQuantity += item.Quantity;
        }

        order.OrderStatus = OrderStatus.Voided;
        _db.SaveChanges();

        var validUserId = _db.Users.Any(u => u.Id == userId)
            ? userId
            : (_db.Users.Select(u => u.Id).FirstOrDefault());
        if (validUserId > 0)
        {
            _db.AuditLogs.Add(new AuditLog
            {
                UserId = validUserId,
                Action = "CANCEL_HELD_ORDER",
                Details = $"Held order '{order.InvoiceNumber}' cancelled/dismissed.",
                Timestamp = DateTime.UtcNow
            });
            _db.SaveChanges();
        }
    }

    public void LogVoidAudit(int userId, int itemCount, decimal amount, string reason)
    {
        var validUserId = _db.Users.Any(u => u.Id == userId)
            ? userId
            : (_db.Users.Select(u => u.Id).FirstOrDefault());
        if (validUserId > 0)
        {
            _db.AuditLogs.Add(new AuditLog
            {
                UserId = validUserId,
                Action = "VOID_BILL",
                Details = $"Active cart voided: {itemCount} items, Total: ₹{amount:F2}. Reason: {reason}",
                Timestamp = DateTime.UtcNow
            });
            _db.SaveChanges();
        }
    }

    public void VoidOrder(int orderId, int userId)
    {
        var order = _db.Orders.Include(o => o.Items).FirstOrDefault(o => o.Id == orderId);
        if (order == null) return;

        // Restore stock
        foreach (var item in order.Items)
        {
            var product = _db.Products.Find(item.ProductId);
            if (product != null)
                product.StockQuantity += item.Quantity;
        }

        order.OrderStatus = OrderStatus.Voided;
        _db.SaveChanges();
    }

    public List<Order> GetAllOrders(OrderStatus? status = null, string? search = null, DateTime? fromDate = null, DateTime? toDate = null)
    {
        var query = _db.Orders
            .Include(o => o.Cashier)
            .Include(o => o.SalesPerson)
            .Include(o => o.Customer)
            .Include(o => o.Items)
            .Include(o => o.Payments)
            .AsQueryable();

        if (status.HasValue)
        {
            query = query.Where(o => o.OrderStatus == status.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(o => o.InvoiceNumber.Contains(search) ||
                                     (o.Customer != null && o.Customer.Name.Contains(search)));
        }

        if (fromDate.HasValue)
        {
            var start = fromDate.Value.Date;
            query = query.Where(o => o.CreatedAt >= start);
        }

        if (toDate.HasValue)
        {
            var end = toDate.Value.Date.AddDays(1).AddTicks(-1);
            query = query.Where(o => o.CreatedAt <= end);
        }

        return query.OrderByDescending(o => o.CreatedAt).ToList();
    }

    public Order? GetOrderWithDetails(int orderId)
    {
        return _db.Orders
            .Include(o => o.Cashier)
            .Include(o => o.SalesPerson)
            .Include(o => o.Customer)
            .Include(o => o.Items)
            .Include(o => o.Payments)
            .FirstOrDefault(o => o.Id == orderId);
    }

    public Order? GetOrderById(int id) => GetOrderWithDetails(id);

    public string GenerateSalesReportCsv(IEnumerable<Order> orders, DateTime? fromDate, DateTime? toDate, StoreProfile? profile = null)
    {
        var orderList = orders.ToList();
        var sb = new System.Text.StringBuilder();

        var storeName = profile?.StoreName ?? "ApexPOS Store";
        var currency = profile?.CurrencySymbol ?? "₹";
        var dateRangeStr = (fromDate.HasValue || toDate.HasValue)
            ? $"{(fromDate.HasValue ? fromDate.Value.ToString("yyyy-MM-dd") : "Beginning")} to {(toDate.HasValue ? toDate.Value.ToString("yyyy-MM-dd") : "Present")}"
            : "All Time";

        // Store and Report Header
        sb.AppendLine($"\"{EscapeCsv(storeName)}\"");
        if (!string.IsNullOrWhiteSpace(profile?.StoreAddress))
            sb.AppendLine($"\"{EscapeCsv(profile.StoreAddress)}\"");
        if (!string.IsNullOrWhiteSpace(profile?.StorePhone))
            sb.AppendLine($"\"Phone: {EscapeCsv(profile.StorePhone)}\"");
        sb.AppendLine("\"SALES & ORDERS REPORT\"");
        sb.AppendLine($"\"Report Period:\",\"{dateRangeStr}\"");
        sb.AppendLine($"\"Generated At:\",\"{DateTime.Now:yyyy-MM-dd HH:mm:ss}\"");
        sb.AppendLine();

        // Summary Metrics
        var totalOrders = orderList.Count;
        var completedOrders = orderList.Count(o => o.OrderStatus == OrderStatus.Completed);
        var onHoldOrders = orderList.Count(o => o.OrderStatus == OrderStatus.OnHold);
        var voidedOrders = orderList.Count(o => o.OrderStatus == OrderStatus.Voided);
        var grossSales = orderList.Where(o => o.OrderStatus == OrderStatus.Completed).Sum(o => o.SubTotal);
        var totalTax = orderList.Where(o => o.OrderStatus == OrderStatus.Completed).Sum(o => o.TaxTotal);
        var totalDiscount = orderList.Where(o => o.OrderStatus == OrderStatus.Completed).Sum(o => o.DiscountTotal);
        var netRevenue = orderList.Where(o => o.OrderStatus == OrderStatus.Completed).Sum(o => o.GrandTotal);

        sb.AppendLine("\"=== EXECUTIVE SUMMARY ===\"");
        sb.AppendLine($"\"Total Orders\",{totalOrders}");
        sb.AppendLine($"\"Completed Orders\",{completedOrders}");
        sb.AppendLine($"\"On-Hold Orders\",{onHoldOrders}");
        sb.AppendLine($"\"Voided Orders\",{voidedOrders}");
        sb.AppendLine($"\"Gross Subtotal ({currency})\",{grossSales:F2}");
        sb.AppendLine($"\"Total Tax Collected ({currency})\",{totalTax:F2}");
        sb.AppendLine($"\"Total Discounts ({currency})\",{totalDiscount:F2}");
        sb.AppendLine($"\"Net Revenue ({currency})\",{netRevenue:F2}");
        sb.AppendLine();

        // Transaction Ledger
        sb.AppendLine("\"=== TRANSACTION LEDGER ===\"");
        sb.AppendLine("\"Invoice #\",\"Date & Time\",\"Status\",\"Customer Name\",\"Customer Phone\",\"Cashier\",\"Items Count\",\"Subtotal\",\"Tax Total\",\"Discount Total\",\"Grand Total\",\"Payment Method(s)\",\"Total Paid\",\"Balance Due\"");
        foreach (var o in orderList)
        {
            var methods = (o.Payments != null && o.Payments.Any())
                ? string.Join("; ", o.Payments.Select(p => p.PaymentMethod.ToString()))
                : "N/A";
            var custName = o.Customer?.Name ?? "Walk-in";
            var custPhone = o.Customer?.Phone ?? "-";
            var cashier = o.Cashier?.FullName ?? $"User #{o.CashierId}";
            var itemsCount = o.Items?.Sum(i => i.Quantity) ?? 0;

            sb.AppendLine($"\"{EscapeCsv(o.InvoiceNumber)}\",\"{o.CreatedAt:yyyy-MM-dd HH:mm}\",\"{o.OrderStatus}\",\"{EscapeCsv(custName)}\",\"{EscapeCsv(custPhone)}\",\"{EscapeCsv(cashier)}\",{itemsCount},{o.SubTotal:F2},{o.TaxTotal:F2},{o.DiscountTotal:F2},{o.GrandTotal:F2},\"{EscapeCsv(methods)}\",{o.TotalPaid:F2},{o.BalanceDue:F2}");
        }
        sb.AppendLine();

        // Product Breakdown
        sb.AppendLine("\"=== PRODUCT SALES BREAKDOWN ===\"");
        sb.AppendLine("\"SKU\",\"Product Name\",\"Quantity Sold\",\"Unit Price (Avg)\",\"Total Sales\"");
        var productGroups = orderList
            .Where(o => o.OrderStatus == OrderStatus.Completed && o.Items != null)
            .SelectMany(o => o.Items)
            .GroupBy(i => new { i.SKU, i.ProductName })
            .OrderByDescending(g => g.Sum(x => x.TotalPrice));

        foreach (var grp in productGroups)
        {
            var totalQty = grp.Sum(x => x.Quantity);
            var totalRevenue = grp.Sum(x => x.TotalPrice);
            var avgPrice = totalQty > 0 ? (totalRevenue / totalQty) : 0;
            sb.AppendLine($"\"{EscapeCsv(grp.Key.SKU)}\",\"{EscapeCsv(grp.Key.ProductName)}\",{totalQty},{avgPrice:F2},{totalRevenue:F2}");
        }

        return sb.ToString();
    }

    private static string EscapeCsv(string? val)
    {
        if (string.IsNullOrEmpty(val)) return "";
        return val.Replace("\"", "\"\"");
    }
}

public class DashboardService : IDashboardService
{
    private readonly ApexDbContext _db;

    public DashboardService(ApexDbContext db) => _db = db;

    public decimal GetTodaySales()
    {
        var today = DateTime.UtcNow.Date;
        return _db.Orders
            .Where(o => o.OrderStatus == OrderStatus.Completed && o.CreatedAt >= today)
            .Select(o => o.GrandTotal)
            .ToList()
            .Sum();
    }

    public decimal GetTodayProfit()
    {
        var today = DateTime.UtcNow.Date;
        var items = _db.OrderItems
            .Include(i => i.Order)
            .Where(i => i.Order.OrderStatus == OrderStatus.Completed && i.Order.CreatedAt >= today)
            .ToList();
        return items.Sum(i => (i.UnitPrice - i.UnitCost) * i.Quantity);
    }

    public int GetTodayTransactionCount()
    {
        var today = DateTime.UtcNow.Date;
        return _db.Orders.Count(o => o.OrderStatus == OrderStatus.Completed && o.CreatedAt >= today);
    }

    public int GetLowStockCount() =>
        _db.Products.Count(p => p.IsActive && p.StockQuantity <= p.ReorderLevel);

    public Dictionary<string, decimal> GetSalesLast7Days()
    {
        var start = DateTime.UtcNow.Date.AddDays(-6);
        var orders = _db.Orders
            .Where(o => o.OrderStatus == OrderStatus.Completed && o.CreatedAt >= start)
            .ToList();

        var result = new Dictionary<string, decimal>();
        for (int i = 0; i < 7; i++)
        {
            var date = start.AddDays(i);
            var dayKey = date.ToString("ddd");
            result[dayKey] = orders.Where(o => o.CreatedAt.Date == date).Sum(o => o.GrandTotal);
        }
        return result;
    }

    public List<(string ProductName, int QuantitySold)> GetTopSellingProducts(int count = 5)
    {
        var today = DateTime.UtcNow.Date.AddDays(-7);
        var items = _db.OrderItems
            .Include(i => i.Order)
            .Where(i => i.Order.OrderStatus == OrderStatus.Completed && i.Order.CreatedAt >= today)
            .ToList();

        return items
            .GroupBy(i => i.ProductName)
            .Select(g => (ProductName: g.Key, QuantitySold: g.Sum(i => i.Quantity)))
            .OrderByDescending(x => x.QuantitySold)
            .Take(count)
            .ToList();
    }

    public List<(string CategoryName, decimal Total)> GetSalesByCategory()
    {
        var today = DateTime.UtcNow.Date.AddDays(-7);
        var items = _db.OrderItems
            .Include(i => i.Order)
            .Include(i => i.Product).ThenInclude(p => p.Category)
            .Where(i => i.Order.OrderStatus == OrderStatus.Completed && i.Order.CreatedAt >= today)
            .ToList();

        return items
            .Where(i => i.Product?.Category != null)
            .GroupBy(i => i.Product.Category.Name)
            .Select(g => (CategoryName: g.Key, Total: g.Sum(i => i.UnitPrice * i.Quantity)))
            .OrderByDescending(x => x.Total)
            .ToList();
    }
}
