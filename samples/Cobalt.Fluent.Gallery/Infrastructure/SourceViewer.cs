using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Cobalt.Fluent.Controls;

namespace Cobalt.Fluent.Gallery.Infrastructure;

/// <summary>
/// 「查看源码」覆层。盖住整个窗口：烟幕 + 居中一张弹出层规格的卡
/// （8px 圆角、实色底、flyout 描边、和 ContentDialog 同一档的阴影——
/// 底色也取 ContentDialog 那支：代码底下透出页面文字没法读，亚克力不适合这里）。
///
/// 一个文件一个页签：本页示例（axaml + code-behind）在前，
/// 这一节讲的控件在库里的 ControlTheme / 控件类在后。
/// 代码按行虚拟化（ListBox），千行的主题文件也只画可见那几十行；
/// 高亮是 <see cref="CodeHighlighter"/> 的三色轻量版。
///
/// 点烟幕或按 Esc 关闭；「复制」拿走当前页签的整份原文——
/// 逐行 TextBlock 没有跨行选择，复制按钮是唯一的取文通道，所以必须有。
///
/// 颜色全部在 Show() 时按 ActualThemeVariant 现查（token 都在 ThemeDictionaries 里，
/// 不带主题查会落空）；覆层开着的时候顶栏被烟幕挡住，主题切不了，所以不用追着主题变。
/// </summary>
public sealed class SourceViewer : Panel
{
    private readonly Border _smoke;
    private readonly Border _card;
    private readonly TextBlock _title = new();
    private readonly TextBlock _path = new();
    private readonly TabControl _tabs = new();

    private IBrush? _comment, _string, _keyword, _plain, _lineNo;

    public SourceViewer()
    {
        IsVisible = false;
        ZIndex = 100;

        // 烟幕：点它等于点关闭
        _smoke = new Border();
        _smoke.PointerPressed += (_, _) => Hide();

        var copy = new Button { Content = "复制", Classes = { "subtle" } };
        copy.Click += async (_, _) =>
        {
            if (CurrentFile() is not { } file) return;
            var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
            if (clipboard is null) return;
            await clipboard.SetTextAsync(file.Text);
            copy.Content = "已复制";
            await Task.Delay(1200);
            copy.Content = "复制";
        };

        var close = new Button
        {
            Classes = { "subtle" },
            Content = new SymbolIcon { Symbol = Symbol.Cancel, FontSize = 12 },
        };
        close.Click += (_, _) => Hide();

        if (App.TryGet("SubtitleTextBlockStyle") is Avalonia.Styling.ControlTheme subtitle)
            _title.Theme = subtitle;
        _path.FontSize = 12;

        var header = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto"),
            Margin = new Thickness(24, 20, 16, 8),
        };
        header.Children.Add(new StackPanel { Spacing = 2, Children = { _title, _path } });
        Grid.SetColumn(copy, 1);
        Grid.SetColumn(close, 2);
        copy.Margin = new Thickness(0, 0, 8, 0);
        copy.VerticalAlignment = close.VerticalAlignment = VerticalAlignment.Top;
        header.Children.Add(copy);
        header.Children.Add(close);

        _tabs.Margin = new Thickness(16, 0, 16, 16);
        _tabs.SelectionChanged += (_, _) =>
        {
            if (CurrentFile() is { } f) _path.Text = f.RepoPath;
        };

        var body = new Grid { RowDefinitions = new RowDefinitions("Auto,*") };
        Grid.SetRow(header, 0);
        Grid.SetRow(_tabs, 1);
        body.Children.Add(header);
        body.Children.Add(_tabs);

        _card = new Border
        {
            CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(1),
            Margin = new Thickness(64, 40),
            MaxWidth = 1120,
            // 和 ContentDialog 同一档。BoxShadow 语法收颜色不收画刷，字面色值是这里的常规
            BoxShadow = BoxShadows.Parse("0 32 64 0 #3D000000, 0 0 8 0 #33000000"),
            Child = body,
        };

        Children.Add(_smoke);
        Children.Add(_card);

