using System.Windows.Input;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Cobalt.Fluent.Controls;
using Xunit;

namespace Cobalt.Fluent.Tests;

/// <summary>
/// 执行机构（JogButton / EStopButton）的安全语义回归测试。
///
/// 这里每一条都对应一个实际存在过的缺陷，全部由对抗性审查在无头 Avalonia 上跑出来。
/// 这两个控件直接驱动设备动作，任何一条回潮都是「界面说停了、轴还在动」
/// 或者「按住回车急停自己解锁」那一类后果。
/// </summary>
public class ActuatorSafetyTests
{
    // ---- JogButton：修饰键只能挡启动，绝不能挡停止 ---------------------------

    [AvaloniaTheory]
    [InlineData(KeyModifiers.Control)]
    [InlineData(KeyModifiers.Alt)]
    [InlineData(KeyModifiers.Meta)]
    public void 带修饰键的按键不会启动点动(KeyModifiers modifiers)
    {
        // 操作员按的是应用快捷键（Ctrl+Enter 确认、Ctrl+Space 切输入法），
        // 焦点碰巧在点动按钮上，轴就动了——而他的手不在按钮上。
        var jog = new JogButton();
        var args = KeyDown(Key.Space, modifiers);

        jog.RaiseEvent(args);

        Assert.False(jog.IsJogging);
        Assert.False(args.Handled, "组合键要放行给应用，不能吞掉");
    }

    [AvaloniaFact]
    public void 裸按键正常启动点动()
    {
        var jog = new JogButton();
        jog.RaiseEvent(KeyDown(Key.Space));

        Assert.True(jog.IsJogging);
    }

    [AvaloniaFact]
    public void 点动中途按下修饰键_松键仍然能停()
    {
        // 停止路径的判据只能比启动路径更宽。按住 Space 点动、中途按下 Ctrl，
        // 松开 Space 时事件带着 Ctrl 修饰符——这里要是也加闸，轴就停不下来了。
        var jog = new JogButton();
        jog.RaiseEvent(KeyDown(Key.Space));
        Assert.True(jog.IsJogging);

        jog.RaiseEvent(new KeyEventArgs
        {
            RoutedEvent = InputElement.KeyUpEvent,
            Key = Key.Space,
            KeyModifiers = KeyModifiers.Control,
        });

        Assert.False(jog.IsJogging, "带修饰键的松键必须照样停");
    }

    // ---- JogButton：停止指令没发出去时不能报「已停止」 -----------------------

    [AvaloniaFact]
    public void 停止指令下发失败时不报已停止()
    {
        // 下游忙 / 通讯断 / 权限不足时 CanExecute 为 false，指令一个字节都没发出去。
        // 此时清掉 :jogging 并抛 JogStopped 就是在告诉操作员「已经停了」——而轴还在动。
        var stop = new GateCommand { CanRun = true };
        var jog = new JogButton { StopCommand = stop };
        jog.RaiseEvent(KeyDown(Key.Space));
        Assert.True(jog.IsJogging);

        var stopped = 0;
        JogStopReason? failedReason = null;
        jog.JogStopped += (_, _) => stopped++;
        jog.StopFailed += (_, e) => failedReason = e.Reason;

        stop.CanRun = false;
        jog.Stop(JogStopReason.PointerReleased);

        Assert.Equal(0, stopped);
        Assert.Equal(JogStopReason.PointerReleased, failedReason);
        Assert.True(jog.IsJogging, "设备可能还在动，状态不能翻成已停止");
        Assert.Contains(":stopfailed", jog.Classes);
    }

    [AvaloniaFact]
    public void 停止指令恢复之后可以真正停下来()
    {
        var stop = new GateCommand { CanRun = false };
        var jog = new JogButton { StopCommand = stop };
        jog.RaiseEvent(KeyDown(Key.Space));
        jog.Stop(JogStopReason.PointerReleased);
        Assert.True(jog.IsJogging);

        stop.CanRun = true;
        jog.Stop(JogStopReason.PointerReleased);

        Assert.False(jog.IsJogging);
        Assert.DoesNotContain(":stopfailed", jog.Classes);
    }

