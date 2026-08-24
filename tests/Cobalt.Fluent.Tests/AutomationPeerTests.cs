using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Cobalt.Fluent.Automation;
using Cobalt.Fluent.Controls;
using Xunit;

namespace Cobalt.Fluent.Tests;

/// <summary>
/// 自动化对等体。
///
/// 这一批断言的对象不是「读屏软件念得对不对」——那个在无头环境里测不了。
/// 测的是**自动化客户端能不能做出它靠截图做不出的判断**：
/// 读数是不是死值、参数行能不能写、急停有没有锁上、报警该不该打断朗读。
/// 工业 HMI 的验收脚本普遍用 UI Automation 驱动界面跑回归，
/// 这些属性一旦回归成 null 或者语义漂移，使用方的脚本会静默地全部失效——
/// 界面照样长得对，脚本照样跑完，只是断言的对象换了。
/// </summary>
public class AutomationPeerTests
{
    // ---- 一、对等体接上了没有 -------------------------------------------------
    //
    // 漏一个 OnCreateAutomationPeer 的后果是控件在 Inspect 里退化成一团没有名字的
    // 矩形，而这在界面上完全看不出来。所以先把「每个控件拿到的是哪个对等体」钉死。

    [AvaloniaTheory]
    [InlineData(typeof(Readout), typeof(ReadoutAutomationPeer))]
    [InlineData(typeof(StatusIndicator), typeof(StatusIndicatorAutomationPeer))]
    [InlineData(typeof(Heartbeat), typeof(HeartbeatAutomationPeer))]
    [InlineData(typeof(AlarmBanner), typeof(AlarmBannerAutomationPeer))]
    [InlineData(typeof(ParameterRow), typeof(ParameterRowAutomationPeer))]
    [InlineData(typeof(JogButton), typeof(JogButtonAutomationPeer))]
    [InlineData(typeof(EStopButton), typeof(EStopButtonAutomationPeer))]
    [InlineData(typeof(DeviceStatusBar), typeof(DeviceStatusBarAutomationPeer))]
    [InlineData(typeof(NumericKeypad), typeof(NumericKeypadAutomationPeer))]
    [InlineData(typeof(InfoBar), typeof(InfoBarAutomationPeer))]
    [InlineData(typeof(Toast), typeof(ToastAutomationPeer))]
    [InlineData(typeof(InfoBadge), typeof(InfoBadgeAutomationPeer))]
    [InlineData(typeof(TeachingTip), typeof(TeachingTipAutomationPeer))]
    [InlineData(typeof(Pagination), typeof(PaginationAutomationPeer))]
    [InlineData(typeof(EmptyState), typeof(EmptyStateAutomationPeer))]
    [InlineData(typeof(PersonPicture), typeof(PersonPictureAutomationPeer))]
    [InlineData(typeof(DataGridToolbar), typeof(DataGridToolbarAutomationPeer))]
    [InlineData(typeof(NavigationView), typeof(NavigationViewAutomationPeer))]
    [InlineData(typeof(ChartLegend), typeof(ChartLegendAutomationPeer))]
    [InlineData(typeof(TrendChart), typeof(TrendChartAutomationPeer))]
    [InlineData(typeof(BarChart), typeof(BarChartAutomationPeer))]
    [InlineData(typeof(Sparkline), typeof(SparklineAutomationPeer))]
    public void 控件拿到自己的对等体(Type controlType, Type peerType)
    {
        var control = (Control)Activator.CreateInstance(controlType)!;

        Assert.IsType(peerType, Peer(control));
    }

    [AvaloniaTheory]
    [InlineData(typeof(Skeleton))]
    [InlineData(typeof(AppBarSeparator))]
    [InlineData(typeof(NavigationViewItemSeparator))]
    [InlineData(typeof(GaugeArcs))]
    [InlineData(typeof(SymbolIcon))]
    public void 装饰性元素退出自动化树(Type controlType)
    {
        var control = (Control)Activator.CreateInstance(controlType)!;

        var peer = Peer(control);

        // NoneAutomationPeer 是 Avalonia 的标准做法：两个「我是不是一个节点」
        // 都答否，客户端遍历时整个跳过。
        Assert.IsType<DecorativeAutomationPeer>(peer);
        Assert.False(peer.IsControlElement());
        Assert.False(peer.IsContentElement());
    }

