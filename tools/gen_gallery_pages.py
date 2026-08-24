#!/usr/bin/env python3
"""生成展柜的目录注册表和各章节页的骨架。"""
import pathlib

ROOT = pathlib.Path(__file__).resolve().parent.parent
PAGES = ROOT / "samples/Cobalt.Fluent.Gallery/Pages"
REG = ROOT / "samples/Cobalt.Fluent.Gallery/Infrastructure/SectionRegistry.cs"

SECTIONS = [
    ("总则", [("设计基线", "FoundationsPage"), ("排版 Typography", "TypographyPage"),
             ("图标 Symbol", "IconsPage")]),
    ("2 · 基础输入", [
        ("Button", "ButtonPage"), ("ToggleButton", "ToggleButtonPage"),
        ("SplitButton / DropDownButton", "SplitButtonPage"), ("TextBox", "TextBoxPage"),
        ("NumberBox", "NumberBoxPage"), ("ComboBox", "ComboBoxPage"),
        ("CheckBox", "CheckBoxPage"), ("RadioButton", "RadioButtonPage"),
        ("ToggleSwitch", "ToggleSwitchPage"), ("Slider", "SliderPage")]),
    ("3 · 容器", [
        ("Card", "CardPage"), ("SettingsCard", "SettingsCardPage"),
        ("Expander", "ExpanderPage"), ("TabControl / TabView", "TabPage"),
        ("NavigationView", "NavigationViewPage")]),
    ("4 · 集合", [
        ("ListBox", "ListBoxPage"), ("DataGrid", "DataGridPage"),
        ("TreeView", "TreeViewPage")]),
    ("5 · 反馈", [
        ("InfoBar", "InfoBarPage"), ("InfoBadge", "InfoBadgePage"),
        ("ProgressBar", "ProgressBarPage"), ("ProgressRing", "ProgressRingPage"),
        ("ToolTip", "ToolTipPage")]),
    ("6 · 弹出", [
        ("Flyout", "FlyoutPage"), ("MenuFlyout", "MenuFlyoutPage"),
        ("ContentDialog", "ContentDialogPage"), ("TeachingTip", "TeachingTipPage"),
        ("CommandBar", "CommandBarPage")]),
    ("7 · HMI 专用", [
        ("总则", "HmiIntroPage"), ("Readout", "ReadoutPage"),
        ("StatusIndicator", "StatusIndicatorPage"), ("AlarmBanner", "AlarmBannerPage"),
        ("ParameterRow", "ParameterRowPage"), ("JogButton", "JogButtonPage"),
        ("EStopButton", "EStopButtonPage"), ("DeviceStatusBar", "DeviceStatusBarPage"),
        ("NumericKeypad", "NumericKeypadPage")]),
    ("8 · 日期时间", [
        ("CalendarDatePicker / TimePicker", "DateTimePickerPage"),
        ("CalendarView / DateRange", "CalendarViewPage")]),
    ("9 · 图表", [
        ("选型与规格", "ChartIntroPage"), ("TrendChart", "TrendChartPage"),
        ("Gauge / Bar / Sparkline", "GaugePage")]),
    ("10 · 表格增强", [
        ("Toolbar / Pagination", "DataGridToolbarPage"),
        ("EmptyState / Skeleton", "EmptyStatePage")]),
    ("11 · 常用补充", [
        ("Suggest / Breadcrumb / Segmented", "SuggestPage"),
        ("Stepper / Chip / Toast", "StepperPage")]),
]

AXAML = '''<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:g="using:Cobalt.Fluent.Gallery.Infrastructure"
             xmlns:fc="using:Cobalt.Fluent.Controls"
             x:Class="Cobalt.Fluent.Gallery.Pages.{cls}">

  <StackPanel Spacing="0" HorizontalAlignment="Left">

    <StackPanel Orientation="Horizontal" Spacing="12">
      <TextBlock Classes="page-title" Text="{title}" />
      <Border Classes="tag"><TextBlock Text="{group}" /></Border>
    </StackPanel>

    <TextBlock Classes="lead" Text="待实现。" />

  </StackPanel>
</UserControl>
'''

CS = '''using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Cobalt.Fluent.Gallery.Pages;

public partial class {cls} : UserControl
{{
    public {cls}() => AvaloniaXamlLoader.Load(this);
}}
'''


def main():
    PAGES.mkdir(parents=True, exist_ok=True)
    made = 0
    for group, items in SECTIONS:
        for title, cls in items:
            ax = PAGES / f"{cls}.axaml"
            cs = PAGES / f"{cls}.axaml.cs"

            # 只给全新的章节搭骨架。code-behind 已经存在就说明这一页有人写过，
            # 不要再往里补文件——有的页整页在 C# 里构建（IconsPage 的图标网格），
            # 给它补一个 x:Class 同名的空 axaml 既不会被 InitializeComponent 加载，
            # 又会在下一个人打开时变成「这页怎么写着待实现」。
            if cs.exists():
                continue

            cs.write_text(CS.format(cls=cls), encoding="utf-8")
            if not ax.exists():
                ax.write_text(
                    AXAML.format(cls=cls, title=title.replace("&", "&amp;"), group=group),
                    encoding="utf-8")
            made += 1

    lines = [
        "using Avalonia.Controls;",
        "using Cobalt.Fluent.Gallery.Pages;",
        "",
        "namespace Cobalt.Fluent.Gallery.Infrastructure;",
        "",
        "/// <summary>",
        "/// 展柜目录。左侧那棵树就是按这个顺序铺的。",
        "/// 本文件由 tools/gen_gallery_pages.py 生成。",
        "/// </summary>",
        "public static class SectionRegistry",
        "{",
        "    public static readonly IReadOnlyList<SectionInfo> Sections =",
        "    [",
    ]
    for group, items in SECTIONS:
        for title, cls in items:
            lines.append(
                f'        new SectionInfo("{group}", "{title}", static () => new {cls}()),')
    lines += [
        "    ];",
        "",
        "    /// <summary>组标题（string）和章节（SectionInfo）混排，供左侧目录直接绑定。</summary>",
        "    public static readonly IReadOnlyList<object> TocItems = Build();",
        "",
        "    private static List<object> Build()",
        "    {",
        "        var items = new List<object>();",
        "        string? group = null;",
        "        foreach (var s in Sections)",
        "        {",
        "            if (s.Group != group) { items.Add(s.Group); group = s.Group; }",
        "            items.Add(s);",
        "        }",
        "        return items;",
        "    }",
        "",
        "    /// <summary>造页。单页出错只影响这一页，不连累整个展柜——对应 gallery.js 的 try/catch 保护器。</summary>",
        "    public static Control Create(SectionInfo section)",
        "    {",
        "        try",
        "        {",
        "            return section.Create();",
        "        }",
        "        catch (Exception ex)",
        "        {",
        "            return new TextBlock",
        "            {",
        "                Text = $\"章节「{section.Title}」构造失败：{ex}\",",
        "                TextWrapping = Avalonia.Media.TextWrapping.Wrap,",
        "            };",
        "        }",
        "    }",
        "}",
        "",
    ]
    REG.write_text("\n".join(lines), encoding="utf-8")
    total = sum(len(i) for _, i in SECTIONS)
    print(f"章节 {total} 个，新建骨架页 {made} 个；注册表写到 {REG.relative_to(ROOT)}")


if __name__ == "__main__":
    main()
