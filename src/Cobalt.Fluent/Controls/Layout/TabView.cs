using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Cobalt.Fluent.Controls;

/// <summary>
/// 浏览器式标签页：每个标签自带关闭按钮，选中的那个「浮」到内容区上。
///
/// 和 <see cref="TabControl"/> 的区别是语义：TabControl 的标签是**固定的视图切换**，
/// TabView 的标签是**用户开出来的文档**，数量不定、可关闭、可能很多。
/// </summary>
public class TabView : TabControl
{
    protected override Type StyleKeyOverride => typeof(TabView);

    public static readonly StyledProperty<bool> IsAddButtonVisibleProperty =
        AvaloniaProperty.Register<TabView, bool>(nameof(IsAddButtonVisible), true);

    public bool IsAddButtonVisible
    {
        get => GetValue(IsAddButtonVisibleProperty);
        set => SetValue(IsAddButtonVisibleProperty, value);
    }

    public static readonly StyledProperty<ICommand?> AddCommandProperty =
        AvaloniaProperty.Register<TabView, ICommand?>(nameof(AddCommand));

    public ICommand? AddCommand
    {
        get => GetValue(AddCommandProperty);
        set => SetValue(AddCommandProperty, value);
    }

    /// <summary>允许在标签栏内拖拽重排。</summary>
    public static readonly StyledProperty<bool> IsReorderEnabledProperty =
        AvaloniaProperty.Register<TabView, bool>(nameof(IsReorderEnabled), true);

    public bool IsReorderEnabled
    {
        get => GetValue(IsReorderEnabledProperty);
        set => SetValue(IsReorderEnabledProperty, value);
    }

    /// <summary>允许把标签拖出标签栏，变成独立窗口。</summary>
    public static readonly StyledProperty<bool> IsTearOutEnabledProperty =
        AvaloniaProperty.Register<TabView, bool>(nameof(IsTearOutEnabled), true);

    public bool IsTearOutEnabled
    {
        get => GetValue(IsTearOutEnabledProperty);
        set => SetValue(IsTearOutEnabledProperty, value);
    }

    /// <summary>
    /// 这台机器上撕出到底可不可行。
    ///
    /// <b>撕出是桌面专有能力。</b><c>Avalonia.LinuxFramebuffer</c>（DRM/KMS 直出，
    /// 嵌入式面板走的那条路）、移动端、浏览器都是单窗口的——它们的生命周期是
    /// <see cref="ISingleViewApplicationLifetime"/>，只有一个 MainView，没有窗口列表。
    ///
    /// 判据取生命周期而不是猜操作系统：这是 Avalonia 自己对「有没有多窗口」的表述，
    /// 而且同一个 OS 上两种都可能（同一份代码既能跑桌面又能跑 framebuffer）。
    ///
    /// 这个属性存在的理由是**不让撕出静默失败**：能力不具备时不该出那个视觉暗示，
    /// 更不该让操作员拖了半天发现没反应。
    /// </summary>
    public bool CanTearOut =>
        IsTearOutEnabled
        && Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime;

    public static readonly RoutedEvent<TabCloseRequestedEventArgs> TabCloseRequestedEvent =
        RoutedEvent.Register<TabView, TabCloseRequestedEventArgs>(
            nameof(TabCloseRequested), RoutingStrategies.Bubble);

    /// <summary>某个标签请求关闭。是否真的移除由使用方决定（可能要先提示保存）。</summary>
    public event EventHandler<TabCloseRequestedEventArgs>? TabCloseRequested
    {
        add => AddHandler(TabCloseRequestedEvent, value);
        remove => RemoveHandler(TabCloseRequestedEvent, value);
    }

    protected override Control CreateContainerForItemOverride(
        object? item, int index, object? recycleKey) => new TabViewItem();

    protected override bool NeedsContainerOverride(
        object? item, int index, out object? recycleKey)
    {
        recycleKey = null;
        return item is not TabViewItem;
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        _strip = e.NameScope.Find<Panel>("PART_Strip");
        _dropIndicator = e.NameScope.Find<Border>("PART_DropIndicator");
        if (_dropIndicator is not null) _dropIndicator.IsVisible = false;

