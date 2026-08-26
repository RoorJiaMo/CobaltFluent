using Avalonia;
using Avalonia.Controls;

namespace Cobalt.Fluent.Controls;

/// <summary>
/// 贴靠之前的窗口状态。取消贴靠时原样放回去。
///
/// 位置存物理像素、尺寸存逻辑像素，各自就是设回去时要用的单位——
/// 中间不再做一次换算。换算做两遍（存一遍、取一遍）的话，跨不同缩放的显示器
/// 来回搬窗口时误差会累积，表现是「还原之后窗口一次比一次小」。
/// </summary>
public readonly record struct SnapRestoreState(
    PixelPoint Position,
    Size ClientSize,
    WindowState WindowState,
    SizeToContent SizeToContent);

/// <summary>
/// 把分区落到窗口上。**这一层由本库自己执行，不调系统的贴靠。**
///
/// 只用到 <see cref="Screens"/>、<see cref="Window.Position"/> 和窗口尺寸这几样
/// Avalonia 各后端都实现了的东西，所以 Windows 10、Linux、macOS、嵌入式面板上
/// 行为一致——这正是自己做而不是交给 shell 的理由。
///
/// <b>能做什么、不能做什么要说清楚。</b>能摆的只有本进程自己的窗口。
/// Windows 11 的贴靠助手会在剩下的分区里列出**别的应用**的窗口，那需要系统级权限，
/// 本库做不到，也不该假装做得到。对多窗口上位机（主控 + 撕出去的趋势窗、报警窗）
/// 来说，能摆自己的窗口已经覆盖了绝大多数场景。
/// </summary>
public static class WindowSnap
{
    /// <summary>
    /// 贴靠前的窗口状态。挂在窗口上而不是标题栏上：一个窗口可能换标题栏，
    /// 但「贴靠前它多大」是窗口自己的事。
    /// </summary>
    public static readonly AttachedProperty<SnapRestoreState?> RestoreStateProperty =
        AvaloniaProperty.RegisterAttached<Window, SnapRestoreState?>(
            "RestoreState", typeof(WindowSnap));

    public static SnapRestoreState? GetRestoreState(Window window) =>
        window.GetValue(RestoreStateProperty);

    public static void SetRestoreState(Window window, SnapRestoreState? value) =>
        window.SetValue(RestoreStateProperty, value);

