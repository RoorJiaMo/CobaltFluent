using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Cobalt.Fluent.Controls;

namespace Cobalt.Fluent.Automation;

// 通用控件的自动化对等体。
//
// 判据只有一条：**这个元素能不能让自动化客户端做出一个它做不到的判断。**
// 能，就给名字、类型和值；不能，就让它整个退出自动化树——
// 装饰性元素留在树里不是「多一点信息」，是把真正要读的东西淹掉。

/// <summary>
/// 通知类横幅（InfoBar）。
///
/// 和 AlarmBanner 同理，这是凭空出现的元素，必须声明为活动区域，
/// 否则读屏软件与自动化客户端都要靠轮询才发现它。级别比工业报警低一档：
/// Error 才用 Assertive，其余用 Polite。
/// </summary>
public class InfoBarAutomationPeer(InfoBar owner) : ControlAutomationPeer(owner)
{
    private InfoBar Control => (InfoBar)Owner;

    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

    protected override string GetClassNameCore() => nameof(InfoBar);

    protected override string? GetNameCore() =>
        PeerText.Join(Control.Severity.ToString(), Control.Title, Control.Message);

    protected override AutomationLiveSetting GetLiveSettingCore() =>
        Control.Severity == InfoBarSeverity.Error
            ? AutomationLiveSetting.Assertive
            : AutomationLiveSetting.Polite;

    /// <summary>关掉的横幅不该还留在树里被读到。</summary>
    protected override bool IsControlElementCore() => Control.IsOpen;
}

/// <summary>浮层提示（Toast）。凭空出现，同样是活动区域；但它会自己消失，用 Polite。</summary>
public class ToastAutomationPeer(Toast owner) : ControlAutomationPeer(owner)
{
    private Toast Control => (Toast)Owner;

    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

    protected override string GetClassNameCore() => nameof(Toast);

    protected override string? GetNameCore() =>
        PeerText.Join(Control.Severity.ToString(), Control.Title, Control.Message);

    protected override AutomationLiveSetting GetLiveSettingCore() => AutomationLiveSetting.Polite;
}

/// <summary>角标。Value 给计数文字——它的全部信息就是那个数。</summary>
public class InfoBadgeAutomationPeer(InfoBadge owner) : ControlAutomationPeer(owner), IValueProvider
{
    private InfoBadge Control => (InfoBadge)Owner;

    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Text;

    protected override string GetClassNameCore() => nameof(InfoBadge);

    protected override string? GetNameCore() =>
        PeerText.Join(Control.Severity.ToString(), Control.IsDot ? CobaltStrings.Current.HasUpdates : Control.Text);

    public string Value => Control.IsDot ? "" : Control.Text ?? "";

    public bool IsReadOnly => true;

    public void SetValue(string? value) =>
        throw new ElementNotEnabledException("An InfoBadge is driven by its data source and cannot be written from the UI.");
}

/// <summary>引导提示。关掉时退出自动化树。</summary>
public class TeachingTipAutomationPeer(TeachingTip owner) : ControlAutomationPeer(owner)
{
    private TeachingTip Control => (TeachingTip)Owner;

    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

    protected override string GetClassNameCore() => nameof(TeachingTip);

    protected override string? GetNameCore() => PeerText.Join(Control.Title, Control.Subtitle);

    protected override bool IsControlElementCore() => Control.IsOpen;
}

/// <summary>
/// 分页。用 IRangeValueProvider 而不是 IValueProvider：页码是有界的数值，
/// 自动化客户端据此就知道翻到头了没有，不用去猜「下一页」按钮还能不能点。
/// </summary>
public class PaginationAutomationPeer(Pagination owner)
    : ControlAutomationPeer(owner), IRangeValueProvider
{
    private Pagination Control => (Pagination)Owner;

    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Spinner;

    protected override string GetClassNameCore() => nameof(Pagination);

    protected override string? GetNameCore() => CobaltStrings.Current.PaginationName;

    protected override string? GetItemStatusCore() => Control.InfoText;

    public double Value => Control.CurrentPage;

    public double Minimum => Control.PageCount > 0 ? 1 : 0;

    public double Maximum => Control.PageCount;

    /// <summary>没有页的时候没什么可翻的。</summary>
    public bool IsReadOnly => Control.PageCount <= 0;

    public double SmallChange => 1;

    public double LargeChange => 1;

    public void SetValue(double value)
    {
        if (IsReadOnly)
            throw new ElementNotEnabledException("The pager currently has no pages.");

        // NaN 与 ±∞ 直接拒收。`(int)double.NaN` 不抛异常，会悄悄落成 0——
        // 那样客户端写进一个非法值，读回来的却是一个看着正常的页码。
        if (!double.IsFinite(value))
            throw new ArgumentOutOfRangeException(nameof(value), "The page number must be a finite value.");

        // 夹进量程，和 PART_Prev / PART_Next 的做法一致。
        // 自动化不该能把控件推进一个指针操作根本到不了的状态——
        // IRangeValueProvider 报了 Maximum=7 却接受 999，客户端读回来会得到
        // 「第 999 页 / 共 7 页」，而这个状态在界面上点不出来。
        Control.CurrentPage = (int)Math.Clamp(value, Minimum, Maximum);
    }
}

