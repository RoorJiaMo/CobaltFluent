using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Interactivity;
using Avalonia.Metadata;

namespace Cobalt.Fluent.Controls;

/// <summary>报警级别。Alarm 及以上要求立即处置。</summary>
public enum AlarmSeverity
{
    /// <summary>提示。跟随主题状态色。</summary>
    Info,

    /// <summary>警告。还能继续跑，但要注意。</summary>
    Warning,

    /// <summary>报警。需要立即处置，用安全红实底 + 呼吸。</summary>
    Alarm,

    /// <summary>故障。安全红实底 + 黄色左边条（ISO 13850 的红黄配）。</summary>
    Fault,
}

/// <summary>
/// 报警横幅。
///
/// 第 7 组的三条硬约束：
///
/// 1. **Alarm / Fault 用安全红，不跟随主题。**
///    不能用 <c>SystemFillColorCriticalBrush</c>——那个在深色主题下是浅粉 <c>#FF99A4</c>，
///    对需要立即处置的级别是错的。
/// 2. **用呼吸不用闪烁**（1.5s，opacity 1↔.62）。
///    高频闪烁引发疲劳，且有光敏性癫痫风险。
/// 3. **`prefers-reduced-motion` 下关掉动画后必须用黄色描边补强**，
///    否则降级后 Alarm 和 Warning 就分不出来了。
///    见 <see cref="IsBreathingEnabled"/>。
///
/// 确认（<see cref="IsAcknowledged"/>）之后停呼吸，但**横幅不消失**——
/// 报警条件还在，只是操作员表示看到了。
/// </summary>
[PseudoClasses(":info", ":warning", ":alarm", ":fault", ":acknowledged", ":breathing")]
public class AlarmBanner : TemplatedControl
{
    public static readonly StyledProperty<AlarmSeverity> SeverityProperty =
        AvaloniaProperty.Register<AlarmBanner, AlarmSeverity>(nameof(Severity), AlarmSeverity.Info);

    public AlarmSeverity Severity
    {
        get => GetValue(SeverityProperty);
        set => SetValue(SeverityProperty, value);
    }

    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<AlarmBanner, string?>(nameof(Title));

    public string? Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public static readonly StyledProperty<string?> DetailProperty =
        AvaloniaProperty.Register<AlarmBanner, string?>(nameof(Detail));

    public string? Detail
    {
        get => GetValue(DetailProperty);
        set => SetValue(DetailProperty, value);
    }

    public static readonly StyledProperty<DateTime?> TimestampProperty =
        AvaloniaProperty.Register<AlarmBanner, DateTime?>(nameof(Timestamp));

    /// <summary>报警发生时刻。复盘时这一列比什么都重要，所以要 tabular-nums。</summary>
    public DateTime? Timestamp
    {
        get => GetValue(TimestampProperty);
        set => SetValue(TimestampProperty, value);
    }

