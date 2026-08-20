using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;

namespace Cobalt.Fluent.Controls;

/// <summary>从 token 里取图表用色。全部走资源，跟着主题翻转。</summary>
internal static class ChartPalette
{
    /// <summary>取第 n 条系列色（1–8，超出会循环）。</summary>
    internal static IBrush SeriesBrush(StyledElement host, int index)
    {
        var n = ((index - 1) % 8 + 8) % 8 + 1;
        return Resolve(host, $"ChartSeries{n}Brush") ?? Brushes.SteelBlue;
    }

    internal static IBrush? Resolve(StyledElement host, string key) =>
        host.TryFindResource(key, host.ActualThemeVariant, out var value) && value is IBrush brush
            ? brush
            : null;

    /// <summary>按线型给一支笔。粗细和虚线段长都写在 README 的线型层级表里。</summary>
    internal static IPen PenFor(StyledElement host, ChartSeries series)
    {
        return series.LineStyle switch
        {
            // 设定值：1px 虚线 4-3
            ChartLineStyle.Setpoint => new Pen(
                Resolve(host, "TextFillColorTertiaryBrush") ?? Brushes.Gray, 1,
                new DashStyle([4, 3], 0)),

            // 报警上下限：1px 虚线 3-3，红色 70%
            ChartLineStyle.Limit => new Pen(
                new SolidColorBrush(
                    (Resolve(host, "SystemFillColorCriticalBrush") as ISolidColorBrush)?.Color
                    ?? Colors.Red, 0.7),
                1, new DashStyle([3, 3], 0)),

            // 通道曲线：1.5px 实线
            _ => new Pen(series.Brush ?? SeriesBrush(host, series.PaletteIndex), 1.5)
            {
                LineJoin = PenLineJoin.Round,
                LineCap = PenLineCap.Round,
            },
        };
    }
}
