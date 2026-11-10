using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Mech5.Shared.Wpf.Controls;

/// <summary>
/// A panel frame with cut top-left and bottom-right corners and an amber corner tick,
/// echoing the MW5 Mercenaries HUD.
/// </summary>
public sealed class ChamferBorder : Decorator
{
    public static readonly DependencyProperty BackgroundProperty = DependencyProperty.Register(
        nameof(Background), typeof(Brush), typeof(ChamferBorder),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty BorderBrushProperty = DependencyProperty.Register(
        nameof(BorderBrush), typeof(Brush), typeof(ChamferBorder),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty AccentBrushProperty = DependencyProperty.Register(
        nameof(AccentBrush), typeof(Brush), typeof(ChamferBorder),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty BorderThicknessProperty = DependencyProperty.Register(
        nameof(BorderThickness), typeof(double), typeof(ChamferBorder),
        new FrameworkPropertyMetadata(1.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ChamferProperty = DependencyProperty.Register(
        nameof(Chamfer), typeof(double), typeof(ChamferBorder),
        new FrameworkPropertyMetadata(10.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty PaddingProperty = DependencyProperty.Register(
        nameof(Padding), typeof(Thickness), typeof(ChamferBorder),
        new FrameworkPropertyMetadata(default(Thickness), FrameworkPropertyMetadataOptions.AffectsMeasure));

    public Brush? Background
    {
        get => (Brush?)GetValue(BackgroundProperty);
        set => SetValue(BackgroundProperty, value);
    }

    public Brush? BorderBrush
    {
        get => (Brush?)GetValue(BorderBrushProperty);
        set => SetValue(BorderBrushProperty, value);
    }

    public Brush? AccentBrush
    {
        get => (Brush?)GetValue(AccentBrushProperty);
        set => SetValue(AccentBrushProperty, value);
    }

    public double BorderThickness
    {
        get => (double)GetValue(BorderThicknessProperty);
        set => SetValue(BorderThicknessProperty, value);
    }

    public double Chamfer
    {
        get => (double)GetValue(ChamferProperty);
        set => SetValue(ChamferProperty, value);
    }

    public Thickness Padding
    {
        get => (Thickness)GetValue(PaddingProperty);
        set => SetValue(PaddingProperty, value);
    }

    protected override Size MeasureOverride(Size constraint)
    {
        var pad = Padding;
        var inner = new Size(
            Math.Max(0, constraint.Width - pad.Left - pad.Right),
            Math.Max(0, constraint.Height - pad.Top - pad.Bottom));
        Child?.Measure(inner);
        var desired = Child?.DesiredSize ?? default;
        return new Size(desired.Width + pad.Left + pad.Right, desired.Height + pad.Top + pad.Bottom);
    }

    protected override Size ArrangeOverride(Size arrangeSize)
    {
        var pad = Padding;
        Child?.Arrange(new Rect(
            pad.Left,
            pad.Top,
            Math.Max(0, arrangeSize.Width - pad.Left - pad.Right),
            Math.Max(0, arrangeSize.Height - pad.Top - pad.Bottom)));
        return arrangeSize;
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        var w = ActualWidth;
        var h = ActualHeight;
        if (w <= 0 || h <= 0)
        {
            return;
        }

        var c = Math.Min(Chamfer, Math.Min(w, h) / 3);
        var half = BorderThickness / 2;
        var outline = new StreamGeometry();
        using (var g = outline.Open())
        {
            g.BeginFigure(new Point(c + half, half), isFilled: true, isClosed: true);
            g.LineTo(new Point(w - half, half), isStroked: true, isSmoothJoin: false);
            g.LineTo(new Point(w - half, h - c - half), isStroked: true, isSmoothJoin: false);
            g.LineTo(new Point(w - c - half, h - half), isStroked: true, isSmoothJoin: false);
            g.LineTo(new Point(half, h - half), isStroked: true, isSmoothJoin: false);
            g.LineTo(new Point(half, c + half), isStroked: true, isSmoothJoin: false);
        }

        outline.Freeze();
        var pen = BorderBrush is null || BorderThickness <= 0 ? null : new Pen(BorderBrush, BorderThickness);
        drawingContext.DrawGeometry(Background, pen, outline);

        if (AccentBrush is not null && c > 0)
        {
            var accent = new Pen(AccentBrush, 2) { StartLineCap = PenLineCap.Flat, EndLineCap = PenLineCap.Flat };
            drawingContext.DrawLine(accent, new Point(1, c + 1), new Point(c + 1, 1));
            drawingContext.DrawLine(accent, new Point(w - c - 1, h - 1), new Point(w - 1, h - c - 1));
        }
    }
}
