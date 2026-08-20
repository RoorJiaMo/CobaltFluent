using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.VisualTree;

namespace Cobalt.Fluent.Controls;

/// <summary>
/// 支持区间选择视觉的日历。
///
/// Avalonia 的 <see cref="Calendar"/> 在 <c>SelectionMode=SingleRange</c> 下会把区间里
/// 每一天都标成 <c>:selected</c>，于是渲染出一串互不相连的圆点。
/// 而规格要的是「中间段方角连成一条」：首尾各圆一边，中间方角、底色更淡。
///
/// 这里按 <see cref="Calendar.SelectedDates"/> 的首尾给日期格补三个伪类：
/// <c>:rangestart</c> · <c>:inrange</c> · <c>:rangeend</c>，样式写在 DateTime.axaml 里。
/// 单选（区间只有一天）时三个伪类都不加，走原本的 <c>:selected</c> 圆点。
/// </summary>
public class RangeCalendar : Calendar
{
    private const string InRange = ":inrange";
    private const string RangeStart = ":rangestart";
    private const string RangeEnd = ":rangeend";

    /// <summary>沿用 Calendar 的 ControlTheme —— 模板一样，只是多补几个伪类。</summary>
    protected override Type StyleKeyOverride => typeof(Calendar);

    public RangeCalendar()
    {
        SelectionMode = CalendarSelectionMode.SingleRange;

        // 日期格是 CalendarItem 在布局期间造/复用的，没有「格子准备好了」这种事件，
        // 所以挂 LayoutUpdated：42 个按钮，一次遍历的代价可以忽略。
        LayoutUpdated += (_, _) => UpdateRangePseudoClasses();
    }

    private void UpdateRangePseudoClasses()
    {
        var buttons = this.GetVisualDescendants().OfType<CalendarDayButton>().ToList();
        if (buttons.Count == 0) return;

        DateTime? first = null;
        DateTime? last = null;

        foreach (var date in SelectedDates)
        {
            if (first is null || date < first) first = date;
            if (last is null || date > last) last = date;
        }

        // 只选了一天就不是区间：让它走原本的 :selected 圆点
        var isRange = first is { } f && last is { } l && f.Date != l.Date;

        foreach (var button in buttons)
        {
            var classes = (IPseudoClasses)button.Classes;

            if (!isRange || button.DataContext is not DateTime day)
            {
                classes.Set(RangeStart, false);
                classes.Set(InRange, false);
                classes.Set(RangeEnd, false);
                continue;
            }

            var date = day.Date;
            var start = date == first!.Value.Date;
            var end = date == last!.Value.Date;

            classes.Set(RangeStart, start);
            classes.Set(RangeEnd, end);
            // 中间段：在区间内但不是端点
            classes.Set(InRange, !start && !end && date > first.Value.Date && date < last.Value.Date);
        }
    }
}
