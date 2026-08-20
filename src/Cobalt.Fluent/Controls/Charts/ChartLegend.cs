using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace Cobalt.Fluent.Controls;

/// <summary>
/// 图表图例。
///
/// **当前值由图例承载，不逐线标在曲线末端** —— 四个通道末值可能非常接近
/// （84.6 / 84.6 / 84.9），右边缘逐线标注必然叠在一起。
/// 图例跟着十字线实时更新：没有十字线时显示末值，有十字线时显示该时刻的值。
///
/// 点图例项可以把对应曲线隐藏。
/// </summary>
public class ChartLegend : TemplatedControl
{
    private WrapPanel? _panel;

    public static readonly StyledProperty<TrendChart?> ChartProperty =
        AvaloniaProperty.Register<ChartLegend, TrendChart?>(nameof(Chart));

    public TrendChart? Chart
    {
        get => GetValue(ChartProperty);
        set => SetValue(ChartProperty, value);
    }

    public static readonly StyledProperty<string> ValueFormatProperty =
        AvaloniaProperty.Register<ChartLegend, string>(nameof(ValueFormat), "F1");

    public string ValueFormat
    {
        get => GetValue(ValueFormatProperty);
        set => SetValue(ValueFormatProperty, value);
    }

    static ChartLegend()
    {
        ChartProperty.Changed.AddClassHandler<ChartLegend>((x, e) => x.OnChartChanged(e));
    }

    private void OnChartChanged(AvaloniaPropertyChangedEventArgs e)
    {
        if (e.OldValue is TrendChart old)
            old.PropertyChanged -= OnChartPropertyChanged;

        if (e.NewValue is TrendChart now)
            now.PropertyChanged += OnChartPropertyChanged;

        Rebuild();
    }

    private void OnChartPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        // 十字线一动就刷新数值；系列换了要整块重建
        if (e.Property == TrendChart.TrackballIndexProperty) Rebuild();
        else if (e.Property == TrendChart.SeriesProperty) Rebuild();
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _panel = e.NameScope.Find<WrapPanel>("PART_Items");
        Rebuild();
    }

    private void Rebuild()
    {
        if (_panel is null) return;

        _panel.Children.Clear();
        if (Chart is not { } chart) return;

        var index = chart.TrackballIndex;

        foreach (var series in chart.Series)
        {
            var brush = series.Brush ?? ChartPalette.SeriesBrush(this, series.PaletteIndex);

            // 没有十字线时显示末值 —— 这是操作员最常想知道的那个数
            var value = series.Values.Count == 0
                ? null
                : (double?)series.Values[Math.Clamp(index ?? series.Values.Count - 1, 0, series.Values.Count - 1)];

            var swatch = new Border
            {
                Width = 10,
                Height = 3,
                CornerRadius = new CornerRadius(1.5),
                Background = brush,
                VerticalAlignment = VerticalAlignment.Center,
            };

            var name = new TextBlock
            {
                Text = series.Name,
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = ChartPalette.Resolve(this, "TextFillColorSecondaryBrush"),
            };

            var reading = new TextBlock
            {
                Text = value?.ToString(ValueFormat, System.Globalization.CultureInfo.CurrentCulture) ?? "—",
                FontSize = 12,
                FontWeight = FontWeight.SemiBold,
                MinWidth = 44,
                TextAlignment = TextAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = ChartPalette.Resolve(this, "TextFillColorPrimaryBrush"),
            };

            if (this.TryFindResource("TabularNumbers", ActualThemeVariant, out var tnum)
                && tnum is FontFeatureCollection features)
            {
                reading.FontFeatures = features;
            }

            var item = new Border
            {
                Padding = new Thickness(0, 2),
                Margin = new Thickness(0, 0, 16, 4),
                Background = Brushes.Transparent,
                Opacity = series.IsHidden ? 0.4 : 1,
                Cursor = new Cursor(StandardCursorType.Arrow),
                Child = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 6,
                    Children = { swatch, name, reading },
                },
            };

            var captured = series;
            item.PointerReleased += (_, _) =>
            {
                // 点一下把该系列藏起来 / 显示回来
                captured.IsHidden = !captured.IsHidden;
                Chart?.InvalidateVisual();
                Rebuild();
            };

            _panel.Children.Add(item);
        }
    }
}
