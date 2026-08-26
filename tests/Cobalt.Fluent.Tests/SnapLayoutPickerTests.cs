using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Cobalt.Fluent;
using Cobalt.Fluent.Controls;
using Xunit;

namespace Cobalt.Fluent.Tests;

/// <summary>
/// 自绘的贴靠布局面板。
///
/// 这个面板存在的全部理由是「不指望 shell」，所以它必须在无头 Linux 上就完整可用——
/// 这里测的就是那件事：布局挑得对、格子摆得对、按下去窗口真的过去了。
/// </summary>
public class SnapLayoutPickerTests
{
    private static (Window Window, SnapLayoutPicker Picker) Show(Action<SnapLayoutPicker>? configure = null)
    {
        var picker = new SnapLayoutPicker();
        configure?.Invoke(picker);

        var window = new Window { Width = 800, Height = 600, Content = picker };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        window.Measure(new Size(800, 600));
        window.Arrange(new Rect(0, 0, 800, 600));
        Dispatcher.UIThread.RunJobs();
        return (window, picker);
    }

    private static SnapZoneButton[] Cells(SnapLayoutPicker picker) =>
        picker.GetVisualDescendants().OfType<SnapZoneButton>().ToArray();

    // ---- 面板内容 ------------------------------------------------------------

    [AvaloniaFact]
    public void 按当前屏幕给出布局()
    {
        var (window, picker) = Show();

        Assert.NotEmpty(picker.Layouts);
        Assert.Equal(WindowSnap.LayoutsFor(window).Select(l => l.Kind), picker.Layouts.Select(l => l.Kind));
    }

    [AvaloniaFact]
    public void 每套布局的每一块都造出一个格子()
    {
        var (_, picker) = Show();

        Assert.Equal(picker.Layouts.Sum(l => l.Zones.Count), Cells(picker).Length);
    }

    [AvaloniaFact]
    public void 格子按分区比例摆放()
    {
        // 面板上画的每一格必须就是窗口要去的那块。摆错的话，
        // 操作员看着「左半屏」按下去，窗口跑到右边——这比不给面板更糟。
        var (_, picker) = Show();

        var panel = picker.GetVisualDescendants().OfType<SnapZonePanel>().First();
        var cells = panel.Children.OfType<SnapZoneButton>().ToArray();
        var w = panel.Bounds.Width;
        var h = panel.Bounds.Height;

        Assert.True(w > 0 && h > 0, "面板没跑布局，下面的判定就没有意义");

        for (var i = 0; i < cells.Length; i++)
        {
            var zone = SnapZonePanel.GetZone(cells[i]);
            var bounds = cells[i].Bounds;

            // Gap 是纯视觉的，允许每边各让出 Gap/2。
            Assert.True(Math.Abs(bounds.Center.X - (zone.X + zone.Width / 2) * w) < 2,
                $"第 {i} 格横向中心对不上：{bounds} vs {zone}");
            Assert.True(Math.Abs(bounds.Center.Y - (zone.Y + zone.Height / 2) * h) < 2,
                $"第 {i} 格纵向中心对不上：{bounds} vs {zone}");
        }
    }

    [AvaloniaFact]
    public void 不认识的分区不画()
    {
        // 摊平成整块画出来的话，示意图会显示一个铺满全屏的分区，
        // 而按下去窗口去的是别处。
        var panel = new SnapZonePanel { Width = 100, Height = 60 };
        var good = new SnapZoneButton();
        var bad = new SnapZoneButton();
        SnapZonePanel.SetZone(good, new SnapZone(0, 0, 0.5, 1));
        SnapZonePanel.SetZone(bad, new SnapZone(0, 0, double.NaN, 1));
        panel.Children.Add(good);
        panel.Children.Add(bad);

        var window = new Window { Width = 400, Height = 300, Content = panel };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        window.Measure(new Size(400, 300));
        window.Arrange(new Rect(0, 0, 400, 300));
        Dispatcher.UIThread.RunJobs();

        Assert.True(good.Bounds.Width > 0);
        Assert.Equal(0, bad.Bounds.Width);
    }

    // ---- 按下去要真的贴过去 --------------------------------------------------

    [AvaloniaFact]
    public void 按下一格窗口就贴过去()
    {
        var (window, picker) = Show();
        var cell = Cells(picker)[0];
        var zone = SnapZonePanel.GetZone(cell);
        var expected = WindowSnap.ZoneRectFor(window, zone);

        cell.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Assert.NotNull(expected);
        Assert.Equal(expected!.Value.Position, window.Position);
        Assert.Equal(expected.Value.Width, (int)window.Width);
    }

