using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Cobalt.Fluent.Gallery.Pages;

public partial class DateTimePickerPage : UserControl
{
    public DateTimePickerPage()
    {
        AvaloniaXamlLoader.Load(this);

        var date = this.FindControl<CalendarDatePicker>("PlayDate")!;
        var time = this.FindControl<TimePicker>("PlayTime")!;
        var echo = this.FindControl<TextBlock>("Echo")!;
        var clear = this.FindControl<Button>("ClearPlay")!;

        // 回填结果单独回显，是为了让「点 ✕ / 点面板外放弃」这条可验证：
        // 滚轮滚起来收起态并不跟着变，只有按 ✓ 才提交，这里才动。
        void Refresh()
        {
            var d = date.SelectedDate is { } dv ? dv.ToString("yyyy-MM-dd") : "—";
            var t = time.SelectedTime is { } tv ? tv.ToString(@"hh\:mm\:ss") : "—";
            echo.Text = $"批次起点  {d}  {t}";
        }

        date.SelectedDateChanged += (_, _) => Refresh();
        time.SelectedTimeChanged += (_, _) => Refresh();

        clear.Click += (_, _) =>
        {
            date.SelectedDate = null;
            time.SelectedTime = null;
        };

        Refresh();
    }
}
