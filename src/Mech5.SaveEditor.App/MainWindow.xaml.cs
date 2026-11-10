using System.ComponentModel;
using System.Windows;
using Mech5.SaveEditor.App.ViewModels;

namespace Mech5.SaveEditor.App;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        StateChanged += (_, _) => Root.Margin = WindowState == WindowState.Maximized ? new Thickness(7) : default;
    }

    protected override void OnClosing(CancelEventArgs e)
    {
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
