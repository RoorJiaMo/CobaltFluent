using Avalonia;
using Avalonia.Media;

namespace Cobalt.Fluent.Controls;

/// <summary>
/// 曲线之外的信息只能靠线型分层 —— 四种颜色已经被通道占满了。
/// 线型层级：实线 → 虚线 → 点线，同色不同型，黑白打印和色觉障碍下都分得开。
/// </summary>
public enum ChartLineStyle
{
    /// <summary>通道曲线：1.5px 实线，用系列色。</summary>
    Solid,

    /// <summary>设定值：1px 虚线 4-3，tertiary 色。</summary>
    Setpoint,

    /// <summary>报警上下限：1px 虚线 3-3，critical 色，70% 不透明。</summary>
    Limit,
}

/// <summary>
/// 一条曲线。
///
/// <see cref="PaletteIndex"/> 指向 <c>ChartSeries1..8</c> 那八个 token，
/// **刻意避开纯红纯绿**：HMI 里绿=运行、红=故障已经是语义色，
/// 一条绿色曲线会被操作员读成「这条正常」，而它可能正在超温。
/// </summary>
public class ChartSeries : AvaloniaObject
{
    public static readonly StyledProperty<string?> NameProperty =
        AvaloniaProperty.Register<ChartSeries, string?>(nameof(Name));

    public string? Name
    {
        get => GetValue(NameProperty);
        set => SetValue(NameProperty, value);
    }

    public static readonly StyledProperty<IReadOnlyList<double>> ValuesProperty =
        AvaloniaProperty.Register<ChartSeries, IReadOnlyList<double>>(nameof(Values), []);

    public IReadOnlyList<double> Values
    {
        get => GetValue(ValuesProperty);
        set => SetValue(ValuesProperty, value);
    }

    /// <summary>1–8，对应 ChartSeries1..8 这八个 token。</summary>
    public static readonly StyledProperty<int> PaletteIndexProperty =
        AvaloniaProperty.Register<ChartSeries, int>(nameof(PaletteIndex), 1);

    public int PaletteIndex
    {
        get => GetValue(PaletteIndexProperty);
        set => SetValue(PaletteIndexProperty, value);
    }

    public static readonly StyledProperty<ChartLineStyle> LineStyleProperty =
        AvaloniaProperty.Register<ChartSeries, ChartLineStyle>(nameof(LineStyle));

    public ChartLineStyle LineStyle
    {
        get => GetValue(LineStyleProperty);
        set => SetValue(LineStyleProperty, value);
    }

    /// <summary>图例点击可以把某条藏起来。藏起来的仍占图例位，只是变灰。</summary>
    public static readonly StyledProperty<bool> IsHiddenProperty =
        AvaloniaProperty.Register<ChartSeries, bool>(nameof(IsHidden));

    public bool IsHidden
    {
        get => GetValue(IsHiddenProperty);
        set => SetValue(IsHiddenProperty, value);
    }

    /// <summary>显式指定颜色。留空就按 <see cref="PaletteIndex"/> 从 token 里取。</summary>
    public static readonly StyledProperty<IBrush?> BrushProperty =
        AvaloniaProperty.Register<ChartSeries, IBrush?>(nameof(Brush));

    public IBrush? Brush
    {
        get => GetValue(BrushProperty);
        set => SetValue(BrushProperty, value);
    }
}
