using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Threading;

namespace Cobalt.Fluent.Controls;

/// <summary>读数字号档。桌面基准分别是 24 / 40 / 72。</summary>
public enum ReadoutSize
{
    Small,
    Medium,
    Large,
}

/// <summary>
/// 数值读数。过程界面上出现频率最高的控件。
///
/// 第 7 组的两条硬约束：
///
/// 1. **刷新时布局绝对不能跳动。** 等宽数字（tnum）+ 按最大位数预留 <see cref="ValueMinChars"/>。
///    比例数字下 84.6 → 84.9 会让整行横移，一屏二十个读数就是一片抖动。
/// 2. **<c>:stale</c> 时保留最后已知值**，只变灰 + 标注多久没更新。
///    换成"—"是错的：通信断了，但设备上的反应还在跑，操作员需要知道断开前的最后一个值。
///
/// <c>:stale</c> 由内部定时器驱动，不等下一次数据到达才判断 ——
/// 数据不来正是要报的那种情况，等它等不到。
/// </summary>
[PseudoClasses(":deviating", ":stale", ":unknownage", ":invalid", ":nodata", ":small", ":medium", ":large")]
public class Readout : TemplatedControl
{
    private DispatcherTimer? _staleTimer;

    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<Readout, string?>(nameof(Label));

    public string? Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    /// <summary>当前值。null 表示从未取到过值（<c>:nodata</c>）。</summary>
    public static readonly StyledProperty<double?> ValueProperty =
        AvaloniaProperty.Register<Readout, double?>(nameof(Value));

    public double? Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public static readonly StyledProperty<string?> UnitProperty =
        AvaloniaProperty.Register<Readout, string?>(nameof(Unit));

    public string? Unit
    {
        get => GetValue(UnitProperty);
        set => SetValue(UnitProperty, value);
    }

    public static readonly StyledProperty<string> FormatProperty =
        AvaloniaProperty.Register<Readout, string>(nameof(Format), "F1");

    public string Format
    {
        get => GetValue(FormatProperty);
        set => SetValue(FormatProperty, value);
    }

    /// <summary>目标值。给了才会算偏差、才可能进 <c>:deviating</c>。</summary>
    public static readonly StyledProperty<double?> SetpointProperty =
        AvaloniaProperty.Register<Readout, double?>(nameof(Setpoint));

    public double? Setpoint
    {
        get => GetValue(SetpointProperty);
        set => SetValue(SetpointProperty, value);
    }

    /// <summary>容差带。|Value - Setpoint| 超过它就进 <c>:deviating</c>。</summary>
    public static readonly StyledProperty<double> ToleranceProperty =
        AvaloniaProperty.Register<Readout, double>(nameof(Tolerance), 1.0d);

    public double Tolerance
    {
        get => GetValue(ToleranceProperty);
        set => SetValue(ToleranceProperty, value);
    }

    public static readonly StyledProperty<ReadoutSize> SizeProperty =
        AvaloniaProperty.Register<Readout, ReadoutSize>(nameof(Size), ReadoutSize.Medium);

    public ReadoutSize Size
    {
        get => GetValue(SizeProperty);
        set => SetValue(SizeProperty, value);
    }

    /// <summary>最后一次收到数据的时刻。每次刷新值都要一起更新它，否则会误判成过期。</summary>
    public static readonly StyledProperty<DateTime?> LastUpdatedProperty =
        AvaloniaProperty.Register<Readout, DateTime?>(nameof(LastUpdated));

    public DateTime? LastUpdated
    {
        get => GetValue(LastUpdatedProperty);
        set => SetValue(LastUpdatedProperty, value);
    }

    /// <summary>超过这么久没有新数据就算过期。设成 <see cref="TimeSpan.Zero"/> 关掉过期判断。</summary>
    public static readonly StyledProperty<TimeSpan> StaleAfterProperty =
        AvaloniaProperty.Register<Readout, TimeSpan>(nameof(StaleAfter), TimeSpan.FromSeconds(3));

