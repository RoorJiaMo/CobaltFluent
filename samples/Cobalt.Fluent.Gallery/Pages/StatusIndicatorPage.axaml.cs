using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Cobalt.Fluent.Controls;

namespace Cobalt.Fluent.Gallery.Pages;

public partial class StatusIndicatorPage : UserControl
{
    private static readonly (DeviceState State, string Label)[] Cycle =
    [
        (DeviceState.Offline, "未连接"),
        (DeviceState.Idle, "待机"),
        (DeviceState.Running, "运行中"),
        (DeviceState.Warning, "参数偏离"),
        (DeviceState.Fault, "超温停机"),
    ];

    public StatusIndicatorPage()
    {
        AvaloniaXamlLoader.Load(this);

        var live = this.FindControl<StatusIndicator>("Live")!;
        var name = this.FindControl<TextBlock>("StateName")!;
        var index = 2;

        void Show()
        {
            var (state, label) = Cycle[index];
            live.State = state;
            live.Label = label;
            name.Text = $":{state.ToString().ToLowerInvariant()}";
        }

        Show();
        this.FindControl<Button>("NextState")!.Click += (_, _) =>
        {
            index = (index + 1) % Cycle.Length;
            Show();
        };
    }
}
