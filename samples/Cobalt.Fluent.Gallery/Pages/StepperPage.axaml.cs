using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Cobalt.Fluent.Controls;

namespace Cobalt.Fluent.Gallery.Pages;

public partial class StepperPage : UserControl
{
    public StepperPage()
    {
        AvaloniaXamlLoader.Load(this);

        WireFlow();
        WireChips();
    }

    private void WireFlow()
    {
        var flow = this.FindControl<Stepper>("Flow")!;
        var verify = this.FindControl<StepperItem>("StepVerify")!;
        var stepCount = flow.Items.Count;

        // CurrentIndex 是唯一的输入，各步状态由 Stepper 自己推。
        // 页面这边不碰 StepperItem.State——两处都能改状态就一定会对不上。
        this.FindControl<Button>("Prev")!.Click += (_, _) =>
            flow.CurrentIndex = Math.Max(0, flow.CurrentIndex - 1);

        this.FindControl<Button>("Next")!.Click += (_, _) =>
        {
            if (flow.CurrentIndex < stepCount - 1)
            {
                flow.CurrentIndex++;
                return;
            }

            // 已经在最后一步：这一下才是「完成」
            Notify(new Toast
            {
                Severity = InfoBarSeverity.Success,
                Title = "配方已下发",
                Message = "Recipe-042 · 5 步全部通过",
            });
        };

        this.FindControl<Button>("Fail")!.Click += (_, _) =>
        {
            verify.IsError = !verify.IsError;

            // IsError 不在 CurrentIndex 那条通知链上，改完得自己推一次
            flow.RefreshStates();

            if (!verify.IsError) return;

            var retry = new Button { Content = "重试" };
            var toast = new Toast
            {
                Severity = InfoBarSeverity.Error,
                Title = "参数校验未通过",
                Message = "腔体温度上限超出配方允许范围",
                ActionContent = retry,
            };

            // 带操作按钮的 Toast 停留 8 秒，够读完再决定点不点
            retry.Click += (_, _) =>
            {
                verify.IsError = false;
                flow.RefreshStates();
            };

            Notify(toast);
        };

        this.FindControl<Button>("Done")!.Click += (_, _) => Notify(new Toast
        {
            Severity = InfoBarSeverity.Success,
            Title = "配方已保存",
            Message = "Recipe-042 · 3 处参数变更",
        });
    }

    private void WireChips()
    {
        var row = this.FindControl<StackPanel>("ChipRow")!;
        var original = row.Children.OfType<Chip>().ToArray();

        foreach (var chip in original) chip.Closed += (_, _) => Remove(chip);

        void Remove(Chip chip)
        {
            row.Children.Remove(chip);

            // 删空了整行收掉，别留一条空筛选栏占着位置
            row.IsVisible = row.Children.Count > 0;
        }

        this.FindControl<Button>("ResetChips")!.Click += (_, _) =>
        {
            row.Children.Clear();
            foreach (var chip in original) row.Children.Add(chip);
            row.IsVisible = true;
        };
    }

    /// <summary>Toast 挂在窗口的 OverlayLayer 上，所以 owner 传页面自己就行。</summary>
    private void Notify(Toast toast) => ToastHost.Show(this, toast);
}
