using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Cobalt.Fluent.Controls;
using Xunit;

namespace Cobalt.Fluent.Tests;

/// <summary>
/// NumericKeypad 的回归测试。这里每一条都对应一个真实发生过的缺陷——
/// 对抗性复核在无头 Avalonia 上跑出来的，不是设想出来的边界。
/// 删掉任何一条，对应的坑都会原样回来。
/// </summary>
public class NumericKeypadRegressionTests
{
    // ---- 赋同值不触发通知，「首键替换」会失效 --------------------------------

    [AvaloniaFact]
    public void 重新装入相同的值仍然回到首键替换()
    {
        // Avalonia 对相等的新值不发变更通知，光写 Text 时 OnTextChanged 不触发，
        // _pristine 会停在上一次编辑留下的 false。
        var pad = new NumericKeypad();
        pad.Append("9");
        pad.Append("5");
        Assert.Equal("95", pad.Text);

        pad.LoadValue("95");        // 同值
        pad.Append("7");

        Assert.Equal("7", pad.Text);
    }

    [AvaloniaFact]
    public void 换到待下发文本相同的宿主后首键仍是替换()
    {
        // 一块键盘轮流服务多个参数是工业面板的常态。两个参数的待下发文本恰好相同时，
        // 上一个参数的残留缓冲会被带进下一个参数的设定值——这是错值下发。
        var a = new ParameterRow { Minimum = 0, Maximum = 200, Format = "F1", Setpoint = 85 };
        var b = new ParameterRow { Minimum = 0, Maximum = 200, Format = "F1", Setpoint = 85 };

        var pad = new NumericKeypad { Target = a };
        pad.Append("1");
        pad.Append("2");
        Assert.Equal("12", pad.Text);

        b.PendingText = "12";       // 让两边缓冲撞上
        pad.Target = b;
        pad.Append("9");

        Assert.Equal("9", pad.Text);
    }

    [AvaloniaFact]
    public void 确认之后回到首键替换()
    {
        // 键盘不会自动收起，缓冲里剩的就是「当前值」，接着输下一个值不该拼在后面
        var pad = new NumericKeypad { Minimum = 0, Maximum = 200 };
        pad.Append("9");
        pad.Append("5");
        pad.Commit();

        pad.Append("1");

        Assert.Equal("1", pad.Text);
    }

    // ---- 宿主拒收 ------------------------------------------------------------

    [AvaloniaFact]
    public void 宿主正在下发时确认既不抛事件也不弄脏宿主()
    {
        // ParameterRow 进了 Writing 就在等回读，此时 Apply 是空操作。
        // 键盘若照抛 Committed，界面会显示「已下发」而设备根本没收到。
        var row = new ParameterRow { Minimum = 0, Maximum = 200, Format = "F1", Setpoint = 85 };
        var pad = new NumericKeypad { Target = row };

        pad.Append("9");
        pad.Append("5");
        pad.Commit();                       // 第一次：正常进 Writing
        Assert.Equal(ParameterWriteState.Writing, row.WriteState);

        var pending = row.PendingText;
        var fired = 0;
        pad.Committed += (_, _) => fired++;

        pad.Clear();
        pad.Append("1");
        pad.Append("2");
        pad.Append("0");
        pad.Commit();                       // 第二次：宿主还在等回读

        Assert.Equal(0, fired);
        Assert.Equal(pending, row.PendingText);
    }

    [AvaloniaFact]
    public void 只读宿主上确认既不抛事件也不弄脏宿主()
    {
        var row = new ParameterRow
        {
            Minimum = 0, Maximum = 200, Format = "F1", Setpoint = 85, IsReadOnly = true,
        };
        var pending = row.PendingText;
        var pad = new NumericKeypad { Target = row };
        var fired = 0;
        pad.Committed += (_, _) => fired++;

        pad.Append("9");
        pad.Append("5");
        pad.Commit();

        Assert.Equal(0, fired);
        Assert.Equal(pending, row.PendingText);
    }

    // ---- 非有限值 ------------------------------------------------------------

