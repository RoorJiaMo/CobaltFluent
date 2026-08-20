using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Cobalt.Fluent.Controls;

namespace Cobalt.Fluent.Gallery.Pages;

public partial class GaugePage : UserControl
{
    public GaugePage()
    {
        AvaloniaXamlLoader.Load(this);

        var bars = this.FindControl<BarChart>("Bars")!;
        bars.Categories = ["08", "09", "10", "11", "12", "13", "14"];
        bars.Series.Add(new BarSeries
        {
            Name = "A 线",
            PaletteIndex = 1,
            Values = [86, 94, 102, 98, 64, 88, 96],
        });
        bars.Series.Add(new BarSeries
        {
            Name = "B 线",
            PaletteIndex = 2,
            Values = [72, 80, 78, 84, 52, 76, 82],
        });
    }
}
