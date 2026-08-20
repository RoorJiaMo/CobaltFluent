using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Headless.XUnit;
using Cobalt.Fluent.Controls;
using Xunit;

namespace Cobalt.Fluent.Tests;

/// <summary>
/// 第 7 组那几个控件的安全语义。
///
/// 这些不是「样式对不对」的问题——写错了设备会失控。它们是第 7 组的硬约束，
/// 所以这里逐条钉死：改坏了 CI 就红。
/// </summary>
public class SafetyBehaviourTests
{
    // ---- JogButton：任何一种失去控制都必须停 --------------------------------

    [AvaloniaTheory]
    [InlineData(JogStopReason.PointerReleased)]
    [InlineData(JogStopReason.PointerCaptureLost)]
    [InlineData(JogStopReason.PointerExited)]
    [InlineData(JogStopReason.LostFocus)]
    [InlineData(JogStopReason.KeyReleased)]
    [InlineData(JogStopReason.Detached)]
    [InlineData(JogStopReason.Watchdog)]
    public void JogButton_每一个停止触发点都能停下来(JogStopReason reason)
    {
        var jog = new JogButton();
        JogStopReason? observed = null;
        jog.JogStopped += (_, e) => observed = e.Reason;

        StartJogging(jog);
        Assert.True(jog.IsJogging, "起步就没动起来，后面的断言没有意义");

        jog.Stop(reason);

        Assert.False(jog.IsJogging);
        Assert.Equal(reason, observed);
        LacksPseudo(jog, ":jogging");
    }

    [AvaloniaFact]
    public void JogButton_停止是幂等的()
    {
        // 六个触发点可能同时命中，下游必须只收到一次停止
        var jog = new JogButton();
        var stops = 0;
        jog.JogStopped += (_, _) => stops++;

        StartJogging(jog);
        jog.Stop(JogStopReason.PointerReleased);
        jog.Stop(JogStopReason.PointerCaptureLost);
        jog.Stop(JogStopReason.LostFocus);

        Assert.Equal(1, stops);
    }

    [AvaloniaFact]
    public void JogButton_被禁用时自动停()
    {
        var jog = new JogButton();
        StartJogging(jog);

        jog.IsEnabled = false;

        Assert.False(jog.IsJogging);
    }

    /// <summary>伪类断言。直接对 Classes 集合断言，xUnit 的分析器才不会报 xUnit2017。</summary>
    private static void HasPseudo(Avalonia.StyledElement element, string pseudoClass) =>
        Assert.Contains(pseudoClass, element.Classes);

    private static void LacksPseudo(Avalonia.StyledElement element, string pseudoClass) =>
        Assert.DoesNotContain(pseudoClass, element.Classes);

    private static void StartJogging(JogButton jog)
    {
        // Start 是私有的（只该由输入事件触发），这里走键盘路径，
        // 和操作员按空格是同一条路。
        jog.RaiseEvent(new Avalonia.Input.KeyEventArgs
        {
            RoutedEvent = InputElement.KeyDownEvent,
            Key = Avalonia.Input.Key.Space,
        });
    }

    // ---- EStopButton：按下即触发、触发即自锁 --------------------------------

    [AvaloniaFact]
    public void EStopButton_触发后自锁_再次触发无效()
    {
        var estop = new EStopButton();
        var engaged = 0;
        estop.Engaged += (_, _) => engaged++;

        estop.Engage();
        estop.Engage();

        Assert.True(estop.IsEngaged);
        Assert.Equal(1, engaged);
        HasPseudo(estop, ":engaged");
    }

    [AvaloniaFact]
    public void EStopButton_复位后回到就绪()
    {
        var estop = new EStopButton();
        var released = 0;
        estop.Released += (_, _) => released++;

        estop.Engage();
        estop.Reset();

        Assert.False(estop.IsEngaged);
        Assert.Equal(1, released);
        LacksPseudo(estop, ":engaged");
    }

    [AvaloniaFact]
    public void EStopButton_没触发时复位是空操作()
    {
        var estop = new EStopButton();
        var released = 0;
        estop.Released += (_, _) => released++;

        estop.Reset();

        Assert.Equal(0, released);
    }

    // ---- ParameterRow：下发成功要填回读值，不是输入值 -----------------------

    [AvaloniaFact]
    public void ParameterRow_下发成功后填的是设备回读值()
    {
        var row = new ParameterRow { Minimum = 0, Maximum = 200, Format = "F1", Setpoint = 85.0 };

        row.PendingText = "86.3";
        Assert.Equal(ParameterWriteState.Dirty, row.WriteState);
        Assert.True(row.CanApply);

        row.Apply();
        Assert.Equal(ParameterWriteState.Writing, row.WriteState);

        // 设备按 0.5 步进量化，回读 86.5
        row.CompleteWrite(86.5);

        Assert.Equal(ParameterWriteState.Clean, row.WriteState);
        Assert.Equal(86.5, row.Setpoint);
        Assert.Equal("86.5", row.PendingText);   // 不是 86.3
    }

