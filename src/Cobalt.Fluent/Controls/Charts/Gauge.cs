using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Metadata;
using Avalonia.VisualTree;
using Cobalt.Fluent.Automation;

namespace Cobalt.Fluent.Controls;

/// <summary>仪表外圈的一段阈值区带。</summary>
public class GaugeZone : AvaloniaObject
{
    public static readonly StyledProperty<double> FromProperty =
        AvaloniaProperty.Register<GaugeZone, double>(nameof(From));

    public double From
    {
        get => GetValue(FromProperty);
        set => SetValue(FromProperty, value);
    }

    public static readonly StyledProperty<double> ToProperty =
        AvaloniaProperty.Register<GaugeZone, double>(nameof(To));

    public double To
    {
        get => GetValue(ToProperty);
        set => SetValue(ToProperty, value);
    }

    /// <summary>区带语义。ok / caution / critical，取对应的状态色。</summary>
    public static readonly StyledProperty<GaugeZoneKind> KindProperty =
        AvaloniaProperty.Register<GaugeZone, GaugeZoneKind>(nameof(Kind));

    public GaugeZoneKind Kind
    {
        get => GetValue(KindProperty);
        set => SetValue(KindProperty, value);
    }
}

public enum GaugeZoneKind
{
    Ok,
    Caution,
    Critical,
}

/// <summary>
/// 环形仪表。WinUI 没有，工业上很常见。270° 扫掠。
///
/// 读数**绝对居中**在环心：用负 margin 是按某个字号手算出来的，字号一变就错位。
/// </summary>
[PseudoClasses(":deviating", ":critical")]
public class Gauge : RangeBase
{
    public static readonly StyledProperty<string?> UnitProperty =
        AvaloniaProperty.Register<Gauge, string?>(nameof(Unit));

    public string? Unit
    {
        get => GetValue(UnitProperty);
        set => SetValue(UnitProperty, value);
    }

    public static readonly StyledProperty<string> FormatProperty =
        AvaloniaProperty.Register<Gauge, string>(nameof(Format), "F0");

    public string Format
    {
        get => GetValue(FormatProperty);
        set => SetValue(FormatProperty, value);
    }

    public static readonly StyledProperty<string?> CaptionProperty =
        AvaloniaProperty.Register<Gauge, string?>(nameof(Caption));

    public string? Caption
    {
        get => GetValue(CaptionProperty);
        set => SetValue(CaptionProperty, value);
    }

    public static readonly StyledProperty<AvaloniaList<GaugeZone>> ZonesProperty =
        AvaloniaProperty.Register<Gauge, AvaloniaList<GaugeZone>>(nameof(Zones));

    [Content]
    public AvaloniaList<GaugeZone> Zones
    {
        get => GetValue(ZonesProperty);
        set => SetValue(ZonesProperty, value);
    }

    /// <summary>超过这个值走 caution 色。</summary>
    public static readonly StyledProperty<double?> CautionThresholdProperty =
        AvaloniaProperty.Register<Gauge, double?>(nameof(CautionThreshold));

    public double? CautionThreshold
    {
        get => GetValue(CautionThresholdProperty);
        set => SetValue(CautionThresholdProperty, value);
    }

    /// <summary>超过这个值走 critical 色。</summary>
    public static readonly StyledProperty<double?> CriticalThresholdProperty =
        AvaloniaProperty.Register<Gauge, double?>(nameof(CriticalThreshold));

    public double? CriticalThreshold
    {
        get => GetValue(CriticalThresholdProperty);
        set => SetValue(CriticalThresholdProperty, value);
    }

    private string _displayValue = "0";

    public static readonly DirectProperty<Gauge, string> DisplayValueProperty =
        AvaloniaProperty.RegisterDirect<Gauge, string>(nameof(DisplayValue), o => o._displayValue);

    public string DisplayValue
    {
        get => _displayValue;
        private set => SetAndRaise(DisplayValueProperty, ref _displayValue, value);
    }

    static Gauge()
    {
        ValueProperty.Changed.AddClassHandler<Gauge>((x, _) => x.Refresh());
        FormatProperty.Changed.AddClassHandler<Gauge>((x, _) => x.Refresh());
        CautionThresholdProperty.Changed.AddClassHandler<Gauge>((x, _) => x.Refresh());
        CriticalThresholdProperty.Changed.AddClassHandler<Gauge>((x, _) => x.Refresh());
    }

    public Gauge()
    {
        Zones = [];
        Minimum = 0;
        Maximum = 100;
        Refresh();
    }

    private void Refresh()
    {
        DisplayValue = Value.ToString(Format, System.Globalization.CultureInfo.CurrentCulture);

        var critical = CriticalThreshold is { } c && Value >= c;
        var caution = !critical && CautionThreshold is { } w && Value >= w;

        PseudoClasses.Set(":critical", critical);
        PseudoClasses.Set(":deviating", caution);
    }
}

/// <summary>
/// 仪表的弧线部分。单独抽出来是为了让读数用普通的居中布局压在上面 ——
/// 读数如果也画在这里，就得手算基线，字号一变就错位。
/// </summary>
public class GaugeArcs : Control
{
    /// <summary>270° 扫掠，缺口朝下。起点 135°（左下），顺时针扫 270°。</summary>
    private const double StartAngle = 135;
    private const double SweepAngle = 270;

