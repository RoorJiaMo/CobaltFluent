using System.Globalization;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;

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
        FormatProperty.Changed.AddClassHandler<ParameterRow>((x, _) => x.UpdateActualText());
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
        // 设定值被外部改掉（比如切了配方）时，输入框跟着走，并把它当成新的基准
        _lastApplied = Setpoint;
        if (WriteState is not ParameterWriteState.Writing)
            PendingText = Setpoint.ToString(Format, CultureInfo.CurrentCulture);
        Evaluate();
    }

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

        if (IsReadOnly)
        {
            SetState(ParameterWriteState.Clean, "只读", canApply: false);
            return;
        }

        if (parsed is null || parsed < Minimum || parsed > Maximum)
        {
            var lo = double.IsNegativeInfinity(Minimum) ? "…" : Minimum.ToString(Format, CultureInfo.CurrentCulture);
            var hi = double.IsPositiveInfinity(Maximum) ? "…" : Maximum.ToString(Format, CultureInfo.CurrentCulture);
            SetState(ParameterWriteState.OutOfRange, $"超量程 {lo}–{hi}", canApply: false);
            return;
        }

        // 比较用格式化后的字符串，避免 85.50 和 85.5 被判成不同
        var same = parsed.Value.ToString(Format, CultureInfo.CurrentCulture)
                   == applied.ToString(Format, CultureInfo.CurrentCulture);

        if (same)
            SetState(ParameterWriteState.Clean, "已生效", canApply: false);
        else
            SetState(ParameterWriteState.Dirty, "待下发", canApply: true);
    }

    private void SetState(ParameterWriteState state, string? text, bool canApply)
    {
        WriteState = state;
        StateText = text;
        CanApply = canApply;

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
    public void Apply()
    {
        if (!CanApply) return;
        if (ParsePending() is not { } target) return;

        SetState(ParameterWriteState.Writing, "写入中", canApply: false);

        if (ApplyCommand?.CanExecute(target) == true)
            ApplyCommand.Execute(target);

        RaiseEvent(new RoutedEventArgs(WriteRequestedEvent));
    }

    /// <summary>
    /// 下发成功。<paramref name="readbackValue"/> 必须是**设备回读回来的值**，
    /// 不是刚才写下去的值——设备可能做了限幅或量化，显示输入值等于骗人。
    /// </summary>
    public void CompleteWrite(double readbackValue)
    {
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
        var applied = _lastApplied ?? Setpoint;
        WriteState = ParameterWriteState.Clean;
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
    public bool CommitPending()
    {
        if (!CanApply) return false;
        Apply();
        return true;
    }

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
}
