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

        var modes = this.FindControl<ComboBox>("ModeBox")!;

        // 显式映射，不用 (SnapLayoutMode)SelectedIndex：下拉框里 Builtin 排第二
        // （那是这一页最想让人试的一项），而枚举里第二个是 System——
        // 靠序号强转的话，选「Builtin」会开出一个 System 窗口，而且看不出哪里错了。
        var order = new[]
        {
            SnapLayoutMode.Auto,
            SnapLayoutMode.Builtin,
            SnapLayoutMode.System,
            SnapLayoutMode.None,
        };

        open.Click += (_, _) => OpenDemoWindow(
            order[Math.Clamp(modes.SelectedIndex, 0, order.Length - 1)]);

        hint.Text = OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000)
            ? "本机是 Windows 11：Auto 会用系统那个面板。想看本库自绘的那个，选 Builtin。"
            : "本机不是 Windows 11：Auto 已经退到本库自绘的面板了，直接开就能看到。";
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

    private static void OpenDemoWindow(SnapLayoutMode mode)
    {
        var bar = new TitleBar
        {
            Icon = Symbol.Diagnostic,
            Title = "贴靠布局演示",
            SnapLayoutMode = mode,
        };
        var body = new TextBlock
        {
            Classes = { "note" },
            Margin = new Avalonia.Thickness(24),
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            Text = "这一整个窗口都没有系统标题栏，上面那一条是 fc:TitleBar。\n\n"
                   + "把指针停在最大化钮上不动约 0.4 秒，贴靠布局面板会弹出来。"
                   + "点其中一格，这个窗口就会摆到屏幕上对应的位置。"
                   + "再开一个演示窗口摆到另一格，就能看出两个窗口是不是严丝合缝。\n\n"
                   + "拖住标题栏空白处可以移窗，双击最大化，Windows 上右键还有系统菜单——"
                   + "这些由 shell 提供，控件一行代码都没写。",
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

        // 开完把实际生效的模式写进标题：选了 System 却跑在 Linux 上会退成 None，
        // 不写出来的话使用者只会看到「怎么没反应」。
        window.Title = $"贴靠布局演示 —— {bar.EffectiveSnapLayoutMode}";
    }
}