    [AvaloniaFact]
    public void ParameterRow_下发失败回滚到上次成功值()
    {
        var row = new ParameterRow { Minimum = 0, Maximum = 200, Format = "F1", Setpoint = 85.0 };

        row.PendingText = "90.0";
        row.Apply();
        row.FailWrite();

        Assert.Equal(ParameterWriteState.Failed, row.WriteState);
        Assert.Equal("85.0", row.PendingText);
        Assert.Equal(85.0, row.Setpoint);
    }

    [AvaloniaFact]
    public void ParameterRow_超量程禁止下发()
    {
        var row = new ParameterRow { Minimum = 0, Maximum = 100, Format = "F1", Setpoint = 50 };

        row.PendingText = "120";

        Assert.Equal(ParameterWriteState.OutOfRange, row.WriteState);
        Assert.False(row.CanApply);

        row.Apply();   // 应该是空操作
        Assert.Equal(ParameterWriteState.OutOfRange, row.WriteState);
    }

    [AvaloniaFact]
    public void ParameterRow_输入解析不了也算超量程()
    {
        var row = new ParameterRow { Minimum = 0, Maximum = 100, Setpoint = 50 };

        row.PendingText = "八十五";

        Assert.Equal(ParameterWriteState.OutOfRange, row.WriteState);
        Assert.False(row.CanApply);
    }

    // ---- Readout：过期时保留最后已知值 --------------------------------------

    [AvaloniaFact]
    public void Readout_过期时保留最后已知值而不是显示破折号()
    {
        var readout = new Readout
        {
            Format = "F1",
            StaleAfter = TimeSpan.FromSeconds(1),
            Value = 84.9,
            LastUpdated = DateTime.Now.AddSeconds(-30),
        };

        Assert.True(readout.IsStale);
        Assert.Equal("84.9", readout.DisplayValue);   // 通信断了，但设备上的反应还在跑
        Assert.Equal("数据过期", readout.StaleText);
    }

    [AvaloniaFact]
    public void Readout_从未取到值才显示破折号()
    {
        var readout = new Readout { Value = null };

        Assert.Equal("—", readout.DisplayValue);
        HasPseudo(readout, ":nodata");
    }

    [AvaloniaFact]
    public void Readout_偏离容差带进_deviating()
    {
        var readout = new Readout
        {
            Setpoint = 85,
            Tolerance = 1.0,
            StaleAfter = TimeSpan.Zero,   // 关掉过期判断，只测偏差
            Value = 87.5,
        };

        HasPseudo(readout, ":deviating");

        readout.Value = 85.4;
        LacksPseudo(readout, ":deviating");
    }

    // ---- AlarmBanner：安全级别不跟随主题 + 降级时补强 -----------------------

    [AvaloniaFact]
    public void AlarmBanner_只有未确认的_Alarm_才呼吸()
    {
        var banner = new AlarmBanner { Severity = AlarmSeverity.Alarm };
        HasPseudo(banner, ":breathing");

        banner.Acknowledge();
        LacksPseudo(banner, ":breathing");
        HasPseudo(banner, ":acknowledged");
    }

    [AvaloniaFact]
    public void AlarmBanner_关掉呼吸后不再呼吸()
    {
        // 关掉动画时模板会补一圈安全黄描边，否则降级后 Alarm 和 Warning 分不出来
        var banner = new AlarmBanner
        {
            Severity = AlarmSeverity.Alarm,
            IsBreathingEnabled = false,
        };

        LacksPseudo(banner, ":breathing");
        HasPseudo(banner, ":alarm");
    }

    [AvaloniaFact]
    public void AlarmBanner_确认是幂等的()
    {
        var banner = new AlarmBanner { Severity = AlarmSeverity.Alarm };
        var acks = 0;
        banner.Acknowledged += (_, _) => acks++;

        banner.Acknowledge();
        banner.Acknowledge();

        Assert.Equal(1, acks);
    }

    // ---- Heartbeat：不喂就停 -------------------------------------------------

    [AvaloniaFact]
    public void Heartbeat_初始是停跳的()
    {
        // 默认活着是危险的默认值：没人喂心跳时必须显示成停跳
        var heartbeat = new Heartbeat();

        Assert.True(heartbeat.IsStopped);
        HasPseudo(heartbeat, ":stopped");
    }

    [AvaloniaFact]
    public void Heartbeat_收到一次就活过来()
    {
        var heartbeat = new Heartbeat();

        heartbeat.Beat();

        Assert.False(heartbeat.IsStopped);
    }
}
