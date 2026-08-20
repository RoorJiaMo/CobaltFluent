using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace Cobalt.Fluent.Controls;

/// <summary>
/// 设置项。左图标 + 标题/描述 + 右侧控件，min-height 68。
/// 成组时放进 <see cref="SettingsGroup"/>：2px 缝隙、首尾圆角、中间不圆——Win11 设置的做法。
/// </summary>
public class SettingsCard : ContentControl
{
    /// <summary>左侧图标。</summary>
    public static readonly StyledProperty<Symbol> IconProperty =
        AvaloniaProperty.Register<SettingsCard, Symbol>(nameof(Icon));

    public Symbol Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    public static readonly StyledProperty<string?> HeaderProperty =
        AvaloniaProperty.Register<SettingsCard, string?>(nameof(Header));

    public string? Header
    {
        get => GetValue(HeaderProperty);
        set => SetValue(HeaderProperty, value);
    }

    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<SettingsCard, string?>(nameof(Description));

    public string? Description
    {
        get => GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }
}

/// <summary>
/// 设置项分组容器。缝隙 2px，首尾圆角、中间方角。
/// 和 <see cref="JogGroup"/> 一样，靠给子项打类实现——
/// ControlTheme 里不允许出现子代选择器。
/// </summary>
public class SettingsGroup : StackPanel
{
    private const string First = "settings-first";
    private const string Last = "settings-last";
    private const string Only = "settings-only";
    private const string Middle = "settings-middle";

    public SettingsGroup()
    {
        Spacing = 2;
        Children.CollectionChanged += (_, _) => Retag();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Retag();
    }

    private void Retag()
    {
        var visible = Children.Where(c => c.IsVisible).ToList();

        foreach (var child in Children)
            child.Classes.RemoveAll([First, Last, Only, Middle]);

        if (visible.Count == 0) return;

        if (visible.Count == 1)
        {
            visible[0].Classes.Add(Only);
            return;
        }

        visible[0].Classes.Add(First);
        visible[^1].Classes.Add(Last);
        for (var i = 1; i < visible.Count - 1; i++)
            visible[i].Classes.Add(Middle);
    }
}
