using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Markup.Xaml;
using Cobalt.Fluent.Controls;

namespace Cobalt.Fluent.Gallery.Pages;

public partial class AlarmBannerPage : UserControl
{
    public AlarmBannerPage()
    {
        AvaloniaXamlLoader.Load(this);

        var stamp = new DateTime(2026, 8, 20, 14, 32, 5);
        foreach (var banner in this.GetLogicalDescendants().OfType<AlarmBanner>())
            banner.Timestamp = stamp;

        var live = this.FindControl<AlarmBanner>("Live")!;
        var log = this.FindControl<TextBlock>("AckLog")!;
        live.Acknowledged += (_, _) =>
            log.Text = "已确认 · 呼吸停止，补充黄环，横幅保留——报警条件仍然存在。";
    }
}
