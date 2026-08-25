using System.Globalization;

namespace Cobalt.Fluent;

/// <summary>
/// 控件内部生成的、面向最终用户的文字。
///
/// **为什么不是 resx。** `ResourceManager` 靠反射查卫星程序集，本库有 NativeAOT 闸口
/// （见 <c>tools/aot-gate.sh</c>），resx 会直接把闸口顶红。这里用普通的虚成员：
/// 零反射、能单独测、整块可换。
///
/// 默认按 <see cref="CultureInfo.CurrentUICulture"/> 选：中文环境给中文，其余给英文。
/// 要固定或换成自己的措辞，在应用启动时赋一次 <see cref="Current"/>：
///
/// <code>
/// CobaltStrings.Current = new CobaltStringsZhHans();          // 固定中文
/// CobaltStrings.Current = new MyPlantStrings();               // 厂内术语
/// </code>
///
/// <b>不要把这里的文字当成自动化断言的锚点。</b>验收脚本该断言的是
/// <c>AutomationId</c>、<c>ControlType</c> 和 <c>Value</c>——本库刻意让
/// <c>Value</c> 只放机器可读的量（枚举名、原始数字），而把会随语言变的判读上下文
/// 放在 <c>Name</c> / <c>ItemStatus</c> / <c>HelpText</c> 里，正是为了这条边界。
/// </summary>
public class CobaltStrings
{
    private static CobaltStrings? _current;

