using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Cobalt.Fluent;
using Cobalt.Fluent.Controls;
using Cobalt.Fluent.Gallery;
using Cobalt.Fluent.Gallery.Infrastructure;

namespace Cobalt.Fluent.Shots;

/// <summary>
/// 无头渲染截图。把展柜的每一页渲染成 PNG，用来肉眼验收，也是 CI 里「渲染不出来就是错」那道关。
///
///   dotnet run --project tools/Cobalt.Fluent.Shots -- &lt;输出目录&gt; [章节名过滤] [light|dark|both]
///
/// 章节名过滤给 <c>*</c> 就是全部；给 <c>shell</c> 渲染整个展柜窗口；
/// 给 <c>shell:Readout</c> 渲染停在指定章节的展柜窗口（截封面图用）；
/// 给 <c>srcview:Button</c> 渲染打开了「本页源码」覆层的展柜窗口。
///
/// 没有这一步的话，「1:1」就只能靠读代码判断。
/// </summary>
internal static class Program
{
    private static int Main(string[] args)
    {
        var outDir = args.Length > 0 ? args[0] : "artifacts/shots";
        var filter = args.Length > 1 && args[1] != "*" ? args[1] : null;
        var themes = args.Length > 2 ? args[2] : "light";

        Directory.CreateDirectory(outDir);

        // 把界面语言钉成中文。展柜的文案是中文的，控件层的措辞得跟它一致——
        // 不钉的话截图内容取决于跑它的机器的 locale：本地是中文、CI runner 是英文，
        // 同一次提交产出两套截图，而「渲染回归」这道闸口的前提正是渲染确定。
        CobaltStrings.Current = new CobaltStringsZhHans();

        AppBuilder.Configure<App>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
            .UseSkia()
            .WithInterFont()
            .SetupWithoutStarting();

        var variants = themes switch
        {
            "dark" => new[] { ThemeVariant.Dark },
            "both" => new[] { ThemeVariant.Light, ThemeVariant.Dark },
            _ => new[] { ThemeVariant.Light },
        };

        // 自绘标题栏 + 弹开的贴靠面板。这一张是「应该看到什么」的基准图：
        // 面板不出来的时候，先和它对一下，就知道是模式没选对还是真坏了。
        if (filter == "snap")
        {
            foreach (var variant in variants)
            {
                Application.Current!.RequestedThemeVariant = variant;
                var suffix = variant == ThemeVariant.Dark ? "dark" : "light";

                var bar = new TitleBar
                {
                    Icon = Symbol.Diagnostic,
                    Title = "研磨工位 · 3 号线",
                    // 必须显式 Builtin：Auto 在 Windows 11 上会走系统面板，
                    // 而系统面板是 shell 画的，截不进我们的帧。
                    SnapLayoutMode = SnapLayoutMode.Builtin,
                };

                var body = new TextBlock
                {
                    Margin = new Thickness(24),
                    TextWrapping = TextWrapping.Wrap,
                    Text = "客户区。把指针停在最大化钮上约 0.4 秒，上面那个面板就会弹出来。",
                };

                var dock = new DockPanel();
                DockPanel.SetDock(bar, Dock.Top);
                dock.Children.Add(bar);
                dock.Children.Add(body);

                var window = new Window { Width = 900, Height = 420, Content = dock };
                TitleBar.ApplyTo(window);
                window.Show();
                Dispatcher.UIThread.RunJobs();

                bar.ShowSnapLayouts();
                Dispatcher.UIThread.RunJobs();

                window.Measure(new Size(900, 420));
                window.Arrange(new Rect(0, 0, 900, 420));
                Dispatcher.UIThread.RunJobs();

                if (!bar.IsSnapLayoutsOpen)
                {
                    Console.Error.WriteLine($"面板没弹出来（生效模式 {bar.EffectiveSnapLayoutMode}）。");
                    return 1;
                }

                // 面板住在窗口的覆盖层里，而覆盖层是后合成的。
                // 同一个进程里连开两个窗口时，不推一下渲染循环，
                // 第二张截出来的帧里面板是空的——控件本身是好的（布局尺寸都对），
                // 空的只是那一帧。
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                Dispatcher.UIThread.RunJobs();

                var frame = window.CaptureRenderedFrame();
                if (frame is null)
                {
                    Console.Error.WriteLine("[空帧] snap");
                    return 1;
                }

                var path = Path.Combine(outDir, $"_snap.{suffix}.png");
                frame.Save(path);
                Console.WriteLine($"  {path}（生效模式 {bar.EffectiveSnapLayoutMode}）");
                window.Close();
            }

            return 0;
        }

        // 单独渲染整个展柜窗口（目录 + 顶栏 + 内容区），用来验收外壳本身。
        // 写成 shell:章节名 可以指定停在哪一页，例如 shell:Readout —— 截封面图用。
        if (filter is not null
            && (filter == "shell"
                || filter.StartsWith("shell:", StringComparison.Ordinal)
                || filter.StartsWith("srcview:", StringComparison.Ordinal)))
        {
            var openSource = filter.StartsWith("srcview:", StringComparison.Ordinal);
            var wanted = filter.Contains(':')
                ? filter[(filter.IndexOf(':') + 1)..]
                : null;

            foreach (var variant in variants)
            {
                Application.Current!.RequestedThemeVariant = variant;
                var suffix = variant == ThemeVariant.Dark ? "dark" : "light";

                var shell = new MainWindow { Width = 1440, Height = 900 };
                shell.Show();
                Dispatcher.UIThread.RunJobs();

                if (wanted is not null)
                {
                    var toc = shell.FindControl<ListBox>("Toc")!;
                    var target = SectionRegistry.Sections.FirstOrDefault(
                        x => x.Title.Contains(wanted, StringComparison.OrdinalIgnoreCase));
                    if (target is null)
                    {
                        Console.Error.WriteLine($"没有匹配「{wanted}」的章节。");
                        return 1;
                    }
                    toc.SelectedItem = target;
                    Dispatcher.UIThread.RunJobs();

                    if (openSource)
                    {
                        shell.ShowSourceForCurrentSection();
                        Dispatcher.UIThread.RunJobs();
                    }
                }

                shell.Measure(new Size(1440, 900));
                shell.Arrange(new Rect(0, 0, 1440, 900));
                Dispatcher.UIThread.RunJobs();

                var shot = shell.CaptureRenderedFrame();
                if (shot is null)
                {
                    Console.Error.WriteLine("[空帧] shell");
                    return 1;
                }

                var name = wanted is null ? "_shell"
                    : openSource ? $"_srcview-{wanted}"
                    : $"_shell-{wanted}";
                var file = Path.Combine(outDir, $"{name}.{suffix}.png");
                shot.Save(file);
                Console.WriteLine($"  {file}");
                shell.Close();
            }

            return 0;
        }

        var sections = SectionRegistry.Sections
            .Where(s => filter is null || s.Title.Contains(filter, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (sections.Count == 0)
        {
            Console.Error.WriteLine($"没有匹配「{filter}」的章节。");
            return 1;
        }

        var failures = 0;

        foreach (var variant in variants)
        {
            Application.Current!.RequestedThemeVariant = variant;
            var suffix = variant == ThemeVariant.Dark ? "dark" : "light";

            foreach (var section in sections)
            {
                var name = Sanitize(section.Title);
                try
                {
                    var window = new Window
                    {
                        Width = 1180,
                        Height = 900,
                        SystemDecorations = SystemDecorations.None,
                        // 必须带 ThemeVariant：token 都在 ThemeDictionaries 里，
                        // 不带的重载查不到，窗口底色会变成纯黑
                        Background = Application.Current!.TryFindResource(
                            "SolidBackgroundFillColorBaseBrush", variant, out var bg)
                            ? bg as Avalonia.Media.IBrush
                            : null,
                        Padding = new Thickness(32, 24, 32, 24),
                        Content = new ScrollViewer { Content = SectionRegistry.Create(section) },
                    };

                    window.Show();
                    // 布局和绑定要跑完才有像素；CaptureRenderedFrame 自己会推一帧。
                    Dispatcher.UIThread.RunJobs();
                    window.Measure(new Size(window.Width, window.Height));
                    window.Arrange(new Rect(0, 0, window.Width, window.Height));
                    Dispatcher.UIThread.RunJobs();

                    var frame = window.CaptureRenderedFrame();
                    if (frame is null)
                    {
                        Console.Error.WriteLine($"[空帧] {section.Title}");
                        failures++;
                    }
                    else
                    {
                        var path = Path.Combine(outDir, $"{name}.{suffix}.png");
                        frame.Save(path);
                        Console.WriteLine($"  {path}");
                    }

                    window.Close();
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"[失败] {section.Title}: {ex.Message}");
                    failures++;
                }
            }
        }

        Console.WriteLine(failures == 0
            ? $"全部 {sections.Count * variants.Length} 张渲染完成。"
            : $"{failures} 张失败。");
        return failures == 0 ? 0 : 1;
    }

    private static string Sanitize(string title)
    {
        var s = title.Replace(" / ", "-").Replace(" ", "-").Replace("·", "-");
        foreach (var c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '-');
        return s;
    }
}
