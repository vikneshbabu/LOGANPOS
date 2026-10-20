using System;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ApexPOS.Wpf.Views;

public partial class RegisterView : UserControl
{
    private static readonly Regex NumericRegex = new("^[0-9]+$");

    public RegisterView()
    {
        InitializeComponent();
    }

    #region Category Navigation & Scrolling

    private void CategoryScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (CategoryScrollViewer != null)
        {
            CategoryScrollViewer.ScrollToHorizontalOffset(CategoryScrollViewer.HorizontalOffset - e.Delta);
            e.Handled = true;
        }
    }

    private void CategoryScrollLeft_Click(object sender, RoutedEventArgs e)
    {
        if (CategoryScrollViewer != null)
        {
            CategoryScrollViewer.ScrollToHorizontalOffset(Math.Max(0, CategoryScrollViewer.HorizontalOffset - 180));
        }
    }

    private void CategoryScrollRight_Click(object sender, RoutedEventArgs e)
    {
        if (CategoryScrollViewer != null)
        {
            CategoryScrollViewer.ScrollToHorizontalOffset(CategoryScrollViewer.HorizontalOffset + 180);
        }
    }

    #endregion

    #region Manual Quantity Entry Handlers

    private void QuantityTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        e.Handled = !NumericRegex.IsMatch(e.Text);
    }

    private void QuantityTextBox_GotFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox tb)
        {
            tb.Dispatcher.BeginInvoke(new Action(() => tb.SelectAll()));
        }
    }

    private void QuantityTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox tb)
        {
            if (string.IsNullOrWhiteSpace(tb.Text) || !int.TryParse(tb.Text, out var val) || val < 1)
            {
                tb.Text = "1";
                var be = tb.GetBindingExpression(TextBox.TextProperty);
                be?.UpdateSource();
            }
        }
    }

    private void QuantityTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter || e.Key == Key.Return)
        {
            Keyboard.ClearFocus();
            e.Handled = true;
        }
    }

    #endregion

    #region Customer Search Handlers

    private void CustomerSearchBox_GotFocus(object sender, RoutedEventArgs e)
    {
        if (DataContext is ViewModels.RegisterViewModel vm)
        {
            vm.IsCustomerDropdownOpen = true;
        }
    }

    private void CustomerSearchBox_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is ViewModels.RegisterViewModel vm)
        {
            vm.IsCustomerDropdownOpen = true;
        }
    }

    private void CustomerSearchBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (DataContext is ViewModels.RegisterViewModel vm)
        {
            if (e.Key == Key.Enter || e.Key == Key.Return)
            {
                vm.SelectFirstFilteredCustomerCommand.Execute(null);
                Keyboard.ClearFocus();
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                vm.IsCustomerDropdownOpen = false;
                e.Handled = true;
            }
        }
    }

    #endregion

    #region Sales Person Search Handlers

    private void SalesPersonSearchBox_GotFocus(object sender, RoutedEventArgs e)
    {
        if (DataContext is ViewModels.RegisterViewModel vm)
        {
            vm.IsSalesPersonDropdownOpen = true;
        }
    }

    private void SalesPersonSearchBox_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is ViewModels.RegisterViewModel vm)
        {
            vm.IsSalesPersonDropdownOpen = true;
        }
    }

    private void SalesPersonSearchBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (DataContext is ViewModels.RegisterViewModel vm)
        {
            if (e.Key == Key.Enter || e.Key == Key.Return)
            {
                vm.SelectFirstFilteredSalesPersonCommand.Execute(null);
                Keyboard.ClearFocus();
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                vm.IsSalesPersonDropdownOpen = false;
                e.Handled = true;
            }
        }
    }

    #endregion

    #region Keyboard Shortcuts Navigation (F1-F7, Ctrl+Z, Esc)

    private void UserControl_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (DataContext is not ViewModels.RegisterViewModel vm) return;

        // F1: Search Products
        if (e.Key == Key.F1)
        {
            ProductSearchTextBox?.Focus();
            ProductSearchTextBox?.SelectAll();
            e.Handled = true;
            return;
        }

        // F2: Focus Customer Search
        if (e.Key == Key.F2)
        {
            CustomerSearchTextBox?.Focus();
            CustomerSearchTextBox?.SelectAll();
            vm.IsCustomerDropdownOpen = true;
            e.Handled = true;
            return;
        }

        // F3: Quick Product Addition
        if (e.Key == Key.F3)
        {
            vm.OpenQuickProductCommand.Execute(null);
            e.Handled = true;
            return;
        }

        // F4: Void Bill
        if (e.Key == Key.F4)
        {
            vm.OpenVoidModalCommand.Execute(null);
            e.Handled = true;
            return;
        }

        // F5: Pay / Checkout
        if (e.Key == Key.F5)
        {
            vm.OpenPaymentCommand.Execute(null);
            e.Handled = true;
            return;
        }

        // F6: Park / Hold Order
        if (e.Key == Key.F6)
        {
            vm.ParkCurrentOrderCommand.Execute(null);
            e.Handled = true;
            return;
        }

        // F7: Held Orders Tray
        if (e.Key == Key.F7)
        {
            vm.ToggleHeldDrawerCommand.Execute(null);
            e.Handled = true;
            return;
        }

        // Ctrl + Z: Undo last cart action
        if (e.Key == Key.Z && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
        {
            vm.UndoLastItemCommand.Execute(null);
            e.Handled = true;
            return;
        }

        // Escape: Dismiss active modal or dropdown
        if (e.Key == Key.Escape)
        {
            if (vm.ShowVoidModal)
            {
                vm.CloseVoidModalCommand.Execute(null);
                e.Handled = true;
            }
            else if (vm.ShowHeldDrawer)
            {
                vm.CloseHeldDrawerCommand.Execute(null);
                e.Handled = true;
            }
            else if (vm.ShowPaymentPanel)
            {
                vm.ClosePaymentCommand.Execute(null);
                e.Handled = true;
            }
            else if (vm.ShowQuickProductModal)
            {
                vm.CloseQuickProductCommand.Execute(null);
                e.Handled = true;
            }
            else if (vm.ShowCreditDialog)
            {
                vm.DismissCreditDialogCommand.Execute(null);
                e.Handled = true;
            }
            else if (vm.IsCustomerDropdownOpen)
            {
                vm.IsCustomerDropdownOpen = false;
                e.Handled = true;
            }
            else if (vm.ShowReceiptModal)
            {
                vm.CloseReceiptModalCommand.Execute(null);
                e.Handled = true;
            }
        }
    }

    private void CloseHeldDrawer_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is ViewModels.RegisterViewModel vm)
        {
            vm.CloseHeldDrawerCommand.Execute(null);
        }
    }

    #endregion
}
