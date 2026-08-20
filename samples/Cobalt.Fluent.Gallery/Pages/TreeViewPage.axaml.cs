using System.Collections;

using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Cobalt.Fluent.Gallery.Pages;

public partial class TreeViewPage : UserControl
{
    public TreeViewPage()
    {
        AvaloniaXamlLoader.Load(this);

        // 状态那一排的 hover 格：一个窗口里指针只能停在一处，想把这一档冻住
        // 只能直接按住伪类。出来的仍是 ControlTheme 里那条
        // ^:pointerover:not(:selected)，不是照着画的假底——主题写错了这里立刻露馅。
        ((IPseudoClasses)this.FindControl<TreeViewItem>("HoverSample")!.Classes)
            .Set(":pointerover", true);

        var tree = this.FindControl<TreeView>("PlayTree")!;
        var pathText = this.FindControl<TextBlock>("PathText")!;

        // 父子关系照声明的结构自己存一份：收起的那一级容器还没生成，
        // 顺着逻辑树往上找会断在半路。
        var parents = new Dictionary<TreeViewItem, TreeViewItem>();
        foreach (var item in Flatten(tree.Items))
            foreach (var child in item.Items.OfType<TreeViewItem>())
                parents[child] = item;

        void ShowPath()
        {
            if (tree.SelectedItem is not TreeViewItem selected)
            {
                pathText.Text = "选中路径：（未选）";
                return;
            }

            var names = new List<string>();
            for (var node = selected; node is not null; node = parents.GetValueOrDefault(node))
                names.Insert(0, node.Tag as string ?? "?");

            pathText.Text = "选中路径：" + string.Join(" / ", names);
        }

        tree.SelectionChanged += (_, _) => ShowPath();
        // XAML 里写的 IsSelected 要等容器备好才同步进 TreeView，
        // 首帧之前问 SelectedItem 还是 null。
        tree.Loaded += (_, _) => ShowPath();

        this.FindControl<Button>("ExpandAll")!.Click += (_, _) => SetExpanded(true);
        this.FindControl<Button>("CollapseAll")!.Click += (_, _) => SetExpanded(false);

        void SetExpanded(bool expanded)
        {
            foreach (var item in Flatten(tree.Items))
                item.IsExpanded = expanded;
        }
    }

    /// <summary>
    /// 深度优先摊平整棵树。走声明出来的 Items 而不是逻辑树 ——
    /// 收起状态下子节点的容器还没实例化，逻辑树上根本看不到它们。
    /// </summary>
    private static IEnumerable<TreeViewItem> Flatten(IEnumerable items)
    {
        foreach (var candidate in items)
        {
            if (candidate is not TreeViewItem item) continue;

            yield return item;
            foreach (var child in Flatten(item.Items)) yield return child;
        }
    }
}
