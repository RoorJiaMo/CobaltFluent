using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Cobalt.Fluent.Controls;

namespace Cobalt.Fluent.Gallery.Pages;

public partial class ContentDialogPage : UserControl
{
    public ContentDialogPage()
    {
        AvaloniaXamlLoader.Load(this);

        var log = this.FindControl<TextBlock>("PlayLog")!;
        log.Text = "点按上方任意按钮，结果显示于此。";

        Wire("OpenDiscard", "DiscardDialog", result => result switch
        {
            ContentDialogResult.Primary => "保存并关闭 — 3 处改动写回 Recipe-042。",
            ContentDialogResult.Secondary => "放弃更改 — 改动已丢弃，配方恢复为磁盘上的版本。",
            _ => "取消 — 停留在编辑器中，改动保留（Esc 同样返回此结果）。",
        });

        Wire("OpenDelete", "DeleteDialog", result => result switch
        {
            ContentDialogResult.Primary => "已删除 Recipe-042 — 3 条排产记录失去配方来源。",
            _ => "取消 — 配方保留。",
        });

        void Wire(string buttonName, string dialogKey, Func<ContentDialogResult, string> describe)
        {
            var button = this.FindControl<Button>(buttonName)!;
            var dialog = (ContentDialog)this.FindResource(dialogKey)!;

            button.Click += async (_, _) =>
            {
                // 开着的时候关掉触发键：同一个实例重复 ShowAsync 拿到的是同一个 Task，
                // 连点两下会给同一次对话记两笔结果。
                button.IsEnabled = false;
                var result = await dialog.ShowAsync(this);
                button.IsEnabled = true;

                log.Text = describe(result);
            };
        }
    }
}