    public TimeSpan StaleAfter
    {
        get => GetValue(StaleAfterProperty);
        set => SetValue(StaleAfterProperty, value);
    }

    /// <summary>
    /// 数值区按几位字符预留宽度。默认 4（形如 "85.5" / "-3.2" / "100."）。
    /// 量程会到四位数的话调大，别让它跟着内容缩放——那正是抖动的来源。
    /// </summary>
    public static readonly StyledProperty<int> ValueMinCharsProperty =
        AvaloniaProperty.Register<Readout, int>(nameof(ValueMinChars), 4);

    public int ValueMinChars
    {
        get => GetValue(ValueMinCharsProperty);
        set => SetValue(ValueMinCharsProperty, value);
    }

    // --- 下面几个是给模板绑的只读投影 ---------------------------------------

    private string _displayValue = "—";

    public static readonly DirectProperty<Readout, string> DisplayValueProperty =
        AvaloniaProperty.RegisterDirect<Readout, string>(
            nameof(DisplayValue), o => o._displayValue);

    /// <summary>格式化后的数值文本。<c>:nodata</c> 时是长破折号。</summary>
    public string DisplayValue
    {
        get => _displayValue;
        private set => SetAndRaise(DisplayValueProperty, ref _displayValue, value);
    }

    private string? _statusText;

    public static readonly DirectProperty<Readout, string?> StatusTextProperty =
        AvaloniaProperty.RegisterDirect<Readout, string?>(
            nameof(StatusText), o => o._statusText);

    /// <summary>值下面那行小字：正常时是"目标 x · 偏差 ±y"，过期时是"最后更新 n 秒前"。</summary>
    public string? StatusText
    {
        get => _statusText;
        private set => SetAndRaise(StatusTextProperty, ref _statusText, value);
    }

    private double _valueMinWidth = 4 * 40 * 0.62;

    public static readonly DirectProperty<Readout, double> ValueMinWidthProperty =
        AvaloniaProperty.RegisterDirect<Readout, double>(
            nameof(ValueMinWidth), o => o._valueMinWidth);

    /// <summary>按 <see cref="ValueMinChars"/> 和字号折算出的预留宽度，模板绑到数值区的 MinWidth。</summary>
    public double ValueMinWidth
    {
        get => _valueMinWidth;
        private set => SetAndRaise(ValueMinWidthProperty, ref _valueMinWidth, value);
    }

    private string? _staleText;

    public static readonly DirectProperty<Readout, string?> StaleTextProperty =
        AvaloniaProperty.RegisterDirect<Readout, string?>(nameof(StaleText), o => o._staleText);

    /// <summary>过期标记，跟在标签后面。不过期时为 null。</summary>
    public string? StaleText
    {
        get => _staleText;
        private set => SetAndRaise(StaleTextProperty, ref _staleText, value);
    }

    private bool _isStale;

    public static readonly DirectProperty<Readout, bool> IsStaleProperty =
        AvaloniaProperty.RegisterDirect<Readout, bool>(nameof(IsStale), o => o._isStale);

    /// <summary>数据是否已过期。只读，由 <see cref="LastUpdated"/> 和 <see cref="StaleAfter"/> 推出来。</summary>
    public bool IsStale
    {
        get => _isStale;
        private set => SetAndRaise(IsStaleProperty, ref _isStale, value);
    }

    static Readout()
    {
        ValueProperty.Changed.AddClassHandler<Readout>((x, _) => x.Refresh());
        SetpointProperty.Changed.AddClassHandler<Readout>((x, _) => x.Refresh());
        ToleranceProperty.Changed.AddClassHandler<Readout>((x, _) => x.Refresh());
        FormatProperty.Changed.AddClassHandler<Readout>((x, _) => x.Refresh());
        LastUpdatedProperty.Changed.AddClassHandler<Readout>((x, _) => x.Refresh());
        StaleAfterProperty.Changed.AddClassHandler<Readout>((x, _) => x.Refresh());
        SizeProperty.Changed.AddClassHandler<Readout>((x, _) => x.OnSizeChanged());
        ValueMinCharsProperty.Changed.AddClassHandler<Readout>((x, _) => x.UpdateMinWidth());
    }

