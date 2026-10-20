using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ApexPOS.Domain.Entities;
using ApexPOS.Services;

namespace ApexPOS.Wpf.ViewModels;

public partial class CustomersViewModel : ObservableObject
{
    private readonly CustomerService _customerService;

    [ObservableProperty] private string _searchQuery = "";
    [ObservableProperty] private Customer? _selectedCustomer;
    [ObservableProperty] private bool _showEditPanel;
    [ObservableProperty] private bool _isNewCustomer;

    // Edit form
    [ObservableProperty] private string _editName = "";
    [ObservableProperty] private string _editCode = "";
    [ObservableProperty] private string _editPhone = "";
    [ObservableProperty] private string _editEmail = "";
    [ObservableProperty] private decimal _editDebt;

    public ObservableCollection<Customer> Customers { get; } = new();

    public CustomersViewModel(CustomerService customerService)
    {
        _customerService = customerService;
    }

    public void LoadData()
    {
        RefreshCustomers();
    }

    private void RefreshCustomers()
    {
        Customers.Clear();
        var customers = string.IsNullOrWhiteSpace(SearchQuery)
            ? _customerService.GetAll()
            : _customerService.Search(SearchQuery);
        foreach (var c in customers)
            Customers.Add(c);
    }

    [RelayCommand]
    public void Search()
    {
        RefreshCustomers();
    }

    [RelayCommand]
    public void NewCustomer()
    {
        IsNewCustomer = true;
        EditName = "";
        EditCode = $"CUST-{DateTime.Now:mmss}";
        EditPhone = "";
        EditEmail = "";
        EditDebt = 0;
        ShowEditPanel = true;
    }

    [RelayCommand]
    public void EditCustomer(Customer customer)
    {
        SelectedCustomer = customer;
        IsNewCustomer = false;
        EditName = customer.Name;
        EditCode = customer.Code;
        EditPhone = customer.Phone;
        EditEmail = customer.Email;
        EditDebt = customer.StoreCredit;
        ShowEditPanel = true;
    }

    [RelayCommand]
    public void SaveCustomer()
    {
        Customer customer;
        if (IsNewCustomer)
            customer = new Customer();
        else
            customer = SelectedCustomer!;

        customer.Name = EditName;
        customer.Code = EditCode;
        customer.Phone = EditPhone;
        customer.Email = EditEmail;
        customer.StoreCredit = EditDebt;

        _customerService.Save(customer);
        ShowEditPanel = false;
        RefreshCustomers();
    }

    [RelayCommand]
    public void CancelEdit()
    {
        ShowEditPanel = false;
    }
}
