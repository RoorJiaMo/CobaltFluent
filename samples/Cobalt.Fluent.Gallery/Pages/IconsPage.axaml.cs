using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Cobalt.Fluent.Controls;

namespace Cobalt.Fluent.Gallery.Pages;

/// <summary>
/// 图标总览。全库用到 38 个字形，
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
                    Text = "这 38 个字形在本库中不使用字体渲染——"
                         + "嵌入式 Linux（如 RK3568）上无法安装所需字体，字形会渲染为缺字方块。"
                         + "因此图标全部改以矢量路径绘制，16×16 设计尺寸，线型字形描边 1.2px 圆头圆角。"
                         + "若目标系统具备该字体，开启 SymbolIcon.UseGlyphFont 即可切回字体渲染。",
                },
                wrap,
            },
        };
    }
}
