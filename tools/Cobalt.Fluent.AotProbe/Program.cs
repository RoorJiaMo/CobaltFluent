using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Cobalt.Fluent;
using Cobalt.Fluent.Automation;
using Cobalt.Fluent.Controls;

namespace Cobalt.Fluent.AotProbe;

/// <summary>无头宿主。把整套主题挂上，测的就是真实的模板。</summary>
internal class ProbeApp : Application
{
    public override void Initialize() => Styles.Add(new CobaltFluentTheme());
}

internal static class Program
{
    private static int _failed;

    private static void Check(bool ok, string what, string detail = "")
    {
        if (!ok) _failed++;
        Console.WriteLine($"  [{(ok ? "OK" : "!!")}] {what}{(detail.Length > 0 ? "  " + detail : "")}");
    }

    private static int Main()
    {
        AppBuilder.Configure<ProbeApp>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = true })
            .SetupWithoutStarting();

        Console.WriteLine("主题变体");
        Variants();

        Console.WriteLine("自动化对等体");
        Peers();

        Console.WriteLine("编译绑定");
        Bindings();

        Console.WriteLine("自绘标题栏");
        Chrome();

        Console.WriteLine(_failed == 0 ? "AOT 探针全部通过" : $"AOT 探针 {_failed} 项失败");
        return _failed == 0 ? 0 : 1;
    }

    /// <summary>
    /// 四套变体都要能解析。自定义变体的字典键走 x:Static——
    /// 那条路径一旦被裁掉，应用在加载主题那一刻就炸，不是运行到某个页面才炸。
    /// </summary>
    private static void Variants()
    {
        var app = Application.Current!;

        foreach (var (variant, expect) in new (ThemeVariant, uint)[]
                 {
                     (ThemeVariant.Light, 0xFFF3F3F3),
                     (ThemeVariant.Dark, 0xFF202020),
                     (CobaltFluentTheme.HighContrastLight, 0xFFFFFFFF),
                     (CobaltFluentTheme.HighContrastDark, 0xFF000000),
                 })
        {
            var ok = app.TryFindResource("SolidBackgroundFillColorBase", variant, out var v)
                     && v is Color c && c.ToUInt32() == expect;
            Check(ok, $"{variant.Key} 的底色", $"= {(v as Color?)?.ToUInt32():X8}");
        }

        // 高对比度里出现半透明，说明有键漏写、静默回落到了继承来的那套。
        var translucent = 0;
        foreach (var variant in new[] { CobaltFluentTheme.HighContrastLight, CobaltFluentTheme.HighContrastDark })
            foreach (var key in new[]
                     {
                         "CardBackgroundFillColorDefault", "ControlFillColorDefault",
                         "SubtleFillColorSecondary", "LayerFillColorDefault",
                         "ControlStrokeColorDefault", "DividerStrokeColorDefault",
                     })
            {
                app.TryFindResource(key, variant, out var v);
                if (v is Color c && c.A is not (0 or 255)) translucent++;
            }

        Check(translucent == 0, "高对比度里没有半透明", $"半透明键数 {translucent}");

        var resolved = CobaltFluentTheme.Resolve(new PlatformColorValues
        {
            ThemeVariant = PlatformThemeVariant.Dark,
            ContrastPreference = ColorContrastPreference.High,
        });
        Check(ReferenceEquals(resolved, CobaltFluentTheme.HighContrastDark),
            "系统偏好映射", resolved.Key?.ToString() ?? "?");
    }

    private static void Peers()
    {
        var readout = new Readout { Label = "腔体温度", Unit = "°C", Value = 85.4, Format = "F1" };
        var peer = ControlAutomationPeer.CreatePeerForElement(readout);

        Check(peer is ReadoutAutomationPeer, "Readout 的对等体类型", peer.GetType().Name);
        Check(peer.GetName() == "腔体温度", "对等体名字", peer.GetName() ?? "<null>");
        Check(peer.GetItemType() == "°C", "对等体单位", peer.GetItemType() ?? "<null>");

        var stop = ControlAutomationPeer.CreatePeerForElement(new EStopButton());
        Check(stop is EStopButtonAutomationPeer, "EStopButton 的对等体类型", stop.GetType().Name);
        Check(ControlAutomationPeer.CreatePeerForElement(new Skeleton()) is DecorativeAutomationPeer,
            "装饰性元素退出自动化树");
    }

    /// <summary>
    /// 控件层里由反射绑定改成编译绑定的那几处。
    /// 裁剪之后绑定断掉不会抛异常——界面照样画出来，少的是那一处的联动。
    /// </summary>
    private static void Bindings()
    {
        var combo = new ComboBox { Width = 260, ItemsSource = new[] { "甲", "乙" } };
        var spin = new NumericUpDown { AllowSpin = false, ShowButtonSpinner = false };
        var text = new TextBox { Watermark = "请输入设定值", Width = 200 };

        var window = new Window
        {
            Width = 900,
            Height = 400,
            Content = new StackPanel { Children = { combo, spin, text } },
        };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        window.Measure(new Size(900, 400));
        window.Arrange(new Rect(0, 0, 900, 400));
        Dispatcher.UIThread.RunJobs();

        var popup = combo.GetVisualDescendants().OfType<Popup>().FirstOrDefault(p => p.Name == "PART_Popup");
        Check(popup is not null && Math.Abs(popup.MinWidth - 260) < 1,
            "下拉面板宽度跟着框走", $"MinWidth={popup?.MinWidth}");

        var spinner = spin.GetVisualDescendants().OfType<ButtonSpinner>().FirstOrDefault();
        Check(spinner is { AllowSpin: false }, "$parent 绑定转达 AllowSpin", $"AllowSpin={spinner?.AllowSpin}");

        var watermark = text.GetVisualDescendants().OfType<TextBlock>()
            .FirstOrDefault(t => t.Name == "PART_Watermark");
        Check(watermark is { IsVisible: true }, "空输入框显示水印");

        text.Text = "85";
        Dispatcher.UIThread.RunJobs();
        Check(watermark is { IsVisible: false }, "有内容后水印消失");
    }

    /// <summary>
    /// 标题栏的非客户区命中角色。
    ///
    /// 这一节在 AOT 闸口里而不是只在单测里，是因为它依赖两条裁剪敏感的路径：
    /// 模板里用 StaticResource 取 TitleBarButton / TitleBarCloseButton 这两个
    /// ControlTheme，以及把 fc:SymbolIcon 当作 Button.Content 放进去。
    /// 这两条要是被裁掉，编译期没有任何信号，界面上表现为「按钮没有样式」
    /// 或者「贴靠布局不弹了」——而后者在非 Windows 上根本复现不出来。
    /// </summary>
    private static void Chrome()
    {
        var bar = new TitleBar { Title = "研磨工位", Icon = Symbol.Diagnostic };
        var window = new Window { Width = 900, Height = 400, Content = bar };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        Button? Btn(string name) => bar.GetVisualDescendants()
            .OfType<Button>().FirstOrDefault(b => b.Name == name);

        var maximize = Btn("PART_Maximize");
        Check(maximize is not null && Win32Properties.GetNonClientHitTestResult(maximize)
              == Win32Properties.Win32HitTestValue.MaxButton,
            "最大化钮标成 MaxButton（贴靠布局本身）");
        Check(Btn("PART_Close") is { } close
              && Win32Properties.GetNonClientHitTestResult(close)
              == Win32Properties.Win32HitTestValue.Close,
            "关闭钮标成 Close");

        var caption = bar.GetVisualDescendants().OfType<Panel>()
            .FirstOrDefault(v => v.Name == "PART_Caption");
        Check(caption is not null && Win32Properties.GetNonClientHitTestResult(caption)
              == Win32Properties.Win32HitTestValue.Caption,
            "空白处标成 Caption");

        // 字形是矢量路径，不是字体码点——裁剪之后这张表还得在。
        var glyph = bar.GetVisualDescendants().OfType<SymbolIcon>()
            .FirstOrDefault(v => v.Name == "PART_MaximizeGlyph");
        Check(glyph is { Symbol: Symbol.Maximize }, "窗口按钮字形还在", $"{glyph?.Symbol}");

        window.WindowState = WindowState.Maximized;
        Dispatcher.UIThread.RunJobs();
        Check(glyph is { Symbol: Symbol.Restore }, "最大化后换成还原字形", $"{glyph?.Symbol}");
    }
}
