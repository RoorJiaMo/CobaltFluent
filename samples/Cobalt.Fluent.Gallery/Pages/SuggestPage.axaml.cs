using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Cobalt.Fluent.Controls;

namespace Cobalt.Fluent.Gallery.Pages;

public partial class SuggestPage : UserControl
{
    private static readonly string[] Recipes =
    [
        "PES-4CH 标准流程",
        "PES 结晶 A",
        "PES 高温变体",
        "结晶 B 慢降温",
        "清洗流程 CIP-1",
    ];

    private static readonly string[] TrailPath =
    [
        "配方库", "聚合反应", "PES-4CH 标准流程",
    ];

    public SuggestPage()
    {
        AvaloniaXamlLoader.Load(this);

        foreach (var name in new[] { "SpecSuggest", "SpecSuggestDisabled", "PlaySuggest" })
            this.FindControl<AutoCompleteBox>(name)!.ItemsSource = Recipes;

        WireSuggest();
        WireMode();
        WireTrail();
    }

    private void WireSuggest()
    {
        var box = this.FindControl<AutoCompleteBox>("PlaySuggest")!;
        box.ItemTemplate = HighlightTemplate(null);

        // 每次输入换一张新模板，而不是指望条目自己重画：
        // 建议列表是 ListBox，容器会复用，同一条数据留在同一个容器里时
        // ItemTemplate 不会再跑一遍，高亮就停在上一次的匹配段上。
        box.PropertyChanged += (_, e) =>
        {
            if (e.Property == AutoCompleteBox.TextProperty)
                box.ItemTemplate = HighlightTemplate(e.NewValue as string);
        };
    }

    private void WireMode()
    {
        var picker = this.FindControl<SegmentedControl>("ModePicker")!;
        var label = this.FindControl<TextBlock>("ModeText")!;

        picker.SelectionChanged += (_, _) => Show();
        Show();

        void Show() =>
            label.Text = "当前模式：" +
                ((picker.SelectedItem as SegmentedItem)?.Content as string ?? "未选");
    }

    private void WireTrail()
    {
        var bar = this.FindControl<BreadcrumbBar>("PlayTrail")!;
        var reset = this.FindControl<Button>("TrailReset")!;

        // 点第 i 段 = 回到那一层，它后面的层级作废。末级不会发这个事件。
        bar.ItemClicked += (_, e) => Rebuild(e.Index + 1);
        reset.Click += (_, _) => Rebuild(TrailPath.Length);
        Rebuild(TrailPath.Length);

        void Rebuild(int depth)
        {
            bar.Items.Clear();
            for (var i = 0; i < depth; i++)
                bar.Items.Add(new BreadcrumbItem { Content = TrailPath[i] });

            // 「谁是末级」是控件按顺序推出来的，不是数据里带的。
            // 改完 Items 必须自己再推一次，否则加粗和分隔符都停在上一次的末级上。
            bar.RefreshStates();
        }
    }

    /// <summary>
    /// 建议条目模板。匹配段用 Run 单独拼一段出来染色，
    /// Avalonia 没有富文本控件，
    /// 也不需要——Inlines 就是干这个的。
    /// </summary>
    private static IDataTemplate HighlightTemplate(string? query) =>
        new FuncDataTemplate<string>((item, _) =>
        {
            var block = new TextBlock { VerticalAlignment = VerticalAlignment.Center };

            var hit = string.IsNullOrEmpty(query)
                ? -1
                : item.IndexOf(query, StringComparison.CurrentCultureIgnoreCase);

            if (hit < 0)
            {
                block.Text = item;
                return block;
            }

            var length = query!.Length;
            block.Inlines =
            [
                new Run(item[..hit]),
                Accent(new Run(item.Substring(hit, length))),
                new Run(item[(hit + length)..]),
            ];
            return block;
        });

    /// <summary>
    /// 主题色从 Application 现取。模板每次输入都重建，所以取一次就够；
    /// 明暗切换时下一次输入自然换过来。查资源必须带主题变体——
    /// token 都在 ThemeDictionaries 里，不带的重载静默查不到。
    /// </summary>
    private static Run Accent(Run run)
    {
        run.FontWeight = FontWeight.SemiBold;

        if (Application.Current is { } app &&
            app.TryFindResource("AccentFillColorDefaultBrush", app.ActualThemeVariant, out var res) &&
            res is IBrush brush)
        {
            run.Foreground = brush;
        }

        return run;
    }
}
