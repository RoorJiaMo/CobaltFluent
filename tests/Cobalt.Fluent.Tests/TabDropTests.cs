using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Avalonia.Threading;
using Cobalt.Fluent.Controls;
using Xunit;

namespace Cobalt.Fluent.Tests;

/// <summary>
/// 标签拖拽的落点判定。
///
/// 真正的撕出与并回在无头环境里测不了——那需要真的开窗口、真的移动光标、
/// 真的让窗口管理器参与。所以判定被拆成了纯函数（<see cref="TabDrop"/>），
/// 在这里逐条钉住；控件层只负责把矩形量出来喂进去。
///
/// 判错的后果都是「看着在动、结果不对」那一类：拖回来插错位置、
/// 明明落在标签栏上却撕成了新窗口、往右拖一格结果原地不动。
/// </summary>
public class TabDropTests
{
    private static PixelRect R(int x, int w) => new(x, 0, w, 32);

    // ---- 落在哪条标签栏 ------------------------------------------------------

    [Fact]
    public void 点在标签栏里就返回那一条()
    {
        var strips = new[] { new PixelRect(0, 0, 400, 32), new PixelRect(600, 0, 400, 32) };

        Assert.Equal(0, TabDrop.StripAt(new PixelPoint(200, 16), strips));
        Assert.Equal(1, TabDrop.StripAt(new PixelPoint(800, 16), strips));
    }

    [Fact]
    public void 点在所有标签栏之外就是撕出()
    {
        // -1 是「撕出去」的信号。判错成 0 的话，把标签拖到桌面空白处会莫名其妙
        // 并回第一个窗口。
        var strips = new[] { new PixelRect(0, 0, 400, 32) };

        Assert.Equal(-1, TabDrop.StripAt(new PixelPoint(200, 500), strips));
        Assert.Equal(-1, TabDrop.StripAt(new PixelPoint(900, 16), strips));
    }

    [Fact]
    public void 一个窗口都没有时也不会炸()
    {
        Assert.Equal(-1, TabDrop.StripAt(new PixelPoint(10, 10), []));
    }

    [Fact]
    public void 重叠时命中在上面的那个()
    {
        // 窗口列表大致按 Z 序。从前往后找的话，被压在下面的窗口会把落点抢走——
        // 而操作员看到的、以为自己拖进去的，是上面那个。
        var strips = new[] { new PixelRect(0, 0, 400, 32), new PixelRect(100, 0, 400, 32) };

        Assert.Equal(1, TabDrop.StripAt(new PixelPoint(200, 16), strips));
    }

    // ---- 插到第几个 ----------------------------------------------------------

    [Fact]
    public void 越过中线才算换位()
    {
        // 判据是中线不是边界。用边界的话，拖到两个标签交界处时插入点会在两个值之间
        // 来回跳，落地位置取决于最后一帧光标停在哪一侧——手抖一像素结果就不同。
        var tabs = new[] { R(0, 100), R(100, 100) };

        Assert.Equal(0, TabDrop.InsertIndexAt(new PixelPoint(49, 16), tabs));
        Assert.Equal(1, TabDrop.InsertIndexAt(new PixelPoint(51, 16), tabs));
        Assert.Equal(1, TabDrop.InsertIndexAt(new PixelPoint(149, 16), tabs));
        Assert.Equal(2, TabDrop.InsertIndexAt(new PixelPoint(151, 16), tabs));
    }

    [Fact]
    public void 拖到最右边是追加到末尾()
    {
        var tabs = new[] { R(0, 100), R(100, 100) };

        Assert.Equal(2, TabDrop.InsertIndexAt(new PixelPoint(9999, 16), tabs));
    }

    [Fact]
    public void 空标签栏插到第零个()
    {
        Assert.Equal(0, TabDrop.InsertIndexAt(new PixelPoint(50, 16), []));
    }

    [Fact]
    public void 宽度不一样的标签也按各自的中线算()
    {
        // 等宽假设在真实标签栏上不成立：标题长短不同，还有 MaxWidth 截断。
        var tabs = new[] { R(0, 40), R(40, 200), R(240, 60) };

        Assert.Equal(0, TabDrop.InsertIndexAt(new PixelPoint(19, 16), tabs));
        Assert.Equal(1, TabDrop.InsertIndexAt(new PixelPoint(21, 16), tabs));
        Assert.Equal(1, TabDrop.InsertIndexAt(new PixelPoint(139, 16), tabs));
        Assert.Equal(2, TabDrop.InsertIndexAt(new PixelPoint(141, 16), tabs));
    }

    // ---- 同栏重排的下标修正 --------------------------------------------------