        if (e.NameScope.Find<Button>("PART_AddButton") is { } add)
        {
            add.Click += (_, _) => RequestAdd();
        }
    }

    public static readonly RoutedEvent<RoutedEventArgs> TabAddRequestedEvent =
        RoutedEvent.Register<TabView, RoutedEventArgs>(
            nameof(TabAddRequested), RoutingStrategies.Bubble);

    /// <summary>
    /// 按了「+」。使用方在这里建自己的标签，建完把 <c>Handled</c> 置真。
    ///
    /// 三条路依次尝试：本事件 → <see cref="AddCommand"/> → 内置的空标签兜底。
    /// 有兜底是因为**「+」画出来了就不能是个死按钮**——按了没反应比按钮不存在更糟，
    /// 操作员会以为是卡住了而反复按。
    /// </summary>
    public event EventHandler<RoutedEventArgs>? TabAddRequested
    {
        add => AddHandler(TabAddRequestedEvent, value);
        remove => RemoveHandler(TabAddRequestedEvent, value);
    }

    public static readonly RoutedEvent<TabViewTearOutEventArgs> TabTearOutRequestedEvent =
        RoutedEvent.Register<TabView, TabViewTearOutEventArgs>(
            nameof(TabTearOutRequested), RoutingStrategies.Bubble);

    /// <summary>
    /// 某个标签被拖出了标签栏，需要一个承载它的窗口。
    ///
    /// 使用方把 <see cref="TabViewTearOutEventArgs.Window"/> 设成自己的窗口类型，
    /// 不设就用一个内置的最小窗口。
    /// </summary>
    public event EventHandler<TabViewTearOutEventArgs>? TabTearOutRequested
    {
        add => AddHandler(TabTearOutRequestedEvent, value);
        remove => RemoveHandler(TabTearOutRequestedEvent, value);
    }

    public static readonly RoutedEvent<TabViewTabMovedEventArgs> TabMovedEvent =
        RoutedEvent.Register<TabView, TabViewTabMovedEventArgs>(
            nameof(TabMoved), RoutingStrategies.Bubble);

    /// <summary>一个标签从别的 TabView 搬了进来，或在本条标签栏内换了位置。</summary>
    public event EventHandler<TabViewTabMovedEventArgs>? TabMoved
    {
        add => AddHandler(TabMovedEvent, value);
        remove => RemoveHandler(TabMovedEvent, value);
    }

    internal void RequestClose(TabViewItem item) =>
        RaiseEvent(new TabCloseRequestedEventArgs(TabCloseRequestedEvent, item));

    private void RequestAdd()
    {
        var args = new RoutedEventArgs(TabAddRequestedEvent);
        RaiseEvent(args);
        if (args.Handled) return;

        if (AddCommand?.CanExecute(null) == true)
        {
            AddCommand.Execute(null);
            return;
        }

        // 兜底。绑了 ItemsSource 时不能自己往里插——那是宿主的数据，
        // 而这种情况下按钮没人接就确实什么也做不了，见 TabAddRequested 的说明。
        if (!CanMutateItems) return;

        var tab = new TabViewItem { Header = CobaltStrings.Current.NewTab };
        Items.Add(tab);
        SelectedItem = tab;
    }

    // ---- 5. 键盘重排 ---------------------------------------------------------

    /// <summary>
    /// 把一个标签左右挪一格。<paramref name="delta"/> 为负是往左。
    ///
    /// 返回是否真的挪了——挪不动（已在头/尾、重排关着、集合动不得）时返回 false，
    /// 调用方据此决定要不要把按键标成已处理。挪不动却吞掉按键的话，
    /// 焦点就卡在这儿了。
    /// </summary>
    internal bool MoveTab(TabViewItem tab, int delta)
    {
        if (!IsReorderEnabled || !CanMutateItems) return false;

        var from = IndexFromContainer(tab);
        if (from < 0) return false;

        var to = from + delta;
        if (to < 0 || to >= Items.Count) return false;

        Items.RemoveAt(from);
        Items.Insert(to, tab);
        SelectedItem = tab;

        RaiseEvent(new TabViewTabMovedEventArgs(TabMovedEvent, tab, this, this, to));
        return true;
    }

