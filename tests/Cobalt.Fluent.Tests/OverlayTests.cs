using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Cobalt.Fluent.Controls;
using Xunit;

namespace Cobalt.Fluent.Tests;

/// <summary>
/// 挂在 OverlayLayer 上的那几个：ContentDialog 和 Toast。
/// 它们不进页面布局，出问题的方式和普通控件不一样（挂不上去、关不掉、关了不还原），
/// 所以单独测一遍。
/// </summary>
public class OverlayTests
{
    private static (Window Window, Panel Root) Host()
    {
        var root = new Panel();
        var window = new Window { Width = 800, Height = 600, Content = root };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return (window, root);
    }

    [AvaloniaFact]
    public void ContentDialog_弹出后进_OverlayLayer_关掉后移除干净()
    {
        var (_, root) = Host();
        var layer = OverlayLayer.GetOverlayLayer(root);
        Assert.NotNull(layer);

        var before = layer!.Children.Count;

        var dialog = new ContentDialog
        {
            Title = "放弃未保存的更改？",
            PrimaryButtonText = "保存并关闭",
            CloseButtonText = "取消",
        };

        var pending = dialog.ShowAsync(root);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(before + 1, layer.Children.Count);
        Assert.False(pending.IsCompleted, "还没点按钮，不该已经有结果");

        dialog.Hide(ContentDialogResult.Primary);
        Dispatcher.UIThread.RunJobs();

        Assert.True(pending.IsCompleted);
        Assert.Equal(ContentDialogResult.Primary, pending.Result);

        // 关掉之后不能在 OverlayLayer 上留残骸 —— 留一层透明遮罩会把底下的点击全吃掉
        Assert.Equal(before, layer.Children.Count);
    }

    [AvaloniaFact]
    public void ContentDialog_Esc_等于按关闭按钮()
    {
        var (_, root) = Host();

        var dialog = new ContentDialog { Title = "确认", CloseButtonText = "取消" };
        var pending = dialog.ShowAsync(root);
        Dispatcher.UIThread.RunJobs();

        var layer = OverlayLayer.GetOverlayLayer(root)!;
        var host = layer.Children.OfType<Panel>().Last();

        host.RaiseEvent(new KeyEventArgs
        {
            RoutedEvent = InputElement.KeyDownEvent,
            Key = Key.Escape,
        });
        Dispatcher.UIThread.RunJobs();

        Assert.True(pending.IsCompleted);
        Assert.Equal(ContentDialogResult.None, pending.Result);
    }

    [AvaloniaFact]
    public void ContentDialog_重复调用_ShowAsync_返回同一个等待()
    {
        var (_, root) = Host();
        var dialog = new ContentDialog { Title = "确认" };

        var first = dialog.ShowAsync(root);
        var second = dialog.ShowAsync(root);
        Dispatcher.UIThread.RunJobs();

        // 不能弹出两份，否则关掉一份之后另一份的遮罩会留在屏幕上
        Assert.Same(first, second);

        var layer = OverlayLayer.GetOverlayLayer(root)!;
        Assert.Single(layer.Children.OfType<Panel>().Where(p => p.Children.Contains(dialog)));

        dialog.Hide();
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public void Toast_挂到_OverlayLayer_并堆叠()
    {
        var (_, root) = Host();

        ToastHost.Show(root, new Toast { Title = "配方已保存" }, TimeSpan.FromMinutes(5));
        ToastHost.Show(root, new Toast { Title = "已导出 CSV" }, TimeSpan.FromMinutes(5));
        Dispatcher.UIThread.RunJobs();

        var layer = OverlayLayer.GetOverlayLayer(root)!;
        var host = layer.Children.OfType<ToastHost>().SingleOrDefault();

        Assert.True(host is not null, "两条 Toast 应该复用同一个 ToastHost，不是各挂一个");
        Assert.Equal(2, host!.Items.Count);
    }

    [AvaloniaFact]
    public void InfoBar_关掉之后不占布局()
    {
        var bar = new InfoBar { Title = "已连接到设备" };
        Assert.True(bar.IsVisible);

        bar.Close();

        // IsVisible=false 而不是只隐藏内容：InfoBar 关掉后不该在页面上留一条空白
        Assert.False(bar.IsVisible);
        Assert.False(bar.IsOpen);
    }
}
