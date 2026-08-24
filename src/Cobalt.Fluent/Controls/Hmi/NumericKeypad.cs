using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace Cobalt.Fluent.Controls;

/// <summary>
/// 能被 <see cref="NumericKeypad"/> 挂接的数值输入宿主。
///
/// <see cref="ParameterRow"/> 实现了它。使用方自己的控件实现这个接口就能复用同一个键盘，
/// 不必依赖本库的具体控件类型——这是「可选挂接」的全部含义。
/// </summary>
public interface INumericInputTarget
{
    /// <summary>正在编辑什么。显示在键盘抬头。</summary>
    string? Label { get; }

    /// <summary>工程单位。跟在量程提示后面。</summary>
    string? Unit { get; }

    double Minimum { get; }

    double Maximum { get; }

    /// <summary>量程提示的数字格式。不用来格式化正在输入的缓冲。</summary>
    string Format { get; }

    /// <summary>待下发的文本。键盘确认时写回这里。</summary>
    string? PendingText { get; set; }

    /// <summary>
    /// 把 <see cref="PendingText"/> 真正下发出去。
    /// 返回 false 表示宿主此刻不受理（正在下发、只读、宿主自身判定不通过），
    /// 键盘据此回滚并且<b>不</b>抛出确认事件——「界面说已下发、设备没收到」是本组要防的事故。
    /// </summary>
    bool CommitPending();
}

/// <summary>
/// 数字键盘。触摸屏上位机的必需件——工业面板绝大多数没有物理键盘，
/// 没有它，<see cref="ParameterRow"/> 这类控件在真实设备上根本改不了值。
///
/// 三条硬约束，都不是外观问题：
///
/// 1. **输入过程中不做量程拦截。** 把 5 改成 50 必然要经过中间态 5 → 50，
///    逐键校验会让一大批合法目标值根本输不进去。所以键盘允许自由输入，
///    只在<b>提交</b>那一刻闸住——<see cref="CanCommit"/> 实时反映能不能提交，
///    确认键随之禁用，但按键本身永远不拒收。
///
/// 2. **超量程时拒绝，不静默限幅。** 上限 120 而操作员输了 150，
///    悄悄改成 120 提交是最危险的做法：他以为设备收到的是 150。
///    要么他自己改，要么不下发。
///
/// 3. **首次按数字替换整个缓冲，不是追加。** 打开键盘时缓冲里是当前值 85.0，
///    要改成 9 却得先按五次退格，是触摸屏上典型的误操作来源。
///    首键替换是计算器沿用几十年的约定，退格与符号键则在既有缓冲上继续编辑。
///
/// 独立使用时绑 <see cref="Text"/>、听 <see cref="Committed"/> 即可；
/// 给 <see cref="Target"/> 赋一个 <see cref="INumericInputTarget"/> 则量程、单位、
/// 格式与标签全部跟随宿主，确认时写回宿主并触发其下发。
/// </summary>
[PseudoClasses(":empty", ":invalid", ":outofrange")]
public class NumericKeypad : TemplatedControl
{
    /// <summary>缓冲是否还是「刚放进来的原值」。首次按数字要整体替换而不是追加。</summary>
    private bool _pristine = true;

    /// <summary>写回宿主的过程中屏蔽反向同步，避免 Text ↔ PendingText 来回弹。</summary>
    private bool _syncing;

