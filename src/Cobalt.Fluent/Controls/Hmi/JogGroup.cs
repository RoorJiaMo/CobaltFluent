using Avalonia.Controls;
using Avalonia.Layout;

namespace Cobalt.Fluent.Controls;

/// <summary>
/// 成对/成组的点动按钮容器（正转-反转、开阀-关阀）。
/// 存在的意义只有一个：让相接处不圆、不双线。
///
/// 实现上给子按钮打 <c>jog-first</c> / <c>jog-last</c> / <c>jog-only</c> 三个类，
/// 圆角规则写在 JogButton 的 ControlTheme 里。
/// 不用 <c>:nth-child</c> 选择器是因为 Avalonia 的 ControlTheme 里不允许出现子代选择器。
/// </summary>
public class JogGroup : StackPanel
{
    private const string First = "jog-first";
    private const string Last = "jog-last";
    private const string Only = "jog-only";

    public JogGroup()
    {
        Orientation = Orientation.Horizontal;
        Children.CollectionChanged += (_, _) => Retag();
    }

    protected override void OnAttachedToVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Retag();
    }

    private void Retag()
    {
        var visible = Children.Where(c => c.IsVisible).ToList();

        foreach (var child in Children)
            child.Classes.RemoveAll([First, Last, Only]);

        if (visible.Count == 0) return;

        if (visible.Count == 1)
        {
            visible[0].Classes.Add(Only);
            return;
        }

        visible[0].Classes.Add(First);
        visible[^1].Classes.Add(Last);
    }
}
