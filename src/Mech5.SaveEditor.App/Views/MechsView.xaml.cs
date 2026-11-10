using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Mech5.SaveEditor.App.ViewModels;

namespace Mech5.SaveEditor.App.Views;

public partial class MechsView : UserControl
{
    public MechsView()
    {
        InitializeComponent();
    }

    private void OnHangarDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (ItemsControl.ContainerFromElement((ItemsControl)sender, (DependencyObject)e.OriginalSource) is ListBoxItem { DataContext: HangarMechViewModel row }
            && DataContext is MechsViewModel vm)
        {
            row.IsChecked = !row.IsChecked;
            vm.EditLoadout(row);
            e.Handled = true;
        }
    }

    private void OnOpenSarna(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: Uri { Scheme: "https", Host: "www.sarna.net" } url })
        {
            Process.Start(new ProcessStartInfo(url.AbsoluteUri) { UseShellExecute = true });
        }
    }
}
