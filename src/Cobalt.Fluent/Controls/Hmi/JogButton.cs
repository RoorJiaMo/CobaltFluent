using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace Cobalt.Fluent.Controls;

/// <summary>点动方向。纯语义，模板据此选箭头字形。</summary>
public enum JogDirection
{
    None,
    Forward,
    Backward,
    Up,
    Down,
    Left,
    Right,
    Open,
    Close,
}

/// <summary>点动停止的原因。会记进日志，出事之后要靠它复盘。</summary>
public enum JogStopReason
{
    /// <summary>正常松手。</summary>
    PointerReleased,

    /// <summary>指针捕获丢失。按住后把指针拖出控件再松开，走的是这条。</summary>
    PointerCaptureLost,

    /// <summary>指针离开了控件。</summary>
    PointerExited,

    /// <summary>控件失焦（切窗口、弹对话框）。</summary>
    LostFocus,

    /// <summary>键盘松键。</summary>
    KeyReleased,

    /// <summary>控件被禁用或从视觉树上摘掉。</summary>
    Detached,

    /// <summary>看门狗超时。前面几条都没触发时的兜底——通常意味着 UI 线程卡过。</summary>
    Watchdog,
}

public sealed class JogStoppedEventArgs(RoutedEvent routedEvent, JogStopReason reason)
    : RoutedEventArgs(routedEvent)
{
    public JogStopReason Reason { get; } = reason;
}

/// <summary>
/// 点动按钮：按住动作，松开停止。
///
/// **这个控件的交互就是它的规格。** 视觉上必须和普通 Button 区别开
/// （描边更重、底边 2px），否则操作员会当成开关按一下就走。
///
/// 安全要点（第 7 组硬约束）：
/// 只监听 <c>PointerReleased</c> 是不够的 —— 按住后把指针拖出按钮，
/// 释放事件可能根本不在这个控件上触发，设备会一直动。
/// 所以这里挂了六个停止触发点：
/// 松手 / 捕获丢失 / 指针离开 / 失焦 / 松键 / 摘除，
/// 外加一个 <see cref="WatchdogTimeout"/> 看门狗兜底 —— 防止 UI 线程卡死时设备失控。
///
/// <see cref="StopCommand"/> 会被重复调用（多个触发点可能同时命中），
/// 所以下游必须做成幂等的。
/// </summary>
[PseudoClasses(":jogging")]
public class JogButton : Button
{
    private DispatcherTimer? _watchdog;
    private bool _isJogging;

    public static readonly StyledProperty<JogDirection> DirectionProperty =
        AvaloniaProperty.Register<JogButton, JogDirection>(nameof(Direction));

    public JogDirection Direction
    {
        get => GetValue(DirectionProperty);
        set => SetValue(DirectionProperty, value);
    }

    /// <summary>开始动作。参数是 <see cref="Speed"/>。</summary>
    public static readonly StyledProperty<ICommand?> StartCommandProperty =
        AvaloniaProperty.Register<JogButton, ICommand?>(nameof(StartCommand));

    public ICommand? StartCommand
    {
        get => GetValue(StartCommandProperty);
        set => SetValue(StartCommandProperty, value);
    }

    /// <summary>停止动作。参数是 <see cref="JogStopReason"/>。**必须做成幂等的。**</summary>
    public static readonly StyledProperty<ICommand?> StopCommandProperty =
        AvaloniaProperty.Register<JogButton, ICommand?>(nameof(StopCommand));

    public ICommand? StopCommand
    {
        get => GetValue(StopCommandProperty);
        set => SetValue(StopCommandProperty, value);
    }

    public static readonly StyledProperty<double> SpeedProperty =
        AvaloniaProperty.Register<JogButton, double>(nameof(Speed), 1.0d);

    public double Speed
    {
        get => GetValue(SpeedProperty);
        set => SetValue(SpeedProperty, value);
    }

    /// <summary>点动前是否需要二次确认。危险轴（比如带刀具的）上打开。</summary>
    public static readonly StyledProperty<bool> RequiresConfirmProperty =
        AvaloniaProperty.Register<JogButton, bool>(nameof(RequiresConfirm));

    public bool RequiresConfirm
    {
        get => GetValue(RequiresConfirmProperty);
        set => SetValue(RequiresConfirmProperty, value);
    }

