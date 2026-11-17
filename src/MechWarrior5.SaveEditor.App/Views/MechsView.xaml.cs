using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MechWarrior5.SaveEditor.App.ViewModels;

namespace MechWarrior5.SaveEditor.App.Views;

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
            // No tick change here: CheckList already toggled the row on each of the two clicks, which cancel out.
            vm.EditLoadout(row);
            e.Handled = true;
        }
    }
}
