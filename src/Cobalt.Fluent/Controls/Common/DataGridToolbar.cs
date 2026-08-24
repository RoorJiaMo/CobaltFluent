using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Metadata;
using Cobalt.Fluent.Automation;

namespace Cobalt.Fluent.Controls;

/// <summary>
/// 表格工具条。左侧动作、右侧计数。
/// 计数要 tabular-nums —— 筛选时数字一直在变，比例数字会让整条右端抖动。
/// </summary>
public class DataGridToolbar : TemplatedControl
{
    public static readonly StyledProperty<AvaloniaList<Control>> ItemsProperty =
        AvaloniaProperty.Register<DataGridToolbar, AvaloniaList<Control>>(nameof(Items));

    [Content]
    public AvaloniaList<Control> Items
    {
        get => GetValue(ItemsProperty);
        set => SetValue(ItemsProperty, value);
    }

    /// <summary>右侧计数文字，如「共 1 248 条」。</summary>
    public static readonly StyledProperty<string?> CountTextProperty =
        AvaloniaProperty.Register<DataGridToolbar, string?>(nameof(CountText));

    public string? CountText
    {
        get => GetValue(CountTextProperty);
        set => SetValue(CountTextProperty, value);
    }

    public DataGridToolbar() => Items = [];

    /// <summary>见 <see cref="Cobalt.Fluent.Automation.DataGridToolbarAutomationPeer"/>。</summary>
    protected override AutomationPeer OnCreateAutomationPeer() => new DataGridToolbarAutomationPeer(this);
}
