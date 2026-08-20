using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;

namespace Cobalt.Fluent.Gallery.Pages;

public partial class EmptyStatePage : UserControl
{
    // 1.1 s：短到不用真的等，长到能看清骨架的形状确实预示了后面那张表。
    // 再短就变成一闪而过的噪声，那还不如不显示。
    private readonly DispatcherTimer _load = new() { Interval = TimeSpan.FromMilliseconds(1100) };

    public EmptyStatePage()
    {
        AvaloniaXamlLoader.Load(this);

        var loading = this.FindControl<Control>("Loading")!;
        var rows = this.FindControl<Control>("Rows")!;
        var empty = this.FindControl<Control>("Empty")!;
        var filtered = this.FindControl<CheckBox>("Filtered")!;

        // 三档互斥。留一个统一入口，免得出现「骨架还在，数据已经压上来」这种叠影。
        void Show(Control only)
        {
            loading.IsVisible = ReferenceEquals(only, loading);
            rows.IsVisible = ReferenceEquals(only, rows);
            empty.IsVisible = ReferenceEquals(only, empty);
        }

        void Reload()
        {
            // 加载必须先走骨架：直接从旧数据跳到空状态，操作员分不清是查完了没有，
            // 还是根本没查。
            Show(loading);
            _load.Stop();
            _load.Start();
        }

        _load.Tick += (_, _) =>
        {
            _load.Stop();
            Show(filtered.IsChecked == true ? empty : rows);
        };

        this.FindControl<Button>("Reload")!.Click += (_, _) => Reload();
        filtered.IsCheckedChanged += (_, _) => Reload();

        // 空状态里的按钮就是那条出路，点了得真把筛选清掉。
        // 清完由 IsCheckedChanged 顺带触发重新加载，不在这里再喊一次。
        this.FindControl<Button>("ClearFilter")!.Click += (_, _) => filtered.IsChecked = false;

        // 页面切走后计时器还在跳会白烧一个 Dispatcher 回调。
        DetachedFromVisualTree += (_, _) => _load.Stop();

        Reload();
    }
}
