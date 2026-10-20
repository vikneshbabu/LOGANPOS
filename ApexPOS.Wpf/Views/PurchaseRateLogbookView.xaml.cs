using System.Windows.Controls;

namespace ApexPOS.Wpf.Views;

public partial class PurchaseRateLogbookView : UserControl
{
    public PurchaseRateLogbookView()
    {
        InitializeComponent();
        Loaded += (s, e) =>
        {
            if (DataContext is ViewModels.PurchaseRateLogbookViewModel vm)
            {
                vm.LoadData();
            }
        };
    }
}
