using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;

namespace Cobalt.Fluent.Controls;

/// <summary>面包屑。最后一项是当前位置，不可点。</summary>
public class BreadcrumbBar : ItemsControl
{
    public static readonly RoutedEvent<BreadcrumbClickedEventArgs> ItemClickedEvent =
        RoutedEvent.Register<BreadcrumbBar, BreadcrumbClickedEventArgs>(
            nameof(ItemClicked), RoutingStrategies.Bubble);

    public event EventHandler<BreadcrumbClickedEventArgs>? ItemClicked
    {
        add => AddHandler(ItemClickedEvent, value);
        remove => RemoveHandler(ItemClickedEvent, value);
    }

    protected override Control CreateContainerForItemOverride(
        object? item, int index, object? recycleKey) => new BreadcrumbItem();

    protected override bool NeedsContainerOverride(
        object? item, int index, out object? recycleKey)
    {
        recycleKey = null;
        return item is not BreadcrumbItem;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        RefreshStates();
    }

    /// <summary>最后一项标记为当前位置。</summary>
    public void RefreshStates()
    {
        var items = Items.OfType<BreadcrumbItem>().ToList();
        for (var i = 0; i < items.Count; i++)
        {
            items[i].IsCurrent = i == items.Count - 1;
            items[i].Index = i;
        }
    }

    internal void RaiseItemClicked(BreadcrumbItem item) =>
        RaiseEvent(new BreadcrumbClickedEventArgs(ItemClickedEvent, item.Index, item.Content));
}

public sealed class BreadcrumbClickedEventArgs(RoutedEvent routedEvent, int index, object? item)
    : RoutedEventArgs(routedEvent)
{
    public int Index { get; } = index;

    public object? Item { get; } = item;
}

/// <summary>面包屑里的一段。</summary>
[PseudoClasses(":current")]
public class BreadcrumbItem : ContentControl
{
    public static readonly StyledProperty<bool> IsCurrentProperty =
        AvaloniaProperty.Register<BreadcrumbItem, bool>(nameof(IsCurrent));

    public bool IsCurrent
    {
        get => GetValue(IsCurrentProperty);
        set => SetValue(IsCurrentProperty, value);
    }

    internal int Index { get; set; }

    static BreadcrumbItem()
    {
        IsCurrentProperty.Changed.AddClassHandler<BreadcrumbItem>(
            (x, e) => x.PseudoClasses.Set(":current", e.NewValue is true));
    }

    protected override void OnPointerReleased(Avalonia.Input.PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);

        // 当前位置不可点：点自己没有意义，还会造成「我是不是没点中」的困惑
        if (IsCurrent) return;

        (this.Parent as BreadcrumbBar
         ?? this.GetLogicalAncestors().OfType<BreadcrumbBar>().FirstOrDefault())
            ?.RaiseItemClicked(this);
    }
}