    public Readout()
    {
        OnSizeChanged();
        Refresh();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        // 过期判断必须由定时器驱动。等下一次数据到达才判断的话，
        // 通信断了就永远不会触发——而那正是最需要报出来的情况。
        _staleTimer = new DispatcherTimer(
            TimeSpan.FromMilliseconds(500), DispatcherPriority.Background, (_, _) => Refresh());
        _staleTimer.Start();
        UpdateMinWidth();   // 构造函数里读不到资源，那时还没有资源作用域
        Refresh();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _staleTimer?.Stop();
        _staleTimer = null;
        base.OnDetachedFromVisualTree(e);
    }

    private void OnSizeChanged()
    {
        PseudoClasses.Set(":small", Size == ReadoutSize.Small);
        PseudoClasses.Set(":medium", Size == ReadoutSize.Medium);
        PseudoClasses.Set(":large", Size == ReadoutSize.Large);
        UpdateMinWidth();
    }

    private void UpdateMinWidth()
    {
        // 字号只能有一个来源。这里此前抄了一份 24/40/72 的字面值，
        // 覆盖 ReadoutFontSize* token 之后两份数字就脱钩了——
        // MinWidth 不再等于「最大位数所需宽度」，防抖动的预留失去意义。
        var key = Size switch
        {
            ReadoutSize.Small => "ReadoutFontSizeSmall",
            ReadoutSize.Large => "ReadoutFontSizeLarge",
            _ => "ReadoutFontSizeMedium",
        };

        var fallback = Size switch
        {
            ReadoutSize.Small => 24d,
            ReadoutSize.Large => 72d,
            _ => 40d,
        };

        var fontSize = this.TryFindResource(key, ActualThemeVariant, out var v) && v is double d
            ? d
            : fallback;

        // 等宽数字大约是 0.62em 宽。宁可略宽也不能让数值区随内容缩放。
        ValueMinWidth = Math.Max(0, ValueMinChars) * fontSize * 0.62;
    }

