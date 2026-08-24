using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Cobalt.Fluent.Controls;
using Xunit;

namespace Cobalt.Fluent.Tests;

/// <summary>
/// Readout / Heartbeat / StatusIndicator / AlarmBanner 的回归测试。
///
/// 这一组的共同主题是「静默失效」：判定条件写得看似正确，
/// 但在某条真实路径上整个不成立，而且失效方向全都朝着「一切正常」。
/// </summary>
public class ReadoutSafetyTests
{
    private static readonly TimeSpan Stale = TimeSpan.FromSeconds(3);

    // ---- 新鲜度：缺时间戳不等于数据新鲜 -------------------------------------

    [AvaloniaFact]
    public void 没给时间戳时新鲜度未知而不是新鲜()
    {
        // LastUpdated 是纯手工簿记的属性，而 MVVM 里最自然的写法就是只绑 Value，
        // README 与展柜里的官方示例也都不带它。静默落到「新鲜」意味着通信断开后
        // 数值以主色停在最后一帧，和正常刷新的读数在视觉上一个像素的差别都没有。
        var readout = new Readout { Value = 85.4, StaleAfter = Stale };

        Assert.Contains(":unknownage", readout.Classes);
        Assert.DoesNotContain(":stale", readout.Classes);
        Assert.Equal("新鲜度未知", readout.StaleText);
    }

    [AvaloniaFact]
    public void 时间戳落在未来时新鲜度未知()
    {
        // 墙钟被回拨（无 RTC 的嵌入式 HMI 开机后 NTP 校时、夏令时切换、手工改表），
        // 或者时间戳来自比 HMI 快的时钟源。差值恒为负，永远不 > StaleAfter——
        // 回拨多久，就有多久所有读数被判成新鲜。
        var readout = new Readout
        {
            Value = 85.4,
            StaleAfter = Stale,
            LastUpdated = DateTime.Now.AddMinutes(5),
        };

        Assert.Contains(":unknownage", readout.Classes);
        Assert.DoesNotContain(":stale", readout.Classes);
    }

    [AvaloniaFact]
    public void 时间戳足够旧时正常判成过期()
    {
        var readout = new Readout
        {
            Value = 85.4,
            StaleAfter = Stale,
            LastUpdated = DateTime.Now.AddMinutes(-5),
        };

        Assert.True(readout.IsStale);
        Assert.Contains(":stale", readout.Classes);
        Assert.DoesNotContain(":unknownage", readout.Classes);
    }

    [AvaloniaFact]
    public void 关掉过期机制时不报新鲜度未知()
    {
        // StaleAfter=0 是显式关掉过期判定，不该被当成配置疏漏
        var readout = new Readout { Value = 85.4, StaleAfter = TimeSpan.Zero };

        Assert.DoesNotContain(":unknownage", readout.Classes);
    }

    [AvaloniaFact]
    public void 从未取到值时只报_nodata()
    {
        var readout = new Readout { StaleAfter = Stale };

        Assert.Contains(":nodata", readout.Classes);
        Assert.DoesNotContain(":unknownage", readout.Classes);
        Assert.Equal("—", readout.DisplayValue);
    }

    // ---- 坏值 ----------------------------------------------------------------

