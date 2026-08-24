using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace Cobalt.Fluent.Tests;

/// <summary>
/// 控件层里由反射绑定改成编译绑定的那 38 处。
///
/// **编译过不等于行为不变。** 反射绑定在运行时按对象实际类型解析路径，编译绑定
/// 在编译期按静态类型解析——路径解错了不会报错，绑定只是不更新，界面照样画出来，
/// 少的是那一处的联动。这正是本仓库反复处理的那类缺陷：失效方向朝着「一切正常」。
///
/// 所以这里钉的不是「能不能编译」，是每一处**绑定两端真的连上了**。
/// </summary>
public class CompiledBindingTests
{
    // ---- TemplatedParent 上带路径的绑定 --------------------------------------

    [AvaloniaFact]
    public void 下拉面板的最小宽度跟着框走()
    {
        // {Binding Bounds.Width, RelativeSource=TemplatedParent}
        // TemplateBinding 收不了 Bounds.Width 这种带路径的，只能走 Binding——
        // 编译绑定要能在 ComboBox 这个静态类型上解出 Bounds.Width 才算数。
        // 断了的话下拉面板会缩成内容宽度，比框窄一截。
        var box = Mount(new ComboBox { Width = 260, ItemsSource = new[] { "甲", "乙", "丙" } });

        var popup = Part<Popup>(box, "PART_Popup");

        Assert.Equal(260, popup.MinWidth, 1);
    }

    // ---- $parent[Type] 绑定 ---------------------------------------------------

    [AvaloniaFact]
    public void 数字框把AllowSpin转达给内部的微调器()
    {
        // {Binding $parent[NumericUpDown].AllowSpin}
        // 断了的话，AllowSpin=False 的数字框上那对上下箭头照样能点——
        // 一个明确关掉的输入通道还开着。
        var box = Mount(new NumericUpDown { AllowSpin = false, ShowButtonSpinner = false });

        var spinner = Descendant<ButtonSpinner>(box);

        Assert.False(spinner.AllowSpin);
        Assert.False(spinner.ShowButtonSpinner);
    }

    [AvaloniaFact]
    public void 数字框的AllowSpin改了之后内部微调器跟着改()
    {
        // 只测初值不够：初值可能是默认值碰巧相同。要看它跟不跟。
        var box = Mount(new NumericUpDown { AllowSpin = true, ShowButtonSpinner = true });
        var spinner = Descendant<ButtonSpinner>(box);
        Assert.True(spinner.AllowSpin);

        box.AllowSpin = false;
        Dispatcher.UIThread.RunJobs();

        Assert.False(spinner.AllowSpin);
    }

    [AvaloniaFact]
    public void 数字框右侧附加内容能透传进去()
    {
        // {Binding $parent[NumericUpDown].InnerRightContent}
        var marker = new TextBlock { Text = "°C" };
        var box = Mount(new NumericUpDown { InnerRightContent = marker });

        Assert.Contains(marker, box.GetVisualDescendants().OfType<TextBlock>());
    }

    [AvaloniaFact]
    public void 自动完成框右侧附加内容能透传进去()
    {
        // {Binding $parent[AutoCompleteBox].InnerRightContent}
        var marker = new TextBlock { Text = "?" };
        var box = Mount(new AutoCompleteBox { InnerRightContent = marker, Width = 240 });

        Assert.Contains(marker, box.GetVisualDescendants().OfType<TextBlock>());
    }

    // ---- MultiBinding 里的元素名 + TemplatedParent ----------------------------

    [AvaloniaFact]
    public void 输入框的水印只在没有内容时显示()
    {
        // MultiBinding：<Binding ElementName=PART_TextPresenter Path=PreeditText>
        //             + <Binding RelativeSource=TemplatedParent Path=Text>
        // 两条腿里任何一条解错，水印要么一直显示（压在用户输入的字上），
        // 要么一直不显示。
        var box = Mount(new TextBox { Watermark = "请输入设定值", Width = 200 });

        var watermark = Part<TextBlock>(box, "PART_Watermark");
        Assert.True(watermark.IsVisible);

        box.Text = "85";
        Dispatcher.UIThread.RunJobs();

        Assert.False(watermark.IsVisible);
    }

    [AvaloniaFact]
    public void 可编辑下拉框的占位文字只在没内容时显示()
    {
        // {Binding Text, RelativeSource=TemplatedParent, Converter=IsNullOrEmpty}
        // 可编辑下拉框内部那只 TextBox 走 ComboBoxEditableTextBox 主题，
        // 水印绑的是**那只 TextBox** 的 Text，不是下拉框的。
        var box = Mount(new ComboBox
        {
            IsEditable = true,
            PlaceholderText = "选择通道",
            ItemsSource = new[] { "甲", "乙" },
            Width = 200,
        });

        var watermark = Part<TextBlock>(box, "PART_Watermark");
        Assert.True(watermark.IsVisible);

        box.Text = "甲";
        Dispatcher.UIThread.RunJobs();

        Assert.False(watermark.IsVisible);
    }

