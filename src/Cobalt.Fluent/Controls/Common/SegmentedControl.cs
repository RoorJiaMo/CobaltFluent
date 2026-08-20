using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace Cobalt.Fluent.Controls;

/// <summary>
/// 互斥模式选择。比 RadioButton 醒目，适合 手动 / 自动 / 维护 这种一屏常驻的模式切换。
///
/// 就是一个 <see cref="SelectingItemsControl"/>，SelectionMode=Single；
/// 外观差异全在 ControlTheme 里。
/// </summary>
public class SegmentedControl : SelectingItemsControl
{
    protected override Type StyleKeyOverride => typeof(SegmentedControl);

    protected override Control CreateContainerForItemOverride(
        object? item, int index, object? recycleKey) => new SegmentedItem();

    protected override bool NeedsContainerOverride(
        object? item, int index, out object? recycleKey)
    {
        recycleKey = null;
        return item is not SegmentedItem;
    }
}

/// <summary>SegmentedControl 里的一段。</summary>
public class SegmentedItem : ListBoxItem
{
    protected override Type StyleKeyOverride => typeof(SegmentedItem);
}