    public static readonly StyledProperty<bool> IsAcknowledgedProperty =
        AvaloniaProperty.Register<AlarmBanner, bool>(
            nameof(IsAcknowledged), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    /// <summary>已确认。停呼吸，但横幅不消失——报警条件还在。</summary>
    public bool IsAcknowledged
    {
        get => GetValue(IsAcknowledgedProperty);
        set => SetValue(IsAcknowledgedProperty, value);
    }

    /// <summary>同类报警还有几条被折叠了。0 表示不显示折叠提示。</summary>
    public static readonly StyledProperty<int> AdditionalCountProperty =
        AvaloniaProperty.Register<AlarmBanner, int>(nameof(AdditionalCount));

    public int AdditionalCount
    {
        get => GetValue(AdditionalCountProperty);
        set => SetValue(AdditionalCountProperty, value);
    }

    public static readonly StyledProperty<ICommand?> AcknowledgeCommandProperty =
        AvaloniaProperty.Register<AlarmBanner, ICommand?>(nameof(AcknowledgeCommand));

    public ICommand? AcknowledgeCommand
    {
        get => GetValue(AcknowledgeCommandProperty);
        set => SetValue(AcknowledgeCommandProperty, value);
    }

    public static readonly StyledProperty<ICommand?> DetailsCommandProperty =
        AvaloniaProperty.Register<AlarmBanner, ICommand?>(nameof(DetailsCommand));

    public ICommand? DetailsCommand
    {
        get => GetValue(DetailsCommandProperty);
        set => SetValue(DetailsCommandProperty, value);
    }

    /// <summary>
    /// 呼吸动画开关。关掉时模板会补一圈安全黄描边——
    /// **这不是可选的补偿**：不补的话降级后 Alarm 和 Warning 长得一样。
    /// 无障碍设置里开了「减少动态效果」，或者嵌入式上要省 GPU 时关掉它。
    /// </summary>
    public static readonly StyledProperty<bool> IsBreathingEnabledProperty =
        AvaloniaProperty.Register<AlarmBanner, bool>(nameof(IsBreathingEnabled), true);

    public bool IsBreathingEnabled
    {
        get => GetValue(IsBreathingEnabledProperty);
        set => SetValue(IsBreathingEnabledProperty, value);
    }

    /// <summary>额外的操作区内容。内置的「确认」按钮之外还要放东西时用它。</summary>
    public static readonly StyledProperty<object?> ActionsProperty =
        AvaloniaProperty.Register<AlarmBanner, object?>(nameof(Actions));

    [Content]
    public object? Actions
    {
        get => GetValue(ActionsProperty);
        set => SetValue(ActionsProperty, value);
    }

    public static readonly StyledProperty<IDataTemplate?> ActionsTemplateProperty =
        AvaloniaProperty.Register<AlarmBanner, IDataTemplate?>(nameof(ActionsTemplate));

    public IDataTemplate? ActionsTemplate
    {
        get => GetValue(ActionsTemplateProperty);
        set => SetValue(ActionsTemplateProperty, value);
    }

    /// <summary>确认按钮上的字。</summary>
    public static readonly StyledProperty<string> AcknowledgeContentProperty =
        AvaloniaProperty.Register<AlarmBanner, string>(nameof(AcknowledgeContent), "确认");

    public string AcknowledgeContent
    {
        get => GetValue(AcknowledgeContentProperty);
        set => SetValue(AcknowledgeContentProperty, value);
    }

    /// <summary>详情按钮上的字。</summary>
    public static readonly StyledProperty<string> DetailsContentProperty =
        AvaloniaProperty.Register<AlarmBanner, string>(nameof(DetailsContent), "详情");

    public string DetailsContent
    {
        get => GetValue(DetailsContentProperty);
        set => SetValue(DetailsContentProperty, value);
    }

    private string? _timeText;

    public static readonly DirectProperty<AlarmBanner, string?> TimeTextProperty =
        AvaloniaProperty.RegisterDirect<AlarmBanner, string?>(nameof(TimeText), o => o._timeText);

    /// <summary>格式化后的时间戳。复盘靠这一列，所以要 tabular-nums。</summary>
    public string? TimeText
    {
        get => _timeText;
        private set => SetAndRaise(TimeTextProperty, ref _timeText, value);
    }

    private string? _additionalText;

    public static readonly DirectProperty<AlarmBanner, string?> AdditionalTextProperty =
        AvaloniaProperty.RegisterDirect<AlarmBanner, string?>(
            nameof(AdditionalText), o => o._additionalText);

    /// <summary>折叠提示文字。<see cref="AdditionalCount"/> 为 0 时是 null。</summary>
    public string? AdditionalText
    {
        get => _additionalText;
        private set => SetAndRaise(AdditionalTextProperty, ref _additionalText, value);
    }

    private Symbol _glyph = Symbol.Info;

    public static readonly DirectProperty<AlarmBanner, Symbol> GlyphProperty =
        AvaloniaProperty.RegisterDirect<AlarmBanner, Symbol>(nameof(Glyph), o => o._glyph);

    public Symbol Glyph
    {
        get => _glyph;
        private set => SetAndRaise(GlyphProperty, ref _glyph, value);
    }

    public static readonly RoutedEvent<RoutedEventArgs> AcknowledgedEvent =
        RoutedEvent.Register<AlarmBanner, RoutedEventArgs>(
            nameof(Acknowledged), RoutingStrategies.Bubble);

    public event EventHandler<RoutedEventArgs>? Acknowledged
    {
        add => AddHandler(AcknowledgedEvent, value);
        remove => RemoveHandler(AcknowledgedEvent, value);
    }

    static AlarmBanner()
    {
        SeverityProperty.Changed.AddClassHandler<AlarmBanner>((x, _) => x.Refresh());
        TimestampProperty.Changed.AddClassHandler<AlarmBanner>((x, _) => x.Refresh());
        AdditionalCountProperty.Changed.AddClassHandler<AlarmBanner>((x, _) => x.Refresh());
        IsAcknowledgedProperty.Changed.AddClassHandler<AlarmBanner>((x, _) => x.Refresh());
        IsBreathingEnabledProperty.Changed.AddClassHandler<AlarmBanner>((x, _) => x.Refresh());
    }

    public AlarmBanner() => Refresh();

    private void Refresh()
    {
        var severity = Severity;
        PseudoClasses.Set(":info", severity == AlarmSeverity.Info);
        PseudoClasses.Set(":warning", severity == AlarmSeverity.Warning);
        PseudoClasses.Set(":alarm", severity == AlarmSeverity.Alarm);
        PseudoClasses.Set(":fault", severity == AlarmSeverity.Fault);
        PseudoClasses.Set(":acknowledged", IsAcknowledged);

        // 只有未确认的 Alarm 呼吸。Fault 不呼吸——它是稳态故障，靠黄色左边条区分。
        PseudoClasses.Set(":breathing",
            severity == AlarmSeverity.Alarm && !IsAcknowledged && IsBreathingEnabled);

        TimeText = Timestamp?.ToString("HH:mm:ss");
        AdditionalText = AdditionalCount > 0 ? $"另有 {AdditionalCount} 条同类报警" : null;

        Glyph = severity switch
        {
            AlarmSeverity.Warning => Symbol.Warning,
            AlarmSeverity.Alarm => Symbol.Warning,
            AlarmSeverity.Fault => Symbol.Error,
            _ => Symbol.Info,
        };
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        if (_ackButton is not null) _ackButton.Click -= OnAckClicked;
        _ackButton = e.NameScope.Find<Button>("PART_Acknowledge");
        if (_ackButton is not null) _ackButton.Click += OnAckClicked;

        if (_detailsButton is not null) _detailsButton.Click -= OnDetailsClicked;
        _detailsButton = e.NameScope.Find<Button>("PART_Details");
        if (_detailsButton is not null) _detailsButton.Click += OnDetailsClicked;
    }

    private Button? _ackButton;
    private Button? _detailsButton;

    private void OnAckClicked(object? sender, RoutedEventArgs e) => Acknowledge();

    private void OnDetailsClicked(object? sender, RoutedEventArgs e)
    {
        if (DetailsCommand?.CanExecute(null) == true)
            DetailsCommand.Execute(null);
    }

    /// <summary>确认。幂等。</summary>
    public void Acknowledge()
    {
        if (IsAcknowledged) return;

        IsAcknowledged = true;

        if (AcknowledgeCommand?.CanExecute(null) == true)
            AcknowledgeCommand.Execute(null);

        RaiseEvent(new RoutedEventArgs(AcknowledgedEvent));
    }
}