    [AvaloniaFact]
    public void 事件带着布局与分区一起报出来()
    {
        var (_, picker) = Show();
        SnapZoneSelectedEventArgs? seen = null;
        picker.ZoneSelected += (_, e) => seen = e;

        var cell = Cells(picker)[1];
        cell.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Assert.NotNull(seen);
        Assert.Equal(SnapZonePanel.GetZone(cell), seen!.Zone);
        Assert.Equal(1, seen.Index);
        Assert.Contains(seen.Zone, seen.Layout.Zones);
    }

    [AvaloniaFact]
    public void 关掉自动贴靠就只发事件()
    {
        // 正经场景里这一下可能要先确认、要记住布局、要顺带安排别的窗口，
        // 所以留一条「只通知、不动手」的路。
        var (window, picker) = Show(p => p.SnapSelectedWindow = false);
        var raised = 0;
        picker.ZoneSelected += (_, _) => raised++;

        Cells(picker)[0].RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(1, raised);
        Assert.False(WindowSnap.IsSnapped(window));
        Assert.Equal(800, window.Width);
    }

    [AvaloniaFact]
    public void 指定了目标窗口就摆那一个()
    {
        // 面板可以画在 A 窗口里而摆 B 窗口——撕出去的趋势窗要摆回主屏时就是这个用法。
        var other = new Window { Width = 500, Height = 400 };
        other.Show();
        Dispatcher.UIThread.RunJobs();

        var (host, picker) = Show(p => p.TargetWindow = other);

        Cells(picker)[0].RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Assert.True(WindowSnap.IsSnapped(other));
        Assert.False(WindowSnap.IsSnapped(host));
    }

    // ---- 朗读名 --------------------------------------------------------------

    [AvaloniaFact]
    public void 每一格的朗读名说的是方位而不是序号()
    {
        // 读屏念「区域 2/4」等于没说。念「右上四分之一」操作员才知道
        // 按下去窗口会去哪。
        var (_, picker) = Show();
        var names = Cells(picker).Select(AutomationProperties.GetName).ToArray();

        Assert.All(names, n => Assert.False(string.IsNullOrWhiteSpace(n)));
        Assert.Contains("左半屏", names);
    }

    [AvaloniaFact]
    public void 换语言之后朗读名跟着换()
    {
        var (_, picker) = Show();
        Assert.Contains("左半屏", Cells(picker).Select(AutomationProperties.GetName));

        var saved = CobaltStrings.Current;
        try
        {
            CobaltStrings.Current = new CobaltStrings();
            Dispatcher.UIThread.RunJobs();
            Assert.Contains("Left half", Cells(picker).Select(AutomationProperties.GetName));
        }
        finally
        {
            CobaltStrings.Current = saved;
            Dispatcher.UIThread.RunJobs();
        }
    }

    [AvaloniaFact]
    public void 对等体报出这块屏幕给了几套布局()
    {
        var (_, picker) = Show();
        var peer = ControlAutomationPeer.CreatePeerForElement(picker);

        Assert.Equal("贴靠布局", peer.GetName());
        Assert.Equal($"{picker.Layouts.Count} 套贴靠布局", peer.GetItemStatus());
    }

    // ---- 键盘 ----------------------------------------------------------------

    [AvaloniaFact]
    public void 左右键在同一套布局里走()
    {
        // 悬停是纯指针手势，而工业面板上不一定有鼠标。
        var (_, picker) = Show();
        var cells = Cells(picker);
        cells[0].Focus();
        Dispatcher.UIThread.RunJobs();

        picker.RaiseEvent(new KeyEventArgs
        {
            RoutedEvent = InputElement.KeyDownEvent,
            Key = Key.Right,
        });
        Dispatcher.UIThread.RunJobs();

        Assert.True(cells[1].IsFocused, "右键没有把焦点移到同一套布局的下一格");
    }

    [AvaloniaFact]
    public void 上下键换布局()
    {
        var (_, picker) = Show();
        var cells = Cells(picker);
        var first = picker.Layouts[0].Zones.Count;

        cells[0].Focus();
        Dispatcher.UIThread.RunJobs();

        picker.RaiseEvent(new KeyEventArgs
        {
            RoutedEvent = InputElement.KeyDownEvent,
            Key = Key.Down,
        });
        Dispatcher.UIThread.RunJobs();

        Assert.True(cells[first].IsFocused, "下键没有换到下一套布局");
    }