    /// <summary>
    /// 看门狗超时。超过这么久还在 jogging 就强制停。
    /// 设成 <see cref="TimeSpan.Zero"/> 可以关掉，但**不建议**——
    /// 它是 UI 线程卡死时的最后一道防线。
    /// </summary>
    public static readonly StyledProperty<TimeSpan> WatchdogTimeoutProperty =
        AvaloniaProperty.Register<JogButton, TimeSpan>(
            nameof(WatchdogTimeout), TimeSpan.FromSeconds(5));

    public TimeSpan WatchdogTimeout
    {
        get => GetValue(WatchdogTimeoutProperty);
        set => SetValue(WatchdogTimeoutProperty, value);
    }

    private bool _isJoggingPublic;

    public static readonly DirectProperty<JogButton, bool> IsJoggingProperty =
        AvaloniaProperty.RegisterDirect<JogButton, bool>(nameof(IsJogging), o => o._isJoggingPublic);

    /// <summary>是否正在动作。</summary>
    public bool IsJogging
    {
        get => _isJoggingPublic;
        private set => SetAndRaise(IsJoggingProperty, ref _isJoggingPublic, value);
    }

    public static readonly RoutedEvent<RoutedEventArgs> JogStartedEvent =
        RoutedEvent.Register<JogButton, RoutedEventArgs>(
            nameof(JogStarted), RoutingStrategies.Bubble);

    public event EventHandler<RoutedEventArgs>? JogStarted
    {
        add => AddHandler(JogStartedEvent, value);
        remove => RemoveHandler(JogStartedEvent, value);
    }

    public static readonly RoutedEvent<JogStoppedEventArgs> JogStoppedEvent =
        RoutedEvent.Register<JogButton, JogStoppedEventArgs>(
            nameof(JogStopped), RoutingStrategies.Bubble);

    /// <summary>停止时抛出，带停止原因。工业场合建议把它记进操作日志。</summary>
    public event EventHandler<JogStoppedEventArgs>? JogStopped
    {
        add => AddHandler(JogStoppedEvent, value);
        remove => RemoveHandler(JogStoppedEvent, value);
    }

    static JogButton()
    {
        IsEnabledProperty.Changed.AddClassHandler<JogButton>((x, e) =>
        {
            if (e.NewValue is false) x.Stop(JogStopReason.Detached);
        });
    }

    // ---- 开始 --------------------------------------------------------------

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            Start();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        // 键盘按住也要能点动，否则纯键盘操作用不了这个控件。
        // 自动重复会反复进 OnKeyDown，Start() 里已经挡了重入。
        if (e.Key is Key.Space or Key.Enter)
        {
            Start();
            e.Handled = true;
            return;
        }

        base.OnKeyDown(e);
    }

    private void Start()
    {
        if (_isJogging || !IsEffectivelyEnabled) return;

        _isJogging = true;
        IsJogging = true;
        PseudoClasses.Set(":jogging", true);

        if (WatchdogTimeout > TimeSpan.Zero)
        {
            _watchdog = new DispatcherTimer(
                WatchdogTimeout, DispatcherPriority.Send, (_, _) => Stop(JogStopReason.Watchdog));
            _watchdog.Start();
        }

        if (StartCommand?.CanExecute(Speed) == true)
            StartCommand.Execute(Speed);

        RaiseEvent(new RoutedEventArgs(JogStartedEvent));
    }

    // ---- 停止：六个触发点 ---------------------------------------------------

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        Stop(JogStopReason.PointerReleased);
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        Stop(JogStopReason.PointerCaptureLost);
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        Stop(JogStopReason.PointerExited);
    }

    protected override void OnLostFocus(RoutedEventArgs e)
    {
        base.OnLostFocus(e);
        Stop(JogStopReason.LostFocus);
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        if (e.Key is Key.Space or Key.Enter)
        {
            Stop(JogStopReason.KeyReleased);
            e.Handled = true;
            return;
        }

        base.OnKeyUp(e);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        Stop(JogStopReason.Detached);
        base.OnDetachedFromVisualTree(e);
    }

    /// <summary>
    /// 停止动作。幂等：不在动作中时调用是空操作。
    /// 六个触发点可能同时命中，第一个到的那个负责真正停下来。
    /// </summary>
    public void Stop(JogStopReason reason)
    {
        if (!_isJogging) return;

        _isJogging = false;
        IsJogging = false;
        PseudoClasses.Set(":jogging", false);

        _watchdog?.Stop();
        _watchdog = null;

        if (StopCommand?.CanExecute(reason) == true)
            StopCommand.Execute(reason);

        RaiseEvent(new JogStoppedEventArgs(JogStoppedEvent, reason));
    }
}
