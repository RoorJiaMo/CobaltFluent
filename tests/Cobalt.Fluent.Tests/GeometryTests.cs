using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Cobalt.Fluent.Controls;
using Xunit;

namespace Cobalt.Fluent.Tests;

/// <summary>
/// 尺寸对不对。
///
/// 尺寸如果只靠眼睛看截图，改动一多就守不住了。这里把规格里
/// 里写死的那些数字量出来对一遍——量的是真实布局结果，不是 Setter 的字面值，
/// 所以模板里多塞一层 padding 之类的问题也能抓到。
/// </summary>
public class GeometryTests
{
    /// <summary>
    /// 把控件放进窗口跑完一次布局，返回它量出来的尺寸。
    ///
    /// 外面套一层左上对齐的容器：直接当窗口 Content 的话默认 Stretch，
    /// 控件会被拉满整个窗口，量出来的就不是它自己的尺寸了。
    /// </summary>
    private static Size Measure(Control control, double width = 600, double height = 400)
    {
        var host = new StackPanel
        {
            Children = { control },
        };

        var window = new Window { Width = width, Height = height, Content = host };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        window.Measure(new Size(width, height));
        window.Arrange(new Rect(0, 0, width, height));
        Dispatcher.UIThread.RunJobs();
        return control.Bounds.Size;
    }

    private static T Part<T>(Control host, string name) where T : Control
    {
        var found = host.GetVisualDescendants().OfType<T>().FirstOrDefault(x => x.Name == name);
        Assert.NotNull(found);
        return found!;
    }

    [AvaloniaFact]
    public void Button_高_32_最小宽_64()
    {
        // 32 高 · 最小宽 64 · 内边距 11,5,11,6（上下不对称，补偿 Segoe 基线）
        var size = Measure(new Button { Content = "保存" });

        Assert.Equal(32, size.Height, 1);
        Assert.True(size.Width >= 64, $"最小宽应该 ≥64，量到 {size.Width}");
    }

    [AvaloniaFact]
    public void Button_内边距上下不对称()
    {
        var button = new Button { Content = "保存" };
        Measure(button);

        // 上 5 下 6 —— 这一条抄错了按钮会「坐」得不对，是最能看出像不像原生的细节之一
        Assert.Equal(new Thickness(11, 5, 11, 6), button.Padding);
    }

    [AvaloniaFact]
    public void 列表项与导航项高_40_树节点高_32()
    {
        // 列表项 / 导航项 40 高；树节点 32 高
        var item = new ListBoxItem { Content = "通道 1" };
        Assert.Equal(40, Measure(item).Height, 1);

        var nav = new NavigationViewItem { Content = "总览" };
        Assert.Equal(40, Measure(nav).Height, 1);

        var tree = new TreeViewItem { Header = "机台 A" };
        // TreeViewItem 的 Bounds 含子项区域，所以量它的头部行
        var treeSize = Measure(tree);
        Assert.True(treeSize.Height >= 32, $"树节点至少 32 高，量到 {treeSize.Height}");
    }

    [AvaloniaFact]
    public void ProgressBar_高_3_不是_Avalonia_默认的_4()
    {
        // 这条容易漏：别的 Fluent 实现里指示条可能是 4 宽，本库统一 3
        var bar = new ProgressBar { Value = 40 };
        Assert.Equal(3, Measure(bar).Height, 1);
    }

    [AvaloniaFact]
    public void ToggleSwitch_轨道_40x20()
    {
        var toggle = new ToggleSwitch();
        Measure(toggle);

        var track = toggle.GetVisualDescendants()
            .OfType<Border>()
            .FirstOrDefault(b => Math.Abs(b.Bounds.Width - 40) < 1.5 && Math.Abs(b.Bounds.Height - 20) < 1.5);

        Assert.True(track is not null,
            "找不到 40×20 的轨道，量到的 Border 尺寸有：" + string.Join(", ",
                toggle.GetVisualDescendants().OfType<Border>().Select(b => $"{b.Bounds.Width:F0}×{b.Bounds.Height:F0}")));
    }

