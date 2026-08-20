using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;

namespace Cobalt.Fluent.Controls;

/// <summary>设备状态。顺序即严重程度递增。</summary>
public enum DeviceState
{
    /// <summary>未连接。空心圈——「没有信息」和「一切正常」必须长得不一样。</summary>
    Offline,

    /// <summary>待机。实心灰点。</summary>
    Idle,

    /// <summary>运行中。绿点 + 脉冲环。</summary>
    Running,

    /// <summary>参数偏离，还能继续跑。</summary>
    Warning,

    /// <summary>故障停机。</summary>
    Fault,
}

/// <summary>
/// 状态指示灯。
///
/// **三重编码：颜色 + 形状/动效 + 文字。任何一种单独都不够。**
/// 男性约 8% 有色觉障碍，强光下的工业屏幕颜色也会失真；
/// 所以 offline 是空心圈、running 带脉冲环、warning/fault 各有自己的字形，
/// 不能只靠红黄绿区分。
///
/// 嵌入式注意：一屏十几个 running 指示灯就是十几个并发动画，
/// Mali 这类 GPU 上会掉帧。那种场合把 <see cref="IsPulseEnabled"/> 关掉，
/// 换成静态外环——形状编码还在，只是不动。
/// </summary>
[PseudoClasses(":offline", ":idle", ":running", ":warning", ":fault")]
public class StatusIndicator : TemplatedControl
{
    public static readonly StyledProperty<DeviceState> StateProperty =
        AvaloniaProperty.Register<StatusIndicator, DeviceState>(nameof(State), DeviceState.Idle);

    public DeviceState State
    {
        get => GetValue(StateProperty);
        set => SetValue(StateProperty, value);
    }

    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<StatusIndicator, string?>(nameof(Label));

    public string? Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public static readonly StyledProperty<bool> ShowLabelProperty =
        AvaloniaProperty.Register<StatusIndicator, bool>(nameof(ShowLabel), true);

    public bool ShowLabel
    {
        get => GetValue(ShowLabelProperty);
        set => SetValue(ShowLabelProperty, value);
    }

    /// <summary>
    /// running 态的脉冲环动画。嵌入式上一屏多个指示灯时建议关掉。
    /// 关掉后外环变成静态的，形状编码不丢。
    /// </summary>
    public static readonly StyledProperty<bool> IsPulseEnabledProperty =
        AvaloniaProperty.Register<StatusIndicator, bool>(nameof(IsPulseEnabled), true);

    public bool IsPulseEnabled
    {
        get => GetValue(IsPulseEnabledProperty);
        set => SetValue(IsPulseEnabledProperty, value);
    }

    private Symbol _glyph = Symbol.None;

    public static readonly DirectProperty<StatusIndicator, Symbol> GlyphProperty =
        AvaloniaProperty.RegisterDirect<StatusIndicator, Symbol>(nameof(Glyph), o => o._glyph);

    /// <summary>该状态的非颜色信号：warning 是三角感叹号，fault 是圈叉，其余没有字形。</summary>
    public Symbol Glyph
    {
        get => _glyph;
        private set => SetAndRaise(GlyphProperty, ref _glyph, value);
    }

    static StatusIndicator()
    {
        StateProperty.Changed.AddClassHandler<StatusIndicator>((x, _) => x.OnStateChanged());
    }

    public StatusIndicator() => OnStateChanged();

    private void OnStateChanged()
    {
        var state = State;
        PseudoClasses.Set(":offline", state == DeviceState.Offline);
        PseudoClasses.Set(":idle", state == DeviceState.Idle);
        PseudoClasses.Set(":running", state == DeviceState.Running);
        PseudoClasses.Set(":warning", state == DeviceState.Warning);
        PseudoClasses.Set(":fault", state == DeviceState.Fault);

        Glyph = state switch
        {
            DeviceState.Warning => Symbol.Warning,
            DeviceState.Fault => Symbol.Error,
            _ => Symbol.None,
        };
    }
}
