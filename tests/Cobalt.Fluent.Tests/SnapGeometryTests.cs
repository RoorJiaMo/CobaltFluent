using Avalonia;
using Cobalt.Fluent.Controls;
using Xunit;

namespace Cobalt.Fluent.Tests;

/// <summary>
/// 贴靠分区的几何。这一层是纯函数，所以能穷举着测。
///
/// 算错的两种表现都不会报错、也不会崩：两个贴靠的窗口之间留一条一像素的缝，
/// 或者最右边那个溢出屏幕一像素。在开发机上肉眼几乎看不出来，
/// 到 4K 面板上并排三个窗口时才发现边缘对不齐。
/// </summary>
public class SnapGeometryTests
{
    // 故意都取除不尽的数：1920 三等分正好整除，测不出取整的问题。
    private static readonly PixelRect[] Areas =
    [
        new(0, 0, 1920, 1080),
        new(0, 0, 1919, 1079),
        new(0, 0, 1366, 768),
        new(0, 0, 3441, 1440),        // 带鱼屏，宽也是奇数
        new(0, 0, 1080, 1920),        // 竖屏
        new(-1920, 30, 1917, 1013),   // 副屏在主屏左边，且有任务栏
        new(100, 100, 7, 5),          // 病态小工作区
    ];

    // ---- 拼接不变量：这是整个几何层的核心 -----------------------------------

    [Fact]
    public void 每套布局都严丝合缝铺满工作区()
    {
        // 判据用的是「面积之和相等 + 两两不相交 + 都在工作区内」这三条。
        // 对轴对齐的整数矩形，这三条合起来等价于恰好铺满：互不相交的若干块
        // 落在工作区内，面积又正好等于工作区，就只能是不重不漏。
        // 逐像素对账在 1920×1080 上是一亿多次判定，跑一次四十秒——
        // 慢到没人愿意跑的测试等于没有测试。
        foreach (var area in Areas)
        foreach (var layout in SnapLayout.All)
        {
            var rects = layout.Zones.Select(z => SnapGeometry.ZoneRect(area, z)).ToArray();
            var what = $"{layout.Kind} 在 {area} 上";

            Assert.Equal((long)area.Width * area.Height, rects.Sum(r => (long)r.Width * r.Height));

            for (var i = 0; i < rects.Length; i++)
            {
                Assert.True(area.Contains(rects[i]), $"{what}：{rects[i]} 越出工作区");
                for (var j = i + 1; j < rects.Length; j++)
                    Assert.False(rects[i].Intersects(rects[j]),
                        $"{what}：{rects[i]} 和 {rects[j]} 重叠");
            }
        }
    }

    [Fact]
    public void 逐像素复核一遍拼接()
    {
        // 上面那条是间接判据。这里在一块小到跑得动的工作区上直接数一遍，
        // 免得判据本身写错了还自我印证。尺寸取质数，避免整除掩盖取整问题。
        var area = new PixelRect(13, 7, 97, 61);

        foreach (var layout in SnapLayout.All)
        {
            var rects = layout.Zones.Select(z => SnapGeometry.ZoneRect(area, z)).ToArray();

            for (var x = area.X; x < area.Right; x++)
            for (var y = area.Y; y < area.Bottom; y++)
            {
                var hits = rects.Count(r => r.ContainsExclusive(new PixelPoint(x, y)));
                Assert.True(hits == 1,
                    $"{layout.Kind} 在 {area} 上，像素 ({x},{y}) 被覆盖 {hits} 次");
            }
        }
    }

    [Fact]
    public void 分区不会越出工作区()
    {
        foreach (var area in Areas)
        foreach (var layout in SnapLayout.All)
        foreach (var zone in layout.Zones)
        {
            var rect = SnapGeometry.ZoneRect(area, zone);
            Assert.True(area.Contains(rect), $"{layout.Kind} 的 {zone} 在 {area} 上算出 {rect}，越界");
        }
    }

    [Fact]
    public void 相邻分区共用同一条边界()
    {
        // 取整取的是边界不是尺寸——这一条就是那个决定的直接后果。
        // 1919 三等分：各自把 639.67 取整成 640 会得到 1920，最右边溢出一像素。
        var area = new PixelRect(0, 0, 1919, 1080);
        var rects = SnapLayout.Thirds.Zones.Select(z => SnapGeometry.ZoneRect(area, z)).ToArray();

        Assert.Equal(rects[0].Right, rects[1].X);
        Assert.Equal(rects[1].Right, rects[2].X);
        Assert.Equal(area.Right, rects[2].Right);
        Assert.Equal(1919, rects.Sum(r => r.Width));
    }

