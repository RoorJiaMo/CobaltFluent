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

        var min = values.Min();
        var max = values.Max();
        var range = max - min;
        // 全平的序列画成中线，别让它贴着底边
        if (range <= 0) range = 1;

        var w = Bounds.Width;
        var h = Bounds.Height;
        const double inset = 2;   // 线宽的一半，留出来免得顶部被切

        Point At(int i) => new(
            w * i / (values.Count - 1.0),
            inset + (h - inset * 2) * (1 - (values[i] - min) / range));

        if (ShowArea)
        {
            var area = new StreamGeometry();
            using (var ctx = area.Open())
            {
                ctx.BeginFigure(new Point(0, h), true);
                for (var i = 0; i < values.Count; i++) ctx.LineTo(At(i));
                ctx.LineTo(new Point(w, h));
                ctx.EndFigure(true);
            }

            var fill = new SolidColorBrush(
                (brush as ISolidColorBrush)?.Color ?? Colors.SteelBlue, 0.12);
            context.DrawGeometry(fill, null, area);
        }

        var line = new StreamGeometry();
        using (var ctx = line.Open())
        {
            ctx.BeginFigure(At(0), false);
            for (var i = 1; i < values.Count; i++) ctx.LineTo(At(i));
            ctx.EndFigure(false);
        }

        context.DrawGeometry(null, new Pen(brush, 1.25)
        {
            LineJoin = PenLineJoin.Round,
            LineCap = PenLineCap.Round,
        }, line);
    }

    /// <summary>见 <see cref="Cobalt.Fluent.Automation.SparklineAutomationPeer"/>。</summary>
    protected override AutomationPeer OnCreateAutomationPeer() => new SparklineAutomationPeer(this);
}
