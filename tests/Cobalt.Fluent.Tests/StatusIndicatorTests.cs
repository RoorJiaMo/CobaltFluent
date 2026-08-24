using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Cobalt.Fluent.Controls;
using Xunit;

namespace Cobalt.Fluent.Tests;

/// <summary>
/// StatusIndicator 的三重编码：颜色 + 形状/动效 + 文字。
///
/// 这个控件的全部价值就在于「不能只靠颜色」——男性约 8% 有色觉障碍，
/// 强光下的工业屏幕颜色还会失真。所以每一重编码都要单独钉死：
/// 光测颜色对不对没有意义，要测的是**颜色之外的那两重还在不在**。
/// </summary>
public class StatusIndicatorTests
{
    // ---- 第一重：状态伪类（颜色的载体）--------------------------------------

    [AvaloniaTheory]
    [InlineData(DeviceState.Offline, ":offline")]
    [InlineData(DeviceState.Idle, ":idle")]
    [InlineData(DeviceState.Running, ":running")]
    [InlineData(DeviceState.Warning, ":warning")]
    [InlineData(DeviceState.Fault, ":fault")]
    public void 每个状态只置一个伪类(DeviceState state, string expected)
    {
        var indicator = new StatusIndicator { State = state };

        HasPseudo(indicator, expected);

        // 同时命中两个状态伪类会让主题里的 Setter 打架，最终画成什么样取决于
        // 声明顺序——这种错误在截图上时隐时现，只能靠断言拦住。
        foreach (var other in AllStatePseudoClasses)
            if (other != expected)
                LacksPseudo(indicator, other);
    }

    [AvaloniaFact]
    public void 状态切换会清掉上一个伪类()
    {
        var indicator = new StatusIndicator { State = DeviceState.Running };
        HasPseudo(indicator, ":running");

        indicator.State = DeviceState.Fault;

        LacksPseudo(indicator, ":running");
        HasPseudo(indicator, ":fault");
    }

    [AvaloniaFact]
    public void 构造完就有伪类_不必等挂载()
    {
        // 默认 Idle。如果伪类要等模板应用才置上，控件在挂进可视树之前
        // 会有一帧是「没有任何状态」的裸样子。
        var indicator = new StatusIndicator();

        Assert.Equal(DeviceState.Idle, indicator.State);
        HasPseudo(indicator, ":idle");
    }

    // ---- 第二重：形状与字形（非颜色信号）------------------------------------

    [AvaloniaTheory]
    [InlineData(DeviceState.Warning, Symbol.Warning)]
    [InlineData(DeviceState.Fault, Symbol.Error)]
    public void 需要处置的两个状态各有自己的字形(DeviceState state, Symbol expected)
    {
        // 这是色觉障碍用户唯一能区分 warning 和 fault 的信号
        var indicator = new StatusIndicator { State = state };

        Assert.Equal(expected, indicator.Glyph);
    }

    [AvaloniaTheory]
    [InlineData(DeviceState.Offline)]
    [InlineData(DeviceState.Idle)]
    [InlineData(DeviceState.Running)]
    public void 不需要处置的状态没有字形(DeviceState state)
    {
        // 正常态挂个图标只会稀释异常态的注意力
        var indicator = new StatusIndicator { State = state };

        Assert.Equal(Symbol.None, indicator.Glyph);
    }

    [AvaloniaFact]
    public void offline_是空心圈_idle_是实心点()
    {
        // 「没有信息」和「一切正常」必须长得不一样。两个都画成灰点的话，
        // 通信断了会被读成设备待机——这是这个控件最危险的一种误读。
        var offline = MountedDot(DeviceState.Offline);
        var idle = MountedDot(DeviceState.Idle);

        Assert.True(IsTransparent(offline.Background),
            $"offline 的圆点必须是空心的，量到填充 {Describe(offline.Background)}");
        Assert.True(offline.BorderThickness.Top > 0,
            "offline 空心圈没有描边就什么都看不见了");

        Assert.False(IsTransparent(idle.Background),
            "idle 必须是实心点，否则和 offline 分不出来");
    }

