using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Cobalt.Fluent.Controls;

namespace Cobalt.Fluent.Gallery.Pages;

public partial class DeviceStatusBarPage : UserControl
{
    private DispatcherTimer? _poll;
    private bool _connected = true;

    public DeviceStatusBarPage()
    {
        AvaloniaXamlLoader.Load(this);

        var bar = this.FindControl<DeviceStatusBar>("Live")!;
        var toggle = this.FindControl<Button>("ToggleComms")!;

        toggle.Click += (_, _) =>
        {
            _connected = !_connected;
            toggle.Content = _connected ? "模拟通信中断" : "恢复通信";
            bar.ConnectionState = _connected ? ConnectionState.Connected : ConnectionState.Disconnected;
        };

        // 每次「收到响应」调一次 Beat()。停掉之后心跳灯自己会转成停跳。
        // 规格样品里 Connected / Degraded 两条也要真的跳，否则心跳灯会（诚实地）显示停跳。
        // Disconnected 那条故意不喂 Beat()，让它自己超时变红。
        var spec = new[] { "SpecConnected", "SpecDegraded" }
            .Select(n => this.FindControl<DeviceStatusBar>(n))
            .OfType<DeviceStatusBar>()
            .ToArray();

        _poll = new DispatcherTimer(TimeSpan.FromMilliseconds(1000), DispatcherPriority.Background,
            (_, _) =>
            {
                foreach (var b in spec) b.Beat();
                if (_connected) bar.Beat();
            });
        _poll.Start();
        bar.Beat();
        foreach (var b in spec) b.Beat();

        DetachedFromVisualTree += (_, _) => { _poll?.Stop(); _poll = null; };
    }
}
