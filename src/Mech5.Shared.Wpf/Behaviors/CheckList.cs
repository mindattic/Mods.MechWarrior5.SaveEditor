using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Mech5.Shared.Wpf.ViewModels;

namespace Mech5.Shared.Wpf.Behaviors;

/// <summary>
/// Turns a ListBox of <see cref="SelectableItem"/> rows into a check list: click a row (or its
/// checkbox) to tick it, Shift+click to tick or untick the whole range from the last clicked row
/// (following that row's state), Space toggles the focused row and Ctrl+A ticks every visible row.
/// </summary>
public static class CheckList
{
    public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached(
        "IsEnabled", typeof(bool), typeof(CheckList), new PropertyMetadata(false, OnIsEnabledChanged));

    private static readonly DependencyProperty AnchorProperty = DependencyProperty.RegisterAttached(
        "Anchor", typeof(SelectableItem), typeof(CheckList));

    public static bool GetIsEnabled(DependencyObject element) => (bool)element.GetValue(IsEnabledProperty);

    public static void SetIsEnabled(DependencyObject element, bool value) => element.SetValue(IsEnabledProperty, value);

    private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ListBox list)
        {
            return;
        }

        list.PreviewMouseLeftButtonDown -= OnMouseDown;
        list.PreviewKeyDown -= OnKeyDown;
        if ((bool)e.NewValue)
        {
            list.PreviewMouseLeftButtonDown += OnMouseDown;
            list.PreviewKeyDown += OnKeyDown;
        }
    }

    private static void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        var list = (ListBox)sender;
        if (e.OriginalSource is not DependencyObject source
            || IsInside<ButtonBase>(source, stopAtCheckBox: true)
            || IsInside<TextBox>(source, stopAtCheckBox: false)
            || ItemsControl.ContainerFromElement(list, source) is not ListBoxItem container
            || container.DataContext is not SelectableItem item)
        {
            return;
        }

        var anchor = (SelectableItem?)list.GetValue(AnchorProperty);
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) && anchor is not null && !ReferenceEquals(anchor, item))
        {
            var from = list.Items.IndexOf(anchor);
            var to = list.Items.IndexOf(item);
            if (from >= 0 && to >= 0)
            {
                var state = anchor.IsChecked;
                for (var i = Math.Min(from, to); i <= Math.Max(from, to); i++)
                {
                    if (list.Items[i] is SelectableItem row)
                    {
                        row.IsChecked = state;
                    }
                }
            }
        }
        else
        {
            item.IsChecked = !item.IsChecked;
            list.SetValue(AnchorProperty, item);
        }

        container.Focus();
        e.Handled = true;
    }

    private static void OnKeyDown(object sender, KeyEventArgs e)
    {
        var list = (ListBox)sender;
        if (e.Key == Key.Space && Keyboard.FocusedElement is ListBoxItem { DataContext: SelectableItem focused })
        {
            focused.IsChecked = !focused.IsChecked;
            list.SetValue(AnchorProperty, focused);
            e.Handled = true;
        }
        else if (e.Key == Key.A && Keyboard.Modifiers == ModifierKeys.Control)
        {
            foreach (var row in list.Items.OfType<SelectableItem>())
            {
                row.IsChecked = true;
            }

            e.Handled = true;
        }
    }

    /// <summary>True when the click landed on a nested control that should keep its own behavior.</summary>
    private static bool IsInside<T>(DependencyObject source, bool stopAtCheckBox)
        where T : DependencyObject
    {
        for (var node = source; node is not null and not ListBoxItem; node = Parent(node))
        {
            if (node is T && !(stopAtCheckBox && node is CheckBox))
            {
                return true;
            }
        }

        return false;
    }

    private static DependencyObject? Parent(DependencyObject node) =>
        node is Visual or System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(node) : LogicalTreeHelper.GetParent(node);
}
