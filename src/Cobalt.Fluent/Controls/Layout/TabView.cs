using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;

namespace Cobalt.Fluent.Controls;

/// <summary>
/// 浏览器式标签页：每个标签自带关闭按钮，选中的那个「浮」到内容区上。
///
/// 和 <see cref="TabControl"/> 的区别是语义：TabControl 的标签是**固定的视图切换**，
/// TabView 的标签是**用户开出来的文档**，数量不定、可关闭、可能很多。
/// </summary>
public class TabView : TabControl
{
    protected override Type StyleKeyOverride => typeof(TabView);

    public static readonly StyledProperty<bool> IsAddButtonVisibleProperty =
        AvaloniaProperty.Register<TabView, bool>(nameof(IsAddButtonVisible), true);

    public bool IsAddButtonVisible
    {
        get => GetValue(IsAddButtonVisibleProperty);
        set => SetValue(IsAddButtonVisibleProperty, value);
    }

    public static readonly StyledProperty<ICommand?> AddCommandProperty =
        AvaloniaProperty.Register<TabView, ICommand?>(nameof(AddCommand));

    public ICommand? AddCommand
    {
        get => GetValue(AddCommandProperty);
        set => SetValue(AddCommandProperty, value);
    }

    public static readonly RoutedEvent<TabCloseRequestedEventArgs> TabCloseRequestedEvent =
        RoutedEvent.Register<TabView, TabCloseRequestedEventArgs>(
            nameof(TabCloseRequested), RoutingStrategies.Bubble);

    /// <summary>某个标签请求关闭。是否真的移除由使用方决定（可能要先提示保存）。</summary>
    public event EventHandler<TabCloseRequestedEventArgs>? TabCloseRequested
    {
        add => AddHandler(TabCloseRequestedEvent, value);
        remove => RemoveHandler(TabCloseRequestedEvent, value);
    }

    protected override Control CreateContainerForItemOverride(
        object? item, int index, object? recycleKey) => new TabViewItem();

    protected override bool NeedsContainerOverride(
        object? item, int index, out object? recycleKey)
    {
        recycleKey = null;
        return item is not TabViewItem;
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        if (e.NameScope.Find<Button>("PART_AddButton") is { } add)
        {
            add.Click += (_, _) =>
            {
                if (AddCommand?.CanExecute(null) == true)
                    AddCommand.Execute(null);
            };
        }
    }

    internal void RequestClose(TabViewItem item) =>
        RaiseEvent(new TabCloseRequestedEventArgs(TabCloseRequestedEvent, item));
}

public sealed class TabCloseRequestedEventArgs(RoutedEvent routedEvent, TabViewItem tab)
    : RoutedEventArgs(routedEvent)
{
    public TabViewItem Tab { get; } = tab;
}

/// <summary>TabView 里的一个标签。</summary>
[PseudoClasses(":closable")]
public class TabViewItem : TabItem
{
    private Button? _closeButton;

    protected override Type StyleKeyOverride => typeof(TabViewItem);

    public static readonly StyledProperty<bool> IsClosableProperty =
        AvaloniaProperty.Register<TabViewItem, bool>(nameof(IsClosable), true);

    public bool IsClosable
    {
        get => GetValue(IsClosableProperty);
        set => SetValue(IsClosableProperty, value);
    }

    public static readonly StyledProperty<Symbol> IconProperty =
        AvaloniaProperty.Register<TabViewItem, Symbol>(nameof(Icon));

    public Symbol Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    static TabViewItem()
    {
        IsClosableProperty.Changed.AddClassHandler<TabViewItem>(
            (x, e) => x.PseudoClasses.Set(":closable", e.NewValue is true));
    }

    public TabViewItem() => PseudoClasses.Set(":closable", true);

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        if (_closeButton is not null) _closeButton.Click -= OnCloseClicked;
        _closeButton = e.NameScope.Find<Button>("PART_Close");
        if (_closeButton is not null) _closeButton.Click += OnCloseClicked;
    }

    private void OnCloseClicked(object? sender, RoutedEventArgs e) =>
        this.GetLogicalAncestors().OfType<TabView>().FirstOrDefault()?.RequestClose(this);
}