    [AvaloniaFact]
    public void 没挂停止命令时按已停处理()
    {
        // 只听事件是常规用法，不能因为没人设 Handled 就整片误判成停止失败
        var jog = new JogButton();
        jog.RaiseEvent(KeyDown(Key.Space));

        jog.Stop(JogStopReason.PointerReleased);

        Assert.False(jog.IsJogging);
        Assert.DoesNotContain(":stopfailed", jog.Classes);
    }

    // ---- JogButton：启动指令没发出去时不能显示正在点动 -----------------------

    [AvaloniaFact]
    public void 启动指令被拒时不进入点动态()
    {
        var start = new GateCommand { CanRun = false };
        var jog = new JogButton { StartCommand = start };

        jog.RaiseEvent(KeyDown(Key.Space));

        Assert.False(jog.IsJogging, "指令没发出去却显示正在点动，操作员会以为轴在动");
        Assert.DoesNotContain(":jogging", jog.Classes);
    }

    // ---- JogButton：看门狗兜底之后必须先松手 ---------------------------------

    [AvaloniaFact]
    public void 看门狗停机后必须先松键才能再启动()
    {
        // 看门狗超时说明正常停止路径全都失效过一次。此时按键多半还按着，
        // OS 自动重复会立刻把轴再启动起来——看门狗就白设了。
        var jog = new JogButton();
        jog.RaiseEvent(KeyDown(Key.Space));
        jog.Stop(JogStopReason.Watchdog);
        Assert.False(jog.IsJogging);

        jog.RaiseEvent(KeyDown(Key.Space));         // 自动重复
        Assert.False(jog.IsJogging, "没松手就不该能重新启动");

        jog.RaiseEvent(KeyUp(Key.Space));           // 松开
        jog.RaiseEvent(KeyDown(Key.Space));

        Assert.True(jog.IsJogging);
    }

    // ---- JogButton：二次确认必须真的是一道门 ---------------------------------

    [AvaloniaFact]
    public void 需要确认时未确认不启动()
    {
        // RequiresConfirm 此前只有声明没有实现，打开它跟没打开一样——
        // 比没有这个属性更糟：使用方以为危险轴上了保险。
        var jog = new JogButton { RequiresConfirm = true };
        var asked = 0;
        jog.ConfirmRequired += (_, _) => asked++;

        jog.RaiseEvent(KeyDown(Key.Space));

        Assert.False(jog.IsJogging);
        Assert.Equal(1, asked);
    }

    [AvaloniaFact]
    public void 确认之后可以启动()
    {
        var jog = new JogButton { RequiresConfirm = true, IsConfirmed = true };

        jog.RaiseEvent(KeyDown(Key.Space));

        Assert.True(jog.IsJogging);
    }

    // ---- EStopButton：修饰键 -------------------------------------------------

    [AvaloniaTheory]
    [InlineData(KeyModifiers.Control)]
    [InlineData(KeyModifiers.Alt)]
    public void 带修饰键的按键不会触发急停(KeyModifiers modifiers)
    {
        var stop = new EStopButton();
        var args = KeyDown(Key.Enter, modifiers);

        stop.RaiseEvent(args);

        Assert.False(stop.IsEngaged);
        Assert.False(args.Handled);
    }

    [AvaloniaFact]
    public void 带修饰键的按键不会解锁急停()
    {
        // 这个方向更危险：一个组合键把自锁解掉
        var stop = new EStopButton { RequireHoldToReset = false };
        stop.Engage();
        Assert.True(stop.IsEngaged);

        stop.RaiseEvent(KeyDown(Key.Enter, KeyModifiers.Control));

        Assert.True(stop.IsEngaged);
    }

    // ---- EStopButton：按住不放不能自己解锁 -----------------------------------

