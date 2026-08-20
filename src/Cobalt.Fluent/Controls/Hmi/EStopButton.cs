using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace Cobalt.Fluent.Controls;

/// <summary>
/// 软件急停。
///
/// **它不能替代硬件急停回路。** 界面上建议同时标注硬件急停的物理位置，
/// 见 <see cref="HardwareLocationHint"/>。软件路径要过 UI 线程、消息队列、通信链路，
/// 任何一环卡住它就不响应；真正的安全回路是硬接线的。
///
/// 行为：
/// - **按下即触发**，不等松手。急停不能有一次松手的延迟。
/// - 触发后自锁：再点无效，必须显式复位。
/// - 复位默认要求长按（<see cref="RequireHoldToReset"/>），对应真实急停"拧一下才能弹起"的手感——
///   防止误碰复位。
///
/// 视觉上刻意打破 Fluent 的扁平：真实急停是物理蘑菇头，要有实体感。
/// 颜色写死安全红 + 安全黄（ISO 13850 要求红钮黄衬），
/// **不跟随主题**，也不占"一屏一个强调色"的名额。
/// </summary>
[PseudoClasses(":engaged", ":resetting")]
public class EStopButton : Button
{
    private DispatcherTimer? _resetHold;

    public static readonly StyledProperty<bool> IsEngagedProperty =
        AvaloniaProperty.Register<EStopButton, bool>(
            nameof(IsEngaged), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    /// <summary>是否已触发并锁定。</summary>
    public bool IsEngaged
    {
        get => GetValue(IsEngagedProperty);
        set => SetValue(IsEngagedProperty, value);
    }

    public static readonly StyledProperty<ICommand?> EngageCommandProperty =
        AvaloniaProperty.Register<EStopButton, ICommand?>(nameof(EngageCommand));

    /// <summary>触发急停。**下游必须做成幂等且不可失败的。**</summary>
    public ICommand? EngageCommand
    {
        get => GetValue(EngageCommandProperty);
        set => SetValue(EngageCommandProperty, value);
    }

    public static readonly StyledProperty<ICommand?> ResetCommandProperty =
        AvaloniaProperty.Register<EStopButton, ICommand?>(nameof(ResetCommand));

    public ICommand? ResetCommand
    {
        get => GetValue(ResetCommandProperty);
        set => SetValue(ResetCommandProperty, value);
    }

    /// <summary>复位是否需要长按。默认要——防误碰。</summary>
    public static readonly StyledProperty<bool> RequireHoldToResetProperty =
        AvaloniaProperty.Register<EStopButton, bool>(nameof(RequireHoldToReset), true);

    public bool RequireHoldToReset
    {
        get => GetValue(RequireHoldToResetProperty);
        set => SetValue(RequireHoldToResetProperty, value);
    }

    public static readonly StyledProperty<TimeSpan> ResetHoldDurationProperty =
        AvaloniaProperty.Register<EStopButton, TimeSpan>(
            nameof(ResetHoldDuration), TimeSpan.FromMilliseconds(1200));

    public TimeSpan ResetHoldDuration
    {
        get => GetValue(ResetHoldDurationProperty);
        set => SetValue(ResetHoldDurationProperty, value);
    }

    /// <summary>硬件急停的物理位置提示，例如"操作台右下 / 设备后侧"。</summary>
    public static readonly StyledProperty<string?> HardwareLocationHintProperty =
        AvaloniaProperty.Register<EStopButton, string?>(nameof(HardwareLocationHint));

    public string? HardwareLocationHint
    {
        get => GetValue(HardwareLocationHintProperty);
        set => SetValue(HardwareLocationHintProperty, value);
    }

    /// <summary>钮下方那行说明字，未触发时显示。</summary>
    public static readonly StyledProperty<string> CaptionProperty =
        AvaloniaProperty.Register<EStopButton, string>(nameof(Caption), "就绪");

    public string Caption
    {
        get => GetValue(CaptionProperty);
        set => SetValue(CaptionProperty, value);
    }

    /// <summary>触发后那行说明字。要明确写出「需复位」——自锁了但没人告诉操作员是最糟的。</summary>
    public static readonly StyledProperty<string> EngagedCaptionProperty =
        AvaloniaProperty.Register<EStopButton, string>(nameof(EngagedCaption), "已触发 · 需复位");

    public string EngagedCaption
    {
        get => GetValue(EngagedCaptionProperty);
        set => SetValue(EngagedCaptionProperty, value);
    }

    private string? _captionText;

    public static readonly DirectProperty<EStopButton, string?> CaptionTextProperty =
        AvaloniaProperty.RegisterDirect<EStopButton, string?>(
            nameof(CaptionText), o => o._captionText);

    /// <summary>当前该显示哪行字。模板绑它。</summary>
    public string? CaptionText
    {
        get => _captionText;
        private set => SetAndRaise(CaptionTextProperty, ref _captionText, value);
    }

    public static readonly RoutedEvent<RoutedEventArgs> EngagedEvent =
        RoutedEvent.Register<EStopButton, RoutedEventArgs>(nameof(Engaged), RoutingStrategies.Bubble);

    public event EventHandler<RoutedEventArgs>? Engaged
    {
        add => AddHandler(EngagedEvent, value);
        remove => RemoveHandler(EngagedEvent, value);
    }

    public static readonly RoutedEvent<RoutedEventArgs> ReleasedEvent =
        RoutedEvent.Register<EStopButton, RoutedEventArgs>(nameof(Released), RoutingStrategies.Bubble);

    /// <summary>已复位。用急停的行话叫「释放」，避免和 <see cref="Reset"/> 方法重名。</summary>
    public event EventHandler<RoutedEventArgs>? Released
    {
        add => AddHandler(ReleasedEvent, value);
        remove => RemoveHandler(ReleasedEvent, value);
    }

    static EStopButton()
    {
        IsEngagedProperty.Changed.AddClassHandler<EStopButton>((x, e) =>
        {
            x.PseudoClasses.Set(":engaged", e.NewValue is true);
            x.UpdateCaption();
        });
        CaptionProperty.Changed.AddClassHandler<EStopButton>((x, _) => x.UpdateCaption());
        EngagedCaptionProperty.Changed.AddClassHandler<EStopButton>((x, _) => x.UpdateCaption());
    }

    public EStopButton()
    {
        PseudoClasses.Set(":engaged", false);
        UpdateCaption();
    }

    private void UpdateCaption() => CaptionText = IsEngaged ? EngagedCaption : Caption;

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;

        if (!IsEngaged)
        {
            // 按下即触发。等 Click（松手）会多出一次松手的延迟，急停不接受。
            Engage();
            return;
        }

        BeginReset();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key is Key.Space or Key.Enter)
        {
            if (!IsEngaged) Engage();
            else BeginReset();
            e.Handled = true;
            return;
        }

