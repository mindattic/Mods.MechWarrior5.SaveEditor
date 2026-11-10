using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MechWarrior5.SaveEditor.App.ViewModels;

namespace MechWarrior5.SaveEditor.App;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        StateChanged += (_, _) => Root.Margin = WindowState == WindowState.Maximized ? new Thickness(7) : default;
    }

    /// <summary>
    /// Number and name boxes write back when they lose focus; a shortcut doesn't move focus, so push the
    /// focused box's text through first, or Ctrl+S would save without the value just typed.
    /// </summary>
    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control) && e.Key is Key.S or Key.O)
        {
            CommitFocusedText();
        }

        base.OnPreviewKeyDown(e);
    }

    private static void CommitFocusedText() =>
        (Keyboard.FocusedElement as TextBox)?.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();

    protected override void OnClosing(CancelEventArgs e)
    {
        CommitFocusedText();
        if (DataContext is ShellViewModel shell && !shell.ConfirmExit())
        {
            e.Cancel = true;
        }

        base.OnClosing(e);
    }

    private void OnMinimize(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void OnMaximize(object sender, RoutedEventArgs e) =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void OnClose(object sender, RoutedEventArgs e) => Close();
}
