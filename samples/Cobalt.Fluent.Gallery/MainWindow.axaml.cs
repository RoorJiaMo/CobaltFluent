using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Cobalt.Fluent.Gallery.Infrastructure;

namespace Cobalt.Fluent.Gallery;

public partial class MainWindow : Window
{
    private readonly ListBox _toc;
    private readonly ContentControl _host;
    private readonly SourceViewer _viewer;
    private SectionInfo? _current;

    public MainWindow()
    {
        InitializeComponent();

        _toc = this.FindControl<ListBox>("Toc")!;
        _host = this.FindControl<ContentControl>("PageHost")!;
        _viewer = this.FindControl<SourceViewer>("SourceOverlay")!;
        var scroll = this.FindControl<ScrollViewer>("ContentScroll")!;
        var search = this.FindControl<TextBox>("TocSearch")!;

        // 目录按 11 组分类；组标题不可选，用 ItemTemplate 区分。
        _toc.ItemsSource = SectionRegistry.TocItems;
        _toc.ItemTemplate = new FuncDataTemplate<object>((item, _) => item switch
        {
            string group => new TextBlock { Text = group, Classes = { "toc-group" } },
            SectionInfo s => new TextBlock { Text = s.Title },
            _ => new TextBlock { Text = item?.ToString() ?? "" },
        });

        // 组标题只是分隔用的，不该能选中、也不该抢焦点
        _toc.ContainerPrepared += (_, e) =>
        {
            if (e.Container is ListBoxItem container)
            {
                var isGroupHeader = e.Container.DataContext is string;
                container.IsEnabled = !isGroupHeader;
                container.Focusable = !isGroupHeader;
            }
        };

        _toc.SelectionChanged += (_, _) =>
        {
            if (_toc.SelectedItem is not SectionInfo section) return;
            if (ReferenceEquals(section, _current)) return;   // 过滤后重选同一节不重建页面
            _current = section;
            _host.Content = SectionRegistry.Create(section);
            scroll.Offset = default;
        };

        _toc.SelectedItem = SectionRegistry.TocItems.OfType<SectionInfo>().FirstOrDefault();

        // 搜索：过滤目录，标题或组名都算命中。清空恢复全量。
        search.TextChanged += (_, _) => ApplyFilter(search.Text);

        this.FindControl<Button>("ViewSourceButton")!.Click += (_, _) => ShowSourceForCurrentSection();

        this.FindControl<ToggleSwitch>("ThemeToggle")!.IsCheckedChanged += (s, _) =>
            GalleryState.SetTheme(((ToggleSwitch)s!).IsChecked == true);

        this.FindControl<ToggleSwitch>("MotionToggle")!.IsCheckedChanged += (s, _) =>
            GalleryState.SetSlowMotion(((ToggleSwitch)s!).IsChecked == true);

        LoadBrandLogo();

        // Ctrl+F 聚焦搜索。覆层开着时 Esc 由覆层自己收，这里不抢。
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.F && e.KeyModifiers == KeyModifiers.Control)
            {
                search.Focus();
                search.SelectAll();
                e.Handled = true;
            }
        };
    }

    /// <summary>
    /// 顶栏应用标与窗口图标。Assets/logo.png 存在时用它，缺席时保持 XAML 里那个
    /// accent 底色的文字标记 —— 品牌资源不该是编译的硬依赖。
    /// </summary>
    private void LoadBrandLogo()
    {
        var uri = new Uri("avares://Cobalt.Fluent.Gallery/Assets/logo.png");
        if (!AssetLoader.Exists(uri)) return;

        using var stream = AssetLoader.Open(uri);
        var bitmap = new Bitmap(stream);

        var image = this.FindControl<Image>("AppLogo")!;
        image.Source = bitmap;
        image.IsVisible = true;
        this.FindControl<Border>("AppLogoFallback")!.IsVisible = false;

        Icon = new WindowIcon(bitmap);
    }

    /// <summary>顶栏「本页源码」。Shots 工具截源码覆层时也直接调这个。</summary>
    public void ShowSourceForCurrentSection()
    {
        if (_current is null || _host.Content is not Control page) return;
        _viewer.Show(_current.Title, SourceIndex.For(page.GetType().Name));
    }

    private void ApplyFilter(string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            _toc.ItemsSource = SectionRegistry.TocItems;
            _toc.SelectedItem = _current;
            return;
        }

        var q = query.Trim();
        var filtered = new List<object>();
        string? group = null;
        foreach (var section in SectionRegistry.Sections)
        {
            if (!section.Title.Contains(q, StringComparison.OrdinalIgnoreCase)
                && !section.Group.Contains(q, StringComparison.OrdinalIgnoreCase))
                continue;
            if (section.Group != group) { filtered.Add(section.Group); group = section.Group; }
            filtered.Add(section);
        }

        _toc.ItemsSource = filtered;
        // 当前节还在结果里就保持选中；不在也不清页面——正在看的东西不该被搜索框赶走
        if (_current is not null && filtered.Contains(_current))
            _toc.SelectedItem = _current;
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
