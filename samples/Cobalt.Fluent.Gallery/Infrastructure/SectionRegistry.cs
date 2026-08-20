using Avalonia.Controls;
using Cobalt.Fluent.Gallery.Pages;

namespace Cobalt.Fluent.Gallery.Infrastructure;

/// <summary>
/// 展柜目录。左侧那棵树就是按这个顺序铺的。
/// 本文件由 tools/gen_gallery_pages.py 生成。
/// </summary>
public static class SectionRegistry
{
    public static readonly IReadOnlyList<SectionInfo> Sections =
    [
        new SectionInfo("总则", "设计基线", static () => new FoundationsPage()),
        new SectionInfo("总则", "排版 Typography", static () => new TypographyPage()),
        new SectionInfo("总则", "图标 Symbol", static () => new IconsPage()),
        new SectionInfo("2 · 基础输入", "Button", static () => new ButtonPage()),
        new SectionInfo("2 · 基础输入", "ToggleButton", static () => new ToggleButtonPage()),
        new SectionInfo("2 · 基础输入", "SplitButton / DropDownButton", static () => new SplitButtonPage()),
        new SectionInfo("2 · 基础输入", "TextBox", static () => new TextBoxPage()),
        new SectionInfo("2 · 基础输入", "NumberBox", static () => new NumberBoxPage()),
        new SectionInfo("2 · 基础输入", "ComboBox", static () => new ComboBoxPage()),
        new SectionInfo("2 · 基础输入", "CheckBox", static () => new CheckBoxPage()),
        new SectionInfo("2 · 基础输入", "RadioButton", static () => new RadioButtonPage()),
        new SectionInfo("2 · 基础输入", "ToggleSwitch", static () => new ToggleSwitchPage()),
        new SectionInfo("2 · 基础输入", "Slider", static () => new SliderPage()),
        new SectionInfo("3 · 容器", "Card", static () => new CardPage()),
        new SectionInfo("3 · 容器", "SettingsCard", static () => new SettingsCardPage()),
        new SectionInfo("3 · 容器", "Expander", static () => new ExpanderPage()),
        new SectionInfo("3 · 容器", "TabControl / TabView", static () => new TabPage()),
        new SectionInfo("3 · 容器", "NavigationView", static () => new NavigationViewPage()),
        new SectionInfo("4 · 集合", "ListBox", static () => new ListBoxPage()),
        new SectionInfo("4 · 集合", "DataGrid", static () => new DataGridPage()),
        new SectionInfo("4 · 集合", "TreeView", static () => new TreeViewPage()),
        new SectionInfo("5 · 反馈", "InfoBar", static () => new InfoBarPage()),
        new SectionInfo("5 · 反馈", "InfoBadge", static () => new InfoBadgePage()),
        new SectionInfo("5 · 反馈", "ProgressBar", static () => new ProgressBarPage()),
        new SectionInfo("5 · 反馈", "ProgressRing", static () => new ProgressRingPage()),
        new SectionInfo("5 · 反馈", "ToolTip", static () => new ToolTipPage()),
        new SectionInfo("6 · 弹出", "Flyout", static () => new FlyoutPage()),
        new SectionInfo("6 · 弹出", "MenuFlyout", static () => new MenuFlyoutPage()),
        new SectionInfo("6 · 弹出", "ContentDialog", static () => new ContentDialogPage()),
        new SectionInfo("6 · 弹出", "TeachingTip", static () => new TeachingTipPage()),
        new SectionInfo("6 · 弹出", "CommandBar", static () => new CommandBarPage()),
        new SectionInfo("7 · HMI 专用", "总则", static () => new HmiIntroPage()),
        new SectionInfo("7 · HMI 专用", "Readout", static () => new ReadoutPage()),
        new SectionInfo("7 · HMI 专用", "StatusIndicator", static () => new StatusIndicatorPage()),
        new SectionInfo("7 · HMI 专用", "AlarmBanner", static () => new AlarmBannerPage()),
        new SectionInfo("7 · HMI 专用", "ParameterRow", static () => new ParameterRowPage()),
        new SectionInfo("7 · HMI 专用", "JogButton", static () => new JogButtonPage()),
        new SectionInfo("7 · HMI 专用", "EStopButton", static () => new EStopButtonPage()),
        new SectionInfo("7 · HMI 专用", "DeviceStatusBar", static () => new DeviceStatusBarPage()),
        new SectionInfo("8 · 日期时间", "CalendarDatePicker / TimePicker", static () => new DateTimePickerPage()),
        new SectionInfo("8 · 日期时间", "CalendarView / DateRange", static () => new CalendarViewPage()),
        new SectionInfo("9 · 图表", "选型与规格", static () => new ChartIntroPage()),
        new SectionInfo("9 · 图表", "TrendChart", static () => new TrendChartPage()),
        new SectionInfo("9 · 图表", "Gauge / Bar / Sparkline", static () => new GaugePage()),
        new SectionInfo("10 · 表格增强", "Toolbar / Pagination", static () => new DataGridToolbarPage()),
        new SectionInfo("10 · 表格增强", "EmptyState / Skeleton", static () => new EmptyStatePage()),
        new SectionInfo("11 · 常用补充", "Suggest / Breadcrumb / Segmented", static () => new SuggestPage()),
        new SectionInfo("11 · 常用补充", "Stepper / Chip / Toast", static () => new StepperPage()),
    ];

    /// <summary>组标题（string）和章节（SectionInfo）混排，供左侧目录直接绑定。</summary>
    public static readonly IReadOnlyList<object> TocItems = Build();

    private static List<object> Build()
    {
        var items = new List<object>();
        string? group = null;
        foreach (var s in Sections)
        {
            if (s.Group != group) { items.Add(s.Group); group = s.Group; }
            items.Add(s);
        }
        return items;
    }

    /// <summary>造页。单页出错只影响这一页，不连累整个展柜——对应 gallery.js 的 try/catch 保护器。</summary>
    public static Control Create(SectionInfo section)
    {
        try
        {
            return section.Create();
        }
        catch (Exception ex)
        {
            return new TextBlock
            {
                Text = $"「{section.Title}」这一页构造失败：{ex}",
                TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            };
        }
    }
}