    private void Refresh()
    {
        // 顺序是有讲究的：安全相关的判定（过期、偏差、伪类）全部先算完，
        // 格式化留到最后并单独兜异常。此前格式化写在第一行，非法的 Format
        // （如 "Q7"）让 ToString 抛 FormatException，异常被 dispatcher 吞掉不报，
        // 而坏的 Format 已经写进了控件——此后每一次 Refresh（包括 500ms 过期
        // 定时器那一次）都停在同一行，过期判定、偏差判定、状态行全部停摆。
        var value = Value;
        var finite = value is { } v0 && double.IsFinite(v0);

        PseudoClasses.Set(":nodata", value is null);
        // 「拿到了坏值」和「从未拿到值」是两回事，不能复用 :nodata
        PseudoClasses.Set(":invalid", value is { } bad && !double.IsFinite(bad));

        // ---- 新鲜度 --------------------------------------------------------
        var stale = false;
        var unknownAge = false;

        if (StaleAfter > TimeSpan.Zero && value is not null)
        {
            if (LastUpdated is { } last)
            {
                var elapsed = DateTime.Now - last;

                // 时间戳落在未来：墙钟被回拨（无 RTC 的嵌入式 HMI 开机后 NTP 校时、
                // 夏令时切换、手工改表），或者时间戳来自比 HMI 快的时钟源（PLC / 采集卡）。
                // 差值恒为负，条件永远不成立——回拨多久，就有多久所有读数被判成新鲜。
                // 留 1 秒容差吸收正常抖动。
                if (elapsed < ClockSkewTolerance) unknownAge = true;
                else stale = elapsed > StaleAfter;
            }
            else
            {
                // 缺 LastUpdated 不等于数据新鲜。这个属性是纯手工簿记的，
                // 而 MVVM 里最自然的写法就是只绑 Value——静默落到「新鲜」意味着
                // 通信断开后数值以主色停在最后一帧，和正常刷新的读数
                // 在视觉上一个像素的差别都没有。
                unknownAge = true;
            }
        }

        IsStale = stale;
        PseudoClasses.Set(":stale", stale);
        PseudoClasses.Set(":unknownage", unknownAge);

        // ---- 偏差 ----------------------------------------------------------
        // Tolerance 非有限或为负都是配置错误：Math.Abs(...) > -1 恒真，
        // 所有值都会进 :deviating；NaN 则相反，恒不成立，偏差监视被静默关掉。
        var toleranceUsable = double.IsFinite(Tolerance) && Tolerance >= 0;
        var setpoint = Setpoint is { } sp && double.IsFinite(sp) ? sp : (double?)null;

        double? delta = null;
        if (finite && setpoint is { } target) delta = value!.Value - target;

        var deviating = toleranceUsable && delta is { } d && Math.Abs(d) > Tolerance;
        PseudoClasses.Set(":deviating", deviating && !stale);

        StaleText = stale ? "数据过期" : unknownAge ? "新鲜度未知" : null;

        // ---- 文字。格式化集中在这里，整段兜异常 ------------------------------
        try
        {
            DisplayValue = value is null ? "—" : finite ? Fmt(value.Value) : "无效";
            StatusText = BuildStatusText(stale, setpoint, delta, toleranceUsable);
        }
        catch (FormatException)
        {
            // Format 非法。安全判定在上面已经全部算完，这里只是显示降级——
            // 绝不能让一个显示格式把过期与偏差判定一起带停。
            DisplayValue = value is null ? "—"
                : finite ? value.Value.ToString(CultureInfo.CurrentCulture)
                : "无效";
            StatusText = "显示格式无效";
        }
    }

    /// <summary>时钟抖动容差。时间戳超前超过这个量就当成时钟异常，而不是「刚刚更新过」。</summary>
    private static readonly TimeSpan ClockSkewTolerance = TimeSpan.FromSeconds(-1);

    /// <summary>按 <see cref="Format"/> 格式化。名字不能叫 Format——和属性重名。</summary>
    private string Fmt(double v) => v.ToString(Format, CultureInfo.CurrentCulture);

    private string? BuildStatusText(bool stale, double? setpoint, double? delta, bool toleranceUsable)
    {
        if (stale && LastUpdated is { } lastSeen)
        {
            // 值本身保留不动，只把「多久没更新」标出来。
            // 偏差信息也要一并留着：过期时 :deviating 被清掉（颜色没了），
            // 状态行又被整条换掉（文字也没了），断开前是否超差这一条判读上下文
            // 会在通信断开的瞬间从界面上彻底消失，只剩一个孤零零的数字。
            var ago = DateTime.Now - lastSeen;
            var text = $"最后更新 {FormatAgo(ago)}前";

            if (toleranceUsable && delta is { } d && Math.Abs(d) > Tolerance)
                text += $" · 断开时偏差 {(d >= 0 ? "+" : "")}{Fmt(d)}";

            return text;
        }

        if (setpoint is not { } target) return null;

        if (!toleranceUsable)
            return $"目标 {Fmt(target)} · 偏差监视不可用（容差 {Tolerance}）";

        if (delta is not { } diff) return $"目标 {Fmt(target)}";

        return $"目标 {Fmt(target)} · 偏差 {(diff >= 0 ? "+" : "")}{Fmt(diff)}";
    }

    private static string FormatAgo(TimeSpan ago) => ago.TotalSeconds switch
    {
        < 60 => $"{(int)ago.TotalSeconds} 秒",
        < 3600 => $"{(int)ago.TotalMinutes} 分",
        _ => $"{(int)ago.TotalHours} 小时",
    };
}
