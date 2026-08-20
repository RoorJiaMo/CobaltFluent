using Avalonia;
using Avalonia.Styling;

namespace Cobalt.Fluent.Gallery.Infrastructure;

/// <summary>展柜右上角那两个开关的状态。</summary>
public static class GalleryState
{
    /// <summary>明暗主题切换。整个库只认 <see cref="ThemeVariant"/>，不认自定义开关。</summary>
    public static void SetTheme(bool dark)
    {
        if (Application.Current is { } app)
            app.RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light;
    }

    /// <summary>
    /// 动效 1× / 0.25×。慢放是用来肉眼确认 167ms 里到底发生了什么的，
    /// 把全局动效放慢，用来肉眼看清过渡的每一帧。
    /// </summary>
    public static void SetSlowMotion(bool slow)
    {
        if (Application.Current is not { } app) return;

        var factor = slow ? 4 : 1;
        app.Resources["ControlFasterAnimationDuration"] = TimeSpan.FromMilliseconds(83 * factor);
        app.Resources["ControlFastAnimationDuration"] = TimeSpan.FromMilliseconds(167 * factor);
        app.Resources["ControlNormalAnimationDuration"] = TimeSpan.FromMilliseconds(250 * factor);
        app.Resources["ControlSlowAnimationDuration"] = TimeSpan.FromMilliseconds(333 * factor);
    }
}
