using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;

namespace Cobalt.Fluent.Controls;

/// <summary>步骤状态。</summary>
public enum StepState
{
    /// <summary>还没走到。</summary>
    Pending,

    /// <summary>已完成，显示对勾。</summary>
    Done,

    /// <summary>当前步。</summary>
    Current,

    /// <summary>这一步失败了。</summary>
    Error,
}

/// <summary>
/// 多步流程指示（配方向导、标定流程）。
/// <see cref="CurrentIndex"/> 一改，各步的状态自动推出来：之前的 Done、当前 Current、之后 Pending。
/// 某一步失败就把它的 <see cref="StepperItem.IsError"/> 打开。
/// </summary>
public class Stepper : ItemsControl
{
    public static readonly StyledProperty<int> CurrentIndexProperty =
        AvaloniaProperty.Register<Stepper, int>(nameof(CurrentIndex));

    public int CurrentIndex
    {
        get => GetValue(CurrentIndexProperty);
        set => SetValue(CurrentIndexProperty, value);
    }

    static Stepper()
    {
        CurrentIndexProperty.Changed.AddClassHandler<Stepper>((x, _) => x.RefreshStates());
    }

    protected override Control CreateContainerForItemOverride(
        object? item, int index, object? recycleKey) => new StepperItem();

    protected override bool NeedsContainerOverride(
        object? item, int index, out object? recycleKey)
    {
        recycleKey = null;
        return item is not StepperItem;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        RefreshStates();
    }

    /// <summary>按 <see cref="CurrentIndex"/> 推出每一步的状态。失败态由各步自己声明，优先级最高。</summary>
    public void RefreshStates()
    {
        var items = Items.OfType<StepperItem>().ToList();
        for (var i = 0; i < items.Count; i++)
        {
            items[i].State = items[i].IsError
                ? StepState.Error
                : i < CurrentIndex ? StepState.Done
                : i == CurrentIndex ? StepState.Current
                : StepState.Pending;

            items[i].StepNumber = i + 1;
            items[i].IsLast = i == items.Count - 1;
        }
    }
}

/// <summary>Stepper 里的一步。</summary>
[PseudoClasses(":pending", ":done", ":current", ":error", ":last")]
public class StepperItem : ContentControl
{
    public static readonly StyledProperty<StepState> StateProperty =
        AvaloniaProperty.Register<StepperItem, StepState>(nameof(State), StepState.Pending);

    public StepState State
    {
        get => GetValue(StateProperty);
        set => SetValue(StateProperty, value);
    }

    public static readonly StyledProperty<bool> IsErrorProperty =
        AvaloniaProperty.Register<StepperItem, bool>(nameof(IsError));

    /// <summary>这一步失败了。优先于 Stepper 推出来的状态。</summary>
    public bool IsError
    {
        get => GetValue(IsErrorProperty);
        set => SetValue(IsErrorProperty, value);
    }

    public static readonly StyledProperty<int> StepNumberProperty =
        AvaloniaProperty.Register<StepperItem, int>(nameof(StepNumber), 1);

    public int StepNumber
    {
        get => GetValue(StepNumberProperty);
        set => SetValue(StepNumberProperty, value);
    }

    public static readonly StyledProperty<bool> IsLastProperty =
        AvaloniaProperty.Register<StepperItem, bool>(nameof(IsLast));

    /// <summary>最后一步不画后面的连接线。</summary>
    public bool IsLast
    {
        get => GetValue(IsLastProperty);
        set => SetValue(IsLastProperty, value);
    }

    static StepperItem()
    {
        StateProperty.Changed.AddClassHandler<StepperItem>((x, _) => x.Refresh());
        IsLastProperty.Changed.AddClassHandler<StepperItem>(
            (x, e) => x.PseudoClasses.Set(":last", e.NewValue is true));
    }

    public StepperItem() => Refresh();

    private void Refresh()
    {
        var s = State;
        PseudoClasses.Set(":pending", s == StepState.Pending);
        PseudoClasses.Set(":done", s == StepState.Done);
        PseudoClasses.Set(":current", s == StepState.Current);
        PseudoClasses.Set(":error", s == StepState.Error);
    }
}
