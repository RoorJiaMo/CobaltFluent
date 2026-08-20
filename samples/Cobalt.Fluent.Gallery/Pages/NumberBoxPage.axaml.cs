using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Cobalt.Fluent.Gallery.Pages;

public partial class NumberBoxPage : UserControl
{
    // 两边互相赋值会来回弹（框写滑杆 → 滑杆写框 → …），一个门闩就够了：
    // 谁先动谁是源，另一边这一轮只跟不回写。
    private bool _syncing;

    public NumberBoxPage()
    {
        AvaloniaXamlLoader.Load(this);

        var slider = this.FindControl<Slider>("TempSlider")!;
        var box = this.FindControl<NumericUpDown>("TempBox")!;

        slider.PropertyChanged += (_, e) =>
        {
            if (e.Property != Slider.ValueProperty || _syncing) return;

            _syncing = true;
            // 滑杆吸附到 0.5 后仍是 double，直接转 decimal 会带上二进制小数的尾巴
            // （85.5 变 85.4999…），先按显示精度收一次，数字框才不会显示成两个值。
            box.Value = (decimal)System.Math.Round(slider.Value, 1);
            _syncing = false;
        };

        box.ValueChanged += (_, _) =>
        {
            if (_syncing || box.Value is not { } value) return;

            _syncing = true;
            slider.Value = (double)value;
            _syncing = false;
        };
    }
}