    [Fact]
    public void 往右拖要扣掉原项占的那一格()
    {
        // 插入下标是在原项还在列表里时算出来的，而移除原项会让它后面的下标前移一位。
        // 不扣这一格，「往右拖一格」会变成原地不动——看起来就是拖拽没生效。
        Assert.Equal(1, TabDrop.NormalizeMoveIndex(from: 0, insertAt: 2));
        Assert.Equal(2, TabDrop.NormalizeMoveIndex(from: 1, insertAt: 3));
    }

    [Fact]
    public void 往左拖不用扣()
    {
        Assert.Equal(0, TabDrop.NormalizeMoveIndex(from: 2, insertAt: 0));
        Assert.Equal(1, TabDrop.NormalizeMoveIndex(from: 3, insertAt: 1));
    }

    [Fact]
    public void 跨标签栏搬家不用扣()
    {
        // from = -1 表示原项不在这条标签栏里，没有哪一格会因为移除而前移。
        Assert.Equal(2, TabDrop.NormalizeMoveIndex(from: -1, insertAt: 2));
    }

    [Fact]
    public void 拖回原位是原地不动()
    {
        // 从第 1 个拖到「第 1 个和第 2 个之间」= 插入下标 2，扣一格之后还是 1。
        Assert.Equal(1, TabDrop.NormalizeMoveIndex(from: 1, insertAt: 2));
        Assert.Equal(1, TabDrop.NormalizeMoveIndex(from: 1, insertAt: 1));
    }

    // ---- 撕出的窗口摆在哪 ----------------------------------------------------

    [Fact]
    public void 撕出的窗口保持抓握偏移()
    {
        // 让标签停在光标下面它被抓住的那个相对位置。窗口左上角对齐光标的话，
        // 松手瞬间窗口会「跳」一下，跳的距离正好是抓握偏移量。
        var origin = TabDrop.TearOutOrigin(new PixelPoint(1000, 500), new PixelPoint(40, 12));

        Assert.Equal(new PixelPoint(960, 488), origin);
    }

    // ---- 控件层：能力闸门 ----------------------------------------------------

    [AvaloniaFact]
    public void 单窗口平台上撕出不可用()
    {
        // 撕出是桌面专有能力。Avalonia.LinuxFramebuffer（DRM/KMS 直出，嵌入式面板
        // 走的那条路）、移动端、浏览器都是单窗口的，没有窗口列表可言。
        // 无头测试宿主没有桌面生命周期，正好走这条路径。
        var view = Mount(new TabView { IsTearOutEnabled = true });

        Assert.True(view.IsTearOutEnabled, "开关是开着的");
        Assert.False(view.CanTearOut, "但这个平台上做不到——不能让操作员拖了才发现没反应");
    }

    [AvaloniaFact]
    public void 关掉开关时撕出也不可用()
    {
        var view = Mount(new TabView { IsTearOutEnabled = false });

        Assert.False(view.CanTearOut);
    }

    // ---- 控件层：量测 --------------------------------------------------------

    [AvaloniaFact]
    public void 量得出每个标签的屏幕矩形()
    {
        var view = Mount(new TabView
        {
            Items =
            {
                new TabViewItem { Header = "第一个" },
                new TabViewItem { Header = "第二个" },
                new TabViewItem { Header = "第三个" },
            },
        });

        var tabs = view.TabBoundsOnScreen();

        Assert.Equal(3, tabs.Count);
        // 横向排列：每一个都在前一个右边，且互不重叠。量错的话落点会整体偏移。
        for (var i = 1; i < tabs.Count; i++)
            Assert.True(tabs[i].X >= tabs[i - 1].X + tabs[i - 1].Width,
                $"第 {i} 个标签的左边 {tabs[i].X} 落在第 {i - 1} 个的右边 "
                + $"{tabs[i - 1].X + tabs[i - 1].Width} 之前");
    }

    [AvaloniaFact]
    public void 标签栏矩形罩得住所有标签()
    {
        // 落点判定先看「在不在这条标签栏里」。标签栏矩形罩不住标签的话，
        // 拖到某个标签正上方反而会被判成撕出。
        var view = Mount(new TabView
        {
            Items = { new TabViewItem { Header = "甲" }, new TabViewItem { Header = "乙" } },
        });

        var strip = view.StripBoundsOnScreen();
        foreach (var tab in view.TabBoundsOnScreen())
        {
            Assert.True(strip.Contains(new PixelPoint(tab.X + tab.Width / 2, tab.Y + tab.Height / 2)),
                $"标签中心 {tab} 不在标签栏 {strip} 里");
        }
    }

    [AvaloniaFact]
    public void 没套模板时量测返回空而不是抛异常()
    {
        // 拖拽随时可能在模板还没套上的控件上发起（刚建出来、还没布局）。
        var view = new TabView();

        Assert.Equal(default, view.StripBoundsOnScreen());
        Assert.Empty(view.TabBoundsOnScreen());
    }

    // ---- 键盘重排 ------------------------------------------------------------