    // ---- 二、Readout：Value 与判读上下文必须分开 -----------------------------

    [AvaloniaFact]
    public void 读数的值不带单位()
    {
        // 单位拼进 Value 的话，断言数值前得先剥字符串——而剥法随单位而异。
        var readout = Mount(new Readout { Label = "腔体温度", Unit = "°C", Value = 85.4, Format = "F1" });

        var peer = Provider<IValueProvider>(readout);

        Assert.Equal("85.4", peer.Value);
        Assert.Equal("°C", Peer(readout).GetItemType());
        Assert.Equal("腔体温度", Peer(readout).GetName());
    }

    [AvaloniaFact]
    public void 死值和实时值在自动化上分得开()
    {
        // 这是 ReadoutAutomationPeer 存在的主要理由：屏幕上这两种情况只差一个灰度，
        // 只读 Value 的脚本会把五分钟前的最后一个值当成当前值断言通过。
        var live = Mount(new Readout
        {
            Value = 85.4, Format = "F1",
            StaleAfter = TimeSpan.FromSeconds(30),
            LastUpdated = DateTime.Now,
        });
        var dead = Mount(new Readout
        {
            Value = 85.4, Format = "F1",
            StaleAfter = TimeSpan.FromSeconds(30),
            LastUpdated = DateTime.Now.AddMinutes(-5),
        });

        // Value 一样——所以判读上下文必须另有出处。
        Assert.Equal(Provider<IValueProvider>(live).Value, Provider<IValueProvider>(dead).Value);

        Assert.Null(Peer(live).GetItemStatus());
        Assert.NotNull(Peer(dead).GetItemStatus());
        Assert.Contains(dead.StaleText!, Peer(dead).GetItemStatus()!);
    }

    [AvaloniaFact]
    public void 偏离设定值进ItemStatus()
    {
        var readout = Mount(new Readout
        {
            Value = 85.4, Format = "F1", Setpoint = 60, Tolerance = 1,
        });

        Assert.Contains("偏离", Peer(readout).GetItemStatus()!);
    }

    [AvaloniaFact]
    public void 读数不可写()
    {
        var peer = Provider<IValueProvider>(Mount(new Readout()));

        Assert.True(peer.IsReadOnly);
        Assert.Throws<ElementNotEnabledException>(() => peer.SetValue("1"));
    }

    // ---- 三、StatusIndicator：Value 是枚举名不是显示文字 ----------------------

    [AvaloniaFact]
    public void 状态灯的值是枚举名()
    {
        // 显示文字会随本地化变。断言挂在它上面的话，界面一翻译脚本就全红。
        var indicator = Mount(new StatusIndicator { State = DeviceState.Fault, Label = "主轴" });

        Assert.Equal("Fault", Provider<IValueProvider>(indicator).Value);
        Assert.Equal("主轴", Peer(indicator).GetName());
    }

    [AvaloniaFact]
    public void 状态灯的值跟着状态走()
    {
        var indicator = Mount(new StatusIndicator { State = DeviceState.Running });
        var peer = Provider<IValueProvider>(indicator);

        Assert.Equal("Running", peer.Value);

        indicator.State = DeviceState.Offline;

        Assert.Equal("Offline", peer.Value);
    }

    // ---- 四、AlarmBanner：活动区域的级别 -------------------------------------

    [AvaloniaTheory]
    [InlineData(AlarmSeverity.Fault, AutomationLiveSetting.Assertive)]
    [InlineData(AlarmSeverity.Alarm, AutomationLiveSetting.Assertive)]
    [InlineData(AlarmSeverity.Warning, AutomationLiveSetting.Polite)]
    [InlineData(AlarmSeverity.Info, AutomationLiveSetting.Off)]
    public void 报警级别决定要不要打断朗读(AlarmSeverity severity, AutomationLiveSetting expected)
    {
        // 不声明活动区域的话，凭空出现的报警要等客户端轮询才被发现——
        // 而报警的价值全在于第一时间被察觉。
        var banner = Mount(new AlarmBanner { Severity = severity, Title = "超温" });

        Assert.Equal(expected, Peer(banner).GetLiveSetting());
    }