        Focusable = true;
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape) { Hide(); e.Handled = true; }
        };
    }

    private SourceFile? CurrentFile() => (_tabs.SelectedItem as TabItem)?.Tag as SourceFile;

    public void Show(string sectionTitle, IReadOnlyList<SourceFile> files)
    {
        _title.Text = $"源码 — {sectionTitle}";

        _smoke.Background = Brush("SmokeFillColorDefaultBrush", Brushes.Black);
        // 实色底，和 ContentDialog 一致。代码底下透出页面文字没法读，亚克力不适合这里
        _card.Background = Brush("SolidBackgroundFillColorBaseBrush", Brushes.White);
        _card.BorderBrush = Brush("SurfaceStrokeColorFlyoutBrush", Brushes.Gray);
        _path.Foreground = Brush("TextFillColorTertiaryBrush", Brushes.Gray);

        _comment = Brush("SystemFillColorSuccessBrush", Brushes.Green);
        _string = Brush("SystemFillColorCautionBrush", Brushes.DarkGoldenrod);
        _keyword = Brush("AccentTextFillColorPrimaryBrush", Brushes.RoyalBlue);
        _plain = Brush("TextFillColorPrimaryBrush", Brushes.Black);
        _lineNo = Brush("TextFillColorTertiaryBrush", Brushes.Gray);

        var items = new List<TabItem>();
        foreach (var file in files)
            items.Add(new TabItem
            {
                Header = file.FileName,
                Tag = file,
                Content = BuildCodeList(file),
            });

        if (items.Count == 0)
            items.Add(new TabItem
            {
                Header = "无源码文件",
                Content = new TextBlock
                {
                    Text = "该页面未登记源码文件。",
                    Margin = new Thickness(12),
                },
            });

        _tabs.ItemsSource = items;
        _tabs.SelectedIndex = 0;
        _path.Text = (items[0].Tag as SourceFile)?.RepoPath ?? "";

        IsVisible = true;
        Focus();
    }

    public void Hide()
    {
        IsVisible = false;
        _tabs.ItemsSource = null;   // 松开几千行 CodeLine，别挂在隐藏的覆层上
    }

    private IBrush Brush(string key, IBrush fallback) =>
        this.TryFindResource(key, ActualThemeVariant, out var v) && v is IBrush b ? b : fallback;

    private Control BuildCodeList(SourceFile file)
    {
        var lines = CodeHighlighter.Highlight(file.Text, file.IsXaml);
        var mono = App.TryGet("MonospaceFontFamily") is FontFamily ff ? ff : FontFamily.Default;

        var list = new ListBox
        {
            ItemsSource = lines,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Focusable = false,
            ItemTemplate = new FuncDataTemplate<CodeLine>((line, _) =>
                line is null ? new Panel() : RenderLine(line, mono)),
        };
        if (App.TryGet("CodeLineContainer") is Avalonia.Styling.ControlTheme ct)
            list.ItemContainerTheme = ct;

        ScrollViewer.SetHorizontalScrollBarVisibility(list, Avalonia.Controls.Primitives.ScrollBarVisibility.Auto);
        return list;
    }

    private Control RenderLine(CodeLine line, FontFamily mono)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("44,*") };

        grid.Children.Add(new TextBlock
        {
            Text = line.Number.ToString(),
            FontFamily = mono,
            FontSize = 12,
            Foreground = _lineNo,
            TextAlignment = TextAlignment.Right,
            Margin = new Thickness(0, 0, 14, 0),
        });

        var code = new TextBlock { FontFamily = mono, FontSize = 12 };
        Grid.SetColumn(code, 1);

        if (line.Spans.Count == 0)
        {
            code.Text = line.Text;
            code.Foreground = _plain;
        }
        else
        {
            var inlines = new InlineCollection();
            var pos = 0;
            foreach (var span in line.Spans)
            {
                if (span.Start > pos)
                    inlines.Add(new Run(line.Text[pos..span.Start]) { Foreground = _plain });
                var brush = span.Kind switch
                {
                    TokenKind.Comment => _comment,
                    TokenKind.String => _string,
                    TokenKind.Keyword => _keyword,
                    _ => _plain,
                };
                var len = Math.Min(span.Length, line.Text.Length - span.Start);
                inlines.Add(new Run(line.Text.Substring(span.Start, len)) { Foreground = brush });
                pos = span.Start + span.Length;
            }
            if (pos < line.Text.Length)
                inlines.Add(new Run(line.Text[pos..]) { Foreground = _plain });
            code.Inlines = inlines;
        }

        grid.Children.Add(code);
        return grid;
    }
}
