using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Cobalt.Fluent.Controls;

namespace Cobalt.Fluent.Gallery.Pages;

public partial class ReadoutPage : UserControl
{
    private readonly Random _random = new(20260820);
    private DispatcherTimer? _timer;
    private double _value = 85.0;
    private bool _connected = true;

    public ReadoutPage()
    {
        AvaloniaXamlLoader.Load(this);

        // 静态样品里的 :stale 需要一个「很久以前」的时间戳才成立
        var stale = this.FindControl<Readout>("Stale")!;
        stale.LastUpdated = DateTime.Now.AddSeconds(-42);
        stale.StaleAfter = TimeSpan.FromSeconds(3);

        foreach (var name in new[] { "Normal", "Deviating" })
            if (this.FindControl<Readout>(name) is { } r)
                r.LastUpdated = DateTime.Now.AddYears(1);   // 永不过期

        var live = this.FindControl<Readout>("Live")!;
        live.Value = _value;
        live.LastUpdated = DateTime.Now;

        var toggle = this.FindControl<Button>("ToggleComms")!;
        toggle.Click += (_, _) =>
        {
            _connected = !_connected;
            toggle.Content = _connected ? "模拟通信中断" : "恢复通信";
        };

        // 500ms 一跳。真实设备上数值刷新要节流到 4–10 Hz——
        // Modbus 可能 50 Hz 轮询，但人眼分辨不出 10 Hz 以上的数字变化。
        _timer = new DispatcherTimer(TimeSpan.FromMilliseconds(500), DispatcherPriority.Background,
            (_, _) =>
            {
                if (!_connected) return;   // 不更新 LastUpdated，控件自己会进 :stale

                _value = Math.Clamp(_value + (_random.NextDouble() - 0.5) * 0.6, 83, 93);
                live.Value = _value;
                live.LastUpdated = DateTime.Now;
            });
        _timer.Start();

        DetachedFromVisualTree += (_, _) => { _timer?.Stop(); _timer = null; };
    }
}
