using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

namespace Cobalt.Fluent.Gallery;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    /// <summary>
    /// 查一个非主题作用域的资源（字阶 ControlTheme、等宽字体、CodeLineContainer 这类）。
    /// 主题色不要走这里——那些在 ThemeDictionaries 里，必须带 ActualThemeVariant 查。
    /// </summary>
    public static object? TryGet(string key) =>
        Current is { } app && app.TryGetResource(key, null, out var value) ? value : null;

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.MainWindow = new MainWindow();

        base.OnFrameworkInitializationCompleted();
    }
}
