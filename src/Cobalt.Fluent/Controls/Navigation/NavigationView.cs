using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Metadata;
using Cobalt.Fluent.Automation;

namespace Cobalt.Fluent.Controls;

/// <summary>导航面板形态。</summary>
public enum NavigationViewPaneDisplayMode
{
    /// <summary>展开，280 宽，图标 + 文字。</summary>
    Left,

    /// <summary>收起，48 宽，只有图标。</summary>
    LeftCompact,
}

/// <summary>
/// 左侧导航。
///
/// 项高 40，对齐 WinUI。
/// 选中项左侧是 3×16 的指示条，不是整行变色。
/// </summary>
[PseudoClasses(":compact")]
public class NavigationView : TemplatedControl
{
    public static readonly StyledProperty<NavigationViewPaneDisplayMode> PaneDisplayModeProperty =
        AvaloniaProperty.Register<NavigationView, NavigationViewPaneDisplayMode>(
            nameof(PaneDisplayMode), NavigationViewPaneDisplayMode.Left);

    public NavigationViewPaneDisplayMode PaneDisplayMode
    {
        get => GetValue(PaneDisplayModeProperty);
        set => SetValue(PaneDisplayModeProperty, value);
    }

    public static readonly StyledProperty<double> OpenPaneLengthProperty =
        AvaloniaProperty.Register<NavigationView, double>(nameof(OpenPaneLength), 280d);

    public double OpenPaneLength
    {
        get => GetValue(OpenPaneLengthProperty);
        set => SetValue(OpenPaneLengthProperty, value);
    }

    public static readonly StyledProperty<double> CompactPaneLengthProperty =
        AvaloniaProperty.Register<NavigationView, double>(nameof(CompactPaneLength), 48d);

    public double CompactPaneLength
    {
        get => GetValue(CompactPaneLengthProperty);
        set => SetValue(CompactPaneLengthProperty, value);
    }

    private double _paneLength = 280;

    public static readonly DirectProperty<NavigationView, double> PaneLengthProperty =
        AvaloniaProperty.RegisterDirect<NavigationView, double>(nameof(PaneLength), o => o._paneLength);

    /// <summary>当前实际面板宽度。模板绑它。</summary>
    public double PaneLength
    {
        get => _paneLength;
        private set => SetAndRaise(PaneLengthProperty, ref _paneLength, value);
    }

    public static readonly StyledProperty<AvaloniaList<Control>> MenuItemsProperty =
        AvaloniaProperty.Register<NavigationView, AvaloniaList<Control>>(nameof(MenuItems));

    [Content]
    public AvaloniaList<Control> MenuItems
    {
        get => GetValue(MenuItemsProperty);
        set => SetValue(MenuItemsProperty, value);
    }

    /// <summary>面板底部的项（设置、帮助这类）。</summary>
    public static readonly StyledProperty<AvaloniaList<Control>> FooterItemsProperty =
        AvaloniaProperty.Register<NavigationView, AvaloniaList<Control>>(nameof(FooterItems));

    public AvaloniaList<Control> FooterItems
    {
        get => GetValue(FooterItemsProperty);
        set => SetValue(FooterItemsProperty, value);
    }

    public static readonly StyledProperty<object?> HeaderProperty =
        AvaloniaProperty.Register<NavigationView, object?>(nameof(Header));

    public object? Header
    {
        get => GetValue(HeaderProperty);
        set => SetValue(HeaderProperty, value);
    }

    public static readonly StyledProperty<object?> ContentProperty =
        AvaloniaProperty.Register<NavigationView, object?>(nameof(Content));

    public object? Content
    {
        get => GetValue(ContentProperty);
        set => SetValue(ContentProperty, value);
    }

