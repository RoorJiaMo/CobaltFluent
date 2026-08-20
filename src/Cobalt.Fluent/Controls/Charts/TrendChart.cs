using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Metadata;

namespace Cobalt.Fluent.Controls;

/// <summary>
/// 趋势图。自绘，不依赖图表库 —— 单通道 / 少通道的 strip chart 这样最轻，
/// RK3568 这类板子上值得这么做。数据量真的很大时再换 ScottPlot / LiveCharts2。
///
/// **十字线是 trackball 模式**：跟随指针的 X 坐标，同时给出所有系列在该时刻的值，
/// 不是 hover 最近点。触摸屏上没有 hover，鼠标场景逐点 hover 也太累。
///
/// 坐标轴单位不画在绘图区里 —— 画进去的话 °C 会压在最上一条刻度上、
/// 秒会压在末位刻度上。单位放抬头（<see cref="ChartFrame"/> 的副标题）。
/// </summary>
public class TrendChart : Control
{
    private const double LeftGutter = 44;
    private const double BottomGutter = 22;
    private const double TopPad = 8;
    private const double RightPad = 8;

    public static readonly StyledProperty<AvaloniaList<ChartSeries>> SeriesProperty =
        AvaloniaProperty.Register<TrendChart, AvaloniaList<ChartSeries>>(nameof(Series));

    [Content]
    public AvaloniaList<ChartSeries> Series
    {
        get => GetValue(SeriesProperty);
        set => SetValue(SeriesProperty, value);
    }

    public static readonly StyledProperty<double> YMinimumProperty =
        AvaloniaProperty.Register<TrendChart, double>(nameof(YMinimum), 0d);

    public double YMinimum
    {
        get => GetValue(YMinimumProperty);
        set => SetValue(YMinimumProperty, value);
    }

    public static readonly StyledProperty<double> YMaximumProperty =
        AvaloniaProperty.Register<TrendChart, double>(nameof(YMaximum), 100d);

    public double YMaximum
    {
        get => GetValue(YMaximumProperty);
        set => SetValue(YMaximumProperty, value);
    }

    /// <summary>Y 轴刻度条数（含首尾）。</summary>
    public static readonly StyledProperty<int> YTickCountProperty =
        AvaloniaProperty.Register<TrendChart, int>(nameof(YTickCount), 5);

    public int YTickCount
    {
        get => GetValue(YTickCountProperty);
        set => SetValue(YTickCountProperty, value);
    }

    public static readonly StyledProperty<string> YFormatProperty =
        AvaloniaProperty.Register<TrendChart, string>(nameof(YFormat), "F0");

    public string YFormat
    {
        get => GetValue(YFormatProperty);
        set => SetValue(YFormatProperty, value);
    }

    public static readonly StyledProperty<IReadOnlyList<string>> XLabelsProperty =
        AvaloniaProperty.Register<TrendChart, IReadOnlyList<string>>(nameof(XLabels), []);

    public IReadOnlyList<string> XLabels
    {
        get => GetValue(XLabelsProperty);
        set => SetValue(XLabelsProperty, value);
    }

    public static readonly StyledProperty<double?> SetpointProperty =
        AvaloniaProperty.Register<TrendChart, double?>(nameof(Setpoint));

    public double? Setpoint
    {
        get => GetValue(SetpointProperty);
        set => SetValue(SetpointProperty, value);
    }

    /// <summary>容差带半宽。给了就在设定值上下各画半个带（3.5% 填充）。</summary>
    public static readonly StyledProperty<double> ToleranceProperty =
        AvaloniaProperty.Register<TrendChart, double>(nameof(Tolerance));

    public double Tolerance
    {
        get => GetValue(ToleranceProperty);
        set => SetValue(ToleranceProperty, value);
    }

    public static readonly StyledProperty<double?> AlarmHighProperty =
        AvaloniaProperty.Register<TrendChart, double?>(nameof(AlarmHigh));

    public double? AlarmHigh
    {
        get => GetValue(AlarmHighProperty);
        set => SetValue(AlarmHighProperty, value);
    }

    public static readonly StyledProperty<double?> AlarmLowProperty =
        AvaloniaProperty.Register<TrendChart, double?>(nameof(AlarmLow));

    public double? AlarmLow
    {
        get => GetValue(AlarmLowProperty);
        set => SetValue(AlarmLowProperty, value);
    }

    /// <summary>报警上限标签。画在绘图区内左上，不压刻度。</summary>
    public static readonly StyledProperty<string?> AlarmHighLabelProperty =
        AvaloniaProperty.Register<TrendChart, string?>(nameof(AlarmHighLabel));

    public string? AlarmHighLabel
    {
        get => GetValue(AlarmHighLabelProperty);
        set => SetValue(AlarmHighLabelProperty, value);
    }

