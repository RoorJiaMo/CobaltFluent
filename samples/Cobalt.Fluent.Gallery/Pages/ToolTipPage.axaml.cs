using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Markup.Xaml;

namespace Cobalt.Fluent.Gallery.Pages;

public partial class ToolTipPage : UserControl
{
    public ToolTipPage()
    {
        AvaloniaXamlLoader.Load(this);

        var delay = this.FindControl<Slider>("Delay")!;
        var target = this.FindControl<Button>("DelayTarget")!;
        var readout = this.FindControl<TextBlock>("DelayReadout")!;

        // 改的是真的 ToolTip.ShowDelay，没有中间层：延迟这种东西讲不明白，
        // 得自己把鼠标划过去几次才知道 400 和 250 差在哪。
        void Apply()
        {
            var ms = (int)delay.Value;
            ToolTip.SetShowDelay(target, ms);
            readout.Text = $"ShowDelay = {ms} ms";
        }

        delay.PropertyChanged += (_, e) =>
        {
            if (e.Property == RangeBase.ValueProperty) Apply();
        };

        Apply();
    }
}
