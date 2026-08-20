using System.ComponentModel;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Cobalt.Fluent.Controls;

namespace Cobalt.Fluent.Gallery.Pages;

/// <summary>
/// 试玩区那 3 120 条模拟批次。
/// 筛选条件按 <see cref="Recipe"/> / <see cref="Status"/> / <see cref="DaysAgo"/> 三个原始字段判，
/// 不拿显示串去比 —— 「85.4 °C」和「近 7 天」这类串一改文案筛选就悄悄失效。
/// </summary>
public sealed class ToolbarBatch : INotifyPropertyChanged
{
    private bool _isPicked;

    public required string Batch { get; init; }

    public required string Recipe { get; init; }

    public required string Status { get; init; }

    public required InfoSeverity Severity { get; init; }

    /// <summary>中断的批次没有峰值。必须是 null 而不是 0 —— 0 是个会被当真的数。</summary>
    public double? Peak { get; init; }

    /// <summary>距今几天，「近 7 天」那个 Chip 按它筛。</summary>
    public int DaysAgo { get; init; }

    public string PeakText =>
        Peak is { } v ? v.ToString("F1", CultureInfo.InvariantCulture) + " °C" : "—";

    /// <summary>选择列。表头三态框要靠它汇总，所以得发 PropertyChanged。</summary>
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

public partial class DataGridToolbarPage : UserControl
{
    /// <summary>千位分隔用空格：等宽数字下逗号照样占满一格，一列里 412 和 1 204 会错开半格。</summary>
    private static readonly NumberFormatInfo Grouped = new()
    {
        NumberGroupSeparator = " ",
        NumberDecimalSeparator = ".",
    };

    /// <summary>上面那张静态表就三行，写死的，不参与试玩区的筛选。</summary>
    private static readonly ToolbarBatch[] Sample =
    [
        new() { Batch = "B-20260819-03", Recipe = "PES 标准", Peak = 85.4, Status = "完成", Severity = InfoSeverity.Success, IsPicked = true },
        new() { Batch = "B-20260819-02", Recipe = "PES 标准", Peak = 86.9, Status = "超调", Severity = InfoSeverity.Caution },
        new() { Batch = "B-20260818-07", Recipe = "结晶 A",   Peak = null, Status = "中断", Severity = InfoSeverity.Critical },
    ];

    private const int TotalRows = 3120;

    private static readonly ToolbarBatch[] All = Build();

    /// <summary>
    /// 每 5 条里有 2 条三个条件全中 —— 三个 Chip 都在时正好 1 248 条，和原文那个数对得上；
    /// 另外三条各自差一个条件，去掉哪个 Chip 就放进来哪一批，命中数是真算出来的不是编的。
    /// </summary>
    private static ToolbarBatch[] Build()
    {
        var baseDate = new DateOnly(2026, 8, 19);

        var seeds = new List<(string Recipe, string Status, int DaysAgo, int Index)>(TotalRows);
        for (var i = 0; i < TotalRows; i++)
        {
            var (recipe, status, daysAgo) = (i % 5) switch
            {
                0 or 1 => ("PES 标准", "完成", i % 7),
                2 => ("结晶 A", "完成", i % 7),                          // 配方不符
                3 => ("PES 标准", i % 2 == 0 ? "超调" : "中断", i % 7),   // 状态不符
                _ => ("PES 标准", "完成", 8 + i % 14),                    // 时间不符
            };

            seeds.Add((recipe, status, daysAgo, i));
        }

        var rows = new List<ToolbarBatch>(TotalRows);

        // 按日期分组编号，同一天里新的排前面 —— 批次号是给操作员读的，
        // 乱序的号码会让人以为表格排序坏了。
        foreach (var day in seeds.GroupBy(s => s.DaysAgo).OrderBy(g => g.Key))
        {
            var date = baseDate.AddDays(-day.Key);
            var seq = day.Count();

            foreach (var seed in day.OrderBy(s => s.Index))
            {
                double? peak = seed.Status switch
                {
                    "中断" => null,
                    "超调" => 86.5 + seed.Index % 7 * 0.1,
                    _ => (seed.Recipe == "结晶 A" ? 119.5 : 85.0) + seed.Index % 9 * 0.1,
                };

                rows.Add(new ToolbarBatch
                {
                    Batch = $"B-{date:yyyyMMdd}-{seq:000}",
                    Recipe = seed.Recipe,
                    Status = seed.Status,
                    Peak = peak,
                    DaysAgo = seed.DaysAgo,
                    Severity = seed.Status switch
                    {
                        "完成" => InfoSeverity.Success,
                        "超调" => InfoSeverity.Caution,
                        _ => InfoSeverity.Critical,
                    },
                });

                seq--;
            }
        }

        return [.. rows];
    }