    /// <summary>当前这套措辞。首次读取时按界面语言选。</summary>
    public static CobaltStrings Current
    {
        get => _current ??= ForCulture(CultureInfo.CurrentUICulture);
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            if (ReferenceEquals(_current, value)) return;

            _current = value;
            CurrentChanged?.Invoke(null, EventArgs.Empty);
        }
    }

    /// <summary>
    /// <see cref="Current"/> 换了。控件订阅它来重算已经显示出来的文字。
    ///
    /// <b>只覆盖控件内部算出来的那部分。</b>作为属性默认值的文字（确认按钮、
    /// 表头这些）在控件构造时取一次，已经建出来的实例不会跟着变——
    /// 换语言通常发生在启动时或换班重启时，为运行时热切换给每个属性加一层投影
    /// 不划算。真要热切换到底，重建那一页即可。
    /// </summary>
    public static event EventHandler? CurrentChanged;

    /// <summary>按语言挑一套。识别不出来的一律给英文。</summary>
    public static CobaltStrings ForCulture(CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(culture);

        // 用 TwoLetterISOLanguageName 而不是精确匹配 zh-CN：
        // zh-Hans / zh-SG / zh-TW 都该拿到中文，而不是掉进英文。
        return culture.TwoLetterISOLanguageName == "zh"
            ? new CobaltStringsZhHans()
            : new CobaltStrings();
    }

    // ---- Readout ------------------------------------------------------------

    /// <summary>读数过期。</summary>
    public virtual string DataStale => "Data stale";

    /// <summary>给了值但没给时间戳，或时间戳在未来。</summary>
    public virtual string AgeUnknown => "Age unknown";

    /// <summary>拿到了 NaN / 无穷。</summary>
    public virtual string InvalidReading => "Invalid";

    /// <summary><c>Format</c> 非法，格式化抛了异常。</summary>
    public virtual string InvalidDisplayFormat => "Display format invalid";

    /// <summary>「最后更新 5 秒前」。整句交给实现，各语言的语序和复数不一样。</summary>
    public virtual string LastUpdated(TimeSpan ago) => $"Last updated {Age(ago)} ago";

    /// <summary>时长的短写。</summary>
    public virtual string Age(TimeSpan ago) => ago.TotalSeconds switch
    {
        < 60 => $"{(int)ago.TotalSeconds} s",
        < 3600 => $"{(int)ago.TotalMinutes} min",
        _ => $"{(int)ago.TotalHours} h",
    };

    /// <summary>过期时补在后面的一段：断开那一刻的偏差。</summary>
    public virtual string DeviationAtDisconnect(string signedDelta) =>
        $" · deviation at disconnect {signedDelta}";

    /// <summary>只给了设定值。</summary>
    public virtual string Target(string setpoint) => $"Target {setpoint}";

    /// <summary>给了设定值，也算出了偏差。</summary>
    public virtual string TargetWithDeviation(string setpoint, string signedDelta) =>
        $"Target {setpoint} · deviation {signedDelta}";

    /// <summary>容差非法，偏差监视这一档整个不成立。</summary>
    public virtual string TargetWithoutDeviationWatch(string setpoint, double tolerance) =>
        $"Target {setpoint} · deviation watch unavailable (tolerance {tolerance})";

    // ---- ParameterRow -------------------------------------------------------

    /// <summary>量程配置本身是错的。**不是操作员输错**，措辞要区分开。</summary>
    public virtual string RangeInvalid => "Range invalid — write blocked";

    public virtual string OutOfRange(string minimum, string maximum) =>
        $"Out of range {minimum}–{maximum}";

    /// <summary>回读值与下发值严格相等。</summary>
    public virtual string Applied => "Applied";

    /// <summary>回读值与下发值在显示精度内一致，但不严格相等（设备量化）。</summary>
    public virtual string MatchesWithinPrecision => "Matches within display precision";

    public virtual string PendingWrite => "Pending";

    public virtual string ReadOnlyState => "Read-only";

    public virtual string Writing => "Writing…";

    public virtual string WriteFailed => "Write failed";

    /// <summary>下发按钮。</summary>
    public virtual string Apply => "Apply";

    /// <summary>撤销按钮。</summary>
    public virtual string Revert => "Revert";

    // ---- ParameterTable 表头 ------------------------------------------------

    public virtual string ColumnParameter => "Parameter";

    public virtual string ColumnActual => "Actual";

    public virtual string ColumnSetpoint => "Setpoint";

    public virtual string ColumnUnit => "Unit";

    public virtual string ColumnState => "State";

    // ---- DeviceStatusBar ----------------------------------------------------

    public virtual string Connected => "Connected";

    public virtual string Degraded => "Link degraded";

    public virtual string Disconnected => "Link down";

    public virtual string PollRate(double hz) =>
        $"Polling {hz.ToString("0.#", CultureInfo.CurrentCulture)} Hz";

    // ---- NumericKeypad ------------------------------------------------------

    public virtual string NotANumber => "Not a number";

    public virtual string BelowMinimum(string minimum) => $"Below minimum {minimum}";

    public virtual string AboveMaximum(string maximum) => $"Above maximum {maximum}";

    // ---- Pagination ---------------------------------------------------------

    public virtual string PageInfo(int totalItems, int currentPage, int pageCount) =>
        $"{totalItems.ToString("N0", CultureInfo.CurrentCulture)} items · page {currentPage} of {pageCount}";

    public virtual string PageInfoWithoutTotal(int currentPage, int pageCount) =>
        $"Page {currentPage} of {pageCount}";

    // ---- AlarmBanner --------------------------------------------------------

    public virtual string AdditionalAlarms(int count) => $"{count} more of the same kind";

    public virtual string Acknowledge => "Acknowledge";

    public virtual string Details => "Details";

    public virtual string Acknowledged => "Acknowledged";

    public virtual string Unacknowledged => "Not acknowledged";

    // ---- EStopButton --------------------------------------------------------

    public virtual string EStopReady => "Ready";

    public virtual string EStopEngaged => "Engaged · reset required";

    /// <summary>急停指令没能下发。设备很可能还在动——措辞必须指向硬件急停。</summary>
    public virtual string EStopCommandNotSent => "Command not sent · use the hardware E-stop now";

    // ---- 通用 ---------------------------------------------------------------

    public virtual string Cancel => "Cancel";

    public virtual string GotIt => "Got it";

    public virtual string On => "On";

    public virtual string Off => "Off";

    // ---- 自动化对等体 -------------------------------------------------------
    //
    // Name / ItemStatus / LocalizedControlType / HelpText 是给人读的，按 UIA 的约定
    // 要本地化。Value 不在这里——它只放机器可读的量，脚本该断言的是那个。

    public virtual string HasUpdates => "Has updates";

    public virtual string PaginationName => "Pagination";

    public virtual string NavigationName => "Navigation";

    public virtual string LegendName => "Legend";

    public virtual string TrendChartName => "Trend chart";

    public virtual string BarChartName => "Bar chart";

    public virtual string SparklineName => "Sparkline";

    public virtual string TrendName => "Trend";

    public virtual string SeriesCount(int count) => $"{count} series";

    public virtual string CategoryCount(int count) => $"{count} categories";

    public virtual string TrackballAt(int index) => $"Trackball at point {index}";

    public virtual string DeviatingFromSetpoint => "Deviating from setpoint";

    public virtual string ReadingInvalid => "Reading invalid";

    public virtual string NoData => "No data";

    public virtual string HeartbeatName => "Communication heartbeat";

    public virtual string RangeHelp(double minimum, double maximum) =>
        $"Range {minimum} – {maximum}";

    public virtual string StopCommandNotSent => "Stop command not sent";

    public virtual string EngageCommandNotSent => "E-stop command not sent";

    public virtual string DeviceStatusName => "Device status";

    public virtual string NumericKeypadName => "Numeric keypad";

    public virtual string CanCommit => "Can commit";

    public virtual string CannotCommit => "Cannot commit";

    public virtual string Jogging => "Jogging";

    public virtual string Idle => "Idle";

    public virtual string Engaged => "Engaged";

    public virtual string Ready => "Ready";
}
