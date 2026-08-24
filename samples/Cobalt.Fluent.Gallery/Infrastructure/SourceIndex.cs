using System.Reflection;

namespace Cobalt.Fluent.Gallery.Infrastructure;

/// <summary>「查看源码」里的一个文件：页签名、仓库相对路径、原文。</summary>
public sealed record SourceFile(string FileName, string RepoPath, string Text)
{
    public bool IsXaml => FileName.EndsWith(".axaml", StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// 章节 → 源码文件的索引。
///
/// 每一页固定带自己的示例（Pages/Xxx.axaml + .axaml.cs），
/// 再按下面那张表补上这一节讲的控件在库里的实现：ControlTheme 和控件类。
/// 原文是编译期嵌进程序集的（见 csproj 的 EmbeddedResource 段），
/// 表里点名的文件不在清单里就静默跳过——比如 IconsPage 没有 axaml。
/// </summary>
public static class SourceIndex
{
    /// <summary>页面类型名 → 库侧文件。路径不带 src:// 前缀，themes/ 和 controls/ 开头。</summary>
    private static readonly Dictionary<string, string[]> Library = new()
    {
        ["FoundationsPage"] = ["themes/Tokens.axaml", "themes/Metrics.axaml", "themes/Shared.axaml"],
        ["TypographyPage"] = ["themes/Typography.axaml"],
        ["IconsPage"] = ["controls/Symbol.cs", "controls/SymbolIcon.cs", "controls/SymbolGeometry.cs"],

        ["ButtonPage"] = ["themes/Controls/Button.axaml"],
        ["ToggleButtonPage"] = ["themes/Controls/ToggleButton.axaml"],
        ["SplitButtonPage"] = ["themes/Controls/SplitButton.axaml"],
        ["TextBoxPage"] = ["themes/Controls/TextBox.axaml"],
        ["NumberBoxPage"] = ["themes/Controls/Misc.axaml", "themes/Controls/TextBox.axaml"],
        ["ComboBoxPage"] = ["themes/Controls/ComboBox.axaml"],
        ["CheckBoxPage"] = ["themes/Controls/CheckRadio.axaml"],
        ["RadioButtonPage"] = ["themes/Controls/CheckRadio.axaml"],
        ["ToggleSwitchPage"] = ["themes/Controls/ToggleSwitch.axaml"],
        ["SliderPage"] = ["themes/Controls/Slider.axaml"],

        ["CardPage"] = ["controls/Layout/Card.cs", "themes/Controls/CardTheme.axaml"],
        ["SettingsCardPage"] = ["controls/Layout/SettingsCard.cs", "themes/Controls/CardTheme.axaml"],
        ["ExpanderPage"] = ["themes/Controls/Expander.axaml"],
        ["TabPage"] = ["themes/Controls/TabControl.axaml", "controls/Layout/TabView.cs", "themes/Controls/TabViewTheme.axaml"],
        ["NavigationViewPage"] = ["controls/Navigation/NavigationView.cs", "themes/Controls/NavigationTheme.axaml"],

        ["ListBoxPage"] = ["themes/Controls/ListBox.axaml"],
        ["DataGridPage"] = ["themes/Controls/DataGrid.axaml"],
        ["TreeViewPage"] = ["themes/Controls/TreeView.axaml"],

        ["InfoBarPage"] = ["controls/Feedback/InfoBar.cs", "themes/Controls/FeedbackTheme.axaml"],
        ["InfoBadgePage"] = ["controls/Feedback/InfoBadge.cs", "themes/Controls/FeedbackTheme.axaml"],
        ["ProgressBarPage"] = ["themes/Controls/ProgressBar.axaml"],
        ["ProgressRingPage"] = ["controls/Feedback/ProgressRing.cs", "themes/Controls/FeedbackTheme.axaml"],
        ["ToolTipPage"] = ["themes/Controls/Flyout.axaml"],

        ["FlyoutPage"] = ["themes/Controls/Flyout.axaml"],
        ["MenuFlyoutPage"] = ["themes/Controls/Flyout.axaml"],
        ["ContentDialogPage"] = ["controls/Overlays/ContentDialog.cs", "themes/Controls/DialogTheme.axaml"],
        ["TeachingTipPage"] = ["controls/Overlays/TeachingTip.cs", "themes/Controls/DialogTheme.axaml"],
        ["CommandBarPage"] = ["controls/Commands/CommandBar.cs", "themes/Controls/CommandBarTheme.axaml"],

        ["HmiIntroPage"] = ["themes/Tokens.axaml"],
        ["ReadoutPage"] = ["controls/Hmi/Readout.cs", "themes/Controls/HmiReadout.axaml"],
        ["StatusIndicatorPage"] = ["controls/Hmi/StatusIndicator.cs", "controls/Hmi/Heartbeat.cs", "themes/Controls/HmiReadout.axaml"],
        ["AlarmBannerPage"] = ["controls/Hmi/AlarmBanner.cs", "themes/Controls/HmiAlarm.axaml"],
        ["ParameterRowPage"] = ["controls/Hmi/ParameterRow.cs", "controls/Hmi/ParameterTable.cs", "themes/Controls/HmiParameter.axaml"],
        ["JogButtonPage"] = ["controls/Hmi/JogButton.cs", "controls/Hmi/JogGroup.cs", "themes/Controls/HmiActuator.axaml"],
        ["EStopButtonPage"] = ["controls/Hmi/EStopButton.cs", "themes/Controls/HmiActuator.axaml"],
        ["DeviceStatusBarPage"] = ["controls/Hmi/DeviceStatusBar.cs", "controls/Hmi/Heartbeat.cs", "themes/Controls/HmiStatusBar.axaml"],
        ["NumericKeypadPage"] = ["controls/Hmi/NumericKeypad.cs", "themes/Controls/HmiKeypad.axaml"],

        ["DateTimePickerPage"] = ["themes/Controls/DateTime.axaml"],
        ["CalendarViewPage"] = ["controls/DateTimeControls/RangeCalendar.cs", "themes/Controls/DateTime.axaml"],

        ["ChartIntroPage"] = ["controls/Charts/ChartSeries.cs", "controls/Charts/ChartPalette.cs", "themes/Controls/ChartTheme.axaml"],
        ["TrendChartPage"] = ["controls/Charts/TrendChart.cs", "controls/Charts/ChartFrame.cs", "controls/Charts/ChartLegend.cs"],
        ["GaugePage"] = ["controls/Charts/Gauge.cs", "controls/Charts/BarChart.cs", "controls/Charts/Sparkline.cs"],

        ["DataGridToolbarPage"] = ["controls/Common/DataGridToolbar.cs", "controls/Common/Pagination.cs", "themes/Controls/CommonTheme.axaml"],
        ["EmptyStatePage"] = ["controls/Common/EmptyState.cs", "controls/Common/Skeleton.cs", "themes/Controls/CommonTheme.axaml"],

        ["SuggestPage"] = ["controls/Common/BreadcrumbBar.cs", "controls/Common/SegmentedControl.cs", "themes/Controls/Misc.axaml", "themes/Controls/CommonTheme.axaml"],
        ["StepperPage"] = ["controls/Common/Stepper.cs", "controls/Common/Chip.cs", "controls/Common/Toast.cs", "controls/Common/PersonPicture.cs", "themes/Controls/StepperTheme.axaml"],
    };

    private static readonly Assembly Asm = typeof(SourceIndex).Assembly;

    /// <summary>清单名统一成正斜杠后的查找表。Windows 上打包时 %(RecursiveDir) 出反斜杠。</summary>
    private static readonly Dictionary<string, string> Manifest =
        Asm.GetManifestResourceNames()
           .Where(static n => n.StartsWith("src://", StringComparison.Ordinal))
           .ToDictionary(static n => n.Replace('\\', '/'), static n => n);

    /// <summary>取一个页面的全部源码文件：示例在前，库实现在后。缺的静默跳过。</summary>
    public static IReadOnlyList<SourceFile> For(string pageTypeName)
    {
        var files = new List<SourceFile>();

        Add(files, $"pages/{pageTypeName}.axaml");
        Add(files, $"pages/{pageTypeName}.axaml.cs");

        if (Library.TryGetValue(pageTypeName, out var extra))
            foreach (var path in extra)
                Add(files, path);

        return files;
    }

    private static void Add(List<SourceFile> files, string path)
    {
        if (!Manifest.TryGetValue($"src://{path}", out var actual)) return;

        using var stream = Asm.GetManifestResourceStream(actual);
        if (stream is null) return;
        using var reader = new StreamReader(stream);

        var repoPath = path.Split('/')[0] switch
        {
            "pages" => $"samples/Cobalt.Fluent.Gallery/Pages/{path["pages/".Length..]}",
            "themes" => $"src/Cobalt.Fluent/Themes/{path["themes/".Length..]}",
            _ => $"src/Cobalt.Fluent/Controls/{path["controls/".Length..]}",
        };

        files.Add(new SourceFile(Path.GetFileName(path), repoPath, reader.ReadToEnd()));
    }
}
