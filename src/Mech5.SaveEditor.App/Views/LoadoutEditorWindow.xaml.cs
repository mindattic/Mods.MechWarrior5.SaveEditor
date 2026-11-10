using System.Windows;
using Mech5.SaveEditor.App.ViewModels;

namespace Mech5.SaveEditor.App.Views;

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