    // ---- 输入缓冲 ------------------------------------------------------------

    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<NumericKeypad, string?>(
            nameof(Text), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    /// <summary>正在输入的文本。外部赋值会重置「首键替换」状态。</summary>
    public string? Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    // ---- 量程与格式 ----------------------------------------------------------

    public static readonly StyledProperty<double> MinimumProperty =
        AvaloniaProperty.Register<NumericKeypad, double>(nameof(Minimum), double.NegativeInfinity);

    public double Minimum
    {
        get => GetValue(MinimumProperty);
        set => SetValue(MinimumProperty, value);
    }

    public static readonly StyledProperty<double> MaximumProperty =
        AvaloniaProperty.Register<NumericKeypad, double>(nameof(Maximum), double.PositiveInfinity);

    public double Maximum
    {
        get => GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    public static readonly StyledProperty<string> FormatProperty =
        AvaloniaProperty.Register<NumericKeypad, string>(nameof(Format), "F1");

    /// <summary>量程提示的数字格式。<b>不</b>用来格式化正在输入的缓冲——
    /// 边输边格式化会把光标位置和小数点抢走。</summary>
    public string Format
    {
        get => GetValue(FormatProperty);
        set => SetValue(FormatProperty, value);
    }

    public static readonly StyledProperty<string?> UnitProperty =
        AvaloniaProperty.Register<NumericKeypad, string?>(nameof(Unit));

    public string? Unit
    {
        get => GetValue(UnitProperty);
        set => SetValue(UnitProperty, value);
    }

    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<NumericKeypad, string?>(nameof(Label));

    /// <summary>正在编辑什么。抬头必须写清楚——操作员面前经常同时开着几个参数。</summary>
    public string? Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    // ---- 输入规则 ------------------------------------------------------------

    public static readonly StyledProperty<bool> AllowNegativeProperty =
        AvaloniaProperty.Register<NumericKeypad, bool>(nameof(AllowNegative), true);

    /// <summary>允许负值。温度、偏差允许；转速、时长不允许。</summary>
    public bool AllowNegative
    {
        get => GetValue(AllowNegativeProperty);
        set => SetValue(AllowNegativeProperty, value);
    }

    public static readonly StyledProperty<bool> AllowDecimalProperty =
        AvaloniaProperty.Register<NumericKeypad, bool>(nameof(AllowDecimal), true);

    /// <summary>允许小数。整数参数（计数、序号）关掉，小数点键随之禁用。</summary>
    public bool AllowDecimal
    {
        get => GetValue(AllowDecimalProperty);
        set => SetValue(AllowDecimalProperty, value);
    }

    public static readonly StyledProperty<int> MaxLengthProperty =
        AvaloniaProperty.Register<NumericKeypad, int>(nameof(MaxLength), 12);

    /// <summary>缓冲最大字符数，含负号与小数点。防的是按住不放刷出一屏数字。</summary>
    public int MaxLength
    {
        get => GetValue(MaxLengthProperty);
        set => SetValue(MaxLengthProperty, value);
    }

    // ---- 可选挂接 ------------------------------------------------------------

    public static readonly StyledProperty<INumericInputTarget?> TargetProperty =
        AvaloniaProperty.Register<NumericKeypad, INumericInputTarget?>(nameof(Target));

    /// <summary>
    /// 挂接的输入宿主。赋值时量程、格式、单位、标签与当前文本一次性从宿主同步过来，
    /// 确认时写回宿主的 <see cref="INumericInputTarget.PendingText"/> 并调用
    /// <see cref="INumericInputTarget.CommitPending"/>。为 null 时键盘完全独立。
    /// </summary>
    public INumericInputTarget? Target
    {
        get => GetValue(TargetProperty);
        set => SetValue(TargetProperty, value);
    }

    // ---- 只读投影 ------------------------------------------------------------

    private double? _value;

    public static readonly DirectProperty<NumericKeypad, double?> ValueProperty =
        AvaloniaProperty.RegisterDirect<NumericKeypad, double?>(nameof(Value), o => o._value);

    /// <summary>缓冲解析出来的值。解析不出来时为 null。</summary>
    public double? Value
    {
        get => _value;
        private set => SetAndRaise(ValueProperty, ref _value, value);
    }

    private bool _canCommit;

    public static readonly DirectProperty<NumericKeypad, bool> CanCommitProperty =
        AvaloniaProperty.RegisterDirect<NumericKeypad, bool>(nameof(CanCommit), o => o._canCommit);

    /// <summary>能否提交。空缓冲、解析失败、超量程时为 false，确认键随之禁用。</summary>
    public bool CanCommit
    {
        get => _canCommit;
        private set => SetAndRaise(CanCommitProperty, ref _canCommit, value);
    }

    private string? _rangeText;

    public static readonly DirectProperty<NumericKeypad, string?> RangeTextProperty =
        AvaloniaProperty.RegisterDirect<NumericKeypad, string?>(nameof(RangeText), o => o._rangeText);

    /// <summary>量程提示，如「20.0 – 120.0 °C」。两端都无界时为 null。
    /// 输入之前就把边界摆出来，比输完再报错省一次来回。</summary>
    public string? RangeText
    {
        get => _rangeText;
        private set => SetAndRaise(RangeTextProperty, ref _rangeText, value);
    }

    private string _decimalSeparatorText = CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator;

    public static readonly DirectProperty<NumericKeypad, string> DecimalSeparatorTextProperty =
        AvaloniaProperty.RegisterDirect<NumericKeypad, string>(
            nameof(DecimalSeparatorText), o => o._decimalSeparatorText);

    /// <summary>小数点键的键面文字。按下去插入的是当前文化的小数点分隔符，
    /// 键面必须跟着走——触摸屏上键面是操作员唯一的可见线索。</summary>
    public string DecimalSeparatorText
    {
        get => _decimalSeparatorText;
        private set => SetAndRaise(DecimalSeparatorTextProperty, ref _decimalSeparatorText, value);
    }

    private string? _validationText;

    public static readonly DirectProperty<NumericKeypad, string?> ValidationTextProperty =
        AvaloniaProperty.RegisterDirect<NumericKeypad, string?>(
            nameof(ValidationText), o => o._validationText);

    /// <summary>为什么不能提交。可以提交时为 null。</summary>
    public string? ValidationText
    {
        get => _validationText;
        private set => SetAndRaise(ValidationTextProperty, ref _validationText, value);
    }

    // ---- 事件 ----------------------------------------------------------------

    public static readonly RoutedEvent<NumericKeypadCommittedEventArgs> CommittedEvent =
        RoutedEvent.Register<NumericKeypad, NumericKeypadCommittedEventArgs>(
            nameof(Committed), RoutingStrategies.Bubble);

    /// <summary>确认。只有 <see cref="CanCommit"/> 为 true 时才会触发。</summary>
    public event EventHandler<NumericKeypadCommittedEventArgs>? Committed
    {
        add => AddHandler(CommittedEvent, value);
        remove => RemoveHandler(CommittedEvent, value);
    }

    public static readonly RoutedEvent<RoutedEventArgs> CancelledEvent =
        RoutedEvent.Register<NumericKeypad, RoutedEventArgs>(
            nameof(Cancelled), RoutingStrategies.Bubble);

    /// <summary>取消。缓冲不变，由使用方决定关闭还是复位。</summary>
    public event EventHandler<RoutedEventArgs>? Cancelled
    {
        add => AddHandler(CancelledEvent, value);
        remove => RemoveHandler(CancelledEvent, value);
    }

    static NumericKeypad()
    {
        TextProperty.Changed.AddClassHandler<NumericKeypad>((x, e) => x.OnTextChanged(e));
        MinimumProperty.Changed.AddClassHandler<NumericKeypad>((x, _) => x.Evaluate());
        MaximumProperty.Changed.AddClassHandler<NumericKeypad>((x, _) => x.Evaluate());
        FormatProperty.Changed.AddClassHandler<NumericKeypad>((x, _) => x.Evaluate());
        UnitProperty.Changed.AddClassHandler<NumericKeypad>((x, _) => x.Evaluate());
        AllowDecimalProperty.Changed.AddClassHandler<NumericKeypad>((x, _) => x.Evaluate());
        AllowNegativeProperty.Changed.AddClassHandler<NumericKeypad>((x, _) => x.Evaluate());
        TargetProperty.Changed.AddClassHandler<NumericKeypad>((x, e) => x.OnTargetChanged(e));
    }

    // Focusable 由 ControlTheme 的 Setter 给（本库惯例），写在构造函数里是 local value，
    // 使用方就没法再用样式关掉了。
    public NumericKeypad() => Evaluate();

    /// <summary>
    /// 装入一个待编辑的值，并回到「首键替换」状态。<b>外部重设缓冲一律走这里。</b>
    ///
    /// 不能只写 <see cref="Text"/>：Avalonia 对相等的新值不发变更通知，
    /// 新值恰好等于当前缓冲时 <c>OnTextChanged</c> 根本不触发，_pristine 会停在上一次编辑的 false。
    /// 一块键盘轮流服务多个参数时，这会把上一个参数的残留缓冲带进下一个参数的设定值。
    /// </summary>
    public void LoadValue(string? text)
    {
        _syncing = true;
        try { Text = text; }
        finally { _syncing = false; }

        _pristine = true;
        Evaluate();
    }

    /// <summary>
    /// 把模板里的按键接到方法上。数字键走循环，其余逐个具名。
    /// 重新应用模板时先摘旧的——ControlTheme 换主题会走第二次。
    /// </summary>
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        foreach (var (button, handler) in _wired)
            button.Click -= handler;
        _wired.Clear();

        for (var digit = 0; digit <= 9; digit++)
        {
            var token = digit.ToString(CultureInfo.InvariantCulture);
            Wire(e, $"PART_Digit{digit}", () => Append(token));
        }

        Wire(e, "PART_Decimal", () => Append(DecimalSeparator));
        Wire(e, "PART_Sign", ToggleSign);
        Wire(e, "PART_Backspace", Backspace);
        Wire(e, "PART_Clear", Clear);
        Wire(e, "PART_Commit", Commit);
        Wire(e, "PART_Cancel", Cancel);
    }

    private readonly List<(Button Button, EventHandler<RoutedEventArgs> Handler)> _wired = [];

    private void Wire(TemplateAppliedEventArgs e, string name, Action action)
    {
        if (e.NameScope.Find<Button>(name) is not { } button) return;

        void Handler(object? _, RoutedEventArgs __) => action();
        button.Click += Handler;
        _wired.Add((button, Handler));
    }

    private void OnTextChanged(AvaloniaPropertyChangedEventArgs e)
    {
        // 外部换值 = 换了个参数在编辑，回到「首键替换」。自己按键改的不算，
        // 那条路径在 SetBuffer 里已经把 _pristine 落下了。
        if (!_syncing) _pristine = true;
        Evaluate();
    }

    private void OnTargetChanged(AvaloniaPropertyChangedEventArgs e)
    {
        if (e.NewValue is not INumericInputTarget target)
        {
            // 解绑要真的回到独立状态，否则会拿上一个参数的量程去卡新输入、
            // 抬头还挂着旧参数的名字。只在确实挂过宿主时清——从没挂过的话
            // 这些值是 XAML 里作者自己写的，不能动。
            if (e.OldValue is INumericInputTarget)
            {
                ClearValue(MinimumProperty);
                ClearValue(MaximumProperty);
                ClearValue(FormatProperty);
                ClearValue(UnitProperty);
                ClearValue(LabelProperty);
            }
            Evaluate();
            return;
        }

        // 宿主的量程和格式是权威的，直接覆盖键盘上的设置——挂接的意义就在这里
        Minimum = target.Minimum;
        Maximum = target.Maximum;
        Format = target.Format;
        Unit = target.Unit;
        Label = target.Label;
        LoadValue(target.PendingText);
    }

    // ---- 按键 ----------------------------------------------------------------

    /// <summary>
    /// 追加一个字符。只认 0–9 与小数点分隔符，其余静默忽略。
    /// <b>不做量程校验</b>——中间态必须能输进来。
    /// </summary>
    public void Append(string token)
    {
        if (string.IsNullOrEmpty(token)) return;

        var separator = DecimalSeparator;

        if (token == separator || token == ".")
        {
            if (!AllowDecimal) return;

            // 首键按小数点：从 0. 起头，别留下一个光秃秃的「.」
            var current = _pristine ? "" : Text ?? "";
            if (current.Contains(separator, StringComparison.Ordinal)) return;
            var bare = current.Length == 0 || current == NegativeSign;
            SetBuffer(bare ? current + "0" + separator : current + separator);
            return;
        }

        if (token.Length != 1 || !char.IsAsciiDigit(token[0])) return;

        var buffer = _pristine ? "" : Text ?? "";

        // 前导零：0 后面再按数字应当替换掉那个 0，但 0. 后面不能吃掉小数点
        if (buffer == "0") buffer = "";
        else if (buffer == NegativeSign + "0") buffer = NegativeSign;

        SetBuffer(buffer + token);
    }

    /// <summary>退格一位。在既有缓冲上编辑，不触发首键替换。</summary>
    public void Backspace()
    {
        var buffer = Text ?? "";
        SetBuffer(buffer.Length > 0 ? buffer[..^1] : "");
    }

    /// <summary>清空。和退格分开——戴手套连按退格是常见误操作，清空要独立一键。</summary>
    public void Clear() => SetBuffer("");

    /// <summary>
    /// 正负号切换。是切换不是追加，按两次回到原样。负号取自当前文化
    /// （瑞典语等用的是 U+2212 而不是 ASCII 连字符，写死 '-' 会拼出双负号）。
    ///
    /// <see cref="AllowNegative"/> 为 false 时只禁止<b>加</b>负号；把已有的负值改成正值
    /// 永远允许——那是往合法方向走，挡住只会逼操作员退格重输。
    /// </summary>
    public void ToggleSign()
    {
        var buffer = Text ?? "";

        if (buffer.StartsWith(NegativeSign, StringComparison.Ordinal))
        {
            SetBuffer(buffer[NegativeSign.Length..]);
            return;
        }

        if (!AllowNegative) return;
        SetBuffer(NegativeSign + buffer);
    }

    /// <summary>
    /// 提交。<see cref="CanCommit"/> 为 false 时是空操作——
    /// <b>不会</b>把超量程的值限幅到边界后提交。
    /// </summary>
    public void Commit()
    {
        if (!CanCommit || Value is not { } value) return;

        if (Target is { } target)
        {
            // 先记下宿主原来的文本：宿主拒收时要原样放回去，
            // 不能把一个没下发成功的值留在它的输入框里冒充待下发。
            var restore = target.PendingText;
            target.PendingText = Text;

            if (!target.CommitPending())
            {
                target.PendingText = restore;
                return;      // 宿主没受理就不是「已确认」，事件不能抛
            }
        }

        // 提交完缓冲里剩的就是「当前值」，语义和刚打开键盘时一样，
        // 因此回到首键替换：键盘不会自动收起，接着输下一个值不该拼在后面。
        _pristine = true;

        RaiseEvent(new NumericKeypadCommittedEventArgs(CommittedEvent, value));
    }

    /// <summary>取消。不动缓冲，由使用方决定收起还是复位。</summary>
    public void Cancel() => RaiseEvent(new RoutedEventArgs(CancelledEvent));

    /// <summary>
    /// 唯一的缓冲写入口。长度闸放在这里而不是各个按键分支里——
    /// 放在数字分支时，小数点和符号键会各自绕过去，缓冲能稳定超出 MaxLength 两位。
    /// 退格与清空只会让缓冲变短，天然不受影响。
    /// </summary>
    private void SetBuffer(string buffer)
    {
        if (MaxLength > 0 && buffer.Length > MaxLength && buffer.Length > (Text?.Length ?? 0)) return;

        _syncing = true;
        try
        {
            Text = buffer;
        }
        finally
        {
            _syncing = false;
        }
        _pristine = false;
        Evaluate();
    }

    // ---- 物理键盘 ------------------------------------------------------------

    /// <summary>
    /// 不少工业面板带硬件小键盘，另外无障碍也要求纯键盘可达。
    /// 映射与面板上的键一一对应，不额外发明快捷键。
    /// </summary>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled) return;

        // 带 Ctrl / Alt / Win 的组合键一律放行给应用。不加这道闸的话
        // Ctrl+Enter 会直接走到 Commit()——一个修饰键组合触发对设备的下发，
        // 同时应用级快捷键还会被这个控件无声吞掉。
        if ((e.KeyModifiers & (KeyModifiers.Control | KeyModifiers.Alt | KeyModifiers.Meta)) != 0)
            return;

        switch (e.Key)
        {
            case >= Key.D0 and <= Key.D9:
                Append(((char)('0' + (e.Key - Key.D0))).ToString());
                break;
            case >= Key.NumPad0 and <= Key.NumPad9:
                Append(((char)('0' + (e.Key - Key.NumPad0))).ToString());
                break;
            // 小键盘的 Decimal 键无条件认。主键盘上的句点与逗号只认其中
            // 真正是本文化小数点的那一个——另一个是千位分隔符键，
            // 把它也映射过去，操作员按习惯敲的 "1,234" 会被静默变成 1.234，
            // 而且这个错值往往落在量程内，可以直接下发。
            case Key.Decimal:
                Append(DecimalSeparator);
                break;
            case Key.OemPeriod when DecimalSeparator == ".":
            case Key.OemComma when DecimalSeparator == ",":
                Append(DecimalSeparator);
                break;
            case Key.Back:
                Backspace();
                break;
            case Key.Delete:
                Clear();
                break;
            case Key.OemMinus or Key.Subtract:
                ToggleSign();
                break;
            case Key.Enter:
                Commit();
                break;
            case Key.Escape:
                Cancel();
                break;
            default:
                return;
        }

        e.Handled = true;
    }