    [AvaloniaFact]
    public void 报警名字带级别和正文()
    {
        var banner = Mount(new AlarmBanner
        {
            Severity = AlarmSeverity.Alarm, Title = "腔体超温", Detail = "实测 132 °C",
        });

        var name = Peer(banner).GetName()!;

        Assert.Contains("Alarm", name);
        Assert.Contains("腔体超温", name);
        Assert.Contains("实测 132 °C", name);
    }

    [AvaloniaFact]
    public void 确认状态可读且可调()
    {
        var banner = Mount(new AlarmBanner { Severity = AlarmSeverity.Alarm, Title = "超温" });

        Assert.Contains("未确认", Peer(banner).GetItemStatus()!);

        Provider<IInvokeProvider>(banner).Invoke();

        Assert.True(banner.IsAcknowledged);
        Assert.Contains("已确认", Peer(banner).GetItemStatus()!);
    }

    [AvaloniaFact]
    public void 宿主拒收确认时自动化也确认不了()
    {
        // Acknowledge() 自己有拒收闸，对等体不该绕过它——否则自动化能把一条
        // 宿主明确不许确认的报警标成已确认。
        var gate = new GateCommand { CanRun = false };
        var banner = Mount(new AlarmBanner
        {
            Severity = AlarmSeverity.Alarm, Title = "超温", AcknowledgeCommand = gate,
        });

        Provider<IInvokeProvider>(banner).Invoke();

        Assert.False(banner.IsAcknowledged);
        Assert.Equal(0, gate.Executions);
    }

    // ---- 五、ParameterRow：写入链路 -----------------------------------------

    [AvaloniaFact]
    public void 参数行的写入链路能被自动化走完()
    {
        // 「写入设定值 → 读回状态 → 断言进了 Writing → 断言回到 Clean」
        // 是验收脚本最常见的一条链路，全靠 Value + ItemStatus。
        var row = Mount(new ParameterRow
        {
            Label = "目标温度", Unit = "°C", Setpoint = 60, Minimum = 0, Maximum = 200,
        });
        var value = Provider<IValueProvider>(row);

        Assert.Equal("Clean", Status(row).Split(" · ")[0]);

        value.SetValue("85");
        Assert.Equal("85", value.Value);
        Assert.Equal("Dirty", Status(row).Split(" · ")[0]);

        row.Apply();
        Assert.Equal("Writing", Status(row).Split(" · ")[0]);

        row.CompleteWrite(85);
        Assert.Equal("Clean", Status(row).Split(" · ")[0]);
    }

    [AvaloniaFact]
    public void 等回读期间自动化写不进去()
    {
        // 界面上输入框此时是锁的。自动化能绕过去的话，「写入中」的徽章下面
        // 可以并排显示一个从未下发、也从未校验过的数字。
        var row = Mount(new ParameterRow { Label = "目标温度", Setpoint = 60, Maximum = 200 });
        var value = Provider<IValueProvider>(row);

        value.SetValue("85");
        row.Apply();

        Assert.True(row.IsInputLocked);
        Assert.True(value.IsReadOnly);
        Assert.Throws<ElementNotEnabledException>(() => value.SetValue("99"));
        Assert.Equal("85", value.Value);
    }

    [AvaloniaFact]
    public void 只读参数行自动化也写不进去()
    {
        var row = Mount(new ParameterRow { Label = "序列号", IsReadOnly = true, Setpoint = 60 });

        Assert.True(Provider<IValueProvider>(row).IsReadOnly);
        Assert.Throws<ElementNotEnabledException>(() => Provider<IValueProvider>(row).SetValue("1"));
    }

    [AvaloniaFact]
    public void 量程进HelpText()
    {
        var row = Mount(new ParameterRow { Minimum = 0, Maximum = 200 });

        Assert.Contains("200", Peer(row).GetHelpText()!);
    }

