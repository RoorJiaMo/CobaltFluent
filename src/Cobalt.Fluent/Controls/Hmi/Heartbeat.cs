using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Threading;
using Cobalt.Fluent.Automation;

namespace Cobalt.Fluent.Controls;

/// <summary>
/// 心跳灯。**由实际通信事件驱动，不是固定周期动画。**
///
/// 这一条是安全要求不是风格偏好：用固定周期的动画假装心跳的话，
/// 通信断了心跳还在跳，操作员会以为系统活着——**比没有心跳灯更危险**。
///
/// 用法：每次收到设备响应就调一次 <see cref="Beat"/>。
/// 超过 <see cref="Timeout"/> 没有新的 Beat，灯自动转成停跳（红色常亮）。
/// </summary>
[PseudoClasses(":beating", ":stopped")]
public class Heartbeat : TemplatedControl
{
    private DispatcherTimer? _watchdog;
    /// <summary>
    /// 上一拍的单调时间戳（毫秒）。<b>不能用 DateTime.Now</b>：墙钟不是单调的，
    /// 夏令时回拨、NTP 阶跃校正、操作员手改系统时间都会让差值变成负数，
    /// 负 TimeSpan 永远不 &gt; Timeout，停跳判定整个失效——回拨多久就有多久
    /// 心跳灯在通信已经断掉的情况下仍然显示活着。往前跳则相反，通信正常也报停跳。
    /// </summary>
    private long _lastBeatTicks;

    /// <summary>有没有喂过。首次 Beat 之前不做超时判定，也避免相减溢出。</summary>
    private bool _everBeat;

    /// <summary>闪烁定时器。全局只留一个，见 <see cref="Beat"/> 的注释。</summary>
    private DispatcherTimer? _flash;

    /// <summary>一次闪烁的时长。</summary>
    public static readonly StyledProperty<TimeSpan> FlashDurationProperty =
        AvaloniaProperty.Register<Heartbeat, TimeSpan>(
            nameof(FlashDuration), TimeSpan.FromMilliseconds(300));

    public TimeSpan FlashDuration
    {
        get => GetValue(FlashDurationProperty);
        set => SetValue(FlashDurationProperty, value);
    }

    /// <summary>超过这么久没有 Beat 就判定停跳。</summary>
    public static readonly StyledProperty<TimeSpan> TimeoutProperty =
        AvaloniaProperty.Register<Heartbeat, TimeSpan>(
            nameof(Timeout), TimeSpan.FromSeconds(3));

    public TimeSpan Timeout
    {
        get => GetValue(TimeoutProperty);
        set => SetValue(TimeoutProperty, value);
    }

    private bool _isStopped = true;

    public static readonly DirectProperty<Heartbeat, bool> IsStoppedProperty =
        AvaloniaProperty.RegisterDirect<Heartbeat, bool>(nameof(IsStopped), o => o._isStopped);

    /// <summary>是否已停跳。停跳 = 通信断了，比任何文字提示都快。</summary>
    public bool IsStopped
    {
        get => _isStopped;
        private set
        {
            if (SetAndRaise(IsStoppedProperty, ref _isStopped, value))
            {
                PseudoClasses.Set(":stopped", value);
                if (value) PseudoClasses.Set(":beating", false);
            }
        }
    }

    public Heartbeat()
    {
        PseudoClasses.Set(":stopped", true);
    }

    /// <summary>
    /// 收到一次设备响应。每次通信成功调一下，灯闪一次。
    ///
    /// <b>必须在 UI 线程调用。</b>设备响应天然到达在通信 / IO 线程上，
    /// 请在调用方那一侧编组。这里不做隐式编组：<c>Post(Beat)</c> 会把时间戳
    /// 推迟到 UI 线程实际执行的时刻，UI 拥塞时会把超时判定一起带偏，
    /// 高频调用下还会淹没 dispatcher 队列。
    /// </summary>
    public void Beat()
    {
        Dispatcher.UIThread.VerifyAccess();

        _lastBeatTicks = Environment.TickCount64;
        _everBeat = true;
        IsStopped = false;

        // 熄灭定时器全局只留一个并顺延。此前每次 Beat 都新起一个一次性定时器、
        // 也没有句柄取消上一个，于是上一拍的定时器到点时会去关掉这一拍点亮的灯：
        // 只要轮询周期短于 FlashDuration，明暗节奏就跟数据流完全脱钩了。
        // 顺延之后慢轮询仍是「一拍一闪」，快轮询变成「有数据就常亮」——这才读得出来。
        PseudoClasses.Set(":beating", true);

        _flash ??= new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = FlashDuration,
        };
        _flash.Stop();
        _flash.Interval = FlashDuration;
        _flash.Tick -= OnFlashElapsed;
        _flash.Tick += OnFlashElapsed;
        _flash.Start();
    }

    /// <summary>
    /// 按<b>真实经过时间</b>恢复心跳状态，用于模板应用晚于首次 Beat() 的场合。
    ///
    /// 不要用 <see cref="Beat"/> 代替：那会把时间戳盖成「现在」，超时窗口从恢复
    /// 那一刻重新起算，实际存活时间最长可达两倍 <see cref="Timeout"/>——
    /// 也就是说链路早就断了，心跳灯还能再亮一个完整的超时周期。
    /// </summary>
    /// <param name="sinceLastBeat">距上一次真实响应过了多久。</param>
    public void Restore(TimeSpan sinceLastBeat)
    {
        Dispatcher.UIThread.VerifyAccess();

        var ms = (long)Math.Max(0, sinceLastBeat.TotalMilliseconds);
        _lastBeatTicks = Environment.TickCount64 - ms;
        _everBeat = true;
        IsStopped = false;

        Evaluate();     // 已经超时的话立刻回到停跳，不给一个假的存活窗口
    }

    private void OnFlashElapsed(object? sender, EventArgs e)
    {
        _flash?.Stop();
        PseudoClasses.Set(":beating", false);
    }

    /// <summary>超时判定。挂载时立刻跑一次，之后由看门狗每 500ms 驱动。</summary>
    private void Evaluate()
    {
        if (!_everBeat) return;   // 从没喂过，构造时已经是停跳
        if (Environment.TickCount64 - _lastBeatTicks > (long)Timeout.TotalMilliseconds)
            IsStopped = true;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        _watchdog = new DispatcherTimer(
            TimeSpan.FromMilliseconds(500), DispatcherPriority.Background, (_, _) => Evaluate());
        _watchdog.Start();

        // 挂载即判定。新看门狗要等满一个 500ms 周期才第一次 tick，
        // 在那之前显示的是卸载那一刻冻结下来的旧状态——如果卸载时正活着，
        // 重新挂载后会有半秒钟在通信早已断掉的情况下显示「活着」。
        Evaluate();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _watchdog?.Stop();
        _watchdog = null;

        _flash?.Stop();
        _flash = null;
        PseudoClasses.Set(":beating", false);

        base.OnDetachedFromVisualTree(e);
    }

    /// <summary>见 <see cref="Cobalt.Fluent.Automation.HeartbeatAutomationPeer"/>。</summary>
    protected override AutomationPeer OnCreateAutomationPeer() => new HeartbeatAutomationPeer(this);
}
