using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Avalonia.Metadata;

namespace Cobalt.Fluent.Controls;

/// <summary>一组柱子。</summary>
public class BarSeries : AvaloniaObject
{
    public static readonly StyledProperty<string?> NameProperty =
        AvaloniaProperty.Register<BarSeries, string?>(nameof(Name));

    public string? Name
    {
        get => GetValue(NameProperty);
        set => SetValue(NameProperty, value);
    }

    public static readonly StyledProperty<IReadOnlyList<double>> ValuesProperty =
        AvaloniaProperty.Register<BarSeries, IReadOnlyList<double>>(nameof(Values), []);

    public IReadOnlyList<double> Values
    {
        get => GetValue(ValuesProperty);
        set => SetValue(ValuesProperty, value);
    }

    public static readonly StyledProperty<int> PaletteIndexProperty =
        AvaloniaProperty.Register<BarSeries, int>(nameof(PaletteIndex), 1);

    public int PaletteIndex
    {
        get => GetValue(PaletteIndexProperty);
        set => SetValue(PaletteIndexProperty, value);
    }
}

/// <summary>柱状图。分组柱，横轴是类别。自绘，不依赖图表库。</summary>
public class BarChart : Control
{
    private const double LeftGutter = 44;
    private const double BottomGutter = 22;
    private const double TopPad = 8;
    private const double RightPad = 8;

    public static readonly StyledProperty<AvaloniaList<BarSeries>> SeriesProperty =
        AvaloniaProperty.Register<BarChart, AvaloniaList<BarSeries>>(nameof(Series));

    [Content]
    public AvaloniaList<BarSeries> Series
    {
        get => GetValue(SeriesProperty);
        set => SetValue(SeriesProperty, value);
    }

    public static readonly StyledProperty<IReadOnlyList<string>> CategoriesProperty =
        AvaloniaProperty.Register<BarChart, IReadOnlyList<string>>(nameof(Categories), []);

    public IReadOnlyList<string> Categories
    {
        get => GetValue(CategoriesProperty);
        set => SetValue(CategoriesProperty, value);
    }

    public static readonly StyledProperty<double> YMaximumProperty =
        AvaloniaProperty.Register<BarChart, double>(nameof(YMaximum), 100d);

    public double YMaximum
    {
        get => GetValue(YMaximumProperty);
        set => SetValue(YMaximumProperty, value);
    }

    public static readonly StyledProperty<int> YTickCountProperty =
        AvaloniaProperty.Register<BarChart, int>(nameof(YTickCount), 5);

    public int YTickCount
    {
        get => GetValue(YTickCountProperty);
        set => SetValue(YTickCountProperty, value);
    }

    static BarChart()
    {
        AffectsRender<BarChart>(SeriesProperty, CategoriesProperty, YMaximumProperty);
    }

    public BarChart()
    {
        Series = [];
        ClipToBounds = true;
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var plot = new Rect(
            LeftGutter, TopPad,
            Math.Max(0, Bounds.Width - LeftGutter - RightPad),
            Math.Max(0, Bounds.Height - TopPad - BottomGutter));

        if (plot.Width <= 0 || plot.Height <= 0) return;

        var gridBrush = ChartPalette.Resolve(this, "ChartGridLineBrush")
                        ?? ChartPalette.Resolve(this, "DividerStrokeColorDefaultBrush");
        var labelBrush = ChartPalette.Resolve(this, "TextFillColorTertiaryBrush") ?? Brushes.Gray;
        var gridPen = new Pen(gridBrush ?? Brushes.LightGray, 1);

        // 网格 + Y 刻度
        var ticks = Math.Max(2, YTickCount);
        for (var i = 0; i < ticks; i++)
        {
            var value = YMaximum * i / (ticks - 1.0);
            var y = plot.Bottom - plot.Height * i / (ticks - 1.0);

            context.DrawLine(gridPen, new Point(plot.X, y), new Point(plot.Right, y));

            var text = Text(value.ToString("F0"), labelBrush);
            context.DrawText(text, new Point(plot.X - 8 - text.Width, y - text.Height / 2));
        }

        var categoryCount = Math.Max(Categories.Count, Series.Count == 0 ? 0 : Series.Max(s => s.Values.Count));
        if (categoryCount == 0) return;

        var slot = plot.Width / categoryCount;
        var groupPad = slot * 0.18;
        var barWidth = Series.Count > 0 ? (slot - groupPad * 2) / Series.Count : slot;

        for (var c = 0; c < categoryCount; c++)
        {
            for (var s = 0; s < Series.Count; s++)
            {
                if (c >= Series[s].Values.Count) continue;

                var value = Series[s].Values[c];
                var height = YMaximum > 0
                    ? plot.Height * Math.Clamp(value / YMaximum, 0, 1)
                    : 0;

                var x = plot.X + slot * c + groupPad + barWidth * s;
                var rect = new Rect(x, plot.Bottom - height, Math.Max(1, barWidth - 2), height);

                context.DrawRectangle(
                    ChartPalette.SeriesBrush(this, Series[s].PaletteIndex), null,
                    new RoundedRect(rect, 2, 2, 0, 0));
            }

            if (c < Categories.Count)
            {
                var text = Text(Categories[c], labelBrush);
                context.DrawText(text, new Point(
                    plot.X + slot * c + (slot - text.Width) / 2, plot.Bottom + 5));
            }
        }

        var axisPen = new Pen(ChartPalette.Resolve(this, "TextFillColorTertiaryBrush") ?? Brushes.Gray, 1);
        context.DrawLine(axisPen, new Point(plot.X, plot.Bottom), new Point(plot.Right, plot.Bottom));
    }

    private FormattedText Text(string text, IBrush brush) => new(
        text, System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
        new Typeface(TextElement.GetFontFamily(this)), 10, brush);
}