    [AvaloniaFact]
    public void 键盘能把标签往左右挪()
    {
        // 拖拽是纯指针手势，而工业面板上不一定有鼠标。
        var view = Mount(Three());
        var second = (TabViewItem)view.Items[1]!;

        Assert.True(second.RaiseKey(Key.PageUp));
        Assert.Equal(0, view.Items.IndexOf(second));

        Assert.True(second.RaiseKey(Key.PageDown));
        Assert.Equal(1, view.Items.IndexOf(second));
    }

    [AvaloniaFact]
    public void 挪不动时不吞按键()
    {
        // 已经在最左边还吞掉 Ctrl+Shift+PageUp，宿主绑在这个组合上的功能
        // 就再也触发不了了。
        var view = Mount(Three());
        var first = (TabViewItem)view.Items[0]!;

        Assert.False(first.RaiseKey(Key.PageUp), "在最左边应当放行");
        Assert.Equal(0, view.Items.IndexOf(first));

        var last = (TabViewItem)view.Items[2]!;
        Assert.False(last.RaiseKey(Key.PageDown), "在最右边应当放行");
    }

    [AvaloniaTheory]
    [InlineData(KeyModifiers.Control)]
    [InlineData(KeyModifiers.Shift)]
    [InlineData(KeyModifiers.Control | KeyModifiers.Shift | KeyModifiers.Alt)]
    [InlineData(KeyModifiers.None)]
    public void 修饰键不全等时不动(KeyModifiers modifiers)
    {
        // Ctrl+Shift+Alt+PageUp 多半是宿主自己的快捷键，吞掉它会让那个功能
        // 在标签有焦点时莫名失灵。
        var view = Mount(Three());
        var second = (TabViewItem)view.Items[1]!;

        Assert.False(second.RaiseKey(Key.PageUp, modifiers));
        Assert.Equal(1, view.Items.IndexOf(second));
    }

    [AvaloniaFact]
    public void 关掉重排后键盘也挪不动()
    {
        var view = Mount(Three());
        view.IsReorderEnabled = false;
        var second = (TabViewItem)view.Items[1]!;

        Assert.False(second.RaiseKey(Key.PageUp));
        Assert.Equal(1, view.Items.IndexOf(second));
    }

    // ---- 新建标签 ------------------------------------------------------------

    [AvaloniaFact]
    public void 加号在没人接的时候也不是死按钮()
    {
        // 按了没反应比按钮不存在更糟：操作员会以为卡住了而反复按。
        var view = Mount(new TabView());
        Assert.Empty(view.Items);

        view.AddButton().RaiseClick();

        Assert.Single(view.Items);
        Assert.Same(view.Items[0], view.SelectedItem);
    }

    [AvaloniaFact]
    public void 事件优先于命令和兜底()
    {
        var view = Mount(new TabView());
        var commandRan = false;
        view.AddCommand = new Gate(() => commandRan = true);
        view.TabAddRequested += (_, e) => e.Handled = true;

        view.AddButton().RaiseClick();

        Assert.False(commandRan, "事件已处理，不该再走命令");
        Assert.Empty(view.Items);
    }

    [AvaloniaFact]
    public void 事件没处理时走命令且不再兜底()
    {
        var view = Mount(new TabView());
        var commandRan = false;
        view.AddCommand = new Gate(() => commandRan = true);

        view.AddButton().RaiseClick();

        Assert.True(commandRan);
        Assert.Empty(view.Items);
    }

    private static TabView Three() => new()
    {
        Items =
        {
            new TabViewItem { Header = "甲" },
            new TabViewItem { Header = "乙" },
            new TabViewItem { Header = "丙" },
        },
    };

    private sealed class Gate(Action run) : System.Windows.Input.ICommand
    {
        public event EventHandler? CanExecuteChanged { add { } remove { } }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) => run();
    }

    private static T Mount<T>(T control) where T : Control
    {
        var window = new Window { Width = 900, Height = 400, Content = control };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        window.Measure(new Size(900, 400));
        window.Arrange(new Rect(0, 0, 900, 400));
        Dispatcher.UIThread.RunJobs();
        return control;
    }
}

internal static class TabTestExtensions
{
    /// <summary>发一次按键，返回它有没有被吞掉。</summary>
    public static bool RaiseKey(
        this TabViewItem tab, Key key,
        KeyModifiers modifiers = KeyModifiers.Control | KeyModifiers.Shift)
    {
        var args = new KeyEventArgs
        {
            RoutedEvent = InputElement.KeyDownEvent,
            Key = key,
            KeyModifiers = modifiers,
        };
        tab.RaiseEvent(args);
        return args.Handled;
    }

    public static Button AddButton(this TabView view) =>
        view.GetVisualDescendants().OfType<Button>().First(b => b.Name == "PART_AddButton");

    public static void RaiseClick(this Button button) =>
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
}
