using Avalonia;
using Avalonia.Controls;

namespace Cobalt.Fluent.Controls;

/// <summary>
/// 参数表。<see cref="ParameterRow"/> 的容器，负责表头和列宽对齐。
///
/// 列宽在 <see cref="ParameterRow"/> 的模板里写死成同一组
/// （标签 * / 读值 96 / 设定 140 / 单位 56 / 状态 auto），
/// 同一个容器里的行拿到的可用宽度相同，所以自然对齐 —— 不用每行各算各的。
/// </summary>
public class ParameterTable : ItemsControl
{
    public static readonly StyledProperty<string> LabelHeaderProperty =
        AvaloniaProperty.Register<ParameterTable, string>(nameof(LabelHeader), "参数");

    public string LabelHeader
    {
        get => GetValue(LabelHeaderProperty);
        set => SetValue(LabelHeaderProperty, value);
    }

    public static readonly StyledProperty<string> ActualHeaderProperty =
        AvaloniaProperty.Register<ParameterTable, string>(nameof(ActualHeader), "读值");

    public string ActualHeader
    {
        get => GetValue(ActualHeaderProperty);
        set => SetValue(ActualHeaderProperty, value);
    }

    public static readonly StyledProperty<string> SetpointHeaderProperty =
        AvaloniaProperty.Register<ParameterTable, string>(nameof(SetpointHeader), "设定");

    public string SetpointHeader
    {
        get => GetValue(SetpointHeaderProperty);
        set => SetValue(SetpointHeaderProperty, value);
    }

    public static readonly StyledProperty<string> UnitHeaderProperty =
        AvaloniaProperty.Register<ParameterTable, string>(nameof(UnitHeader), "单位");

    public static readonly StyledProperty<string> StateHeaderProperty =
        AvaloniaProperty.Register<ParameterTable, string>(nameof(StateHeader), "状态");

    public string UnitHeader
    {
        get => GetValue(UnitHeaderProperty);
        set => SetValue(UnitHeaderProperty, value);
    }

    public string StateHeader
    {
        get => GetValue(StateHeaderProperty);
        set => SetValue(StateHeaderProperty, value);
    }
}
