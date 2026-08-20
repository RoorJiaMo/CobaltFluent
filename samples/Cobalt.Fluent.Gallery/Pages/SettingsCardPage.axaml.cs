using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Cobalt.Fluent.Controls;

namespace Cobalt.Fluent.Gallery.Pages;

public partial class SettingsCardPage : UserControl
{
    // 增出来的行按顺序取，用完循环。图标和标题得跟着变，
    // 否则连加三行全长一个样，看不出末项换的是谁。
    private static readonly (Symbol Icon, string Header, string Description)[] Extras =
    [
        (Symbol.Folder,   "数据目录",   @"D:\Cobalt\Runs"),
        (Symbol.Document, "导出格式",   "CSV，UTF-8 带 BOM"),
        (Symbol.Pulse,    "心跳超时",   "超过 3 s 未收到帧即判离线"),
        (Symbol.Tune,     "整定向导",   "开机后允许自动 PID 整定"),
    ];

    private int _next;

    public SettingsCardPage()
    {
        AvaloniaXamlLoader.Load(this);

        var group = this.FindControl<SettingsGroup>("PlayGroup")!;
        var add = this.FindControl<Button>("AddRow")!;
        var remove = this.FindControl<Button>("RemoveRow")!;
        var log = this.FindControl<TextBlock>("PlayLog")!;

        void Report()
        {
            // 直接把 SettingsGroup 打上去的类念出来——圆角改了哪几个角，
            // 看这一行比数像素快。
            var rows = group.Children
                .OfType<SettingsCard>()
                .Select(c => $"{c.Header}：{Tag(c)}");

            log.Text = $"{group.Children.Count} 项 · " + string.Join(" / ", rows);

            // 只剩一项时再删就空了，空的 SettingsGroup 没什么可看的。
            remove.IsEnabled = group.Children.Count > 1;
        }

        add.Click += (_, _) =>
        {
            var (icon, header, description) = Extras[_next++ % Extras.Length];
            group.Children.Add(new SettingsCard
            {
                Icon = icon,
                Header = header,
                Description = description,
                Content = new Button { Content = "配置" },
            });
            Report();
        };

        remove.Click += (_, _) =>
        {
            if (group.Children.Count <= 1) return;
            group.Children.RemoveAt(group.Children.Count - 1);
            Report();
        };

        Report();
    }

    private static string Tag(SettingsCard card) =>
        card.Classes.FirstOrDefault(c => c.StartsWith("settings-")) ?? "（未打类）";
}
