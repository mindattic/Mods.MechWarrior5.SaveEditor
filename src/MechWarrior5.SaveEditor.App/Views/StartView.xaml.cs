using System.Windows.Controls;
using System.Windows.Input;
using MechWarrior5.SaveEditor.App.ViewModels;
using MechWarrior5.Mercs.SaveEditor.Core.Game;

namespace MechWarrior5.SaveEditor.App.Views;

public partial class StartView : UserControl
{
    public StartView()
    {
        InitializeComponent();
    }

    private void OnSaveDoubleClick(object sender, MouseButtonEventArgs e) => OpenSelected();

    private void OnSaveKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            OpenSelected();
            e.Handled = true;
        }
    }

    private void OpenSelected()
    {
        if (Saves.SelectedItem is SaveInfo save && DataContext is StartViewModel vm)
        {
            vm.OpenCommand.Execute(save);
        }
    }
}