/// <summary>空状态。它承载的是「为什么这里什么都没有」，那句话必须能被读到。</summary>
public class EmptyStateAutomationPeer(EmptyState owner) : ControlAutomationPeer(owner)
{
    private EmptyState Control => (EmptyState)Owner;

    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Text;

    protected override string GetClassNameCore() => nameof(EmptyState);

    protected override string? GetNameCore() => PeerText.Join(Control.Title, Control.Description);
}

/// <summary>头像。名字是它唯一的信息，图片本身不是。</summary>
public class PersonPictureAutomationPeer(PersonPicture owner) : ControlAutomationPeer(owner)
{
    private PersonPicture Control => (PersonPicture)Owner;

    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Image;

    protected override string GetClassNameCore() => nameof(PersonPicture);

    protected override string? GetNameCore() => Control.DisplayName;

    /// <summary>没有名字的头像就是纯装饰，不该占一个树节点。</summary>
    protected override bool IsControlElementCore() => !string.IsNullOrWhiteSpace(Control.DisplayName);
}

/// <summary>表格工具条。</summary>
public class DataGridToolbarAutomationPeer(DataGridToolbar owner) : ControlAutomationPeer(owner)
{
    private DataGridToolbar Control => (DataGridToolbar)Owner;

    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.ToolBar;

    protected override string GetClassNameCore() => nameof(DataGridToolbar);

    protected override string? GetItemStatusCore() => Control.CountText;
}

/// <summary>导航视图。</summary>
public class NavigationViewAutomationPeer(NavigationView owner) : ControlAutomationPeer(owner)
{
    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Pane;

    protected override string GetClassNameCore() => nameof(NavigationView);

    protected override string? GetNameCore() => CobaltStrings.Current.NavigationName;
}

/// <summary>图例。</summary>
public class ChartLegendAutomationPeer(ChartLegend owner) : ControlAutomationPeer(owner)
{
    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

    protected override string GetClassNameCore() => nameof(ChartLegend);

    protected override string? GetNameCore() => CobaltStrings.Current.LegendName;
}

// ---------------------------------------------------------------------------
// 图表
//
// UI Automation 没有「图表」这个控件类型。把整张图报成一个 Custom 节点、
// 名字说清楚它画的是什么，比硬套 Image 或 Table 都诚实——后者会让客户端
// 去找根本不存在的行列结构。
// ---------------------------------------------------------------------------

/// <summary>
/// 趋势图。轨迹球选中的那个点是这张图上唯一可被断言的量，放 ItemStatus。
/// </summary>
public class TrendChartAutomationPeer(TrendChart owner) : ControlAutomationPeer(owner)
{
    private TrendChart Control => (TrendChart)Owner;

    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Custom;

    protected override string GetClassNameCore() => nameof(TrendChart);

    protected override string GetLocalizedControlTypeCore() => CobaltStrings.Current.TrendChartName;

    protected override string? GetNameCore() =>
        PeerText.Join(CobaltStrings.Current.TrendChartName,
            Control.Series is { Count: > 0 } s ? CobaltStrings.Current.SeriesCount(s.Count) : null);

    // TrackballIndex 是 int?：`>= 0` 在 null 上也答 false，但那是可空提升的
    // 副作用而不是这里想表达的判断。显式匹配，别让语义挂在比较运算的边角规则上。
    protected override string? GetItemStatusCore() =>
        Control.TrackballIndex is { } i ? CobaltStrings.Current.TrackballAt(i) : null;
}

