using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
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
    public void 最大化钮标成_MaxButton()
    {
        // 这一条就是 Snap Layouts 本身。没有它，Windows 眼里那块像素只是
        // 客户区，shell 不知道那是最大化钮，悬停面板不再弹出。
        var (_, bar) = Show();

        Assert.Equal(
            Win32Properties.Win32HitTestValue.MaxButton,
            Win32Properties.GetNonClientHitTestResult(Part<Button>(bar, "PART_Maximize")));
    }

    [AvaloniaFact]
    public void 最小化与关闭钮各自标对角色()
    {
        var (_, bar) = Show();

        Assert.Equal(
            Win32Properties.Win32HitTestValue.MinButton,
            Win32Properties.GetNonClientHitTestResult(Part<Button>(bar, "PART_Minimize")));
        Assert.Equal(
            Win32Properties.Win32HitTestValue.Close,
            Win32Properties.GetNonClientHitTestResult(Part<Button>(bar, "PART_Close")));
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
    public void 非_Windows_11_上报不支持贴靠布局()
    {
        // 测试跑在哪个平台是已知的，所以这条在两边都有意义：
        // Linux/macOS 上必须是 false，Windows 11 上必须是 true。
        var (_, bar) = Show();

        Assert.Equal(OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000), bar.SupportsSnapLayouts);
    }

    [AvaloniaFact]
    public void 藏掉最大化钮就等于关掉贴靠布局()
    {
        var (_, bar) = Show(b => b.IsMaximizeVisible = false);

        Assert.False(bar.SupportsSnapLayouts);
    }

    [AvaloniaFact]
    public void 不可缩放的窗口不上报贴靠布局()
    {
        // shell 的那些布局全都要改窗口尺寸，窗口锁死尺寸时面板本来就不弹。
        var bar = new TitleBar();
        var window = new Window { Width = 800, Height = 600, CanResize = false, Content = bar };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        Assert.False(bar.SupportsSnapLayouts);
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
