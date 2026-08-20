using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Cobalt.Fluent.Gallery.Pages;

public partial class CheckBoxPage : UserControl
{
    private bool _syncing;

    public CheckBoxPage()
    {
        AvaloniaXamlLoader.Load(this);

        var parent = this.FindControl<CheckBox>("Parent")!;
        var children = new[] { "Ch1", "Ch2", "Ch3" }
            .Select(n => this.FindControl<CheckBox>(n)!)
            .ToArray();

        // 子 → 父：部分选中变 indeterminate，全选变 checked
        void SyncParent()
        {
            if (_syncing) return;
            _syncing = true;

            var checkedCount = children.Count(c => c.IsChecked == true);
            parent.IsChecked = checkedCount switch
            {
                0 => false,
                var n when n == children.Length => true,
                _ => null,
            };

            _syncing = false;
        }

        foreach (var child in children)
            child.IsCheckedChanged += (_, _) => SyncParent();

        // 父 → 子：indeterminate 时点一下按「全选」处理
        parent.IsCheckedChanged += (_, _) =>
        {
            if (_syncing) return;
            if (parent.IsChecked is not { } value) return;

            _syncing = true;
            foreach (var child in children) child.IsChecked = value;
            _syncing = false;
        };

        SyncParent();
    }
}
