using Avalonia;
using Avalonia.Controls;

namespace Cobalt.Fluent.Controls;

/// <summary>
/// 按分区比例摆放子元素的面板。贴靠面板里那一格格的示意图就是它铺的。
///
/// 用比例而不是 Grid 的行列：分区不都是规整的网格（「左半屏 + 右侧上下两块」
/// 就不是），用 Grid 得为每套布局单独写一份行列定义，加一套布局改一处 XAML——
/// 而布局表是数据，不该逼着模板跟着改。
/// </summary>
public class SnapZonePanel : Panel
{
    /// <summary>子元素占的分区。</summary>
    public static readonly AttachedProperty<SnapZone> ZoneProperty =
        AvaloniaProperty.RegisterAttached<SnapZonePanel, Control, SnapZone>("Zone");

    public static SnapZone GetZone(Control control) => control.GetValue(ZoneProperty);

    public static void SetZone(Control control, SnapZone value) => control.SetValue(ZoneProperty, value);

    /// <summary>
    /// 分区之间留的缝，逻辑像素。<b>纯视觉</b>——真正贴靠时分区是严丝合缝的，
    /// 这条缝只是让示意图上看得出是几块独立的窗口。
    /// </summary>
    public static readonly StyledProperty<double> GapProperty =
        AvaloniaProperty.Register<SnapZonePanel, double>(nameof(Gap), 2d);

    public double Gap
    {
        get => GetValue(GapProperty);
        set => SetValue(GapProperty, value);
    }

    static SnapZonePanel()
    {
        AffectsArrange<SnapZonePanel>(GapProperty);
        AffectsParentArrange<SnapZonePanel>(ZoneProperty);
    }

    /// <summary>示意图没给尺寸时的默认大小。宽高比取 16:10，和常见面板接近。</summary>
    private static readonly Size Fallback = new(120, 75);

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsInfinity(availableSize.Width) ? Fallback.Width : availableSize.Width;
        var height = double.IsInfinity(availableSize.Height) ? Fallback.Height : availableSize.Height;

        foreach (var child in Children)
        {
            var zone = GetZone(child);
            child.Measure(zone.IsValid
                ? new Size(width * zone.Width, height * zone.Height)
                : default);
        }

        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var inset = Math.Max(0, Gap) / 2;

        foreach (var child in Children)
        {
            var zone = GetZone(child);
            if (!zone.IsValid)
            {
                // 不认识的分区就不画。摊平成整块画出来的话，示意图会显示一个
                // 铺满全屏的分区，而按下去窗口去的是别处。
                child.Arrange(default);
                continue;
            }

            var cell = new Rect(
                zone.X * finalSize.Width,
                zone.Y * finalSize.Height,
                zone.Width * finalSize.Width,
                zone.Height * finalSize.Height);

            // 缝比格子还宽时不能减成负数：Arrange 收到负尺寸会抛。
            var dx = Math.Min(inset, cell.Width / 2);
            var dy = Math.Min(inset, cell.Height / 2);
            child.Arrange(cell.Deflate(new Thickness(dx, dy)));
        }

        return finalSize;
    }
}