    public static readonly StyledProperty<bool> IsTrackballEnabledProperty =
        AvaloniaProperty.Register<TrendChart, bool>(nameof(IsTrackballEnabled), true);

    public bool IsTrackballEnabled
    {
        get => GetValue(IsTrackballEnabledProperty);
        set => SetValue(IsTrackballEnabledProperty, value);
    }

    private int? _trackballIndex;

    public static readonly DirectProperty<TrendChart, int?> TrackballIndexProperty =
        AvaloniaProperty.RegisterDirect<TrendChart, int?>(
            nameof(TrackballIndex), o => o._trackballIndex);

    /// <summary>十字线当前落在第几个采样点上。没有十字线时是 null。图例绑它显示实时值。</summary>
    public int? TrackballIndex
    {
        get => _trackballIndex;
        private set => SetAndRaise(TrackballIndexProperty, ref _trackballIndex, value);
    }

    static TrendChart()
    {
        AffectsRender<TrendChart>(
            SeriesProperty, YMinimumProperty, YMaximumProperty, YTickCountProperty,
            XLabelsProperty, SetpointProperty, ToleranceProperty,
            AlarmHighProperty, AlarmLowProperty, TrackballIndexProperty);
    }

    public TrendChart()
    {
        Series = [];
        ClipToBounds = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        MoveTrackballTo(e.GetPosition(this));
    }

    /// <summary>
    /// 把十字线挪到某个点（控件坐标系）。点在绘图区外就清掉十字线。
    ///
    /// 单拎出来是为了：一是可测（不用伪造指针事件），
    /// 二是键盘/编码器也能驱动十字线 —— 工业面板上不一定有鼠标。
    /// </summary>
    public void MoveTrackballTo(Point point)
    {
        if (!IsTrackballEnabled)
        {
            TrackballIndex = null;
            return;
        }

        var plot = PlotRect();
        var count = MaxSampleCount();

        if (count < 2 || !plot.Contains(point))
        {
            TrackballIndex = null;
            return;
        }

        // **只看 X**。这就是 trackball 和「hover 最近点」的区别：
        // 同一个 X、不同的 Y 必须落在同一个采样点上。
        var t = (point.X - plot.X) / plot.Width;
        TrackballIndex = Math.Clamp((int)Math.Round(t * (count - 1)), 0, count - 1);
    }

