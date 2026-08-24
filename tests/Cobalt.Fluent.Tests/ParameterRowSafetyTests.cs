using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Cobalt.Fluent.Controls;
using Xunit;

namespace Cobalt.Fluent.Tests;

/// <summary>
/// ParameterRow 的状态机与量程闸回归测试。
///
/// 这一组每一条都对应一个实际存在过的缺陷：一头是 NaN 被原样写进设备，
/// 另一头是参数行永久停在「写入中」而操作员没有任何出路。
/// </summary>
public class ParameterRowSafetyTests
{
    // ---- 量程闸：非有限值不能穿过去 -----------------------------------------

    [AvaloniaTheory]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    [InlineData("-Infinity")]
    [InlineData("1e400")]
    public void 非有限值不能下发(string text)
    {
        // NaN 与任何数比较恒为 false，写成 v < Min || v > Max 会让它从整个闸门
        // 底下穿过去，被判成 Dirty、CanApply=true，然后原样写进 PLC 浮点点位。
        var cmd = new RecordingCommand();
        var row = new ParameterRow
        {
            Minimum = 0, Maximum = 100, Format = "F1", Setpoint = 50, ApplyCommand = cmd,
        };

        row.PendingText = text;

        Assert.False(row.CanApply);
        Assert.Equal(ParameterWriteState.OutOfRange, row.WriteState);

        row.Apply();
        Assert.Empty(cmd.Received);
    }

    [AvaloniaFact]
    public void 量程边界为_NaN_时整行拒绝下发()
    {
        // 未初始化的量程绑定很容易给出 NaN。边界一旦是 NaN，闸门整体失效——
        // 不需要任何奇怪文本，普通数字就能穿过配置好的 0–100。
        var cmd = new RecordingCommand();
        var row = new ParameterRow
        {
            Minimum = 0, Maximum = double.NaN, Format = "F1", ApplyCommand = cmd,
        };

        row.PendingText = "99999";

        Assert.False(row.CanApply);
        Assert.Equal("量程无效，禁止下发", row.StateText);

        row.Apply();
        Assert.Empty(cmd.Received);
    }

    [AvaloniaFact]
    public void 上下限颠倒时整行拒绝下发()
    {
        var row = new ParameterRow { Minimum = 100, Maximum = 0, Format = "F1" };

        row.PendingText = "50";

        Assert.False(row.CanApply);
        Assert.Equal("量程无效，禁止下发", row.StateText);
    }

    [AvaloniaFact]
    public void 边界值本身可以正常下发()
    {
        // 闸门收紧之后不能把合法的边界值也一起挡掉
        var row = new ParameterRow { Minimum = 0, Maximum = 100, Format = "F1", Setpoint = 50 };

        row.PendingText = "100";
        Assert.True(row.CanApply);

        row.PendingText = "0";
        Assert.True(row.CanApply);
    }

    // ---- 下游不受理时不能卡死在 Writing --------------------------------------

    [AvaloniaFact]
    public void 下发命令被拒时不会卡死在写入中()
    {
        // Evaluate 的第一行在 Writing 态直接 return，冻结一切重新判定；
        // CanApply 恒为 false，Apply 再也进不去，而唯一出口 CompleteWrite /
        // FailWrite 永远不会有人来调——状态机死锁，操作员只能重启界面。
        var cmd = new GateCommand { CanRun = false };
        var row = new ParameterRow
        {
            Minimum = 0, Maximum = 200, Format = "F1", Setpoint = 85, ApplyCommand = cmd,
        };
        row.PendingText = "90.0";
        Assert.True(row.CanApply);

        var accepted = row.Apply();

        Assert.False(accepted);
        Assert.NotEqual(ParameterWriteState.Writing, row.WriteState);
        Assert.True(row.CanApply, "回到可重试状态，否则操作员没有任何出路");
    }

    [AvaloniaFact]
    public void 下发命令被拒时_CommitPending_返回_false()
    {
        // 键盘据此决定要不要报「已确认」。返回 true 就是「界面说已下发、设备没收到」。
        var cmd = new GateCommand { CanRun = false };
        var row = new ParameterRow
        {
            Minimum = 0, Maximum = 200, Format = "F1", Setpoint = 85, ApplyCommand = cmd,
        };
        row.PendingText = "90.0";

        Assert.False(row.CommitPending());
    }

