using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;

namespace Cobalt.Fluent.Controls;

/// <summary>
/// 图标。默认画矢量路径，不依赖 Segoe Fluent Icons —— 嵌入式 Linux 上没有那套字体，
/// 靠字体会渲染成豆腐块。
///
/// <code>
/// &lt;fc:SymbolIcon Symbol="Save" /&gt;
/// &lt;fc:SymbolIcon Symbol="Warning" FontSize="20"
///                Foreground="{DynamicResource SystemFillColorCautionBrush}" /&gt;
/// </code>
///
/// 确实装了那套字体、想用字体渲染的话，把 <see cref="UseGlyphFont"/> 打开；
/// 或者给 <see cref="Glyph"/> 一个自定义码位。
/// </summary>
public class SymbolIcon : Control
{
    /// <summary>路径表的设计尺寸。所有字形都画在 16×16 里。</summary>
    private const double DesignSize = 16d;

    public static readonly StyledProperty<Symbol> SymbolProperty =
        AvaloniaProperty.Register<SymbolIcon, Symbol>(nameof(Symbol));

    public Symbol Symbol
    {
        get => GetValue(SymbolProperty);
        set => SetValue(SymbolProperty, value);
    }

    /// <summary>自定义字形码位。给了就覆盖 <see cref="Symbol"/> 自带的那个。</summary>
    public static readonly StyledProperty<string?> GlyphProperty =
        AvaloniaProperty.Register<SymbolIcon, string?>(nameof(Glyph));

    public string? Glyph
    {
        get => GetValue(GlyphProperty);
        set => SetValue(GlyphProperty, value);
    }

    /// <summary>
    /// 用字体而不是矢量路径渲染。默认关。
    /// 只有确认目标机器上装了 Segoe Fluent Icons 才打开——否则是豆腐块。
    /// </summary>
    public static readonly StyledProperty<bool> UseGlyphFontProperty =
        AvaloniaProperty.Register<SymbolIcon, bool>(nameof(UseGlyphFont));

    public bool UseGlyphFont
    {
        get => GetValue(UseGlyphFontProperty);
        set => SetValue(UseGlyphFontProperty, value);
    }

    public static readonly StyledProperty<double> FontSizeProperty =
        TextElement.FontSizeProperty.AddOwner<SymbolIcon>(
            new StyledPropertyMetadata<double>(defaultValue: 16d));

    /// <summary>图标边长。16 是控件内的默认档，CommandBar 用 16，SettingsCard 用 20。</summary>
    public double FontSize
    {
        get => GetValue(FontSizeProperty);
        set => SetValue(FontSizeProperty, value);
    }

    public static readonly StyledProperty<IBrush?> ForegroundProperty =
        TextElement.ForegroundProperty.AddOwner<SymbolIcon>();

    public IBrush? Foreground
    {
        get => GetValue(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    /// <summary>
    /// 描边粗细。按设计尺寸给，会跟着 FontSize 一起缩放，
    /// 所以放大图标时线宽比例不变。
    /// </summary>
    public static readonly StyledProperty<double> StrokeThicknessProperty =
        AvaloniaProperty.Register<SymbolIcon, double>(nameof(StrokeThickness), 1.2d);

    public double StrokeThickness
    {
        get => GetValue(StrokeThicknessProperty);
        set => SetValue(StrokeThicknessProperty, value);
    }

    static SymbolIcon()
    {
        AffectsRender<SymbolIcon>(
            SymbolProperty, GlyphProperty, UseGlyphFontProperty,
            ForegroundProperty, StrokeThicknessProperty);
        AffectsMeasure<SymbolIcon>(SymbolProperty, FontSizeProperty);
    }

    protected override Size MeasureOverride(Size availableSize) =>
        new(FontSize, FontSize);

    public override void Render(DrawingContext context)
    {
        var brush = Foreground ?? Brushes.Black;

        if (UseGlyphFont)
        {
            RenderGlyph(context, brush);
            return;
        }

        var geometry = SymbolGeometry.Get(Symbol);
        if (geometry is null) return;

        var scale = FontSize / DesignSize;
        // 图标在自己的方格里居中：控件可能比 FontSize 大（比如被拉伸）
        var offsetX = (Bounds.Width - FontSize) / 2;
        var offsetY = (Bounds.Height - FontSize) / 2;

        using var _ = context.PushTransform(
            Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(offsetX, offsetY));

        if (SymbolGeometry.IsStroked(Symbol))
        {
            var pen = new Pen(brush, StrokeThickness)
            {
                LineCap = PenLineCap.Round,
                LineJoin = PenLineJoin.Round,
            };
            context.DrawGeometry(null, pen, geometry);
        }
        else
        {
            context.DrawGeometry(brush, null, geometry);
        }
    }

    private void RenderGlyph(DrawingContext context, IBrush brush)
    {
        var text = Glyph ?? SymbolGeometry.GlyphOf(Symbol);
        if (string.IsNullOrEmpty(text)) return;

        var family = this.TryFindResource("SymbolThemeFontFamily", ActualThemeVariant, out var f)
                     && f is FontFamily ff
            ? ff
            : FontFamily.Default;

        var formatted = new FormattedText(
            text, System.Globalization.CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface(family), FontSize, brush);

        context.DrawText(formatted, new Point(
            (Bounds.Width - formatted.Width) / 2,
            (Bounds.Height - formatted.Height) / 2));
    }
}