    [Fact]
    public void 工作区带偏移时分区跟着偏移()
    {
        // 副屏常常在主屏左边，X 是负的；顶部任务栏让 Y 不是 0。
        // 把原点当成 0 的实现在主屏上完全正常，一插副屏就把窗口摆到屏幕外。
        var area = new PixelRect(-1920, 40, 1920, 1040);
        var left = SnapGeometry.ZoneRect(area, SnapLayout.Halves.Zones[0]);

        Assert.Equal(new PixelRect(-1920, 40, 960, 1040), left);
    }

    // ---- 入参校验 ------------------------------------------------------------

    [Theory]
    [InlineData(0, 0, 0, 1)]          // 零宽
    [InlineData(0, 0, 1, 0)]          // 零高
    [InlineData(-0.1, 0, 0.5, 1)]     // 越出左边
    [InlineData(0.6, 0, 0.5, 1)]      // 越出右边
    [InlineData(0, 0, double.NaN, 1)] // NaN
    public void 无效分区当场抛而不是静默缩到左上角(double x, double y, double w, double h)
    {
        // (int)double.NaN 是 0。放行 NaN 的话分区会静默缩成左上角一个点，
        // 窗口贴过去变成一条缝——不报错、不留日志。
        var zone = new SnapZone(x, y, w, h);

        Assert.False(zone.IsValid);
        Assert.Throws<ArgumentOutOfRangeException>(
            () => SnapGeometry.ZoneRect(new PixelRect(0, 0, 1920, 1080), zone));
    }

