using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Cobalt.Fluent.Controls;
using Xunit;

namespace Cobalt.Fluent.Tests;

/// <summary>
/// 把分区落到真窗口上。
///
/// 无头平台给的是一块 1920×1280、缩放 1 的屏幕，位置和尺寸设了都能读回来，
/// 所以这一层不用只靠读代码判断——贴上去到底占了哪块像素，这里量得出来。
/// </summary>
public class WindowSnapTests
{
    private const int ScreenW = 1920;
    private const int ScreenH = 1280;

    private static Window Show(int w = 800, int h = 600)
    {
        var window = new Window { Width = w, Height = h, Position = new PixelPoint(120, 90) };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    // ---- 贴上去 --------------------------------------------------------------

    [AvaloniaFact]
    public void 贴到左半屏就真的占左半屏()
    {
        var window = Show();

        Assert.True(WindowSnap.Snap(window, SnapLayout.Halves.Zones[0]));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(new PixelPoint(0, 0), window.Position);
        Assert.Equal(ScreenW / 2d, window.Width);
        Assert.Equal((double)ScreenH, window.Height);
    }

    [AvaloniaFact]
    public void 贴到右下四分之一()
    {
        var window = Show();

        Assert.True(WindowSnap.Snap(window, SnapLayout.Quadrants.Zones[3]));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(new PixelPoint(ScreenW / 2, ScreenH / 2), window.Position);
        Assert.Equal(ScreenW / 2d, window.Width);
        Assert.Equal(ScreenH / 2d, window.Height);
    }

    [AvaloniaFact]
    public void 两个窗口贴左右两半之后正好拼满且不重叠()
    {
        // 这是这套东西存在的意义：并排的两个窗口之间不能有缝、也不能压住对方。
        var left = Show();
        var right = Show();

        WindowSnap.Snap(left, SnapLayout.Halves.Zones[0]);
        WindowSnap.Snap(right, SnapLayout.Halves.Zones[1]);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(left.Position.X + (int)left.Width, right.Position.X);
        Assert.Equal(ScreenW, (int)left.Width + (int)right.Width);
    }

    [AvaloniaFact]
    public void 最大化的窗口贴靠前先落回_Normal()
    {
        // 最大化态下设位置和尺寸，各平台表现不一：有的静默忽略，
        // 有的等还原之后才生效——两种都是「贴了没反应」。
        var window = Show();
        window.WindowState = WindowState.Maximized;
        Dispatcher.UIThread.RunJobs();

        WindowSnap.Snap(window, SnapLayout.Halves.Zones[0]);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(WindowState.Normal, window.WindowState);
        Assert.Equal(ScreenW / 2d, window.Width);
    }

    [AvaloniaFact]
    public void 自动尺寸的窗口贴靠时会被关掉自动尺寸()
    {
        // 不关的话下一轮布局又把尺寸改回去，表现是「贴了一下弹回原样」。
        var window = new Window { SizeToContent = SizeToContent.WidthAndHeight };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        WindowSnap.Snap(window, SnapLayout.Halves.Zones[0]);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(SizeToContent.Manual, window.SizeToContent);
    }

    [AvaloniaFact]
    public void 不可缩放的窗口贴不了()
    {
        // 贴靠必然改尺寸。锁死尺寸的窗口贴过去要么被平台夹回来、要么破坏约束。
        var window = new Window { Width = 400, Height = 300, CanResize = false };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        Assert.False(WindowSnap.CanSnap(window));
        Assert.False(WindowSnap.Snap(window, SnapLayout.Halves.Zones[0]));
        Assert.Equal(400, window.Width);
        Assert.False(WindowSnap.IsSnapped(window));
    }

    // ---- 还原 ----------------------------------------------------------------

    [AvaloniaFact]
    public void 取消贴靠回到原来的位置和尺寸()
    {
        var window = Show(760, 540);
        var pos = window.Position;

        WindowSnap.Snap(window, SnapLayout.Quadrants.Zones[1]);
        Dispatcher.UIThread.RunJobs();
        Assert.True(WindowSnap.IsSnapped(window));

        Assert.True(WindowSnap.Unsnap(window));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(pos, window.Position);
        Assert.Equal(760, window.Width);
        Assert.Equal(540, window.Height);
        Assert.False(WindowSnap.IsSnapped(window));
    }

    [AvaloniaFact]
    public void 换布局不会把还原点改成当前分区()
    {
        // 每贴一次就更新还原点的实现，在这里会退回到「左半屏」而不是原始尺寸——
        // 而操作员以为「取消贴靠」总能回到他一开始那个窗口。
        var window = Show(760, 540);
        var pos = window.Position;

        WindowSnap.Snap(window, SnapLayout.Halves.Zones[0]);
        Dispatcher.UIThread.RunJobs();
        WindowSnap.Snap(window, SnapLayout.Quadrants.Zones[3]);
        Dispatcher.UIThread.RunJobs();

        WindowSnap.Unsnap(window);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(pos, window.Position);
        Assert.Equal(760, window.Width);
        Assert.Equal(540, window.Height);
    }

    [AvaloniaFact]
    public void 没贴靠时取消贴靠是空操作()
    {
        var window = Show();

        Assert.False(WindowSnap.Unsnap(window));
        Assert.Equal(800, window.Width);
    }

    [AvaloniaFact]
    public void 从最大化态贴靠再取消会回到最大化()
    {
        // 最大化 → 贴靠 → 取消，该回到最大化，而不是回到最大化之前那个尺寸。
        var window = Show();
        window.WindowState = WindowState.Maximized;
        Dispatcher.UIThread.RunJobs();

        WindowSnap.Snap(window, SnapLayout.Halves.Zones[1]);
        Dispatcher.UIThread.RunJobs();
        WindowSnap.Unsnap(window);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(WindowState.Maximized, window.WindowState);
    }

    [AvaloniaFact]
    public void 自动尺寸的窗口取消贴靠会恢复自动尺寸()
    {
        var window = new Window { SizeToContent = SizeToContent.WidthAndHeight };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        WindowSnap.Snap(window, SnapLayout.Halves.Zones[0]);
        Dispatcher.UIThread.RunJobs();
        WindowSnap.Unsnap(window);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(SizeToContent.WidthAndHeight, window.SizeToContent);
    }

    // ---- 能力与查询 ----------------------------------------------------------

    [AvaloniaFact]
    public void 能力如实上报()
    {
        var window = Show();

        Assert.True(WindowSnap.CanSnap(window));
        Assert.NotEmpty(WindowSnap.LayoutsFor(window));
    }

    [AvaloniaFact]
    public void 预览算出来的矩形和真贴上去的一致()
    {
        // 面板上画的预览必须和按下去的结果是同一块像素，否则就是在骗人。
        var window = Show();
        var zone = SnapLayout.LeftAndStack.Zones[2];

        var preview = WindowSnap.ZoneRectFor(window, zone);
        Assert.NotNull(preview);

        WindowSnap.Snap(window, zone);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(preview!.Value.Position, window.Position);
        Assert.Equal(preview.Value.Width, (int)window.Width);
        Assert.Equal(preview.Value.Height, (int)window.Height);
    }

    // ---- 无头环境测不到的两处换算，抽成纯函数在这里钉 ----------------------
    //
    // 无头平台的缩放恒为 1、也没有系统装饰（FrameSize 恒为 null），
    // 所以「贴到真窗口上」那批测试对这两条完全没有约束力。
    // 而它们写错的表现恰好都只在别人的机器上出现。

    [Fact]
    public void 高_DPI_下换算成逻辑尺寸()
    {
        // 200% 缩放：960 物理像素宽的分区，要设给窗口的是 480 逻辑像素。
        // 少除这一下，窗口只有分区的一半大；多除一下，溢出一倍。
        var size = WindowSnap.ClientSizeFor(new PixelRect(0, 0, 960, 1080), 2, default);

        Assert.Equal(480, size.Width);
        Assert.Equal(540, size.Height);
    }

    [Fact]
    public void 非整数缩放也算得对()
    {
        var size = WindowSnap.ClientSizeFor(new PixelRect(0, 0, 1280, 800), 1.25, default);

        Assert.Equal(1024, size.Width);
        Assert.Equal(640, size.Height);
    }

    [Fact]
    public void 缩放拿到零或_NaN_时按一倍处理()
    {
        // 除出无穷大之后尺寸变成 NaN，窗口的表现从「不变」到「崩溃」都有。
        foreach (var scaling in new[] { 0d, -1d, double.NaN })
        {
            var size = WindowSnap.ClientSizeFor(new PixelRect(0, 0, 960, 540), scaling, default);
            Assert.Equal(960, size.Width);
            Assert.Equal(540, size.Height);
        }
    }

    [Fact]
    public void 系统装饰那一圈要从客户区里减掉()
    {
        // 不减的话，用系统装饰的窗口贴靠之后比分区大出一整圈边框，
        // 右边和下边压在隔壁分区上——并排两个窗口之间就有了一条重叠带。
        var padding = WindowSnap.FramePadding(frame: new Size(816, 639), client: new Size(800, 600));

        Assert.Equal(16, padding.Width);
        Assert.Equal(39, padding.Height);

        var size = WindowSnap.ClientSizeFor(new PixelRect(0, 0, 960, 1080), 1, padding);
        Assert.Equal(944, size.Width);
        Assert.Equal(1041, size.Height);
    }

    [Fact]
    public void 扩展客户区时没有装饰要减()
    {
        // 自绘标题栏（ExtendClientAreaToDecorationsHint）下外框就是客户区。
        Assert.Equal(default, WindowSnap.FramePadding(new Size(800, 600), new Size(800, 600)));

        // 平台不报外框尺寸时也当 0。多减一圈会让窗口一次比一次小。
        Assert.Equal(default, WindowSnap.FramePadding(null, new Size(800, 600)));

        // 布局还没跑完时两个尺寸可能不是同一时刻的，算出负数——按 0 处理。
        Assert.Equal(default, WindowSnap.FramePadding(new Size(400, 300), new Size(800, 600)));
    }

    [Fact]
    public void 换算结果不会小于一个像素()
    {
        // 零尺寸窗口在各平台上的表现从「不可见」到「崩溃」都有。
        var size = WindowSnap.ClientSizeFor(new PixelRect(0, 0, 10, 10), 1, new Size(400, 400));

        Assert.Equal(1, size.Width);
        Assert.Equal(1, size.Height);
    }

    [AvaloniaFact]
    public void 空引用一律当场抛()
    {
        Assert.Throws<ArgumentNullException>(() => WindowSnap.Snap(null!, SnapLayout.Halves.Zones[0]));
        Assert.Throws<ArgumentNullException>(() => WindowSnap.Unsnap(null!));
        Assert.Throws<ArgumentNullException>(() => WindowSnap.CanSnap(null!));
        Assert.Throws<ArgumentNullException>(() => WindowSnap.IsSnapped(null!));
    }
}
