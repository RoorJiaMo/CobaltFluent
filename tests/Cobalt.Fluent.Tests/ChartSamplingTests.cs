using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Cobalt.Fluent.Controls;
using Xunit;

namespace Cobalt.Fluent.Tests;

/// <summary>
/// 曲线抽稀。
///
/// 一个 8 小时班次按 1 Hz 采样是 28800 点/通道。这一批钉两件事：
/// **顶点数被压到屏幕能显示的量级**，以及**压下去之后尖峰还在**。
/// 后者才是关键——「每 N 个取一个」也能把顶点数压下去，代价是把超调整个抹掉，
/// 而操作员调出趋势图正是为了看那个超调。
/// </summary>
public class ChartSamplingTests
{
    private const int ShiftSamples = 28800;   // 8 小时 @ 1 Hz
    private const int PlotColumns = 800;      // 典型绘图区宽度

    private readonly List<(int Index, double Value)> _out = [];

    // ---- 一、顶点数 ----------------------------------------------------------

    [Fact]
    public void 一个班次的数据被压到每像素列两个点()
    {
        var values = Flat(ShiftSamples, 50);

        ChartSampling.Decimate(values, PlotColumns, _out);

        // 一个像素列画不出第三个 y，多出来的顶点只是让每一帧多传一遍。
        Assert.True(_out.Count <= PlotColumns * 2,
            $"抽稀后 {_out.Count} 个顶点，超过 {PlotColumns} 列 × 2");
        Assert.True(_out.Count >= PlotColumns,
            "压得比每列一个点还狠就说明丢了整列");
    }

    [Fact]
    public void 点数少于每列两个时原样保留()
    {
        // 稀疏数据分桶没有意义，抄过去更快也更准。
        var values = Ramp(100);

        ChartSampling.Decimate(values, PlotColumns, _out);

        Assert.Equal(100, _out.Count);
        for (var i = 0; i < 100; i++)
        {
            Assert.Equal(i, _out[i].Index);
            Assert.Equal(values[i], _out[i].Value);
        }
    }

    // ---- 二、尖峰必须活下来 --------------------------------------------------

    [Fact]
    public void 三个采样点的超调不会被抹掉()
    {
        // 这条是整个抽稀策略成立与否的判据。28800 个平稳点里插一次 3 点超调，
        // 「隔 36 取 1」有 92% 的概率整个丢掉，画出来是一条平静的线。
        var values = Flat(ShiftSamples, 50);
        values[12000] = 97;
        values[12001] = 99;
        values[12002] = 96;

        ChartSampling.Decimate(values, PlotColumns, _out);

        Assert.Contains(_out, s => s.Index == 12001 && s.Value == 99);
    }

    [Fact]
    public void 单点尖峰也不会被抹掉()
    {
        var values = Flat(ShiftSamples, 50);
        values[20000] = 88;

        ChartSampling.Decimate(values, PlotColumns, _out);

        Assert.Contains(_out, s => s.Index == 20000 && s.Value == 88);
    }

    [Fact]
    public void 同一像素列里的上冲和下冲都保住()
    {
        // 一个桶里同时有极大和极小时只留一个，曲线的包络会塌一半——
        // 而工艺上「摆幅多大」和「峰值多高」是两个不同的问题。
        var values = Flat(ShiftSamples, 50);
        values[12000] = 90;   // 同一个桶（每桶 36 个点）
        values[12005] = 10;

        ChartSampling.Decimate(values, PlotColumns, _out);

        Assert.Contains(_out, s => s.Index == 12000 && s.Value == 90);
        Assert.Contains(_out, s => s.Index == 12005 && s.Value == 10);
    }

    [Fact]
    public void 整条曲线的上下包络与全量一致()
    {
        // 「渲染结果和全量画在像素级上一致」全靠这条：垂直方向的极值一个不少。
        var values = Noisy(ShiftSamples);

        ChartSampling.Decimate(values, PlotColumns, _out);

        var kept = _out.Select(s => s.Value).ToArray();
        Assert.Equal(values.Min(), kept.Min());
        Assert.Equal(values.Max(), kept.Max());
    }

    // ---- 三、顺序与下标 ------------------------------------------------------

    [Fact]
    public void 输出按时间先后排列()
    {
        // 桶内先写极大再写极小的话，会给曲线引入原始数据里没有的折返方向。
        var values = Noisy(ShiftSamples);

        ChartSampling.Decimate(values, PlotColumns, _out);

        for (var i = 1; i < _out.Count; i++)
            Assert.True(_out[i].Index >= _out[i - 1].Index,
                $"下标回退：#{i - 1}={_out[i - 1].Index} → #{i}={_out[i].Index}");
    }

    [Fact]
    public void 下标是原数组下标()
    {
        // 十字线、末值标注都还挂在原数组上。抽稀返回桶号的话，
        // 十字线会指到一个和它显示的读数无关的时刻。
        var values = Ramp(ShiftSamples);

        ChartSampling.Decimate(values, PlotColumns, _out);

        foreach (var (index, value) in _out)
            Assert.Equal(values[index], value);
    }

