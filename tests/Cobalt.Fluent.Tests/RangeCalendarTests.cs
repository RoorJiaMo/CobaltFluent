using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Cobalt.Fluent.Controls;
using Xunit;

namespace Cobalt.Fluent.Tests;

/// <summary>
/// 区间选择的三个伪类分得对不对。
///
/// 规格要的是「中间段方角连成一条，首尾各圆一边」。
/// Avalonia 的 Calendar 在 SingleRange 下只给 <c>:selected</c>——区间里每一天
/// 拿到的是同一个态，渲染出来是一串互不相连的圆点。
/// <see cref="RangeCalendar"/> 按 SelectedDates 的首尾把差的那三个补上，
/// 这里把「谁该拿到哪个」钉死：改坏了截图上只是圆角变了，肉眼未必看得出来。
///
/// 中段**不算选中**：它走常规文字色 + subtle 底，和首尾的 accent 实底分得开。
/// </summary>
public class RangeCalendarTests
{
    /// <summary>
    /// 把日历挂进窗口跑完布局，按日期取出当月的日期格。
    ///
    /// 日期格是 CalendarItem 在布局期间造的，不 Show 就一个都没有；
    /// 伪类又挂在 LayoutUpdated 上，所以布局后还得再抽一次任务队列。
    /// </summary>
    private static Dictionary<DateTime, CalendarDayButton> Days(RangeCalendar calendar)
    {
        var window = new Window { Width = 400, Height = 400, Content = calendar };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var map = new Dictionary<DateTime, CalendarDayButton>();
        foreach (var button in calendar.GetVisualDescendants().OfType<CalendarDayButton>())
        {
            if (button.DataContext is DateTime day)
                map[day.Date] = button;
        }

        return map;
    }

    private static (bool Start, bool Middle, bool End) Range(CalendarDayButton button) =>
        (button.Classes.Contains(":rangestart"),
         button.Classes.Contains(":inrange"),
         button.Classes.Contains(":rangeend"));

    [AvaloniaFact]
    public void 区间首尾拿到端点伪类中段拿到inrange()
    {
        var anchor = new DateTime(2026, 8, 10);
        var calendar = new RangeCalendar { DisplayDate = anchor };
        calendar.SelectedDates.AddRange(anchor, anchor.AddDays(3));

        var days = Days(calendar);

        Assert.Equal((true, false, false), Range(days[anchor]));
        Assert.Equal((false, true, false), Range(days[anchor.AddDays(1)]));
        Assert.Equal((false, true, false), Range(days[anchor.AddDays(2)]));
        Assert.Equal((false, false, true), Range(days[anchor.AddDays(3)]));

        // 区间外一律干净：端点的圆角要是漏到隔壁，一整行都会变成方块
        Assert.Equal((false, false, false), Range(days[anchor.AddDays(-1)]));
        Assert.Equal((false, false, false), Range(days[anchor.AddDays(4)]));
    }

    [AvaloniaFact]
    public void 只选一天时三个伪类都不加()
    {
        var anchor = new DateTime(2026, 8, 10);
        var calendar = new RangeCalendar { DisplayDate = anchor };
        calendar.SelectedDates.Add(anchor);

        var days = Days(calendar);

        // 单日不是区间：让它走原本的 :selected 圆点，
        // 补上端点伪类的话一颗孤零零的日子会被切成半圆。
        Assert.Equal((false, false, false), Range(days[anchor]));
        Assert.Contains(":selected", days[anchor].Classes);
    }

    [AvaloniaFact]
    public void 清空选择后端点伪类跟着撤掉()
    {
        var anchor = new DateTime(2026, 8, 10);
        var calendar = new RangeCalendar { DisplayDate = anchor };
        calendar.SelectedDates.AddRange(anchor, anchor.AddDays(2));

        var days = Days(calendar);
        Assert.Equal((true, false, false), Range(days[anchor]));

        calendar.SelectedDates.Clear();
        Dispatcher.UIThread.RunJobs();
        calendar.InvalidateArrange();
        Dispatcher.UIThread.RunJobs();

        // 伪类是加上去的，不会自己掉——清空后没撤干净的话，
        // 下一段区间会带着上一段的半圆。
        foreach (var day in days.Values)
            Assert.Equal((false, false, false), Range(day));
    }
}
