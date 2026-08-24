using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Cobalt.Fluent.Controls;

namespace Cobalt.Fluent.Automation;

/// <summary>
/// 第 7 组控件的自动化对等体。
///
/// 这一组的自动化不是「给读屏软件用的」那种可有可无的东西：**工业 HMI 的验收
/// 普遍用 UI Automation 驱动界面跑回归**，测试台要能读出读数、参数状态、急停有没有
/// 锁上。没有对等体时这些控件在 Inspect 里只是一团没有名字的 Custom 矩形，
/// 使用方的脚本根本抓不到。
///
/// 三条贯穿这一组的原则：
///
/// 1. <b>Value 只放机器可读的那个量，判读上下文放 ItemStatus。</b>
///    测试台读到 "85.4" 无从判断这是实时值还是五分钟前的死值——
///    过期、偏离、写入中这些都属于 ItemStatus，混进 Value 会让断言没法写。
/// 2. <b>危险动作不通过自动化模式暴露成一次调用。</b>急停的长按复位存在的理由
///    就是防误碰，自动化客户端不该绕过它。
/// 3. <b>装饰性元素主动退出自动化树。</b>占位骨架、分隔线进树只是噪音，
///    会把真正要读的东西淹掉。
/// </summary>
internal static class PeerText
{
    /// <summary>把若干段拼成一行，空段跳过。自动化名字里不该出现「 · 」开头这种残句。</summary>
    public static string? Join(params string?[] parts)
    {
        var kept = parts.Where(p => !string.IsNullOrWhiteSpace(p)).ToArray();
        return kept.Length == 0 ? null : string.Join(" · ", kept);
    }
}

// ---------------------------------------------------------------------------

/// <summary>
/// 读数。Value 只给格式化后的数字，单位进 ItemType，新鲜度与偏离进 ItemStatus。
/// </summary>
public class ReadoutAutomationPeer(Readout owner) : ControlAutomationPeer(owner), IValueProvider
{
    private Readout Control => (Readout)Owner;

    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Text;

    protected override string GetClassNameCore() => nameof(Readout);

    protected override string? GetNameCore() => Control.Label;

    /// <summary>工程单位。放这里而不是拼进 Value——断言数值时不该还要剥单位。</summary>
    protected override string? GetItemTypeCore() => Control.Unit;

    /// <summary>
    /// 新鲜度与偏离。<b>这一条是本对等体存在的主要理由</b>：
    /// 只读到数字的测试台分不出「85.4 是当前值」和「85.4 是通信断开前的最后一个值」，
    /// 而这两种情况在屏幕上也只差一个灰度。
    /// </summary>
    protected override string? GetItemStatusCore() => PeerText.Join(
        Control.StaleText,
        Control.Classes.Contains(":deviating") ? "偏离设定值" : null,
        Control.Classes.Contains(":invalid") ? "读值无效" : null,
        Control.Classes.Contains(":nodata") ? "无数据" : null);

    public string Value => Control.DisplayValue ?? "";

    public bool IsReadOnly => true;

    public void SetValue(string? value) =>
        throw new ElementNotEnabledException("Readout 是只读显示控件，设定值请写 ParameterRow。");
}

/// <summary>
/// 状态灯。Value 给状态枚举名（机器可读），Name 给标签（人读）。
/// </summary>
public class StatusIndicatorAutomationPeer(StatusIndicator owner)
    : ControlAutomationPeer(owner), IValueProvider
{
    private StatusIndicator Control => (StatusIndicator)Owner;

    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Text;

    protected override string GetClassNameCore() => nameof(StatusIndicator);

    protected override string? GetNameCore() => Control.Label;

    /// <summary>枚举名而不是显示文字：显示文字会随本地化变，断言不能挂在它上面。</summary>
    public string Value => Control.State.ToString();

    public bool IsReadOnly => true;

    public void SetValue(string? value) =>
        throw new ElementNotEnabledException("设备状态由设备侧决定，界面不可写。");
}

