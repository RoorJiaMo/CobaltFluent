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
[PseudoClasses(":deviating", ":stale", ":nodata", ":small", ":medium", ":large")]
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
        FontSizeProperty.Changed.AddClassHandler<Readout>((x, _) => x.UpdateMinWidth());
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
        // 等宽数字大约是 0.62em 宽。宁可略宽也不能让数值区随内容缩放。
        var fontSize = Size switch
        {
            ReadoutSize.Small => 24d,
            ReadoutSize.Large => 72d,
            _ => 40d,
        };
        ValueMinWidth = Math.Max(0, ValueMinChars) * fontSize * 0.62;
    }

    private void Refresh()
    {
        var value = Value;

        PseudoClasses.Set(":nodata", value is null);

        DisplayValue = value is { } v
            ? v.ToString(Format, System.Globalization.CultureInfo.CurrentCulture)
            : "—";

        var stale = false;
        if (StaleAfter > TimeSpan.Zero && LastUpdated is { } last)
            stale = DateTime.Now - last > StaleAfter;

        IsStale = stale;
        PseudoClasses.Set(":stale", stale);

        var deviating = false;
        if (value is { } val && Setpoint is { } sp)
            deviating = Math.Abs(val - sp) > Tolerance;

        PseudoClasses.Set(":deviating", deviating && !stale);

        StaleText = stale ? "数据过期" : null;

        if (stale && LastUpdated is { } lastSeen)
        {
            var ago = DateTime.Now - lastSeen;
            // 值本身保留不动，只把"多久没更新"标出来
            StatusText = $"最后更新 {FormatAgo(ago)}前";
        }
        else if (value is { } current && Setpoint is { } target)
        {
            var delta = current - target;
            var sign = delta >= 0 ? "+" : "";
            StatusText = $"目标 {target.ToString(Format, System.Globalization.CultureInfo.CurrentCulture)}"
                       + $" · 偏差 {sign}{delta.ToString(Format, System.Globalization.CultureInfo.CurrentCulture)}";
        }
        else
        {
            StatusText = null;
        }
    }

    private static string FormatAgo(TimeSpan ago) => ago.TotalSeconds switch
    {
        < 60 => $"{(int)ago.TotalSeconds} 秒",
        < 3600 => $"{(int)ago.TotalMinutes} 分",
        _ => $"{(int)ago.TotalHours} 小时",
    };
}
