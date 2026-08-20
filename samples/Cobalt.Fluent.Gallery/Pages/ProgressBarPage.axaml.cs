using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;

namespace Cobalt.Fluent.Gallery.Pages;

public partial class ProgressBarPage : UserControl
{
    // 1200 s 的批次压成 15 s 看完：每 100ms 走 8 个模拟秒。
    // 再快就看不清 paused / error 换色时那 167ms 的过渡了。
    private const double Total = 1200;
    private const double StepSeconds = 8;

    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(100) };

    public ProgressBarPage()
    {
        AvaloniaXamlLoader.Load(this);

        var bar = this.FindControl<ProgressBar>("Run")!;
        var elapsed = this.FindControl<TextBlock>("Elapsed")!;
        var indeterminate = this.FindControl<CheckBox>("Indeterminate")!;

        // paused 和 error 是互斥的两档，同时挂上后哪个赢取决于 ControlTheme 里的书写顺序——
        // 那是隐式的，不能依赖。所以统一从这里进出，一次只留一个。
        void Variant(string? only)
        {
            foreach (var c in new[] { "paused", "error" })
            {
                if (c == only) bar.Classes.Add(c);
                else bar.Classes.Remove(c);
            }
        }

        void Show()
        {
            // 不确定态下进度数字是假的，宁可不显示——给个跑着的数字比不给更糟。
            elapsed.Text = bar.IsIndeterminate
                ? "时长未知"
                : $"{bar.Value:F0} / {Total:F0} s";
        }

        _timer.Tick += (_, _) =>
        {
            bar.Value = Math.Min(Total, bar.Value + StepSeconds);
            if (bar.Value >= Total) _timer.Stop();
            Show();
        };

        this.FindControl<Button>("Go")!.Click += (_, _) =>
        {
            Variant(null);
            if (bar.Value < Total) _timer.Start();
        };

        this.FindControl<Button>("Pause")!.Click += (_, _) =>
        {
            _timer.Stop();
            Variant("paused");
        };

        this.FindControl<Button>("Fault")!.Click += (_, _) =>
        {
            _timer.Stop();
            Variant("error");
        };

        this.FindControl<Button>("Reset")!.Click += (_, _) =>
        {
            _timer.Stop();
            Variant(null);
            bar.Value = 0;
            Show();
        };

        indeterminate.IsCheckedChanged += (_, _) =>
        {
            bar.IsIndeterminate = indeterminate.IsChecked == true;
            Show();
        };

        // 页面切走后计时器还在跳会白烧一个 Dispatcher 回调，展柜里几十页，攒起来很可观。
        DetachedFromVisualTree += (_, _) => _timer.Stop();

        Show();
    }
}
