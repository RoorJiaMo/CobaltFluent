using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Cobalt.Fluent;
using Cobalt.Fluent.Controls;
using Xunit;

namespace Cobalt.Fluent.Tests;

/// <summary>
/// 自绘标题栏。这里盯的是「贴靠布局还在不在」那条链路。
///
/// 无头环境下测不到的东西必须先说清楚：Snap Layouts 的面板是 Windows shell 弹的，
/// 它到底弹不弹，只有真机上才知道。这里能钉住的是我们这一侧的前置条件——
/// <c>WM_NCHITTEST</c> 的角色有没有落到正确的部件上。少标一个 MaxButton，
/// 面板就永远不弹，而且编译、运行、界面全都正常，只是那个功能悄悄没了。
/// </summary>
public class TitleBarTests
{
    private static (Window Window, TitleBar Bar) Show(Action<TitleBar>? configure = null)
    {
        var bar = new TitleBar();
        configure?.Invoke(bar);

        var window = new Window { Width = 800, Height = 600, Content = bar };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return (window, bar);
    }

    private static T Part<T>(TitleBar bar, string name) where T : Visual =>
        Assert.IsAssignableFrom<T>(
            bar.GetVisualDescendants().OfType<StyledElement>()
               .First(v => v.Name == name));

    // ---- 命中测试角色：贴靠布局的全部前提 ------------------------------------

    [AvaloniaFact]
    public void System_模式下最大化钮标成_MaxButton()
    {
        // 这一条就是「把贴靠交给 shell」那条路。没有它，Windows 眼里那块像素
        // 只是客户区，悬停面板不会弹。
        var (_, bar) = Show(b => b.SnapLayoutMode = SnapLayoutMode.System);

        var expected = bar.EffectiveSnapLayoutMode == SnapLayoutMode.System
            ? Win32Properties.Win32HitTestValue.MaxButton
            : Win32Properties.Win32HitTestValue.Client;

        Assert.Equal(expected,
            Win32Properties.GetNonClientHitTestResult(Part<Button>(bar, "PART_Maximize")));
    }

    [AvaloniaFact]
    public void Builtin_模式下最大化钮必须标回_Client()
    {
        // 两套机制只能二选一：标成 MaxButton 之后指针事件不再送到 Avalonia，
        // 我们自己那个面板的悬停就永远触发不了——界面上表现为「面板不弹」，
        // 不报错、不留痕迹。
        var (_, bar) = Show(b => b.SnapLayoutMode = SnapLayoutMode.Builtin);

        Assert.Equal(Win32Properties.Win32HitTestValue.Client,
            Win32Properties.GetNonClientHitTestResult(Part<Button>(bar, "PART_Maximize")));
    }

    [AvaloniaFact]
    public void 换模式会重标最大化钮()
    {
        // 模式是可以运行时改的（设置页里的一个开关）。不重标的话，
        // 从 System 切到 Builtin 之后那块像素还是非客户区，新面板永远弹不出来。
        //
        // 判定本身在下面穷举；这里要证的是「换模式之后确实重标了一遍」。
        // 直接把命中角色改脏，再换模式看它有没有被改回来——
        // 只比较模式前后的值的话，在非 Windows 平台上两边都是 Client，证明不了任何事。
        var (_, bar) = Show(b => b.SnapLayoutMode = SnapLayoutMode.System);
        var maximize = Part<Button>(bar, "PART_Maximize");

        Win32Properties.SetNonClientHitTestResult(maximize, Win32Properties.Win32HitTestValue.Nowhere);
        bar.SnapLayoutMode = SnapLayoutMode.Builtin;
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(TitleBar.MaximizeHitTestRole(bar.EffectiveSnapLayoutMode),
            Win32Properties.GetNonClientHitTestResult(maximize));
        Assert.NotEqual(Win32Properties.Win32HitTestValue.Nowhere,
            Win32Properties.GetNonClientHitTestResult(maximize));
    }