    // ---- 拖拽落点需要的量测。全部换算到屏幕坐标 ------------------------------

    private Panel? _strip;
    private Border? _dropIndicator;

    /// <summary>标签栏在屏幕上的矩形。拿不到部件（模板还没套上）时返回空矩形。</summary>
    internal PixelRect StripBoundsOnScreen()
    {
        if (_strip is not { IsVisible: true } strip || strip.Bounds.Width <= 0)
            return default;

        var topLeft = strip.PointToScreen(new Point(0, 0));
        var bottomRight = strip.PointToScreen(new Point(strip.Bounds.Width, strip.Bounds.Height));
        return new PixelRect(topLeft, bottomRight);
    }

    /// <summary>每个标签在屏幕上的矩形，按显示顺序。</summary>
    internal IReadOnlyList<PixelRect> TabBoundsOnScreen()
    {
        var result = new List<PixelRect>();
        for (var i = 0; i < ItemCount; i++)
        {
            if (ContainerFromIndex(i) is not Control c || !c.IsVisible || c.Bounds.Width <= 0)
                continue;

            var topLeft = c.PointToScreen(new Point(0, 0));
            var bottomRight = c.PointToScreen(new Point(c.Bounds.Width, c.Bounds.Height));
            result.Add(new PixelRect(topLeft, bottomRight));
        }

        return result;
    }

    internal void ShowDropIndicator(int insertAt)
    {
        if (_dropIndicator is null || _strip is null) return;

        var tabs = TabBoundsOnScreen();
        if (tabs.Count == 0)
        {
            _dropIndicator.IsVisible = false;
            return;
        }

        // 插到第 n 个 = 画在第 n 个标签的左边；插到末尾 = 画在最后一个的右边。
        var edge = insertAt < tabs.Count
            ? tabs[insertAt].X
            : tabs[^1].X + tabs[^1].Width;

        var local = _strip.PointToClient(new PixelPoint(edge, _strip.PointToScreen(new Point(0, 0)).Y));

        _dropIndicator.Margin = new Thickness(local.X - 1, 0, 0, 0);
        _dropIndicator.IsVisible = true;
    }

    internal void HideDropIndicator()
    {
        if (_dropIndicator is not null) _dropIndicator.IsVisible = false;
    }

    // ---- 搬家与撕出 ----------------------------------------------------------

    /// <summary>
    /// 控件能不能自己改这个集合。
    ///
    /// <b>绑了 <see cref="ItemsControl.ItemsSource"/> 就不能。</b>那是宿主的数据，
    /// 控件擅自往里插删会和宿主自己的增删打架，而且 ItemsSource 可能是只读视图。
    /// 这种情况下搬家必须由宿主处理事件来完成——事件没人处理就**拒绝搬**，
    /// 而不是搬一半留下一个既不在这边也不在那边的标签。
    /// </summary>
    private bool CanMutateItems => ItemsSource is null;

    internal void AcceptTab(TabView from, TabViewItem tab, int insertAt)
    {
        if (ReferenceEquals(from, this) && !IsReorderEnabled) return;
        if (!ReferenceEquals(from, this) && !IsTearOutEnabled) return;

        var args = new TabViewTabMovedEventArgs(TabMovedEvent, tab, from, this, insertAt);
        RaiseEvent(args);
        if (args.Handled) return;

        if (!from.CanMutateItems || !CanMutateItems) return;

        MoveInto(from, tab, this, insertAt);
    }

