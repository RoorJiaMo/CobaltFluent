using System.Collections;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.VisualTree;

namespace Cobalt.Fluent.Gallery.Pages;

public partial class MenuFlyoutPage : UserControl
{
    public MenuFlyoutPage()
    {
        AvaloniaXamlLoader.Load(this);

        WireActionButton();
        WireContextMenu();
    }

    private void WireActionButton()
    {
        var button = this.FindControl<DropDownButton>("ActionButton")!;
        var log = this.FindControl<TextBlock>("ActionLog")!;

        log.Text = "点「操作」展开菜单。";

        // 菜单项挂在 Flyout 上，页面的视觉树里没有它们，FindControl 摸不到。
        // MenuFlyout.Items 是现成的集合，弹层开没开都能取。
        foreach (var item in Leaves(((MenuFlyout)button.Flyout!).Items))
        {
            var header = item.Header as string ?? "";
            item.Click += (_, _) => log.Text = $"执行了「{header}」。菜单点完自己收，不用手动 Hide()。";
        }
    }

    private void WireContextMenu()
    {
        var list = this.FindControl<ListBox>("RecipeList")!;
        var log = this.FindControl<TextBlock>("ContextLog")!;

        log.Text = "在任意一行上点右键。";

        // Avalonia 的 ListBox 右键不改选中项。菜单里的命令作用在「选中的那一行」上，
        // 不补这一句，右键 A 行弹出来的菜单删的是之前左键选的 B 行。
        // 走 Tunnel：ContextMenu 是在 PointerPressed 冒泡阶段弹的，
        // 等冒泡上来选中项已经晚了一拍。
        list.AddHandler(
            InputElement.PointerPressedEvent,
            (_, e) =>
            {
                if (!e.GetCurrentPoint(list).Properties.IsRightButtonPressed) return;
                if ((e.Source as Visual)?.FindAncestorOfType<ListBoxItem>() is { } row)
                    list.SelectedItem = row;
            },
            RoutingStrategies.Tunnel);

        foreach (var item in Leaves(list.ContextMenu!.Items))
        {
            var header = item.Header as string ?? "";
            item.Click += (_, _) =>
            {
                var target = (list.SelectedItem as ListBoxItem)?.Content as string ?? "（未选中）";
                log.Text = $"对「{target}」执行了「{header}」。";
            };
        }
    }

    /// <summary>
    /// 只收叶子项。带子菜单的父项（「导出数据」）点它本身只是展开，
    /// 给它接 Click 会在每次展开时白记一笔。
    /// </summary>
    private static IEnumerable<MenuItem> Leaves(IEnumerable items)
    {
        foreach (var raw in items)
        {
            if (raw is not MenuItem item) continue;

            var children = Leaves(item.Items).ToArray();
            if (children.Length == 0) yield return item;
            else foreach (var child in children) yield return child;
        }
    }
}