    /// <summary>这个窗口现在是不是贴靠状态。</summary>
    public static bool IsSnapped(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);
        return GetRestoreState(window) is not null;
    }

    /// <summary>
    /// 这个窗口能不能贴靠。
    ///
    /// 两个条件：拿得到屏幕信息（单窗口平台拿不到——嵌入式 framebuffer、移动端、
    /// 浏览器），以及窗口可缩放（贴靠必然改尺寸，锁死尺寸的窗口贴不了）。
    ///
    /// 报出来是为了让界面能提前变灰，而不是让操作员点下去才发现没反应。
    /// </summary>
    public static bool CanSnap(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);
        return window.CanResize && ScreenOf(window) is not null;
    }

    /// <summary>
    /// 算出这个窗口贴到该分区之后会占哪块像素。**不改窗口**，供预览和测试用。
    /// </summary>
    /// <returns>拿不到屏幕信息时返回 null。</returns>
    public static PixelRect? ZoneRectFor(Window window, SnapZone zone)
    {
        ArgumentNullException.ThrowIfNull(window);
        var screen = ScreenOf(window);
        return screen is null ? null : SnapGeometry.ZoneRect(screen.WorkingArea, zone);
    }

    /// <summary>当前窗口所在屏幕上可用的布局。拿不到屏幕信息时是空表。</summary>
    public static IReadOnlyList<SnapLayout> LayoutsFor(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);
        var screen = ScreenOf(window);
        return screen is null ? [] : SnapGeometry.LayoutsFor(screen.WorkingArea, screen.Scaling);
    }

    /// <summary>
    /// 把窗口贴到指定分区。
    ///
    /// 已经贴靠的窗口再贴到别处，<b>还原点保持第一次贴靠之前的那个</b>——
    /// 每贴一次就把还原点改成当前分区的话，来回换几次布局之后就再也回不到原始尺寸了，
    /// 而操作员以为「取消贴靠」总能回到他一开始那个窗口。
    /// </summary>
    /// <returns>贴上去了返回 true；拿不到屏幕信息或窗口不可缩放返回 false。</returns>
    public static bool Snap(Window window, SnapZone zone)
    {
        ArgumentNullException.ThrowIfNull(window);

        if (!window.CanResize) return false;
        if (ScreenOf(window) is not { } screen) return false;

        var target = SnapGeometry.ZoneRect(screen.WorkingArea, zone);

        if (GetRestoreState(window) is null) SetRestoreState(window, Capture(window));

        // 最大化的窗口设位置和尺寸是没有效果的（各平台表现不一，有的静默忽略，
        // 有的等还原之后才生效）。先落回 Normal。
        window.WindowState = WindowState.Normal;

        // 自动尺寸的窗口会在下一轮布局里把我们设的尺寸改回去，
        // 表现是「贴靠了一下又弹回原样」。
        window.SizeToContent = SizeToContent.Manual;

        Resize(window, screen.Scaling, target);
        return true;
    }

    /// <summary>
    /// 取消贴靠，把窗口放回贴靠之前的位置和尺寸。
    /// </summary>
    /// <returns>本来就没贴靠时返回 false。</returns>
    public static bool Unsnap(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);

        if (GetRestoreState(window) is not { } saved) return false;

        SetRestoreState(window, null);

        window.WindowState = saved.WindowState;
        window.SizeToContent = saved.SizeToContent;

        // 最大化 / 全屏态下不去动位置尺寸：那两个状态本来就由平台决定几何，
        // 设了要么被忽略，要么在还原时冒出一个错的尺寸。
        if (saved.WindowState != WindowState.Normal) return true;

        window.Position = saved.Position;
        if (saved.SizeToContent == SizeToContent.Manual)
        {
            window.Width = saved.ClientSize.Width;
            window.Height = saved.ClientSize.Height;
        }

        return true;
    }

    private static SnapRestoreState Capture(Window window) => new(
        window.Position,
        window.ClientSize,
        window.WindowState,
        window.SizeToContent);

    private static void Resize(Window window, double scaling, PixelRect target)
    {
        window.Position = target.Position;

        var size = ClientSizeFor(target, scaling, FramePadding(window.FrameSize, window.ClientSize));
        window.Width = size.Width;
        window.Height = size.Height;
    }

    /// <summary>
    /// 系统装饰占掉的那一圈（逻辑像素）。
    ///
    /// <see cref="Window.Position"/> 是窗口外框的左上角（含装饰），
    /// 而 <see cref="Window.Width"/> / <see cref="Window.Height"/> 是客户区。
    /// 不减掉这一圈的话，用系统装饰的窗口贴靠之后会比分区大出一整圈边框，
    /// 右边和下边压在隔壁分区上——并排两个窗口就有了一条重叠带。
    ///
    /// 用了扩展客户区（自绘标题栏）时外框就是客户区，这一圈是 0。
    ///
    /// <b>抽成独立函数是因为无头环境里没有装饰</b>：<c>FrameSize</c> 恒为 null，
    /// 补偿写错也测不出来。放在这里就能直接钉住。
    /// </summary>
    internal static Size FramePadding(Size? frame, Size client)
    {
        if (frame is not { } f) return default;

        // 负数说明拿到的两个尺寸不是同一时刻的（布局还没跑完），按 0 处理：
        // 宁可少减一点，也不要把窗口越贴越小。
        return new Size(Math.Max(0, f.Width - client.Width), Math.Max(0, f.Height - client.Height));
    }

    /// <summary>
    /// 分区（物理像素）换算成要设给窗口的客户区尺寸（逻辑像素）。
    ///
    /// <b>无头环境的缩放恒为 1，这条换算在那里测不出来</b>——而它一旦写错，
    /// 表现是「200% 缩放的机器上贴靠之后窗口只有分区的一半大」，
    /// 开发机上永远复现不出来。
    /// </summary>
    internal static Size ClientSizeFor(PixelRect target, double scaling, Size framePadding)
    {
        // 缩放拿到 0 或 NaN 时按 1：除出无穷大之后尺寸变成 NaN，
        // 窗口的表现从「尺寸不变」到「崩溃」都有。
        var scale = scaling > 0 && !double.IsNaN(scaling) ? scaling : 1d;

        return new Size(
            Math.Max(1, target.Width / scale - framePadding.Width),
            Math.Max(1, target.Height / scale - framePadding.Height));
    }

    private static Avalonia.Platform.Screen? ScreenOf(Window window)
    {
        // 拿不到所在屏幕就退回主屏：窗口刚创建、还没被平台放到某块屏幕上时
        // ScreenFromWindow 会是 null，这时候按主屏算比什么都不做有用。
        // 两个都拿不到就是真没有屏幕信息（无头 / 单窗口平台），返回 null，
        // 由调用方把入口变灰——贴靠是锦上添花的功能，不该让应用崩在这里。
        return window.Screens?.ScreenFromWindow(window) ?? window.Screens?.Primary;
    }
}
