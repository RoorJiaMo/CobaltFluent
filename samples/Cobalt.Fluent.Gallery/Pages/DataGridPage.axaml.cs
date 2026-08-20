using System.ComponentModel;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Cobalt.Fluent.Controls;

namespace Cobalt.Fluent.Gallery.Pages;

/// <summary>
/// 一条工艺批次记录。展柜里是死数据，字段照真实项目的批次表排。
/// 数值以原始类型存，显示串由只读属性现算 —— 存字符串的话列就没法按数值排序，
/// 也没法在别处换个单位重用。
/// </summary>
public sealed class BatchRecord
{
    /// <summary>千位分隔用空格而不是逗号。等宽数字下逗号照样占满一格，
    /// 一列里 412 和 1 204 的小数点会错开半格。</summary>
    private static readonly NumberFormatInfo Grouped = new()
    {
        NumberGroupSeparator = " ",
        NumberDecimalSeparator = ".",
    };

    public required string Batch { get; init; }

    public required string Recipe { get; init; }

    public required double Target { get; init; }

    /// <summary>中断的批次没有峰值。这里必须是 null 而不是 0——0 是个会被当真的数。</summary>
    public double? Peak { get; init; }

    public required int Elapsed { get; init; }

    public required string Status { get; init; }

    public required InfoSeverity Severity { get; init; }

    public string TargetText => Target.ToString("F1", CultureInfo.InvariantCulture) + " °C";

    public string PeakText =>
        Peak is { } v ? v.ToString("F1", CultureInfo.InvariantCulture) + " °C" : "—";

    public string ElapsedText => Elapsed.ToString("#,0", Grouped) + " s";
}

/// <summary>
/// 试玩区的行。勾选态要双向流动（行 → 表头汇总、表头 → 全体行），
/// 所以必须发 PropertyChanged——只靠 CheckBox 的 TwoWay 绑定，
/// 从代码这一侧改 <see cref="IsPicked"/> 时界面不会跟着动。
/// </summary>
public sealed class PickableBatch : INotifyPropertyChanged
{
    private bool _isPicked;

    public required string Batch { get; init; }

    public required string Recipe { get; init; }

    public required string PeakText { get; init; }

    public bool IsPicked
    {
        get => _isPicked;
        set
        {
            if (_isPicked == value) return;
            _isPicked = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsPicked)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}

public partial class DataGridPage : UserControl
{
    private static readonly BatchRecord[] Batches =
    [
        new() { Batch = "B-20260819-03", Recipe = "PES 标准", Target = 85.0,  Peak = 85.4,  Elapsed = 1204, Status = "完成", Severity = InfoSeverity.Success },
        new() { Batch = "B-20260819-02", Recipe = "PES 标准", Target = 85.0,  Peak = 86.9,  Elapsed = 1198, Status = "超调", Severity = InfoSeverity.Caution },
        new() { Batch = "B-20260819-01", Recipe = "结晶 A",   Target = 120.0, Peak = 119.8, Elapsed = 3640, Status = "完成", Severity = InfoSeverity.Success },
        new() { Batch = "B-20260818-07", Recipe = "结晶 A",   Target = 120.0, Peak = null,  Elapsed = 412,  Status = "中断", Severity = InfoSeverity.Critical },
    ];

    private static readonly PickableBatch[] Picks =
    [
        new() { Batch = "B-20260819-03", Recipe = "PES 标准", PeakText = "85.4 °C" },
        new() { Batch = "B-20260819-02", Recipe = "PES 标准", PeakText = "86.9 °C" },
        new() { Batch = "B-20260818-07", Recipe = "结晶 A",   PeakText = "92.1 °C" },
    ];

    private bool _syncing;

    public DataGridPage()
    {
        AvaloniaXamlLoader.Load(this);

        this.FindControl<DataGrid>("Grid")!.ItemsSource = Batches;

        var play = this.FindControl<DataGrid>("PlayGrid")!;
        play.ItemsSource = Picks;

        // 表头那只框放在列的 Header 上，不在 UserControl 的可视树里，
        // 从列上取比 FindControl 稳。
        var all = (CheckBox)play.Columns[0].Header!;

        // 行 → 表头：部分选中是「结果」，只由这里算出来。
        void SyncHeader()
        {
            if (_syncing) return;
            _syncing = true;

            var picked = Picks.Count(p => p.IsPicked);
            all.IsChecked = picked switch
            {
                0 => false,
                var n when n == Picks.Length => true,
                _ => null,
            };

            _syncing = false;
        }

        foreach (var pick in Picks)
            pick.PropertyChanged += (_, _) => SyncHeader();

        // 表头 → 行。按「点之前是什么」判方向，而不是按点完之后的值：
        // 三态 CheckBox 从 checked 点一下会落到 indeterminate，
        // 而 indeterminate 对全选框来说不是一个用户点得出来的态——它只表示部分选中。
        // 于是 部分/全不选 → 全选，全选 → 全不选，一次点击到位。
        var previous = all.IsChecked;
        all.IsCheckedChanged += (_, _) =>
        {
            if (_syncing)
            {
                previous = all.IsChecked;
                return;
            }

            var selectAll = previous != true;

            _syncing = true;
            foreach (var pick in Picks) pick.IsPicked = selectAll;
            all.IsChecked = selectAll;
            previous = all.IsChecked;
            _syncing = false;
        };

        SyncHeader();
    }
}
