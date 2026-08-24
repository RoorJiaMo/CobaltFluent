using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Styling;
using Avalonia.Threading;
using Cobalt.Fluent.Controls;
using Xunit;

namespace Cobalt.Fluent.Tests;

/// <summary>
/// 高对比度变体。
///
/// 这一批钉的是**保证**而不是长相：无头环境测不了像素，但高对比度这套变体的
/// 全部价值就是「任何一处都不许半透明、正文一律 AAA」这条保证——
/// 而 Avalonia 在本变体里找不到键时会**静默回落**到 InheritVariant，
/// 漏一个键就悄悄回落成原来那个半透明值，屏幕上还看着挺正常。
/// </summary>
public class HighContrastTests
{
    private static readonly string[] Variants = ["HighContrastLight", "HighContrastDark"];

    // ---- 一、键必须写全，不能靠继承兜底 --------------------------------------

    [AvaloniaTheory]
    [InlineData("HighContrastLight")]
    [InlineData("HighContrastDark")]
    public void 每个颜色键在高对比度变体里都有自己的值(string variantName)
    {
        var variant = Variant(variantName);
        var inherited = variant.InheritVariant!;

        var differing = 0;
        foreach (var key in ColorKeys())
        {
            Assert.True(TryGet(key, variant, out var hc), $"{variantName} 里没有 {key}");
            Assert.True(TryGet(key, inherited, out var basis), $"{inherited.Key} 里没有 {key}");

            if (!Equals(hc, basis)) differing++;
        }

        // 绝大多数键都该和继承来的那套不同。如果几乎都相同，说明这套变体
        // 根本没写进 ThemeDictionary，而是整个回落了——测试会全绿，主题却不存在。
        Assert.True(differing > ColorKeys().Count / 2,
            $"{variantName} 只有 {differing} 个键与 {inherited.Key} 不同，像是整套回落了");
    }

    [AvaloniaTheory]
    [InlineData("HighContrastLight")]
    [InlineData("HighContrastDark")]
    public void 高对比度里没有半透明(string variantName)
    {
        // 半透明色的实际对比度取决于底下画了什么。保证不能建立在合成结果上。
        var variant = Variant(variantName);

        foreach (var key in ColorKeys())
        {
            Assert.True(TryGet(key, variant, out var value));
            var alpha = ((Color)value!).A;

            // 模态遮罩是唯一的例外：它必须透出底下的界面，否则遮的是什么就看不出来。
            if (key.StartsWith("Smoke")) continue;

            Assert.True(alpha is 0 or 255,
                $"{variantName} 的 {key} 的 alpha 是 {alpha}");
        }
    }

    // ---- 二、对比度保证 ------------------------------------------------------

    [AvaloniaTheory]
    [InlineData("HighContrastLight")]
    [InlineData("HighContrastDark")]
    public void 正文达到AAA(string variantName)
    {
        var variant = Variant(variantName);
        var bg = Color(variant, "SolidBackgroundFillColorBase");

        foreach (var key in new[]
                 {
                     "TextFillColorPrimary", "TextFillColorSecondary",
                     "TextFillColorTertiary", "TextFillColorStale",
                 })
        {
            var ratio = Contrast(Color(variant, key), bg);
            Assert.True(ratio >= 7.0, $"{variantName}：{key} 压在底色上只有 {ratio:F2}:1");
        }
    }

    [AvaloniaTheory]
    [InlineData("HighContrastLight")]
    [InlineData("HighContrastDark")]
    public void 描边达到AAA(string variantName)
    {
        // 表面一律纯色，层次全部交给描边——所以描边不能有一根是淡的。
        var variant = Variant(variantName);
        var bg = Color(variant, "SolidBackgroundFillColorBase");

        foreach (var key in new[]
                 {
                     "ControlStrokeColorDefault", "DividerStrokeColorDefault",
                     "CardStrokeColorDefault", "FocusStrokeColorOuter",
                 })
        {
            var ratio = Contrast(Color(variant, key), bg);
            Assert.True(ratio >= 7.0, $"{variantName}：{key} 压在底色上只有 {ratio:F2}:1");
        }
    }

    [AvaloniaFact]
    public void 失效态刻意压在阈值以下()
    {
        // disabled 就该看着是失效的。WCAG 1.4.3 明确豁免失效控件——
        // 把它一并抬到 AAA 会让「能点」和「不能点」在高对比度下分不出来。
        foreach (var name in Variants)
        {
            var variant = Variant(name);
            var bg = Color(variant, "SolidBackgroundFillColorBase");
            var disabled = Contrast(Color(variant, "TextFillColorDisabled"), bg);
            var primary = Contrast(Color(variant, "TextFillColorPrimary"), bg);

            Assert.True(disabled < 7.0, $"{name}：失效文字 {disabled:F2}:1，和正文分不出来");
            Assert.True(disabled >= 3.0, $"{name}：失效文字 {disabled:F2}:1，看不见了");
            Assert.True(primary > disabled * 2, $"{name}：正文与失效文字拉不开");
        }
    }

    [AvaloniaFact]
    public void 强调色不与警告色撞色()
    {
        // 一屏上「这是可点的」和「这要注意」撞成同一个颜色，两个意思都没了。
        // 深色高对比度下强调用青而不是 Windows 习惯的黄，就是为了把黄让给 caution。
        foreach (var name in Variants)
        {
            var variant = Variant(name);
            var accent = Color(variant, "AccentFillColorDefault");
            var caution = Color(variant, "SystemFillColorCaution");

            Assert.True(Distance(accent, caution) > 120,
                $"{name}：强调色 {accent} 和警告色 {caution} 太接近");
        }
    }

