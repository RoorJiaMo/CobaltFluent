using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Cobalt.Fluent;
using Cobalt.Fluent.Controls;

// 打包闸口的运行时那一半：从**真正装上的包**里用这个库，而不是从项目引用。
//
// 项目引用和包引用不是一回事——AXAML 有没有真的编进 dll、XML 文档有没有进包、
// 依赖闭包对不对，只有装一遍才知道。
//
// 还有一件只有这里能测的事：整套回归测试把界面语言钉成了中文（见 TestApp），
// 所以「非中文 locale 拿到英文」这条默认路径在仓库里没有任何东西在跑。
// 这个探针在固定区域性下跑，正好走那一条。
//
// 一个使用方会写的最小应用：挂主题、放几个控件、量一下。
internal class ConsumerApp : Application
{
    public override void Initialize() => Styles.Add(new CobaltFluentTheme());
}

internal static class Program
{
    static int Main()
    {
        var bad = 0;
        void Check(bool ok, string what, string detail = "")
        {
            if (!ok) bad++;
            Console.WriteLine($"  [{(ok ? "OK" : "!!")}] {what}{(detail.Length > 0 ? "  " + detail : "")}");
        }

        AppBuilder.Configure<ConsumerApp>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = true })
            .SetupWithoutStarting();

        var app = Application.Current!;

        // 1) 主题里的 token 解析得到吗（AXAML 有没有真的编进 dll）
        Check(app.TryFindResource("SolidBackgroundFillColorBaseBrush", ThemeVariant.Light, out var brush)
              && brush is ISolidColorBrush, "主题资源可解析");
        Check(app.TryFindResource("SolidBackgroundFillColorBase", CobaltFluentTheme.HighContrastDark, out var hc)
              && hc is Color c && c.ToUInt32() == 0xFF000000, "高对比度变体可用");

        // 2) 控件模板套上了吗
        var row = new ParameterRow { Label = "腔体温度", Unit = "°C", Setpoint = 85, Maximum = 200 };
        var win = new Window { Width = 900, Height = 400, Content = new StackPanel { Children = { row } } };
        win.Show();
        Dispatcher.UIThread.RunJobs();
        win.Measure(new Size(900, 400)); win.Arrange(new Rect(0, 0, 900, 400));
        Dispatcher.UIThread.RunJobs();

        Check(row.GetVisualDescendants().Any(), "控件模板已应用");
        Check(row.Bounds.Height >= 44, "参数行高度符合规格", $"{row.Bounds.Height}");

        // 3) 语言：默认跟 UI culture 走，且能整块换
        Console.WriteLine($"       当前 UI culture = '{System.Globalization.CultureInfo.CurrentUICulture.Name}'");
        Check(row.StateText == CobaltStrings.Current.Applied, "状态文字走 CobaltStrings", row.StateText ?? "<null>");

        CobaltStrings.Current = new CobaltStringsZhHans();
        Dispatcher.UIThread.RunJobs();
        Check(row.StateText == "已生效", "换成中文后状态文字跟着变", row.StateText ?? "<null>");

        // 4) XML 文档进包了吗（使用方的 IntelliSense 靠它）
        // IntelliSense 读的是**包缓存**里那份，不是使用方输出目录里的——
        // NuGet 从来不把 lib/*/x.xml 复制到 bin/。查错地方会得到一个假的失败。
        // 包缓存的位置**必须读 NUGET_PACKAGES**，不能写死 ~/.nuget/packages：
        // 闸口刻意用隔离缓存跑，写死的话这条会去翻上一次的旧包——
        // 在干净的机器上红，在开发机上却因为翻到了缓存而绿，方向还反着。
        var root = Environment.GetEnvironmentVariable("NUGET_PACKAGES")
                   ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                                   ".nuget", "packages");
        var version = typeof(ParameterRow).Assembly.GetName().Version!;
        var cache = Path.Combine(root, "cobalt.fluent",
            $"{version.Major}.{version.Minor}.{version.Build}", "lib", "net8.0", "Cobalt.Fluent.xml");
        Check(File.Exists(cache), "XML 文档随包安装（供 IntelliSense）", cache);

        Console.WriteLine(bad == 0 ? "使用方冒烟全部通过" : $"{bad} 项失败");
        return bad == 0 ? 0 : 1;
    }
}
