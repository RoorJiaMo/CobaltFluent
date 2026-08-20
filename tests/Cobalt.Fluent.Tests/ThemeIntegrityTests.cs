using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Styling;
using Xunit;

namespace Cobalt.Fluent.Tests;

/// <summary>
/// 变量层的不变量。这些是 docs/CONVENTIONS.md 里「校验过的不变量」那一节，
/// 翻成 Avalonia 之后同样要成立。
/// </summary>
public class ThemeIntegrityTests
{
    private static readonly string[] BothThemesRequired =
    [
        "TextFillColorPrimaryBrush", "TextFillColorSecondaryBrush",
        "TextFillColorTertiaryBrush", "TextFillColorDisabledBrush",
        "TextOnAccentFillColorPrimaryBrush", "AccentTextFillColorPrimaryBrush",
        "SolidBackgroundFillColorBaseBrush", "SolidBackgroundFillColorSecondaryBrush",
        "SolidBackgroundFillColorTertiaryBrush", "SolidBackgroundFillColorQuarternaryBrush",
        "LayerFillColorDefaultBrush", "CardBackgroundFillColorDefaultBrush",
        "CardStrokeColorDefaultBrush", "AcrylicBackgroundFillColorDefaultBrush",
        "SmokeFillColorDefaultBrush",
        "ControlFillColorDefaultBrush", "ControlFillColorSecondaryBrush",
        "ControlFillColorTertiaryBrush", "ControlFillColorDisabledBrush",
        "ControlFillColorInputActiveBrush", "ControlStrongFillColorDefaultBrush",
        "ControlSolidFillColorDefaultBrush",
        "ControlAltFillColorSecondaryBrush", "ControlAltFillColorTertiaryBrush",
        "ControlAltFillColorQuarternaryBrush",
        "SubtleFillColorSecondaryBrush", "SubtleFillColorTertiaryBrush",
        "AccentFillColorDefaultBrush", "AccentFillColorSecondaryBrush",
        "AccentFillColorTertiaryBrush", "AccentFillColorDisabledBrush",
        "ControlStrokeColorDefaultBrush", "ControlStrokeColorSecondaryBrush",
        "ControlStrokeColorOnAccentDefaultBrush", "ControlStrokeColorOnAccentSecondaryBrush",
        "ControlStrongStrokeColorDefaultBrush", "SurfaceStrokeColorDefaultBrush",
        "SurfaceStrokeColorFlyoutBrush", "DividerStrokeColorDefaultBrush",
        "FocusStrokeColorOuterBrush", "FocusStrokeColorInnerBrush",
        "SystemFillColorSuccessBrush", "SystemFillColorCautionBrush",
        "SystemFillColorCriticalBrush", "SystemFillColorNeutralBrush",
        "SystemFillColorSuccessBackgroundBrush", "SystemFillColorCautionBackgroundBrush",
        "SystemFillColorCriticalBackgroundBrush", "SystemFillColorAttentionBackgroundBrush",
        "SafetyRedBrush", "SafetyRedHighBrush", "SafetyYellowBrush",
        "TextOnSafetyFillColorPrimaryBrush",
        "ControlElevationBorderBrush", "AccentControlElevationBorderBrush",
        "TextControlElevationBorderBrush",
        "ChartSeries1Brush", "ChartSeries2Brush", "ChartSeries3Brush", "ChartSeries4Brush",
        "ChartSeries5Brush", "ChartSeries6Brush", "ChartSeries7Brush", "ChartSeries8Brush",
    ];

    [AvaloniaTheory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public void 每个资源键在明暗两套主题下都解析得到(string variantName)
    {
        var variant = variantName == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light;

        // 从 Application 查，不从游离的控件查：TryFindResource 是沿逻辑树往上找的，
        // 没挂进树的控件够不到 Application.Styles 里的资源。
        var app = Application.Current!;

        var missing = BothThemesRequired
            .Where(key => !app.TryFindResource(key, variant, out var v) || v is null)
            .ToList();

        Assert.True(missing.Count == 0, "这些键取不到值：" + string.Join(", ", missing));
    }

    [AvaloniaFact]
    public void 安全色在明暗两套主题下都是红的()
    {
        // 这一条是安全要求：急停和 Alarm 级报警不能跟随主题变成浅粉。
        foreach (var variant in new[] { ThemeVariant.Light, ThemeVariant.Dark })
        {
            var red = Resolve("SafetyRedBrush", variant);
            Assert.True(red.R > 150, $"{variant} 下 SafetyRed 的红分量只有 {red.R}");
            Assert.True(red.R > red.G * 2 && red.R > red.B * 2,
                $"{variant} 下 SafetyRed 不够红：#{red.R:X2}{red.G:X2}{red.B:X2}");
        }
    }

    [AvaloniaFact]
    public void 深色主题下的_critical_确实是浅粉_所以安全色必须另开一支()
    {
        // 反过来钉住那条约束的前提：SystemFillColorCritical 在深色主题下是浅粉，
        // 所以急停不能用它。这条断言失效就说明 token 被改了，安全色的理由要重新审。
        var critical = Resolve("SystemFillColorCriticalBrush", ThemeVariant.Dark);
        Assert.True(critical.G > 100 && critical.B > 100,
            $"深色下 critical 变成了 #{critical.R:X2}{critical.G:X2}{critical.B:X2}，" +
            "不再是浅粉——安全色单开一支的理由需要复核");
    }

    [AvaloniaFact]
    public void 圆角只有_8_和_4_两档()
    {
        var app = Application.Current!;

        Assert.True(app.TryFindResource("ControlCornerRadius", ThemeVariant.Light, out var control));
        Assert.True(app.TryFindResource("OverlayCornerRadius", ThemeVariant.Light, out var overlay));

        Assert.Equal(new CornerRadius(4), (CornerRadius)control!);
        Assert.Equal(new CornerRadius(8), (CornerRadius)overlay!);
    }

    [AvaloniaFact]
    public void 强调色在明暗两套下前景对比方向相反()
    {
        // 浅色主题强调色深、文字用白；深色主题强调色浅、文字用黑。
        // 写死白字是最常见的深色主题事故。
        var lightOnAccent = Resolve("TextOnAccentFillColorPrimaryBrush", ThemeVariant.Light);
        var darkOnAccent = Resolve("TextOnAccentFillColorPrimaryBrush", ThemeVariant.Dark);

        Assert.True(lightOnAccent.R > 200, "浅色主题下强调色上的文字应该是白的");
        Assert.True(darkOnAccent.R < 60, "深色主题下强调色上的文字应该是黑的");
    }

    private static Color Resolve(string key, ThemeVariant variant)
    {
        Assert.True(Application.Current!.TryFindResource(key, variant, out var value), $"取不到 {key}");
        var brush = Assert.IsAssignableFrom<ISolidColorBrush>(value);
        return brush.Color;
    }
}