    [AvaloniaFact]
    public void 按住不放不会因为自动重复而解锁()
    {
        // 不识别自动重复时的灾难路径：按住回车 → 第一次 KeyDown 触发 Engage()，
        // 自动重复的第二次 KeyDown 看到已 engaged，转去 BeginReset() 开始长按计时，
        // 手一直没松 → 计时走完，急停自己解锁。
        var stop = new EStopButton { RequireHoldToReset = false };   // 长按需求关掉，放大问题

        stop.RaiseEvent(KeyDown(Key.Enter));                          // 首次按下
        Assert.True(stop.IsEngaged);

        stop.RaiseEvent(KeyDown(Key.Enter));                          // 自动重复
        stop.RaiseEvent(KeyDown(Key.Enter));                          // 自动重复

        Assert.True(stop.IsEngaged, "按住不放绝不能把急停解开");
    }

    [AvaloniaFact]
    public void 松键之后再按才进入复位()
    {
        var stop = new EStopButton { RequireHoldToReset = false };
        stop.RaiseEvent(KeyDown(Key.Enter));
        Assert.True(stop.IsEngaged);

        stop.RaiseEvent(KeyUp(Key.Enter));
        stop.RaiseEvent(KeyDown(Key.Enter));

        Assert.False(stop.IsEngaged, "松开之后重新按下才算一次新的操作");
    }

    // ---- EStopButton：急停指令没下发时要有第三态 -----------------------------

    [AvaloniaFact]
    public void 急停指令下发失败时进入显式失败态()
    {
        // 回滚成「就绪」同样是假陈述——操作员确实按下了急停。
        // 必须是第三态，且文字直接把人指向硬件急停。
        var cmd = new GateCommand { CanRun = false };
        var stop = new EStopButton { EngageCommand = cmd };

        stop.Engage();

        Assert.True(stop.IsEngaged, "操作员确实按了，不能退回就绪");
        Assert.Contains(":engagefailed", stop.Classes);
        Assert.Equal(stop.EngageFailedCaption, stop.CaptionText);
    }

    [AvaloniaFact]
    public void 急停指令正常下发时不进失败态()
    {
        var cmd = new GateCommand { CanRun = true };
        var stop = new EStopButton { EngageCommand = cmd };

        stop.Engage();

        Assert.True(stop.IsEngaged);
        Assert.DoesNotContain(":engagefailed", stop.Classes);
        Assert.Equal(stop.EngagedCaption, stop.CaptionText);
    }

    [AvaloniaFact]
    public void 复位会清掉失败态()
    {
        var cmd = new GateCommand { CanRun = false };
        var stop = new EStopButton { EngageCommand = cmd };
        stop.Engage();
        Assert.Contains(":engagefailed", stop.Classes);

        stop.Reset();

        Assert.False(stop.IsEngaged);
        Assert.DoesNotContain(":engagefailed", stop.Classes);
        Assert.Equal(stop.Caption, stop.CaptionText);
    }

    // ---- 辅助 ----------------------------------------------------------------

    private static KeyEventArgs KeyDown(Key key, KeyModifiers modifiers = KeyModifiers.None) =>
        new() { RoutedEvent = InputElement.KeyDownEvent, Key = key, KeyModifiers = modifiers };

    private static KeyEventArgs KeyUp(Key key, KeyModifiers modifiers = KeyModifiers.None) =>
        new() { RoutedEvent = InputElement.KeyUpEvent, Key = key, KeyModifiers = modifiers };

    /// <summary>CanExecute 可以现场开关的命令，用来模拟下游忙 / 通讯断 / 权限不足。</summary>
    private sealed class GateCommand : ICommand
    {
        public bool CanRun { get; set; } = true;

        public int Executions { get; private set; }

        public event EventHandler? CanExecuteChanged;

        public bool CanExecute(object? parameter) => CanRun;

        public void Execute(object? parameter) => Executions++;

        public void Raise() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}