    private readonly HashSet<string> _filters = ["recipe", "status", "days"];

    private DataGridToolbar _toolbar = null!;
    private DataGrid _playGrid = null!;
    private Pagination _pager = null!;

    private ToolbarBatch[] _view = [];
    private int _pageSize = 20;

    public DataGridToolbarPage()
    {
        AvaloniaXamlLoader.Load(this);

        var grid = this.FindControl<DataGrid>("Grid")!;
        grid.ItemsSource = Sample;

        // 行状态类只能在 LoadingRow 里打：行是复用的，所以两个分支都要写，
        // 只在命中时 Add 的话，被回收的那行会把红底带给下一条数据。
        grid.LoadingRow += (_, e) =>
        {
            var isError = e.Row.DataContext is ToolbarBatch { Severity: InfoSeverity.Critical };

            if (isError && !e.Row.Classes.Contains("row-error")) e.Row.Classes.Add("row-error");
            else if (!isError) e.Row.Classes.Remove("row-error");
        };

        _toolbar = this.FindControl<DataGridToolbar>("PlayToolbar")!;
        _playGrid = this.FindControl<DataGrid>("PlayGrid")!;
        _pager = this.FindControl<Pagination>("Pager")!;

        var chips = new[] { "RecipeChip", "StatusChip", "DaysChip" }
            .Select(n => this.FindControl<Chip>(n)!)
            .ToArray();

        // Chip 点 × 只发 Closed，删不删由外面定 —— 控件自己删的话，
        // 「删之前先问一句」这种流程就没地方插了。
        foreach (var chip in chips)
        {
            chip.Closed += (_, _) =>
            {
                _toolbar.Items.Remove(chip);
                _filters.Remove((string)chip.Tag!);
                ApplyFilter();
            };
        }

        this.FindControl<Button>("ClearFilters")!.Click += (_, _) =>
        {
            foreach (var chip in chips) _toolbar.Items.Remove(chip);
            _filters.Clear();
            ApplyFilter();
        };

        var pageSize = this.FindControl<ComboBox>("PageSize")!;
        pageSize.SelectionChanged += (_, _) =>
        {
            if (pageSize.SelectedItem is ComboBoxItem { Tag: string tag } && int.TryParse(tag, out var size))
                _pageSize = size;

            ApplyFilter();
        };

        _pager.PropertyChanged += (_, e) =>
        {
            if (e.Property == Pagination.CurrentPageProperty) ShowPage();
        };

        ApplyFilter();
    }

    /// <summary>筛选条件一变，页数和当前页都得重算。</summary>
    private void ApplyFilter()
    {
        IEnumerable<ToolbarBatch> query = All;

        if (_filters.Contains("recipe")) query = query.Where(b => b.Recipe == "PES 标准");
        if (_filters.Contains("status")) query = query.Where(b => b.Status == "完成");
        if (_filters.Contains("days")) query = query.Where(b => b.DaysAgo < 7);

        _view = [.. query];

        _toolbar.CountText = $"共 {_view.Length.ToString("#,0", Grouped)} 条";
        _pager.TotalItems = _view.Length;
        _pager.PageCount = Math.Max(1, (_view.Length + _pageSize - 1) / _pageSize);

        // 退回第 1 页。留着旧页码是个坑：条件放宽之后第 40 页装的完全不是同一批数据。
        _pager.CurrentPage = 1;

        ShowPage();
    }

    /// <summary>当前页的切片。这里是内存里切，真项目上万条要把页码和条件一起发给服务端。</summary>
    private void ShowPage()
    {
        var page = Math.Clamp(_pager.CurrentPage, 1, Math.Max(1, _pager.PageCount));
        _playGrid.ItemsSource = _view.Skip((page - 1) * _pageSize).Take(_pageSize).ToArray();
    }
}