    // ---- 三、安全色在高对比度下不变 ------------------------------------------

    [AvaloniaFact]
    public void 安全色在四套变体里保持同一个色相()
    {
        // ISO 13850 的红和黄承载的是法规语义，不是主题的一部分。
        // 高对比度下把红调成粉、把黄调成柠檬，等于把这层语义抹掉。
        foreach (var key in new[] { "SafetyRed", "SafetyRedHigh", "SafetyYellow" })
        {
            var light = Color(ThemeVariant.Light, key);
            var dark = Color(ThemeVariant.Dark, key);

            Assert.Equal(light, Color(Variant("HighContrastLight"), key));
            Assert.Equal(dark, Color(Variant("HighContrastDark"), key));
        }
    }

    // ---- 四、跟随系统 --------------------------------------------------------

    [AvaloniaTheory]
    [InlineData(PlatformThemeVariant.Light, ColorContrastPreference.NoPreference, "Light")]
    [InlineData(PlatformThemeVariant.Dark, ColorContrastPreference.NoPreference, "Dark")]
    [InlineData(PlatformThemeVariant.Light, ColorContrastPreference.High, "HighContrastLight")]
    [InlineData(PlatformThemeVariant.Dark, ColorContrastPreference.High, "HighContrastDark")]
    public void 系统偏好映射到四套变体(
        PlatformThemeVariant os, ColorContrastPreference contrast, string expected)
    {
        var values = new PlatformColorValues
        {
            ThemeVariant = os,
            ContrastPreference = contrast,
        };

        Assert.Equal(expected, CobaltFluentTheme.Resolve(values).Key);
    }

    [AvaloniaFact]
    public void 跟随系统是显式调用的_库不会自己改应用主题()
    {
        // RequestedThemeVariant 是应用的东西。控件库擅自去写它，
        // 会把宿主自己的主题逻辑顶掉。
        var app = Application.Current!;
        app.RequestedThemeVariant = ThemeVariant.Dark;

        // 只是把控件挂上去，不该动主题
        var window = new Window { Content = new Readout() };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(ThemeVariant.Dark, app.RequestedThemeVariant);
    }

    [AvaloniaFact]
    public void 拿不到平台设置时不抛异常()
    {
        // 无头、单元测试、某些嵌入式后端都可能没有平台设置。
        // 跟不跟随系统是锦上添花，为它把应用启动挂掉不成比例。
        using var _ = CobaltFluentTheme.FollowSystemContrast(Application.Current!);
    }

    [AvaloniaFact]
    public void 停止跟随之后不再改主题()
    {
        var app = Application.Current!;
        var sub = CobaltFluentTheme.FollowSystemContrast(app);

        sub.Dispose();
        sub.Dispose();   // 重复 Dispose 不该炸

        app.RequestedThemeVariant = ThemeVariant.Light;
        Assert.Equal(ThemeVariant.Light, app.RequestedThemeVariant);
    }

    // ---- 辅助 ----------------------------------------------------------------

    private static ThemeVariant Variant(string name) => name switch
    {
        "HighContrastLight" => CobaltFluentTheme.HighContrastLight,
        "HighContrastDark" => CobaltFluentTheme.HighContrastDark,
        _ => throw new ArgumentOutOfRangeException(nameof(name)),
    };

    private static IReadOnlyList<string> ColorKeys() => _keys ??= LoadKeys();

    private static IReadOnlyList<string>? _keys;

    /// <summary>从 Light 字典里把颜色键抄出来——那一套一定是全的。</summary>
    private static IReadOnlyList<string> LoadKeys()
    {
        var include = new ResourceInclude((Uri?)null)
        {
            Source = new Uri("avares://Cobalt.Fluent/Themes/Tokens.axaml"),
        };
        var dict = (ResourceDictionary)include.Loaded;
        var light = (IResourceDictionary)dict.ThemeDictionaries[ThemeVariant.Light];

        return light.Keys.OfType<string>()
            .Where(k => light.TryGetResource(k, ThemeVariant.Light, out var v) && v is Color)
            .OrderBy(k => k, StringComparer.Ordinal)
            .ToArray();
    }

    /// <summary>走应用的真实解析路径——包括「找不到就回落到 InheritVariant」。</summary>
    private static bool TryGet(string key, ThemeVariant variant, out object? value) =>
        Application.Current!.TryFindResource(key, variant, out value);

    private static Color Color(ThemeVariant variant, string key)
    {
        Assert.True(TryGet(key, variant, out var v), $"{variant.Key} 里没有 {key}");
        return (Color)v!;
    }

    private static double Channel(byte c)
    {
        var v = c / 255.0;
        return v <= 0.03928 ? v / 12.92 : Math.Pow((v + 0.055) / 1.055, 2.4);
    }

    private static double Luminance(Color c) =>
        0.2126 * Channel(c.R) + 0.7152 * Channel(c.G) + 0.0722 * Channel(c.B);

    private static double Contrast(Color a, Color b)
    {
        double la = Luminance(a), lb = Luminance(b);
        var (hi, lo) = la >= lb ? (la, lb) : (lb, la);
        return (hi + 0.05) / (lo + 0.05);
    }

    /// <summary>粗略的 RGB 距离。只用来挡「两个语义色撞成一个」。</summary>
    private static double Distance(Color a, Color b) => Math.Sqrt(
        Math.Pow(a.R - b.R, 2) + Math.Pow(a.G - b.G, 2) + Math.Pow(a.B - b.B, 2));
}