    /// <summary>
    /// 把标签从一个 TabView 搬到另一个。
    ///
    /// <b>跨窗口时必须把摘除和插入分成两个调度轮次。</b>两个视觉根都活着的时候，
    /// 同一轮里先摘后插会抛
    /// <c>ArgumentException: Attempt to call InvalidateArrange on wrong LayoutManager</c>——
    /// 摘除产生的布局失效还排在源窗口的队列里，控件却已经挂到了目标窗口的布局管理器上。
    ///
    /// 这和标签有没有内容无关，空标签一样抛；先把选中项挪开也没用。
    /// 唯一有效的是让摘除先走完。（撕出那条路径碰不到：新窗口在插入时还没 Show，
    /// 压根没有布局管理器。）
    /// </summary>
    private static void MoveInto(TabView from, TabViewItem tab, TabView to, int insertAt)
    {
        var sameRoot = ReferenceEquals(from.GetVisualRoot(), to.GetVisualRoot());

        from.Items.Remove(tab);

        if (sameRoot)
        {
            Insert();
            return;
        }

        // 这一轮到下一轮之间，标签哪个集合里都不在。所以插入这一步不能失败：
        // 目标要是同期没了（窗口被关掉），把标签放回源里，
        // 而不是让它消失——那是操作员的文档。
        Dispatcher.UIThread.Post(() =>
        {
            if (to.GetVisualRoot() is null && from.GetVisualRoot() is not null)
            {
                from.Items.Insert(Math.Clamp(insertAt, 0, from.Items.Count), tab);
                from.SelectedItem = tab;
                return;
            }

            Insert();
        }, DispatcherPriority.Loaded);

        void Insert()
        {
            to.Items.Insert(Math.Clamp(insertAt, 0, to.Items.Count), tab);
            to.SelectedItem = tab;
        }
    }

    internal void TearOut(TabViewItem tab, PixelPoint at)
    {
        if (!CanTearOut) return;

        var args = new TabViewTearOutEventArgs(TabTearOutRequestedEvent, tab, at);
        RaiseEvent(args);

        if (args.Handled) return;
        if (!CanMutateItems) return;

        var host = args.Window ?? CreateTearOutWindow();
        if (host.Content is not TabView view)
        {
            view = new TabView();
            host.Content = view;
        }

        // 先摆位置再 Show：Show 之后再挪，操作员会看见窗口在屏幕上跳一下。
        host.Position = at;

        // 宿主给的窗口可能已经是活的（比如把标签扔进一个已经开着的工具窗），
        // 那就走和并回同一条路——跨活窗口搬必须分两轮。
        if (host.IsVisible)
        {
            MoveInto(this, tab, view, view.Items.Count);
            host.Activate();
            return;
        }

        Items.Remove(tab);
        view.Items.Add(tab);
        view.SelectedItem = tab;

        host.Show();
    }

    /// <summary>使用方不提供窗口时的兜底。撕出来的窗口本身也是 TabView，所以还能往里拖。</summary>
    private Window CreateTearOutWindow() => new()
    {
        Width = Math.Max(480, Bounds.Width),
        Height = Math.Max(320, Bounds.Height),
        Content = new TabView(),
    };
}

public sealed class TabViewTearOutEventArgs(
    RoutedEvent routedEvent, TabViewItem tab, PixelPoint screenPosition)
    : RoutedEventArgs(routedEvent)
{
    public TabViewItem Tab { get; } = tab;

    /// <summary>松手时光标所在的屏幕位置。窗口应当摆在这里。</summary>
    public PixelPoint ScreenPosition { get; } = screenPosition;

    /// <summary>承载这个标签的窗口。留空则用内置的最小窗口。</summary>
    public Window? Window { get; set; }
}

public sealed class TabViewTabMovedEventArgs(
    RoutedEvent routedEvent, TabViewItem tab, TabView from, TabView to, int index)
    : RoutedEventArgs(routedEvent)
{
    public TabViewItem Tab { get; } = tab;

    public TabView From { get; } = from;

    public TabView To { get; } = to;

    /// <summary>要插到的位置。同一条标签栏内重排时，这个下标已经扣掉了原项占的那一格。</summary>
    public int Index { get; } = index;
}

public sealed class TabCloseRequestedEventArgs(RoutedEvent routedEvent, TabViewItem tab)
    : RoutedEventArgs(routedEvent)
{
    public TabViewItem Tab { get; } = tab;
}

/// <summary>TabView 里的一个标签。</summary>
[PseudoClasses(":closable")]
public class TabViewItem : TabItem
{
    private Button? _closeButton;

    protected override Type StyleKeyOverride => typeof(TabViewItem);

    public static readonly StyledProperty<bool> IsClosableProperty =
        AvaloniaProperty.Register<TabViewItem, bool>(nameof(IsClosable), true);

