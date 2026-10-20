using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ApexPOS.Domain.Entities;
using ApexPOS.Domain.Enums;
using ApexPOS.Services;

namespace ApexPOS.Wpf.ViewModels;

public partial class CategoriesViewModel : ObservableObject
{
    private readonly CategoryService _categoryService;
    private readonly PurchaseService _purchaseService;

    [ObservableProperty] private string _searchQuery = "";
    [ObservableProperty] private int? _filterSupplierId;
    [ObservableProperty] private bool _showEditModal;
    [ObservableProperty] private bool _isNewCategory;
    [ObservableProperty] private Category? _selectedCategory;

    // Edit fields
    [ObservableProperty] private string _editName = "";
    [ObservableProperty] private string _editIcon = "🏷️";
    [ObservableProperty] private CategoryIconItem? _selectedIconItem;
    [ObservableProperty] private int? _editSupplierId;
    [ObservableProperty] private int _editSortOrder = 1;
    [ObservableProperty] private string _statusMessage = "";

    public ObservableCollection<Category> Categories { get; } = new();
    public ObservableCollection<Supplier> Suppliers { get; } = new();
    public ObservableCollection<CategoryIconItem> AvailableIcons { get; } = new();

    public CategoriesViewModel(CategoryService categoryService, PurchaseService purchaseService)
    {
        _categoryService = categoryService;
        _purchaseService = purchaseService;

        foreach (var iconItem in CategoryIconItem.GetAll())
            AvailableIcons.Add(iconItem);
    }

    partial void OnSelectedIconItemChanged(CategoryIconItem? value)
    {
        if (value != null)
        {
            EditIcon = value.Icon;
        }
    }

    public void LoadData()
    {
        Suppliers.Clear();
        foreach (var s in _purchaseService.GetAllSuppliers())
            Suppliers.Add(s);

        RefreshCategories();
    }

    private void RefreshCategories()
    {
        Categories.Clear();
        var list = _categoryService.GetAll();
        if (FilterSupplierId.HasValue)
        {
            list = list.Where(c => c.SupplierId == FilterSupplierId.Value).ToList();
        }

        if (!string.IsNullOrWhiteSpace(SearchQuery))
        {
            list = list.Where(c => c.Name.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase) ||
                                   (c.Supplier != null && c.Supplier.Name.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase))).ToList();
        }

        foreach (var c in list)
            Categories.Add(c);
    }

    [RelayCommand]
    public void Search()
    {
        RefreshCategories();
    }

    [RelayCommand]
    public void ResetFilter()
    {
        SearchQuery = "";
        FilterSupplierId = null;
        RefreshCategories();
    }

    partial void OnSearchQueryChanged(string value)
    {
        RefreshCategories();
    }

    partial void OnFilterSupplierIdChanged(int? value)
    {
        RefreshCategories();
    }

    [RelayCommand]
    public void NewCategory()
    {
        IsNewCategory = true;
        EditName = "";
        EditIcon = "🏷️";
        SelectedIconItem = AvailableIcons.FirstOrDefault(i => i.Icon == "🏷️") ?? AvailableIcons.FirstOrDefault();
        EditSupplierId = Suppliers.FirstOrDefault()?.Id;
        EditSortOrder = Categories.Count + 1;
        ShowEditModal = true;
        StatusMessage = "";
    }

    [RelayCommand]
    public void EditCategory(Category category)
    {
        SelectedCategory = category;
        IsNewCategory = false;
        EditName = category.Name;
        EditIcon = category.Icon;
        SelectedIconItem = AvailableIcons.FirstOrDefault(i => i.Icon == category.Icon)
                           ?? new CategoryIconItem { Icon = category.Icon, Name = "Custom Icon" };
        EditSupplierId = category.SupplierId;
        EditSortOrder = category.SortOrder;
        ShowEditModal = true;
        StatusMessage = "";
    }

    [RelayCommand]
    public void SaveCategory()
    {
        if (string.IsNullOrWhiteSpace(EditName))
        {
            StatusMessage = "Category name cannot be empty.";
            return;
        }

        var cat = IsNewCategory ? new Category() : SelectedCategory!;
        cat.Name = EditName.Trim();
        cat.Icon = !string.IsNullOrWhiteSpace(EditIcon) ? EditIcon.Trim() : (SelectedIconItem?.Icon ?? "🏷️");
        cat.SupplierId = EditSupplierId;
        cat.SortOrder = EditSortOrder;
        cat.IsActive = true;

        _categoryService.Save(cat);
        ShowEditModal = false;
        RefreshCategories();
        StatusMessage = $"Category '{cat.Name}' saved successfully.";
    }

    [RelayCommand]
    public void CloseModal()
    {
        ShowEditModal = false;
    }

    [RelayCommand]
    public void DeleteCategory(Category category)
    {
        _categoryService.Delete(category.Id, userId: 1);
        RefreshCategories();
        StatusMessage = $"Category '{category.Name}' deleted. You can restore it anytime.";
    }
}
