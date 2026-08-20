using System.Windows.Input;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Cobalt.Fluent.Controls;

namespace Cobalt.Fluent.Gallery.Pages;

public partial class TabPage : UserControl
{
    // 页面里已经摆了 042 / 043，新开的接着往下编号，免得出现两张同名标签
    private int _nextRecipe = 44;

    public TabPage()
    {
        AvaloniaXamlLoader.Load(this);

        // 规格那一格的 TabView 也接上：展柜里没有死按钮，「+」画出来就得能按
        foreach (var name in new[] { "SpecTabs", "PlayTabs" })
            Wire(this.FindControl<TabView>(name)!);
    }

    private void Wire(TabView tabs)
    {
        // 控件只是「请求」关闭，真删由使用方做：正经场景这里要先弹保存确认，
        // 控件自己删掉的话，使用方就没有拦下来的机会了。
        tabs.TabCloseRequested += (_, e) => tabs.Items.Remove(e.Tab);

        tabs.AddCommand = new DelegateCommand(() =>
        {
            var tab = NewRecipeTab($"Recipe-{_nextRecipe++:D3}");
            tabs.Items.Add(tab);
            // 新开的立刻切过去 —— 浏览器就是这个行为，开完还停在原页会让人以为没开成
            tabs.SelectedItem = tab;
        });
    }

    private static TabViewItem NewRecipeTab(string name) => new()
    {
        Header = name,
        Icon = Symbol.Document,
        Content = new TextBlock
        {
            Classes = { "note" },
            Text = name + " —— 空白配方，尚未保存。",
        },
    };

    /// <summary>TabView 的「+」走 ICommand，展柜里没有 ViewModel，就地包一个。</summary>
    private sealed class DelegateCommand(Action execute) : ICommand
    {
        // CanExecute 恒真，没有需要重新求值的时机
        public event EventHandler? CanExecuteChanged { add { } remove { } }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) => execute();
    }
}
