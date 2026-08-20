using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;

namespace Cobalt.Fluent.Controls;

/// <summary>徽章语义。和 <see cref="InfoBar"/> 用同一套。</summary>
public enum InfoSeverity
{
    /// <summary>中性提示，走强调色。</summary>
    Informational,

    Success,
    Caution,
    Critical,

    /// <summary>灰底，用于「无状态」「未启用」。</summary>
    Neutral,
}

/// <summary>
/// 小徽章。默认「浅底 + 状态色文字」，明暗两套主题下对比度都成立。
///
/// <see cref="IsSolid"/> 是实底变体，用于导航计数这类需要强提示的场景。
/// 实底变体的前景复用 <c>TextOnAccentFillColorPrimaryBrush</c>
/// （浅色主题白 / 深色主题黑）—— 状态色和强调色遵循同一套明暗翻转逻辑，
/// 写死白色的话深色主题下会糊在一起。
/// </summary>
[PseudoClasses(":informational", ":success", ":caution", ":critical", ":neutral", ":dot", ":solid")]
public class InfoBadge : TemplatedControl
{
    public static readonly StyledProperty<InfoSeverity> SeverityProperty =
        AvaloniaProperty.Register<InfoBadge, InfoSeverity>(
            nameof(Severity), InfoSeverity.Informational);

    public InfoSeverity Severity
    {
        get => GetValue(SeverityProperty);
        set => SetValue(SeverityProperty, value);
    }

    /// <summary>徽章文字。留空且 <see cref="IsDot"/> 为 false 时是一个空圆角块。</summary>
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<InfoBadge, string?>(nameof(Text));

    public string? Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    /// <summary>圆点变体：4×4，不显示文字。用于「有更新」这种不需要计数的提示。</summary>
    public static readonly StyledProperty<bool> IsDotProperty =
        AvaloniaProperty.Register<InfoBadge, bool>(nameof(IsDot));

    public bool IsDot
    {
        get => GetValue(IsDotProperty);
        set => SetValue(IsDotProperty, value);
    }

    /// <summary>实底变体。导航计数等需要强提示的场景用。</summary>
    public static readonly StyledProperty<bool> IsSolidProperty =
        AvaloniaProperty.Register<InfoBadge, bool>(nameof(IsSolid));

    public bool IsSolid
    {
        get => GetValue(IsSolidProperty);
        set => SetValue(IsSolidProperty, value);
    }

    static InfoBadge()
    {
        SeverityProperty.Changed.AddClassHandler<InfoBadge>((x, _) => x.Refresh());
        IsDotProperty.Changed.AddClassHandler<InfoBadge>((x, _) => x.Refresh());
        IsSolidProperty.Changed.AddClassHandler<InfoBadge>((x, _) => x.Refresh());
    }

    public InfoBadge() => Refresh();

    private void Refresh()
    {
        var s = Severity;
        PseudoClasses.Set(":informational", s == InfoSeverity.Informational);
        PseudoClasses.Set(":success", s == InfoSeverity.Success);
        PseudoClasses.Set(":caution", s == InfoSeverity.Caution);
        PseudoClasses.Set(":critical", s == InfoSeverity.Critical);
        PseudoClasses.Set(":neutral", s == InfoSeverity.Neutral);
        PseudoClasses.Set(":dot", IsDot);
        PseudoClasses.Set(":solid", IsSolid);
    }
}
