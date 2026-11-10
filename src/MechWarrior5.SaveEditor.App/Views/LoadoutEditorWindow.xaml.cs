using System.Windows;
using MechWarrior5.SaveEditor.App.ViewModels;

namespace MechWarrior5.SaveEditor.App.Views;

public partial class LoadoutEditorWindow : Window
{
    public LoadoutEditorWindow(LoadoutEditorViewModel editor)
    {
        InitializeComponent();
        DataContext = editor;
        // A borderless window maximizes past the screen edge by its resize border; pull the frame back in, as the main window does.
        StateChanged += (_, _) =>
        {
            if (Content is FrameworkElement frame)
            {
                frame.Margin = WindowState == WindowState.Maximized ? new Thickness(7) : default;
            }
        };
        editor.CloseRequested += (_, applied) =>
        {
            DialogResult = applied;
            Close();
        };
    }
}
