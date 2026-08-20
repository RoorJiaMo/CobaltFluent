using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace Cobalt.Fluent.Controls;

/// <summary>
/// 图表外框：卡片底 + 抬头 + 绘图区。
///
/// **坐标轴单位放在抬头（<see cref="Subtitle"/>），不要画进绘图区** ——
/// 画进去的话 °C 会压在最上一条刻度上、秒会压在末位刻度上。
/// </summary>
public class ChartFrame : ContentControl
{
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<ChartFrame, string?>(nameof(Title));

    public string? Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    /// <summary>副标题。单位、量程、采样周期这类信息放这里。</summary>
    public static readonly StyledProperty<string?> SubtitleProperty =
        AvaloniaProperty.Register<ChartFrame, string?>(nameof(Subtitle));

    public string? Subtitle
    {
        get => GetValue(SubtitleProperty);
        set => SetValue(SubtitleProperty, value);
    }

    /// <summary>抬头右侧的操作区。</summary>
    public static readonly StyledProperty<object?> ActionsProperty =
        AvaloniaProperty.Register<ChartFrame, object?>(nameof(Actions));

    public object? Actions
    {
        get => GetValue(ActionsProperty);
        set => SetValue(ActionsProperty, value);
    }

    /// <summary>底部图例区。</summary>
    public static readonly StyledProperty<object?> LegendProperty =
        AvaloniaProperty.Register<ChartFrame, object?>(nameof(Legend));

    public object? Legend
    {
        get => GetValue(LegendProperty);
        set => SetValue(LegendProperty, value);
    }
}
