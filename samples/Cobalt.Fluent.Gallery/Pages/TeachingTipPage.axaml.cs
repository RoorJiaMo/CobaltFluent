using System.Windows.Input;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Cobalt.Fluent.Controls;

namespace Cobalt.Fluent.Gallery.Pages;

public partial class TeachingTipPage : UserControl
{
    private bool _actionTaken;

    public TeachingTipPage()
    {
        AvaloniaXamlLoader.Load(this);

        // 规格样张也是真控件，点「知道了」它就真关了——展柜里那两条不能被点没，
        // 立刻开回来。按钮的按压反馈照样看得到，只是关不掉。
        foreach (var name in new[] { "SpecTip", "CompareTip" })
        {
            var sample = this.FindControl<TeachingTip>(name)!;
            sample.Closed += (_, _) => sample.IsOpen = true;
        }

        var tip = this.FindControl<TeachingTip>("PlayTip")!;
        var rearm = this.FindControl<Button>("Rearm")!;
        var log = this.FindControl<TextBlock>("PlayLog")!;

        log.Text = "引导正开着。点掉它才能往下走——这是 TeachingTip 和 ToolTip 最实在的区别。";
        rearm.IsEnabled = false;

        // 动作按钮在模板里，外面拿不到它的 Click，只能走 ActionCommand。
        // 展柜没有 ViewModel，就地包一个。
        tip.ActionCommand = new DelegateCommand(() => _actionTaken = true);

        // 两个按钮都汇到 Closed，用 _actionTaken 分辨是哪一个：
        // 动作按钮的顺序是先跑 ActionCommand 再 Close()。
        tip.Closed += (_, _) =>
        {
            log.Text = _actionTaken
                ? "点了「查看示例」：跳到并行通道那一页，同时记为已读。动作和关闭是一次操作，不该让用户再点一下关。"
                : "点了「知道了」：一样记为已读。下次进来不再弹，除非版本更新带来新东西。";

            _actionTaken = false;
            rearm.IsEnabled = true;
        };

        rearm.Click += (_, _) =>
        {
            tip.IsOpen = true;
            rearm.IsEnabled = false;
            log.Text = "又弹了一次。真实产品里只有清掉「已读」标记才走得到这一步——每次启动都弹的引导，操作员第三天就开始无视了。";
        };
    }

    /// <summary>展柜里没有 ViewModel，ActionCommand 就地包一个。</summary>
    private sealed class DelegateCommand(Action execute) : ICommand
    {
        // CanExecute 恒真，没有需要重新求值的时机
        public event EventHandler? CanExecuteChanged { add { } remove { } }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) => execute();
    }
}