    // ---- 判定 ----------------------------------------------------------------

    private static string DecimalSeparator =>
        CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator;

    /// <summary>当前文化的负号。瑞典语等用 U+2212，不是 ASCII 连字符。</summary>
    private static string NegativeSign =>
        CultureInfo.CurrentCulture.NumberFormat.NegativeSign;

    private void Evaluate()
    {
        DecimalSeparatorText = DecimalSeparator;
        RangeText = BuildRangeText();

        var buffer = Text ?? "";
        var empty = buffer.Length == 0;

        // IsFinite 这一道不能省：NumberStyles.Float 接受 "NaN" / "Infinity" / "1e400"，
        // 而 NaN 与任何数比较恒为 false，会从下面的量程判定底下整个穿过去，
        // 带着 NaN 抛给应用侧。非有限值一律归到「解析不出来」。
        Value = double.TryParse(buffer, NumberStyles.Float, CultureInfo.CurrentCulture, out var v)
                && double.IsFinite(v)
            ? v
            : null;

        string? problem;
        var outOfRange = false;

        if (empty)
            // 空缓冲不报错——还没开始输就提示是噪音。不能提交，但不占那行提示位。
            problem = null;
        else if (Value is null)
            problem = "无法解析";          // 只剩「-」或「0.」这类中间态
        // 写成 !(x >= Minimum) 而不是 x < Minimum：边界本身是 NaN 时前者为 true（拒），
        // 后者为 false（放行）。量程判定要失败即拒。
        else if (!(Value >= Minimum))
        {
            problem = $"低于下限 {Bound(Minimum)}";
            outOfRange = true;
        }
        else if (!(Value <= Maximum))
        {
            problem = $"高于上限 {Bound(Maximum)}";
            outOfRange = true;
        }
        else
            problem = null;

        ValidationText = problem;
        CanCommit = !empty && problem is null;

        PseudoClasses.Set(":empty", empty);
        PseudoClasses.Set(":outofrange", outOfRange);
        // :invalid 覆盖一切不能提交的情形，包括超量程——
        // 主题只想「有问题就变色」时不必把三个伪类都列一遍
        PseudoClasses.Set(":invalid", !empty && problem is not null);
    }

    private string Bound(double value) =>
        value.ToString(Format, CultureInfo.CurrentCulture);

    private string? BuildRangeText()
    {
        var lo = double.IsNegativeInfinity(Minimum);
        var hi = double.IsPositiveInfinity(Maximum);
        if (lo && hi) return null;

        var text = $"{(lo ? "…" : Bound(Minimum))} – {(hi ? "…" : Bound(Maximum))}";
        return string.IsNullOrEmpty(Unit) ? text : $"{text} {Unit}";
    }
}

/// <summary>确认事件。<see cref="Value"/> 是解析并通过量程校验后的值。</summary>
public class NumericKeypadCommittedEventArgs(RoutedEvent routedEvent, double value)
    : RoutedEventArgs(routedEvent)
{
    public double Value { get; } = value;
}
