using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Metadata;
using Avalonia.Threading;

namespace Cobalt.Fluent.Controls;

/// <summary>通信连接状态。</summary>
public enum ConnectionState
{
    /// <summary>已连接，轮询正常。</summary>
    Connected,

    /// <summary>连着但不稳：偶发超时、重传。</summary>
    Degraded,

    /// <summary>断开。</summary>
    Disconnected,
}

/// <summary>
/// 设备状态栏。常驻底栏，操作员靠它判断「系统还活着」。
///
/// 心跳灯（<see cref="Heartbeat"/>）由**实际通信事件**驱动：
/// 每次收到设备响应调一次 <see cref="Beat"/>。
/// 不要用固定周期动画假装——通信断了心跳还在跳，比没有心跳灯更危险。
/// </summary>
[PseudoClasses(":connected", ":degraded", ":disconnected")]
public class DeviceStatusBar : TemplatedControl
{
    public static readonly StyledProperty<ConnectionState> ConnectionStateProperty =
        AvaloniaProperty.Register<DeviceStatusBar, ConnectionState>(
            nameof(ConnectionState), ConnectionState.Disconnected);

    public ConnectionState ConnectionState
    {
        get => GetValue(ConnectionStateProperty);
        set => SetValue(ConnectionStateProperty, value);
    }

    /// <summary>通信端点，例如 "Modbus TCP 192.168.1.50:502"。</summary>
    public static readonly StyledProperty<string?> EndpointProperty =
        AvaloniaProperty.Register<DeviceStatusBar, string?>(nameof(Endpoint));

    public string? Endpoint
    {
        get => GetValue(EndpointProperty);
        set => SetValue(EndpointProperty, value);
    }

    public static readonly StyledProperty<DateTime?> LastResponseProperty =
        AvaloniaProperty.Register<DeviceStatusBar, DateTime?>(nameof(LastResponse));

    public DateTime? LastResponse
    {
        get => GetValue(LastResponseProperty);
        set => SetValue(LastResponseProperty, value);
    }

    /// <summary>轮询频率（Hz）。</summary>
    public static readonly StyledProperty<double> PollRateProperty =
        AvaloniaProperty.Register<DeviceStatusBar, double>(nameof(PollRate), 1d);

    public double PollRate
    {
        get => GetValue(PollRateProperty);
        set => SetValue(PollRateProperty, value);
    }

    public static readonly StyledProperty<string?> CurrentUserProperty =
        AvaloniaProperty.Register<DeviceStatusBar, string?>(nameof(CurrentUser));

    public string? CurrentUser
    {
        get => GetValue(CurrentUserProperty);
        set => SetValue(CurrentUserProperty, value);
    }

    public static readonly StyledProperty<bool> ShowClockProperty =
        AvaloniaProperty.Register<DeviceStatusBar, bool>(nameof(ShowClock), true);

    public bool ShowClock
    {
        get => GetValue(ShowClockProperty);
        set => SetValue(ShowClockProperty, value);
    }

    /// <summary>右侧自定义段。放产线号、批次号这类项目相关的东西。</summary>
    public static readonly StyledProperty<Avalonia.Controls.Controls> ItemsProperty =
        AvaloniaProperty.Register<DeviceStatusBar, Avalonia.Controls.Controls>(nameof(Items));

    [Content]
    public Avalonia.Controls.Controls Items
    {
        get => GetValue(ItemsProperty);
        set => SetValue(ItemsProperty, value);
    }

    private Heartbeat? _heartbeat;
    private DispatcherTimer? _clock;

    private string? _stateText;

    public static readonly DirectProperty<DeviceStatusBar, string?> StateTextProperty =
        AvaloniaProperty.RegisterDirect<DeviceStatusBar, string?>(nameof(StateText), o => o._stateText);

    /// <summary>连接状态的文字说明。颜色之外的第二重编码。</summary>
    public string? StateText
    {
        get => _stateText;
        private set => SetAndRaise(StateTextProperty, ref _stateText, value);
    }

    private string? _clockText;

    public static readonly DirectProperty<DeviceStatusBar, string?> ClockTextProperty =
        AvaloniaProperty.RegisterDirect<DeviceStatusBar, string?>(nameof(ClockText), o => o._clockText);

    /// <summary>当前时间。工业现场记录操作时刻要用，所以默认显示到秒。</summary>
    public string? ClockText
    {
        get => _clockText;
        private set => SetAndRaise(ClockTextProperty, ref _clockText, value);
    }

    private string? _pollRateText;

    public static readonly DirectProperty<DeviceStatusBar, string?> PollRateTextProperty =
        AvaloniaProperty.RegisterDirect<DeviceStatusBar, string?>(
            nameof(PollRateText), o => o._pollRateText);

    public string? PollRateText
    {
        get => _pollRateText;
        private set => SetAndRaise(PollRateTextProperty, ref _pollRateText, value);
    }

    static DeviceStatusBar()
    {
        ConnectionStateProperty.Changed.AddClassHandler<DeviceStatusBar>((x, _) => x.Refresh());
        PollRateProperty.Changed.AddClassHandler<DeviceStatusBar>((x, _) => x.Refresh());
    }

    public DeviceStatusBar()
    {
        Items = [];
        Refresh();
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _heartbeat = e.NameScope.Find<Heartbeat>("PART_Heartbeat");

        // 模板是后应用的：在此之前调过的 Beat() 拿不到心跳灯，状态会丢。
        // 用 LastResponse 把它补回来——否则刚建好的状态栏会（错误地）显示停跳。
        if (_heartbeat is not null && LastResponse is { } last
            && DateTime.Now - last <= _heartbeat.Timeout)
        {
            _heartbeat.Beat();
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        _clock = new DispatcherTimer(
            TimeSpan.FromSeconds(1), DispatcherPriority.Background,
            (_, _) => ClockText = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        _clock.Start();
        ClockText = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _clock?.Stop();
        _clock = null;
        base.OnDetachedFromVisualTree(e);
    }

    private void Refresh()
    {
        var state = ConnectionState;
        PseudoClasses.Set(":connected", state == ConnectionState.Connected);
        PseudoClasses.Set(":degraded", state == ConnectionState.Degraded);
        PseudoClasses.Set(":disconnected", state == ConnectionState.Disconnected);

        StateText = state switch
        {
            ConnectionState.Connected => "已连接",
            ConnectionState.Degraded => "通信不稳",
            _ => "通信中断",
        };

        PollRateText = PollRate > 0 ? $"轮询 {PollRate:0.#} Hz" : null;
    }

    /// <summary>
    /// 收到一次设备响应。每次通信成功调一下：心跳灯闪一次，
    /// <see cref="LastResponse"/> 跟着更新。
    /// </summary>
    public void Beat()
    {
        LastResponse = DateTime.Now;
        _heartbeat?.Beat();
    }
}
