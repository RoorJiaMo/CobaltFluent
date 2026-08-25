using System.Windows.Input;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Cobalt.Fluent.Automation;

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
[PseudoClasses(":engaged", ":resetting", ":engagefailed")]
public class EStopButton : Button
{
    private DispatcherTimer? _resetHold;

    /// <summary>
    /// Space/Enter 当前是否处于按下状态。用来识别 OS 键盘自动重复。
    ///
    /// 不识别的话有一条灾难路径：按住回车不放 → 第一次 KeyDown 触发 Engage()，
    /// 自动重复的第二次 KeyDown 看到已经 engaged，转去 BeginReset() 开始长按计时，
    /// 手一直没松 → ResetHoldDuration 到点，<b>急停自己解锁了</b>。
    /// </summary>
    private bool _actionKeyDown;

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

    /// <summary>钮下方那行说明字，未触发时显示。留空则用 <see cref="CobaltStrings"/> 里的措辞。</summary>
    public static readonly StyledProperty<string?> CaptionProperty =
        AvaloniaProperty.Register<EStopButton, string?>(nameof(Caption));

    public string? Caption
    {
        get => GetValue(CaptionProperty);
        set => SetValue(CaptionProperty, value);
    }

    /// <summary>
    /// 急停指令没能下发时那行说明字。默认直接把人指向硬件急停——
    /// 软件路径已经证明不通了，这时候唯一还能信的就是硬接线的那个。
    /// </summary>
    public static readonly StyledProperty<string?> EngageFailedCaptionProperty =
        AvaloniaProperty.Register<EStopButton, string?>(nameof(EngageFailedCaption));

    public string? EngageFailedCaption
    {
        get => GetValue(EngageFailedCaptionProperty);
        set => SetValue(EngageFailedCaptionProperty, value);
    }

    private bool _engageFailed;

    /// <summary>触发后那行说明字。要明确写出「需复位」——自锁了但没人告诉操作员是最糟的。</summary>
    public static readonly StyledProperty<string?> EngagedCaptionProperty =
        AvaloniaProperty.Register<EStopButton, string?>(nameof(EngagedCaption));

    public string? EngagedCaption
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
        EngageFailedCaptionProperty.Changed.AddClassHandler<EStopButton>((x, _) => x.UpdateCaption());
    }

    /// <summary>换语言之后重算已经显示出来的文字。见 <see cref="CobaltStrings.CurrentChanged"/>。</summary>
    private readonly StringsWatcher _strings;

    public EStopButton()
    {
        _strings = new StringsWatcher(UpdateCaption);

        PseudoClasses.Set(":engaged", false);
        UpdateCaption();
    }

    // 三个 caption 都留空注册、在这里回落，而不是把默认值写进 Register：
    // 注册的默认值在静态构造时就定死了，之后换 CobaltStrings.Current 也带不动它。
    // 空值回落既让默认跟着语言走，也让运行时换语言能生效。
    private void UpdateCaption() =>
        CaptionText = _engageFailed ? EngageFailedCaption ?? CobaltStrings.Current.EStopCommandNotSent
            : IsEngaged ? EngagedCaption ?? CobaltStrings.Current.EStopEngaged
            : Caption ?? CobaltStrings.Current.EStopReady;

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
        // 带 Ctrl / Alt / Win 的组合键一律放行。两个方向都不能要：
        // 误触发急停会无谓停产，而误触发的是复位则等于把自锁解掉。
        var command = (e.KeyModifiers & (KeyModifiers.Control | KeyModifiers.Alt | KeyModifiers.Meta)) != 0;
        if (command)
        {
            // 这里**不能**转交给 base：基类 Button 自己会吞掉 Enter/Space 并抛 Click，
            // 组合键照样到不了应用，加这道闸就白加了。直接返回，让事件继续冒泡。
            if (e.Key is Key.Space or Key.Enter) return;
            base.OnKeyDown(e);
            return;
        }

        if (e.Key is Key.Space or Key.Enter)
        {
            // OS 自动重复只算一次按下。不挡的话按住回车不放会先 Engage、
            // 再被重复事件带进 BeginReset，长按计时走完自己把急停解锁。
            if (_actionKeyDown)
            {
                e.Handled = true;
                return;
            }
            _actionKeyDown = true;

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

    /// <summary>松键。<b>不查修饰键</b>——取消长按复位属于往安全方向走，判据只能更宽。</summary>
    protected override void OnKeyUp(KeyEventArgs e)
    {
        if (e.Key is Key.Space or Key.Enter)
        {
            _actionKeyDown = false;
            CancelReset();
            e.Handled = true;
            return;
        }

        base.OnKeyUp(e);
    }

    /// <summary>
    /// 卸载时停掉长按定时器。此前本控件是第 7 组里唯一带定时器却不覆写这个方法的：
    /// 定时器在控件离开可视树之后仍然存活并到点，DoReset() 照样执行——
    /// 急停会在界面上已经看不到它的时候自己解锁。
    /// </summary>
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _strings.Detach();
        CancelReset();
        _actionKeyDown = false;
        base.OnDetachedFromVisualTree(e);
    }

    protected override void OnLostFocus(RoutedEventArgs e)
    {
        base.OnLostFocus(e);
        // 失焦后收不到 KeyUp，闩锁不清的话回来时第一次按下会被当成自动重复吞掉
        _actionKeyDown = false;
        CancelReset();
    }

    /// <summary>
    /// 触发并自锁。已经触发时是空操作。
    ///
    /// <b>下发失败时不回滚成「就绪」。</b>挂了 <see cref="EngageCommand"/> 而它
    /// <c>CanExecute</c> 为 false 时，指令没发出去、设备没停；但退回「就绪」
    /// 同样是假陈述——操作员确实按下了急停。这时进第三态 <c>:engagefailed</c>，
    /// 说明文字直接指向硬件急停：软件路径已经证明不通，唯一还能信的是硬接线那个。
    /// </summary>
    public void Engage()
    {
        if (IsEngaged) return;

        IsEngaged = true;

        var accepted = true;
        if (EngageCommand is { } engage)
        {
            if (engage.CanExecute(null)) engage.Execute(null);
            else accepted = false;
        }

        _engageFailed = !accepted;
        PseudoClasses.Set(":engagefailed", !accepted);
        UpdateCaption();

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

        // 重入保护：每次进来都 new 一个已启动的定时器而不停掉上一个，
        // 旧定时器只是丢了引用，仍然在跑且仍然会到点调 DoReset()——
        // 既漏定时器，又让「松手取消」形同虚设（只取消得掉最后那一个）。
        if (_resetHold is not null) return;

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

        _engageFailed = false;
        PseudoClasses.Set(":engagefailed", false);
        IsEngaged = false;

        if (ResetCommand?.CanExecute(null) == true)
            ResetCommand.Execute(null);

        RaiseEvent(new RoutedEventArgs(ReleasedEvent));
    }

    /// <summary>见 <see cref="Cobalt.Fluent.Automation.EStopButtonAutomationPeer"/>。</summary>
    protected override AutomationPeer OnCreateAutomationPeer() => new EStopButtonAutomationPeer(this);

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _strings.Attach();
    }
}
