using Avalonia.Headless.XUnit;
using Cobalt.Fluent.Controls;
using Xunit;

namespace Cobalt.Fluent.Tests;

/// <summary>
/// 数字键盘的输入语义。
///
/// 这个控件的难点不在画键盘，在于三条互相拉扯的规则：允许中间态、拒绝越界、
/// 首键替换。任何一条写反，触摸屏上就会出现「这个值我根本输不进去」或者
/// 「我明明输了 150，设备上却是 120」——后者是事故。
/// </summary>
public class NumericKeypadTests
{
    // ---- 约束一：输入过程中不拦截，只在提交时闸住 ---------------------------

    [AvaloniaFact]
    public void 中间态越界也必须能输进来()
    {
        // 把 5 改成 50，中间必然经过 5。逐键校验会让 50 永远输不进去。
        var pad = new NumericKeypad { Minimum = 20, Maximum = 120 };

        pad.Append("5");
        Assert.Equal("5", pad.Text);
        Assert.False(pad.CanCommit, "5 低于下限 20，这时不该允许提交");

        pad.Append("0");
        Assert.Equal("50", pad.Text);
        Assert.True(pad.CanCommit, "50 在量程内，越过中间态之后必须能提交");
    }

    [AvaloniaFact]
    public void 越界时确认是空操作()
    {
        var pad = new NumericKeypad { Minimum = 20, Maximum = 120, Text = "150" };
        var fired = 0;
        pad.Committed += (_, _) => fired++;

        pad.Commit();

        Assert.Equal(0, fired);
        Assert.False(pad.CanCommit);
    }

    // ---- 约束二：拒绝，不静默限幅 -------------------------------------------

    [AvaloniaFact]
    public void 越界不会被悄悄限幅到边界()
    {
        // 上限 120 而操作员输了 150，把它改成 120 提交是最危险的做法：
        // 他会以为设备收到的是 150。
        var pad = new NumericKeypad { Minimum = 20, Maximum = 120, Text = "150" };
        double? committed = null;
        pad.Committed += (_, e) => committed = e.Value;

        pad.Commit();

        Assert.Null(committed);
        Assert.Equal("150", pad.Text);
        Assert.Equal(150, pad.Value);
    }

    [AvaloniaFact]
    public void 量程内确认带出解析后的值()
    {
        var pad = new NumericKeypad { Minimum = 20, Maximum = 120, Text = "85.5" };
        double? committed = null;
        pad.Committed += (_, e) => committed = e.Value;

        pad.Commit();

        Assert.Equal(85.5, committed);
    }

    // ---- 约束三：首键替换 ----------------------------------------------------

    [AvaloniaFact]
    public void 首次按数字替换整个缓冲而不是追加()
    {
        // 打开键盘时是当前值 85.0，要改成 9 不该先按五次退格
        var pad = new NumericKeypad { Text = "85.0" };

        pad.Append("9");

        Assert.Equal("9", pad.Text);
    }

    [AvaloniaFact]
    public void 首键之后恢复追加()
    {
        var pad = new NumericKeypad { Text = "85.0" };

        pad.Append("9");
        pad.Append("5");

        Assert.Equal("95", pad.Text);
    }

    [AvaloniaFact]
    public void 退格是在既有缓冲上编辑_不触发替换()
    {
        // 按退格说明操作员想改这个值，不是想重输
        var pad = new NumericKeypad { Text = "85.0" };

        pad.Backspace();

        Assert.Equal("85.", pad.Text);
    }

    [AvaloniaFact]
    public void 外部换值会重新进入首键替换()
    {
        // 换了个参数在编辑，缓冲就该整体可替换
        var pad = new NumericKeypad { Text = "85.0" };
        pad.Append("9");
        Assert.Equal("9", pad.Text);

        pad.Text = "42.0";
        pad.Append("7");

        Assert.Equal("7", pad.Text);
    }

    [AvaloniaFact]
    public void 首键按小数点补出前导零()
    {
        var pad = new NumericKeypad { Text = "85.0" };

        pad.Append(".");

        Assert.Equal("0.", pad.Text);
    }

    // ---- 输入规则 ------------------------------------------------------------

    [AvaloniaFact]
    public void 小数点只能有一个()
    {
        var pad = new NumericKeypad();
        pad.Append("1");
        pad.Append(".");
        pad.Append("5");
        pad.Append(".");

        Assert.Equal("1.5", pad.Text);
    }

    [AvaloniaFact]
    public void 不允许小数时小数点无效()
    {
        var pad = new NumericKeypad { AllowDecimal = false };
        pad.Append("1");
        pad.Append(".");

        Assert.Equal("1", pad.Text);
    }

    [AvaloniaFact]
    public void 负号是切换不是追加()
    {
        var pad = new NumericKeypad { Text = "20" };

        pad.ToggleSign();
        Assert.Equal("-20", pad.Text);

        pad.ToggleSign();
        Assert.Equal("20", pad.Text);
    }

