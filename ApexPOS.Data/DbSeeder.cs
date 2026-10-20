using System.Security.Cryptography;
using ApexPOS.Domain.Entities;
using ApexPOS.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace ApexPOS.Data;

public static class DbSeeder
{
    public static void Seed(ApexDbContext db)
    {
        db.Database.EnsureCreated();
        EnsureSchemaAndDistributorLinks(db);

        if (db.Products.Any()) return; // Already seeded

        // --- Users ---
        var admin = new User
        {
            Username = "admin",
            PasswordHash = HashPassword("admin123"),
            PinCode = "0000",
            FullName = "System Administrator",
            Role = UserRole.Admin
        };
        var manager = new User
        {
            Username = "manager",
            PasswordHash = HashPassword("manager123"),
            PinCode = "1111",
            FullName = "Store Manager",
            Role = UserRole.Manager
        };
        var cashier1 = new User
        {
            Username = "cashier1",
            PasswordHash = HashPassword("cashier123"),
            PinCode = "1234",
            FullName = "Jane Smith",
            Role = UserRole.Cashier
        };
        var cashier2 = new User
        {
            Username = "cashier2",
            PasswordHash = HashPassword("cashier123"),
            PinCode = "5678",
            FullName = "John Doe",
            Role = UserRole.Cashier
        };
        var sales1 = new User
        {
            Username = "sales1",
            PasswordHash = HashPassword("sales123"),
            PinCode = "2222",
            FullName = "Ravi Kumar (Sales Person)",
            Role = UserRole.SalesPerson,
            LoyaltyPoints = 50.0m,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        db.Users.AddRange(admin, manager, cashier1, cashier2, sales1);
        db.SaveChanges();

        // Load distributors for category assignment
        var agro = db.Suppliers.FirstOrDefault(s => s.Name.Contains("Agro"));
        var fmcg = db.Suppliers.FirstOrDefault(s => s.Name.Contains("FMCG"));
        var dairy = db.Suppliers.FirstOrDefault(s => s.Name.Contains("Dairy"));

        // --- Categories ---
        var categories = new[]
        {
            new Category { Name = "Beverages", Icon = "🥤", SortOrder = 1, SupplierId = dairy?.Id },
            new Category { Name = "Snacks", Icon = "🍿", SortOrder = 2, SupplierId = fmcg?.Id },
            new Category { Name = "Dairy", Icon = "🧀", SortOrder = 3, SupplierId = dairy?.Id },
            new Category { Name = "Bakery", Icon = "🍞", SortOrder = 4, SupplierId = fmcg?.Id },
            new Category { Name = "Fruits & Vegetables", Icon = "🥕", SortOrder = 5, SupplierId = agro?.Id },
            new Category { Name = "Frozen Foods", Icon = "🧊", SortOrder = 6, SupplierId = agro?.Id },
            new Category { Name = "Personal Care", Icon = "🧴", SortOrder = 7, SupplierId = fmcg?.Id },
            new Category { Name = "Household", Icon = "🏠", SortOrder = 8, SupplierId = fmcg?.Id },
        };
        db.Categories.AddRange(categories);
        db.SaveChanges();

        // --- Products ---
        var rng = new Random(42);
        var productData = new (string Name, string Sku, int CatIdx, decimal Cost, decimal Price, int Stock)[]
        {
            ("Coca-Cola 500ml", "BEV-001", 0, 0.80m, 1.50m, 200),
            ("Pepsi 500ml", "BEV-002", 0, 0.75m, 1.50m, 180),
            ("Orange Juice 1L", "BEV-003", 0, 1.50m, 2.99m, 100),
            ("Mineral Water 500ml", "BEV-004", 0, 0.30m, 0.99m, 500),
            ("Coffee Latte RTD", "BEV-005", 0, 1.20m, 2.49m, 120),
            ("Green Tea 330ml", "BEV-006", 0, 0.90m, 1.79m, 150),
            ("Energy Drink 250ml", "BEV-007", 0, 1.00m, 2.29m, 90),
            ("Potato Chips Classic", "SNK-001", 1, 0.90m, 1.99m, 250),
            ("Tortilla Chips", "SNK-002", 1, 1.10m, 2.49m, 180),
            ("Chocolate Bar", "SNK-003", 1, 0.60m, 1.29m, 300),
            ("Trail Mix 200g", "SNK-004", 1, 1.50m, 3.49m, 100),
            ("Pretzel Sticks", "SNK-005", 1, 0.80m, 1.99m, 120),
            ("Popcorn Butter", "SNK-006", 1, 1.00m, 2.29m, 200),
            ("Whole Milk 1L", "DRY-001", 2, 0.80m, 1.49m, 150),
            ("Cheddar Cheese 200g", "DRY-002", 2, 1.50m, 3.29m, 80),
            ("Greek Yogurt 500g", "DRY-003", 2, 1.20m, 2.79m, 100),
            ("Butter 250g", "DRY-004", 2, 1.30m, 2.99m, 90),
            ("Cream Cheese 200g", "DRY-005", 2, 1.10m, 2.49m, 70),
            ("Sourdough Bread", "BKR-001", 3, 1.50m, 3.49m, 60),
            ("Croissant", "BKR-002", 3, 0.80m, 1.99m, 100),
            ("Blueberry Muffin", "BKR-003", 3, 0.70m, 1.79m, 80),
            ("Baguette", "BKR-004", 3, 1.00m, 2.29m, 50),
            ("Cinnamon Roll", "BKR-005", 3, 0.90m, 2.19m, 70),
            ("Bananas 1kg", "FRV-001", 4, 0.80m, 1.49m, 200),
            ("Apples 1kg", "FRV-002", 4, 1.20m, 2.49m, 150),
            ("Tomatoes 500g", "FRV-003", 4, 0.60m, 1.29m, 180),
            ("Baby Spinach 250g", "FRV-004", 4, 1.00m, 2.49m, 100),
            ("Avocado", "FRV-005", 4, 0.70m, 1.99m, 120),
            ("Frozen Pizza", "FRZ-001", 5, 2.00m, 4.99m, 80),
            ("Ice Cream 500ml", "FRZ-002", 5, 1.50m, 3.99m, 100),
            ("Frozen Fries 1kg", "FRZ-003", 5, 1.00m, 2.79m, 120),
            ("Fish Fingers 400g", "FRZ-004", 5, 1.80m, 3.99m, 60),
            ("Shampoo 400ml", "PER-001", 6, 2.00m, 4.99m, 80),
            ("Body Wash 500ml", "PER-002", 6, 1.80m, 4.49m, 90),
            ("Toothpaste 100g", "PER-003", 6, 0.80m, 1.99m, 200),
            ("Deodorant Spray", "PER-004", 6, 1.50m, 3.49m, 100),
            ("Dish Soap 750ml", "HOU-001", 7, 1.00m, 2.49m, 150),
            ("Paper Towels 6pk", "HOU-002", 7, 2.00m, 4.99m, 80),
            ("Trash Bags 30ct", "HOU-003", 7, 1.50m, 3.49m, 100),
            ("All-Purpose Cleaner", "HOU-004", 7, 1.20m, 2.99m, 120),
        };

        var products = new List<Product>();
        foreach (var (name, sku, catIdx, cost, price, stock) in productData)
        {
            var cat = categories[catIdx];
            var lower = name.ToLower();
            string unit = "piece";
            if (lower.Contains("500ml") || lower.Contains("400ml") || lower.Contains("330ml") || lower.Contains("250ml") || lower.Contains("750ml"))
                unit = "mL";
            else if (lower.Contains("1l") || lower.Contains("juice"))
                unit = "lit";
            else if (lower.Contains("1kg") || lower.Contains("2kg"))
                unit = "kg";
            else if (lower.Contains("200g") || lower.Contains("250g") || lower.Contains("500g") || lower.Contains("100g"))
                unit = "gm";
            else if (lower.Contains("chips") || lower.Contains("popcorn"))
                unit = "packet";
            else if (lower.Contains("fingers") || lower.Contains("pizza"))
                unit = "box";
            else if (lower.Contains("6pk"))
                unit = "pack of 5";
            else if (lower.Contains("30ct"))
                unit = "pack of 30";

            products.Add(new Product
            {
                Name = name,
                SKU = sku,
                Barcode = $"490{rng.Next(1000000, 9999999)}",
                CategoryId = cat.Id,
                SupplierId = cat.SupplierId,
                Unit = unit,
                CostPrice = cost,
                SellingPrice = price,
                StockQuantity = stock,
                ReorderLevel = 15,
                TaxRate = 0.0m
            });
        }
        db.Products.AddRange(products);
        db.SaveChanges();

        // --- Customers ---
        var customers = new[]
        {
            new Customer { Code = "CUST-001", Name = "Alice Johnson", Phone = "+1-555-0101", Email = "alice@example.com", LoyaltyPoints = 150, StoreCredit = 250.00m },
            new Customer { Code = "CUST-002", Name = "Bob Williams", Phone = "+1-555-0102", Email = "bob@example.com", LoyaltyPoints = 320, StoreCredit = 120.50m },
            new Customer { Code = "CUST-003", Name = "Carol Davis", Phone = "+1-555-0103", Email = "carol@example.com", LoyaltyPoints = 85, StoreCredit = 0.00m },
            new Customer { Code = "CUST-004", Name = "Dave Martinez", Phone = "+1-555-0104", Email = "dave@example.com", LoyaltyPoints = 500, StoreCredit = 750.00m },
            new Customer { Code = "CUST-005", Name = "Eve Thompson", Phone = "+1-555-0105", Email = "eve@example.com", LoyaltyPoints = 210, StoreCredit = 50.00m },
        };
        db.Customers.AddRange(customers);
        db.SaveChanges();

        // --- Historical Orders (for dashboard data) ---
        var now = DateTime.UtcNow;
        var invoiceCounter = 1;

        for (int daysAgo = 6; daysAgo >= 0; daysAgo--)
        {
            int ordersPerDay = rng.Next(3, 8);
            for (int i = 0; i < ordersPerDay; i++)
            {
                var orderDate = now.AddDays(-daysAgo).Date.AddHours(rng.Next(8, 20)).AddMinutes(rng.Next(0, 60));
                var selectedProducts = products.OrderBy(_ => rng.Next()).Take(rng.Next(1, 5)).ToList();

                var items = selectedProducts.Select(p => new OrderItem
                {
                    ProductId = p.Id,
                    SKU = p.SKU,
                    ProductName = p.Name,
                    UnitPrice = p.SellingPrice,
                    UnitCost = p.CostPrice,
                    Quantity = rng.Next(1, 4),
                    TaxRate = p.TaxRate,
                    DiscountAmount = 0
                }).ToList();

                decimal subTotal = items.Sum(it => it.UnitPrice * it.Quantity);
                decimal grandTotal = subTotal;

                var order = new Order
                {
                    InvoiceNumber = $"INV-{invoiceCounter++:D6}",
                    CashierId = rng.Next(0, 2) == 0 ? cashier1.Id : cashier2.Id,
                    CustomerId = rng.Next(0, 3) == 0 ? customers[rng.Next(customers.Length)].Id : null,
                    SubTotal = subTotal,
                    TaxTotal = 0.00m,
                    DiscountTotal = 0,
                    GrandTotal = grandTotal,
                    PaymentStatus = PaymentStatus.Paid,
                    OrderStatus = OrderStatus.Completed,
                    CreatedAt = orderDate,
                    CompletedAt = orderDate.AddMinutes(rng.Next(1, 5)),
                };

                db.Orders.Add(order);
                db.SaveChanges();

                foreach (var item in items)
                {
                    item.OrderId = order.Id;
                }
                db.OrderItems.AddRange(items);

                db.Payments.Add(new Payment
                {
                    OrderId = order.Id,
                    PaymentMethod = (PaymentMethod)rng.Next(0, 3),
                    AmountPaid = grandTotal,
                    ChangeGiven = 0,
                    CreatedAt = orderDate
                });

                db.SaveChanges();
            }
        }

        // --- Settings ---
        db.Settings.AddRange(
            new Setting { Key = "StoreName", Value = "Govindarajan store" },
            new Setting { Key = "StoreAddress", Value = "bazzar street sirkali" },
            new Setting { Key = "StorePhone", Value = "9487848939" },
            new Setting { Key = "TaxRate", Value = "0.00" },
            new Setting { Key = "CurrencySymbol", Value = "₹" },
            new Setting { Key = "ReceiptFooter", Value = "Thank you for shopping at Govindarajan store! Please visit again." }
        );
        db.SaveChanges();

        // --- Suppliers / Distributors ---
        if (!db.Suppliers.Any())
        {
            var s1 = new Supplier
            {
                Name = "Sirkali Agro Traders",
                ContactPerson = "Ramesh Kumar",
                Phone = "9443210987",
                Email = "sirkaliagro@gmail.com",
                Address = "12 Railway Feeder Road, Sirkali",
                HandledProducts = "Rice, Grains, Sugar, Dhal, Flours, Edible Oils",
                Notes = "Primary wholesale grocery and grain supplier."
            };
            var s2 = new Supplier
            {
                Name = "Cauvery FMCG Distributors",
                ContactPerson = "M. Venkatesh",
                Phone = "9842156789",
                Email = "cauveryfmcg@outlook.com",
                Address = "45 Car Street, Mayiladuthurai",
                HandledProducts = "Soaps, Detergents, Toothpaste, Biscuits, Chocolates, Packaged Snacks",
                Notes = "Weekly delivery on Tuesdays and Fridays."
            };
            var s3 = new Supplier
            {
                Name = "Delta Dairy & Beverages",
                ContactPerson = "S. Swaminathan",
                Phone = "9751432100",
                Email = "deltadairy@gmail.com",
                Address = "8 Bazaar Street, Chidambaram",
                HandledProducts = "Milk, Ghee, Paneer, Curd, Butter, Mineral Water, Soft Drinks",
                Notes = "Daily morning supply of fresh dairy products."
            };

            db.Suppliers.AddRange(s1, s2, s3);
            db.SaveChanges();

            // Sample Purchase Order
            var sampleProduct = db.Products.FirstOrDefault();
            if (sampleProduct != null)
            {
                var po = new PurchaseOrder
                {
                    PONumber = "PO-2026-001",
                    SupplierId = s1.Id,
                    TotalAmount = 4500.00m,
                    Status = PurchaseOrderStatus.Received,
                    Notes = "Bulk monthly grocery inventory restock",
                    CreatedAt = DateTime.UtcNow.AddDays(-3),
                    ReceivedAt = DateTime.UtcNow.AddDays(-2)
                };
                db.PurchaseOrders.Add(po);
                db.SaveChanges();

                db.PurchaseOrderItems.Add(new PurchaseOrderItem
                {
                    PurchaseOrderId = po.Id,
                    ProductId = sampleProduct.Id,
                    ProductName = sampleProduct.Name,
                    Quantity = 50,
                    UnitCost = sampleProduct.CostPrice
                });
                db.SaveChanges();
            }
        }
    }

    private static void EnsureSchemaAndDistributorLinks(ApexDbContext db)
    {
        // 1. Raw Schema Migrations (Executed FIRST before ANY Entity Framework model queries)
        try { db.Database.ExecuteSqlRaw("ALTER TABLE Products ADD COLUMN Description TEXT DEFAULT '';"); } catch { }
        try { db.Database.ExecuteSqlRaw("ALTER TABLE Products ADD COLUMN ReorderLevel INTEGER DEFAULT 10;"); } catch { }
        try { db.Database.ExecuteSqlRaw("ALTER TABLE Products ADD COLUMN Unit TEXT DEFAULT 'piece';"); } catch { }
        try { db.Database.ExecuteSqlRaw("ALTER TABLE Products ADD COLUMN SupplierId INTEGER;"); } catch { }
        try { db.Database.ExecuteSqlRaw("ALTER TABLE Products ADD COLUMN MRP TEXT DEFAULT '0.00';"); } catch { }
        try { db.Database.ExecuteSqlRaw("ALTER TABLE Categories ADD COLUMN SupplierId INTEGER;"); } catch { }
        try { db.Database.ExecuteSqlRaw("ALTER TABLE Categories ADD COLUMN SortOrder INTEGER DEFAULT 0;"); } catch { }
        try { db.Database.ExecuteSqlRaw("ALTER TABLE Users ADD COLUMN LoyaltyPoints TEXT DEFAULT '0.00';"); } catch { }
        try { db.Database.ExecuteSqlRaw("ALTER TABLE Orders ADD COLUMN SalesPersonId INTEGER;"); } catch { }
        try { db.Database.ExecuteSqlRaw("ALTER TABLE Orders ADD COLUMN SalesPersonName TEXT;"); } catch { }
        try { db.Database.ExecuteSqlRaw("ALTER TABLE Orders ADD COLUMN SalesPersonLoyaltyPointsEarned TEXT DEFAULT '0.00';"); } catch { }

        // Sanitize existing NULL values for newly added non-nullable columns
        try { db.Database.ExecuteSqlRaw("UPDATE Products SET Unit = 'piece' WHERE Unit IS NULL;"); } catch { }
        try { db.Database.ExecuteSqlRaw("UPDATE Products SET Description = '' WHERE Description IS NULL;"); } catch { }
        try { db.Database.ExecuteSqlRaw("UPDATE Products SET Barcode = '' WHERE Barcode IS NULL;"); } catch { }
        try { db.Database.ExecuteSqlRaw("UPDATE Products SET ReorderLevel = 10 WHERE ReorderLevel IS NULL;"); } catch { }
        try { db.Database.ExecuteSqlRaw("UPDATE Categories SET Icon = '📦' WHERE Icon IS NULL;"); } catch { }
        try { db.Database.ExecuteSqlRaw("UPDATE Categories SET SortOrder = 0 WHERE SortOrder IS NULL;"); } catch { }
        try { db.Database.ExecuteSqlRaw("UPDATE Users SET LoyaltyPoints = '0.00' WHERE LoyaltyPoints IS NULL;"); } catch { }
        try { db.Database.ExecuteSqlRaw("UPDATE Orders SET SalesPersonLoyaltyPointsEarned = '0.00' WHERE SalesPersonLoyaltyPointsEarned IS NULL;"); } catch { }

        // Ensure PurchaseRateLogs table exists
        try
        {
            db.Database.ExecuteSqlRaw(@"
                CREATE TABLE IF NOT EXISTS PurchaseRateLogs (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    ProductId INTEGER NOT NULL,
                    ProductName TEXT NOT NULL,
                    SKU TEXT,
                    Barcode TEXT,
                    OldCostPrice TEXT NOT NULL,
                    NewCostPrice TEXT NOT NULL,
                    SellingPrice TEXT NOT NULL,
                    MRP TEXT DEFAULT '0.00',
                    SupplierId INTEGER,
                    SupplierName TEXT,
                    Source TEXT,
                    Notes TEXT,
                    EffectiveDate TEXT NOT NULL,
                    CreatedAt TEXT NOT NULL,
                    UserId INTEGER,
                    UserName TEXT,
                    FOREIGN KEY (ProductId) REFERENCES Products(Id) ON DELETE CASCADE,
                    FOREIGN KEY (SupplierId) REFERENCES Suppliers(Id) ON DELETE SET NULL
                );
                CREATE INDEX IF NOT EXISTS IX_PurchaseRateLogs_ProductId ON PurchaseRateLogs(ProductId);
                CREATE INDEX IF NOT EXISTS IX_PurchaseRateLogs_EffectiveDate ON PurchaseRateLogs(EffectiveDate);
            ");
        }
        catch { }

        try { db.Database.ExecuteSqlRaw("ALTER TABLE PurchaseRateLogs ADD COLUMN MRP TEXT DEFAULT '0.00';"); } catch { }
        try { db.Database.ExecuteSqlRaw("UPDATE Products SET MRP = SellingPrice WHERE MRP IS NULL OR MRP = '0' OR MRP = '0.00';"); } catch { }
        try { db.Database.ExecuteSqlRaw("UPDATE PurchaseRateLogs SET MRP = SellingPrice WHERE MRP IS NULL OR MRP = '0' OR MRP = '0.00';"); } catch { }

        // Ensure Suppliers exist
        if (!db.Suppliers.Any())
        {
            var s1 = new Supplier
            {
                Name = "Sirkali Agro Traders",
                ContactPerson = "Ramesh Kumar",
                Phone = "9443210987",
                Email = "sirkaliagro@gmail.com",
                Address = "12 Railway Feeder Road, Sirkali",
                HandledProducts = "Rice, Grains, Sugar, Dhal, Flours, Edible Oils, Fruits & Vegetables, Frozen Foods",
                Notes = "Primary wholesale grocery, fresh produce, and grain supplier."
            };
            var s2 = new Supplier
            {
                Name = "Cauvery FMCG Distributors",
                ContactPerson = "M. Venkatesh",
                Phone = "9842156789",
                Email = "cauveryfmcg@outlook.com",
                Address = "45 Car Street, Mayiladuthurai",
                HandledProducts = "Soaps, Detergents, Toothpaste, Biscuits, Chocolates, Packaged Snacks, Bakery, Household",
                Notes = "Weekly delivery on Tuesdays and Fridays."
            };
            var s3 = new Supplier
            {
                Name = "Delta Dairy & Beverages",
                ContactPerson = "S. Swaminathan",
                Phone = "9751432100",
                Email = "deltadairy@gmail.com",
                Address = "8 Bazaar Street, Chidambaram",
                HandledProducts = "Milk, Ghee, Paneer, Curd, Butter, Mineral Water, Soft Drinks, Juices",
                Notes = "Daily morning supply of fresh dairy and beverage products."
            };
            db.Suppliers.AddRange(s1, s2, s3);
            db.SaveChanges();
        }

        var agro = db.Suppliers.FirstOrDefault(s => s.Name.Contains("Agro"));
        var fmcg = db.Suppliers.FirstOrDefault(s => s.Name.Contains("FMCG"));
        var dairy = db.Suppliers.FirstOrDefault(s => s.Name.Contains("Dairy"));

        // Link Categories to Distributors if not linked
        var categories = db.Categories.ToList();
        foreach (var cat in categories)
        {
            if (cat.SupplierId == null)
            {
                if (cat.Name == "Beverages" || cat.Name == "Dairy")
                    cat.SupplierId = dairy?.Id;
                else if (cat.Name == "Fruits & Vegetables" || cat.Name == "Frozen Foods")
                    cat.SupplierId = agro?.Id;
                else
                    cat.SupplierId = fmcg?.Id;
            }
        }
        db.SaveChanges();

        // Update Products with Units and SupplierId if not linked or empty
        var products = db.Products.Include(p => p.Category).ToList();
        foreach (var p in products)
        {
            if (p.SupplierId == null && p.Category != null)
            {
                p.SupplierId = p.Category.SupplierId;
            }

            if (string.IsNullOrWhiteSpace(p.Unit) || p.Unit == "piece")
            {
                var lower = p.Name.ToLower();
                if (lower.Contains("500ml") || lower.Contains("400ml") || lower.Contains("330ml") || lower.Contains("250ml") || lower.Contains("750ml"))
                    p.Unit = "mL";
                else if (lower.Contains("1l") || lower.Contains("juice"))
                    p.Unit = "lit";
                else if (lower.Contains("1kg") || lower.Contains("2kg"))
                    p.Unit = "kg";
                else if (lower.Contains("200g") || lower.Contains("250g") || lower.Contains("500g") || lower.Contains("100g"))
                    p.Unit = "gm";
                else if (lower.Contains("chips") || lower.Contains("popcorn"))
                    p.Unit = "packet";
                else if (lower.Contains("fingers") || lower.Contains("pizza"))
                    p.Unit = "box";
                else if (lower.Contains("6pk"))
                    p.Unit = "pack of 5";
                else if (lower.Contains("30ct"))
                    p.Unit = "pack of 30";
                else
                    p.Unit = "piece";
            }
        }
        db.SaveChanges();

        // Ensure Customers have sample store credits
        var sampleCredits = new Dictionary<string, decimal>
        {
            { "Alice Johnson", 250.00m },
            { "Bob Williams", 120.50m },
            { "Dave Martinez", 750.00m },
            { "Eve Thompson", 50.00m },
            { "karupu", 150.00m },
            { "VINO", 300.00m }
        };
        var existingCusts = db.Customers.ToList();
        bool custUpdated = false;
        foreach (var cust in existingCusts)
        {
            if (cust.StoreCredit == 0 && sampleCredits.TryGetValue(cust.Name, out var credit))
            {
                cust.StoreCredit = credit;
                custUpdated = true;
            }
        }
        if (custUpdated)
        {
            db.SaveChanges();
        }

        // Ensure a sample SalesPerson user exists for existing databases
        try
        {
            if (db.Users.Any() && !db.Users.Any(u => u.Role == UserRole.SalesPerson))
            {
                var salesPerson = new User
                {
                    Username = "sales1",
                    PasswordHash = HashPassword("sales123"),
                    PinCode = "2222",
                    FullName = "Ravi Kumar (Sales Person)",
                    Role = UserRole.SalesPerson,
                    LoyaltyPoints = 50.0m,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };
                db.Users.Add(salesPerson);
                db.SaveChanges();
            }
        }
        catch { }

        // Seed initial purchase rate logs if empty
        try
        {
            if (!db.PurchaseRateLogs.Any())
            {
                var productsList = db.Products.Include(p => p.Supplier).Take(25).ToList();
                var now = DateTime.UtcNow;
                var rand = new Random(42);

                foreach (var prod in productsList)
                {
                    decimal pastCost = Math.Round(prod.CostPrice > 1.5m ? prod.CostPrice * 0.90m : prod.CostPrice, 2);
                    db.PurchaseRateLogs.Add(new PurchaseRateLog
                    {
                        ProductId = prod.Id,
                        ProductName = prod.Name,
                        SKU = prod.SKU,
                        Barcode = prod.Barcode,
                        OldCostPrice = pastCost > 0.5m ? pastCost - 0.20m : pastCost,
                        NewCostPrice = pastCost,
                        SellingPrice = prod.SellingPrice,
                        SupplierId = prod.SupplierId,
                        SupplierName = prod.Supplier?.Name ?? "Main Distributor",
                        Source = "Initial Procurement",
                        Notes = "Base initial supplier contract rate",
                        EffectiveDate = now.AddDays(-60),
                        CreatedAt = now.AddDays(-60),
                        UserId = 1,
                        UserName = "admin"
                    });

                    if (prod.CostPrice != pastCost)
                    {
                        db.PurchaseRateLogs.Add(new PurchaseRateLog
                        {
                            ProductId = prod.Id,
                            ProductName = prod.Name,
                            SKU = prod.SKU,
                            Barcode = prod.Barcode,
                            OldCostPrice = pastCost,
                            NewCostPrice = prod.CostPrice,
                            SellingPrice = prod.SellingPrice,
                            SupplierId = prod.SupplierId,
                            SupplierName = prod.Supplier?.Name ?? "Main Distributor",
                            Source = "Supplier Price Revision",
                            Notes = $"Market rate revision (+₹{prod.CostPrice - pastCost:F2})",
                            EffectiveDate = now.AddDays(-rand.Next(5, 25)),
                            CreatedAt = now.AddDays(-rand.Next(5, 25)),
                            UserId = 1,
                            UserName = "admin"
                        });
                    }
                }
                db.SaveChanges();
            }
        }
        catch { }
    }

    public static string HashPassword(string password)
    {
        byte[] salt = RandomNumberGenerator.GetBytes(16);
        byte[] hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, 100_000, HashAlgorithmName.SHA256, 32);
        return $"{Convert.ToBase64String(salt)}:{Convert.ToBase64String(hash)}";
    }

    public static bool VerifyPassword(string password, string storedHash)
    {
        var parts = storedHash.Split(':');
        if (parts.Length != 2) return false;
        byte[] salt = Convert.FromBase64String(parts[0]);
        byte[] expectedHash = Convert.FromBase64String(parts[1]);
        byte[] actualHash = Rfc2898DeriveBytes.Pbkdf2(password, salt, 100_000, HashAlgorithmName.SHA256, 32);
        return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
    }
}
