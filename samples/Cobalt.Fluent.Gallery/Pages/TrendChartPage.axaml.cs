using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Cobalt.Fluent.Controls;

namespace Cobalt.Fluent.Gallery.Pages;

public partial class TrendChartPage : UserControl
{
    public TrendChartPage()
    {
        AvaloniaXamlLoader.Load(this);

        var chart = this.FindControl<TrendChart>("Chart")!;
        var random = new Random(20260820);

        // 四个通道：围绕各自的基线游走。四条末值刻意贴得很近，
        // 用来说明为什么当前值要由图例承载而不是逐线标在末端。
        chart.Series.Add(Channel("通道 1 · 上区", 1, 86, 0.9, random));
        chart.Series.Add(Channel("通道 2 · 中区", 2, 85, 1.1, random));
        chart.Series.Add(Channel("通道 3 · 下区", 3, 84, 0.8, random));
        chart.Series.Add(Channel("通道 4 · 出口", 4, 88, 1.4, random));

        chart.XLabels = Enumerable.Range(0, 61)
            .Select(i => i % 10 == 0 ? $"{i}" : "")
            .ToArray();

        this.FindControl<ChartLegend>("Legend")!.Chart = chart;

        this.FindControl<Sparkline>("Spark1")!.Values = Walk(20, 40, 1.6, random);
        this.FindControl<Sparkline>("Spark2")!.Values = Walk(20, 60, 1.2, random);
        this.FindControl<Sparkline>("Spark3")!.Values = Walk(20, 50, 0.4, random);
    }

    private static ChartSeries Channel(string name, int palette, double baseline, double amp, Random random)
        => new()
        {
            Name = name,
            PaletteIndex = palette,
            Values = Walk(61, baseline, amp, random),
        };

    private static double[] Walk(int count, double start, double amplitude, Random random)
    {
        var values = new double[count];
        var v = start;
        for (var i = 0; i < count; i++)
        {
            v += (random.NextDouble() - 0.5) * amplitude;
            values[i] = v;
        }

        return values;
    }
}
