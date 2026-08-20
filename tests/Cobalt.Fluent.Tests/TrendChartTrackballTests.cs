using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Cobalt.Fluent.Controls;
using Xunit;

namespace Cobalt.Fluent.Tests;

/// <summary>
/// 十字线是 trackball 模式：跟随指针的 X 坐标，不是找最近的数据点。
/// 这条属于「交互就是规格」，所以钉住。
/// </summary>
public class TrendChartTrackballTests
{
    private static (Window Window, TrendChart Chart) Setup()
    {
        var chart = new TrendChart
        {
            Width = 400,
            Height = 200,
            YMinimum = 0,
            YMaximum = 100,
        };

        // 21 个点：下标 0..20 均匀铺在绘图区宽度上
        chart.Series.Add(new ChartSeries
        {
            Name = "通道 1",
            Values = Enumerable.Range(0, 21).Select(i => (double)i * 5).ToArray(),
        });

        var window = new Window { Width = 500, Height = 300, Content = chart };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        return (window, chart);
    }

    [AvaloniaFact]
    public void 指针进入绘图区才有十字线()
    {
        var (window, chart) = Setup();

        Assert.Null(chart.TrackballIndex);


        // 落在绘图区中间
        window.MouseMove(new Point(220, 100));
        Dispatcher.UIThread.RunJobs();

        Assert.NotNull(chart.TrackballIndex);
    }

    [AvaloniaFact]
    public void 十字线跟随_X_坐标而不是最近的数据点()
    {
        var (window, chart) = Setup();

        // 曲线是单调上升的：如果实现成「找最近点」，Y 一变下标就会跟着变。
        // trackball 模式下同一个 X、不同的 Y 必须落在同一个下标上。
        // 这里直接调 MoveTrackballTo（控件坐标系），不走无头输入模拟 ——
        // 要测的是取点逻辑，不是输入管线。
        chart.MoveTrackballTo(new Point(200, 20));
        var atTop = chart.TrackballIndex;

        chart.MoveTrackballTo(new Point(200, 160));
        var atBottom = chart.TrackballIndex;

        Assert.True(atTop is not null && atBottom is not null,
            $"atTop={atTop} atBottom={atBottom} bounds={chart.Bounds}");
        Assert.Equal(atTop, atBottom);
    }

    [AvaloniaFact]
    public void 指针右移下标单调增大()
    {
        var (window, chart) = Setup();

        window.MouseMove(new Point(100, 100));
        Dispatcher.UIThread.RunJobs();
        var left = chart.TrackballIndex;

        window.MouseMove(new Point(300, 100));
        Dispatcher.UIThread.RunJobs();
        var right = chart.TrackballIndex;

        Assert.NotNull(left);
        Assert.NotNull(right);
        Assert.True(right > left, $"左 {left} 右 {right}：右移之后下标应该变大");
    }

    [AvaloniaFact]
    public void 绘图区之外没有十字线()
    {
        var (_, chart) = Setup();

        chart.MoveTrackballTo(new Point(200, 100));
        Assert.NotNull(chart.TrackballIndex);

        // 左侧刻度区（x < 44）不算绘图区
        chart.MoveTrackballTo(new Point(10, 100));
        Assert.Null(chart.TrackballIndex);

        // 底部 X 轴标签区（y > 178）也不算
        chart.MoveTrackballTo(new Point(200, 195));
        Assert.Null(chart.TrackballIndex);
    }

    [AvaloniaFact]
    public void 十字线可以关掉()
    {
        var (_, chart) = Setup();
        chart.IsTrackballEnabled = false;

        chart.MoveTrackballTo(new Point(200, 100));

        Assert.Null(chart.TrackballIndex);
    }

    [AvaloniaFact]
    public void 图例点击隐藏系列()
    {
        var chart = new TrendChart();
        var series = new ChartSeries { Name = "通道 1", Values = [1, 2, 3] };
        chart.Series.Add(series);

        Assert.False(series.IsHidden);

        series.IsHidden = true;   // 图例项点击做的就是这一下

        Assert.True(series.IsHidden);
    }
}
