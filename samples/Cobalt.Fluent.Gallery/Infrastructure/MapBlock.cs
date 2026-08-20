using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Metadata;

namespace Cobalt.Fluent.Gallery.Infrastructure;

/// <summary>API 速查块里的一行：左边键名，右边值。</summary>
public sealed class MapRow : AvaloniaObject
{
    public static readonly StyledProperty<string> KeyProperty =
        AvaloniaProperty.Register<MapRow, string>(nameof(Key), "");

    public string Key { get => GetValue(KeyProperty); set => SetValue(KeyProperty, value); }

    public static readonly StyledProperty<string> ValueProperty =
        AvaloniaProperty.Register<MapRow, string>(nameof(Value), "");

    public string Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
}

/// <summary>
/// 每一节结尾的 API 速查块。
/// 前六组给的是资源键和选择器；第 7 组是本库新写的控件，给的是属性面和伪类清单。
/// </summary>
public sealed class MapBlock : Decorator
{
    public MapBlock()
    {
        Rows = [];
    }

    public static readonly DirectProperty<MapBlock, AvaloniaList<MapRow>> RowsProperty =
        AvaloniaProperty.RegisterDirect<MapBlock, AvaloniaList<MapRow>>(
            nameof(Rows), o => o.Rows, (o, v) => o.Rows = v);

    private AvaloniaList<MapRow> _rows = [];

    [Content]
    public AvaloniaList<MapRow> Rows
    {
        get => _rows;
        set => SetAndRaise(RowsProperty, ref _rows, value);
    }

    public static readonly StyledProperty<string?> NoteProperty =
        AvaloniaProperty.Register<MapBlock, string?>(nameof(Note));

    /// <summary>块尾那段小字：踩过的坑、为什么这么定。</summary>
    public string? Note { get => GetValue(NoteProperty); set => SetValue(NoteProperty, value); }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), RowSpacing = 2 };

        for (var i = 0; i < Rows.Count; i++)
        {
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

            var key = new TextBlock
            {
                Text = Rows[i].Key,
                Classes = { "k" },
                Margin = new Thickness(0, 0, 16, 0),
                VerticalAlignment = VerticalAlignment.Top,
            };
            Grid.SetRow(key, i);
            grid.Children.Add(key);

            var val = new TextBlock { Text = Rows[i].Value, Classes = { "v" } };
            Grid.SetRow(val, i);
            Grid.SetColumn(val, 1);
            grid.Children.Add(val);
        }

        var stack = new StackPanel { Spacing = 0 };
        stack.Children.Add(grid);

        if (!string.IsNullOrWhiteSpace(Note))
        {
            stack.Children.Add(new TextBlock
            {
                Text = Note,
                Classes = { "k" },
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 10, 0, 0),
            });
        }

        Child = new Border { Classes = { "map" }, Child = stack };
    }
}
