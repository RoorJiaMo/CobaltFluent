using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Media;
using Cobalt.Fluent.Automation;

namespace Cobalt.Fluent.Controls;

/// <summary>迷你趋势的语义着色。</summary>
public enum SparklineTrend
{
    /// <summary>中性，用系列色 1。</summary>
    Neutral,

    /// <summary>上行，绿色。</summary>
    Up,

    /// <summary>下行，红色。</summary>
    Down,
}

/// <summary>
/// 嵌在表格单元格里的迷你趋势。72×20，**无轴无标签** ——
/// 它的作用是让人一眼看出形状，不是读数。要读数就该用 Readout。
/// </summary>
[PseudoClasses(":up", ":down")]
public class Sparkline : Control
{
    public static readonly StyledProperty<IReadOnlyList<double>> ValuesProperty =
        AvaloniaProperty.Register<Sparkline, IReadOnlyList<double>>(nameof(Values), []);

    public IReadOnlyList<double> Values
    {
        get => GetValue(ValuesProperty);
        set => SetValue(ValuesProperty, value);
    }

    public static readonly StyledProperty<SparklineTrend> TrendProperty =
        AvaloniaProperty.Register<Sparkline, SparklineTrend>(nameof(Trend));

    public SparklineTrend Trend
    {
        get => GetValue(TrendProperty);
        set => SetValue(TrendProperty, value);
    }

    /// <summary>曲线下方的淡填充。</summary>
    public static readonly StyledProperty<bool> ShowAreaProperty =
        AvaloniaProperty.Register<Sparkline, bool>(nameof(ShowArea), true);

    public bool ShowArea
    {
        get => GetValue(ShowAreaProperty);
        set => SetValue(ShowAreaProperty, value);
    }

    static Sparkline()
    {
        AffectsRender<Sparkline>(ValuesProperty, TrendProperty, ShowAreaProperty);
        TrendProperty.Changed.AddClassHandler<Sparkline>((x, _) => x.Refresh());
    }

    public Sparkline()
    {
        Width = 72;
        Height = 20;
    }

    private void Refresh()
    {
        PseudoClasses.Set(":up", Trend == SparklineTrend.Up);
        PseudoClasses.Set(":down", Trend == SparklineTrend.Down);
    }

    protected override Size MeasureOverride(Size availableSize) => new(72, 20);

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var values = Values;
        if (values.Count < 2) return;

        var brush = Trend switch
        {
            SparklineTrend.Up => ChartPalette.Resolve(this, "SystemFillColorSuccessBrush"),
            SparklineTrend.Down => ChartPalette.Resolve(this, "SystemFillColorCriticalBrush"),
            _ => ChartPalette.SeriesBrush(this, 1),
        } ?? Brushes.SteelBlue;

        // 极值只看有效值——一个 NaN 会让 values.Min()/Max() 连同整条线一起塌掉。
        // 见 ChartSampling.FiniteExtent。
        if (ChartSampling.FiniteExtent(values, out var min, out var max) < 2) return;

        var range = max - min;
        // 全平的序列画成中线，别让它贴着底边。取反写，NaN 也走这条。
        if (!(range > 0)) range = 1;

        var w = Bounds.Width;
        var h = Bounds.Height;
        const double inset = 2;   // 线宽的一半，留出来免得顶部被切

        Point At(int index, double value) => new(
            w * index / (values.Count - 1.0),
            inset + (h - inset * 2) * (1 - (value - min) / range));

        // 抽稀。72 px 宽的格子里塞一个班次的 28800 点，每像素列摊到 400 个顶点，
        // 而表格里往往同时有几十个 Sparkline。见 ChartSampling.Decimate。
        ChartSampling.Decimate(values, (int)w, _samples);

        if (ShowArea)
        {
            var area = new StreamGeometry();
            using (var ctx = area.Open())
            {
                var open = false;
                var last = new Point(0, h);

                foreach (var (index, value) in _samples)
                {
                    // 断点处把这一段的面积收口，下一段重新起。整条连过去的话，
                    // 中断期间会填出一块和数据无关的色块。
                    if (double.IsNaN(value))
                    {
                        if (open) { ctx.LineTo(new Point(last.X, h)); ctx.EndFigure(true); open = false; }
                        continue;
                    }

                    last = At(index, value);
                    if (!open) { ctx.BeginFigure(new Point(last.X, h), true); open = true; }
                    ctx.LineTo(last);
                }

                if (open) { ctx.LineTo(new Point(last.X, h)); ctx.EndFigure(true); }
            }

            var fill = new SolidColorBrush(
                (brush as ISolidColorBrush)?.Color ?? Colors.SteelBlue, 0.12);
            context.DrawGeometry(fill, null, area);
        }

        var line = new StreamGeometry();
        using (var ctx = line.Open())
        {
            var open = false;
            foreach (var (index, value) in _samples)
            {
                if (double.IsNaN(value))
                {
                    if (open) { ctx.EndFigure(false); open = false; }
                    continue;
                }

                var point = At(index, value);
                if (open) ctx.LineTo(point);
                else { ctx.BeginFigure(point, false); open = true; }
            }

            if (open) ctx.EndFigure(false);
        }

        context.DrawGeometry(null, new Pen(brush, 1.25)
        {
            LineJoin = PenLineJoin.Round,
            LineCap = PenLineCap.Round,
        }, line);
    }

    /// <summary>抽稀输出缓冲。每帧复用，别在渲染路径上分配。</summary>
    private readonly List<(int Index, double Value)> _samples = [];

    /// <summary>见 <see cref="Cobalt.Fluent.Automation.SparklineAutomationPeer"/>。</summary>
    protected override AutomationPeer OnCreateAutomationPeer() => new SparklineAutomationPeer(this);
}