/// <summary>
/// 心跳。停跳是「链路断了」的最快信号，必须能被自动化读到。
/// </summary>
public class HeartbeatAutomationPeer(Heartbeat owner) : ControlAutomationPeer(owner), IValueProvider
{
    private Heartbeat Control => (Heartbeat)Owner;

    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Text;

    protected override string GetClassNameCore() => nameof(Heartbeat);

    protected override string? GetNameCore() => "通信心跳";

    public string Value => Control.IsStopped ? "Stopped" : "Beating";

    public bool IsReadOnly => true;

    public void SetValue(string? value) =>
        throw new ElementNotEnabledException("心跳由通信事件驱动，界面不可写。");
}

/// <summary>
/// 报警横幅。
///
/// <b>GetLiveSettingCore 是这一组里最要紧的一处。</b>报警是「凭空出现」的元素，
/// 不声明为活动区域的话，读屏软件与自动化客户端都要靠轮询才发现它——
/// 而报警的价值全在于第一时间被察觉。Alarm / Fault 用 Assertive（打断当前朗读），
/// Warning 用 Polite（等当前朗读结束），Info 不播报。
/// </summary>
public class AlarmBannerAutomationPeer(AlarmBanner owner)
    : ControlAutomationPeer(owner), IInvokeProvider
{
    private AlarmBanner Control => (AlarmBanner)Owner;

    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

    protected override string GetClassNameCore() => nameof(AlarmBanner);

    protected override string? GetNameCore() =>
        PeerText.Join(Control.Severity.ToString(), Control.Title, Control.Detail);

    protected override string? GetItemStatusCore() => PeerText.Join(
        Control.IsAcknowledged ? "已确认" : "未确认",
        Control.AdditionalCount > 0 ? $"另有 {Control.AdditionalCount} 条同类报警" : null);

    protected override AutomationLiveSetting GetLiveSettingCore() => Control.Severity switch
    {
        AlarmSeverity.Alarm or AlarmSeverity.Fault => AutomationLiveSetting.Assertive,
        AlarmSeverity.Warning => AutomationLiveSetting.Polite,
        _ => AutomationLiveSetting.Off,
    };

    /// <summary>确认。宿主拒收时 Acknowledge() 自己会挡住，这里不重复判断。</summary>
    public void Invoke() => Control.Acknowledge();
}

/// <summary>
/// 参数行。Value 是待下发文本（可写），状态机进 ItemStatus。
///
/// 这是整组里自动化价值最高的一个：验收脚本要能「写入设定值 → 读回状态 →
/// 断言进了 Writing → 断言回到 Clean」，这条链路全靠 Value + ItemStatus。
/// </summary>
public class ParameterRowAutomationPeer(ParameterRow owner)
    : ControlAutomationPeer(owner), IValueProvider
{
    private ParameterRow Control => (ParameterRow)Owner;

    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Edit;

    protected override string GetClassNameCore() => nameof(ParameterRow);

    protected override string? GetNameCore() => Control.Label;

    protected override string? GetItemTypeCore() => Control.Unit;

    /// <summary>Clean / Dirty / Writing / Failed / OutOfRange 的枚举名 + 徽章文字。</summary>
    protected override string? GetItemStatusCore() =>
        PeerText.Join(Control.WriteState.ToString(), Control.StateText);

    protected override string? GetHelpTextCore() =>
        double.IsFinite(Control.Minimum) || double.IsFinite(Control.Maximum)
            ? $"量程 {Control.Minimum} – {Control.Maximum}"
            : null;

    public string Value => Control.PendingText ?? "";

    /// <summary>等回读期间与只读时不可写——和界面上输入框的锁是同一个判据。</summary>
    public bool IsReadOnly => Control.IsInputLocked;

    public void SetValue(string? value)
    {
        if (IsReadOnly)
            throw new ElementNotEnabledException("参数行当前不可编辑（只读或正在等待设备回读）。");

        Control.PendingText = value;
    }
}

