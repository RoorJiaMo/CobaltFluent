using Avalonia.Automation.Peers;
using Cobalt.Fluent.Automation;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;

namespace Cobalt.Fluent.Controls;

/// <summary>指向目标的方向。beak 画在对侧。</summary>
public enum TeachingTipPlacement
{
    Top,
    Bottom,
    Left,
    Right,
}

/// <summary>
/// 引导提示。带 beak（小尖角）指向目标控件。
///
/// 和 <see cref="ToolTip"/> 的区别：ToolTip 是悬停即现、移开即走的补充说明；
/// TeachingTip 是主动弹出、要用户确认的一次性引导，可以带操作按钮。
/// </summary>
[PseudoClasses(":top", ":bottom", ":left", ":right")]
public class TeachingTip : TemplatedControl
{
    private Button? _actionButton;
    private Button? _closeButton;

    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<TeachingTip, string?>(nameof(Title));

    public string? Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public static readonly StyledProperty<string?> SubtitleProperty =
        AvaloniaProperty.Register<TeachingTip, string?>(nameof(Subtitle));

    public string? Subtitle
    {
        get => GetValue(SubtitleProperty);
        set => SetValue(SubtitleProperty, value);
    }

    public static readonly StyledProperty<string?> ActionButtonContentProperty =
        AvaloniaProperty.Register<TeachingTip, string?>(nameof(ActionButtonContent));

    public string? ActionButtonContent
    {
        get => GetValue(ActionButtonContentProperty);
        set => SetValue(ActionButtonContentProperty, value);
    }

    public static readonly StyledProperty<string?> CloseButtonContentProperty =
        AvaloniaProperty.Register<TeachingTip, string?>(nameof(CloseButtonContent), "知道了");

    public string? CloseButtonContent
    {
        get => GetValue(CloseButtonContentProperty);
        set => SetValue(CloseButtonContentProperty, value);
    }

    public static readonly StyledProperty<ICommand?> ActionCommandProperty =
        AvaloniaProperty.Register<TeachingTip, ICommand?>(nameof(ActionCommand));

    public ICommand? ActionCommand
    {
        get => GetValue(ActionCommandProperty);
        set => SetValue(ActionCommandProperty, value);
    }

    public static readonly StyledProperty<TeachingTipPlacement> PlacementProperty =
        AvaloniaProperty.Register<TeachingTip, TeachingTipPlacement>(
            nameof(Placement), TeachingTipPlacement.Bottom);

    /// <summary>相对目标的位置。Bottom 表示提示在目标下方，beak 朝上。</summary>
    public TeachingTipPlacement Placement
    {
        get => GetValue(PlacementProperty);
        set => SetValue(PlacementProperty, value);
    }

    public static readonly StyledProperty<bool> IsOpenProperty =
        AvaloniaProperty.Register<TeachingTip, bool>(
            nameof(IsOpen), true, defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    public bool IsOpen
    {
        get => GetValue(IsOpenProperty);
        set => SetValue(IsOpenProperty, value);
    }

    public static readonly RoutedEvent<RoutedEventArgs> ClosedEvent =
        RoutedEvent.Register<TeachingTip, RoutedEventArgs>(nameof(Closed), RoutingStrategies.Bubble);

    public event EventHandler<RoutedEventArgs>? Closed
    {
        add => AddHandler(ClosedEvent, value);
        remove => RemoveHandler(ClosedEvent, value);
    }

    static TeachingTip()
    {
        PlacementProperty.Changed.AddClassHandler<TeachingTip>((x, _) => x.Refresh());
        IsOpenProperty.Changed.AddClassHandler<TeachingTip>((x, e) => x.IsVisible = e.NewValue is true);
    }

    public TeachingTip() => Refresh();

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        if (_actionButton is not null) _actionButton.Click -= OnAction;
        _actionButton = e.NameScope.Find<Button>("PART_ActionButton");
        if (_actionButton is not null) _actionButton.Click += OnAction;

        if (_closeButton is not null) _closeButton.Click -= OnClose;
        _closeButton = e.NameScope.Find<Button>("PART_CloseButton");
        if (_closeButton is not null) _closeButton.Click += OnClose;
    }

    private void OnAction(object? sender, RoutedEventArgs e)
    {
        if (ActionCommand?.CanExecute(null) == true)
            ActionCommand.Execute(null);

        Close();
    }

    private void OnClose(object? sender, RoutedEventArgs e) => Close();

    public void Close()
    {
        if (!IsOpen) return;
        IsOpen = false;
        RaiseEvent(new RoutedEventArgs(ClosedEvent));
    }

    private void Refresh()
    {
        var p = Placement;
        PseudoClasses.Set(":top", p == TeachingTipPlacement.Top);
        PseudoClasses.Set(":bottom", p == TeachingTipPlacement.Bottom);
        PseudoClasses.Set(":left", p == TeachingTipPlacement.Left);
        PseudoClasses.Set(":right", p == TeachingTipPlacement.Right);
    }

    /// <summary>见 <see cref="Cobalt.Fluent.Automation.TeachingTipAutomationPeer"/>。</summary>
    protected override AutomationPeer OnCreateAutomationPeer() => new TeachingTipAutomationPeer(this);
}