    public static readonly StyledProperty<Gauge?> OwnerProperty =
        AvaloniaProperty.Register<GaugeArcs, Gauge?>(nameof(Owner));

    public Gauge? Owner
    {
        get => GetValue(OwnerProperty);
        set => SetValue(OwnerProperty, value);
    }

    public static readonly StyledProperty<double> ThicknessProperty =
        AvaloniaProperty.Register<GaugeArcs, double>(nameof(Thickness), 10d);

    public double Thickness
    {
        get => GetValue(ThicknessProperty);
        set => SetValue(ThicknessProperty, value);
    }

    static GaugeArcs()
    {
        AffectsRender<GaugeArcs>(OwnerProperty, ThicknessProperty);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        Owner ??= this.FindAncestorOfType<Gauge>();
        if (Owner is not null)
            Owner.PropertyChanged += (_, _) => InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (Owner is not { } gauge) return;

        var size = Math.Min(Bounds.Width, Bounds.Height);
        if (size <= 0) return;

        var center = new Point(Bounds.Width / 2, Bounds.Height / 2);
        var radius = (size - Thickness) / 2;
        if (radius <= 0) return;

        var range = gauge.Maximum - gauge.Minimum;
        var fraction = range > 0
            ? Math.Clamp((gauge.Value - gauge.Minimum) / range, 0, 1)
            : 0;

        // 底环
        var track = ChartPalette.Resolve(this, "ControlAltFillColorSecondaryBrush") ?? Brushes.LightGray;
        DrawArc(context, center, radius, StartAngle, SweepAngle,
            new Pen(track, Thickness) { LineCap = PenLineCap.Round });

        // 阈值区带画在外圈 +8px，不和值弧挤在一条线上
        foreach (var zone in gauge.Zones)
        {
            var brushKey = zone.Kind switch
            {
                GaugeZoneKind.Caution => "SystemFillColorCautionBrush",
                GaugeZoneKind.Critical => "SystemFillColorCriticalBrush",
                _ => "SystemFillColorSuccessBrush",
            };

            var zoneBrush = ChartPalette.Resolve(this, brushKey);
            if (zoneBrush is null || range <= 0) continue;

            var from = Math.Clamp((zone.From - gauge.Minimum) / range, 0, 1);
            var to = Math.Clamp((zone.To - gauge.Minimum) / range, 0, 1);
            if (to <= from) continue;

            var opacity = zone.Kind == GaugeZoneKind.Ok ? 0.35 : 0.45;
            var pen = new Pen(
                new SolidColorBrush((zoneBrush as ISolidColorBrush)?.Color ?? Colors.Gray, opacity), 3);

            DrawArc(context, center, radius + Thickness / 2 + 5,
                StartAngle + SweepAngle * from, SweepAngle * (to - from), pen);
        }

        // 值弧
        if (fraction > 0)
        {
            var valueBrush =
                gauge.Classes.Contains(":critical")
                    ? ChartPalette.Resolve(this, "SystemFillColorCriticalBrush")
                : gauge.Classes.Contains(":deviating")
                    ? ChartPalette.Resolve(this, "SystemFillColorCautionBrush")
                : ChartPalette.Resolve(this, "AccentFillColorDefaultBrush");

            DrawArc(context, center, radius, StartAngle, SweepAngle * fraction,
                new Pen(valueBrush ?? Brushes.SteelBlue, Thickness) { LineCap = PenLineCap.Round });
        }
    }

    /// <summary>角度按屏幕坐标算：0° 指向右，顺时针为正。</summary>
    private static void DrawArc(
        DrawingContext context, Point center, double radius,
        double startDegrees, double sweepDegrees, IPen pen)
    {
        if (sweepDegrees <= 0 || radius <= 0) return;

        // 一整圈画不出 ArcTo（起点等于终点），拆成两段
        if (sweepDegrees >= 360)
        {
            DrawArc(context, center, radius, startDegrees, 180, pen);
            DrawArc(context, center, radius, startDegrees + 180, 180, pen);
            return;
        }

        var start = PointOn(center, radius, startDegrees);
        var end = PointOn(center, radius, startDegrees + sweepDegrees);

        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(start, false);
            ctx.ArcTo(
                end,
                new Size(radius, radius),
                0,
                sweepDegrees > 180,
                SweepDirection.Clockwise);
            ctx.EndFigure(false);
        }

        context.DrawGeometry(null, pen, geometry);
    }

    private static Point PointOn(Point center, double radius, double degrees)
    {
        var rad = degrees * Math.PI / 180;
        return new Point(center.X + radius * Math.Cos(rad), center.Y + radius * Math.Sin(rad));
    }

    /// <summary>装饰性元素，主动退出自动化树。见 <see cref="Cobalt.Fluent.Automation.DecorativeAutomationPeer"/>。</summary>
    protected override AutomationPeer OnCreateAutomationPeer() => new DecorativeAutomationPeer(this);
}