    [AvaloniaTheory]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    [InlineData("-Infinity")]
    [InlineData("1e400")]
    public void 非有限值一律当作解析失败(string text)
    {
        // NumberStyles.Float 接受这些写法，而 NaN 与任何数比较恒为 false，
        // 会从量程判定底下整个穿过去，带着 NaN 抛给应用侧。
        var pad = new NumericKeypad { Minimum = 20, Maximum = 120, Text = text };

        Assert.Null(pad.Value);
        Assert.False(pad.CanCommit);
    }

    [AvaloniaFact]
    public void 边界是_NaN_时一律拒绝提交()
    {
        // 量程判定必须失败即拒：写成 Value < Minimum 的话 NaN 边界会放行一切
        var pad = new NumericKeypad { Minimum = double.NaN, Maximum = double.NaN, Text = "999" };

        Assert.False(pad.CanCommit);
    }

    // ---- 解绑 ----------------------------------------------------------------

    [AvaloniaFact]
    public void 解绑宿主后回到独立状态()
    {
        var row = new ParameterRow
        {
            Label = "腔体温度", Unit = "°C", Minimum = 20, Maximum = 120, Format = "F1", Setpoint = 85,
        };
        var pad = new NumericKeypad { Target = row };
        Assert.Equal(20, pad.Minimum);

        pad.Target = null;

        Assert.True(double.IsNegativeInfinity(pad.Minimum), "解绑后还留着旧宿主的下限");
        Assert.True(double.IsPositiveInfinity(pad.Maximum), "解绑后还留着旧宿主的上限");
        Assert.Null(pad.Label);
        Assert.Null(pad.Unit);
        Assert.Null(pad.RangeText);
    }

    [AvaloniaFact]
    public void 从未挂过宿主时置空不会动作者写的量程()
    {
        // XAML 里声明的量程不是宿主留下的，不能被清掉
        var pad = new NumericKeypad { Minimum = 0, Maximum = 10 };

        pad.Target = null;

        Assert.Equal(0, pad.Minimum);
        Assert.Equal(10, pad.Maximum);
    }

    // ---- 修饰键 --------------------------------------------------------------

    [AvaloniaTheory]
    [InlineData(KeyModifiers.Control)]
    [InlineData(KeyModifiers.Alt)]
    [InlineData(KeyModifiers.Meta)]
    public void 带修饰键的回车不会下发(KeyModifiers modifiers)
    {
        // 一个修饰键组合直接触发对设备的写值是不可接受的；
        // 同时应用级快捷键也不该被这个控件无声吞掉。
        var row = new ParameterRow { Minimum = 0, Maximum = 200, Format = "F1", Setpoint = 85 };
        var pad = new NumericKeypad { Target = row };
        pad.Append("9");
        pad.Append("5");

        var fired = 0;
        pad.Committed += (_, _) => fired++;

        var args = new KeyEventArgs
        {
            RoutedEvent = InputElement.KeyDownEvent,
            Key = Key.Enter,
            KeyModifiers = modifiers,
        };
        pad.RaiseEvent(args);

        Assert.Equal(0, fired);
        Assert.False(args.Handled, "组合键必须放行给应用，不能吞掉");
        Assert.Equal(ParameterWriteState.Clean, row.WriteState);
    }

    [AvaloniaFact]
    public void 带修饰键的数字不会改缓冲()
    {
        var pad = new NumericKeypad { Text = "85" };

        pad.RaiseEvent(new KeyEventArgs
        {
            RoutedEvent = InputElement.KeyDownEvent,
            Key = Key.D1,
            KeyModifiers = KeyModifiers.Control,
        });

        Assert.Equal("85", pad.Text);
    }

    [AvaloniaFact]
    public void 裸回车正常确认()
    {
        var pad = new NumericKeypad { Minimum = 0, Maximum = 200, Text = "95" };
        var fired = 0;
        pad.Committed += (_, _) => fired++;

        var args = new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter };
        pad.RaiseEvent(args);