    [Fact]
    public void 空工作区当场抛()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => SnapGeometry.ZoneRect(new PixelRect(0, 0, 0, 1080), SnapLayout.Halves.Zones[0]));
    }

    [Fact]
    public void 病态小工作区也不会算出零宽的分区()
    {
        // 零宽窗口在各平台上的表现从「不可见」到「崩溃」都有。
        foreach (var zone in SnapLayout.Quadrants.Zones)
        {
            var rect = SnapGeometry.ZoneRect(new PixelRect(0, 0, 3, 3), zone);
            Assert.True(rect.Width >= 1 && rect.Height >= 1, $"{zone} → {rect}");
        }
    }

    // ---- 布局按屏幕挑 --------------------------------------------------------

    [Fact]
    public void 窄屏不给三栏()
    {
        // 1366 宽三等分是每栏 455，摆不下一个正常表单，选了等于白选。
        var layouts = SnapGeometry.LayoutsFor(new PixelRect(0, 0, 1366, 768), 1);

        Assert.DoesNotContain(SnapLayoutKind.Thirds, layouts.Select(l => l.Kind));
        Assert.Contains(SnapLayoutKind.Halves, layouts.Select(l => l.Kind));
    }

    [Fact]
    public void 宽屏给三栏()
    {
        var layouts = SnapGeometry.LayoutsFor(new PixelRect(0, 0, 1920, 1080), 1);

        Assert.Contains(SnapLayoutKind.Thirds, layouts.Select(l => l.Kind));
    }

    [Fact]
    public void 门槛按逻辑像素判而不是物理像素()
    {
        // 13 寸 2560×1600 笔记本、150% 缩放：物理 2560 够宽，逻辑只有 1707——
        // 三等分每栏 569 逻辑像素，摆不下东西，不该给三栏。
        // 按物理像素判的实现在这里会给，而且在开发机（多半是 100% 缩放的外接屏）
        // 上永远复现不出来。
        var highDpi = SnapGeometry.LayoutsFor(new PixelRect(0, 0, 2560, 1600), 1.5)
                                  .Select(l => l.Kind).ToArray();
        Assert.DoesNotContain(SnapLayoutKind.Thirds, highDpi);

        // 同一块物理尺寸、100% 缩放：逻辑 2560，该给。
        var native = SnapGeometry.LayoutsFor(new PixelRect(0, 0, 2560, 1600), 1)
                                 .Select(l => l.Kind).ToArray();
        Assert.Contains(SnapLayoutKind.Thirds, native);

        // 4K 屏 200%：逻辑 1920，够三栏；宽高比 1.78，不是带鱼屏。
        var uhd = SnapGeometry.LayoutsFor(new PixelRect(0, 0, 3840, 2160), 2)
                              .Select(l => l.Kind).ToArray();
        Assert.Contains(SnapLayoutKind.Thirds, uhd);
        Assert.DoesNotContain(SnapLayoutKind.WideCenter, uhd);
    }

    [Fact]
    public void 带鱼屏才给宽屏三栏()
    {
        var wide = SnapGeometry.LayoutsFor(new PixelRect(0, 0, 3440, 1440), 1)
                               .Select(l => l.Kind).ToArray();
        var normal = SnapGeometry.LayoutsFor(new PixelRect(0, 0, 1920, 1080), 1)
                                 .Select(l => l.Kind).ToArray();

        Assert.Contains(SnapLayoutKind.WideCenter, wide);
        Assert.DoesNotContain(SnapLayoutKind.WideCenter, normal);
    }

    [Fact]
    public void 竖屏换成上下切分()
    {
        // 1080×1920 的面板左右切开，每半只有 540 宽。竖屏就该上下切。
        var kinds = SnapGeometry.LayoutsFor(new PixelRect(0, 0, 1080, 1920), 1)
                                .Select(l => l.Kind).ToArray();

        Assert.Contains(SnapLayoutKind.StackedHalves, kinds);
        Assert.DoesNotContain(SnapLayoutKind.Halves, kinds);
        Assert.DoesNotContain(SnapLayoutKind.WideLeft, kinds);
    }

    [Fact]
    public void 缩放拿到零或_NaN_时按一倍处理()
    {
        // 除出无穷大的话，任何屏幕都会被判成超宽屏——包括一台 7 寸面板。
        foreach (var scaling in new[] { 0d, -1d, double.NaN })
        {
            var kinds = SnapGeometry.LayoutsFor(new PixelRect(0, 0, 1366, 768), scaling)
                                    .Select(l => l.Kind).ToArray();
            Assert.DoesNotContain(SnapLayoutKind.WideCenter, kinds);
            Assert.DoesNotContain(SnapLayoutKind.Thirds, kinds);
        }
    }

    [Fact]
    public void 空工作区不给任何布局()
    {
        Assert.Empty(SnapGeometry.LayoutsFor(new PixelRect(0, 0, 0, 0), 1));
    }

    // ---- 分类（朗读名的依据） ------------------------------------------------

    [Fact]
    public void 常见形状都能归类()
    {
        Assert.Equal(SnapZoneKind.LeftHalf, SnapGeometry.Classify(SnapLayout.Halves.Zones[0]));
        Assert.Equal(SnapZoneKind.RightHalf, SnapGeometry.Classify(SnapLayout.Halves.Zones[1]));

        Assert.Equal(SnapZoneKind.LeftTwoThirds, SnapGeometry.Classify(SnapLayout.WideLeft.Zones[0]));
        Assert.Equal(SnapZoneKind.RightThird, SnapGeometry.Classify(SnapLayout.WideLeft.Zones[1]));

        Assert.Equal(SnapZoneKind.CenterThird, SnapGeometry.Classify(SnapLayout.Thirds.Zones[1]));

        Assert.Equal(SnapZoneKind.TopLeftQuarter, SnapGeometry.Classify(SnapLayout.Quadrants.Zones[0]));
        Assert.Equal(SnapZoneKind.BottomRightQuarter, SnapGeometry.Classify(SnapLayout.Quadrants.Zones[3]));

        Assert.Equal(SnapZoneKind.TopHalf, SnapGeometry.Classify(SnapLayout.StackedHalves.Zones[0]));
        Assert.Equal(SnapZoneKind.BottomHalf, SnapGeometry.Classify(SnapLayout.StackedHalves.Zones[1]));
    }

    [Fact]
    public void 宽屏三栏的中间那块不能被叫成右半屏()
    {
        // 它是半宽、通栏高，只按宽度判会撞上「右半屏」那一档。
        // 读屏念错方位，操作员按下去窗口跑到别处——比不念还糟。
        var zones = SnapLayout.WideCenter.Zones;

        Assert.Equal(SnapZoneKind.LeftQuarter, SnapGeometry.Classify(zones[0]));
        Assert.Equal(SnapZoneKind.CenterHalf, SnapGeometry.Classify(zones[1]));
        Assert.Equal(SnapZoneKind.RightQuarter, SnapGeometry.Classify(zones[2]));
    }

    [Fact]
    public void 归不了类就说归不了类()
    {
        // 不认识的形状硬套一个名字，是在骗读屏用户。
        Assert.Equal(SnapZoneKind.Custom, SnapGeometry.Classify(new SnapZone(0.1, 0.2, 0.3, 0.4)));
        Assert.Equal(SnapZoneKind.Custom, SnapGeometry.Classify(new SnapZone(0, 0, double.NaN, 1)));
    }

    [Fact]
    public void 全部布局的每一块都有名字()
    {
        // 布局表是我们自己的，出现 Custom 就说明加布局时忘了补分类。
        foreach (var layout in SnapLayout.All)
        foreach (var zone in layout.Zones)
        {
            Assert.True(SnapGeometry.Classify(zone) != SnapZoneKind.Custom,
                $"{layout.Kind} 的 {zone} 没有分类，读屏只能念出坐标");
        }
    }
}
