using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Cobalt.Fluent.Controls;

namespace Cobalt.Fluent.Gallery.Pages;

public partial class InfoBarPage : UserControl
{
    public InfoBarPage()
    {
        AvaloniaXamlLoader.Load(this);

        var bar = this.FindControl<InfoBar>("Closable")!;
        var restore = this.FindControl<Button>("Restore")!;

        // 关掉之后这条就永远没了，试玩区只能玩一次——给个回收按钮，
        // 顺便把「重开也是改 IsOpen」这件事摆出来：外面不该去动 IsVisible。
        bar.Closed += (_, _) => restore.IsEnabled = true;

        restore.Click += (_, _) =>
        {
            bar.IsOpen = true;
            restore.IsEnabled = false;
        };
    }
}