    // ---- 关掉脉冲之后，形状编码必须还在 --------------------------------------

    [AvaloniaFact]
    public void running_开着脉冲时外环可见()
    {
        var pulse = MountedPulse(DeviceState.Running, pulseEnabled: true);

        Assert.True(pulse.Opacity > 0, "running 的脉冲环是它的非颜色信号，不能不可见");
    }

    [AvaloniaFact]
    public void running_关掉脉冲后外环变成静态但仍然可见()
    {
        // README 与控件注释都承诺：嵌入式上关掉脉冲后「外环还在，只是不动」。
        // 形状编码不能因为性能降级而丢失——丢了之后 running 就只剩一个绿点，
        // 和 idle 的灰点只差颜色，色觉障碍用户彻底读不出来。
        var pulse = MountedPulse(DeviceState.Running, pulseEnabled: false);

        Assert.True(pulse.Opacity > 0,
            $"关掉脉冲后外环不该消失，量到 Opacity={pulse.Opacity:F2}");

        var scale = pulse.RenderTransform as ScaleTransform;
        Assert.NotNull(scale);
        Assert.True(scale!.ScaleX > 1,
            $"静态外环要比圆点大才看得出是个环，量到 ScaleX={scale.ScaleX:F2}");
    }

    [AvaloniaFact]
    public void 非_running_状态没有外环()
    {
        // 外环是 running 专属的信号，别的状态挂上就没有区分度了
        var pulse = MountedPulse(DeviceState.Idle, pulseEnabled: true);

        Assert.Equal(0, pulse.Opacity, 2);
    }

    // ---- 第三重：文字 --------------------------------------------------------

    [AvaloniaFact]
    public void 关掉标签后文字不显示()
    {
        var indicator = new StatusIndicator
        {
            State = DeviceState.Running,
            Label = "主轴",
            ShowLabel = false,
        };

        var label = Part<TextBlock>(Mount(indicator), "PART_Label");
        Assert.False(label.IsVisible);
    }

    // ---- 辅助 ----------------------------------------------------------------

    private static readonly string[] AllStatePseudoClasses =
        [":offline", ":idle", ":running", ":warning", ":fault"];

    private static void HasPseudo(StyledElement element, string pseudoClass) =>
        Assert.Contains(pseudoClass, element.Classes);

    private static void LacksPseudo(StyledElement element, string pseudoClass) =>
        Assert.DoesNotContain(pseudoClass, element.Classes);

    /// <summary>
    /// 放进窗口跑完布局。测的是主题里真实的模板与 Style 选择器，
    /// 不是控件属性的字面值——「关掉脉冲」这类承诺兑现在 Style 里，
    /// 不挂载就验不到。
    /// </summary>
    private static StatusIndicator Mount(StatusIndicator indicator)
    {
        var window = new Window
        {
            Width = 400,
            Height = 200,
            Content = new StackPanel { Children = { indicator } },
        };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        window.Measure(new Size(400, 200));
        window.Arrange(new Rect(0, 0, 400, 200));
        Dispatcher.UIThread.RunJobs();
        return indicator;
    }

    private static Border MountedDot(DeviceState state) =>
        Part<Border>(Mount(new StatusIndicator { State = state }), "PART_Dot");

    private static Border MountedPulse(DeviceState state, bool pulseEnabled) =>
        Part<Border>(
            Mount(new StatusIndicator { State = state, IsPulseEnabled = pulseEnabled }),
            "PART_Pulse");

    private static T Part<T>(Control host, string name) where T : Control
    {
        var found = host.GetVisualDescendants().OfType<T>().FirstOrDefault(x => x.Name == name);
        Assert.NotNull(found);
        return found!;
    }

    private static bool IsTransparent(IBrush? brush) =>
        brush is null || (brush is ISolidColorBrush solid && solid.Color.A == 0);

    private static string Describe(IBrush? brush) =>
        brush is ISolidColorBrush solid ? solid.Color.ToString() : brush?.ToString() ?? "null";
}
