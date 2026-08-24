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

/// <summary>
/// 停止指令没能下发出去。<see cref="JogButton.StopFailed"/> 的载荷。
///
/// 这不是「已停止」——设备很可能还在动。使用方收到它必须走升级路径
/// （报警、切断使能、提示操作员按硬件急停），不能当成一次普通的停止处理。
/// </summary>
public sealed class JogStopFailedEventArgs(RoutedEvent routedEvent, JogStopReason reason)
    : RoutedEventArgs(routedEvent)
{
    /// <summary>本来打算以什么理由停。</summary>
    public JogStopReason Reason { get; } = reason;
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
[PseudoClasses(":jogging", ":stopfailed")]
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

    private Symbol _glyph = Symbol.None;

    public static readonly DirectProperty<JogButton, Symbol> GlyphProperty =
        AvaloniaProperty.RegisterDirect<JogButton, Symbol>(nameof(Glyph), o => o._glyph);

    /// <summary>
    /// <see cref="Direction"/> 对应的箭头字形，模板绑它。
    ///
    /// 此前 Direction 的文档写着「模板据此选箭头字形」，而模板里根本没有任何地方
    /// 读它——展柜里 Direction="Open" / "Forward" 一律不产生任何效果。
    /// 方向是点动按钮上最要紧的信息（按错方向就是撞机），只靠 Content 里的
    /// 文字承载不够：一屏多个点动键时，箭头是扫一眼就能分辨的那一路编码。
    /// </summary>
    public Symbol Glyph
    {
        get => _glyph;
        private set => SetAndRaise(GlyphProperty, ref _glyph, value);
    }

    private void OnDirectionChanged() => Glyph = Direction switch
    {
        JogDirection.Forward or JogDirection.Right => Symbol.ChevronRight,
        JogDirection.Backward or JogDirection.Left => Symbol.ChevronLeft,
        JogDirection.Up or JogDirection.Open => Symbol.ChevronUp,
        JogDirection.Down or JogDirection.Close => Symbol.ChevronDown,
        _ => Symbol.None,
    };

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

    public static readonly StyledProperty<bool> IsConfirmedProperty =
        AvaloniaProperty.Register<JogButton, bool>(
            nameof(IsConfirmed), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    /// <summary>
    /// 操作员已经确认过这次点动。<see cref="RequiresConfirm"/> 为 true 时，
    /// 这个没置上就不许启动——启动请求会转成 <see cref="ConfirmRequired"/> 事件。
    /// 使用方在确认框点「确定」后置 true；什么时候清由使用方决定
    /// （每次点动都要确认就在 <see cref="JogStopped"/> 里清掉）。
    /// </summary>
    public bool IsConfirmed
    {
        get => GetValue(IsConfirmedProperty);
        set => SetValue(IsConfirmedProperty, value);
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

    /// <summary>
    /// 必须先松键/松手才允许再次启动。看门狗超时后置上：
    /// 超时说明前面所有正常停止路径都没生效，此时按键很可能还按着，
    /// OS 自动重复会立刻把轴再启动起来——看门狗就白设了。
    /// </summary>
    private bool _needsRelease;

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

    public static readonly RoutedEvent<JogStopFailedEventArgs> StopFailedEvent =
        RoutedEvent.Register<JogButton, JogStopFailedEventArgs>(
            nameof(StopFailed), RoutingStrategies.Bubble);

    /// <summary>
    /// 停止指令没能下发（<see cref="StopCommand"/> 存在但 <c>CanExecute</c> 为 false）。
    /// <b>设备可能仍在动作。</b>此时不会抛 <see cref="JogStopped"/>，
    /// 因为那个事件的语义是「已经停了」。
    /// </summary>
    public event EventHandler<JogStopFailedEventArgs>? StopFailed
    {
        add => AddHandler(StopFailedEvent, value);
        remove => RemoveHandler(StopFailedEvent, value);
    }

    public static readonly RoutedEvent<RoutedEventArgs> ConfirmRequiredEvent =
        RoutedEvent.Register<JogButton, RoutedEventArgs>(
            nameof(ConfirmRequired), RoutingStrategies.Bubble);

    /// <summary>
    /// <see cref="RequiresConfirm"/> 为 true 而 <see cref="IsConfirmed"/> 还没置上时，
    /// 启动被拒并抛出这个事件。使用方据此弹确认框，确认后把 <see cref="IsConfirmed"/> 置 true。
    /// </summary>
    public event EventHandler<RoutedEventArgs>? ConfirmRequired
    {
        add => AddHandler(ConfirmRequiredEvent, value);
        remove => RemoveHandler(ConfirmRequiredEvent, value);
    }

    static JogButton()
    {
        IsEnabledProperty.Changed.AddClassHandler<JogButton>((x, e) =>
        {
            if (e.NewValue is false) x.Stop(JogStopReason.Detached);
        });
        DirectionProperty.Changed.AddClassHandler<JogButton>((x, _) => x.OnDirectionChanged());
    }

    public JogButton() => OnDirectionChanged();

    // ---- 开始 --------------------------------------------------------------

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            Start();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        // 带 Ctrl / Alt / Win 的组合键一律放行。操作员按的是应用快捷键
        // （Ctrl+Enter 确认、Ctrl+Space 切输入法），意图不是让轴动起来；
        // 而且松开修饰键不会停，只有松开 Space/Enter 才停。
        //
        // 注意这道闸**只加在启动侧**：OnKeyUp 永远不查修饰键——
        // 按住 Space 点动中途按下 Ctrl，松开 Space 时事件带着 Ctrl 修饰符，
        // 停止路径要是也加闸，轴就停不下来了。
        var command = (e.KeyModifiers & (KeyModifiers.Control | KeyModifiers.Alt | KeyModifiers.Meta)) != 0;
        if (command)
        {
            // 这里**不能**转交给 base：基类 Button 自己会吞掉 Enter/Space 并抛 Click，
            // 组合键照样到不了应用，加这道闸就白加了。直接返回，让事件继续冒泡。
            if (e.Key is Key.Space or Key.Enter) return;
            base.OnKeyDown(e);
            return;
        }

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

        // 看门狗刚兜过底，说明正常停止路径全都没生效。此时按键多半还按着，
        // 让自动重复立刻把轴再启动起来等于看门狗白设。必须先松开。
        if (_needsRelease) return;

        // 二次确认是危险轴的硬门槛。以前这个属性只有声明没有实现，
        // 打开它跟没打开一样——比没有这个属性更糟。
        if (RequiresConfirm && !IsConfirmed)
        {
            RaiseEvent(new RoutedEventArgs(ConfirmRequiredEvent));
            return;
        }

        PseudoClasses.Set(":stopfailed", false);

        _isJogging = true;
        IsJogging = true;
        PseudoClasses.Set(":jogging", true);

        if (WatchdogTimeout > TimeSpan.Zero)
        {
            _watchdog = new DispatcherTimer(
                WatchdogTimeout, DispatcherPriority.Send, (_, _) => Stop(JogStopReason.Watchdog));
            _watchdog.Start();
        }

        // 先置状态再派发：同步的 JogStarted 处理器要能看到 IsJogging=true。
        // 派发完谁都没受理，再退回去——界面显示「正在点动」而指令根本没发出去，
        // 操作员会以为轴在动。
        var accepted = false;
        if (StartCommand is { } start)
        {
            if (start.CanExecute(Speed)) { start.Execute(Speed); accepted = true; }
        }
        else
        {
            // 没挂命令时事件是唯一通道，无从判断受理与否，按受理处理，
            // 否则只听事件的常规用法会被整片误判成启动失败。
            accepted = true;
        }

        var args = new RoutedEventArgs(JogStartedEvent);
        RaiseEvent(args);

        if (!accepted && _isJogging)
        {
            _watchdog?.Stop();
            _watchdog = null;
            _isJogging = false;
            IsJogging = false;
            PseudoClasses.Set(":jogging", false);
        }
    }

    // ---- 停止：六个触发点 ---------------------------------------------------

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        _needsRelease = false;
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

    /// <summary>
    /// 松键即停。<b>这里永远不查修饰键</b>——点动途中按下 Ctrl 再松开 Space，
    /// 事件会带着 Ctrl 修饰符过来，加闸就意味着轴停不下来。
    /// 停止路径的判据只能比启动路径更宽，不能更窄。
    /// </summary>
    protected override void OnKeyUp(KeyEventArgs e)
    {
        if (e.Key is Key.Space or Key.Enter)
        {
            _needsRelease = false;
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
    /// 七个触发点可能同时命中，第一个到的那个负责真正停下来。
    ///
    /// <b>停止指令没能下发时不会报「已停止」。</b>挂了 <see cref="StopCommand"/>
    /// 而它的 <c>CanExecute</c> 返回 false（下游忙、通讯断、权限不足），
    /// 指令实际上一个字节都没发出去，此时清掉 <c>:jogging</c> 并抛
    /// <see cref="JogStopped"/> 就是在告诉操作员「已经停了」——而轴还在动。
    /// 这种情况改为进入 <c>:stopfailed</c>、保持 <see cref="IsJogging"/> 为 true、
    /// 抛 <see cref="StopFailed"/>，并且<b>看门狗继续跑</b>：它是最后一道防线，
    /// 恰恰在停不下来的时候最不该被关掉。
    /// </summary>
    public void Stop(JogStopReason reason)
    {
        if (!_isJogging) return;

        // 挂了命令却不能执行 = 停止指令没发出去。没挂命令时事件是唯一通道，
        // 无从判断受理与否，按已停处理，否则只听事件的常规用法会被整片误判。
        if (StopCommand is { } stop && !stop.CanExecute(reason))
        {
            PseudoClasses.Set(":stopfailed", true);
            RaiseEvent(new JogStopFailedEventArgs(StopFailedEvent, reason));
            return;      // 状态、伪类、看门狗全部保持——设备还在动
        }

        _isJogging = false;
        IsJogging = false;
        PseudoClasses.Set(":jogging", false);
        PseudoClasses.Set(":stopfailed", false);

        // 看门狗超时说明前面所有正常停止路径都失效过一次，
        // 按键很可能还按着，必须先松开才允许再启动。
        if (reason == JogStopReason.Watchdog) _needsRelease = true;

        _watchdog?.Stop();
        _watchdog = null;

        StopCommand?.Execute(reason);

        RaiseEvent(new JogStoppedEventArgs(JogStoppedEvent, reason));
    }

    /// <summary>见 <see cref="Cobalt.Fluent.Automation.JogButtonAutomationPeer"/>。</summary>
    protected override AutomationPeer OnCreateAutomationPeer() => new JogButtonAutomationPeer(this);
}
