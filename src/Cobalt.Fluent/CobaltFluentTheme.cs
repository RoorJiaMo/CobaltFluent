using Avalonia;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using Avalonia.Styling;

namespace Cobalt.Fluent;

/// <summary>
/// 控件库入口。在 App.axaml 里这样接：
/// <code>
/// &lt;Application xmlns:fc="using:Cobalt.Fluent"&gt;
///   &lt;Application.Styles&gt;
///     &lt;fc:CobaltFluentTheme /&gt;
///   &lt;/Application.Styles&gt;
/// &lt;/Application&gt;
/// </code>
/// 切主题走 <c>Application.Current.RequestedThemeVariant</c>：
/// <see cref="ThemeVariant.Light"/>、<see cref="ThemeVariant.Dark"/>、
/// <see cref="ThemeVariant.Default"/>，外加本库自带的两套高对比度变体
/// <see cref="HighContrastLight"/> 与 <see cref="HighContrastDark"/>。
/// </summary>
public class CobaltFluentTheme : Styles
{
    /// <summary>
    /// 高对比度 · 浅色（纯白底、纯黑字与描边）。
    ///
    /// 这不是「把浅色主题调得更狠一点」，是另一套规则：
    /// **表面一律纯色、层次全部交给描边、任何一处都不许半透明。**
    /// 半透明色的实际对比度取决于底下画了什么，而「保证对比度」正是这套变体
    /// 存在的全部理由——保证不能建立在合成结果上。
    ///
    /// 正文一律达到 WCAG AAA（7:1）。唯一不抬到 AAA 的是安全红：
    /// 饱和红压白字或黑字都到不了 7:1，这是这个色相的物理上限，
    /// 而 ISO 13850 的红是承载语义的，不能为了凑数字改成粉色。
    /// </summary>
    public static ThemeVariant HighContrastLight { get; } =
        new(nameof(HighContrastLight), ThemeVariant.Light);

    /// <summary>高对比度 · 深色（纯黑底、纯白字与描边）。规则同 <see cref="HighContrastLight"/>。</summary>
    public static ThemeVariant HighContrastDark { get; } =
        new(nameof(HighContrastDark), ThemeVariant.Dark);

    public CobaltFluentTheme(IServiceProvider? serviceProvider = null)
    {
        AvaloniaXamlLoader.Load(serviceProvider, this);
    }

    /// <summary>
    /// 按系统的明暗与对比度偏好挑一个变体。
    /// </summary>
    public static ThemeVariant Resolve(PlatformColorValues values)
    {
        var dark = values.ThemeVariant == PlatformThemeVariant.Dark;

        if (values.ContrastPreference == ColorContrastPreference.High)
            return dark ? HighContrastDark : HighContrastLight;

        return dark ? ThemeVariant.Dark : ThemeVariant.Light;
    }

    /// <summary>
    /// 让应用跟随系统的明暗与高对比度设置，并在设置变化时跟着切。
    /// 返回值 Dispose 掉就停止跟随。
    ///
    /// <b>这是可选的，本库不会自己去改应用的主题。</b>
    /// <see cref="Application.RequestedThemeVariant"/> 是应用的东西——
    /// 一个控件库擅自去写它，会把宿主自己的主题逻辑顶掉。
    /// 需要跟随就在启动时显式调一次：
    /// <code>
    /// public override void OnFrameworkInitializationCompleted()
    /// {
    ///     CobaltFluentTheme.FollowSystemContrast(this);
    ///     base.OnFrameworkInitializationCompleted();
    /// }
    /// </code>
    /// </summary>
    public static IDisposable FollowSystemContrast(Application app)
    {
        ArgumentNullException.ThrowIfNull(app);

        // 拿不到平台设置（无头、单元测试、某些嵌入式后端）时不抛异常：
        // 跟不跟随系统是锦上添花，为它把应用启动挂掉不成比例。
        if (app.PlatformSettings is not { } settings)
            return new Unsubscribe(null, null);

        void Apply(object? _, PlatformColorValues values) =>
            app.RequestedThemeVariant = Resolve(values);

        app.RequestedThemeVariant = Resolve(settings.GetColorValues());
        settings.ColorValuesChanged += Apply;

        return new Unsubscribe(settings, Apply);
    }

    private sealed class Unsubscribe(
        IPlatformSettings? settings, EventHandler<PlatformColorValues>? handler) : IDisposable
    {
        private IPlatformSettings? _settings = settings;
        private EventHandler<PlatformColorValues>? _handler = handler;

        public void Dispose()
        {
            if (_settings is not null && _handler is not null)
                _settings.ColorValuesChanged -= _handler;

            _settings = null;
            _handler = null;
        }
    }
}
