using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Cobalt.Fluent.Gallery.Infrastructure;

/// <summary>
/// 状态矩阵里 <c>:focus-visible</c> 那一格的静态替身。
///
/// 真正的聚焦框走 <c>FocusAdorner</c>，只在控件真的拿到键盘焦点时出现——
/// 一个窗口同一时刻只有一个焦点，所以矩阵里没法让每个控件都「正在聚焦」。
/// 这里按同一组 token 把两道环画出来，几何和 Themes/Shared.axaml 里的
/// FluentFocusAdorner 保持一致：控件边 → 1px 反色内环 → 2px 深色外环。
///
/// 和 SpecMatrix 里别的格子一样：它是规格，不是能操作的样品。
/// </summary>
public sealed class FocusRingHost : Decorator
{
    private const double InnerRing = 1;
    private const double OuterRing = 2;
    private const double Inset = InnerRing + OuterRing;   // 3px

    public FocusRingHost()
    {
        Padding = new Thickness(Inset);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var radius = 4.0;
        if (this.TryFindResource("ControlCornerRadius", ActualThemeVariant, out var cr) && cr is CornerRadius c)
            radius = c.TopLeft;

        Draw("FocusStrokeColorOuterBrush", OuterRing, OuterRing / 2, radius + Inset);
        Draw("FocusStrokeColorInnerBrush", InnerRing, OuterRing + InnerRing / 2, radius + InnerRing);

        void Draw(string brushKey, double thickness, double inset, double cornerRadius)
        {
            // 注意必须带 ActualThemeVariant：token 都在 ThemeDictionaries 里，
            // 不带主题变体的重载查不到，会静默返回 false。
            if (!this.TryFindResource(brushKey, ActualThemeVariant, out var res) || res is not IBrush brush)
                return;

            var rect = new Rect(Bounds.Size).Deflate(inset);
            if (rect.Width <= 0 || rect.Height <= 0) return;

            context.DrawRectangle(
                null,
                new Pen(brush, thickness),
                new RoundedRect(rect, cornerRadius));
        }
    }
}
