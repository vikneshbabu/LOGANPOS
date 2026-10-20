namespace ApexPOS.Domain.Enums;

public enum OrderStatus
{
    Active,
    Completed,
    Voided,
    OnHold,
    Refunded
}

public enum PaymentMethod
{
    Cash,
    Card,
    DigitalWallet,
    StoreCredit,
    Split
}

public enum PaymentStatus
{
    Pending,
    Paid,
    PartiallyPaid,
    Refunded
}

public enum InventoryTransactionType
{
    Sale,
    Return,
    Adjustment,
    Restock,
    Damage,
    Transfer
}

public enum UserRole
{
    Admin,
    Manager,
    Cashier,
    StockMaintainer,
    SalesPerson
}

public enum PurchaseOrderStatus
{
    Draft,
    Ordered,
    Received,
    Cancelled
}