        base.OnKeyDown(e);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        CancelReset();
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        CancelReset();
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        if (e.Key is Key.Space or Key.Enter)
        {
            CancelReset();
            e.Handled = true;
            return;
        }

        base.OnKeyUp(e);
    }

    protected override void OnLostFocus(RoutedEventArgs e)
    {
        base.OnLostFocus(e);
        CancelReset();
    }

    /// <summary>触发并自锁。已经触发时是空操作。</summary>
    public void Engage()
    {
        if (IsEngaged) return;

        IsEngaged = true;

        if (EngageCommand?.CanExecute(null) == true)
            EngageCommand.Execute(null);

        RaiseEvent(new RoutedEventArgs(EngagedEvent));
    }

    private void BeginReset()
    {
        if (!IsEngaged) return;

        if (!RequireHoldToReset)
        {
            DoReset();
            return;
        }

        PseudoClasses.Set(":resetting", true);
        _resetHold = new DispatcherTimer(
            ResetHoldDuration, DispatcherPriority.Input, (_, _) => DoReset());
        _resetHold.Start();
    }

    private void CancelReset()
    {
        _resetHold?.Stop();
        _resetHold = null;
        PseudoClasses.Set(":resetting", false);
    }

    /// <summary>
    /// 复位。用代码复位时绕过长按要求 —— 长按是防误触的界面手段，
    /// 不是安全联锁；真正的联锁在设备侧。
    /// </summary>
    public void Reset() => DoReset();

    private void DoReset()
    {
        CancelReset();
        if (!IsEngaged) return;

        IsEngaged = false;

        if (ResetCommand?.CanExecute(null) == true)
            ResetCommand.Execute(null);

        RaiseEvent(new RoutedEventArgs(ReleasedEvent));
    }
}