    [AvaloniaFact]
    public void 超量程在ItemStatus里看得见()
    {
        var row = Mount(new ParameterRow { Setpoint = 60, Minimum = 0, Maximum = 200 });

        Provider<IValueProvider>(row).SetValue("999");

        Assert.Equal("OutOfRange", Status(row).Split(" · ")[0]);
    }

    // ---- 六、EStopButton：锁上没有 + 不给近路 --------------------------------

    [AvaloniaFact]
    public void 急停锁定状态可读()
    {
        var stop = Mount(new EStopButton());
        var toggle = Provider<IToggleProvider>(stop);

        Assert.Equal(ToggleState.Off, toggle.ToggleState);
        Assert.Contains("Ready", Status(stop));

        stop.Engage();

        Assert.Equal(ToggleState.On, toggle.ToggleState);
        Assert.Contains("Engaged", Status(stop));
    }

    [AvaloniaFact]
    public void 自动化能触发急停()
    {
        var stop = Mount(new EStopButton());

        Provider<IToggleProvider>(stop).Toggle();

        Assert.True(stop.IsEngaged);
    }

    [AvaloniaFact]
    public void 自动化不能用Toggle解锁急停()
    {
        // 复位默认要求长按，那道门存在的理由就是防误碰。让自动化一次调用就把
        // 自锁解掉，等于给它开了一条现场操作员都没有的近路。
        var stop = Mount(new EStopButton());
        stop.Engage();

        Assert.Throws<ElementNotEnabledException>(() => Provider<IToggleProvider>(stop).Toggle());
        Assert.True(stop.IsEngaged);

        // 显式复位仍然走得通——挡的是「Toggle 当成复位用」，不是复位本身。
        stop.Reset();
        Assert.False(stop.IsEngaged);
    }

    [AvaloniaFact]
    public void 急停按钮报出硬件位置()
    {
        // 软急停不是急停。这句提示是它在自动化上唯一能说清自己身份的地方。
        var stop = Mount(new EStopButton { HardwareLocationHint = "面板右下角红蘑菇头" });

        Assert.Equal("面板右下角红蘑菇头", Peer(stop).GetHelpText());
    }

    // ---- 七、JogButton -------------------------------------------------------

    [AvaloniaFact]
    public void 点动按钮的名字带方向()
    {
        // 一屏多个点动键，只有 Content 文字的话自动化客户端分不出哪个是哪个轴。
        var jog = Mount(new JogButton { Content = "X 轴", Direction = JogDirection.Left });

        var name = Peer(jog).GetName()!;

        Assert.Contains("X 轴", name);
        Assert.Contains("Left", name);
    }

    [AvaloniaFact]
    public void 正在点动看得见()
    {
        var jog = Mount(new JogButton { Content = "X 轴" });

        Assert.Contains("Idle", Status(jog));

        jog.RaiseEvent(KeyDown(Key.Space));

        Assert.Contains("Jogging", Status(jog));
    }

    [AvaloniaFact]
    public void 停止指令未下发会在自动化上报出来()
    {
        // 这是 fail-safe 方向最要紧的一处：停不下来的时候界面上只多一圈描边，
        // 自动化必须能读到，否则监控脚本会把「还在动」当成「已停止」。
        var jog = Mount(new JogButton
        {
            Content = "X 轴", StopCommand = new GateCommand { CanRun = false },
        });
        jog.RaiseEvent(KeyDown(Key.Space));

        jog.Stop(JogStopReason.KeyReleased);

        Assert.True(jog.IsJogging);
        Assert.Contains("停止指令未下发", Status(jog));
    }

    // ---- 八、Heartbeat / DeviceStatusBar ------------------------------------

    [AvaloniaFact]
    public void 心跳停跳可读()
    {
        var beat = Mount(new Heartbeat { Timeout = TimeSpan.FromSeconds(2) });
        var value = Provider<IValueProvider>(beat);

        beat.Beat();
        Assert.Equal("Beating", value.Value);

        beat.Restore(TimeSpan.FromSeconds(30));
        Assert.Equal("Stopped", value.Value);
    }

