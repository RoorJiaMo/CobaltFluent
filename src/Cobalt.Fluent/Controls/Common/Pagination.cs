using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Cobalt.Fluent.Automation;

namespace Cobalt.Fluent.Controls;

/// <summary>
/// 分页。
///
/// 一句话提醒：**数据量大时别做客户端分页。** 一两千条还行，上万条要走服务端分页 + 虚拟化。
/// </summary>
public class Pagination : TemplatedControl
{
    public static readonly StyledProperty<int> PageCountProperty =
        AvaloniaProperty.Register<Pagination, int>(nameof(PageCount), 1);

    public int PageCount
    {
        get => GetValue(PageCountProperty);
        set => SetValue(PageCountProperty, value);
    }

    public static readonly StyledProperty<int> CurrentPageProperty =
        AvaloniaProperty.Register<Pagination, int>(
            nameof(CurrentPage), 1, defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    /// <summary>当前页，从 1 开始。</summary>
    public int CurrentPage
    {
        get => GetValue(CurrentPageProperty);
        set => SetValue(CurrentPageProperty, value);
    }

    public static readonly StyledProperty<int> TotalItemsProperty =
        AvaloniaProperty.Register<Pagination, int>(nameof(TotalItems));

    public int TotalItems
    {
        get => GetValue(TotalItemsProperty);
        set => SetValue(TotalItemsProperty, value);
    }

    /// <summary>当前页两侧各显示几个页码。两端和当前页之间用省略号连接。</summary>
    public static readonly StyledProperty<int> SiblingCountProperty =
        AvaloniaProperty.Register<Pagination, int>(nameof(SiblingCount), 1);

    public int SiblingCount
    {
        get => GetValue(SiblingCountProperty);
        set => SetValue(SiblingCountProperty, value);
    }

    private string? _infoText;

    public static readonly DirectProperty<Pagination, string?> InfoTextProperty =
        AvaloniaProperty.RegisterDirect<Pagination, string?>(nameof(InfoText), o => o._infoText);

    /// <summary>左侧那句「共 N 条 · 第 X / Y 页」。</summary>
    public string? InfoText
    {
        get => _infoText;
        private set => SetAndRaise(InfoTextProperty, ref _infoText, value);
    }

    private IReadOnlyList<int?> _pages = [];

    public static readonly DirectProperty<Pagination, IReadOnlyList<int?>> PagesProperty =
        AvaloniaProperty.RegisterDirect<Pagination, IReadOnlyList<int?>>(nameof(Pages), o => o._pages);

    /// <summary>要渲染的页码序列。<c>null</c> 表示一个省略号。</summary>
    public IReadOnlyList<int?> Pages
    {
        get => _pages;
        private set => SetAndRaise(PagesProperty, ref _pages, value);
    }

    static Pagination()
    {
        PageCountProperty.Changed.AddClassHandler<Pagination>((x, _) => x.Refresh());
        CurrentPageProperty.Changed.AddClassHandler<Pagination>((x, _) => x.Refresh());
        TotalItemsProperty.Changed.AddClassHandler<Pagination>((x, _) => x.Refresh());
        SiblingCountProperty.Changed.AddClassHandler<Pagination>((x, _) => x.Refresh());
    }

    /// <summary>换语言之后重算已经显示出来的文字。见 <see cref="CobaltStrings.CurrentChanged"/>。</summary>
    private readonly StringsWatcher _strings;

    public Pagination()
    {
        _strings = new StringsWatcher(Refresh);
        Refresh();
    }

    private Panel? _pagesPanel;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        if (e.NameScope.Find<Button>("PART_Previous") is { } prev)
            prev.Click += (_, _) => CurrentPage = Math.Max(1, CurrentPage - 1);

        if (e.NameScope.Find<Button>("PART_Next") is { } next)
            next.Click += (_, _) => CurrentPage = Math.Min(PageCount, CurrentPage + 1);

        _pagesPanel = e.NameScope.Find<Panel>("PART_Pages");
        RebuildPageButtons();
    }

    /// <summary>
    /// 页码按钮在代码里建，不走 ItemTemplate ——
    /// 序列里混着页码和省略号（null），用一套 DataTemplate 表达要绕好几个转换器，
    /// 直接建反而清楚。
    /// </summary>
    private void RebuildPageButtons()
    {
        if (_pagesPanel is null) return;

        _pagesPanel.Children.Clear();

        foreach (var page in Pages)
        {
            if (page is not { } number)
            {
                _pagesPanel.Children.Add(new TextBlock
                {
                    Text = "…",
                    MinWidth = 32,
                    TextAlignment = Avalonia.Media.TextAlignment.Center,
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                    [!ForegroundProperty] = this[!ForegroundProperty],
                });
                continue;
            }

            var button = new Button
            {
                Content = number.ToString(),
                MinWidth = 32,
                Height = 32,
                Padding = new Thickness(8, 0),
                // 直接复用 Button 的两个变体：当前页 accent 实底，其余 subtle 无底色
                Classes = { number == CurrentPage ? "accent" : "subtle" },
            };

            // 页码要等宽，否则翻到两位数时按钮宽度会跳
            if (this.TryFindResource("TabularNumbers", ActualThemeVariant, out var tnum)
                && tnum is Avalonia.Media.FontFeatureCollection features)
            {
                button.FontFeatures = features;
            }

            button.Click += (_, _) => CurrentPage = number;
            _pagesPanel.Children.Add(button);
        }
    }

    private void Refresh()
    {
        var count = Math.Max(1, PageCount);
        var current = Math.Clamp(CurrentPage, 1, count);

        InfoText = TotalItems > 0
            ? CobaltStrings.Current.PageInfo(TotalItems, current, count)
            : CobaltStrings.Current.PageInfoWithoutTotal(current, count);

        Pages = BuildPages(current, count, Math.Max(0, SiblingCount));
        RebuildPageButtons();
    }

    /// <summary>首末页始终显示，当前页两侧各留 sibling 个，中间断开处放省略号。</summary>
    internal static IReadOnlyList<int?> BuildPages(int current, int count, int siblings)
    {
        var window = siblings * 2 + 5;   // 首 + 末 + 当前 + 两侧 + 两个省略号位
        if (count <= window)
            return Enumerable.Range(1, count).Select(i => (int?)i).ToList();

        var pages = new List<int?> { 1 };

        var start = Math.Max(2, current - siblings);
        var end = Math.Min(count - 1, current + siblings);

        if (start > 2) pages.Add(null);
        for (var i = start; i <= end; i++) pages.Add(i);
        if (end < count - 1) pages.Add(null);

        pages.Add(count);
        return pages;
    }

    /// <summary>见 <see cref="Cobalt.Fluent.Automation.PaginationAutomationPeer"/>。</summary>
    protected override AutomationPeer OnCreateAutomationPeer() => new PaginationAutomationPeer(this);

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _strings.Attach();
    }
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _strings.Detach();
    }
}