    [AvaloniaFact]
    public void 不允许负值时切换无效()
    {
        var pad = new NumericKeypad { AllowNegative = false, Text = "20" };

        pad.ToggleSign();

        Assert.Equal("20", pad.Text);
    }

    [AvaloniaFact]
    public void 前导零会被下一个数字替换()
    {
        var pad = new NumericKeypad();
        pad.Append("0");
        pad.Append("5");

        Assert.Equal("5", pad.Text);
    }

    [AvaloniaFact]
    public void 零点后面的数字不吃掉小数点()
    {
        var pad = new NumericKeypad();
        pad.Append("0");
        pad.Append(".");
        pad.Append("5");

        Assert.Equal("0.5", pad.Text);
    }

    [AvaloniaFact]
    public void 超过最大长度不再接收()
    {
        var pad = new NumericKeypad { MaxLength = 4 };
        foreach (var d in "1234567") pad.Append(d.ToString());

        Assert.Equal("1234", pad.Text);
    }

    [AvaloniaFact]
    public void 清空之后是空缓冲且不能提交()
    {
        var pad = new NumericKeypad { Text = "85.0" };

        pad.Clear();

        Assert.Equal("", pad.Text);
        Assert.False(pad.CanCommit);
        Assert.Contains(":empty", pad.Classes);
    }

    [AvaloniaFact]
    public void 只剩负号是解析不出来的中间态()
    {
        // 不能崩，也不能当成 0 提交
        var pad = new NumericKeypad();
        pad.ToggleSign();

        Assert.Equal("-", pad.Text);
        Assert.Null(pad.Value);
        Assert.False(pad.CanCommit);
    }

    // ---- 量程提示 ------------------------------------------------------------

    [AvaloniaFact]
    public void 量程提示带上单位()
    {
        var pad = new NumericKeypad { Minimum = 20, Maximum = 120, Format = "F1", Unit = "°C" };

        Assert.Equal("20.0 – 120.0 °C", pad.RangeText);
    }

    [AvaloniaFact]
    public void 两端无界时不显示量程()
    {
        Assert.Null(new NumericKeypad().RangeText);
    }

    // ---- 可选挂接 ------------------------------------------------------------

    [AvaloniaFact]
    public void 挂接后量程与格式跟随宿主()
    {
        var row = new ParameterRow
        {
            Label = "腔体温度",
            Unit = "°C",
            Minimum = 20,
            Maximum = 120,
            Format = "F1",
            Setpoint = 85,
        };

        var pad = new NumericKeypad { Target = row };

        Assert.Equal(20, pad.Minimum);
        Assert.Equal(120, pad.Maximum);
        Assert.Equal("F1", pad.Format);
        Assert.Equal("°C", pad.Unit);
        Assert.Equal("腔体温度", pad.Label);
        Assert.Equal(row.PendingText, pad.Text);
    }

    [AvaloniaFact]
    public void 挂接后确认写回宿主并触发下发()
    {
        var row = new ParameterRow { Minimum = 20, Maximum = 120, Format = "F1", Setpoint = 85 };
        var pad = new NumericKeypad { Target = row };

        pad.Append("9");
        pad.Append("5");
        pad.Commit();

        Assert.Equal("95", row.PendingText);
        Assert.Equal(ParameterWriteState.Writing, row.WriteState);
    }

    [AvaloniaFact]
    public void 挂接后越界不会碰宿主()
    {
        // 键盘越界时必须在自己这一层就闸住，不能把脏值推给宿主
        var row = new ParameterRow { Minimum = 20, Maximum = 120, Format = "F1", Setpoint = 85 };
        var before = row.PendingText;
        var pad = new NumericKeypad { Target = row };

        pad.Append("1");
        pad.Append("5");
        pad.Append("0");
        pad.Commit();

        Assert.Equal(before, row.PendingText);
        Assert.Equal(ParameterWriteState.Clean, row.WriteState);
    }

    [AvaloniaFact]
    public void 挂接后宿主仍保留自己的回读语义()
    {
        // 键盘只负责把值送到宿主门口，限幅与量化仍由设备回读决定
        var row = new ParameterRow { Minimum = 20, Maximum = 120, Format = "F1", Setpoint = 85 };
        var pad = new NumericKeypad { Target = row };

        pad.Append("9");
        pad.Append("5");
        pad.Append(".");
        pad.Append("3");
        pad.Commit();

        row.CompleteWrite(95.5);   // 设备按 0.5 步进量化了

        Assert.Equal(95.5, row.Setpoint);
        Assert.Equal("95.5", row.PendingText);
    }

    // ---- 取消 ----------------------------------------------------------------

    [AvaloniaFact]
    public void 取消不动缓冲()
    {
        // 收起还是复位由使用方决定，键盘不替它做主
        var pad = new NumericKeypad { Text = "85.0" };
        var fired = 0;
        pad.Cancelled += (_, _) => fired++;

        pad.Cancel();

        Assert.Equal(1, fired);
        Assert.Equal("85.0", pad.Text);
    }
}
