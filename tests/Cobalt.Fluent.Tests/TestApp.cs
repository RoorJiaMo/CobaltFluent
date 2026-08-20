using Avalonia;
using Avalonia.Headless;
using Avalonia.Themes.Fluent;
using Cobalt.Fluent;

[assembly: AvaloniaTestApplication(typeof(Cobalt.Fluent.Tests.TestApp))]

namespace Cobalt.Fluent.Tests;

/// <summary>无头测试用的宿主应用。把控件库整套主题挂上，测的就是真实的模板。</summary>
public class TestApp : Application
{
    public override void Initialize()
    {
        Styles.Add(new CobaltFluentTheme());
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<TestApp>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = true })
            .WithInterFont();
}
