using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Markup.Xaml;

namespace Cobalt.Fluent.Gallery.Pages;

public partial class SliderPage : UserControl
{
    private bool _syncing;

    public SliderPage()
    {
        AvaloniaXamlLoader.Load(this);

        var slider = this.FindControl<Slider>("Live")!;
        var number = this.FindControl<NumericUpDown>("LiveNumber")!;

        slider.PropertyChanged += (_, e) =>
        {
            if (e.Property != RangeBase.ValueProperty || _syncing) return;
            _syncing = true;
            number.Value = (decimal)slider.Value;
            _syncing = false;
        };

        number.ValueChanged += (_, _) =>
        {
            if (_syncing || number.Value is not { } v) return;
            _syncing = true;
            slider.Value = (double)v;
            _syncing = false;
        };
    }
}