    public bool IsClosable
    {
        get => GetValue(IsClosableProperty);
        set => SetValue(IsClosableProperty, value);
    }

    public static readonly StyledProperty<Symbol> IconProperty =
        AvaloniaProperty.Register<TabViewItem, Symbol>(nameof(Icon));

    public Symbol Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    static TabViewItem()
    {
        IsClosableProperty.Changed.AddClassHandler<TabViewItem>(
            (x, e) => x.PseudoClasses.Set(":closable", e.NewValue is true));
    }

    public TabViewItem() => PseudoClasses.Set(":closable", true);

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        if (_closeButton is not null) _closeButton.Click -= OnCloseClicked;
        _closeButton = e.NameScope.Find<Button>("PART_Close");
        if (_closeButton is not null) _closeButton.Click += OnCloseClicked;
    }

    private void OnCloseClicked(object? sender, RoutedEventArgs e) =>
        Owner?.RequestClose(this);

    private TabView? Owner => this.GetLogicalAncestors().OfType<TabView>().FirstOrDefault();

    // ---- 拖拽 ---------------------------------------------------------------
    //
    // 按下先只记坐标，超过阈值才真的开拖：按下即开拖会让「点一下选中标签」
    // 变得很难点——手指或鼠标在按下瞬间总会漂一两个像素。

    private PixelPoint? _pressedAt;

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        if (Owner is not { } owner) return;
        if (!owner.IsReorderEnabled && !owner.IsTearOutEnabled) return;

        _pressedAt = this.PointToScreen(e.GetPosition(this));
        e.Pointer.Capture(this);
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);

        if (Owner is not { } owner) return;

        var now = this.PointToScreen(e.GetPosition(this));

        if (TabViewDrag.Active is { } drag)
        {
            drag.Update(now);
            return;
        }

        if (_pressedAt is not { } start) return;
        if (!TabViewDrag.PastThreshold(start, now)) return;

        TabViewDrag.Begin(owner, this, start).Update(now);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);

        _pressedAt = null;

        // **先落地再释放捕获。** 释放捕获会触发 OnPointerCaptureLost，而那里把会话
        // 当成中断取消掉——顺序反过来的话，Complete 拿到的是一个已经被取消的会话，
        // 于是松手之后什么也不会发生：拖了半天，标签回到原位，没有任何报错。
        TabViewDrag.Active?.Complete(this.PointToScreen(e.GetPosition(this)));

        e.Pointer.Capture(null);
    }

    /// <summary>
    /// 键盘重排。<c>Ctrl+Shift+PageUp/PageDown</c> —— 浏览器与 VS Code 的既有约定，
    /// 不自己发明一套。
    ///
    /// 拖拽是纯指针手势，而工业面板上不一定有鼠标：没有这条路径的话，
    /// 只有触摸屏或只有薄膜键盘的机台上，重排功能等于不存在。
    ///
    /// 修饰键必须全等判定，不能只查「含 Ctrl」：<c>Ctrl+Shift+Alt+PageUp</c>
    /// 多半是宿主自己的快捷键，吞掉它会让那个功能在标签有焦点时莫名失灵。
    /// </summary>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled) return;

        if (e.KeyModifiers != (KeyModifiers.Control | KeyModifiers.Shift)) return;

        var delta = e.Key switch
        {
            Key.PageUp => -1,
            Key.PageDown => 1,
            _ => 0,
        };

        if (delta == 0) return;

        // 挪不动就不吞按键 —— 已经在最左边还吞掉 Ctrl+Shift+PageUp，
        // 宿主绑在这个组合上的功能就再也触发不了。
        if (Owner?.MoveTab(this, delta) == true)
        {
            Focus();
            e.Handled = true;
        }
    }

    /// <summary>
    /// 捕获丢了（切窗口、弹系统对话框、显示器休眠）。
    ///
    /// 必须取消而不是当成松手：捕获丢失时最后一次已知的光标位置未必是操作员想放的地方，
    /// 按那个位置撕出会把标签扔到一个他没指定的屏幕坐标上。
    /// </summary>
    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);

        _pressedAt = null;
        TabViewDrag.Active?.Cancel();
    }
}
