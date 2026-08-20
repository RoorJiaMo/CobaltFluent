using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.VisualTree;

namespace Cobalt.Fluent.Controls;

/// <summary>AppBarButton 的标签位置。</summary>
public enum CommandBarLabelPosition
{
    /// <summary>图标上、文字下。CommandBar 的默认形态。</summary>
    Bottom,

    /// <summary>图标左、文字右。</summary>
    Right,

    /// <summary>只有图标。紧凑工具栏用。</summary>
    Collapsed,
}

/// <summary>
/// 命令栏。高 48，属于 base layer，所以**背景透明**——
/// 给它上底色会把两层结构（base / content）打乱。
/// </summary>
public class CommandBar : ItemsControl
{
    public static readonly StyledProperty<CommandBarLabelPosition> DefaultLabelPositionProperty =
        AvaloniaProperty.Register<CommandBar, CommandBarLabelPosition>(
            nameof(DefaultLabelPosition), CommandBarLabelPosition.Bottom);

    /// <summary>子项没单独设置时用这个。设成 Collapsed 就是纯图标工具栏。</summary>
    public CommandBarLabelPosition DefaultLabelPosition
    {
        get => GetValue(DefaultLabelPositionProperty);
        set => SetValue(DefaultLabelPositionProperty, value);
    }
}

/// <summary>命令栏里的按钮。无底色，靠 subtle 悬停反馈。</summary>
[PseudoClasses(":label-bottom", ":label-right", ":label-collapsed")]
public class AppBarButton : Button
{
    public static readonly StyledProperty<Symbol> IconProperty =
        AvaloniaProperty.Register<AppBarButton, Symbol>(nameof(Icon));

    public Symbol Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<AppBarButton, string?>(nameof(Label));

    public string? Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public static readonly StyledProperty<CommandBarLabelPosition> LabelPositionProperty =
        AvaloniaProperty.Register<AppBarButton, CommandBarLabelPosition>(
            nameof(LabelPosition), CommandBarLabelPosition.Bottom);

    public CommandBarLabelPosition LabelPosition
    {
        get => GetValue(LabelPositionProperty);
        set => SetValue(LabelPositionProperty, value);
    }

    static AppBarButton()
    {
        LabelPositionProperty.Changed.AddClassHandler<AppBarButton>((x, _) => x.Refresh());
    }

    public AppBarButton() => Refresh();

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        // 没单独设过就跟着 CommandBar 的默认值走
        if (!IsSet(LabelPositionProperty)
            && this.FindAncestorOfType<CommandBar>() is { } bar)
        {
            LabelPosition = bar.DefaultLabelPosition;
        }
    }

    private void Refresh()
    {
        var p = LabelPosition;
        PseudoClasses.Set(":label-bottom", p == CommandBarLabelPosition.Bottom);
        PseudoClasses.Set(":label-right", p == CommandBarLabelPosition.Right);
        PseudoClasses.Set(":label-collapsed", p == CommandBarLabelPosition.Collapsed);
    }
}

/// <summary>命令栏里的竖分隔线，1×24。</summary>
public class AppBarSeparator : TemplatedControl
{
}
