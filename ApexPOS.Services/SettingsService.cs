using ApexPOS.Data;
using ApexPOS.Domain.Entities;

namespace ApexPOS.Services;

public class StoreProfile
{
    public string StoreName { get; set; } = "Govindarajan store";
    public string StoreAddress { get; set; } = "bazzar street sirkali";
    public string StorePhone { get; set; } = "9487848939";
    public decimal TaxRate { get; set; } = 0.0m;
    public string CurrencySymbol { get; set; } = "₹";
    public string ReceiptFooter { get; set; } = "Thank you for shopping at Govindarajan store! Please visit again.";
}

public class SettingsService : ISettingsService
{
    private readonly ApexDbContext _db;

    public SettingsService(ApexDbContext db) => _db = db;

    public StoreProfile GetProfile()
    {
        var profile = new StoreProfile();
        var name = _db.Settings.Find("StoreName")?.Value;
        if (!string.IsNullOrWhiteSpace(name)) profile.StoreName = name;

        var addr = _db.Settings.Find("StoreAddress")?.Value;
        if (!string.IsNullOrWhiteSpace(addr)) profile.StoreAddress = addr;

        var phone = _db.Settings.Find("StorePhone")?.Value;
        if (!string.IsNullOrWhiteSpace(phone)) profile.StorePhone = phone;

        profile.TaxRate = 0.0m;

        var curr = _db.Settings.Find("CurrencySymbol")?.Value;
        if (!string.IsNullOrWhiteSpace(curr)) profile.CurrencySymbol = curr;

        var footer = _db.Settings.Find("ReceiptFooter")?.Value;
        if (!string.IsNullOrWhiteSpace(footer)) profile.ReceiptFooter = footer;

        return profile;
    }

    public void SaveProfile(StoreProfile profile)
    {
        Set("StoreName", profile.StoreName);
        Set("StoreAddress", profile.StoreAddress);
        Set("StorePhone", profile.StorePhone);
        Set("TaxRate", "0.00");
        Set("CurrencySymbol", profile.CurrencySymbol);
        Set("ReceiptFooter", profile.ReceiptFooter);
        _db.SaveChanges();
    }

    public string? Get(string key) => _db.Settings.Find(key)?.Value;

    public void Set(string key, string value)
    {
        var s = _db.Settings.Find(key);
        if (s == null)
            _db.Settings.Add(new Setting { Key = key, Value = value });
        else
            s.Value = value;
    }

    public LoyaltySettings GetLoyaltySettings()
    {
        var s = new LoyaltySettings();
        var amt = Get("Loyalty_AmountPerPoint");
        if (decimal.TryParse(amt, out var a) && a > 0) s.AmountPerPoint = a;

        var redeem = Get("Loyalty_PointRedeemValue");
        if (decimal.TryParse(redeem, out var r) && r > 0) s.PointRedeemValue = r;

        var enable = Get("Loyalty_Enable");
        if (bool.TryParse(enable, out var e)) s.EnableLoyalty = e;

        return s;
    }

    public void SaveLoyaltySettings(LoyaltySettings settings)
    {
        Set("Loyalty_AmountPerPoint", settings.AmountPerPoint.ToString("F2"));
        Set("Loyalty_PointRedeemValue", settings.PointRedeemValue.ToString("F2"));
        Set("Loyalty_Enable", settings.EnableLoyalty.ToString());
        _db.SaveChanges();
    }
}

public class LoyaltySettings
{
    public decimal AmountPerPoint { get; set; } = 250.00m; // Default: ₹250 = 1 point
    public decimal PointRedeemValue { get; set; } = 1.00m; // 1 point = ₹1
    public bool EnableLoyalty { get; set; } = true;
}