    /// <summary>清掉十字线。</summary>
    public void ClearTrackball() => TrackballIndex = null;

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        ClearTrackball();
    }

    private int MaxSampleCount() =>
        Series.Count == 0 ? 0 : Series.Max(s => s.Values.Count);

    private Rect PlotRect() => new(
        LeftGutter, TopPad,
        Math.Max(0, Bounds.Width - LeftGutter - RightPad),
        Math.Max(0, Bounds.Height - TopPad - BottomGutter));

    private double YToPixel(double value, Rect plot)
    {
        var range = YMaximum - YMinimum;
        if (range <= 0) return plot.Bottom;
        var t = (value - YMinimum) / range;
        return plot.Bottom - t * plot.Height;
    }

    private double IndexToPixel(int index, int count, Rect plot) =>
        count < 2 ? plot.X : plot.X + plot.Width * index / (count - 1.0);

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var plot = PlotRect();
        if (plot.Width <= 0 || plot.Height <= 0) return;

        var grid = ChartPalette.Resolve(this, "ChartGridLineBrush")
                   ?? ChartPalette.Resolve(this, "DividerStrokeColorDefaultBrush");
        var axis = ChartPalette.Resolve(this, "ChartAxisLineBrush")
                   ?? ChartPalette.Resolve(this, "TextFillColorTertiaryBrush");
        var labelBrush = ChartPalette.Resolve(this, "TextFillColorTertiaryBrush") ?? Brushes.Gray;
        var band = ChartPalette.Resolve(this, "ChartBandBrush");

        var gridPen = new Pen(grid ?? Brushes.LightGray, 1);
        var axisPen = new Pen(axis ?? Brushes.Gray, 1);

        // --- 容差带（画在最底下，别盖住曲线） ---
        if (band is not null && Setpoint is { } sp && Tolerance > 0)
        {
            var top = YToPixel(sp + Tolerance, plot);
            var bottom = YToPixel(sp - Tolerance, plot);
            context.FillRectangle(band, new Rect(plot.X, top, plot.Width, Math.Max(0, bottom - top)));
        }

        // --- 网格 + Y 轴刻度 ---
        var ticks = Math.Max(2, YTickCount);
        for (var i = 0; i < ticks; i++)
        {
            var value = YMinimum + (YMaximum - YMinimum) * i / (ticks - 1.0);
            var y = YToPixel(value, plot);

            context.DrawLine(gridPen, new Point(plot.X, y), new Point(plot.Right, y));

            var text = FormatText(
                value.ToString(YFormat, System.Globalization.CultureInfo.CurrentCulture),
                labelBrush);
            context.DrawText(text, new Point(plot.X - 8 - text.Width, y - text.Height / 2));
        }

        // --- 轴线 ---
        context.DrawLine(axisPen, new Point(plot.X, plot.Y), new Point(plot.X, plot.Bottom));
        context.DrawLine(axisPen, new Point(plot.X, plot.Bottom), new Point(plot.Right, plot.Bottom));

        // --- 设定值 / 报警限 ---
        DrawLevel(context, Setpoint, ChartLineStyle.Setpoint, plot);
        DrawLevel(context, AlarmHigh, ChartLineStyle.Limit, plot);
        DrawLevel(context, AlarmLow, ChartLineStyle.Limit, plot);

        // 报警上限标签放绘图区内左上 —— 放右边会和末值标注叠在一起
        if (AlarmHigh is { } high && !string.IsNullOrEmpty(AlarmHighLabel))
        {
            var critical = ChartPalette.Resolve(this, "SystemFillColorCriticalBrush") ?? Brushes.Red;
            var text = FormatText(AlarmHighLabel!, critical);
            context.DrawText(text, new Point(plot.X + 6, YToPixel(high, plot) + 3));
        }

        // --- X 轴标签 ---
        DrawXLabels(context, plot, labelBrush);

        // --- 曲线 ---
        var count = MaxSampleCount();
        foreach (var series in Series)
        {
            if (series.IsHidden || series.Values.Count < 2) continue;

            var pen = ChartPalette.PenFor(this, series);
            var geometry = new StreamGeometry();
            using (var ctx = geometry.Open())
            {
                for (var i = 0; i < series.Values.Count; i++)
                {
                    var point = new Point(
                        IndexToPixel(i, count, plot),
                        YToPixel(series.Values[i], plot));

                    if (i == 0) ctx.BeginFigure(point, false);
                    else ctx.LineTo(point);
                }

                ctx.EndFigure(false);
            }

            context.DrawGeometry(null, pen, geometry);
        }

        // --- 十字线 ---
        DrawTrackball(context, plot, count);
    }

    private void DrawLevel(DrawingContext context, double? value, ChartLineStyle style, Rect plot)
    {
        if (value is not { } v) return;
        if (v < YMinimum || v > YMaximum) return;

        var pen = ChartPalette.PenFor(this, new ChartSeries { LineStyle = style });
        var y = YToPixel(v, plot);
        context.DrawLine(pen, new Point(plot.X, y), new Point(plot.Right, y));
    }

    private void DrawXLabels(DrawingContext context, Rect plot, IBrush brush)
    {
        if (XLabels.Count == 0) return;

        // 标签太密就抽稀，宁可少画也不能叠字
        var step = Math.Max(1, (int)Math.Ceiling(XLabels.Count / Math.Max(1, plot.Width / 56)));

        for (var i = 0; i < XLabels.Count; i += step)
        {
            var text = FormatText(XLabels[i], brush);
            var x = IndexToPixel(i, XLabels.Count, plot);

            // 首尾贴边对齐，中间居中 —— 否则第一个和最后一个会溢出绘图区
            var left = i == 0 ? x
                : i >= XLabels.Count - step ? x - text.Width
                : x - text.Width / 2;

            context.DrawText(text, new Point(left, plot.Bottom + 5));
        }
    }

    private void DrawTrackball(DrawingContext context, Rect plot, int count)
    {
        if (TrackballIndex is not { } index || count < 2) return;

        var crossBrush = ChartPalette.Resolve(this, "TextFillColorSecondaryBrush") ?? Brushes.Gray;
        var crossPen = new Pen(crossBrush, 1, new DashStyle([2, 2], 0));
        var x = IndexToPixel(index, count, plot);

        context.DrawLine(crossPen, new Point(x, plot.Y), new Point(x, plot.Bottom));

        // 每条曲线在该时刻的取值点上打一个实心圆
        foreach (var series in Series)
        {
            if (series.IsHidden || index >= series.Values.Count) continue;

            var brush = series.Brush ?? ChartPalette.SeriesBrush(this, series.PaletteIndex);
            var y = YToPixel(series.Values[index], plot);
            context.DrawEllipse(brush, null, new Point(x, y), 3, 3);
        }
    }

    private FormattedText FormatText(string text, IBrush brush) => new(
        text,
        System.Globalization.CultureInfo.CurrentCulture,
        FlowDirection.LeftToRight,
        new Typeface(TextElement.GetFontFamily(this)),
        10,
        brush);
}