    // ---- 四、通信中断 --------------------------------------------------------

    [Fact]
    public void 整段中断变成断点()
    {
        // 一条从中断前直连到恢复后的直线，看上去是一段平稳过程，
        // 而那段时间根本没有数据。
        var values = Flat(ShiftSamples, 50);
        for (var i = 10000; i < 14000; i++) values[i] = double.NaN;

        ChartSampling.Decimate(values, PlotColumns, _out);

        Assert.Contains(_out, s => double.IsNaN(s.Value));
        Assert.All(_out.Where(s => double.IsNaN(s.Value)),
            s => Assert.InRange(s.Index, 10000, 13999));
    }

    [Fact]
    public void 单点丢包不会把整幅图打成虚线()
    {
        // 判据是「整桶都没有有效值」而不是「桶里有一个 NaN」：36:1 的压缩下
        // 单点丢包是亚像素的，为它断线的话 1% 丢包率的链路上整张图会变成一排虚线。
        var values = Flat(ShiftSamples, 50);
        for (var i = 0; i < ShiftSamples; i += 100) values[i] = double.NaN;

        ChartSampling.Decimate(values, PlotColumns, _out);

        Assert.DoesNotContain(_out, s => double.IsNaN(s.Value));
    }

    [Fact]
    public void 坏点不参与极值()
    {
        // Math.Min(x, NaN) 返回 NaN——放进极值计算的话，一个坏点会把整个
        // 像素列的包络抹成 NaN。
        var values = Flat(ShiftSamples, 50);
        values[12000] = double.NaN;
        values[12001] = 90;
        values[12002] = double.PositiveInfinity;

        ChartSampling.Decimate(values, PlotColumns, _out);

        Assert.Contains(_out, s => s.Index == 12001 && s.Value == 90);
        Assert.DoesNotContain(_out, s => double.IsInfinity(s.Value));
    }

    [Fact]
    public void 全是坏点时不产生任何有效顶点()
    {
        var values = Enumerable.Repeat(double.NaN, ShiftSamples).ToArray();

        ChartSampling.Decimate(values, PlotColumns, _out);

        Assert.All(_out, s => Assert.True(double.IsNaN(s.Value)));
    }