    [AvaloniaFact]
    public void 命令恢复之后可以重新下发()
    {
        var cmd = new GateCommand { CanRun = false };
        var row = new ParameterRow
        {
            Minimum = 0, Maximum = 200, Format = "F1", Setpoint = 85, ApplyCommand = cmd,
        };
        row.PendingText = "90.0";
        row.Apply();

        cmd.CanRun = true;
        Assert.True(row.Apply());
        Assert.Equal(ParameterWriteState.Writing, row.WriteState);
    }

    [AvaloniaFact]
    public void 没挂命令时按受理处理()
    {
        // 本控件的契约是「等设备回读再调 CompleteWrite / FailWrite」，
        // 只听事件的用法不会去设 Handled。按未受理回滚会把随后真正到达的回读吞掉。
        var row = new ParameterRow { Minimum = 0, Maximum = 200, Format = "F1", Setpoint = 85 };
        row.PendingText = "90.0";

        Assert.True(row.Apply());
        Assert.Equal(ParameterWriteState.Writing, row.WriteState);

        row.CompleteWrite(90.0);
        Assert.Equal(ParameterWriteState.Clean, row.WriteState);
        Assert.Equal(90.0, row.Setpoint);
    }

    // ---- 回读回调的状态闸 ----------------------------------------------------

    [AvaloniaFact]
    public void 迟到的成功回读会被丢弃()
    {
        // 异步设备通讯里迟到、重复、串号的应答是常态。不加闸的话它们会直接
        // 改写基准与设定值，把一个早已作废的结果盖到当前编辑上。
        var row = new ParameterRow { Minimum = 0, Maximum = 200, Format = "F1", Setpoint = 85 };
        row.PendingText = "90.0";
        Assert.Equal(ParameterWriteState.Dirty, row.WriteState);

        row.CompleteWrite(70.0);          // 上一轮超时之后才到的应答

        Assert.Equal(ParameterWriteState.Dirty, row.WriteState);
        Assert.Equal("90.0", row.PendingText);
        Assert.Equal(85, row.Setpoint);
    }

    [AvaloniaFact]
    public void 凭空的失败回调会被丢弃()
    {
        var row = new ParameterRow { Minimum = 0, Maximum = 200, Format = "F1", Setpoint = 85 };
        row.PendingText = "90.0";

        row.FailWrite("超时");

        Assert.Equal(ParameterWriteState.Dirty, row.WriteState);
        Assert.Equal("90.0", row.PendingText);
    }

    // ---- 等回读期间外部改设定值不能顶掉回滚基准 ------------------------------

    [AvaloniaFact]
    public void 写入期间外部改设定值不影响失败回滚()
    {
        // Setpoint 是 TwoWay：轮询回来的寄存器、或 VM 下发时顺手回写的乐观值，
        // 都会在等回读的窗口里改到它。让它顶掉基准之后，回滚拿到的就不是上次成功值。
        var row = new ParameterRow { Minimum = 0, Maximum = 200, Format = "F1", Setpoint = 85 };
        row.PendingText = "90.0";
        row.Apply();
        Assert.Equal(ParameterWriteState.Writing, row.WriteState);

        row.Setpoint = 90;                // VM 乐观回写
        row.FailWrite("通讯超时");

        Assert.Equal(ParameterWriteState.Failed, row.WriteState);
        Assert.Equal("85.0", row.PendingText);
        Assert.Equal(85, row.Setpoint);   // 两个公开属性不能互相矛盾
    }

    // ---- 赋同值不触发通知 ----------------------------------------------------

    [AvaloniaFact]
    public void 重新装入相同的设定值仍会重同步输入框()
    {
        // Avalonia 对相等的新值不发变更通知，只写 Setpoint 时 OnSetpointChanged
        // 根本不跑，「切了配方输入框跟着走」在这条路径上静默失效——
        // 框里会留着上一个配方编辑到一半的值。
        var row = new ParameterRow { Minimum = 0, Maximum = 200, Format = "F1", Setpoint = 85 };
        row.PendingText = "12";
        Assert.Equal(ParameterWriteState.Dirty, row.WriteState);

        row.LoadSetpoint(85);             // 同值

        Assert.Equal("85.0", row.PendingText);
        Assert.Equal(ParameterWriteState.Clean, row.WriteState);
        Assert.False(row.CanApply);
    }

    // ---- 只读不冒充 Clean ----------------------------------------------------

    [AvaloniaFact]
    public void 只读不会把超量程伪装成已生效()
    {
        // Clean 的定义是「输入值和已生效值一致」，而框里可以留着一个超量程的值。
        // 早退成 Clean 还会把 :outofrange 伪类一并清掉，三重提示全部消失。
        var row = new ParameterRow { Minimum = 0, Maximum = 100, Format = "F1", Setpoint = 50 };
        row.PendingText = "999";
        Assert.Equal(ParameterWriteState.OutOfRange, row.WriteState);

        row.IsReadOnly = true;

        Assert.Equal(ParameterWriteState.OutOfRange, row.WriteState);
        Assert.Equal("只读", row.StateText);
        Assert.False(row.CanApply);
        Assert.Contains(":outofrange", row.Classes);
    }

