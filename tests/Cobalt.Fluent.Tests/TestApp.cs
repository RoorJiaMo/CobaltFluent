using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Headless;
using Avalonia.Themes.Fluent;
using Cobalt.Fluent;

[assembly: AvaloniaTestApplication(typeof(Cobalt.Fluent.Tests.TestApp))]

// 语言是全局静态状态，测试之间会互相看见。整个程序集串行跑——
// 这一套 320 项跑 5 秒，并行省下的时间远不值一个偶发的串台。
[assembly: Xunit.CollectionBehavior(DisableTestParallelization = true)]

namespace Cobalt.Fluent.Tests;

/// <summary>无头测试用的宿主应用。把控件库整套主题挂上，测的就是真实的模板。</summary>
public class TestApp : Application
{
    /// <summary>
    /// 把界面语言钉成中文。
    ///
    /// **不钉的话这套测试就耦合在跑它的机器的 locale 上**——本地开发机是中文、
    /// CI runner 是英文，同一份代码两边结果不同，而失败信息只会说
    /// 「期望「数据过期」，实际 "Data stale"」，得盯一会儿才看出是环境差异。
    ///
    /// 语言选择本身由 <c>LocalizationTests</c> 单独覆盖，不靠这批行为测试兼职。
    /// </summary>
    [ModuleInitializer]
    internal static void PinLanguage() => CobaltStrings.Current = new CobaltStringsZhHans();

    public override void Initialize()
    {
        Styles.Add(new CobaltFluentTheme());
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<TestApp>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = true })
            .WithInterFont();
}
