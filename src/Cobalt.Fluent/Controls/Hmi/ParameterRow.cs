using System.Globalization;
using System.Windows.Input;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Cobalt.Fluent.Automation;

namespace Cobalt.Fluent.Controls;

/// <summary>参数下发状态机。</summary>
public enum ParameterWriteState
{
    /// <summary>输入值和已生效值一致。</summary>
    Clean,

    /// <summary>已修改未下发。**这个态必须一眼看见**，否则操作员会以为已生效。</summary>
    Dirty,

    /// <summary>下发中，等设备回读。</summary>
    Writing,

    /// <summary>下发失败，值已回滚到上次成功值。</summary>
    Failed,

    /// <summary>输入超量程，不允许下发。</summary>
    OutOfRange,
}

/// <summary>
/// 参数行。过程控制的主力控件。
///
/// **核心是把「我改了」和「设备收到了」区分开** —— 这两者之间的空档是事故高发区：
/// 操作员改完数字就走，以为已生效，实际上还躺在输入框里。
///
/// 状态机：<c>Clean → Dirty →（下发）→ Writing →（回读）→ Clean</c>
/// 或 <c>Writing →（失败）→ Failed</c>，超量程时进 <c>OutOfRange</c> 且禁止下发。
///
/// 两个容易写错的地方：
/// 1. **下发成功后填回读值，不是输入值。** 设备可能做了限幅或量化——
///    你写 85.3，它按 0.5 步进量化成 85.5。显示输入值就是在骗人。
///    所以 <see cref="CompleteWrite"/> 收的是设备回读回来的值。
/// 2. **失败要回滚到上次成功值**，不能把失败的输入留在框里。
///
/// 界面上 <c>:dirty</c> 要三重提示（整行淡黄底 + 输入框底边变色 + 行尾徽章）——
/// 一屏二十行参数时，只改 2px 边框根本看不见。
///
/// 整张表的列宽用 <c>Grid.IsSharedSizeScope</c> + SharedSizeGroup 对齐，别每行各算各的。
/// </summary>
[PseudoClasses(":dirty", ":writing", ":failed", ":outofrange", ":readonly")]
public class ParameterRow : TemplatedControl, INumericInputTarget
{
    /// <summary>上次成功下发并回读到的值。失败时回滚到它。</summary>
    private double? _lastApplied;

    private Button? _applyButton;
    private Button? _revertButton;

    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<ParameterRow, string?>(nameof(Label));

    public string? Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public static readonly StyledProperty<string?> UnitProperty =
        AvaloniaProperty.Register<ParameterRow, string?>(nameof(Unit));

    public string? Unit
    {
        get => GetValue(UnitProperty);
        set => SetValue(UnitProperty, value);
    }

    /// <summary>设备当前实际值（读值）。和设定值分开显示。</summary>
    public static readonly StyledProperty<double?> ActualValueProperty =
        AvaloniaProperty.Register<ParameterRow, double?>(nameof(ActualValue));

    public double? ActualValue
    {
        get => GetValue(ActualValueProperty);
        set => SetValue(ActualValueProperty, value);
    }

