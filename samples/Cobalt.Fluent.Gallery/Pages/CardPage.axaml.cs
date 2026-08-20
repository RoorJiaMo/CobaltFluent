using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Cobalt.Fluent.Controls;

namespace Cobalt.Fluent.Gallery.Pages;

public partial class CardPage : UserControl
{
    public CardPage()
    {
        AvaloniaXamlLoader.Load(this);

        var cards = new[] { "PlayR1", "PlayR2", "PlayR3" }
            .Select(n => this.FindControl<Card>(n)!)
            .ToArray();
        var toggle = this.FindControl<ToggleSwitch>("ClickableSwitch")!;
        var log = this.FindControl<TextBlock>("PlayLog")!;

        foreach (var card in cards)
        {
            // 点击行为不在控件里：Card 是 ContentControl，IsClickable 只驱动视觉。
            // 这里照着「视觉说能点就真能点」的规矩，把关卡在同一个标志上。
            card.Tapped += (_, _) =>
            {
                if (!card.IsClickable)
                {
                    log.Text = "静态卡片：没有反馈，也不该有响应。";
                    return;
                }

                foreach (var c in cards)
                {
                    if (ReferenceEquals(c, card))
                    {
                        if (!c.Classes.Contains("picked")) c.Classes.Add("picked");
                    }
                    else
                    {
                        c.Classes.Remove("picked");
                    }
                }

                log.Text = $"已选 {card.Tag}。";
            };
        }

        toggle.IsCheckedChanged += (_, _) =>
        {
            var clickable = toggle.IsChecked == true;

            foreach (var card in cards)
            {
                card.IsClickable = clickable;
                // 关掉可点击就把选中痕迹一起收掉，别留一圈点不动的高亮描边
                if (!clickable) card.Classes.Remove("picked");
            }

            log.Text = clickable
                ? "悬停、按下、点一张卡片试试。"
                : "拨回静态：悬停和按下都没了底色变化，整排卡片重新变成三块分区。";
        };
    }
}