    [AvaloniaTheory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void 坏值单独成一档而不是伪装成正常读数(double bad)
    {
        // NaN 不是 null，:nodata 不会置上；而 NaN 与任何数比较恒为 false，
        // 偏差门也整个穿过去——控件对外表现为「在容差带内」，
        // DisplayValue 把 "NaN" 原样打出来，颜色却还是正常的主色。
        var readout = new Readout { Value = bad, Setpoint = 85, Tolerance = 1 };

        Assert.Contains(":invalid", readout.Classes);
        Assert.DoesNotContain(":nodata", readout.Classes);
        Assert.DoesNotContain(":deviating", readout.Classes);
        Assert.Equal("无效", readout.DisplayValue);
    }

    [AvaloniaTheory]
    [InlineData(double.NaN)]
    [InlineData(-1d)]
    public void 容差配错时显式报出而不是静默关掉偏差监视(double tolerance)
    {
        // Tolerance=-1 时 Math.Abs(...) > -1 恒真，所有值都进 :deviating；
        // NaN 则相反，恒不成立，偏差监视被静默关掉。两者都是配置错误。
        var readout = new Readout { Value = 500, Setpoint = 85, Tolerance = tolerance };

        Assert.DoesNotContain(":deviating", readout.Classes);
        Assert.Contains("偏差监视不可用", readout.StatusText!);
    }

    [AvaloniaFact]
    public void 容差正常时偏差判定照常工作()
    {
        var readout = new Readout { Value = 90, Setpoint = 85, Tolerance = 1 };

        Assert.Contains(":deviating", readout.Classes);
        Assert.Contains("偏差 +5", readout.StatusText!);
    }

    // ---- 非法格式不能把安全判定一起带停 --------------------------------------

    [AvaloniaFact]
    public void 非法格式不影响过期与偏差判定()
    {
        // 格式化此前写在 Refresh 第一行，非法 Format 让 ToString 抛 FormatException，
        // 异常被 dispatcher 吞掉不报，而坏的 Format 已经写进控件——此后每一次
        // Refresh（包括 500ms 过期定时器那一次）都停在同一行，
        // 过期判定、偏差判定、状态行全部停摆。
        var readout = new Readout
        {
            Value = 85.4,
            Setpoint = 85,
            Tolerance = 0.1,
            StaleAfter = Stale,
            LastUpdated = DateTime.Now.AddMinutes(-5),
            Format = "Q7",
        };

        Assert.True(readout.IsStale, "安全判定必须照常完成");
        Assert.Contains(":stale", readout.Classes);
        Assert.NotNull(readout.DisplayValue);
        Assert.NotEqual("—", readout.DisplayValue);
    }

    // ---- 过期时不能把判读上下文一起丢掉 --------------------------------------

    [AvaloniaFact]
    public void 过期时状态行保留断开前的偏差()
    {
        // 过期时 :deviating 被清掉（颜色没了），状态行又被整条换成「最后更新 n 秒前」
        // （文字也没了）。断开前是否超差这条判读上下文会在通信断开的瞬间
        // 从界面上彻底消失，只剩一个孤零零的数字。
        var readout = new Readout
        {
            Value = 95,
            Setpoint = 85,
            Tolerance = 1,
            StaleAfter = Stale,
            LastUpdated = DateTime.Now.AddMinutes(-5),
        };

        Assert.True(readout.IsStale);
        Assert.Contains("最后更新", readout.StatusText!);
        Assert.Contains("断开时偏差 +10", readout.StatusText!);
    }

    // ---- 预留宽度只能有一个字号来源 ------------------------------------------

    [AvaloniaFact]
    public void 预留宽度跟随主题字号而不是复制的字面值()
    {
        // 此前 C# 里抄了一份 24/40/72，覆盖 ReadoutFontSize* token 之后两份数字脱钩，
        // MinWidth 不再等于「最大位数所需宽度」，防抖动的预留失去意义。
        var small = Mount(new Readout { Size = ReadoutSize.Small, ValueMinChars = 5 });
        var large = Mount(new Readout { Size = ReadoutSize.Large, ValueMinChars = 5 });

        Assert.True(large.ValueMinWidth > small.ValueMinWidth * 2,
            $"大档应显著宽于小档，量到 {large.ValueMinWidth:F1} 与 {small.ValueMinWidth:F1}");
    }

    // ---- Heartbeat -----------------------------------------------------------

    [AvaloniaFact]
    public void 心跳默认是停跳的()
    {
        // 「默认活着」是危险的默认值：没人喂心跳时必须显示成停跳
        Assert.True(new Heartbeat().IsStopped);
    }

    [AvaloniaFact]
    public void 连续快速喂心跳时灯保持点亮()
    {
        // 此前每次 Beat 都新起一个一次性定时器、也没有句柄取消上一个，
        // 上一拍的定时器到点时会去关掉这一拍点亮的灯：
        // 只要轮询周期短于 FlashDuration，明暗节奏就跟数据流完全脱钩。
        var beat = Mount(new Heartbeat());

        beat.Beat();
        beat.Beat();
        beat.Beat();

        Assert.False(beat.IsStopped);
        Assert.Contains(":beating", beat.Classes);
    }

    [AvaloniaFact]
    public void 按真实经过时间恢复而不是把时间戳盖成现在()
    {
        // DeviceStatusBar 在模板应用时要把首次 Beat 之前丢掉的心跳补回来。
        // 走 Beat() 会把时间戳盖成「现在」，超时窗口从那一刻重新起算——
        // 链路早就断了，心跳灯还能再亮一个完整的超时周期。
        var beat = Mount(new Heartbeat { Timeout = TimeSpan.FromSeconds(2) });

        beat.Restore(TimeSpan.FromSeconds(10));   // 上一次响应是 10 秒前，早就超时了

        Assert.True(beat.IsStopped, "已经超时的恢复不能给出一个假的存活窗口");
    }

    [AvaloniaFact]
    public void 恢复一个仍在超时窗口内的心跳()
    {
        var beat = Mount(new Heartbeat { Timeout = TimeSpan.FromSeconds(30) });

        beat.Restore(TimeSpan.FromSeconds(1));

        Assert.False(beat.IsStopped);
    }

    // ---- StatusIndicator -----------------------------------------------------

    [AvaloniaFact]
    public void 枚举范围外的状态归到未连接()
    {
        // (DeviceState)plcStatusByte 这类强转在 HMI 里很常见。范围外的值会让
        // 五个伪类全部落空——没有任何 Style 命中，Glyph 也被兜成 None，
        // 三重编码三路同时失守，而且是往「正常」方向失守。
        var indicator = new StatusIndicator { State = (DeviceState)99 };

        Assert.Contains(":offline", indicator.Classes);
        Assert.Equal(Symbol.None, indicator.Glyph);
    }

    // ---- AlarmBanner ---------------------------------------------------------

    [AvaloniaFact]
    public void 降级黄环画在实色底之上()
    {
        // Panel 里后面的子元素画在上面。黄环此前排在 PART_Root 之前，
        // 被 :alarm 的 SafetyRed 实底整个盖住——伪类和 IsVisible 全是对的，
        // 渲染出来却没有环，只看伪类的测试一路绿灯。
        var banner = Mount(new AlarmBanner
        {
            Severity = AlarmSeverity.Alarm,
            IsBreathingEnabled = false,
        });

        var panel = banner.GetVisualDescendants().OfType<Panel>()
            .First(p => p.Children.Any(c => c.Name == "PART_Root"));

        var rootIndex = IndexOf(panel, "PART_Root");
        var ringIndex = IndexOf(panel, "PART_ReducedMotionRing");

        Assert.True(ringIndex > rootIndex,
            $"黄环必须排在实色底之后才画得出来，量到 ring={ringIndex} root={rootIndex}");
    }

    [AvaloniaFact]
    public void 关掉呼吸时黄环可见()
    {
        var banner = Mount(new AlarmBanner
        {
            Severity = AlarmSeverity.Alarm,
            IsBreathingEnabled = false,
        });

        var ring = banner.GetVisualDescendants().OfType<Border>()
            .First(b => b.Name == "PART_ReducedMotionRing");

        Assert.True(ring.IsVisible);
        Assert.DoesNotContain(":breathing", banner.Classes);
    }

    [AvaloniaFact]
    public void 确认命令拒收时不显示成已确认()
    {
        // 命令没执行，界面却停了呼吸、隐藏了确认按钮——
        // 一条未被受理的 Alarm 从此和普通静态红条没有任何区别。
        var cmd = new GateCommand { CanRun = false };
        var banner = new AlarmBanner { Severity = AlarmSeverity.Alarm, AcknowledgeCommand = cmd };
        var fired = 0;
        banner.Acknowledged += (_, _) => fired++;

        banner.Acknowledge();

        Assert.False(banner.IsAcknowledged);
        Assert.Equal(0, fired);
        Assert.Contains(":breathing", banner.Classes);
    }

    [AvaloniaFact]
    public void 确认命令可执行时正常确认()
    {
        var cmd = new GateCommand { CanRun = true };
        var banner = new AlarmBanner { Severity = AlarmSeverity.Alarm, AcknowledgeCommand = cmd };

        banner.Acknowledge();

        Assert.True(banner.IsAcknowledged);
        Assert.Equal(1, cmd.Executions);
    }

    [AvaloniaFact]
    public void 没挂命令时是纯本地确认()
    {
        // 类文档说的「只是操作员表示看到了」，不能因为加了拒收闸就一起挡掉
        var banner = new AlarmBanner { Severity = AlarmSeverity.Alarm };

        banner.Acknowledge();

        Assert.True(banner.IsAcknowledged);
    }

    // ---- 辅助 ----------------------------------------------------------------

    private static int IndexOf(Panel panel, string name)
    {
        for (var i = 0; i < panel.Children.Count; i++)
            if (panel.Children[i].Name == name) return i;
        return -1;
    }

    private static T Mount<T>(T control) where T : Control
    {
        var window = new Window
        {
            Width = 900,
            Height = 300,
            Content = new StackPanel { Children = { control } },
        };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        window.Measure(new Size(900, 300));
        window.Arrange(new Rect(0, 0, 900, 300));
        Dispatcher.UIThread.RunJobs();
        return control;
    }

    private sealed class GateCommand : System.Windows.Input.ICommand
    {
        public bool CanRun { get; set; } = true;

        public int Executions { get; private set; }

        public event EventHandler? CanExecuteChanged;

        public bool CanExecute(object? parameter) => CanRun;

        public void Execute(object? parameter) => Executions++;

        public void Raise() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}