    // ---- 五、边界 ------------------------------------------------------------

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-800)]
    public void 零宽或负宽不抛异常(int columns)
    {
        // 控件挂上还没布局时 Bounds 是 0；折叠面板里还会算出负的绘图区。
        ChartSampling.Decimate(Ramp(ShiftSamples), columns, _out);

        Assert.NotEmpty(_out);
        Assert.True(_out.Count <= 2, "退化到一列时最多两个点");
    }

    [Fact]
    public void 空数组得到空结果()
    {
        ChartSampling.Decimate([], PlotColumns, _out);

        Assert.Empty(_out);
    }

    [Fact]
    public void 缓冲被复用时不会串数据()
    {
        // 渲染路径上这个 List 是每帧复用的字段。忘了 Clear 的话，
        // 第二条曲线会带着第一条的顶点一起画。
        ChartSampling.Decimate(Ramp(ShiftSamples), PlotColumns, _out);
        var first = _out.Count;

        ChartSampling.Decimate(Ramp(50), PlotColumns, _out);

        Assert.Equal(50, _out.Count);
        Assert.True(first > 50);
    }

    [Fact]
    public void 极窄绘图区上的桶边界不会回退()
    {
        // from/to 用 int 算的话，28800 × 大列数会溢出成负数，桶边界往回跑，
        // 曲线画成一团。这条走的是最容易溢出的组合。
        ChartSampling.Decimate(Ramp(ShiftSamples), 4000, _out);

        for (var i = 1; i < _out.Count; i++)
            Assert.True(_out[i].Index >= _out[i - 1].Index);
    }

    // ---- 六、桶数按曲线自己的跨度算 ------------------------------------------

    [Fact]
    public void 短曲线按它自己占的那段像素分桶()
    {
        // 1000 点的曲线在 28800 点的时间轴上只占 27 px。按整幅 800 列分桶的话
        // 它根本不会被抽稀，一个像素列里照样挤 37 个点——比不抽稀还糟，
        // 因为长曲线抽了、短曲线没抽，一屏里最重的那条反而是最短的。
        var columns = ChartSampling.ColumnsFor(1000, ShiftSamples, PlotColumns);

        Assert.InRange(columns, 20, 34);

        ChartSampling.Decimate(Ramp(1000), columns, _out);
        Assert.True(_out.Count <= columns * 2, $"{_out.Count} 个顶点挤在 {columns} 列里");
    }

    [Fact]
    public void 最长的那条曲线铺满绘图区()
    {
        Assert.Equal(PlotColumns, ChartSampling.ColumnsFor(ShiftSamples, ShiftSamples, PlotColumns));
    }

    [Theory]
    [InlineData(0, 0, 800)]        // 空
    [InlineData(1, 1, 800)]        // 单点
    [InlineData(500, 500, 0)]      // 还没布局，Bounds 是 0
    [InlineData(500, 500, -80)]    // 折叠面板里算出负的绘图区
    [InlineData(500, 500, double.NaN)]
    public void 退化输入至少给一列(int seriesCount, int maxCount, double width)
    {
        // 返回 0 的话 Decimate 里的 `values.Count / columns` 会除零。
        Assert.True(ChartSampling.ColumnsFor(seriesCount, maxCount, width) >= 1);
    }

    // ---- 七、控件层：抽稀不能动交互语义 --------------------------------------

    [AvaloniaFact]
    public void 抽稀之后十字线仍然按原始下标定位()
    {
        // 抽稀纯粹是渲染层的事。十字线读的是原数组，两者一旦对不齐，
        // 十字线会指到一个和它显示的读数无关的时刻。
        var chart = new TrendChart { Width = 400, Height = 200, YMinimum = 0, YMaximum = 100 };
        chart.Series.Add(new ChartSeries { Values = Ramp(ShiftSamples) });

        var window = new Window { Width = 500, Height = 300, Content = chart };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        chart.MoveTrackballTo(new Point(44 + (400 - 44 - 8) / 2.0, 100));

        Assert.NotNull(chart.TrackballIndex);
        Assert.InRange(chart.TrackballIndex!.Value, ShiftSamples / 2 - 100, ShiftSamples / 2 + 100);
    }

    [AvaloniaFact]
    public void 非有限的设定值与报警限不会被画出去()
    {
        // `v < YMinimum || v > YMaximum` 这种写法放 NaN 过去，限值线会画到
        // 不确定的位置——而报警限画错位置比不画更危险。
        var chart = new TrendChart
        {
            Width = 400, Height = 200, YMinimum = 0, YMaximum = 100,
            Setpoint = double.NaN,
            AlarmHigh = double.NaN,
            AlarmHighLabel = "上限",
        };
        chart.Series.Add(new ChartSeries { Values = Ramp(500) });

        var window = new Window { Width = 500, Height = 300, Content = chart };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        window.Measure(new Size(500, 300));
        window.Arrange(new Rect(0, 0, 500, 300));
        Dispatcher.UIThread.RunJobs();

        // 渲染跑完没抛异常即可——无头环境测不了像素，但 NaN 几何是会炸的。
        Assert.Null(chart.TrackballIndex);
    }

    // ---- 八、极值：一个坏点不能让整条线塌掉 ----------------------------------

    [Fact]
    public void 极值跳过坏点()
    {
        // values.Min() 在含 NaN 的数组上返回 NaN，接着 range 是 NaN，
        // 而 `range <= 0` 又放 NaN 过去，于是每个点的 y 都算成 NaN——
        // 那一格变成空白，旁边几十格看着都正常，是最难发现的那种。
        var count = ChartSampling.FiniteExtent([10, 20, double.NaN, 40, 30], out var min, out var max);

        Assert.Equal(4, count);
        Assert.Equal(10, min);
        Assert.Equal(40, max);
    }

    [Fact]
    public void 极值也跳过无穷()
    {
        // ±∞ 参与的话会算出无穷的像素坐标。
        var count = ChartSampling.FiniteExtent(
            [double.NegativeInfinity, 5, double.PositiveInfinity], out var min, out var max);

        Assert.Equal(1, count);
        Assert.Equal(5, min);
        Assert.Equal(5, max);
    }

    [Fact]
    public void 全是坏点时有效点数为零()
    {
        // 调用方据此直接不画。返回 0 而不是抛异常——通信全断是运行时常态。
        Assert.Equal(0, ChartSampling.FiniteExtent(
            [double.NaN, double.NaN], out _, out _));
    }

    [Fact]
    public void 空数组的有效点数为零()
    {
        Assert.Equal(0, ChartSampling.FiniteExtent([], out _, out _));
    }

    [Fact]
    public void 迷你趋势线也做抽稀()
    {
        // 72 px 宽的格子里塞一个班次的 28800 点，每像素列摊到 400 个顶点，
        // 而表格里往往同时有几十个 Sparkline。
        const int columns = 72;

        ChartSampling.Decimate(Noisy(ShiftSamples), columns, _out);

        Assert.True(_out.Count <= columns * 2, $"{_out.Count} 个顶点挤在 {columns} 列里");
    }

    // ---- 辅助 ----------------------------------------------------------------

    private static double[] Flat(int count, double value) =>
        Enumerable.Repeat(value, count).ToArray();

    private static double[] Ramp(int count) =>
        Enumerable.Range(0, count).Select(i => (double)i).ToArray();

    /// <summary>确定性的锯齿 + 缓慢漂移，不用随机数——测试要能复现。</summary>
    private static double[] Noisy(int count) =>
        Enumerable.Range(0, count)
            .Select(i => 50 + 20 * Math.Sin(i / 97.0) + (i % 13) - 6)
            .ToArray();
}
