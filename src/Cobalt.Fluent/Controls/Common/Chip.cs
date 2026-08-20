using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;

namespace Cobalt.Fluent.Controls;

/// <summary>
/// 可删除的筛选标签。高 24，胶囊形。
/// <see cref="IsClosable"/> 关掉就是纯展示标签（右侧内边距会补齐）。
/// </summary>
[PseudoClasses(":closable", ":accent")]
public class Chip : ContentControl
{
    private Button? _closeButton;

    public static readonly StyledProperty<bool> IsClosableProperty =
        AvaloniaProperty.Register<Chip, bool>(nameof(IsClosable), true);

    public bool IsClosable
    {
        get => GetValue(IsClosableProperty);
        set => SetValue(IsClosableProperty, value);
    }

    /// <summary>强调外观。用于「当前生效的筛选条件」。</summary>
    public static readonly StyledProperty<bool> IsAccentProperty =
        AvaloniaProperty.Register<Chip, bool>(nameof(IsAccent));

    public bool IsAccent
    {
        get => GetValue(IsAccentProperty);
        set => SetValue(IsAccentProperty, value);
    }

    public static readonly StyledProperty<ICommand?> CloseCommandProperty =
        AvaloniaProperty.Register<Chip, ICommand?>(nameof(CloseCommand));

    public ICommand? CloseCommand
    {
        get => GetValue(CloseCommandProperty);
        set => SetValue(CloseCommandProperty, value);
    }

    public static readonly RoutedEvent<RoutedEventArgs> ClosedEvent =
        RoutedEvent.Register<Chip, RoutedEventArgs>(nameof(Closed), RoutingStrategies.Bubble);

    public event EventHandler<RoutedEventArgs>? Closed
    {
        add => AddHandler(ClosedEvent, value);
        remove => RemoveHandler(ClosedEvent, value);
    }

    static Chip()
    {
        IsClosableProperty.Changed.AddClassHandler<Chip>((x, e) =>
            x.PseudoClasses.Set(":closable", e.NewValue is true));
        IsAccentProperty.Changed.AddClassHandler<Chip>((x, e) =>
            x.PseudoClasses.Set(":accent", e.NewValue is true));
    }

    public Chip()
    {
        PseudoClasses.Set(":closable", IsClosable);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        if (_closeButton is not null) _closeButton.Click -= OnCloseClicked;
        _closeButton = e.NameScope.Find<Button>("PART_Close");
        if (_closeButton is not null) _closeButton.Click += OnCloseClicked;
    }

    private void OnCloseClicked(object? sender, RoutedEventArgs e)
    {
        if (CloseCommand?.CanExecute(null) == true)
            CloseCommand.Execute(null);

        RaiseEvent(new RoutedEventArgs(ClosedEvent));
    }
}
