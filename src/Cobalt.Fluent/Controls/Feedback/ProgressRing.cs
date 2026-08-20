using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;

namespace Cobalt.Fluent.Controls;

/// <summary>
/// 转圈。Avalonia 本体没有这个控件。
///
/// 嵌入式注意：这是个常驻动画，在 Mali 这类 GPU 上一直转是持续开销。
/// 长时间加载建议改用 ProgressBar 的 indeterminate —— 那个只动 transform，代价低得多。
/// </summary>
[PseudoClasses(":indeterminate", ":determinate")]
public class ProgressRing : RangeBase
{
    public static readonly StyledProperty<bool> IsIndeterminateProperty =
        AvaloniaProperty.Register<ProgressRing, bool>(nameof(IsIndeterminate), true);

    public bool IsIndeterminate
    {
        get => GetValue(IsIndeterminateProperty);
        set => SetValue(IsIndeterminateProperty, value);
    }

    /// <summary>圆环线宽。留 0 表示按直径自动折算（约 1/10）。</summary>
    public static readonly StyledProperty<double> StrokeThicknessProperty =
        AvaloniaProperty.Register<ProgressRing, double>(nameof(StrokeThickness), 0d);

    public double StrokeThickness
    {
        get => GetValue(StrokeThicknessProperty);
        set => SetValue(StrokeThicknessProperty, value);
    }

    private double _effectiveThickness = 3;

    public static readonly DirectProperty<ProgressRing, double> EffectiveThicknessProperty =
        AvaloniaProperty.RegisterDirect<ProgressRing, double>(
            nameof(EffectiveThickness), o => o._effectiveThickness);

    /// <summary>实际用的线宽。模板绑它。</summary>
    public double EffectiveThickness
    {
        get => _effectiveThickness;
        private set => SetAndRaise(EffectiveThicknessProperty, ref _effectiveThickness, value);
    }

    private double _sweepAngle;

    public static readonly DirectProperty<ProgressRing, double> SweepAngleProperty =
        AvaloniaProperty.RegisterDirect<ProgressRing, double>(
            nameof(SweepAngle), o => o._sweepAngle);

    /// <summary>确定进度时值弧扫过的角度。模板绑它。</summary>
    public double SweepAngle
    {
        get => _sweepAngle;
        private set => SetAndRaise(SweepAngleProperty, ref _sweepAngle, value);
    }

    static ProgressRing()
    {
        IsIndeterminateProperty.Changed.AddClassHandler<ProgressRing>((x, _) => x.Refresh());
        ValueProperty.Changed.AddClassHandler<ProgressRing>((x, _) => x.Refresh());
        MinimumProperty.Changed.AddClassHandler<ProgressRing>((x, _) => x.Refresh());
        MaximumProperty.Changed.AddClassHandler<ProgressRing>((x, _) => x.Refresh());
        WidthProperty.Changed.AddClassHandler<ProgressRing>((x, _) => x.Refresh());
        StrokeThicknessProperty.Changed.AddClassHandler<ProgressRing>((x, _) => x.Refresh());
    }

    public ProgressRing()
    {
        Minimum = 0;
        Maximum = 100;
        Refresh();
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var result = base.ArrangeOverride(finalSize);
        Refresh();
        return result;
    }

    private void Refresh()
    {
        PseudoClasses.Set(":indeterminate", IsIndeterminate);
        PseudoClasses.Set(":determinate", !IsIndeterminate);

        var range = Maximum - Minimum;
        var fraction = range > 0 ? Math.Clamp((Value - Minimum) / range, 0, 1) : 0;
        SweepAngle = fraction * 360;

        if (StrokeThickness > 0)
        {
            EffectiveThickness = StrokeThickness;
            return;
        }

        // 线宽跟直径走：20 → 2、32 → 3、64 → 5
        var size = double.IsNaN(Width) ? Bounds.Width : Width;
        if (size <= 0) size = 32;
        EffectiveThickness = Math.Max(2, Math.Round(size / 11));
    }
}
