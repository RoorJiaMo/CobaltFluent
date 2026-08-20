using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;

namespace Cobalt.Fluent.Controls;

/// <summary>
/// 卡片。半透明底 + 1px 描边 + 4px 圆角，**不加阴影**——
/// 阴影只留给悬浮层（ToolTip / Flyout / MenuFlyout / ContentDialog / TeachingTip），
/// 页面内元素一律靠描边分层。
///
/// <see cref="IsClickable"/> 打开后会响应悬停/按下。
/// 可点击的卡片在高对比度下必须有可见描边，否则操作员看不出能点——别只靠背景色区分。
/// </summary>
[PseudoClasses(":clickable")]
public class Card : ContentControl
{
    public static readonly StyledProperty<bool> IsClickableProperty =
        AvaloniaProperty.Register<Card, bool>(nameof(IsClickable));

    public bool IsClickable
    {
        get => GetValue(IsClickableProperty);
        set => SetValue(IsClickableProperty, value);
    }

    static Card()
    {
        IsClickableProperty.Changed.AddClassHandler<Card>(
            (x, e) => x.PseudoClasses.Set(":clickable", e.NewValue is true));
    }
}
