using Avalonia.Markup.Xaml;
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
/// 切主题走 <c>Application.Current.RequestedThemeVariant</c>，Light / Dark / Default 三档。
/// </summary>
public class CobaltFluentTheme : Styles
{
    public CobaltFluentTheme(IServiceProvider? serviceProvider = null)
    {
        AvaloniaXamlLoader.Load(serviceProvider, this);
    }
}