    // ---- #元素名.属性 ---------------------------------------------------------

    [AvaloniaFact]
    public void 展开时按钮拿到open类()
    {
        // Classes.open="{Binding #PART_Popup.IsOpen}"
        // Avalonia 的 DatePicker / TimePicker 没有「展开」伪类，用类绑定代替。
        // 断了的话箭头不翻转、边框不高亮——「已经展开了」在视觉上没有反馈。
        var picker = Mount(new DatePicker { Width = 320 });
        var button = Part<Button>(picker, "PART_FlyoutButton");

        Assert.DoesNotContain("open", button.Classes);

        Part<Popup>(picker, "PART_Popup").IsOpen = true;
        Dispatcher.UIThread.RunJobs();

        Assert.Contains("open", button.Classes);

        Part<Popup>(picker, "PART_Popup").IsOpen = false;
        Dispatcher.UIThread.RunJobs();

        Assert.DoesNotContain("open", button.Classes);
    }

    [AvaloniaFact]
    public void 时间选择器展开时也拿到open类()
    {
        // TimePicker 是另一处独立的站点（DateTime.axaml:789），和 DatePicker 那处
        // 长得一样但各绑各的，得分开钉。
        var picker = Mount(new TimePicker { Width = 320 });
        var button = Part<Button>(picker, "PART_FlyoutButton");

        Assert.DoesNotContain("open", button.Classes);

        Part<Popup>(picker, "PART_Popup").IsOpen = true;
        Dispatcher.UIThread.RunJobs();

        Assert.Contains("open", button.Classes);
    }

    // ---- TwoWay -------------------------------------------------------------

    [AvaloniaFact]
    public void 日历选择器的选中日期到达内部日历()
    {
        // {Binding SelectedDate, Mode=TwoWay, RelativeSource=TemplatedParent}
        //
        // 变异验证的结果值得记下来：把整条绑定拿掉，这条测试转红；
        // 但只把 Mode=TwoWay 去掉，它照样绿——**反向同步是 CalendarDatePicker
        // 自己的代码在做，不是这条绑定**。所以下面那半段反向断言测的不是这条绑定，
        // 留着是为了守住控件整体的行为，别把它当成 TwoWay 的证据。
        var picker = Mount(new CalendarDatePicker { Width = 320 });
        Part<Popup>(picker, "PART_Popup").IsOpen = true;
        Dispatcher.UIThread.RunJobs();

        // 弹出层的内容挂在 OverlayLayer 上，不在 picker 自己的可视树里。
        var calendar = InPopup<Calendar>(picker, "PART_Popup", "PART_Calendar");
        var day = new DateTime(2026, 8, 24);

        picker.SelectedDate = day;
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(day, calendar.SelectedDate);

        var other = new DateTime(2026, 9, 1);
        calendar.SelectedDate = other;
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(other, picker.SelectedDate);
    }

    // ---- 辅助 ----------------------------------------------------------------

    private static T Part<T>(Control host, string name) where T : Control
    {
        var found = host.GetVisualDescendants().OfType<T>().FirstOrDefault(c => c.Name == name);
        Assert.True(found is not null, $"模板里找不到 {name}");
        return found!;
    }

    /// <summary>
    /// 弹出层里的部件。Popup 在无头环境下挂的是独立的 PopupRoot，
    /// 既不在宿主自己的可视树里，也不在主窗口的可视树里——只能从 Child 往下找。
    /// </summary>
    private static T InPopup<T>(Control host, string popupName, string name) where T : Control
    {
        var child = Part<Popup>(host, popupName).Child;
        Assert.True(child is not null, $"{popupName} 没有内容");

        var found = child as T is { } self && self.Name == name
            ? self
            : child!.GetVisualDescendants().OfType<T>().FirstOrDefault(c => c.Name == name)
              ?? child.GetLogicalDescendants().OfType<T>().FirstOrDefault(c => c.Name == name);

        Assert.True(found is not null, $"{popupName} 里找不到 {name}");
        return found!;
    }

    private static T Descendant<T>(Control host) where T : Control
    {
        var found = host.GetVisualDescendants().OfType<T>().FirstOrDefault();
        Assert.True(found is not null, $"模板里找不到 {typeof(T).Name}");
        return found!;
    }

    private static T Mount<T>(T control) where T : Control
    {
        var window = new Window
        {
            Width = 900,
            Height = 400,
            Content = new StackPanel { Children = { control } },
        };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        window.Measure(new Size(900, 400));
        window.Arrange(new Rect(0, 0, 900, 400));
        Dispatcher.UIThread.RunJobs();
        return control;
    }
}