    /// <summary>已生效的设定值。下发成功后由回读值更新。</summary>
    public static readonly StyledProperty<double> SetpointProperty =
        AvaloniaProperty.Register<ParameterRow, double>(
            nameof(Setpoint), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    public double Setpoint
    {
        get => GetValue(SetpointProperty);
        set => SetValue(SetpointProperty, value);
    }

    /// <summary>操作员正在输入的文本。非空且与已生效值不同即 <c>:dirty</c>。</summary>
    public static readonly StyledProperty<string?> PendingTextProperty =
        AvaloniaProperty.Register<ParameterRow, string?>(
            nameof(PendingText), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    public string? PendingText
    {
        get => GetValue(PendingTextProperty);
        set => SetValue(PendingTextProperty, value);
    }

    public static readonly StyledProperty<double> MinimumProperty =
        AvaloniaProperty.Register<ParameterRow, double>(nameof(Minimum), double.NegativeInfinity);

    public double Minimum
    {
        get => GetValue(MinimumProperty);
        set => SetValue(MinimumProperty, value);
    }

    public static readonly StyledProperty<double> MaximumProperty =
        AvaloniaProperty.Register<ParameterRow, double>(nameof(Maximum), double.PositiveInfinity);

    public double Maximum
    {
        get => GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    public static readonly StyledProperty<string> FormatProperty =
        AvaloniaProperty.Register<ParameterRow, string>(nameof(Format), "F1");

    public string Format
    {
        get => GetValue(FormatProperty);
        set => SetValue(FormatProperty, value);
    }

    public static readonly StyledProperty<bool> IsReadOnlyProperty =
        AvaloniaProperty.Register<ParameterRow, bool>(nameof(IsReadOnly));

    public bool IsReadOnly
    {
        get => GetValue(IsReadOnlyProperty);
        set => SetValue(IsReadOnlyProperty, value);
    }

    /// <summary>
    /// 下发命令。参数是解析后的目标值（double）。
    /// 执行完不要自己改状态——等设备回读，再调
    /// <see cref="CompleteWrite"/> 或 <see cref="FailWrite"/>。
    /// </summary>
    public static readonly StyledProperty<ICommand?> ApplyCommandProperty =
        AvaloniaProperty.Register<ParameterRow, ICommand?>(nameof(ApplyCommand));

    public ICommand? ApplyCommand
    {
        get => GetValue(ApplyCommandProperty);
        set => SetValue(ApplyCommandProperty, value);
    }

    public static readonly StyledProperty<ICommand?> RevertCommandProperty =
        AvaloniaProperty.Register<ParameterRow, ICommand?>(nameof(RevertCommand));

    public ICommand? RevertCommand
    {
        get => GetValue(RevertCommandProperty);
        set => SetValue(RevertCommandProperty, value);
    }

    // ---- 只读投影 ----------------------------------------------------------

    private ParameterWriteState _writeState = ParameterWriteState.Clean;

    public static readonly DirectProperty<ParameterRow, ParameterWriteState> WriteStateProperty =
        AvaloniaProperty.RegisterDirect<ParameterRow, ParameterWriteState>(
            nameof(WriteState), o => o._writeState);

    public ParameterWriteState WriteState
    {
        get => _writeState;
        private set => SetAndRaise(WriteStateProperty, ref _writeState, value);
    }

    private string? _stateText;

    public static readonly DirectProperty<ParameterRow, string?> StateTextProperty =
        AvaloniaProperty.RegisterDirect<ParameterRow, string?>(nameof(StateText), o => o._stateText);

    /// <summary>行尾徽章的文字。</summary>
    public string? StateText
    {
        get => _stateText;
        private set => SetAndRaise(StateTextProperty, ref _stateText, value);
    }

    private string? _actualText;

    public static readonly DirectProperty<ParameterRow, string?> ActualTextProperty =
        AvaloniaProperty.RegisterDirect<ParameterRow, string?>(nameof(ActualText), o => o._actualText);

    /// <summary>格式化后的读值。必须过 <see cref="Format"/>，否则 85.0 会显示成 85，
    /// 一列数字的小数位数对不齐——数值列对不齐就失去了 tabular-nums 的意义。</summary>
    public string? ActualText
    {
        get => _actualText;
        private set => SetAndRaise(ActualTextProperty, ref _actualText, value);
    }

    private bool _inputLocked;

    public static readonly DirectProperty<ParameterRow, bool> IsInputLockedProperty =
        AvaloniaProperty.RegisterDirect<ParameterRow, bool>(nameof(IsInputLocked), o => o._inputLocked);

    /// <summary>
    /// 输入框是否该锁住。只读，或正在等回读时都要锁——
    /// Evaluate() 在 Writing 态直接 return，此时改框里的字不会被重新判定，
    /// 「写入中」的徽章下面可以并排显示一个从未下发、也从未校验过的数字。
    /// </summary>
    public bool IsInputLocked
    {
        get => _inputLocked;
        private set => SetAndRaise(IsInputLockedProperty, ref _inputLocked, value);
    }

    private bool _canApply;

    public static readonly DirectProperty<ParameterRow, bool> CanApplyProperty =
        AvaloniaProperty.RegisterDirect<ParameterRow, bool>(nameof(CanApply), o => o._canApply);

    /// <summary>下发按钮是否可用。超量程、下发中、只读、没改动时都不可用。</summary>
    public bool CanApply
    {
        get => _canApply;
        private set => SetAndRaise(CanApplyProperty, ref _canApply, value);
    }

    public static readonly RoutedEvent<RoutedEventArgs> WriteRequestedEvent =
        RoutedEvent.Register<ParameterRow, RoutedEventArgs>(
            nameof(WriteRequested), RoutingStrategies.Bubble);

    public event EventHandler<RoutedEventArgs>? WriteRequested
    {
        add => AddHandler(WriteRequestedEvent, value);
        remove => RemoveHandler(WriteRequestedEvent, value);
    }

    static ParameterRow()
    {
        PendingTextProperty.Changed.AddClassHandler<ParameterRow>((x, _) => x.Evaluate());
        SetpointProperty.Changed.AddClassHandler<ParameterRow>((x, _) => x.OnSetpointChanged());
        MinimumProperty.Changed.AddClassHandler<ParameterRow>((x, _) => x.Evaluate());
        MaximumProperty.Changed.AddClassHandler<ParameterRow>((x, _) => x.Evaluate());
        ActualValueProperty.Changed.AddClassHandler<ParameterRow>((x, _) => x.UpdateActualText());
        // Evaluate 里「有没有改动」和超量程徽章文字都依赖 Format，只刷新读值文本的话
        // 状态机会停在按旧格式算出来的结论上。Evaluate 内部有 Writing 闸，不会打断在途写入。
        FormatProperty.Changed.AddClassHandler<ParameterRow>((x, _) =>
        {
            x.UpdateActualText();
            x.Evaluate();
        });
        IsReadOnlyProperty.Changed.AddClassHandler<ParameterRow>(
            (x, e) => { x.PseudoClasses.Set(":readonly", e.NewValue is true); x.Evaluate(); });
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        if (_applyButton is not null)
            _applyButton.Click -= OnApplyClicked;

        _applyButton = e.NameScope.Find<Button>("PART_Apply");
        if (_applyButton is not null)
            _applyButton.Click += OnApplyClicked;

        if (_revertButton is not null)
            _revertButton.Click -= OnRevertClicked;

        _revertButton = e.NameScope.Find<Button>("PART_Revert");
        if (_revertButton is not null)
            _revertButton.Click += OnRevertClicked;
    }

    private void OnApplyClicked(object? sender, RoutedEventArgs e) => Apply();

    private void OnRevertClicked(object? sender, RoutedEventArgs e) => Revert();

    public ParameterRow()
    {
        _lastApplied = Setpoint;
        PendingText = Setpoint.ToString(Format, CultureInfo.CurrentCulture);
        UpdateActualText();
        Evaluate();
    }

    private void UpdateActualText() =>
        ActualText = ActualValue?.ToString(Format, CultureInfo.CurrentCulture) ?? "—";

    private void OnSetpointChanged()
    {
        // 设定值被外部改掉（比如切了配方）时，输入框跟着走，并把它当成新的基准。
        //
        // Writing 期间不更新基准：Setpoint 是 TwoWay，轮询回来的设定值寄存器、
        // 或 VM 下发时顺手回写的乐观值，都会在等回读的窗口里改到它。
        // 让它顶掉 _lastApplied 之后，FailWrite 回滚拿到的就不再是「上次成功值」。
        if (WriteState is not ParameterWriteState.Writing)
        {
            _lastApplied = Setpoint;
            PendingText = Setpoint.ToString(Format, CultureInfo.CurrentCulture);
        }

        Evaluate();
    }

    /// <summary>
    /// 装入一个新的设定值并把它作为新基准。<b>外部重设设定值一律走这里。</b>
    ///
    /// 不能只写 <see cref="Setpoint"/>：Avalonia 对相等的新值不发变更通知，
    /// 新值恰好等于当前设定值时 <c>OnSetpointChanged</c> 根本不触发，
    /// 「切了配方之后输入框跟着走」在这条路径上静默失效——框里会留着上一个配方
    /// 编辑到一半的值。对应 <see cref="NumericKeypad.LoadValue"/>。
    ///
    /// 正在等回读时只更新 <see cref="Setpoint"/> 本身，不动基准与输入框。
    /// </summary>
    public void LoadSetpoint(double value)
    {
        SetValue(SetpointProperty, value);

        if (WriteState is not ParameterWriteState.Writing)
        {
            _lastApplied = value;
            PendingText = value.ToString(Format, CultureInfo.CurrentCulture);
        }

        Evaluate();
    }

    /// <summary>
    /// 边界是否可用：有限值可以，本控件默认的那一侧无穷也可以（表示不设限）。
    /// NaN 与方向相反的无穷都是配置错误。
    /// </summary>
    private static bool IsValidBound(double bound, double openSide) =>
        double.IsFinite(bound) || bound.Equals(openSide);

    /// <summary>解析当前输入。解析不出来时返回 null。</summary>
    public double? ParsePending() =>
        double.TryParse(PendingText, NumberStyles.Float, CultureInfo.CurrentCulture, out var v)
            ? v
            : null;

    private void Evaluate()
    {
        if (WriteState == ParameterWriteState.Writing) return;   // 下发中不改判定

        var parsed = ParsePending();
        var applied = _lastApplied ?? Setpoint;

        // 量程本身配错时整行 fail-closed。未初始化的量程绑定很容易给出 NaN，
        // 而 NaN 边界会让闸门整体失效——普通数字也能穿过去。
        // 这不是操作员输错，别用「超量程」的措辞把配置错误伪装成他的问题。
        if (!IsValidBound(Minimum, double.NegativeInfinity)
            || !IsValidBound(Maximum, double.PositiveInfinity)
            || Minimum > Maximum)
        {
            SetState(ParameterWriteState.OutOfRange, "量程无效，禁止下发", canApply: false);
            return;
        }

        // 闸门写成 !(v >= Minimum && v <= Maximum) 而不是 v < Minimum || v > Maximum：
        // NaN 与任何数比较恒为 false，后者会让 NaN 从整个闸门底下穿过去，
        // 被判成 Dirty、CanApply=true，然后原样下发给 ApplyCommand 写进 PLC 浮点点位。
        // IsFinite 这一道同样不能省：NumberStyles.Float 接受 "NaN" / "Infinity" / "1e400"。
        // 与 NumericKeypad.Evaluate 里的两道闸同源。
        if (parsed is not { } value || !double.IsFinite(value)
            || !(value >= Minimum && value <= Maximum))
        {
            var lo = double.IsNegativeInfinity(Minimum) ? "…" : Minimum.ToString(Format, CultureInfo.CurrentCulture);
            var hi = double.IsPositiveInfinity(Maximum) ? "…" : Maximum.ToString(Format, CultureInfo.CurrentCulture);
            SetState(ParameterWriteState.OutOfRange, $"超量程 {lo}–{hi}", canApply: false);
            return;
        }

        // 比较用格式化后的字符串。这一条真正在扛的是设备量化：写 90.0 回读 90.04，
        // 按显示精度算是「已生效」，按 == 算会永远判成 Dirty、再也回不到 Clean。
        // 但两者不严格相等时徽章不能替设备说「已生效」——换一句诚实的措辞。
        var appliedText = applied.ToString(Format, CultureInfo.CurrentCulture);
        var sameText = value.ToString(Format, CultureInfo.CurrentCulture) == appliedText;

        if (sameText)
            SetState(
                ParameterWriteState.Clean,
                value == applied ? "已生效" : "显示精度内一致",
                canApply: false);
        else
            SetState(ParameterWriteState.Dirty, "待下发", canApply: true);
    }

    private void SetState(ParameterWriteState state, string? text, bool canApply)
    {
        // 只读在这里统一压制，而不是在 Evaluate 里早退成 Clean。
        // 早退是假陈述：Clean 的定义是「输入值和已生效值一致」，而框里可以留着
        // 一个超量程或未下发的值；而且会把 :dirty / :outofrange 三重提示一并清掉，
        // 未下发的编辑不该因为锁定就从屏幕上消失。
        if (IsReadOnly)
        {
            text = "只读";
            canApply = false;
        }

        WriteState = state;
        StateText = text;
        CanApply = canApply;
        IsInputLocked = IsReadOnly || state == ParameterWriteState.Writing;

        PseudoClasses.Set(":dirty", state == ParameterWriteState.Dirty);
        PseudoClasses.Set(":writing", state == ParameterWriteState.Writing);
        PseudoClasses.Set(":failed", state == ParameterWriteState.Failed);
        PseudoClasses.Set(":outofrange", state == ParameterWriteState.OutOfRange);
    }

    /// <summary>
    /// 请求下发。超量程或没改动时是空操作。
    /// 进 <see cref="ParameterWriteState.Writing"/> 之后就等着应用侧回调
    /// <see cref="CompleteWrite"/> / <see cref="FailWrite"/>。
    /// </summary>
    public bool Apply()
    {
        if (!CanApply) return false;
        if (ParsePending() is not { } target) return false;

        // 必须先进 Writing 再派发：同步的 WriteRequested 处理器里直接调 CompleteWrite
        // 是合法用法，先派发后置状态会把它的结果覆盖掉。
        SetState(ParameterWriteState.Writing, "写入中", canApply: false);

        // 挂了命令却不能执行 = 指令没发出去。此时若仍停在 Writing，Evaluate 的第一行
        // 会冻结一切重新判定，CanApply 恒为 false，Apply 再也进不去，
        // 而唯一出口 CompleteWrite / FailWrite 永远不会有人来调——状态机死锁在「写入中」。
        //
        // 没挂命令时事件是唯一通道。本控件的契约就是「执行完等设备回读再调
        // CompleteWrite / FailWrite」，只听事件的用法不会去设 Handled，
        // 按未受理回滚会把随后真正到达的回读一并吞掉，所以这里按受理处理。
        var refused = ApplyCommand is { } cmd && !cmd.CanExecute(target);
        if (!refused) ApplyCommand?.Execute(target);

        var args = new RoutedEventArgs(WriteRequestedEvent);
        RaiseEvent(args);

        if (refused && !args.Handled && WriteState == ParameterWriteState.Writing)
        {
            WriteState = ParameterWriteState.Clean;   // 先退出 Writing，Evaluate 才会重新判定
            Evaluate();                               // 回到 Dirty，操作员可以重试
            return false;
        }

        return true;
    }

    /// <summary>
    /// 下发成功。<paramref name="readbackValue"/> 必须是**设备回读回来的值**，
    /// 不是刚才写下去的值——设备可能做了限幅或量化，显示输入值等于骗人。
    /// </summary>
    public void CompleteWrite(double readbackValue)
    {
        // 只在等回读时受理。异步设备通讯里迟到、重复、串号的应答是常态
        // （超时后又收到确认、重试的两条应答先后到达），不加闸的话它们会
        // 直接改写基准与设定值，把一个早已作废的结果盖到当前编辑上。
        // 只想更新设备读值不碰状态机的话，直接写 ActualValue 就行。
        if (WriteState is not ParameterWriteState.Writing) return;

        _lastApplied = readbackValue;
        WriteState = ParameterWriteState.Clean;         // 先退出 Writing，Evaluate 才会重新判定
        Setpoint = readbackValue;
        ActualValue = readbackValue;
        PendingText = readbackValue.ToString(Format, CultureInfo.CurrentCulture);
        Evaluate();
    }

    /// <summary>下发失败。值回滚到上次成功值，让操作员看到设备上真实生效的是什么。</summary>
    public void FailWrite(string? message = null)
    {
        if (WriteState is not ParameterWriteState.Writing) return;

        var applied = _lastApplied ?? Setpoint;
        WriteState = ParameterWriteState.Clean;

        // 设定值一并压回上次成功值。只回滚 PendingText 的话，Failed 态下
        // Setpoint（TwoWay，VM 手上那份）和框里的数字会互相矛盾。
        Setpoint = applied;
        PendingText = applied.ToString(Format, CultureInfo.CurrentCulture);

        SetState(ParameterWriteState.Failed, message ?? "下发失败", canApply: false);
    }

    /// <summary>
    /// <see cref="INumericInputTarget"/> 的下发入口。转调 <see cref="Apply"/>——
    /// 键盘不该绕过这里的量程判定与状态机自己写值。
    ///
    /// 正在下发（等回读）、只读、或本行自身判定不通过时返回 false，
    /// 由键盘负责回滚并且不报「已确认」。
    /// </summary>
    public bool CommitPending() => Apply();

    /// <summary>放弃修改，回到上次成功值。</summary>
    public void Revert()
    {
        var applied = _lastApplied ?? Setpoint;
        WriteState = ParameterWriteState.Clean;
        PendingText = applied.ToString(Format, CultureInfo.CurrentCulture);
        Evaluate();

        if (RevertCommand?.CanExecute(null) == true)
            RevertCommand.Execute(null);
    }

    /// <summary>见 <see cref="Cobalt.Fluent.Automation.ParameterRowAutomationPeer"/>。</summary>
    protected override AutomationPeer OnCreateAutomationPeer() => new ParameterRowAutomationPeer(this);
}