/// <summary>
/// 点动按钮。继承 Button 的对等体，补上方向与「正在动作」——
/// 一屏多个点动键时，只有 Content 文字的话自动化客户端分不出哪个是哪个轴。
/// </summary>
public class JogButtonAutomationPeer(JogButton owner) : ButtonAutomationPeer(owner)
{
    private JogButton Control => (JogButton)Owner;

    protected override string GetClassNameCore() => nameof(JogButton);

    protected override string? GetNameCore() => PeerText.Join(
        base.GetNameCore(),
        Control.Direction == JogDirection.None ? null : Control.Direction.ToString());

    protected override string? GetItemStatusCore() => PeerText.Join(
        Control.IsJogging ? "Jogging" : "Idle",
        Control.Classes.Contains(":stopfailed") ? "停止指令未下发" : null);
}

/// <summary>
/// 急停。IToggleProvider 让自动化能读出「有没有锁上」——这是验收脚本必须能断言的状态。
///
/// <b>Toggle() 只触发，不解锁。</b>复位默认要求长按，那道门存在的理由就是防误碰；
/// 让自动化客户端一次调用就把自锁解掉，等于给它开了一条现场操作员都没有的近路。
/// 需要复位的测试请显式调 <see cref="EStopButton.Reset"/>。
/// </summary>
public class EStopButtonAutomationPeer(EStopButton owner)
    : ButtonAutomationPeer(owner), IToggleProvider
{
    private EStopButton Control => (EStopButton)Owner;

    protected override string GetClassNameCore() => nameof(EStopButton);

    protected override string? GetNameCore() =>
        PeerText.Join(base.GetNameCore(), Control.CaptionText);

    protected override string? GetHelpTextCore() => Control.HardwareLocationHint;

    protected override string? GetItemStatusCore() => PeerText.Join(
        Control.IsEngaged ? "Engaged" : "Ready",
        Control.Classes.Contains(":engagefailed") ? "急停指令未下发" : null);

    public ToggleState ToggleState => Control.IsEngaged ? ToggleState.On : ToggleState.Off;

    public void Toggle()
    {
        if (Control.IsEngaged)
            throw new ElementNotEnabledException(
                "急停已锁定。复位需要显式操作，不能通过 Toggle 绕过防误碰的长按。");

        Control.Engage();
    }
}

/// <summary>设备状态栏。</summary>
public class DeviceStatusBarAutomationPeer(DeviceStatusBar owner) : ControlAutomationPeer(owner)
{
    private DeviceStatusBar Control => (DeviceStatusBar)Owner;

    protected override AutomationControlType GetAutomationControlTypeCore() =>
        AutomationControlType.StatusBar;

    protected override string GetClassNameCore() => nameof(DeviceStatusBar);

    /// <summary>端点地址就是这条状态栏的身份——一屏可能挂着好几台设备。</summary>
    protected override string? GetNameCore() => PeerText.Join(Control.Endpoint, "设备状态");

    protected override string? GetItemStatusCore() => PeerText.Join(
        Control.ConnectionState.ToString(), Control.StateText, Control.PollRateText);

    protected override string? GetHelpTextCore() => Control.CurrentUser;
}

/// <summary>
/// 数字键盘。Value 是当前缓冲，能不能提交进 ItemStatus。
/// </summary>
public class NumericKeypadAutomationPeer(NumericKeypad owner)
    : ControlAutomationPeer(owner), IValueProvider
{
    private NumericKeypad Control => (NumericKeypad)Owner;

    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Edit;

    protected override string GetClassNameCore() => nameof(NumericKeypad);

    protected override string? GetNameCore() => PeerText.Join(Control.Label, "数字键盘");

    protected override string? GetItemTypeCore() => Control.Unit;

    protected override string? GetHelpTextCore() => Control.RangeText;

    protected override string? GetItemStatusCore() => PeerText.Join(
        Control.CanCommit ? "可提交" : "不可提交",
        Control.ValidationText);

    public string Value => Control.Text ?? "";

    public bool IsReadOnly => false;

    /// <summary>走 LoadValue 而不是直接写 Text：外部重设缓冲一律走那个入口。</summary>
    public void SetValue(string? value) => Control.LoadValue(value);
}
