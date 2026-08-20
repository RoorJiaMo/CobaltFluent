using System.Globalization;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Markup.Xaml;

namespace Cobalt.Fluent.Gallery.Pages;

public partial class FlyoutPage : UserControl
{
    private const int FullLog = 1248;

    private int _records = FullLog;

    public FlyoutPage()
    {
        AvaloniaXamlLoader.Load(this);

        var trigger = this.FindControl<Button>("ClearTrigger")!;
        var refill = this.FindControl<Button>("Refill")!;
        var log = this.FindControl<TextBlock>("PlayLog")!;

        // 弹层内容挂在 Flyout 上，页面的视觉树里没有它，FindControl 未必摸得到。
        // 从 Flyout.Content 往下按 Name 取，弹层开没开都成立。
        var body = (Control)((Flyout)trigger.Flyout!).Content!;
        var detail = Named<TextBlock>(body, "ConfirmDetail");
        var cancel = Named<Button>(body, "CancelClear");
        var confirm = Named<Button>(body, "ConfirmClear");

        void RefreshDetail() =>
            detail.Text = $"此操作不可撤销，将删除 {Grouped(_records)} 条记录。";

        RefreshDetail();
        log.Text = "点「清空日志」弹确认层。";

        // 两个按钮都得自己 Hide()：Flyout 不管里面被点了什么，
        // 只有点到弹层外面才算轻关闭。
        cancel.Click += (_, _) =>
        {
            trigger.Flyout!.Hide();
            log.Text = $"取消了，{Grouped(_records)} 条记录还在。";
        };

        confirm.Click += (_, _) =>
        {
            var removed = _records;
            _records = 0;
            RefreshDetail();
            trigger.Flyout!.Hide();

            // 清完关掉触发键：弹层照样能弹，但里面只剩「将删除 0 条记录」，
            // 一句废话还要人确认一次。
            trigger.IsEnabled = false;
            refill.IsEnabled = true;
            log.Text = $"已清空 {Grouped(removed)} 条记录。";
        };

        refill.Click += (_, _) =>
        {
            _records = FullLog;
            RefreshDetail();
            trigger.IsEnabled = true;
            refill.IsEnabled = false;
            log.Text = $"日志回到 {Grouped(FullLog)} 条，可以再来一遍。";
        };
    }

    /// <summary>千位用空格分组 —— 数字宽了才好读，逗号在中文界面里容易和顿号混。</summary>
    private static string Grouped(int value) =>
        value.ToString("N0", CultureInfo.InvariantCulture).Replace(',', ' ');

    private static T Named<T>(Control root, string name) where T : Control =>
        root.GetLogicalDescendants().OfType<T>().First(c => c.Name == name);
}
