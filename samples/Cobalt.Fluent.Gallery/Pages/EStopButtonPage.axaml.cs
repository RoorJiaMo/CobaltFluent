using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Cobalt.Fluent.Controls;

namespace Cobalt.Fluent.Gallery.Pages;

public partial class EStopButtonPage : UserControl
{
    public EStopButtonPage()
    {
        AvaloniaXamlLoader.Load(this);

        var live = this.FindControl<EStopButton>("Live")!;
        var log = this.FindControl<TextBlock>("Log")!;

        live.Engaged += (_, _) =>
            log.Text = $"已触发 · 主回路已切断 · {DateTime.Now:HH:mm:ss} —— 长按钮体 1.2 秒复位。"
                     + (string.IsNullOrEmpty(live.HardwareLocationHint)
                         ? ""
                         : $"（{live.HardwareLocationHint}）");

        live.Released += (_, _) =>
            log.Text = $"已复位 · {DateTime.Now:HH:mm:ss} —— 复位不等于恢复运行，还要显式启动。";
    }
}
