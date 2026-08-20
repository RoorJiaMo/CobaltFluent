using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Cobalt.Fluent.Controls;

namespace Cobalt.Fluent.Gallery.Pages;

public partial class NavigationViewPage : UserControl
{
    public NavigationViewPage()
    {
        AvaloniaXamlLoader.Load(this);

        var expanded = this.FindControl<NavigationView>("Expanded")!;
        var compact = this.FindControl<NavigationView>("Compact")!;
        var log = this.FindControl<TextBlock>("NavLog")!;

        expanded.SelectedItem = expanded.MenuItems.OfType<NavigationViewItem>().FirstOrDefault();
        compact.SelectedItem = compact.MenuItems.OfType<NavigationViewItem>().FirstOrDefault();

        expanded.SelectionChanged += (_, _) =>
        {
            log.Text = $"已选中：{expanded.SelectedItem?.Content}";
            expanded.Header = new TextBlock
            {
                Text = expanded.SelectedItem?.Content?.ToString(),
                Theme = (Avalonia.Styling.ControlTheme?)this.FindResource("SubtitleTextBlockStyle"),
            };
        };

        this.FindControl<Button>("TogglePane")!.Click += (_, _) =>
            expanded.PaneDisplayMode =
                expanded.PaneDisplayMode == NavigationViewPaneDisplayMode.Left
                    ? NavigationViewPaneDisplayMode.LeftCompact
                    : NavigationViewPaneDisplayMode.Left;
    }
}
