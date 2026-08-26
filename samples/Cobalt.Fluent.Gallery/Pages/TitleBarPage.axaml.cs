using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.VisualTree;
using Cobalt.Fluent;
using Cobalt.Fluent.Controls;

namespace Cobalt.Fluent.Gallery.Pages;

public partial class TitleBarPage : UserControl
{
    public TitleBarPage()
    {
        AvaloniaXamlLoader.Load(this);

        var open = this.FindControl<Button>("OpenDemo")!;
        var hint = this.FindControl<TextBlock>("OpenHint")!;

        // 单窗口平台（嵌入式 framebuffer、移动端、浏览器）开不出第二个窗口。
        // 展柜里不留死按钮：开不出来就直接说开不出来，而不是按下去没反应。
        if (Avalonia.Application.Current?.ApplicationLifetime
            is not IClassicDesktopStyleApplicationLifetime)
        {
            open.IsEnabled = false;
            hint.Text = "本平台是单窗口的，开不出第二个窗口。TitleBar 本身仍然可用——"
                        + "只是贴靠布局是 Windows 独有的。";
            return;
        }

        open.Click += (_, _) => OpenDemoWindow();
    }

    protected override void OnAttachedToVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        // 拿一个临时实例来问能力：SupportsSnapLayouts 是按所在窗口算的，
        // 而这一页就住在展柜窗口里，问它等于问展柜窗口。
        var probe = this.FindControl<TitleBar>("Sample")!;
        var status = this.FindControl<TextBlock>("SnapStatus")!;

        WirePicker();

        status.Text = probe.SupportsSnapLayouts
            ? "本机支持贴靠布局：把指针停在最大化钮上，shell 会弹出布局面板。"
            : "本机不弹贴靠面板。三条前置条件里至少缺一条——"
              + $"Windows 11(22000+)：{OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000)}；"
              + "最大化钮可见、窗口可缩放请看所在窗口。"
              + "这不是控件坏了，是这个平台上本来就没有这个功能。";
    }

    /// <summary>
    /// 展柜里的面板只报选中的分区，不真的摆窗口——翻到这一页随手一点，
    /// 整个展柜窗口跑到屏幕角落去，那不是演示，那是事故。
    /// </summary>
    private void WirePicker()
    {
        var picker = this.FindControl<SnapLayoutPicker>("Picker")!;
        var status = this.FindControl<TextBlock>("PickerStatus")!;

        status.Text = picker.Layouts.Count == 0
            ? "这块屏幕上没有可用的贴靠布局——多半是拿不到屏幕信息（单窗口平台）。"
            : $"本机给了 {picker.Layouts.Count} 套布局。点一格看看它对应屏幕上的哪块像素。";

        picker.ZoneSelected += (_, e) =>
        {
            var rect = this.GetVisualRoot() is Window window
                ? WindowSnap.ZoneRectFor(window, e.Zone)
                : null;

            var name = CobaltStrings.Current.SnapZoneName(SnapGeometry.Classify(e.Zone), e.Zone);

            status.Text = rect is null
                ? $"选中「{name}」。拿不到屏幕信息，算不出像素矩形。"
                : $"选中「{name}」（{CobaltStrings.Current.SnapLayoutName(e.Layout.Kind)} 的第 "
                  + $"{e.Index + 1} 块）→ 屏幕上的 {rect.Value.X},{rect.Value.Y} "
                  + $"{rect.Value.Width}×{rect.Value.Height}。这就是窗口会去的那块像素。";
        };
    }

    private static void OpenDemoWindow()
    {
        var bar = new TitleBar { Icon = Symbol.Diagnostic, Title = "贴靠布局演示" };
        var body = new TextBlock
        {
            Classes = { "note" },
            Margin = new Avalonia.Thickness(24),
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            Text = "这一整个窗口都没有系统标题栏，上面那一条是 fc:TitleBar。\n\n"
                   + "Windows 11 上：把指针停在最大化钮上不动，shell 会弹出贴靠布局面板；"
                   + "拖住空白处可以移窗，双击最大化，右键出系统菜单——"
                   + "这些全部由 shell 提供，控件一行代码都没写。\n\n"
                   + "其余平台上：三个按钮走控件自己的 Click，行为一致，只是没有贴靠面板。",
        };

        var dock = new DockPanel();
        DockPanel.SetDock(bar, Dock.Top);
        dock.Children.Add(bar);
        dock.Children.Add(body);

        var window = new Window
        {
            Width = 720,
            Height = 420,
            Title = "贴靠布局演示",
            Content = dock,
        };

        // 三条窗口提示一次设齐。漏掉任何一条的表现都不一样，见页面上的说明。
        TitleBar.ApplyTo(window);
        window.Show();
    }
}
