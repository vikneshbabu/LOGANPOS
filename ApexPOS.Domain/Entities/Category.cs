namespace ApexPOS.Domain.Entities;

public class Category
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Icon { get; set; } = "📦";
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;

    public int? SupplierId { get; set; } // Associated Distributor / Supplier

    // Navigation
    public Supplier? Supplier { get; set; }
    public ICollection<Product> Products { get; set; } = new List<Product>();
}
