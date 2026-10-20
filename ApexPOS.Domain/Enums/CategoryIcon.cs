namespace ApexPOS.Domain.Enums;

public enum CategoryIcon
{
    GeneralBox,
    Food,
    Beverages,
    Grocery,
    Bakery,
    Dairy,
    Produce,
    Meat,
    Electronics,
    Clothing,
    Pharmacy,
    PersonalCare,
    Household,
    Stationery,
    Hardware,
    Home,
    Coffee,
    Gifts,
    Deals,
    Cosmetics,
    Sports,
    BabyCare,
    PetSupplies
}

public class CategoryIconItem
{
    public string Icon { get; set; } = "📦";
    public string Name { get; set; } = "General";
    public CategoryIcon IconType { get; set; }
    public string DisplayText => $"{Icon}  {Name}";

    public static List<CategoryIconItem> GetAll() => new()
    {
        new() { Icon = "📦", Name = "General Goods", IconType = CategoryIcon.GeneralBox },
        new() { Icon = "🍔", Name = "Fast Food & Meals", IconType = CategoryIcon.Food },
        new() { Icon = "🥤", Name = "Beverages & Drinks", IconType = CategoryIcon.Beverages },
        new() { Icon = "🛒", Name = "Grocery & Provisions", IconType = CategoryIcon.Grocery },
        new() { Icon = "🍞", Name = "Bakery & Snacks", IconType = CategoryIcon.Bakery },
        new() { Icon = "🥛", Name = "Dairy & Milk Products", IconType = CategoryIcon.Dairy },
        new() { Icon = "🍎", Name = "Fruits & Fresh Produce", IconType = CategoryIcon.Produce },
        new() { Icon = "🥩", Name = "Meat, Poultry & Seafood", IconType = CategoryIcon.Meat },
        new() { Icon = "☕", Name = "Coffee, Tea & Cafe", IconType = CategoryIcon.Coffee },
        new() { Icon = "📱", Name = "Electronics & Gadgets", IconType = CategoryIcon.Electronics },
        new() { Icon = "👕", Name = "Apparel & Fashion", IconType = CategoryIcon.Clothing },
        new() { Icon = "💊", Name = "Pharmacy & Healthcare", IconType = CategoryIcon.Pharmacy },
        new() { Icon = "🧴", Name = "Personal Care & Hygiene", IconType = CategoryIcon.PersonalCare },
        new() { Icon = "💄", Name = "Beauty & Cosmetics", IconType = CategoryIcon.Cosmetics },
        new() { Icon = "🧹", Name = "Cleaning & Household", IconType = CategoryIcon.Household },
        new() { Icon = "📚", Name = "Books & Stationery", IconType = CategoryIcon.Stationery },
        new() { Icon = "🛠️", Name = "Hardware & Tools", IconType = CategoryIcon.Hardware },
        new() { Icon = "🏠", Name = "Home & Kitchen", IconType = CategoryIcon.Home },
        new() { Icon = "🎁", Name = "Gifts & Toys", IconType = CategoryIcon.Gifts },
        new() { Icon = "⚽", Name = "Sports & Fitness", IconType = CategoryIcon.Sports },
        new() { Icon = "👶", Name = "Baby Care & Toys", IconType = CategoryIcon.BabyCare },
        new() { Icon = "🐾", Name = "Pet Food & Supplies", IconType = CategoryIcon.PetSupplies },
        new() { Icon = "🏷️", Name = "Special Deals & Discounts", IconType = CategoryIcon.Deals }
    };
}