    // ---- 输入框在等回读期间要锁 ----------------------------------------------

    [AvaloniaFact]
    public void 等回读期间输入框锁住()
    {
        // Evaluate 在 Writing 态直接 return，此时改框里的字不会被重新判定，
        // 「写入中」的徽章下面就会并排出现一个从未下发、也从未校验过的数字。
        var row = new ParameterRow { Minimum = 0, Maximum = 200, Format = "F1", Setpoint = 85 };
        Assert.False(row.IsInputLocked);

        row.PendingText = "90.0";
        row.Apply();

        Assert.True(row.IsInputLocked);

        row.CompleteWrite(90.0);
        Assert.False(row.IsInputLocked);
    }

    [AvaloniaFact]
    public void 只读时输入框锁住()
    {
        Assert.True(new ParameterRow { IsReadOnly = true }.IsInputLocked);
    }

    // ---- Format 变更要重算 ---------------------------------------------------

    [AvaloniaFact]
    public void 改格式会重算状态()
    {
        // 「有没有改动」的判定和超量程徽章文字都依赖 Format。只刷读值文本的话，
        // 同样的数据会因为属性赋值顺序不同而得到不同的状态。
        var row = new ParameterRow { Minimum = 0, Maximum = 200, Format = "F1", Setpoint = 85 };
        row.PendingText = "85.4";
        Assert.Equal(ParameterWriteState.Dirty, row.WriteState);   // F1 下 85.4 ≠ 85.0

        row.Format = "F0";                // F0 下 85.4 与 85 同形

        Assert.Equal(ParameterWriteState.Clean, row.WriteState);
    }

    [AvaloniaFact]
    public void 显示精度内一致时徽章不替设备说已生效()
    {
        // 设备做了量化：写 85.0 回读 85.04。按显示精度算是一致（否则会永远
        // 判成 Dirty 再也回不到 Clean），但徽章不能声称严格生效。
        var row = new ParameterRow { Minimum = 0, Maximum = 200, Format = "F1", Setpoint = 85 };
        row.PendingText = "90.0";                 // 先造一次真实改动，否则根本进不了 Writing
        row.Apply();
        row.CompleteWrite(85.04);                 // 设备限幅并量化，回读 85.04

        Assert.Equal(ParameterWriteState.Clean, row.WriteState);
        Assert.Equal("显示精度内一致", row.StateText);
    }

    // ---- 撤销按钮必须真的存在 ------------------------------------------------

    [AvaloniaFact]
    public void 模板里有撤销按钮且点击会回滚()
    {
        // 此前模板里根本没有 PART_Revert，OnApplyTemplate 永远拿到 null，
        // Revert() 和公开属性 RevertCommand 在默认主题下从界面上完全不可达。
        var row = Mount(new ParameterRow
        {
            Minimum = 0, Maximum = 200, Format = "F1", Setpoint = 85,
        });
        row.PendingText = "120.0";
        Assert.Equal(ParameterWriteState.Dirty, row.WriteState);

        var revert = row.GetVisualDescendants().OfType<Button>()
            .FirstOrDefault(b => b.Name == "PART_Revert");
        Assert.NotNull(revert);

        revert!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        Assert.Equal("85.0", row.PendingText);
        Assert.Equal(ParameterWriteState.Clean, row.WriteState);
    }

    // ---- 辅助 ----------------------------------------------------------------

    private static ParameterRow Mount(ParameterRow row)
    {
        var window = new Window
        {
            Width = 900,
            Height = 200,
            Content = new StackPanel { Children = { row } },
        };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        window.Measure(new Size(900, 200));
        window.Arrange(new Rect(0, 0, 900, 200));
        Dispatcher.UIThread.RunJobs();
        return row;
    }

    private sealed class GateCommand : ICommand
    {
        public bool CanRun { get; set; } = true;

        public event EventHandler? CanExecuteChanged;

        public bool CanExecute(object? parameter) => CanRun;

        public void Execute(object? parameter) { }

        public void Raise() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }

    private sealed class RecordingCommand : ICommand
    {
        public List<object?> Received { get; } = [];

        public event EventHandler? CanExecuteChanged;

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) => Received.Add(parameter);

        public void Raise() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}
