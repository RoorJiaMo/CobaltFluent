using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Cobalt.Fluent.Controls;

namespace Cobalt.Fluent.Gallery.Pages;

public partial class CommandBarPage : UserControl
{
    public CommandBarPage()
    {
        AvaloniaXamlLoader.Load(this);

        var bar = this.FindControl<CommandBar>("PlayBar")!;
        var picker = this.FindControl<SegmentedControl>("PositionPicker")!;
        var run = this.FindControl<AppBarButton>("RunButton")!;
        var pause = this.FindControl<AppBarButton>("PauseButton")!;
        var stop = this.FindControl<AppBarButton>("StopButton")!;
        var status = this.FindControl<TextBlock>("StatusText")!;

        // 逐个按钮改，不是只改栏上的 DefaultLabelPosition：
        // 子项挂上可视树时把默认值抄进自己的 LabelPosition，之后就不再回头看栏了。
        picker.SelectionChanged += (_, _) =>
        {
            var position = picker.SelectedIndex switch
            {
                1 => CommandBarLabelPosition.Right,
                2 => CommandBarLabelPosition.Collapsed,
                _ => CommandBarLabelPosition.Bottom,
            };

            bar.DefaultLabelPosition = position;
            foreach (var button in bar.Items.OfType<AppBarButton>())
                button.LabelPosition = position;
        };

        // 三个按钮互相禁用。命令栏上「点了没反应」比「点不动」更糟：
        // 前者要读完提示才知道机器没接受，后者一眼就看出来。
        void SetState(bool running, bool paused, string text)
        {
            run.IsEnabled = !running;
            pause.IsEnabled = running;
            stop.IsEnabled = running || paused;
            status.Text = "状态：" + text;
        }

        run.Click += (_, _) => SetState(running: true, paused: false, "运行中");
        pause.Click += (_, _) => SetState(running: false, paused: true, "已暂停");
        stop.Click += (_, _) => SetState(running: false, paused: false, "待机");
    }
}
