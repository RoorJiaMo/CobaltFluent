using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;

namespace Cobalt.Fluent.Gallery.Pages;

public partial class TypographyPage : UserControl
{
    // 抖动只有在数值真的一直在变时才看得出来，静态截图上永远说服不了人。
    private readonly DispatcherTimer _timer = new() { Interval = System.TimeSpan.FromMilliseconds(300) };
    private readonly System.Random _rng = new();

    private int _rpm = 5000;

    public TypographyPage()
    {
        AvaloniaXamlLoader.Load(this);

        var tabular = this.FindControl<TextBlock>("TabularLive")!;
        var proportional = this.FindControl<TextBlock>("ProportionalLive")!;
        var toggle = this.FindControl<Button>("ToggleRefresh")!;

        _timer.Tick += (_, _) =>
        {
            // 夹在 4000–6000：位数恒定四位，排除「字符数变了」这个干扰项，
            // 剩下的横移就只能是字形宽度差——这正是 tnum 要解决的那一点。
            _rpm = System.Math.Clamp(_rpm + _rng.Next(-140, 141), 4000, 6000);

            var text = _rpm.ToString("F0");
            tabular.Text = text;
            proportional.Text = text;
        };

        toggle.Click += (_, _) =>
        {
            if (_timer.IsEnabled)
            {
                _timer.Stop();
                toggle.Content = "开始刷新";
            }
            else
            {
                _timer.Start();
                toggle.Content = "暂停刷新";
            }
        };

        // 页面切走后别让计时器继续跑，展柜里几十个页面都这么干会拖垮低端板子
        AttachedToVisualTree += (_, _) =>
        {
            _timer.Start();
            toggle.Content = "暂停刷新";
        };
        DetachedFromVisualTree += (_, _) => _timer.Stop();
    }
}