    public static readonly StyledProperty<NavigationViewItem?> SelectedItemProperty =
        AvaloniaProperty.Register<NavigationView, NavigationViewItem?>(
            nameof(SelectedItem), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    public NavigationViewItem? SelectedItem
    {
        get => GetValue(SelectedItemProperty);
        set => SetValue(SelectedItemProperty, value);
    }

    public static readonly RoutedEvent<RoutedEventArgs> SelectionChangedEvent =
        RoutedEvent.Register<NavigationView, RoutedEventArgs>(
            nameof(SelectionChanged), RoutingStrategies.Bubble);

    public event EventHandler<RoutedEventArgs>? SelectionChanged
    {
        add => AddHandler(SelectionChangedEvent, value);
        remove => RemoveHandler(SelectionChangedEvent, value);
    }

    static NavigationView()
    {
        PaneDisplayModeProperty.Changed.AddClassHandler<NavigationView>((x, _) => x.Refresh());
        OpenPaneLengthProperty.Changed.AddClassHandler<NavigationView>((x, _) => x.Refresh());
        CompactPaneLengthProperty.Changed.AddClassHandler<NavigationView>((x, _) => x.Refresh());
        SelectedItemProperty.Changed.AddClassHandler<NavigationView>((x, e) => x.OnSelectionChanged(e));
    }

    public NavigationView()
    {
        MenuItems = [];
        FooterItems = [];
        Refresh();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        foreach (var item in MenuItems.Concat(FooterItems).OfType<NavigationViewItem>())
            item.Owner = this;

        Refresh();
    }

    private void OnSelectionChanged(AvaloniaPropertyChangedEventArgs e)
    {
        if (e.OldValue is NavigationViewItem old) old.IsSelected = false;
        if (e.NewValue is NavigationViewItem now) now.IsSelected = true;
        RaiseEvent(new RoutedEventArgs(SelectionChangedEvent));
    }

    internal void Select(NavigationViewItem item) => SelectedItem = item;

    private void Refresh()
    {
        var compact = PaneDisplayMode == NavigationViewPaneDisplayMode.LeftCompact;
        PseudoClasses.Set(":compact", compact);
        PaneLength = compact ? CompactPaneLength : OpenPaneLength;

        // 项和分组标题的紧凑态得由面板转达 —— 它们不是模板的一部分，
        // ControlTheme 里也不允许写后代选择器，所以只能在这里逐个设。
        foreach (var item in MenuItems.Concat(FooterItems))
        {
            switch (item)
            {
                case NavigationViewItem navItem:
                    ((IPseudoClasses)navItem.Classes).Set(":compact", compact);
                    break;
                case NavigationViewItemHeader header:
                    header.IsVisible = !compact;
                    break;
            }
        }
    }

    /// <summary>见 <see cref="Cobalt.Fluent.Automation.NavigationViewAutomationPeer"/>。</summary>
    protected override AutomationPeer OnCreateAutomationPeer() => new NavigationViewAutomationPeer(this);
}

/// <summary>导航项。</summary>
[PseudoClasses(":selected", ":compact")]
public class NavigationViewItem : ContentControl
{
    internal NavigationView? Owner { get; set; }

    public static readonly StyledProperty<Symbol> IconProperty =
        AvaloniaProperty.Register<NavigationViewItem, Symbol>(nameof(Icon));

    public Symbol Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    public static readonly StyledProperty<bool> IsSelectedProperty =
        AvaloniaProperty.Register<NavigationViewItem, bool>(nameof(IsSelected));

    public bool IsSelected
    {
        get => GetValue(IsSelectedProperty);
        set => SetValue(IsSelectedProperty, value);
    }

    /// <summary>右侧徽章。放报警计数这类。</summary>
    public static readonly StyledProperty<object?> BadgeProperty =
        AvaloniaProperty.Register<NavigationViewItem, object?>(nameof(Badge));

    public object? Badge
    {
        get => GetValue(BadgeProperty);
        set => SetValue(BadgeProperty, value);
    }

    static NavigationViewItem()
    {
        IsSelectedProperty.Changed.AddClassHandler<NavigationViewItem>(
            (x, e) => x.PseudoClasses.Set(":selected", e.NewValue is true));
    }

    protected override void OnPointerReleased(Avalonia.Input.PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (!IsEffectivelyEnabled) return;

        (Owner ?? this.GetLogicalAncestors().OfType<NavigationView>().FirstOrDefault())?.Select(this);
    }
}

/// <summary>导航分组标题。紧凑模式下隐藏。</summary>
public class NavigationViewItemHeader : ContentControl
{
}

/// <summary>导航分隔线。</summary>
public class NavigationViewItemSeparator : TemplatedControl
{
    /// <summary>装饰性元素，主动退出自动化树。见 <see cref="Cobalt.Fluent.Automation.DecorativeAutomationPeer"/>。</summary>
    protected override AutomationPeer OnCreateAutomationPeer() => new DecorativeAutomationPeer(this);
}
