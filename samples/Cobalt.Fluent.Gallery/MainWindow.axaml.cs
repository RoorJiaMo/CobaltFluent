using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Cobalt.Fluent.Gallery.Infrastructure;

namespace Cobalt.Fluent.Gallery;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        var toc = this.FindControl<ListBox>("Toc")!;
        var host = this.FindControl<ContentControl>("PageHost")!;
        var scroll = this.FindControl<ScrollViewer>("ContentScroll")!;

        // 目录按 11 组分类；组标题不可选，用 ItemTemplate 区分。
        toc.ItemsSource = SectionRegistry.TocItems;
        toc.ItemTemplate = new FuncDataTemplate<object>((item, _) => item switch
        {
            string group => new TextBlock { Text = group, Classes = { "toc-group" } },
            SectionInfo s => new TextBlock { Text = s.Title },
            _ => new TextBlock { Text = item?.ToString() ?? "" },
        });

        // 组标题只是分隔用的，不该能选中、也不该抢焦点
        toc.ContainerPrepared += (_, e) =>
        {
            if (e.Container is ListBoxItem container)
            {
                var isGroupHeader = e.Container.DataContext is string;
                container.IsEnabled = !isGroupHeader;
                container.Focusable = !isGroupHeader;
            }
        };

        toc.SelectionChanged += (_, _) =>
        {
            if (toc.SelectedItem is not SectionInfo section) return;
            host.Content = SectionRegistry.Create(section);
            scroll.Offset = default;
        };

        toc.SelectedItem = SectionRegistry.TocItems.OfType<SectionInfo>().FirstOrDefault();

        this.FindControl<CheckBox>("ThemeToggle")!.IsCheckedChanged += (s, _) =>
            GalleryState.SetTheme(((CheckBox)s!).IsChecked == true);

        this.FindControl<CheckBox>("MotionToggle")!.IsCheckedChanged += (s, _) =>
            GalleryState.SetSlowMotion(((CheckBox)s!).IsChecked == true);
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