    [AvaloniaFact]
    public void 设备状态栏靠端点区分身份()
    {
        // 一屏挂着好几台设备时，「设备状态」四个字对客户端毫无用处。
        var bar = Mount(new DeviceStatusBar
        {
            Endpoint = "192.168.1.10:502", ConnectionState = ConnectionState.Degraded,
        });

        Assert.Contains("192.168.1.10:502", Peer(bar).GetName()!);
        Assert.Contains("Degraded", Status(bar));
    }

    // ---- 九、NumericKeypad ---------------------------------------------------

    [AvaloniaFact]
    public void 键盘缓冲可读可写()
    {
        var pad = Mount(new NumericKeypad { Label = "目标温度", Unit = "°C", Maximum = 200 });
        var value = Provider<IValueProvider>(pad);

        value.SetValue("85");

        Assert.Equal("85", value.Value);
        Assert.Equal("°C", Peer(pad).GetItemType());
        Assert.Contains("可提交", Status(pad));
    }

    [AvaloniaFact]
    public void 键盘的写入走LoadValue而不是直接改Text()
    {
        // 直接写 Text 会绕过量程判定：不可提交的值看起来会像可提交的。
        var pad = Mount(new NumericKeypad { Minimum = 0, Maximum = 200 });

        Provider<IValueProvider>(pad).SetValue("999");

        Assert.False(pad.CanCommit);
        Assert.Contains("不可提交", Status(pad));
    }

    // ---- 十、通用控件 --------------------------------------------------------

    [AvaloniaFact]
    public void 关掉的横幅退出自动化树()
    {
        // 留在树里的话，客户端会读到一条屏幕上根本不存在的通知。
        var bar = Mount(new InfoBar { Title = "已保存", IsOpen = true });

        Assert.True(Peer(bar).IsControlElement());

        bar.IsOpen = false;

        Assert.False(Peer(bar).IsControlElement());
    }

    [AvaloniaTheory]
    [InlineData(InfoBarSeverity.Error, AutomationLiveSetting.Assertive)]
    [InlineData(InfoBarSeverity.Warning, AutomationLiveSetting.Polite)]
    [InlineData(InfoBarSeverity.Success, AutomationLiveSetting.Polite)]
    [InlineData(InfoBarSeverity.Informational, AutomationLiveSetting.Polite)]
    public void 通知横幅是活动区域(InfoBarSeverity severity, AutomationLiveSetting expected)
    {
        var bar = Mount(new InfoBar { Severity = severity, Title = "提示", IsOpen = true });

        Assert.Equal(expected, Peer(bar).GetLiveSetting());
    }

    [AvaloniaFact]
    public void 浮层提示总是Polite()
    {
        // Toast 会自己消失，打断当前朗读不划算。
        var toast = Mount(new Toast { Severity = InfoBarSeverity.Error, Title = "失败" });

        Assert.Equal(AutomationLiveSetting.Polite, Peer(toast).GetLiveSetting());
    }

    [AvaloniaFact]
    public void 角标的计数可读且不可写()
    {
        var badge = Mount(new InfoBadge { Text = "12" });

        Assert.Equal("12", Provider<IValueProvider>(badge).Value);
        Assert.Throws<ElementNotEnabledException>(
            () => Provider<IValueProvider>(badge).SetValue("0"));
    }

    [AvaloniaFact]
    public void 圆点角标报的是有更新而不是空字符串()
    {
        var badge = Mount(new InfoBadge { IsDot = true });

        Assert.Contains("有更新", Peer(badge).GetName()!);
        Assert.Equal("", Provider<IValueProvider>(badge).Value);
    }

    [AvaloniaFact]
    public void 分页报成有界数值()
    {
        // 客户端据此就知道翻到头了没有，不用去猜「下一页」按钮还能不能点。
        var pager = Mount(new Pagination { PageCount = 7, CurrentPage = 3 });
        var range = Provider<IRangeValueProvider>(pager);

        Assert.Equal(3, range.Value);
        Assert.Equal(1, range.Minimum);
        Assert.Equal(7, range.Maximum);

        range.SetValue(5);

        Assert.Equal(5, pager.CurrentPage);
    }

