using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using fc = Cobalt.Fluent.Controls;

namespace Cobalt.Fluent.Gallery.Pages;

public partial class ParameterRowPage : UserControl
{
    public ParameterRowPage()
    {
        AvaloniaXamlLoader.Load(this);

        BuildSpecTable();
        BuildPlayTable();
    }

    /// <summary>
    /// 状态矩阵：五个态并排。这里直接把控件推进各个状态，
    /// 显示的就是 ControlTheme 里真正写的那几条规则。
    /// </summary>
    private void BuildSpecTable()
    {
        var table = this.FindControl<fc.ParameterTable>("SpecTable")!;

        var clean = NewRow("腔体温度", "°C", 85.0, 85.0);

        var dirty = NewRow("进料压力", "bar", 2.4, 2.5);
        dirty.PendingText = "2.8";

        var writing = NewRow("搅拌转速", "rpm", 120, 120);
        writing.PendingText = "140";
        writing.Apply();                       // 停在 :writing，不给它完成

        var failed = NewRow("加热功率", "%", 62.0, 62.0);
        failed.PendingText = "70.0";
        failed.Apply();
        failed.FailWrite("下发失败");

        var outOfRange = NewRow("主轴转速", "rpm", 1420, 1420, min: 0, max: 3000, format: "F0");
        outOfRange.PendingText = "4200";

        var readOnly = NewRow("环境温度", "°C", 23.6, 23.6);
        readOnly.IsReadOnly = true;

        foreach (var row in new[] { clean, dirty, writing, failed, outOfRange, readOnly })
            table.Items.Add(row);
    }

    private void BuildPlayTable()
    {
        var table = this.FindControl<fc.ParameterTable>("PlayTable")!;

        // 成功：设备按 0.5 步进量化，回读值不等于输入值
        var temperature = NewRow("腔体温度", "°C", 85.0, 85.0, min: 20, max: 200);
        temperature.WriteRequested += (_, _) => Simulate(temperature, succeed: true);

        // 失败：值回滚到上次成功值
        var pressure = NewRow("进料压力", "bar", 2.4, 2.5, min: 0, max: 10);
        pressure.WriteRequested += (_, _) => Simulate(pressure, succeed: false);

        // 超量程：下发按钮禁用
        var speed = NewRow("主轴转速", "rpm", 1420, 1420, min: 0, max: 3000, format: "F0");
        speed.WriteRequested += (_, _) => Simulate(speed, succeed: true);

        foreach (var row in new[] { temperature, pressure, speed })
            table.Items.Add(row);
    }

    /// <summary>
    /// 模拟一次下发往返。真实项目里这一段是 Modbus / OPC UA 的写 + 回读。
    /// 注意成功分支填的是**回读值**：设备按 0.5 步进量化了。
    /// </summary>
    private static void Simulate(fc.ParameterRow row, bool succeed)
    {
        DispatcherTimer.RunOnce(() =>
        {
            if (!succeed)
            {
                row.FailWrite("下发失败 · 设备拒绝");
                return;
            }

            if (row.ParsePending() is not { } target) return;
            var readback = Math.Round(target * 2, MidpointRounding.AwayFromZero) / 2;
            row.CompleteWrite(readback);
        }, TimeSpan.FromMilliseconds(1400));
    }

    private static fc.ParameterRow NewRow(
        string label, string unit, double actual, double setpoint,
        double min = double.NegativeInfinity, double max = double.PositiveInfinity,
        string format = "F1") => new()
        {
            Label = label,
            Unit = unit,
            ActualValue = actual,
            Format = format,
            Minimum = min,
            Maximum = max,
            Setpoint = setpoint,
        };
}