    // ---- 模式判定本身。穷举，因为里面有两条分支在桌面测试环境里造不出来 ----
    //
    // 「拿不到屏幕信息」是单窗口平台（嵌入式 framebuffer、移动端、浏览器），
    // 「跑在 Windows 11 上」是另一个操作系统。留在实例方法里就只能靠读代码判断。

    [Theory]
    // requested,               maximize, window, resize, screen, win11  → expected
    [InlineData(SnapLayoutMode.Auto, true, true, true, true, true, SnapLayoutMode.System)]
    [InlineData(SnapLayoutMode.Auto, true, true, true, true, false, SnapLayoutMode.Builtin)]
    [InlineData(SnapLayoutMode.Auto, true, true, true, false, false, SnapLayoutMode.None)]
    [InlineData(SnapLayoutMode.Auto, true, true, true, false, true, SnapLayoutMode.System)]
    [InlineData(SnapLayoutMode.System, true, true, true, true, true, SnapLayoutMode.System)]
    [InlineData(SnapLayoutMode.System, true, true, true, true, false, SnapLayoutMode.None)]
    [InlineData(SnapLayoutMode.Builtin, true, true, true, true, true, SnapLayoutMode.Builtin)]
    [InlineData(SnapLayoutMode.Builtin, true, true, true, true, false, SnapLayoutMode.Builtin)]
    [InlineData(SnapLayoutMode.Builtin, true, true, true, false, false, SnapLayoutMode.None)]
    [InlineData(SnapLayoutMode.None, true, true, true, true, true, SnapLayoutMode.None)]
    // 三条共同前提，缺一条谁都别想有面板
    [InlineData(SnapLayoutMode.Auto, false, true, true, true, true, SnapLayoutMode.None)]
    [InlineData(SnapLayoutMode.Auto, true, false, true, true, true, SnapLayoutMode.None)]
    [InlineData(SnapLayoutMode.Auto, true, true, false, true, true, SnapLayoutMode.None)]
    [InlineData(SnapLayoutMode.Builtin, true, true, false, true, false, SnapLayoutMode.None)]
    public void 模式判定穷举(
        SnapLayoutMode requested, bool maximizeVisible, bool hasWindow,
        bool canResize, bool hasScreen, bool systemAvailable, SnapLayoutMode expected)
    {
        Assert.Equal(expected, TitleBar.ResolveSnapMode(
            requested, maximizeVisible, hasWindow, canResize, hasScreen, systemAvailable));
    }

    [Fact]
    public void 单窗口平台上_Builtin_退成没有面板()
    {
        // 自绘的那个面板要摆窗口，摆不了就别弹——
        // 弹一个按下去没反应的面板比不弹更糟。
        // 这条分支在桌面上造不出来：无头平台也有屏幕信息。
        Assert.Equal(SnapLayoutMode.None, TitleBar.ResolveSnapMode(
            SnapLayoutMode.Builtin, true, true, true, hasScreen: false, systemAvailable: false));
    }

    [Fact]
    public void 只有_System_模式才标_MaxButton()
    {
        Assert.Equal(Win32Properties.Win32HitTestValue.MaxButton,
            TitleBar.MaximizeHitTestRole(SnapLayoutMode.System));

        foreach (var mode in new[] { SnapLayoutMode.Builtin, SnapLayoutMode.None, SnapLayoutMode.Auto })
        {
            Assert.Equal(Win32Properties.Win32HitTestValue.Client,
                TitleBar.MaximizeHitTestRole(mode));
        }
    }

    [AvaloniaFact]
    public void 空白处标成_Caption()
    {
        // 拖动移窗、双击最大化、右键系统菜单全靠这一条，全部由 shell 提供。
        var (_, bar) = Show();

        Assert.Equal(
            Win32Properties.Win32HitTestValue.Caption,
            Win32Properties.GetNonClientHitTestResult(Part<Panel>(bar, "PART_Caption")));
    }

