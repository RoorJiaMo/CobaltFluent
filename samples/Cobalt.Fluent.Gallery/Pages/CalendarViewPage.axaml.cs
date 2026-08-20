// 只引类型不引命名空间：System.Globalization.Calendar 会和 Avalonia.Controls.Calendar 撞名
using CultureInfo = System.Globalization.CultureInfo;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Cobalt.Fluent.Gallery.Pages;

public partial class CalendarViewPage : UserControl
{
    /// <summary>展柜里「本批次」写死成最近三天；真实界面上这个窗口由当前选中的批次记录给。</summary>
    private const int BatchDays = 3;

    private bool _syncing;

    public CalendarViewPage()
    {
        AvaloniaXamlLoader.Load(this);

        var today = DateTime.Today;

        SetUpSpecCalendar(today);
        SetUpRangePicker(today);
        SetUpPlayCalendar();
    }

    private void SetUpSpecCalendar(DateTime today)
    {
        var calendar = this.FindControl<Calendar>("SpecCalendar")!;

        // 区间不写死在 2026-08：展柜哪天打开，:today 都得落在区间末端，
        // 否则「今天=描边、选中=实底、两者同时命中」这条规格就演示不出来。
        var start = today.AddDays(-2);
        // 月初时往回两天会掉到上个月，起点在当前视图里根本看不见
        if (start.Month != today.Month)
            start = new DateTime(today.Year, today.Month, 1);

        calendar.SelectedDates.AddRange(start, today);

        // 先选后禁：BlackoutDates 拒绝盖住已选日期，顺序反了会抛。
        calendar.BlackoutDates.Add(new CalendarDateRange(today.AddDays(1), today.AddYears(3)));
    }

    private void SetUpRangePicker(DateTime today)
    {
        var from = this.FindControl<CalendarDatePicker>("RangeFrom")!;
        var to = this.FindControl<CalendarDatePicker>("RangeTo")!;
        var echo = this.FindControl<TextBlock>("RangeEcho")!;

        void Sync()
        {
            if (from.SelectedDate is not { } a || to.SelectedDate is not { } b)
            {
                echo.Text = "选一段区间";
                return;
            }

            // 互相卡范围：倒序区间在业务上没有意义，让它根本翻不出来，
            // 比等查询回来报「结束早于开始」强。
            to.DisplayDateStart = a;
            from.DisplayDateEnd = b;

            var days = (b.Date - a.Date).Days + 1;
            echo.Text = $"{Iso(a)} 至 {Iso(b)} · 共 {days} 天";
        }

        // 手动拨反了直接把另一头跟过去，不弹提示——操作员的意图很明确，
        // 是要把区间挪到那儿，不是要看一句错误。
        from.SelectedDateChanged += (_, _) =>
        {
            if (_syncing) return;
            if (from.SelectedDate is { } a && to.SelectedDate is { } b && b < a)
                Set(to, a);
            Sync();
        };

        to.SelectedDateChanged += (_, _) =>
        {
            if (_syncing) return;
            if (from.SelectedDate is { } a && to.SelectedDate is { } b && b < a)
                Set(from, b);
            Sync();
        };

        void Apply(DateTime a, DateTime b)
        {
            // 先松开两边的可选范围再落值，否则新窗口整个落在旧范围之外时会被卡住
            from.DisplayDateEnd = null;
            to.DisplayDateStart = null;
            Set(from, a);
            Set(to, b);
            Sync();
        }

        this.FindControl<Button>("PresetToday")!.Click += (_, _) => Apply(today, today);
        this.FindControl<Button>("Preset7")!.Click += (_, _) => Apply(today.AddDays(-6), today);
        this.FindControl<Button>("Preset30")!.Click += (_, _) => Apply(today.AddDays(-29), today);
        this.FindControl<Button>("PresetBatch")!.Click += (_, _) => Apply(today.AddDays(-(BatchDays - 1)), today);

        Apply(today.AddDays(-6), today);
    }

    private void SetUpPlayCalendar()
    {
        var calendar = this.FindControl<Calendar>("PlayCalendar")!;
        var echo = this.FindControl<TextBlock>("PlayEcho")!;

        void Refresh()
        {
            if (calendar.SelectedDates.Count == 0)
            {
                echo.Text = "点一天选起点，再点一天选终点";
                return;
            }

            var a = calendar.SelectedDates.Min();
            var b = calendar.SelectedDates.Max();
            echo.Text = a == b
                ? $"{Iso(a)} · 单日"
                : $"{Iso(a)} 至 {Iso(b)} · 共 {(b.Date - a.Date).Days + 1} 天";
        }

        calendar.SelectedDatesChanged += (_, _) => Refresh();
        this.FindControl<Button>("PlayClear")!.Click += (_, _) => calendar.SelectedDates.Clear();

        Refresh();
    }

    private void Set(CalendarDatePicker picker, DateTime value)
    {
        _syncing = true;
        picker.SelectedDate = value;
        _syncing = false;
    }

    // 日期一律 ISO：这个字符串要拿去和工单、日志对，跟着系统区域走就对不上了。
    private static string Iso(DateTime value) =>
        value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
