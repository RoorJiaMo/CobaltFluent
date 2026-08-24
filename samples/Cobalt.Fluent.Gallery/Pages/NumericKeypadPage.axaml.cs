using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Cobalt.Fluent.Controls;

namespace Cobalt.Fluent.Gallery.Pages;

/// <summary>
/// 数字键盘。挂接演示走的是完整回路：键盘确认 → 写回 ParameterRow → 进 Writing
/// → 设备回读 → CompleteWrite。中间那一段「等回读」正是 ParameterRow 存在的理由，
/// 演示里必须留着，否则看起来像点一下就生效了。
/// </summary>
public partial class NumericKeypadPage : UserControl
{
    public NumericKeypadPage()
    {
        AvaloniaXamlLoader.Load(this);

        var row = this.FindControl<ParameterRow>("Row")!;
        var pad = this.FindControl<NumericKeypad>("Pad")!;
        var log = this.FindControl<TextBlock>("Log")!;
        var readback = this.FindControl<Button>("Readback")!;

        pad.Target = row;

        pad.Committed += (_, e) =>
            log.Text = $"已下发 {e.Value}，等待设备回读。";

        pad.Cancelled += (_, _) =>
            log.Text = "已取消，缓冲保持不变。";

        // 设备可能限幅或量化：写 95.3 回来 95.5。ParameterRow 显示的是回读值，
        // 不是输入值——显示输入值等于骗人。
        readback.Click += (_, _) =>
        {
            row.CompleteWrite(95.5);
            pad.LoadValue(row.PendingText);
            log.Text = "设备回读 95.5（按 0.5 步进量化），设定值已更新为回读值。";
        };
    }
}