    [AvaloniaFact]
    public void 左右内容区标回_Client()
    {
        // 漏标的表现是「放在标题栏上的菜单点了没反应」——不报错、不留痕迹。
        var (_, bar) = Show();

        Assert.Equal(
            Win32Properties.Win32HitTestValue.Client,
            Win32Properties.GetNonClientHitTestResult(Part<ContentPresenter>(bar, "PART_LeftContent")));
        Assert.Equal(
            Win32Properties.Win32HitTestValue.Client,
            Win32Properties.GetNonClientHitTestResult(Part<ContentPresenter>(bar, "PART_RightContent")));
    }

    // ---- 能力上报 ------------------------------------------------------------

    [AvaloniaFact]
    public void Auto_在非_Windows_11_上退到自绘面板()
    {
        // 这正是「框架内部自己做贴靠」要解决的事：Windows 10、Linux、macOS、
        // 嵌入式面板上系统都不给贴靠面板，退到自绘的那个之后照样有。
        var (_, bar) = Show();

        var expected = OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000)
            ? SnapLayoutMode.System
            : SnapLayoutMode.Builtin;

        Assert.Equal(expected, bar.EffectiveSnapLayoutMode);
        Assert.True(bar.SupportsSnapLayouts);
    }

    [AvaloniaFact]
    public void System_模式在非_Windows_11_上没有面板()
    {
        // 显式选了系统那个，而系统没有——不能假装有。
        var (_, bar) = Show(b => b.SnapLayoutMode = SnapLayoutMode.System);

        if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000)) return;

        Assert.Equal(SnapLayoutMode.None, bar.EffectiveSnapLayoutMode);
        Assert.False(bar.SupportsSnapLayouts);
    }

    [AvaloniaFact]
    public void 明确关掉就没有面板()
    {
        var (_, bar) = Show(b => b.SnapLayoutMode = SnapLayoutMode.None);

        Assert.Equal(SnapLayoutMode.None, bar.EffectiveSnapLayoutMode);
        Assert.False(bar.SupportsSnapLayouts);
    }

    [AvaloniaFact]
    public void 藏掉最大化钮就等于关掉贴靠布局()
    {
        // 面板是靠悬停最大化钮触发的。钮没了，两种模式都无从触发。
        var (_, bar) = Show(b => b.IsMaximizeVisible = false);

        Assert.False(bar.SupportsSnapLayouts);
        Assert.Equal(SnapLayoutMode.None, bar.EffectiveSnapLayoutMode);
    }

    [AvaloniaFact]
    public void 不可缩放的窗口不上报贴靠布局()
    {
        // 所有布局都要改窗口尺寸。锁死尺寸时弹一个按下去没反应的面板，
        // 比不弹更糟。两种模式都不该给。
        foreach (var mode in new[] { SnapLayoutMode.Auto, SnapLayoutMode.Builtin, SnapLayoutMode.System })
        {
            var bar = new TitleBar { SnapLayoutMode = mode };
            var window = new Window { Width = 800, Height = 600, CanResize = false, Content = bar };
            window.Show();
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(SnapLayoutMode.None, bar.EffectiveSnapLayoutMode);
            Assert.False(bar.SupportsSnapLayouts);
        }
    }

    [AvaloniaFact]
    public void 脱离窗口后不再上报贴靠布局()
    {
        var (window, bar) = Show();
        window.Content = null;
        Dispatcher.UIThread.RunJobs();

        Assert.False(bar.SupportsSnapLayouts);
    }

    // ---- 伪类跟随窗口状态 ----------------------------------------------------

    [AvaloniaFact]
    public void 最大化后置位_maximized()
    {
        // 这条同时守着一个生命周期陷阱：附加到可视树在前、套模板在后。
        // OnApplyTemplate 里要是把窗口订阅一起退了，这里就永远是 false。
        var (window, bar) = Show();
        Assert.DoesNotContain(":maximized", bar.Classes);

        window.WindowState = WindowState.Maximized;
        Dispatcher.UIThread.RunJobs();
        Assert.Contains(":maximized", bar.Classes);

        window.WindowState = WindowState.Normal;
        Dispatcher.UIThread.RunJobs();
        Assert.DoesNotContain(":maximized", bar.Classes);
    }

    [AvaloniaFact]
    public void 全屏也算_maximized()
    {
        // 全屏时同样没有窗口边框可参照，钮上必须是「还原」的字形。
        var (window, bar) = Show();

        window.WindowState = WindowState.FullScreen;
        Dispatcher.UIThread.RunJobs();

        Assert.Contains(":maximized", bar.Classes);
    }

    [AvaloniaFact]
    public void 最大化后钮换成还原字形()
    {
        var (window, bar) = Show();
        var glyph = Part<SymbolIcon>(bar, "PART_MaximizeGlyph");
        Assert.Equal(Symbol.Maximize, glyph.Symbol);

        window.WindowState = WindowState.Maximized;
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(Symbol.Restore, glyph.Symbol);
    }

    // ---- 字形 ----------------------------------------------------------------

    [AvaloniaFact]
    public void 三个窗口按钮全是矢量路径()
    {
        // 本库不依赖 Segoe Fluent Icons：嵌入式 Linux 上没有那套字体，
        // 用码点会渲染成豆腐块——而且是运行时才看得到。
        foreach (var symbol in new[] { Symbol.Minimize, Symbol.Maximize, Symbol.Restore })
        {
            Assert.NotNull(SymbolGeometry.Get(symbol));
        }
    }

    [AvaloniaFact]
    public void 窗口按钮字形画在中间十格里()
    {
        // Windows 的标题栏字形是 10×10 的。画满 16 格的话，和系统标题栏
        // 并排时我们的按钮明显大一圈。
        foreach (var symbol in new[] { Symbol.Minimize, Symbol.Maximize, Symbol.Restore })
        {
            var bounds = SymbolGeometry.Get(symbol)!.Bounds;

            Assert.True(bounds.Left >= 2.5 && bounds.Right <= 13.5,
                $"{symbol} 横向越界：{bounds}");
            Assert.True(bounds.Top >= 2.5 && bounds.Bottom <= 13.5,
                $"{symbol} 纵向越界：{bounds}");
            Assert.True(bounds.Width >= 8 && bounds.Height <= 10.5,
                $"{symbol} 尺寸不对：{bounds}");
        }
    }

    [AvaloniaFact]
    public void 字形前景色跟着按钮走()
    {
        // 关闭钮悬停时整个按钮的 Foreground 换成白色，红底上才看得见那个叉。
        // 字形上要是钉死了自己的画刷，这条继承就断了——而断掉的表现是
        // 「红底上一个几乎看不见的深色叉」，截图里才发现。
        var (_, bar) = Show();
        var button = Part<Button>(bar, "PART_Close");
        var glyph = Part<SymbolIcon>(bar, "PART_CloseGlyph");

        button.Foreground = Brushes.Magenta;
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(Brushes.Magenta, glyph.Foreground);
    }

    // ---- 标题回退 ------------------------------------------------------------

    [AvaloniaFact]
    public void 没给标题时用窗口标题()
    {
        var bar = new TitleBar();
        var window = new Window { Width = 800, Height = 600, Title = "研磨工位", Content = bar };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("研磨工位", bar.EffectiveTitle);
    }

    [AvaloniaFact]
    public void 窗口标题后来改了也跟着走()
    {
        // 直接往 Title 里回填的实现会在这里翻车：回填之后属性就有了值，
        // 窗口标题再变也跟不上，退化成「只取第一次」。
        var bar = new TitleBar();
        var window = new Window { Width = 800, Height = 600, Title = "第一版", Content = bar };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        window.Title = "第二版";
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("第二版", bar.EffectiveTitle);
    }

    [AvaloniaFact]
    public void 显式给了标题就压过窗口标题()
    {
        var bar = new TitleBar { Title = "自定义" };
        var window = new Window { Width = 800, Height = 600, Title = "窗口标题", Content = bar };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("自定义", bar.EffectiveTitle);

        bar.Title = null;
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("窗口标题", bar.EffectiveTitle);
    }

    // ---- 按钮动作（非 Windows 平台走这条） -----------------------------------

    [AvaloniaFact]
    public void 点最大化钮在最大化与还原之间来回()
    {
        var (window, bar) = Show();
        var button = Part<Button>(bar, "PART_Maximize");

        ClickButton(button);
        Assert.Equal(WindowState.Maximized, window.WindowState);

        ClickButton(button);
        Assert.Equal(WindowState.Normal, window.WindowState);
    }

    [AvaloniaFact]
    public void 点最小化钮把窗口收起来()
    {
        var (window, bar) = Show();

        ClickButton(Part<Button>(bar, "PART_Minimize"));

        Assert.Equal(WindowState.Minimized, window.WindowState);
    }

    [AvaloniaFact]
    public void 搬到另一个窗口后按钮仍然有效()
    {
        // 模板只套一次。脱离可视树时要是把按钮订阅也退了，搬家之后
        // 三个钮全部点不动——而且控件看着完全正常。
        var (first, bar) = Show();
        first.Content = null;
        Dispatcher.UIThread.RunJobs();

        var second = new Window { Width = 800, Height = 600, Content = bar };
        second.Show();
        Dispatcher.UIThread.RunJobs();

        ClickButton(Part<Button>(bar, "PART_Maximize"));

        Assert.Equal(WindowState.Maximized, second.WindowState);
        Assert.Equal(WindowState.Normal, first.WindowState);
    }

    private static void ClickButton(Button button)
    {
        button.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }

    // ---- 朗读名与自动化对等体 ------------------------------------------------

    [AvaloniaFact]
    public void 三个钮都有朗读名()
    {
        // 三个钮都只有一个字形、没有文字。不给名字的话读屏软件只报「按钮」，
        // 而这三个里有一个是「关闭窗口」。
        var (_, bar) = Show();

        Assert.Equal("最小化", AutomationProperties.GetName(Part<Button>(bar, "PART_Minimize")));
        Assert.Equal("最大化", AutomationProperties.GetName(Part<Button>(bar, "PART_Maximize")));
        Assert.Equal("关闭", AutomationProperties.GetName(Part<Button>(bar, "PART_Close")));
    }

    [AvaloniaFact]
    public void 最大化后那个钮的朗读名换成还原()
    {
        // 字形换了而名字没换，读屏用户听到的就是一个错的动作。
        var (window, bar) = Show();

        window.WindowState = WindowState.Maximized;
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("还原", AutomationProperties.GetName(Part<Button>(bar, "PART_Maximize")));
    }

    [AvaloniaFact]
    public void 换语言之后朗读名跟着换()
    {
        var (_, bar) = Show();
        var close = Part<Button>(bar, "PART_Close");
        Assert.Equal("关闭", AutomationProperties.GetName(close));

        var saved = CobaltStrings.Current;
        try
        {
            CobaltStrings.Current = new CobaltStrings();   // 基类是英文
            Dispatcher.UIThread.RunJobs();
            Assert.Equal("Close", AutomationProperties.GetName(close));
        }
        finally
        {
            CobaltStrings.Current = saved;
            Dispatcher.UIThread.RunJobs();
        }

        Assert.Equal("关闭", AutomationProperties.GetName(close));
    }

    [AvaloniaFact]
    public void 卸载之后不再吃语言切换事件()
    {
        // 静态事件持有实例引用是典型的泄漏源：一屏几十个控件反复建销，
        // 忘了退订就一个都回收不掉，而功能上完全看不出来。
        var (window, bar) = Show();
        window.Content = null;
        Dispatcher.UIThread.RunJobs();

        var before = AutomationProperties.GetName(Part<Button>(bar, "PART_Close"));

        var saved = CobaltStrings.Current;
        try
        {
            CobaltStrings.Current = new CobaltStrings();
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(before, AutomationProperties.GetName(Part<Button>(bar, "PART_Close")));
        }
        finally
        {
            CobaltStrings.Current = saved;
        }
    }

    [AvaloniaFact]
    public void 对等体报出标题与贴靠布局能力()
    {
        var bar = new TitleBar();
        var window = new Window { Width = 800, Height = 600, Title = "研磨工位", Content = bar };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var peer = ControlAutomationPeer.CreatePeerForElement(bar);

        // 名字走 EffectiveTitle：没给 Title 时读屏也该念出窗口标题，而不是一片空白。
        Assert.Equal("研磨工位", peer.GetName());

        // 贴靠布局出不来时，先能从这里查到是「本来就不支持」还是「配置不对」。
        Assert.Equal(
            bar.SupportsSnapLayouts ? "贴靠布局可用" : "贴靠布局不可用",
            peer.GetItemStatus());
    }

    // ---- 悬停弹出自绘面板 ----------------------------------------------------
    //
    // 这是「本库自己做贴靠」在界面上的入口。没有这一段的话，
    // SnapLayoutPicker 造得再对也没人能弹出来它。

    private static SnapLayoutPicker? OpenPicker(Window window) =>
        window.GetVisualDescendants().OfType<SnapLayoutPicker>()
              .FirstOrDefault(p => p.GetVisualRoot() is not null);

    [AvaloniaFact]
    public void 悬停最大化钮会弹出自绘面板()
    {
        // 走的是无头平台的真鼠标事件，经过真正的命中测试——
        // 伪造一个 PointerEntered 只能证明处理器接上了，证明不了指针真能落到那个钮上。
        //
        // 延时设成 0 走「当场弹」那条：触摸屏就是这么配的，
        // 顺带让这条测试不依赖真实时钟。
        var (window, bar) = Show(b =>
        {
            b.SnapLayoutMode = SnapLayoutMode.Builtin;
            b.SnapLayoutHoverDelay = TimeSpan.Zero;
        });

        Assert.Null(OpenPicker(window));

        var maximize = Part<Button>(bar, "PART_Maximize");
        Assert.True(maximize.Bounds.Width > 0, "前提不成立：按钮没有跑过布局，坐标无从谈起");

        var center = maximize.TranslatePoint(new Point(maximize.Bounds.Width / 2, maximize.Bounds.Height / 2), window);
        Assert.NotNull(center);

        window.MouseMove(center!.Value);
        Dispatcher.UIThread.RunJobs();

        Assert.NotNull(OpenPicker(window));
    }

    [AvaloniaFact]
    public void 面板里的格子按下去窗口就贴过去且面板收起()
    {
        var (window, bar) = Show(b =>
        {
            b.SnapLayoutMode = SnapLayoutMode.Builtin;
            b.SnapLayoutCloseDelay = TimeSpan.Zero;
        });

        bar.ShowSnapLayouts();
        Dispatcher.UIThread.RunJobs();

        var picker = OpenPicker(window);
        Assert.NotNull(picker);

        var cell = picker!.GetVisualDescendants().OfType<SnapZoneButton>().First();
        var zone = SnapZonePanel.GetZone(cell);
        var expected = WindowSnap.ZoneRectFor(window, zone);

        cell.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Assert.NotNull(expected);
        Assert.Equal(expected!.Value.Position, window.Position);
        Assert.Null(OpenPicker(window));
    }

    [AvaloniaFact]
    public void 面板摆的是标题栏所在的那个窗口()
    {
        // 面板挂在弹出层里，可视根不是 Window。不显式把目标窗口交给它，
        // 界面上就是「面板弹出来了，按下去没反应」。
        var (window, bar) = Show(b => b.SnapLayoutMode = SnapLayoutMode.Builtin);

        bar.ShowSnapLayouts();
        Dispatcher.UIThread.RunJobs();

        Assert.Same(window, OpenPicker(window)!.TargetWindow);
    }

    [AvaloniaFact]
    public void System_与_None_模式下叫不出自绘面板()
    {
        // 系统那个面板是 shell 弹的，我们叫不动它；明确关掉的就更不该弹。
        foreach (var mode in new[] { SnapLayoutMode.System, SnapLayoutMode.None })
        {
            var (window, bar) = Show(b => b.SnapLayoutMode = mode);

            bar.ShowSnapLayouts();
            Dispatcher.UIThread.RunJobs();

            Assert.Null(OpenPicker(window));
        }
    }

    [AvaloniaFact]
    public void 收起之后面板不再留在树上()
    {
        var (window, bar) = Show(b => b.SnapLayoutMode = SnapLayoutMode.Builtin);

        bar.ShowSnapLayouts();
        Dispatcher.UIThread.RunJobs();
        Assert.NotNull(OpenPicker(window));

        bar.CloseSnapLayouts();
        Dispatcher.UIThread.RunJobs();
        Assert.Null(OpenPicker(window));
    }

    [AvaloniaFact]
    public void 标题栏被拆掉时面板跟着收起()
    {
        // 定时器和弹出层都活在标题栏外面。不收的话，界面已经换页了，
        // 一个贴靠面板还浮在上面——而且它指着一个已经不存在的按钮。
        //
        // 观察点是 Popup.IsOpen，不是「面板还在不在可视树上」：
        // 无头平台的弹出层是同窗口覆盖层，拆树时它自己就没了，
        // 按「在不在树上」判的话，收与不收看起来一模一样。
        // 桌面上弹出层是独立窗口，不主动收就会留在屏幕上。
        // 说清这条测试的份量：它钉的是「拆掉之后面板不能还开着」这个契约，
        // 但钉不住是谁做到的——Avalonia 的 Popup 在放置目标脱离时会自己关，
        // 把控件里那行显式收起删掉，这条测试照样绿。三种拆法都试过。
        // 留着它是因为契约本身值得钉：真弄坏了（比如换成别的弹出方式）它会红。
        var bar = new TitleBar { SnapLayoutMode = SnapLayoutMode.Builtin };
        var host = new Panel { Children = { bar } };
        var window = new Window { Width = 800, Height = 600, Content = host };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        bar.ShowSnapLayouts();
        Dispatcher.UIThread.RunJobs();
        Assert.True(bar.IsSnapLayoutsOpen, "前提不成立：面板压根没弹出来");

        host.Children.Remove(bar);
        Dispatcher.UIThread.RunJobs();

        Assert.False(bar.IsSnapLayoutsOpen);
    }

    // ---- ApplyTo -------------------------------------------------------------

    [AvaloniaFact]
    public void ApplyTo_三条提示一条都不能少()
    {
        // 漏掉的表现各不相同：不扩展客户区则被系统标题栏挤在下面；
        // 不设 NoChrome 则系统按钮和自绘按钮同时出现。
        var window = new Window();

        TitleBar.ApplyTo(window);

        Assert.True(window.ExtendClientAreaToDecorationsHint);
        Assert.Equal(ExtendClientAreaChromeHints.NoChrome, window.ExtendClientAreaChromeHints);
        Assert.Equal(-1, window.ExtendClientAreaTitleBarHeightHint);
    }

    [AvaloniaFact]
    public void ApplyTo_不接受_null()
    {
        Assert.Throws<ArgumentNullException>(() => TitleBar.ApplyTo(null!));
    }
}
