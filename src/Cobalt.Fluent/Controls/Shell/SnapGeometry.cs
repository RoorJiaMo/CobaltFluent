using Avalonia;

namespace Cobalt.Fluent.Controls;

/// <summary>
/// 贴靠布局的几何。**这一层是纯函数，不碰窗口、不碰平台。**
///
/// 分区算错的表现有两种，都不会报错：两个贴靠的窗口之间留一条一像素的缝，
/// 或者最右边那个溢出屏幕一像素。所以这里的每条规则都单独钉住。
/// </summary>
public static class SnapGeometry
{
    /// <summary>
    /// 三栏布局的最小工作区宽度（逻辑像素）。
    ///
    /// 低于这个宽度就不给三栏：1366 宽的面板上三等分是每栏 455，
    /// 摆不下一个正常的表单，选了等于白选。这个门槛和 Windows 11 一致。
    /// </summary>
    public const double ThreeColumnMinWidth = 1920;

    /// <summary>
    /// 宽屏「小-大-小」布局的最小宽高比。带鱼屏上才有意义，
    /// 16:9 上中间那块占一半宽已经足够宽，两边的 25% 太窄。
    /// </summary>
    public const double UltraWideMinAspect = 2.0;

    /// <summary>
    /// 把比例分区换算成屏幕上的像素矩形。
    ///
    /// <b>取整取的是边界，不是尺寸。</b>1919 像素三等分，各自把 639.67 取整成 640
    /// 就得到 1920，最右边那栏溢出一像素；反过来取 639 就在右边留一条 2 像素的缝。
    /// 改成两条边界各自从比例取整（0→0、1/3→640、2/3→1279、1→1919），
    /// 相邻分区共用同一个边界值，拼起来严丝合缝且正好铺满。
    ///
    /// 取整方式用 <see cref="MidpointRounding.AwayFromZero"/> 只是为了可预期；
    /// 拼接是否严丝合缝与取整方式无关——相邻两块调用的是同一个函数、同一个入参。
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">分区无效，或工作区没有正的面积。</exception>
    public static PixelRect ZoneRect(PixelRect workArea, SnapZone zone)
    {
        if (!zone.IsValid)
            throw new ArgumentOutOfRangeException(nameof(zone), zone, "分区必须落在工作区之内且有正的面积。");
        if (workArea.Width <= 0 || workArea.Height <= 0)
            throw new ArgumentOutOfRangeException(nameof(workArea), workArea, "工作区必须有正的面积。");

        var left = Edge(workArea.X, workArea.Width, zone.X);
        var right = Edge(workArea.X, workArea.Width, zone.Right);
        var top = Edge(workArea.Y, workArea.Height, zone.Y);
        var bottom = Edge(workArea.Y, workArea.Height, zone.Bottom);

        // 比例合法但工作区太小时（比如 3 像素宽三等分），两条边界可能取整到同一个值。
        // 宽高至少给 1：零宽的窗口在各平台上的表现从「不可见」到「崩溃」都有。
        return new PixelRect(left, top, Math.Max(1, right - left), Math.Max(1, bottom - top));

        static int Edge(int origin, int extent, double fraction) =>
            origin + (int)Math.Round(extent * fraction, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// 当前工作区该提供哪些布局。
    ///
    /// 竖屏（高比宽大）不给分栏：竖屏上把窗口切成左右两半，每半都窄到没法用。
    /// 这时候换成上下切分——这正是竖屏摆两个窗口的自然方式。
    /// </summary>
    /// <param name="workArea">工作区，像素。</param>
    /// <param name="scaling">该屏幕的缩放倍率。门槛按逻辑像素判，
    /// 否则 4K 屏上 200% 缩放会被当成超宽屏。</param>
    public static IReadOnlyList<SnapLayout> LayoutsFor(PixelRect workArea, double scaling)
    {
        if (workArea.Width <= 0 || workArea.Height <= 0) return [];

        // 缩放拿到 0 或 NaN 时按 1 处理：宁可门槛判得保守，也不要除出个无穷大
        // 然后把超宽屏布局发到一台 7 寸面板上。
        var scale = scaling > 0 && !double.IsNaN(scaling) ? scaling : 1d;
        var logicalWidth = workArea.Width / scale;
        var aspect = (double)workArea.Width / workArea.Height;

        if (workArea.Height > workArea.Width) return SnapLayout.PortraitSet;

        var layouts = new List<SnapLayout>(SnapLayout.LandscapeSet);
        if (logicalWidth >= ThreeColumnMinWidth) layouts.Add(SnapLayout.Thirds);
        if (logicalWidth >= ThreeColumnMinWidth && aspect >= UltraWideMinAspect)
            layouts.Add(SnapLayout.WideCenter);

        return layouts;
    }

    /// <summary>
    /// 给分区归类，供朗读名使用。归不了类就是 <see cref="SnapZoneKind.Custom"/>，
    /// 由调用方退回百分比描述——不认识的形状不能硬套一个名字，那是在骗读屏用户。
    /// </summary>
    public static SnapZoneKind Classify(SnapZone zone)
    {
        if (!zone.IsValid) return SnapZoneKind.Custom;

        var fullWidth = Near(zone.X, 0) && Near(zone.Width, 1);
        var fullHeight = Near(zone.Y, 0) && Near(zone.Height, 1);

        if (fullHeight)
        {
            // 通栏高的分区按「左 / 中 / 右」和宽度定名。中间那一档不能漏：
            // 宽屏三栏（25/50/25）的中间块正好是半宽通栏高，只按宽度判会被叫成「右半屏」。
            var side = Near(zone.X, 0) ? Side.Left
                     : Near(zone.Right, 1) ? Side.Right
                     : Side.Center;

            if (Near(zone.Width, 1d / 2))
                return side switch
                {
                    Side.Left => SnapZoneKind.LeftHalf,
                    Side.Right => SnapZoneKind.RightHalf,
                    _ => SnapZoneKind.CenterHalf,
                };

            if (Near(zone.Width, 1d / 3))
                return side switch
                {
                    Side.Left => SnapZoneKind.LeftThird,
                    Side.Right => SnapZoneKind.RightThird,
                    _ => SnapZoneKind.CenterThird,
                };

            if (Near(zone.Width, 2d / 3))
                return side == Side.Left ? SnapZoneKind.LeftTwoThirds : SnapZoneKind.RightTwoThirds;

            if (Near(zone.Width, 1d / 4))
                return side == Side.Left ? SnapZoneKind.LeftQuarter
                     : side == Side.Right ? SnapZoneKind.RightQuarter
                     : SnapZoneKind.Custom;
        }

        if (fullWidth && Near(zone.Height, 1d / 2))
            return Near(zone.Y, 0) ? SnapZoneKind.TopHalf : SnapZoneKind.BottomHalf;

        if (Near(zone.Width, 1d / 2) && Near(zone.Height, 1d / 2)
            && (Near(zone.X, 0) || Near(zone.Right, 1))
            && (Near(zone.Y, 0) || Near(zone.Bottom, 1)))
        {
            return (Near(zone.X, 0), Near(zone.Y, 0)) switch
            {
                (true, true) => SnapZoneKind.TopLeftQuarter,
                (false, true) => SnapZoneKind.TopRightQuarter,
                (true, false) => SnapZoneKind.BottomLeftQuarter,
                (false, false) => SnapZoneKind.BottomRightQuarter,
            };
        }

        return SnapZoneKind.Custom;

        // 同样写成「不满足即不相等」：NaN 不能被判成「接近」。
        static bool Near(double a, double b) => !(Math.Abs(a - b) > 1e-6);
    }

    private enum Side { Left, Center, Right }
}