    [AvaloniaFact]
    public void 带修饰键的方向键不接管也不吞掉()
    {
        // Ctrl+Left 是应用级快捷键。接管的话既挪走了焦点，又把快捷键吞了，
        // 两个后果都难查。
        var (_, picker) = Show();
        var cells = Cells(picker);
        cells[0].Focus();
        Dispatcher.UIThread.RunJobs();

        var e = new KeyEventArgs
        {
            RoutedEvent = InputElement.KeyDownEvent,
            Key = Key.Right,
            KeyModifiers = KeyModifiers.Control,
        };
        picker.RaiseEvent(e);
        Dispatcher.UIThread.RunJobs();

        Assert.False(e.Handled);
        Assert.True(cells[0].IsFocused);
    }

    [AvaloniaFact]
    public void 焦点不在格子上时方向键不动它()
    {
        // 面板可以是页面的一部分而不是弹出层。焦点还在别处时按方向键，
        // 面板不该抢焦点，也不该把按键吞掉。
        var picker = new SnapLayoutPicker();
        var elsewhere = new TextBox { Width = 120 };
        var window = new Window
        {
            Width = 800,
            Height = 600,
            Content = new StackPanel { Children = { elsewhere, picker } },
        };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        elsewhere.Focus();
        Dispatcher.UIThread.RunJobs();
        Assert.True(elsewhere.IsFocused, "前提不成立：焦点没落在面板之外");

        var e = new KeyEventArgs
        {
            RoutedEvent = InputElement.KeyDownEvent,
            Key = Key.Right,
        };
        picker.RaiseEvent(e);
        Dispatcher.UIThread.RunJobs();

        Assert.False(e.Handled);
        Assert.DoesNotContain(Cells(picker), c => c.IsFocused);
    }

    [AvaloniaFact]
    public void 放在弹出层里也找得到窗口()
    {
        // 这个面板天生就是放在弹出层里的，而弹出层的可视根是 PopupRoot 不是 Window。
        // 只看一层的话，放进 Flyout 的面板会得到一张空布局表——不报错，就是什么都不显示。
        var picker = new SnapLayoutPicker();
        var popup = new Popup { Child = picker };
        var host = new Panel { Children = { popup } };
        var window = new Window { Width = 800, Height = 600, Content = host };

        window.Show();
        Dispatcher.UIThread.RunJobs();
        popup.IsOpen = true;
        Dispatcher.UIThread.RunJobs();

        Assert.NotEmpty(picker.Layouts);
        Assert.Equal(WindowSnap.LayoutsFor(window).Count, picker.Layouts.Count);

        // 说清这里覆盖到的是哪一条路：无头平台没有窗口化的弹出层，Popup 走的是
        // 同窗口覆盖层，可视根就是 Window。移动端、浏览器、嵌入式 framebuffer
        // 也是这一条——正好是本库的目标平台，所以这条测试不是白测。
        //
        // 桌面上 Popup 是独立的窗口，可视根是 PopupRoot，走的是宿主链那一条。
        // 那条分支在这个环境里造不出来（拿不到 IPopupImpl），只能靠桌面上手测。
        Assert.IsType<Window>(picker.GetVisualRoot());
    }

    [AvaloniaFact]
    public void 弹出层里按下去摆的是宿主窗口()
    {
        var picker = new SnapLayoutPicker();
        var popup = new Popup { Child = picker };
        var window = new Window
        {
            Width = 800,
            Height = 600,
            Content = new Panel { Children = { popup } },
        };

        window.Show();
        Dispatcher.UIThread.RunJobs();
        popup.IsOpen = true;
        Dispatcher.UIThread.RunJobs();

        Cells(picker)[0].RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Assert.True(WindowSnap.IsSnapped(window));
    }

    [AvaloniaFact]
    public void 拿不到屏幕信息时面板是空的而不是崩()
    {
        // 单窗口平台（嵌入式 framebuffer、移动端、浏览器）上没有屏幕信息。
        // 贴靠是锦上添花的功能，没有就是没有，不该让应用崩在这里。
        var picker = new SnapLayoutPicker();

        Assert.Empty(picker.Layouts);

        var e = new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Right };
        picker.RaiseEvent(e);
        Assert.False(e.Handled);
    }
}
