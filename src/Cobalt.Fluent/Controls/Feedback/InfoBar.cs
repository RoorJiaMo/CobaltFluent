using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Metadata;

namespace Cobalt.Fluent.Controls;

/// <summary>
/// 页面内的持久提示条。Avalonia 本体没有这个控件。
///
/// 和 <see cref="Toast"/> 的区别是它**占布局、不自动消失**——
/// 用于「这条信息在条件解除前一直成立」的场景。
/// 需要立即处置的设备级报警用 <see cref="AlarmBanner"/>，那个走安全色。
/// </summary>
[PseudoClasses(":informational", ":success", ":warning", ":error")]
public class InfoBar : TemplatedControl
{
    private Button? _closeButton;

    public static readonly StyledProperty<InfoBarSeverity> SeverityProperty =
        AvaloniaProperty.Register<InfoBar, InfoBarSeverity>(
            nameof(Severity), InfoBarSeverity.Informational);

    public InfoBarSeverity Severity
    {
        get => GetValue(SeverityProperty);
        set => SetValue(SeverityProperty, value);
    }

    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<InfoBar, string?>(nameof(Title));

    public string? Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public static readonly StyledProperty<string?> MessageProperty =
        AvaloniaProperty.Register<InfoBar, string?>(nameof(Message));

    public string? Message
    {
        get => GetValue(MessageProperty);
        set => SetValue(MessageProperty, value);
    }

    public static readonly StyledProperty<bool> IsClosableProperty =
        AvaloniaProperty.Register<InfoBar, bool>(nameof(IsClosable), true);

    public bool IsClosable
    {
        get => GetValue(IsClosableProperty);
        set => SetValue(IsClosableProperty, value);
    }

    public static readonly StyledProperty<bool> IsOpenProperty =
        AvaloniaProperty.Register<InfoBar, bool>(
            nameof(IsOpen), true, defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    /// <summary>关掉之后整条不占布局（IsVisible=false），不是只隐藏内容。</summary>
    public bool IsOpen
    {
        get => GetValue(IsOpenProperty);
        set => SetValue(IsOpenProperty, value);
    }

    /// <summary>右侧动作区。放「重试」「查看详情」这类按钮。</summary>
    public static readonly StyledProperty<object?> ActionContentProperty =
        AvaloniaProperty.Register<InfoBar, object?>(nameof(ActionContent));

    [Content]
    public object? ActionContent
    {
        get => GetValue(ActionContentProperty);
        set => SetValue(ActionContentProperty, value);
    }

    public static readonly StyledProperty<ICommand?> CloseCommandProperty =
        AvaloniaProperty.Register<InfoBar, ICommand?>(nameof(CloseCommand));

    public ICommand? CloseCommand
    {
        get => GetValue(CloseCommandProperty);
        set => SetValue(CloseCommandProperty, value);
    }

    private Symbol _glyph = Symbol.Info;

    public static readonly DirectProperty<InfoBar, Symbol> GlyphProperty =
        AvaloniaProperty.RegisterDirect<InfoBar, Symbol>(nameof(Glyph), o => o._glyph);

    public Symbol Glyph
    {
        get => _glyph;
        private set => SetAndRaise(GlyphProperty, ref _glyph, value);
    }

    public static readonly RoutedEvent<RoutedEventArgs> ClosedEvent =
        RoutedEvent.Register<InfoBar, RoutedEventArgs>(nameof(Closed), RoutingStrategies.Bubble);

    public event EventHandler<RoutedEventArgs>? Closed
    {
        add => AddHandler(ClosedEvent, value);
        remove => RemoveHandler(ClosedEvent, value);
    }

    static InfoBar()
    {
        SeverityProperty.Changed.AddClassHandler<InfoBar>((x, _) => x.Refresh());
        IsOpenProperty.Changed.AddClassHandler<InfoBar>(
            (x, e) => x.IsVisible = e.NewValue is true);
    }

    public InfoBar() => Refresh();

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        if (_closeButton is not null) _closeButton.Click -= OnCloseClicked;
        _closeButton = e.NameScope.Find<Button>("PART_Close");
        if (_closeButton is not null) _closeButton.Click += OnCloseClicked;
    }

    private void OnCloseClicked(object? sender, RoutedEventArgs e) => Close();

    /// <summary>关闭。幂等。</summary>
    public void Close()
    {
        if (!IsOpen) return;

        IsOpen = false;

        if (CloseCommand?.CanExecute(null) == true)
            CloseCommand.Execute(null);

        RaiseEvent(new RoutedEventArgs(ClosedEvent));
    }

    private void Refresh()
    {
        var s = Severity;
        PseudoClasses.Set(":informational", s == InfoBarSeverity.Informational);
        PseudoClasses.Set(":success", s == InfoBarSeverity.Success);
        PseudoClasses.Set(":warning", s == InfoBarSeverity.Warning);
        PseudoClasses.Set(":error", s == InfoBarSeverity.Error);

        Glyph = s switch
        {
            InfoBarSeverity.Success => Symbol.Completed,
            InfoBarSeverity.Warning => Symbol.Warning,
            InfoBarSeverity.Error => Symbol.Error,
            _ => Symbol.Info,
        };
    }
}

/// <summary>InfoBar 的四个级别：informational / success / warning / error。</summary>
public enum InfoBarSeverity
{
    Informational,
    Success,
    Warning,
    Error,
}
