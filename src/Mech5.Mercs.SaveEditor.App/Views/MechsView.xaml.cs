using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;

namespace Mech5.Mercs.SaveEditor.App.Views;

public partial class MechsView : UserControl
{
    public MechsView()
    {
        InitializeComponent();
    }

    private void OnOpenSarna(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: Uri { Scheme: "https", Host: "www.sarna.net" } url })
        {
            Process.Start(new ProcessStartInfo(url.AbsoluteUri) { UseShellExecute = true });
        }
    }
}
