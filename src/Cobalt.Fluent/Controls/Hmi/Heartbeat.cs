using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Threading;

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
    private DateTime _lastBeat = DateTime.MinValue;

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

    /// <summary>收到一次设备响应。每次通信成功调一下，灯闪一次。</summary>
    public void Beat()
    {
        _lastBeat = DateTime.Now;
        IsStopped = false;

        PseudoClasses.Set(":beating", true);
        DispatcherTimer.RunOnce(
            () => PseudoClasses.Set(":beating", false), FlashDuration, DispatcherPriority.Background);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        _watchdog = new DispatcherTimer(
            TimeSpan.FromMilliseconds(500), DispatcherPriority.Background, (_, _) =>
            {
                if (!IsStopped && DateTime.Now - _lastBeat > Timeout)
                    IsStopped = true;
            });
        _watchdog.Start();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _watchdog?.Stop();
        _watchdog = null;
        base.OnDetachedFromVisualTree(e);
    }
}