        Assert.Equal(1, fired);
        Assert.True(args.Handled);
    }

    // ---- 长度闸 --------------------------------------------------------------

    [AvaloniaFact]
    public void 小数点与符号键同样受最大长度约束()
    {
        // 长度闸只写在数字分支时，这两个键各自能再突破一位
        var pad = new NumericKeypad { MaxLength = 4 };
        foreach (var d in "1234") pad.Append(d.ToString());
        Assert.Equal("1234", pad.Text);

        pad.Append(".");
        Assert.Equal("1234", pad.Text);

        pad.ToggleSign();
        Assert.Equal("1234", pad.Text);
    }

    [AvaloniaFact]
    public void 超长缓冲仍然可以退格()
    {
        // 外部灌进来一个超长值时不能把人锁死在里面
        var pad = new NumericKeypad { MaxLength = 3, Text = "123456" };

        pad.Backspace();

        Assert.Equal("12345", pad.Text);
    }

    // ---- 符号键 --------------------------------------------------------------

    [AvaloniaFact]
    public void 禁止负值时仍可把已有负值改成正值()
    {
        // 禁止输入负值没错，但去掉负号是往合法方向走，挡住只会逼操作员退格重输
        var pad = new NumericKeypad { AllowNegative = false, Text = "-20" };

        pad.ToggleSign();

        Assert.Equal("20", pad.Text);
    }

    // ---- 提示位 --------------------------------------------------------------

    [AvaloniaFact]
    public void 空缓冲不占用提示位()
    {
        // 还没开始输就常驻一行「未输入」是噪音，模板注释也是这么写的
        var pad = new NumericKeypad();

        Assert.Null(pad.ValidationText);
        Assert.False(pad.CanCommit);
        Assert.Contains(":empty", pad.Classes);
    }

    // ---- 端到端：真的走模板 --------------------------------------------------

    [AvaloniaFact]
    public void 点击模板里的按键真的会改缓冲()
    {
        // 25 项基础测试全是直接调方法，一条都没走过模板。
        // PART 名字拼错、接线漏掉，那些测试一个都抓不到。
        var pad = Mount(new NumericKeypad { Minimum = 0, Maximum = 200, Text = "85" });

        Click(pad, "PART_Digit7");
        Assert.Equal("7", pad.Text);

        Click(pad, "PART_Digit3");
        Assert.Equal("73", pad.Text);

        Click(pad, "PART_Backspace");
        Assert.Equal("7", pad.Text);

        Click(pad, "PART_Clear");
        Assert.Equal("", pad.Text);
    }

    [AvaloniaFact]
    public void 点过按键之后物理回车仍然能确认()
    {
        // 键如果可聚焦，点过之后焦点落在那个 Button 上，
        // 物理 Enter 被 Button 自己转成 Click，永远到不了 NumericKeypad.OnKeyDown。
        var pad = Mount(new NumericKeypad { Minimum = 0, Maximum = 200, Text = "85" });
        var fired = 0;
        pad.Committed += (_, _) => fired++;

        Click(pad, "PART_Digit9");
        Click(pad, "PART_Digit5");

        var args = new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter };
        pad.RaiseEvent(args);

        Assert.Equal(1, fired);
    }

    [AvaloniaFact]
    public void 模板里的按键不参与焦点()
    {
        var pad = Mount(new NumericKeypad());

        foreach (var button in pad.GetVisualDescendants().OfType<Button>())
            Assert.False(button.Focusable, $"{button.Name} 不该抢焦点");
    }

    [AvaloniaFact]
    public void 键盘整体可聚焦()
    {
        Assert.True(Mount(new NumericKeypad()).Focusable);
    }

    [AvaloniaFact]
    public void 小数点键面与实际插入的分隔符一致()
    {
        var pad = Mount(new NumericKeypad());
        var key = pad.GetVisualDescendants().OfType<Button>().First(b => b.Name == "PART_Decimal");

        pad.Append(pad.DecimalSeparatorText);

        Assert.Equal(pad.DecimalSeparatorText, key.Content);
        Assert.Contains(pad.DecimalSeparatorText, pad.Text!);
    }

    // ---- 辅助 ----------------------------------------------------------------

    private static NumericKeypad Mount(NumericKeypad pad)
    {
        var window = new Window
        {
            Width = 600,
            Height = 700,
            Content = new StackPanel { Children = { pad } },
        };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        window.Measure(new Size(600, 700));
        window.Arrange(new Rect(0, 0, 600, 700));
        Dispatcher.UIThread.RunJobs();
        return pad;
    }

    private static void Click(NumericKeypad pad, string partName)
    {
        var button = pad.GetVisualDescendants().OfType<Button>().FirstOrDefault(b => b.Name == partName);
        Assert.NotNull(button);
        button!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    }
}