    [AvaloniaFact]
    public void 空页时分页的下界是零()
    {
        // Minimum=1 而 Maximum=0 是个非法区间，客户端读到会算出负的可翻页数。
        var range = Provider<IRangeValueProvider>(Mount(new Pagination { PageCount = 0 }));

        Assert.Equal(0, range.Minimum);
        Assert.Equal(0, range.Maximum);
        Assert.True(range.IsReadOnly, "没有页就没什么可翻的");
        Assert.Throws<ElementNotEnabledException>(() => range.SetValue(1));
    }

    [AvaloniaFact]
    public void 分页不接受量程外的页码()
    {
        // 报了 Maximum=7 却接受 999，客户端读回来会得到「第 999 页 / 共 7 页」——
        // 而这个状态在界面上点不出来：Prev / Next 自己是夹过的。
        var pager = Mount(new Pagination { PageCount = 7, CurrentPage = 3 });
        var range = Provider<IRangeValueProvider>(pager);

        range.SetValue(999);
        Assert.Equal(7, pager.CurrentPage);

        range.SetValue(-5);
        Assert.Equal(1, pager.CurrentPage);
    }

    [AvaloniaFact]
    public void 分页拒收非有限页码()
    {
        // (int)double.NaN 不抛异常，会悄悄落成 0——客户端写进一个非法值，
        // 读回来的却是一个看着正常的页码。
        var range = Provider<IRangeValueProvider>(Mount(new Pagination { PageCount = 7 }));

        Assert.Throws<ArgumentOutOfRangeException>(() => range.SetValue(double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => range.SetValue(double.PositiveInfinity));
    }

    [AvaloniaFact]
    public void 空状态把原因读出来()
    {
        var empty = Mount(new EmptyState { Title = "暂无报警", Description = "最近 24 小时内没有记录" });

        var name = Peer(empty).GetName()!;

        Assert.Contains("暂无报警", name);
        Assert.Contains("最近 24 小时内没有记录", name);
    }

    [AvaloniaFact]
    public void 没有名字的头像退出自动化树()
    {
        // 有名字的头像承载身份；没有的就是个彩色圆圈，进树只是噪音。
        Assert.False(Peer(Mount(new PersonPicture())).IsControlElement());
        Assert.True(Peer(Mount(new PersonPicture { DisplayName = "张伟" })).IsControlElement());
    }

    [AvaloniaFact]
    public void 图表报成Custom并给出本地化类型名()
    {
        // UI Automation 没有「图表」这个控件类型。硬套 Image 或 Table 会让客户端
        // 去找根本不存在的行列结构。
        var chart = Mount(new TrendChart());

        Assert.Equal(AutomationControlType.Custom, Peer(chart).GetAutomationControlType());
        Assert.Equal("趋势图", Peer(chart).GetLocalizedControlType());
    }

    // ---- 辅助 ----------------------------------------------------------------

    private static AutomationPeer Peer(Control control) =>
        ControlAutomationPeer.CreatePeerForElement(control);

    private static T Provider<T>(Control control) where T : class
    {
        var provider = Peer(control).GetProvider<T>();
        Assert.NotNull(provider);
        return provider!;
    }

    private static KeyEventArgs KeyDown(Key key) => new()
    {
        RoutedEvent = InputElement.KeyDownEvent,
        Key = key,
    };

    private static string Status(Control control) => Peer(control).GetItemStatus() ?? "";

    private static T Mount<T>(T control) where T : Control
    {
        var window = new Window
        {
            Width = 900,
            Height = 300,
            Content = new StackPanel { Children = { control } },
        };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        window.Measure(new Size(900, 300));
        window.Arrange(new Rect(0, 0, 900, 300));
        Dispatcher.UIThread.RunJobs();
        return control;
    }

    private sealed class GateCommand : System.Windows.Input.ICommand
    {
        public bool CanRun { get; set; } = true;

        public int Executions { get; private set; }

        public event EventHandler? CanExecuteChanged;

        public bool CanExecute(object? parameter) => CanRun;

        public void Execute(object? parameter) => Executions++;
    }
}
