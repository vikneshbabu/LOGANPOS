using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ApexPOS.Data;
using ApexPOS.Domain.Entities;
using ApexPOS.Domain.Enums;
using ApexPOS.Services;
using ApexPOS.Wpf.ViewModels;
using Xunit;

namespace ApexPOS.Tests;

public class PosBusinessLogicTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ApexDbContext _db;

    public PosBusinessLogicTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<ApexDbContext>()
            .UseSqlite(_connection)
            .Options;

        _db = new ApexDbContext(options);
        _db.Database.EnsureCreated();
        DbSeeder.Seed(_db);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public void CartItem_Calculations_AreAccurate()
    {
        // Arrange
        var item = new CartItem
        {
            ProductId = 1,
            Sku = "TEST-SKU",
            ProductName = "Espresso",
            UnitPrice = 4.50m,
            UnitCost = 1.20m,
            Quantity = 2,
            TaxRate = 0.10m, // 10% tax
            DiscountAmount = 1.00m // $1 line discount
        };

        // Act & Assert
        // LineSubtotal = 4.50 * 2 = 9.00
        Assert.Equal(9.00m, item.LineSubtotal);

        // Taxable = 9.00 - 1.00 = 8.00. Tax = 0.00 (Tax removed)
        Assert.Equal(0.00m, item.LineTax);

        // LineTotal = 9.00 - 1.00 = 8.00
        Assert.Equal(8.00m, item.LineTotal);
    }

    [Fact]
    public void CartItem_ManualQuantityChange_UpdatesTotalsAccurately()
    {
        // Arrange
        var item = new CartItem
        {
            ProductId = 1,
            UnitPrice = 10.00m,
            TaxRate = 0.10m,
            Quantity = 1
        };
        Assert.Equal(10.00m, item.LineSubtotal);
        Assert.Equal(10.00m, item.LineTotal);

        // Act - Cashier manually types 5 into quantity field
        item.Quantity = 5;

        // Assert
        Assert.Equal(50.00m, item.LineSubtotal);
        Assert.Equal(0.00m, item.LineTax);
        Assert.Equal(50.00m, item.LineTotal);

        // Act - Cashier accidentally types 0 or negative
        item.Quantity = 0;
        Assert.Equal(1, item.Quantity); // Clamped back to 1
        Assert.Equal(10.00m, item.LineSubtotal);
    }

    [Fact]
    public void RegisterViewModel_ManualQuantityChange_RecalculatesCartTotals()
    {
        // Arrange
        var pService = new ProductService(_db);
        var cService = new CategoryService(_db);
        var oService = new OrderService(_db);
        var custService = new CustomerService(_db);
        var rService = new ReceiptService();
        var vm = new RegisterViewModel(pService, cService, oService, custService, rService);

        var product = _db.Products.First();
        vm.AddToCart(product);
        Assert.Single(vm.CartItems);
        Assert.Equal(1, vm.CartItemCount);
        decimal singleTotal = vm.CartGrandTotal;

        // Act - Cashier manually edits quantity to 4
        vm.CartItems[0].Quantity = 4;

        // Assert
        Assert.Equal(4, vm.CartItemCount);
        Assert.Equal(singleTotal * 4, vm.CartGrandTotal);
    }

    [Fact]
    public void DistributorBasedCategory_And_CategoryBasedProduct_Associations_Work()
    {
        // Arrange
        var pService = new ProductService(_db);
        var cService = new CategoryService(_db);

        // Act
        var categories = cService.GetAll();
        var products = pService.GetAll();

        // Assert
        Assert.NotEmpty(categories);
        Assert.All(categories, c => Assert.NotNull(c.SupplierId)); // Categories are distributor-based

        Assert.NotEmpty(products);
        Assert.All(products, p =>
        {
            Assert.False(string.IsNullOrWhiteSpace(p.Unit)); // Unit is populated (e.g. piece, mL, kg, packet)
            Assert.False(string.IsNullOrWhiteSpace(p.DistributorName));
        });

        // Test filtering category by distributor
        int firstSupplierId = categories.First().SupplierId!.Value;
        var supplierCats = cService.GetBySupplier(firstSupplierId);
        Assert.NotEmpty(supplierCats);
        Assert.All(supplierCats, c => Assert.Equal(firstSupplierId, c.SupplierId));

        // Test filtering product by distributor
        var supplierProducts = pService.GetBySupplier(firstSupplierId);
        Assert.NotEmpty(supplierProducts);
    }

    [Fact]
    public void InventoryViewModel_DistributorAndCategoryCascading_FiltersAccurately()
    {
        // Arrange
        var pService = new ProductService(_db);
        var cService = new CategoryService(_db);
        var purService = new PurchaseService(_db);
        var vm = new InventoryViewModel(pService, cService, purService);

        // Act
        vm.LoadData();
        Assert.NotEmpty(vm.Suppliers);
        Assert.NotEmpty(vm.AvailableUnits);
        Assert.Contains("pack of 12", vm.AvailableUnits);
        Assert.Contains("kg", vm.AvailableUnits);
        Assert.Contains("piece", vm.AvailableUnits);

        int totalCount = vm.Products.Count;

        // Select a distributor filter
        var supplier = vm.Suppliers.First();
        vm.FilterSupplierId = supplier.Id;

        // Assert categories are cascaded
        Assert.NotEmpty(vm.FilteredListCategories);
        Assert.All(vm.FilteredListCategories, c => Assert.Equal(supplier.Id, c.SupplierId));

        // Assert products are filtered
        Assert.NotEmpty(vm.Products);
        Assert.True(vm.Products.Count <= totalCount);
    }

    [Fact]
    public void OrderService_CreateOrder_CalculatesTotalsAndDeductsStock()
    {
        // Arrange
        var orderService = new OrderService(_db);
        var product = _db.Products.First(p => p.Id == 1);
        int initialStock = product.StockQuantity;
        int qtyToBuy = 3;

        var items = new List<OrderItem>
        {
            new OrderItem
            {
                ProductId = product.Id,
                SKU = product.SKU,
                ProductName = product.Name,
                UnitPrice = product.SellingPrice,
                UnitCost = product.CostPrice,
                Quantity = qtyToBuy,
                TaxRate = product.TaxRate,
                DiscountAmount = 0m
            }
        };

        decimal expectedSubtotal = product.SellingPrice * qtyToBuy;
        decimal expectedTax = 0.00m;
        decimal expectedGrandTotal = expectedSubtotal;

        // Act
        var order = orderService.CreateOrder(cashierId: 1, customerId: null, items, discountTotal: 0m);

        // Assert
        Assert.NotNull(order);
        Assert.StartsWith("INV-", order.InvoiceNumber);
        Assert.Equal(expectedSubtotal, order.SubTotal);
        Assert.Equal(expectedTax, order.TaxTotal);
        Assert.Equal(expectedGrandTotal, order.GrandTotal);
        Assert.Equal(OrderStatus.Active, order.OrderStatus);
        Assert.Equal(PaymentStatus.Pending, order.PaymentStatus);

        // Stock deduction check
        var updatedProduct = _db.Products.Find(product.Id);
        Assert.NotNull(updatedProduct);
        Assert.Equal(initialStock - qtyToBuy, updatedProduct.StockQuantity);
    }

    [Fact]
    public void OrderService_CompletePayment_CalculatesChangeAndCompletesOrder()
    {
        // Arrange
        var orderService = new OrderService(_db);
        var product = _db.Products.First();
        var items = new List<OrderItem>
        {
            new OrderItem
            {
                ProductId = product.Id,
                SKU = product.SKU,
                ProductName = product.Name,
                UnitPrice = 20.00m,
                UnitCost = 10.00m,
                Quantity = 1,
                TaxRate = 0.10m,
                DiscountAmount = 0m
            }
        };

        // GrandTotal = 20.00 (Tax removed)
        var order = orderService.CreateOrder(1, null, items, 0m);
        decimal amountTendered = 30.00m;
        decimal expectedChange = 10.00m;

        // Act
        orderService.CompletePayment(order.Id, PaymentMethod.Cash, amountTendered);

        // Assert
        var updatedOrder = _db.Orders.Include(o => o.Payments).First(o => o.Id == order.Id);
        Assert.Equal(OrderStatus.Completed, updatedOrder.OrderStatus);
        Assert.Equal(PaymentStatus.Paid, updatedOrder.PaymentStatus);
        Assert.Single(updatedOrder.Payments);
        Assert.Equal(expectedChange, updatedOrder.Payments.First().ChangeGiven);
    }

    [Fact]
    public void OrderService_HoldAndResume_UpdatesStatusAccurately()
    {
        // Arrange
        var orderService = new OrderService(_db);
        var product = _db.Products.First();
        var items = new List<OrderItem>
        {
            new OrderItem
            {
                ProductId = product.Id,
                SKU = product.SKU,
                ProductName = product.Name,
                UnitPrice = product.SellingPrice,
                UnitCost = product.CostPrice,
                Quantity = 1,
                TaxRate = product.TaxRate,
                DiscountAmount = 0m
            }
        };

        var order = orderService.CreateOrder(1, null, items, 0m);

        // Act 1: Hold
        orderService.HoldOrder(order.Id, "Customer forgot wallet");
        var heldOrder = _db.Orders.Find(order.Id);
        Assert.NotNull(heldOrder);
        Assert.Equal(OrderStatus.OnHold, heldOrder.OrderStatus);
        Assert.Equal("Customer forgot wallet", heldOrder.HoldNotes);

        // Act 2: Resume
        orderService.ResumeOrder(order.Id);
        var resumedOrder = _db.Orders.Find(order.Id);
        Assert.NotNull(resumedOrder);
        Assert.Equal(OrderStatus.Active, resumedOrder.OrderStatus);
        Assert.Null(resumedOrder.HoldNotes);
    }

    [Fact]
    public void OrderService_VoidOrder_RestoresInventoryStock()
    {
        // Arrange
        var orderService = new OrderService(_db);
        var product = _db.Products.First(p => p.Id == 1);
        int initialStock = product.StockQuantity;
        int purchaseQty = 4;

        var items = new List<OrderItem>
        {
            new OrderItem
            {
                ProductId = product.Id,
                SKU = product.SKU,
                ProductName = product.Name,
                UnitPrice = product.SellingPrice,
                UnitCost = product.CostPrice,
                Quantity = purchaseQty,
                TaxRate = product.TaxRate,
                DiscountAmount = 0m
            }
        };

        var order = orderService.CreateOrder(1, null, items, 0m);
        Assert.Equal(initialStock - purchaseQty, _db.Products.Find(product.Id)!.StockQuantity);

        // Act: Void
        orderService.VoidOrder(order.Id, userId: 1);

        // Assert
        var voidedOrder = _db.Orders.Find(order.Id);
        Assert.NotNull(voidedOrder);
        Assert.Equal(OrderStatus.Voided, voidedOrder.OrderStatus);

        // Stock must be restored to initial
        var restoredProduct = _db.Products.Find(product.Id);
        Assert.NotNull(restoredProduct);
        Assert.Equal(initialStock, restoredProduct.StockQuantity);
    }

    [Fact]
    public void AuthService_LoginWithPasswordAndPin_Succeeds()
    {
        // Arrange
        var authService = new AuthService(_db);

        // Act & Assert Admin Password
        var adminUser = authService.Login("admin", "admin123");
        Assert.NotNull(adminUser);
        Assert.Equal("System Administrator", adminUser.FullName);

        // Invalid Password
        var failUser = authService.Login("admin", "wrongpassword");
        Assert.Null(failUser);

        // PIN Login (Cashier 1 PIN is 1234 in seeds)
        var cashier = authService.LoginByPin("1234");
        Assert.NotNull(cashier);
        Assert.Equal("cashier1", cashier.Username);

        // Invalid PIN
        var invalidPinUser = authService.LoginByPin("9999");
        Assert.Null(invalidPinUser);
    }

    [Fact]
    public void ReceiptService_GeneratesFormattedThermalReceipt()
    {
        // Arrange
        var receiptService = new ReceiptService();
        var order = new Order
        {
            InvoiceNumber = "INV-000123",
            CashierId = 1,
            Cashier = new User { Id = 1, FullName = "Sarah Connor" },
            Customer = new Customer { Name = "John Doe", Phone = "555-1234" },
            SubTotal = 50.00m,
            TaxTotal = 5.00m,
            DiscountTotal = 2.00m,
            GrandTotal = 53.00m,
            CreatedAt = new DateTime(2026, 9, 15, 14, 30, 0),
            Items = new List<OrderItem>
            {
                new OrderItem
                {
                    ProductName = "Organic Coffee Beans",
                    Quantity = 2,
                    UnitPrice = 25.00m,
                    DiscountAmount = 2.00m
                }
            },
            Payments = new List<Payment>
            {
                new Payment
                {
                    PaymentMethod = PaymentMethod.Cash,
                    AmountPaid = 60.00m,
                    ChangeGiven = 7.00m
                }
            }
        };

        // Act
        string receipt = receiptService.GenerateReceiptText(order);

        // Assert
        Assert.Contains("Govindarajan store", receipt);
        Assert.Contains("bazzar street sirkali", receipt);
        Assert.Contains("9487848939", receipt);
        Assert.Contains("INV-000123", receipt);
        Assert.Contains("Sarah Connor", receipt);
        Assert.Contains("John Doe", receipt);
        Assert.Contains("Organic Coffee Beans", receipt);
        Assert.Contains("₹53.00", receipt); // Grand total
        Assert.Contains("Change Given:", receipt);
        Assert.Contains("₹7.00", receipt);
    }

    [Fact]
    public void SettingsViewModel_DefaultsToStoreManager_WithAllTogglesOn()
    {
        // Arrange
        var settingsService = new SettingsService(_db);
        var vm = new ApexPOS.Wpf.ViewModels.SettingsViewModel(settingsService);

        // Assert
        Assert.Equal("Store Manager", vm.SelectedRole);
        Assert.Equal("Role Permissions", vm.ActiveNavItem);
        Assert.Contains("Store Manager", vm.Roles);
        Assert.Contains("Cashier", vm.Roles);
        Assert.Contains("Supervisor", vm.Roles);
        Assert.Contains("Admin", vm.Roles);

        // Transactions Group
        Assert.True(vm.ProcessSales);
        Assert.True(vm.IssueRefunds);
        Assert.True(vm.VoidTransaction);
        Assert.True(vm.ManagerOverride);

        // User Management Group
        Assert.True(vm.AddEditUsers);
        Assert.True(vm.ResetPasswords);
        Assert.True(vm.ChangeRoles);

        // Inventory Group
        Assert.True(vm.AddEditProducts);
        Assert.True(vm.ViewStock);
        Assert.True(vm.AdjustStockLevels);
        Assert.True(vm.PrintBarcodes);

        // Reporting Group
        Assert.True(vm.ViewSalesReports);
        Assert.True(vm.ViewInventoryReports);
        Assert.True(vm.DailySummaryZReport);

        // Settings & Config Group 1 & 2
        Assert.True(vm.EditPosRules1);
        Assert.True(vm.HardwareSetup1);
        Assert.True(vm.ReceiptCustomization1);
        Assert.True(vm.EditPosRules2);
        Assert.True(vm.HardwareSetup2);
        Assert.True(vm.ReceiptCustomization2);
    }

    [Fact]
    public void SettingsViewModel_SwitchingRole_UpdatesToggleMatrix()
    {
        // Arrange
        var settingsService = new SettingsService(_db);
        var vm = new ApexPOS.Wpf.ViewModels.SettingsViewModel(settingsService);

        // Act - Switch to Cashier
        vm.SelectedRole = "Cashier";

        // Assert Cashier restricted permissions
        Assert.True(vm.ProcessSales);
        Assert.False(vm.IssueRefunds);
        Assert.False(vm.VoidTransaction);
        Assert.False(vm.ManagerOverride);
        Assert.False(vm.AddEditUsers);
        Assert.False(vm.ResetPasswords);
        Assert.False(vm.ChangeRoles);
        Assert.False(vm.AddEditProducts);
        Assert.True(vm.ViewStock);
        Assert.False(vm.AdjustStockLevels);
        Assert.True(vm.PrintBarcodes);
        Assert.False(vm.ViewSalesReports);
        Assert.False(vm.ViewInventoryReports);
        Assert.True(vm.DailySummaryZReport);
        Assert.False(vm.EditPosRules1);

        // Act - Switch back to Store Manager
        vm.SelectedRole = "Store Manager";
        Assert.True(vm.ProcessSales);
        Assert.True(vm.IssueRefunds);
        Assert.True(vm.AddEditUsers);
    }

    [Fact]
    public void SettingsViewModel_SavePermissions_ShowsToastNotification()
    {
        // Arrange
        var settingsService = new SettingsService(_db);
        var vm = new ApexPOS.Wpf.ViewModels.SettingsViewModel(settingsService);

        // Act
        vm.SavePermissions();

        // Assert exact toast message
        Assert.True(vm.ShowToast);
        Assert.Equal("✔ Changes saved. API endpoints updated.", vm.ToastMessage);

        // Test manual dismissal
        vm.DismissToast();
        Assert.False(vm.ShowToast);
    }

    [Fact]
    public void SettingsViewModel_CancelPermissions_RevertsUnsavedChanges()
    {
        // Arrange
        var settingsService = new SettingsService(_db);
        var vm = new ApexPOS.Wpf.ViewModels.SettingsViewModel(settingsService);
        Assert.True(vm.ProcessSales);

        // Act - Modify toggle and cancel
        vm.ProcessSales = false;
        Assert.False(vm.ProcessSales);

        vm.CancelPermissions();

        // Assert reverted
        Assert.True(vm.ProcessSales);
    }

    [Fact]
    public void SettingsViewModel_SelectNavItem_UpdatesActiveItem()
    {
        // Arrange
        var settingsService = new SettingsService(_db);
        var vm = new ApexPOS.Wpf.ViewModels.SettingsViewModel(settingsService);

        // Act & Assert
        vm.SelectNavItem("User Profiles");
        Assert.Equal("User Profiles", vm.ActiveNavItem);

        vm.SelectNavItem("Printer Config");
        Assert.Equal("Printer Config", vm.ActiveNavItem);

        vm.SelectNavItem("Role Permissions");
        Assert.Equal("Role Permissions", vm.ActiveNavItem);
    }

    [Fact]
    public void CustomerSelection_ShowsCustomerNameInInputField()
    {
        // Arrange
        var vm = new RegisterViewModel(new ProductService(_db), new CategoryService(_db),
            new OrderService(_db), new CustomerService(_db), new ReceiptService());
        var customer = new Customer { Id = 991, Name = "Johnathan Doe", Phone = "+1-555-1111", Code = "CUST-991", StoreCredit = 0 };

        // Act
        vm.SelectCustomer(customer);

        // Assert
        Assert.NotNull(vm.SelectedCustomer);
        Assert.Equal("Johnathan Doe", vm.CustomerSearchText);
        Assert.False(vm.IsCustomerDropdownOpen);
        Assert.False(vm.ShowCreditDialog);
    }

    [Fact]
    public void CustomerSelection_DoesNotOpenDebtDialogImmediately_OpensOnPayClick()
    {
        // Arrange
        var vm = new RegisterViewModel(new ProductService(_db), new CategoryService(_db),
            new OrderService(_db), new CustomerService(_db), new ReceiptService());
        var customer = new Customer { Id = 992, Name = "Alice Credit", Phone = "+1-555-2222", Code = "CUST-992", StoreCredit = 150.00m };

        // Add an item to cart: $50
        vm.CartItems.Add(new CartItem
        {
            ProductId = 1,
            ProductName = "Coffee Beans",
            UnitPrice = 50.00m,
            Quantity = 1,
            TaxRate = 0
        });

        // Act - Select customer with debt
        vm.SelectCustomer(customer);

        // Assert - selecting customer should NOT open dialog immediately
        Assert.NotNull(vm.SelectedCustomer);
        Assert.Equal("Alice Credit", vm.CustomerSearchText);
        Assert.False(vm.ShowCreditDialog);

        // Act 2 - Clicking Pay button should now open debt dialog
        vm.OpenPayment();
        Assert.True(vm.ShowCreditDialog);
        Assert.False(vm.ShowPaymentPanel); // Waits for debt decision
        Assert.Equal(150.00m, vm.CreditAmountToApply); // Defaults to full past debt
    }

    [Fact]
    public void ApplyDebtToBill_AddsDebtToCartGrandTotal()
    {
        // Arrange
        var vm = new RegisterViewModel(new ProductService(_db), new CategoryService(_db),
            new OrderService(_db), new CustomerService(_db), new ReceiptService());
        var customer = new Customer { Id = 993, Name = "Bob Balance", Phone = "+1-555-3333", Code = "CUST-993", StoreCredit = 80.00m };

        vm.CartItems.Add(new CartItem
        {
            ProductId = 1,
            ProductName = "Wireless Mouse",
            UnitPrice = 100.00m,
            Quantity = 1,
            TaxRate = 0
        });

        vm.SelectCustomer(customer);
        Assert.False(vm.ShowCreditDialog);

        // Click pay to open debt dialog
        vm.OpenPayment();
        Assert.True(vm.ShowCreditDialog);

        // Act - Apply $30 customer debt to bill
        vm.CreditAmountToApply = 30.00m;
        vm.ApplyCreditToBill();

        // Assert
        Assert.False(vm.ShowCreditDialog);
        Assert.True(vm.ShowPaymentPanel); // Proceeds to payment panel
        Assert.Equal(30.00m, vm.AppliedStoreCredit);
        Assert.Equal(130.00m, vm.CartGrandTotal); // $100 items + $30 debt = $130
        Assert.Equal(50.00m, vm.RemainingCustomerCredit); // $80 - $30 = $50
        Assert.Equal(130.00m, vm.PaymentAmount);
    }

    [Fact]
    public void ApplyDebtToBill_Validation_RejectsAmountExceedingCustomerDebt()
    {
        // Arrange
        var vm = new RegisterViewModel(new ProductService(_db), new CategoryService(_db),
            new OrderService(_db), new CustomerService(_db), new ReceiptService());
        var customer = new Customer { Id = 994, Name = "Charlie Check", Phone = "+1-555-4444", Code = "CUST-994", StoreCredit = 50.00m };

        vm.CartItems.Add(new CartItem
        {
            ProductId = 1,
            ProductName = "Keycap Set",
            UnitPrice = 40.00m,
            Quantity = 1,
            TaxRate = 0
        });

        vm.SelectCustomer(customer);
        vm.OpenPayment();
        Assert.True(vm.ShowCreditDialog);

        // Act 1: Try applying more than customer debt ($60 > $50)
        vm.CreditAmountToApply = 60.00m;
        vm.ApplyCreditToBill();
        Assert.True(vm.ShowCreditDialog);
        Assert.Contains("cannot exceed customer debt", vm.CreditDialogErrorMessage);

        // Act 2: Apply valid debt ($40)
        vm.CreditAmountToApply = 40.00m;
        vm.ApplyCreditToBill();
        Assert.False(vm.ShowCreditDialog);
        Assert.Equal(40.00m, vm.AppliedStoreCredit);
        Assert.Equal(80.00m, vm.CartGrandTotal); // $40 items + $40 debt = $80
    }

    [Fact]
    public void ProcessPayment_WithAddedCustomerDebt_CollectsTotalAndDeductsFromCustomerDebt()
    {
        // Arrange
        var customerService = new CustomerService(_db);
        var orderService = new OrderService(_db);
        var customer = new Customer { Name = "Dave Deduction", Phone = "+1-555-5555", Code = "CUST-995", StoreCredit = 100.00m, IsActive = true };
        customerService.Save(customer);

        var vm = new RegisterViewModel(new ProductService(_db), new CategoryService(_db),
            orderService, customerService, new ReceiptService());
        vm.CashierId = 1;

        vm.CartItems.Add(new CartItem
        {
            ProductId = 1,
            Sku = "SKU-001",
            ProductName = "Headphones",
            UnitPrice = 80.00m,
            Quantity = 1,
            TaxRate = 0
        });

        vm.SelectCustomer(customer);
        vm.OpenPayment();
        Assert.True(vm.ShowCreditDialog);

        vm.CreditAmountToApply = 30.00m;
        vm.ApplyCreditToBill();

        Assert.Equal(30.00m, vm.AppliedStoreCredit);
        Assert.Equal(110.00m, vm.CartGrandTotal); // $80 items + $30 debt = $110
        Assert.Equal(110.00m, vm.PaymentAmount);

        // Cashier tenders $110 via Cash
        vm.SelectedPaymentMethod = PaymentMethod.Cash;
        vm.PaymentAmount = 110.00m;

        // Act - Process payment
        vm.ProcessPayment();

        // Assert
        Assert.True(vm.ShowReceiptModal);
        Assert.Empty(vm.CartItems);
        Assert.Null(vm.SelectedCustomer);
        Assert.Equal(0, vm.AppliedStoreCredit);
        Assert.Equal("", vm.CustomerSearchText);

        // Verify customer's debt was reduced by $30 (100 - 30 = 70)
        var updatedCustomer = customerService.GetById(customer.Id);
        Assert.NotNull(updatedCustomer);
        Assert.Equal(70.00m, updatedCustomer.StoreCredit);

        var lastOrder = _db.Orders.Include(o => o.Payments).OrderByDescending(o => o.Id).First();
        Assert.Equal(OrderStatus.Completed, lastOrder.OrderStatus);
        Assert.Equal(PaymentStatus.Paid, lastOrder.PaymentStatus);
        Assert.Equal(110.00m, lastOrder.GrandTotal);
        Assert.Single(lastOrder.Payments);

        var cashPayment = lastOrder.Payments.First();
        Assert.Equal(PaymentMethod.Cash, cashPayment.PaymentMethod);
        Assert.Equal(110.00m, cashPayment.AmountPaid);
    }

    [Fact]
    public void SkipDebt_ProceedsToPaymentWithOnlyItemsTotal()
    {
        // Arrange
        var customerService = new CustomerService(_db);
        var orderService = new OrderService(_db);
        var customer = new Customer { Name = "Eve Entire", Phone = "+1-555-6666", Code = "CUST-996", StoreCredit = 150.00m, IsActive = true };
        customerService.Save(customer);

        var vm = new RegisterViewModel(new ProductService(_db), new CategoryService(_db),
            orderService, customerService, new ReceiptService());
        vm.CashierId = 1;

        vm.CartItems.Add(new CartItem
        {
            ProductId = 1,
            Sku = "SKU-002",
            ProductName = "USB Cable",
            UnitPrice = 25.00m,
            Quantity = 1,
            TaxRate = 0
        });

        vm.SelectCustomer(customer);
        vm.OpenPayment();
        Assert.True(vm.ShowCreditDialog);

        // Act - Skip debt
        vm.CloseCreditDialog();

        // Assert - proceeds to payment without debt
        Assert.False(vm.ShowCreditDialog);
        Assert.True(vm.ShowPaymentPanel);
        Assert.Equal(0.00m, vm.AppliedStoreCredit);
        Assert.Equal(25.00m, vm.CartGrandTotal);
        Assert.Equal(25.00m, vm.PaymentAmount);

        // Process payment
        vm.SelectedPaymentMethod = PaymentMethod.Cash;
        vm.ProcessPayment();

        Assert.True(vm.ShowReceiptModal);
        // Customer debt was NOT changed since debt was skipped
        var updatedCustomer = customerService.GetById(customer.Id);
        Assert.NotNull(updatedCustomer);
        Assert.Equal(150.00m, updatedCustomer.StoreCredit);
    }

    [Fact]
    public void OpenPayment_WithSelectedItems_DisplaysItemsInPaymentDialogAndCalculatesTotalsCorrectly()
    {
        // Arrange
        var customerService = new CustomerService(_db);
        var orderService = new OrderService(_db);
        var vm = new RegisterViewModel(new ProductService(_db), new CategoryService(_db),
            orderService, customerService, new ReceiptService());
        vm.CashierId = 1;

        var item1 = new CartItem
        {
            ProductId = 1,
            Sku = "SKU-001",
            ProductName = "Wireless Mouse",
            UnitPrice = 20.00m,
            Quantity = 2,
            TaxRate = 0.10m // 10% tax: subtotal = 40.00, tax = 4.00, total = 44.00
        };

        var item2 = new CartItem
        {
            ProductId = 2,
            Sku = "SKU-002",
            ProductName = "Keyboard",
            UnitPrice = 50.00m,
            Quantity = 1,
            TaxRate = 0.10m // 10% tax: subtotal = 50.00, tax = 5.00, total = 55.00
        };

        vm.CartItems.Add(item1);
        vm.CartItems.Add(item2);

        // Pre-assertions on cart state
        Assert.Equal(2, vm.CartItems.Count);
        Assert.Equal(3, vm.CartItemCount);
        Assert.Equal(90.00m, vm.CartSubTotal);
        Assert.Equal(0.00m, vm.CartTaxTotal);
        Assert.Equal(90.00m, vm.CartGrandTotal);
        Assert.False(vm.ShowPaymentPanel);

        // Act - Cashier clicks "Pay Now"
        vm.OpenPayment();

        // Assert - Payment dialog is open
        Assert.True(vm.ShowPaymentPanel);

        // Assert - Selected items are available in list for the payment dialog
        Assert.Equal(2, vm.CartItems.Count);
        Assert.Equal("Wireless Mouse", vm.CartItems[0].ProductName);
        Assert.Equal(2, vm.CartItems[0].Quantity);
        Assert.Equal(20.00m, vm.CartItems[0].UnitPrice);
        Assert.Equal(40.00m, vm.CartItems[0].LineTotal);

        Assert.Equal("Keyboard", vm.CartItems[1].ProductName);
        Assert.Equal(1, vm.CartItems[1].Quantity);
        Assert.Equal(50.00m, vm.CartItems[1].UnitPrice);
        Assert.Equal(50.00m, vm.CartItems[1].LineTotal);

        // Assert - Payment amount defaults to grand total
        Assert.Equal(90.00m, vm.PaymentAmount);
        Assert.Equal(0.00m, vm.ChangeAmount);

        // Act - Cashier closes dialog
        vm.ClosePayment();

        // Assert - Payment dialog closes, cart items remain preserved
        Assert.False(vm.ShowPaymentPanel);
        Assert.Equal(2, vm.CartItems.Count);
    }

    [Fact]
    public void RegisterViewModel_PartialPayment_AddsRemainingBalanceToCustomerDebt()
    {
        var orderService = new OrderService(_db);
        var customerService = new CustomerService(_db);
        var vm = new RegisterViewModel(
            new ProductService(_db), new CategoryService(_db),
            orderService, customerService, new ReceiptService());
        vm.CashierId = 1;

        var customer = new Customer
        {
            Name = "Partial Pay Customer",
            Phone = "9988776655",
            StoreCredit = 0.00m
        };
        customerService.Save(customer);

        var p = _db.Products.First();
        var item = new CartItem
        {
            ProductId = p.Id,
            ProductName = "Test Product 200",
            Sku = "TP-200",
            UnitPrice = 200.00m,
            UnitCost = 100.00m,
            Quantity = 1,
            TaxRate = 0.00m
        };
        vm.CartItems.Add(item);
        vm.SelectCustomer(customer);
        vm.OpenPayment();

        Assert.Equal(200.00m, vm.CartGrandTotal);
        Assert.Equal(200.00m, vm.PaymentAmount);

        // Act - Customer tenders only ₹150
        vm.PaymentAmount = 150.00m;
        Assert.True(vm.IsPartialPaymentWithDebt);
        Assert.Equal(50.00m, vm.UnpaidDebtAmount);
        Assert.Equal(0.00m, vm.ChangeAmount);

        // Complete payment
        vm.ProcessPayment();

        // Assert - Customer now has 50.00 debt
        var updatedCust = _db.Customers.Find(customer.Id);
        Assert.NotNull(updatedCust);
        Assert.Equal(50.00m, updatedCust.StoreCredit);

        // Assert - Order is completed with 2 payments (150 Cash, 50 StoreCredit)
        var lastOrder = _db.Orders.Include(o => o.Payments).OrderByDescending(o => o.Id).First();
        Assert.Equal(OrderStatus.Completed, lastOrder.OrderStatus);
        Assert.Equal(PaymentStatus.Paid, lastOrder.PaymentStatus);
        Assert.Equal(2, lastOrder.Payments.Count);

        var cashPayment = lastOrder.Payments.First(p => p.PaymentMethod == PaymentMethod.Cash);
        var debtPayment = lastOrder.Payments.First(p => p.PaymentMethod == PaymentMethod.StoreCredit);

        Assert.Equal(150.00m, cashPayment.AmountPaid);
        Assert.Equal(50.00m, debtPayment.AmountPaid);

        // Assert - Receipt modal displayed and receipt text includes Added to Debt
        Assert.True(vm.ShowReceiptModal);
        Assert.Contains("Added to Debt:", vm.CurrentReceiptText);
        Assert.Contains("50.00", vm.CurrentReceiptText);
    }

    [Fact]
    public void RegisterViewModel_PartialPayment_BlocksWhenNoCustomerSelected()
    {
        var orderService = new OrderService(_db);
        var customerService = new CustomerService(_db);
        var vm = new RegisterViewModel(
            new ProductService(_db), new CategoryService(_db),
            orderService, customerService, new ReceiptService());
        vm.CashierId = 1;

        var p = _db.Products.First();
        vm.CartItems.Add(new CartItem
        {
            ProductId = p.Id,
            ProductName = "Test Item",
            Sku = "TI-100",
            UnitPrice = 200.00m,
            Quantity = 1,
            TaxRate = 0.00m
        });

        // No customer selected
        vm.OpenPayment();
        Assert.Null(vm.SelectedCustomer);

        // Enter partial payment 150 < 200
        vm.PaymentAmount = 150.00m;
        Assert.True(vm.IsPartialPaymentWithoutCustomer);

        // Act - Attempt to complete payment
        vm.ProcessPayment();

        // Assert - Payment is blocked, cart not cleared, error shown
        Assert.Single(vm.CartItems);
        Assert.False(vm.ShowReceiptModal);
        Assert.Contains("requires selecting a customer", vm.PaymentStatus);
    }

    [Fact]
    public void ProductService_DeleteAndRestore_MaintainsAuditLogAndRecycleBin()
    {
        // Arrange
        var pService = new ProductService(_db);
        var product = pService.GetAll().First();
        int initialActiveCount = pService.GetAll().Count;

        // Act - Delete product
        pService.Delete(product.Id, 1, "Discontinued stock item");

        // Assert - Soft deleted, removed from active catalog, appears in deleted list
        Assert.Equal(initialActiveCount - 1, pService.GetAll().Count);
        Assert.DoesNotContain(pService.GetAll(), p => p.Id == product.Id);

        var deletedProducts = pService.GetDeletedProducts();
        Assert.Contains(deletedProducts, p => p.Id == product.Id);

        // Verify Audit Log entry
        var logs = pService.GetDeleteLogs();
        Assert.NotEmpty(logs);
        var deleteEntry = logs.First(l => l.Action == "DELETE_PRODUCT" && l.Details.Contains(product.Name));
        Assert.NotNull(deleteEntry);
        Assert.Contains("Discontinued stock item", deleteEntry.Details);

        // Act - Restore product
        pService.Restore(product.Id, 1);

        // Assert - Restored back to active catalog
        Assert.Equal(initialActiveCount, pService.GetAll().Count);
        Assert.Contains(pService.GetAll(), p => p.Id == product.Id);
        Assert.DoesNotContain(pService.GetDeletedProducts(), p => p.Id == product.Id);

        // Verify Restore Audit Log entry
        var updatedLogs = pService.GetDeleteLogs();
        var restoreEntry = updatedLogs.First(l => l.Action == "RESTORE_PRODUCT" && l.Details.Contains(product.Name));
        Assert.NotNull(restoreEntry);
    }

    [Fact]
    public void CategoryIconPresets_ProvideComprehensiveRetailIcons()
    {
        var icons = CategoryIconItem.GetAll();

        Assert.NotEmpty(icons);
        Assert.True(icons.Count >= 20);

        // Ensure key retail categories have preset icons
        Assert.Contains(icons, i => i.IconType == CategoryIcon.Coffee);
        Assert.Contains(icons, i => i.IconType == CategoryIcon.Food);
        Assert.Contains(icons, i => i.IconType == CategoryIcon.Grocery);
        Assert.Contains(icons, i => i.IconType == CategoryIcon.Electronics);
        Assert.Contains(icons, i => i.IconType == CategoryIcon.Clothing);
        Assert.Contains(icons, i => i.IconType == CategoryIcon.Pharmacy);

        // Verify formatted DisplayText
        var coffee = icons.First(i => i.IconType == CategoryIcon.Coffee);
        Assert.Contains("☕", coffee.DisplayText);
        Assert.Contains("Coffee", coffee.DisplayText);
    }

    [Fact]
    public void QuickProduct_CreatesProductAndAddsToCartDirectly()
    {
        // Arrange
        var pService = new ProductService(_db);
        var cService = new CategoryService(_db);
        var oService = new OrderService(_db);
        var custService = new CustomerService(_db);
        var rService = new ReceiptService();
        var vm = new RegisterViewModel(pService, cService, oService, custService, rService);
        vm.CashierId = 1;

        // Act - Enter manual product on register page
        vm.OpenQuickProduct();
        Assert.True(vm.ShowQuickProductModal);

        vm.QuickProductName = "Custom Fresh Bakery Delight";
        vm.QuickProductPrice = 145.00m;
        vm.QuickProductCostPrice = 85.00m;
        vm.QuickProductQty = 3;

        vm.SaveQuickProduct();

        // Assert - Modal closed, product added to cart, and persisted in database
        Assert.False(vm.ShowQuickProductModal);
        Assert.Single(vm.CartItems);

        var cartItem = vm.CartItems.First();
        Assert.Equal("Custom Fresh Bakery Delight", cartItem.ProductName);
        Assert.Equal(145.00m, cartItem.UnitPrice);
        Assert.Equal(3, cartItem.Quantity);
        Assert.Equal(435.00m, cartItem.LineSubtotal);

        // Verify product now exists in inventory database for future editing
        var dbProduct = pService.GetById(cartItem.ProductId);
        Assert.NotNull(dbProduct);
        Assert.Equal("Custom Fresh Bakery Delight", dbProduct.Name);
        Assert.Equal(145.00m, dbProduct.SellingPrice);
        Assert.Equal(85.00m, dbProduct.CostPrice);
        Assert.True(dbProduct.StockQuantity >= 3);
    }

    [Fact]
    public void EmailService_BuildDailySummary_CalculatesSalesProfitAndStock()
    {
        // Arrange
        var settingsService = new SettingsService(_db);
        var emailService = new EmailService(_db, settingsService);

        // Act - Build daily summary
        var summary = emailService.BuildDailySummary(DateTime.Today);

        // Assert
        Assert.NotNull(summary);
        Assert.NotNull(summary.LowStockItems);

        // Generate HTML report and verify formatting
        var (subject, htmlBody, plainBody) = emailService.GenerateReportEmail(summary);

        Assert.Contains("Daily Business Summary", subject);
        Assert.Contains("₹", htmlBody);
        Assert.Contains("Today's Sales", htmlBody);
        Assert.Contains("Net Profit", htmlBody);
        Assert.NotEmpty(plainBody);
    }

    [Fact]
    public void OrderService_GenerateSalesReportCsv_ExportsCompleteData()
    {
        // Arrange
        var oService = new OrderService(_db);
        var sService = new SettingsService(_db);
        var orders = oService.GetAllOrders(null);
        var profile = sService.GetProfile();

        // Act
        var csv = oService.GenerateSalesReportCsv(orders, null, null, profile);

        // Assert
        Assert.NotNull(csv);
        Assert.Contains("Invoice #", csv);
        Assert.Contains("Date & Time", csv);
        Assert.Contains("Subtotal", csv);
        Assert.Contains("Grand Total", csv);
        Assert.Contains("Payment Method", csv);
    }

    [Fact]
    public void AuthService_UserManagement_CreateUpdateDelete_WorksCorrectly()
    {
        // Arrange
        var authService = new AuthService(_db);

        // Act - Create Cashier
        var cashier = authService.CreateUser("test_cashier", "Test Cashier", UserRole.Cashier, "pass123", "1122", 1);
        Assert.NotNull(cashier);
        Assert.Equal(UserRole.Cashier, cashier.Role);
        Assert.True(cashier.IsActive);
        Assert.Equal("1122", cashier.PinCode);

        // Act - Create Stock Maintainer
        var stockMan = authService.CreateUser("test_stock", "Test Stockman", UserRole.StockMaintainer, "pass123", "3344", 1);
        Assert.NotNull(stockMan);
        Assert.Equal(UserRole.StockMaintainer, stockMan.Role);

        // Assert - Duplicate username rejected
        Assert.Throws<InvalidOperationException>(() =>
            authService.CreateUser("test_cashier", "Duplicate", UserRole.Cashier, "pass", "9999", 1));

        // Act - Update user
        authService.UpdateUser(cashier.Id, "Updated Cashier Name", UserRole.Cashier, null, "5566", true, 1);
        var updated = _db.Users.Find(cashier.Id);
        Assert.Equal("Updated Cashier Name", updated!.FullName);
        Assert.Equal("5566", updated.PinCode);

        // Act - Deactivate user
        authService.DeleteUser(cashier.Id, 1);
        var deactivated = _db.Users.Find(cashier.Id);
        Assert.False(deactivated!.IsActive);
    }

    [Fact]
    public void MainViewModel_RoleBasedPermissions_RestrictsNavigationCorrectly()
    {
        // Arrange - Setup MainViewModel with dummy view factories
        var mainVm = new MainViewModel(
            () => null!, () => null!, () => null!, () => null!,
            () => null!, () => null!, () => null!, () => null!);

        // Act 1 - Log in as Cashier
        mainVm.SetLoggedInUser("Ravi", "Cashier", 2);

        // Assert 1 - Cashier permissions
        Assert.True(mainVm.CanViewRegister);
        Assert.True(mainVm.CanViewCustomers);
        Assert.True(mainVm.CanViewOrders);
        Assert.False(mainVm.CanViewDashboard);
        Assert.False(mainVm.CanViewInventory);
        Assert.False(mainVm.CanViewCategories);
        Assert.False(mainVm.CanViewPurchases);
        Assert.False(mainVm.CanViewSettings);
        Assert.Equal("Register", mainVm.CurrentViewName);

        // Act 1b - Cashier attempts to navigate to unauthorized view (Settings)
        mainVm.NavigateTo("Settings");
        Assert.Equal("Register", mainVm.CurrentViewName); // Redirects to allowed landing view

        // Act 2 - Log in as Stock Maintainer
        mainVm.SetLoggedInUser("Kumar", "Stock Maintainer", 3);

        // Assert 2 - Stock Maintainer permissions
        Assert.True(mainVm.CanViewInventory);
        Assert.True(mainVm.CanViewCategories);
        Assert.True(mainVm.CanViewPurchases);
        Assert.True(mainVm.CanViewDashboard);
        Assert.False(mainVm.CanViewRegister);
        Assert.False(mainVm.CanViewCustomers);
        Assert.False(mainVm.CanViewOrders);
        Assert.False(mainVm.CanViewSettings);
        Assert.Equal("Inventory", mainVm.CurrentViewName);

        // Act 3 - Log in as Admin
        mainVm.SetLoggedInUser("Admin", "Admin", 1);

        // Assert 3 - Admin has full access to all 8 views
        Assert.True(mainVm.CanViewDashboard);
        Assert.True(mainVm.CanViewRegister);
        Assert.True(mainVm.CanViewInventory);
        Assert.True(mainVm.CanViewCategories);
        Assert.True(mainVm.CanViewCustomers);
        Assert.True(mainVm.CanViewPurchases);
        Assert.True(mainVm.CanViewOrders);
        Assert.True(mainVm.CanViewSettings);
        Assert.Equal("Dashboard", mainVm.CurrentViewName);
    }

    [Fact]
    public void LoyaltyProgram_ConfigurableSpending_CalculatesAndAwardsPoints()
    {
        // Arrange
        var settingsService = new SettingsService(_db);
        var customerService = new CustomerService(_db);
        var productService = new ProductService(_db);
        var orderService = new OrderService(_db);
        var receiptService = new ReceiptService(settingsService);

        // Configure ₹250 = 1 point (as requested by user)
        settingsService.SaveLoyaltySettings(new LoyaltySettings
        {
            AmountPerPoint = 250.00m,
            PointRedeemValue = 1.00m,
            EnableLoyalty = true
        });

        var regVm = new RegisterViewModel(productService, new CategoryService(_db), orderService,
            customerService, receiptService, settingsService);

        var customer = new Customer
        {
            Name = "Loyalty Test Customer",
            Phone = "9988776655",
            LoyaltyPoints = 0,
            StoreCredit = 0
        };
        customerService.Save(customer);

        // Act 1: ₹200 purchase with ₹250 threshold -> 0 points
        regVm.CartGrandTotal = 200m;
        Assert.Equal(0m, regVm.PointsEarnedThisSale);

        // Act 2: ₹250 purchase with ₹250 threshold -> 1 point
        regVm.CartGrandTotal = 250m;
        Assert.Equal(1m, regVm.PointsEarnedThisSale);

        // Act 3: ₹500 purchase with ₹250 threshold -> 2 points
        regVm.CartGrandTotal = 500m;
        Assert.Equal(2m, regVm.PointsEarnedThisSale);

        // Act 4: ₹1000 purchase with ₹250 threshold -> 4 points
        regVm.CartGrandTotal = 1000m;
        Assert.Equal(4m, regVm.PointsEarnedThisSale);

        // Act 5: Change config in Settings: ₹100 = 1 point
        settingsService.SaveLoyaltySettings(new LoyaltySettings
        {
            AmountPerPoint = 100.00m,
            PointRedeemValue = 1.00m,
            EnableLoyalty = true
        });

        // Now ₹250 purchase earns 2 points (250 / 100 = 2)
        regVm.CartGrandTotal = 250m;
        Assert.Equal(2m, regVm.PointsEarnedThisSale);

        // Act 6: Award points to customer directly
        customerService.AddLoyaltyPoints(customer.Id, 5);
        var updatedCustomer = customerService.GetById(customer.Id);
        Assert.NotNull(updatedCustomer);
        Assert.Equal(5, updatedCustomer.LoyaltyPoints);
    }

    private void EnsureCoffeeTestProducts()
    {
        var bevCat = _db.Categories.FirstOrDefault(c => c.Name == "Beverages") ?? _db.Categories.First();
        if (!_db.Products.Any(p => p.Name == "Espresso"))
        {
            _db.Products.Add(new Product { Name = "Espresso", SKU = "COF-001", SellingPrice = 120m, CostPrice = 40m, StockQuantity = 50, CategoryId = bevCat.Id, IsActive = true });
            _db.Products.Add(new Product { Name = "Cappuccino", SKU = "COF-002", SellingPrice = 150m, CostPrice = 50m, StockQuantity = 50, CategoryId = bevCat.Id, IsActive = true });
            _db.Products.Add(new Product { Name = "Caramel Macchiato", SKU = "COF-003", SellingPrice = 180m, CostPrice = 60m, StockQuantity = 50, CategoryId = bevCat.Id, IsActive = true });
            _db.SaveChanges();
        }
    }

    [Fact]
    public void AdaptiveSearch_WithTypoTolerance_FindsMatches()
    {
        EnsureCoffeeTestProducts();
        var productService = new ProductService(_db);

        // 1. Exact match
        var exact = productService.Search("Espresso");
        Assert.NotEmpty(exact);
        Assert.Equal("Espresso", exact[0].Name);

        // 2. Typo tolerance: "espreso" with single 's'
        var typoEspresso = productService.Search("espreso");
        Assert.NotEmpty(typoEspresso);
        Assert.Contains(typoEspresso, p => p.Name.Contains("Espresso", StringComparison.OrdinalIgnoreCase));

        // 3. Typo tolerance: "cappucino" missing double 'c'
        var typoCappuccino = productService.Search("cappucino");
        Assert.NotEmpty(typoCappuccino);
        Assert.Contains(typoCappuccino, p => p.Name.Contains("Cappuccino", StringComparison.OrdinalIgnoreCase));

        // 4. Typo tolerance: "croisant" missing double 's'
        var typoCroissant = productService.Search("croisant");
        Assert.NotEmpty(typoCroissant);
        Assert.Contains(typoCroissant, p => p.Name.Contains("Croissant", StringComparison.OrdinalIgnoreCase));

        // 5. Acronym match: "CM" for "Caramel Macchiato"
        var acronymMatch = productService.Search("CM");
        Assert.NotEmpty(acronymMatch);
        Assert.Contains(acronymMatch, p => p.Name == "Caramel Macchiato");
    }

    [Fact]
    public void VisualCart_And_UndoCapability_RestoresState()
    {
        EnsureCoffeeTestProducts();
        var productService = new ProductService(_db);
        var orderService = new OrderService(_db);
        var customerService = new CustomerService(_db);
        var vm = new RegisterViewModel(productService, new CategoryService(_db), orderService,
            customerService, new ReceiptService());

        var espresso = productService.Search("Espresso").First();
        var croissant = productService.Search("Croissant").First();

        // 1. Add item sets CategoryIcon and tracks history
        vm.AddToCart(espresso);
        Assert.Single(vm.CartItems);
        Assert.Equal("Espresso", vm.CartItems[0].ProductName);
        Assert.False(string.IsNullOrEmpty(vm.CartItems[0].CategoryIcon));

        // 2. Add second item
        vm.AddToCart(croissant);
        Assert.Equal(2, vm.CartItems.Count);

        // 3. Undo second item
        vm.UndoLastItem();
        Assert.Single(vm.CartItems);
        Assert.Equal("Espresso", vm.CartItems[0].ProductName);

        // 4. Increase quantity and undo
        vm.IncreaseQuantity(vm.CartItems[0]);
        Assert.Equal(2, vm.CartItems[0].Quantity);

        vm.UndoLastItem();
        Assert.Equal(1, vm.CartItems[0].Quantity);

        // 5. Undo initial add
        vm.UndoLastItem();
        Assert.Empty(vm.CartItems);

        // 6. Undo on empty cart reports notice gracefully without error
        vm.UndoLastItem();
        Assert.Contains("Nothing to undo", vm.PaymentStatus);
    }

    [Fact]
    public void VoidBill_ClearsCart_And_LogsSecurityAudit()
    {
        var productService = new ProductService(_db);
        var orderService = new OrderService(_db);
        var customerService = new CustomerService(_db);
        var vm = new RegisterViewModel(productService, new CategoryService(_db), orderService,
            customerService, new ReceiptService());
        vm.CashierId = 1;

        var product = productService.GetAll().First();
        vm.AddToCart(product);
        vm.AddToCart(product);

        Assert.Equal(2, vm.CartItemCount);
        Assert.True(vm.CartGrandTotal > 0);

        // Open void modal
        vm.OpenVoidModal();
        Assert.True(vm.ShowVoidModal);
        vm.VoidReason = "Customer changed mind";

        // Confirm void
        vm.ConfirmVoid();

        Assert.False(vm.ShowVoidModal);
        Assert.Empty(vm.CartItems);
        Assert.Equal(0m, vm.CartGrandTotal);

        // Verify audit log has VOID_BILL
        var auditLogs = _db.AuditLogs.Where(a => a.Action == "VOID_BILL").ToList();
        Assert.NotEmpty(auditLogs);
        var latest = auditLogs.Last();
        Assert.Contains("Customer changed mind", latest.Details);
    }

    [Fact]
    public void InRegister_HeldOrders_AllowsSeamlessParkingAndResuming()
    {
        var productService = new ProductService(_db);
        var orderService = new OrderService(_db);
        var customerService = new CustomerService(_db);
        var vm = new RegisterViewModel(productService, new CategoryService(_db), orderService,
            customerService, new ReceiptService());
        vm.CashierId = 1;

        var customer = new Customer { Name = "Park Test Customer", Phone = "9988112233", StoreCredit = 50m };
        customerService.Save(customer);
        vm.SelectCustomer(customer);

        var product = productService.GetAll().First();
        vm.AddToCart(product);

        Assert.Single(vm.CartItems);
        Assert.NotNull(vm.SelectedCustomer);

        // Act 1: Park current order from register
        vm.ParkCurrentOrder();

        Assert.Empty(vm.CartItems);
        Assert.Null(vm.SelectedCustomer);
        Assert.True(vm.HeldOrdersCount >= 1);

        var parkedOrder = vm.HeldOrdersList.First(o => o.CustomerId == customer.Id);
        Assert.NotNull(parkedOrder);

        // Act 2: Resume held order into register
        vm.ResumeHeldOrderFromRegister(parkedOrder);

        Assert.Single(vm.CartItems);
        Assert.NotNull(vm.SelectedCustomer);
        Assert.Equal("Park Test Customer", vm.SelectedCustomer.Name);
    }

    [Fact]
    public void CrossSellingSuggestions_RecommendsComplementaryProducts()
    {
        EnsureCoffeeTestProducts();
        var productService = new ProductService(_db);
        var orderService = new OrderService(_db);
        var customerService = new CustomerService(_db);
        var vm = new RegisterViewModel(productService, new CategoryService(_db), orderService,
            customerService, new ReceiptService());

        // When a beverage is added, suggestions should recommend bakery/snacks
        var espresso = productService.Search("Espresso").First();
        vm.AddToCart(espresso);

        Assert.NotEmpty(vm.CrossSellingSuggestions);
        Assert.DoesNotContain(vm.CrossSellingSuggestions, p => p.Id == espresso.Id);

        // 1-click add suggested product
        var suggested = vm.CrossSellingSuggestions.First();
        vm.AddSuggestedProduct(suggested);

        Assert.Equal(2, vm.CartItems.Count);
        Assert.Contains(vm.CartItems, c => c.ProductId == suggested.Id);
    }

    [Fact]
    public void DbSeeder_MigratesExistingDatabaseWithoutMRPColumnCleanly()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        // Create an older schema without MRP column in Products
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = @"
                CREATE TABLE Suppliers (Id INTEGER PRIMARY KEY, Name TEXT, ContactPerson TEXT, Phone TEXT, Email TEXT, Address TEXT, HandledProducts TEXT, Notes TEXT, IsActive INTEGER, CreatedAt TEXT);
                CREATE TABLE Categories (Id INTEGER PRIMARY KEY, Name TEXT, Icon TEXT, SortOrder INTEGER, SupplierId INTEGER, IsActive INTEGER);
                CREATE TABLE Users (Id INTEGER PRIMARY KEY, Username TEXT, PasswordHash TEXT, FullName TEXT, Role INTEGER, PinCode TEXT, IsActive INTEGER, CreatedAt TEXT);
                CREATE TABLE Customers (Id INTEGER PRIMARY KEY, Name TEXT, Phone TEXT, Email TEXT, Code TEXT, LoyaltyPoints TEXT, StoreCredit TEXT, IsActive INTEGER, CreatedAt TEXT);
                CREATE TABLE Products (Id INTEGER PRIMARY KEY, Name TEXT, SKU TEXT, Barcode TEXT, Description TEXT, CostPrice TEXT, SellingPrice TEXT, StockQuantity INTEGER, ReorderLevel INTEGER, CategoryId INTEGER, TaxRate TEXT, IsActive INTEGER, CreatedAt TEXT, Unit TEXT, SupplierId INTEGER);
                INSERT INTO Categories (Id, Name, Icon, SortOrder, SupplierId, IsActive) VALUES (1, 'Dairy', '🥛', 1, NULL, 1);
                INSERT INTO Users (Id, Username, PasswordHash, FullName, Role, PinCode, IsActive, CreatedAt) VALUES (1, 'admin', 'hash', 'Admin', 0, '0000', 1, '2026-01-01');
                INSERT INTO Products (Id, Name, SKU, Barcode, Description, CostPrice, SellingPrice, StockQuantity, ReorderLevel, CategoryId, TaxRate, IsActive, CreatedAt) VALUES (1, 'Test Milk 500ml', 'P001', 'B001', '', '20.00', '25.00', 10, 2, 1, '0.00', 1, '2026-01-01');
            ";
            cmd.ExecuteNonQuery();
        }

        var options = new DbContextOptionsBuilder<ApexDbContext>()
            .UseSqlite(connection)
            .Options;

        using (var db = new ApexDbContext(options))
        {
            // Act: This should NOT throw 'no such column: p.MRP'
            DbSeeder.Seed(db);

            // Assert: Products can now be queried with MRP
            var prod = db.Products.First();
            Assert.Equal("Test Milk 500ml", prod.Name);
            Assert.Equal(25.00m, prod.MRP);
            Assert.Equal(25.00m, prod.SellingPrice);
            Assert.Equal(20.00m, prod.CostPrice);
            Assert.Equal(5.00m, prod.MarginAmount);
        }
    }
}