/// <summary>柱状图。</summary>
public class BarChartAutomationPeer(BarChart owner) : ControlAutomationPeer(owner)
{
    private BarChart Control => (BarChart)Owner;

    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Custom;

    protected override string GetClassNameCore() => nameof(BarChart);

    protected override string GetLocalizedControlTypeCore() => CobaltStrings.Current.BarChartName;

    protected override string? GetNameCore() =>
        PeerText.Join(CobaltStrings.Current.BarChartName,
            Control.Categories is { Count: > 0 } c ? CobaltStrings.Current.CategoryCount(c.Count) : null);
}

/// <summary>迷你趋势线。趋势方向是它唯一的语义。</summary>
public class SparklineAutomationPeer(Sparkline owner) : ControlAutomationPeer(owner)
{
    private Sparkline Control => (Sparkline)Owner;

    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Custom;

    protected override string GetClassNameCore() => nameof(Sparkline);

    protected override string GetLocalizedControlTypeCore() => CobaltStrings.Current.SparklineName;

    protected override string? GetNameCore() => PeerText.Join(CobaltStrings.Current.TrendName, Control.Trend.ToString());
}

// ---------------------------------------------------------------------------
// 主动退出自动化树
//
// 这几个元素不承载任何自动化客户端做不到的判断。留在树里不是「多一点信息」，
// 是让 Inspect 和验收脚本要在一堆没有名字的节点里翻找真正要读的那个。
// NoneAutomationPeer 是 Avalonia 为此提供的标准做法。
// ---------------------------------------------------------------------------

/// <summary>
/// 标题栏。
///
/// Name 是窗口标题（<see cref="TitleBar.EffectiveTitle"/>，没给 Title 时就是窗口自己的），
/// <b>ItemStatus 报出贴靠布局到底可不可用</b>。
///
/// 「最大化钮悬停没弹出布局面板」是自绘标题栏最常见的投诉，而原因往往不在标题栏本身——
/// 窗口设了不可缩放、跑在 Windows 10、最大化钮被藏起来，三种都会让 shell 不弹。
/// 把结论摆在自动化树上，排查时一眼能看见，不用去猜。
/// </summary>
public class TitleBarAutomationPeer(TitleBar owner) : ControlAutomationPeer(owner)
{
    private TitleBar Control => (TitleBar)Owner;

    protected override AutomationControlType GetAutomationControlTypeCore() =>
        AutomationControlType.TitleBar;

    protected override string GetClassNameCore() => nameof(TitleBar);

    protected override string? GetNameCore() => Control.EffectiveTitle;

    protected override string? GetItemStatusCore() => Control.SupportsSnapLayouts
        ? CobaltStrings.Current.SnapLayoutsAvailable
        : CobaltStrings.Current.SnapLayoutsUnavailable;
}

/// <summary>
/// 贴靠布局面板。
///
/// Name 是「贴靠布局」，ItemStatus 报出当前这块屏幕给了几套布局——
/// 竖屏、窄屏、带鱼屏拿到的套数不一样，排查「我这台机器上怎么少一个布局」
/// 时先看这里。
///
/// 面板本身是分组容器，真正可操作的是里面那些格子，各自带方位朗读名。
/// </summary>
public class SnapLayoutPickerAutomationPeer(SnapLayoutPicker owner) : ControlAutomationPeer(owner)
{
    private SnapLayoutPicker Control => (SnapLayoutPicker)Owner;

    protected override AutomationControlType GetAutomationControlTypeCore() =>
        AutomationControlType.Group;

    protected override string GetClassNameCore() => nameof(SnapLayoutPicker);

    protected override string? GetNameCore() => CobaltStrings.Current.SnapLayouts;

    protected override string? GetItemStatusCore() =>
        CobaltStrings.Current.SnapLayoutCount(Control.Layouts.Count);
}

/// <summary>
/// 装饰性元素的对等体。
///
/// 用在：占位骨架（内容还没来）、分隔线（纯视觉分组）、图标（含义由它所在的
/// 按钮或标签承载，图标自己重复一遍只会让朗读变啰嗦）、仪表的弧段
/// （它是 Gauge 的绘制部件，Gauge 本身已经报了值）。
/// </summary>
public class DecorativeAutomationPeer(Avalonia.Controls.Control owner) : NoneAutomationPeer(owner);
