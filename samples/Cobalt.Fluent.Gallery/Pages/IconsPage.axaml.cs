using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Cobalt.Fluent.Controls;

namespace Cobalt.Fluent.Gallery.Pages;

/// <summary>
/// 图标总览。全库用到 36 个字形，
/// 本库改成矢量路径——嵌入式 Linux 上没有那套字体。这一页用来肉眼验收每个字形画得对不对。
/// </summary>
public partial class IconsPage : UserControl
{
    public IconsPage()
    {
        var wrap = new WrapPanel { Orientation = Orientation.Horizontal };

        foreach (Symbol symbol in Enum.GetValues<Symbol>())
        {
            if (symbol == Symbol.None) continue;

            wrap.Children.Add(new StackPanel
            {
                Width = 108,
                Spacing = 6,
                Margin = new Avalonia.Thickness(0, 0, 8, 16),
                Children =
                {
                    new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        Spacing = 10,
                        Children =
                        {
                            new SymbolIcon { Symbol = symbol, FontSize = 16, VerticalAlignment = VerticalAlignment.Center },
                            new SymbolIcon { Symbol = symbol, FontSize = 24, VerticalAlignment = VerticalAlignment.Center },
                        },
                    },
                    new TextBlock { Text = symbol.ToString(), FontSize = 11, TextWrapping = TextWrapping.Wrap },
                },
            });
        }

        Content = new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            Children =
            {
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 12,
                    Children =
                    {
                        new TextBlock { Text = "图标 Symbol", Classes = { "page-title" } },
                        new Border { Classes = { "tag" }, Child = new TextBlock { Text = "总则" } },
                    },
                },
                new TextBlock
                {
                    Classes = { "lead" },
                    Text = "这 36 个字形本库不走字体——"
                         + "嵌入式 Linux（RK3568 这类）上装不到，会渲染成豆腐块。"
                         + "所以图标全部改画矢量路径，16×16 设计尺寸，线型字形描边 1.2px 圆头圆角。"
                         + "手上有那套字体的话，把 SymbolIcon.UseGlyphFont 打开即可切回字体渲染。",
                },
                wrap,
            },
        };
    }
}
