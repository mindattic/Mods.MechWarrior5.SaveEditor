using System.Windows;
using MechWarrior5.SaveEditor.App.ViewModels;

namespace MechWarrior5.SaveEditor.App.Views;

public partial class LoadoutEditorWindow : Window
{
    public LoadoutEditorWindow(LoadoutEditorViewModel editor)
    {
        InitializeComponent();
        DataContext = editor;
        editor.CloseRequested += (_, applied) =>
        {
            DialogResult = applied;
            Close();
        };
    }
}