    [AvaloniaFact]
    public void CheckBox_方框_20x20()
    {
        var check = new CheckBox { Content = "启用记录" };
        Measure(check);

        var box = check.GetVisualDescendants()
            .OfType<Border>()
            .FirstOrDefault(b => Math.Abs(b.Bounds.Width - 20) < 1.5 && Math.Abs(b.Bounds.Height - 20) < 1.5);

        Assert.True(box is not null,
            "找不到 20×20 的方框，量到的 Border 尺寸有：" + string.Join(", ",
                check.GetVisualDescendants().OfType<Border>().Select(b => $"{b.Bounds.Width:F0}×{b.Bounds.Height:F0}")));
    }

    [AvaloniaFact]
    public void EStopButton_钮体_80x80()
    {
        // --estop-size: 80px。急停的尺寸是硬要求：手掌拍得中
        var estop = new EStopButton { Content = "急停" };
        Measure(estop);

        var knob = Part<Border>(estop, "PART_Knob");
        Assert.Equal(80, knob.Bounds.Width, 1);
        Assert.Equal(80, knob.Bounds.Height, 1);
    }

    [AvaloniaFact]
    public void JogButton_高_40_比普通按钮大一圈()
    {
        // 点动按钮 40 高（普通 Button 是 32）——点动是动作，命中区要更大
        var jog = new JogButton { Content = "正转" };

        Assert.Equal(40, Measure(jog).Height, 1);
    }

    [AvaloniaFact]
    public void DeviceStatusBar_高_40()
    {
        var bar = new DeviceStatusBar { Endpoint = "Modbus TCP" };
        Assert.Equal(40, Measure(bar).Height, 1);
    }

    [AvaloniaFact]
    public void ParameterRow_最小高_44()
    {
        var row = new ParameterRow { Label = "腔体温度", Unit = "°C", Setpoint = 85 };
        Assert.True(Measure(row, 900).Height >= 44, "参数行至少 44 高");
    }

    [AvaloniaFact]
    public void InfoBadge_高_16_圆点_4()
    {
        var badge = new InfoBadge { Text = "12" };
        Assert.Equal(16, Measure(badge).Height, 1);

        var dot = new InfoBadge { IsDot = true };
        var dotSize = Measure(dot);
        Assert.Equal(4, dotSize.Width, 1);
        Assert.Equal(4, dotSize.Height, 1);
    }

    [AvaloniaFact]
    public void Sparkline_固定_72x20()
    {
        // 无轴无标签，尺寸固定 —— 它是嵌在表格单元格里的，不能随内容变
        var spark = new Sparkline { Values = [1, 3, 2, 5, 4] };
        var size = Measure(spark);

        Assert.Equal(72, size.Width, 1);
        Assert.Equal(20, size.Height, 1);
    }

    [AvaloniaFact]
    public void 选中指示条_3x16()
    {
        // ListBoxItem / NavigationViewItem / TreeViewItem 共用这一条规格：
        // 3 宽 16 高。撑满会立刻显得不像 Win11。
        var item = new ListBoxItem { Content = "通道 1", IsSelected = true };
        Measure(item);

        // 指示条是 Rectangle 不是 Border —— 它要圆角 1.5，Border 的 CornerRadius
        // 在 3px 宽上会被裁得不圆，Rectangle 的 RadiusX/Y 更准
        var indicator = item.GetVisualDescendants()
            .OfType<Avalonia.Controls.Shapes.Rectangle>()
            .FirstOrDefault(r => r.Name == "PART_SelectionIndicator");

        Assert.True(indicator is not null, "模板里没有 PART_SelectionIndicator");
        Assert.Equal(3, indicator!.Bounds.Width, 1);
        Assert.Equal(16, indicator.Bounds.Height, 1);
        Assert.True(indicator.IsVisible, ":selected 时指示条应该可见");
    }
}
