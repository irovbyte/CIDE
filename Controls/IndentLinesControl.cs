using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace CIDE.Controls;

public class IndentLinesControl : Control
{
    public static readonly StyledProperty<int> DepthProperty =
        AvaloniaProperty.Register<IndentLinesControl, int>(nameof(Depth), 0);

    public static readonly StyledProperty<double> IndentWidthProperty =
        AvaloniaProperty.Register<IndentLinesControl, double>(nameof(IndentWidth), 16.0);

    public static readonly StyledProperty<IBrush> LineColorProperty =
        AvaloniaProperty.Register<IndentLinesControl, IBrush>(
            nameof(LineColor),
            new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)));

    public static readonly StyledProperty<IBrush> ActiveLineColorProperty =
        AvaloniaProperty.Register<IndentLinesControl, IBrush>(
            nameof(ActiveLineColor),
            new SolidColorBrush(Color.FromArgb(100, 156, 39, 176)));

    public static readonly StyledProperty<bool> IsSelectedProperty =
        AvaloniaProperty.Register<IndentLinesControl, bool>(nameof(IsSelected), false);

    public int Depth
    {
        get => GetValue(DepthProperty);
        set => SetValue(DepthProperty, value);
    }

    public double IndentWidth
    {
        get => GetValue(IndentWidthProperty);
        set => SetValue(IndentWidthProperty, value);
    }

    public IBrush LineColor
    {
        get => GetValue(LineColorProperty);
        set => SetValue(LineColorProperty, value);
    }

    public IBrush ActiveLineColor
    {
        get => GetValue(ActiveLineColorProperty);
        set => SetValue(ActiveLineColorProperty, value);
    }

    public bool IsSelected
    {
        get => GetValue(IsSelectedProperty);
        set => SetValue(IsSelectedProperty, value);
    }
    static IndentLinesControl()
    {
        AffectsRender<IndentLinesControl>(
            DepthProperty,
            IndentWidthProperty,
            LineColorProperty,
            ActiveLineColorProperty,
            IsSelectedProperty);
    }
    protected override Size MeasureOverride(Size availableSize)
        => new(Depth * IndentWidth, 0);
    public override void Render(DrawingContext context)
    {
        if (Depth <= 0)
        {
            return;
        }

        var height = Bounds.Height;
        if (height <= 0)
        {
            height = 22;
        }

        for (var i = 0; i < Depth; i++)
        {
            var x = (i * IndentWidth) + (IndentWidth / 2);
            var brush = (i == Depth - 1 && IsSelected)
                ? ActiveLineColor
                : LineColor;

            var pen = new Pen(brush, 1.0);

            context.DrawLine(pen,
                new Point(x, 0),
                new Point(x, height));
        }
    }
}
