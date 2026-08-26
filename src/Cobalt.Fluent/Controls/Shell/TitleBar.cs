using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.Platform;
using Avalonia.VisualTree;
using Cobalt.Fluent.Automation;
using Avalonia.Automation;
using Avalonia.Automation.Peers;

namespace Cobalt.Fluent.Controls;

/// <summary>贴靠布局面板由谁来出。</summary>
public enum SnapLayoutMode
{
    /// <summary>
    /// 按平台挑（默认）。Windows 11 上用系统的，其余平台用本库自己的。
    ///
    /// 这样挑是因为两者各有一处对方做不到：系统那个能把<b>别的应用</b>的窗口
    /// 安排进剩下的分区（贴靠助手），需要系统级权限，本库做不到；
    /// 而本库这个在 Windows 10、Linux、macOS、嵌入式面板上都有，系统那个只有 Windows 11。
    ///
    /// 要四个平台行为完全一致（比如同一套培训材料、同一份验收用例），
    /// 显式选 <see cref="Builtin"/>。
    /// </summary>
    Auto,

    /// <summary>
    /// 用 Windows 11 的贴靠布局：最大化钮标成非客户区的 <c>HTMAXBUTTON</c>，
    /// 面板由 shell 弹出。非 Windows 平台上等同于 <see cref="None"/>。
    /// </summary>
    System,

    /// <summary>
    /// 用本库自己画的面板。悬停最大化钮弹出 <see cref="SnapLayoutPicker"/>，
    /// 分区几何和窗口摆放都由本库执行，四个平台一致。
    ///
    /// 这个模式下最大化钮<b>不能</b>标成非客户区——标了之后指针事件不再送到
    /// Avalonia，我们自己的悬停就永远触发不了。两套机制只能二选一。
    /// </summary>
    Builtin,

    /// <summary>都不要。最大化钮只是一个普通的最大化钮。</summary>
    None,
}

/// <summary>
/// 自绘标题栏。
///
/// <b>它存在的主要理由是 Windows 11 的贴靠布局（Snap Layouts）。</b>
/// 用系统装饰时贴靠布局本来就是好的——标题栏按钮是 shell 画的，指针悬停在最大化钮上
/// 时由 shell 弹出那个布局面板，应用什么都不用做。**真正会弄坏它的正是自绘标题栏**：
/// 自己画的按钮在 Windows 眼里只是客户区里的一块像素，shell 不知道那是最大化钮，
/// 于是面板再也不弹了。这是所有自定义标题栏应用共同的坑。
///
/// 修法不是去画一个假的布局面板——那个面板是 shell 的 UI，画得再像也接不上
/// 真正的窗口贴靠。要做的是**告诉 Windows「这块像素是最大化钮」**：
/// <c>WM_NCHITTEST</c> 在那里返回 <c>HTMAXBUTTON</c>。Avalonia 把这件事包成了
/// 附加属性 <see cref="Win32Properties.SetNonClientHitTestResult"/>，
/// 见 <see cref="ApplyHitTestRoles"/>。
///
/// 那个附加属性在 Avalonia.Controls 里，不需要 P/Invoke、不需要按平台分支编译；
/// 非 Windows 后端根本不读它，天然是空操作。
///
/// <b>代价要说清楚。</b>被标成非客户区的那几块像素，指针事件不再送到 Avalonia——
/// Windows 自己处理点击（这正是我们要的），但也意味着：
///
/// - 按钮的 <c>Click</c> 事件在 Windows 上不会触发。所以三个按钮的动作分两条路走：
///   Windows 交给系统，其余平台走我们自己的 Click。
/// - 悬停高亮也收不到指针事件。Windows 会发非客户区的移动消息，各版本 Avalonia
///   的转译程度不同，所以视觉反馈在 Windows 上可能弱于其他平台。
///
/// 左右两侧放内容的地方一律标回 <c>Client</c>：整条标题栏都标成非客户区的话，
/// 放在上面的菜单、搜索框会全部失灵，而且失灵的方式是「点了没反应」。
/// </summary>
[PseudoClasses(":maximized", ":inactive")]
[TemplatePart("PART_Caption", typeof(Panel))]
[TemplatePart("PART_Minimize", typeof(Button))]
[TemplatePart("PART_Maximize", typeof(Button))]
[TemplatePart("PART_Close", typeof(Button))]
public class TitleBar : TemplatedControl
{
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<TitleBar, string?>(nameof(Title));

    /// <summary>标题文字。留空则用所在窗口的 <see cref="Window.Title"/>。</summary>
    public string? Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public static readonly StyledProperty<Symbol> IconProperty =
        AvaloniaProperty.Register<TitleBar, Symbol>(nameof(Icon));

    public Symbol Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    /// <summary>图标与标题之后的内容。菜单、面包屑放这里。</summary>
    public static readonly StyledProperty<object?> LeftContentProperty =
        AvaloniaProperty.Register<TitleBar, object?>(nameof(LeftContent));

    public object? LeftContent
    {
        get => GetValue(LeftContentProperty);
        set => SetValue(LeftContentProperty, value);
    }

    /// <summary>窗口按钮之前的内容。搜索框、账户头像放这里。</summary>
    public static readonly StyledProperty<object?> RightContentProperty =
        AvaloniaProperty.Register<TitleBar, object?>(nameof(RightContent));

    public object? RightContent
    {
        get => GetValue(RightContentProperty);
        set => SetValue(RightContentProperty, value);
    }

    public static readonly StyledProperty<bool> IsMinimizeVisibleProperty =
        AvaloniaProperty.Register<TitleBar, bool>(nameof(IsMinimizeVisible), true);

    public bool IsMinimizeVisible
    {
        get => GetValue(IsMinimizeVisibleProperty);
        set => SetValue(IsMinimizeVisibleProperty, value);
    }

    /// <summary>
    /// 最大化钮可见。<b>关掉它就等于关掉贴靠布局</b>——
    /// shell 是靠悬停在最大化钮上来决定弹不弹面板的。
    /// </summary>
    public static readonly StyledProperty<bool> IsMaximizeVisibleProperty =
        AvaloniaProperty.Register<TitleBar, bool>(nameof(IsMaximizeVisible), true);

    public bool IsMaximizeVisible
    {
        get => GetValue(IsMaximizeVisibleProperty);
        set => SetValue(IsMaximizeVisibleProperty, value);
    }

    public static readonly StyledProperty<bool> IsCloseVisibleProperty =
        AvaloniaProperty.Register<TitleBar, bool>(nameof(IsCloseVisible), true);

    public bool IsCloseVisible
    {
        get => GetValue(IsCloseVisibleProperty);
        set => SetValue(IsCloseVisibleProperty, value);
    }

    private string? _effectiveTitle;

    public static readonly DirectProperty<TitleBar, string?> EffectiveTitleProperty =
        AvaloniaProperty.RegisterDirect<TitleBar, string?>(
            nameof(EffectiveTitle), o => o._effectiveTitle);

    /// <summary>
    /// 实际显示的标题：<see cref="Title"/> 为空时退回所在窗口的 <see cref="Window.Title"/>。
    ///
    /// 单开一个只读属性而不是直接往 <see cref="Title"/> 里回填，是因为回填之后
    /// 那个属性就有了值，窗口标题后续再变就跟不上了——退化成「只取第一次」。
    /// </summary>
    public string? EffectiveTitle
    {
        get => _effectiveTitle;
        private set => SetAndRaise(EffectiveTitleProperty, ref _effectiveTitle, value);
    }

    public static readonly StyledProperty<SnapLayoutMode> SnapLayoutModeProperty =
        AvaloniaProperty.Register<TitleBar, SnapLayoutMode>(
            nameof(SnapLayoutMode), SnapLayoutMode.Auto);

    /// <summary>贴靠布局面板由谁来出。见 <see cref="Cobalt.Fluent.Controls.SnapLayoutMode"/>。</summary>
    public SnapLayoutMode SnapLayoutMode
    {
        get => GetValue(SnapLayoutModeProperty);
        set => SetValue(SnapLayoutModeProperty, value);
    }

    private SnapLayoutMode _effectiveMode;

    public static readonly DirectProperty<TitleBar, SnapLayoutMode> EffectiveSnapLayoutModeProperty =
        AvaloniaProperty.RegisterDirect<TitleBar, SnapLayoutMode>(
            nameof(EffectiveSnapLayoutMode), o => o._effectiveMode);

    /// <summary>
    /// 解析 <see cref="SnapLayoutMode.Auto"/>、并核对过能力之后，实际生效的模式。
    ///
    /// 贴靠面板不出来时先看这里：是 <see cref="SnapLayoutMode.None"/> 就说明
    /// 前置条件没满足（窗口不可缩放、最大化钮被藏起来、拿不到屏幕信息、
    /// 或者选了 System 但跑在非 Windows 11 上），不是控件坏了。
    /// </summary>
    public SnapLayoutMode EffectiveSnapLayoutMode
    {
        get => _effectiveMode;
        private set => SetAndRaise(EffectiveSnapLayoutModeProperty, ref _effectiveMode, value);
    }

    private bool _snapLayouts;

    public static readonly DirectProperty<TitleBar, bool> SupportsSnapLayoutsProperty =
        AvaloniaProperty.RegisterDirect<TitleBar, bool>(
            nameof(SupportsSnapLayouts), o => o._snapLayouts);

    /// <summary>
    /// 悬停最大化钮会不会出现贴靠布局面板——<b>不论那个面板是谁画的</b>。
    ///
    /// 等价于 <see cref="EffectiveSnapLayoutMode"/> 不是
    /// <see cref="Cobalt.Fluent.Controls.SnapLayoutMode.None"/>。想知道是系统那个
    /// 还是本库自己画的那个，看 <see cref="EffectiveSnapLayoutMode"/>。
    ///
    /// 报出来是为了让使用方能查：面板不出来的时候，先看这里是不是 false，
    /// 而不是去猜是不是 Avalonia 的锅。
    /// </summary>
    public bool SupportsSnapLayouts
    {
        get => _snapLayouts;
        private set => SetAndRaise(SupportsSnapLayoutsProperty, ref _snapLayouts, value);
    }

    /// <summary>
    /// 把窗口切成「自绘标题栏」模式。三条提示缺一不可，漏掉的表现各不相同：
    /// 不扩展客户区则标题栏被系统标题栏挤在下面；不设 NoChrome 则系统按钮和
    /// 自绘按钮同时出现；不设高度提示则顶部留一条系统预留的空白。
    /// </summary>
    public static void ApplyTo(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);

        window.ExtendClientAreaToDecorationsHint = true;
        window.ExtendClientAreaChromeHints = ExtendClientAreaChromeHints.NoChrome;
        window.ExtendClientAreaTitleBarHeightHint = -1;
    }

    private INameScope? _scope;
    private Window? _window;
    private Button? _minimize;
    private Button? _maximize;
    private Button? _close;

    // 三个钮都只有一个字形、没有文字，朗读名是它们唯一的可及信息——
    // 不给的话读屏软件只报「按钮」，而这三个里有一个是「关闭窗口」。
    // 名字取自 CobaltStrings，换语言之后已经挂上去的那几个得重算。
    private readonly StringsWatcher _strings;

    public TitleBar() => _strings = new StringsWatcher(Refresh);

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        // 这里只退按钮，不动窗口订阅：附加到可视树在前、套模板在后，
        // 把窗口订阅也退掉的话，:maximized / :inactive 和三个按钮就全哑了。
        DetachButtons();

        _minimize = e.NameScope.Find<Button>("PART_Minimize");
        _maximize = e.NameScope.Find<Button>("PART_Maximize");
        _close = e.NameScope.Find<Button>("PART_Close");

        if (_minimize is not null) _minimize.Click += OnMinimize;
        if (_maximize is not null) _maximize.Click += OnMaximize;
        if (_close is not null) _close.Click += OnClose;

        _scope = e.NameScope;
        ApplyHitTestRoles(e.NameScope);
        WireSnapHover();
        Refresh();
    }

    /// <summary>
    /// 给标题栏的各块像素标上「Windows 该把这里当成什么」。
    ///
    /// <b>PART_Maximize 上的 MaxButton 就是贴靠布局的开关。</b>
    /// 没有这一行，Windows 只看到客户区里一块普通像素，面板不会弹。
    ///
    /// 空白处标 Caption 之后，拖动移窗、双击最大化、右键系统菜单全部由 shell 提供，
    /// 我们一行代码都不用写——而且行为和系统标题栏完全一致，这一致性是自己实现
    /// 拿不到的（比如按住拖到屏幕顶边触发最大化）。
    /// </summary>
    private void ApplyHitTestRoles(INameScope scope)
    {
        Set(scope.Find<Panel>("PART_Caption"), Win32Properties.Win32HitTestValue.Caption);
        Set(_minimize, Win32Properties.Win32HitTestValue.MinButton);
        Set(_close, Win32Properties.Win32HitTestValue.Close);

        // 最大化钮的角色取决于面板由谁来出，见 MaximizeHitTestRole。
        Set(_maximize, MaximizeHitTestRole(EffectiveSnapLayoutMode));

        // 左右两侧的内容要标回客户区。不标的话它们落在 PART_Caption 的
        // 命中结果里，菜单、搜索框点了没反应——而且是「静默没反应」，
        // 不报错、不留痕迹。
        Set(scope.Find<ContentPresenter>("PART_LeftContent"), Win32Properties.Win32HitTestValue.Client);
        Set(scope.Find<ContentPresenter>("PART_RightContent"), Win32Properties.Win32HitTestValue.Client);

        static void Set(Visual? visual, Win32Properties.Win32HitTestValue value)
        {
            if (visual is not null) Win32Properties.SetNonClientHitTestResult(visual, value);
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        DetachWindow();

        _window = this.GetVisualRoot() as Window;
        if (_window is not null)
        {
            _window.PropertyChanged += OnWindowPropertyChanged;
            _window.Activated += OnWindowActivation;
            _window.Deactivated += OnWindowActivation;
        }

        _strings.Attach();
        Refresh();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        DetachWindow();
        _strings.Detach();

        // 收面板这一半是保险：Avalonia 的 Popup 在放置目标脱离可视树时会自己关，
        // 试过三种拆法都分辨不出有没有这一行。真正必要的是它顺带停掉的两个定时器——
        // 指针正停在最大化钮上时把标题栏拆掉，那个 400ms 的定时器还在跑，
        // 而它闭包持有 this，控件在这段时间里回收不掉。
        CloseSnapLayouts();
        Refresh();
        base.OnDetachedFromVisualTree(e);
    }

    private void DetachWindow()
    {
        if (_window is null) return;

        _window.PropertyChanged -= OnWindowPropertyChanged;
        _window.Activated -= OnWindowActivation;
        _window.Deactivated -= OnWindowActivation;
        _window = null;
    }

    /// <summary>重新套模板时用。旧模板里的按钮要先退订，否则旧实例被处理器钉住。</summary>
    private void DetachButtons()
    {
        UnwireSnapHover();
        if (_minimize is not null) _minimize.Click -= OnMinimize;
        if (_maximize is not null) _maximize.Click -= OnMaximize;
        if (_close is not null) _close.Click -= OnClose;
        _minimize = _maximize = _close = null;
    }

    private void OnWindowPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == Window.WindowStateProperty
            || e.Property == Window.CanResizeProperty
            || e.Property == Window.IsActiveProperty
            || e.Property == Window.TitleProperty)
        {
            Refresh();
        }
    }

    private void OnWindowActivation(object? sender, EventArgs e) => Refresh();

    private void Refresh()
    {
        EffectiveTitle = string.IsNullOrEmpty(Title) ? _window?.Title : Title;

        var maximized = _window?.WindowState is WindowState.Maximized or WindowState.FullScreen;
        PseudoClasses.Set(":maximized", maximized);

        // 最大化之后那个钮做的事是「还原」，朗读名要跟着换——
        // 字形换了而名字没换，读屏用户听到的就是一个错的动作。
        var strings = CobaltStrings.Current;
        if (_minimize is not null) AutomationProperties.SetName(_minimize, strings.Minimize);
        if (_maximize is not null)
            AutomationProperties.SetName(_maximize, maximized ? strings.Restore : strings.Maximize);
        if (_close is not null) AutomationProperties.SetName(_close, strings.Close);

        PseudoClasses.Set(":inactive", _window is { IsActive: false });

        var mode = ResolveSnapMode();
        var changed = mode != EffectiveSnapLayoutMode;
        EffectiveSnapLayoutMode = mode;
        SupportsSnapLayouts = mode != SnapLayoutMode.None;

        // 模式变了就得重标最大化钮：System 与其余模式对那块像素的要求正好相反。
        if (changed && _scope is not null) ApplyHitTestRoles(_scope);
        if (mode != SnapLayoutMode.Builtin) CloseSnapLayouts();
    }

    /// <summary>
    /// 解析出实际生效的模式。判定本身抽成静态纯函数——
    /// 里面有一条分支（拿不到屏幕信息）在桌面测试环境里造不出来，
    /// 留在实例方法里就只能靠读代码判断对不对。
    /// </summary>
    private SnapLayoutMode ResolveSnapMode() => ResolveSnapMode(
        requested: SnapLayoutMode,
        maximizeVisible: IsMaximizeVisible,
        hasWindow: _window is not null,
        canResize: _window is { CanResize: true },
        hasScreen: _window is not null && WindowSnap.CanSnap(_window),
        systemAvailable: OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000));

    /// <summary>
    /// 模式判定。
    ///
    /// 三条共同前提，缺一条就是 <see cref="SnapLayoutMode.None"/>：最大化钮可见
    /// （面板是靠悬停它触发的）、窗口可缩放（所有布局都要改窗口尺寸）、
    /// 确实挂在一个窗口上。
    ///
    /// <paramref name="systemAvailable"/> 是 Windows 11（内部版本 22000）起才有的
    /// 系统贴靠布局；Windows 10 会忽略 <c>HTMAXBUTTON</c> 的悬停——点击行为仍然正常，
    /// 但面板不弹，所以那里不能报成有。
    ///
    /// <paramref name="hasScreen"/> 为假是单窗口平台（嵌入式 framebuffer、移动端、
    /// 浏览器）：自绘的那个面板要摆窗口，摆不了就别弹——
    /// 弹一个按下去没反应的面板比不弹更糟。
    /// </summary>
    internal static SnapLayoutMode ResolveSnapMode(
        SnapLayoutMode requested,
        bool maximizeVisible,
        bool hasWindow,
        bool canResize,
        bool hasScreen,
        bool systemAvailable)
    {
        if (requested == SnapLayoutMode.None) return SnapLayoutMode.None;
        if (!maximizeVisible || !hasWindow || !canResize) return SnapLayoutMode.None;

        return requested switch
        {
            SnapLayoutMode.System => systemAvailable ? SnapLayoutMode.System : SnapLayoutMode.None,
            SnapLayoutMode.Builtin => hasScreen ? SnapLayoutMode.Builtin : SnapLayoutMode.None,
            _ => systemAvailable ? SnapLayoutMode.System
               : hasScreen ? SnapLayoutMode.Builtin
               : SnapLayoutMode.None,
        };
    }

    /// <summary>
    /// 最大化钮该标成什么。两套机制对这块像素的要求正好相反，所以只能二选一：
    ///
    /// <list type="bullet">
    /// <item>System —— <c>MaxButton</c>，shell 接管悬停，弹它自己的面板。</item>
    /// <item>其余 —— <c>Client</c>。标成 <c>MaxButton</c> 的话指针事件不再送到
    /// Avalonia，我们自己的悬停就永远触发不了，界面上表现为「面板不弹」。</item>
    /// </list>
    /// </summary>
    internal static Win32Properties.Win32HitTestValue MaximizeHitTestRole(SnapLayoutMode effective) =>
        effective == SnapLayoutMode.System
            ? Win32Properties.Win32HitTestValue.MaxButton
            : Win32Properties.Win32HitTestValue.Client;

    // ---- 自绘的贴靠布局面板 --------------------------------------------------
    //
    // 只有 Builtin 模式走这条路。System 模式下最大化钮是非客户区，
    // 下面这些指针事件根本到不了 Avalonia。

    public static readonly StyledProperty<TimeSpan> SnapLayoutHoverDelayProperty =
        AvaloniaProperty.Register<TitleBar, TimeSpan>(
            nameof(SnapLayoutHoverDelay), TimeSpan.FromMilliseconds(400));

    /// <summary>
    /// 指针停在最大化钮上多久才弹面板。默认 400ms，和 Windows 的手感对齐。
    ///
    /// 太短会在指针掠过标题栏时误弹。触摸屏上没有「悬停」这回事，
    /// 那种机器该设成 <see cref="TimeSpan.Zero"/>（下一轮调度就弹）
    /// 并另外给一个显式入口（<see cref="ShowSnapLayouts"/>）。
    /// </summary>
    public TimeSpan SnapLayoutHoverDelay
    {
        get => GetValue(SnapLayoutHoverDelayProperty);
        set => SetValue(SnapLayoutHoverDelayProperty, value);
    }

    public static readonly StyledProperty<TimeSpan> SnapLayoutCloseDelayProperty =
        AvaloniaProperty.Register<TitleBar, TimeSpan>(
            nameof(SnapLayoutCloseDelay), TimeSpan.FromMilliseconds(250));

    /// <summary>
    /// 指针离开后多久收起面板。默认 250ms。
    ///
    /// 这一段不能是 0：面板弹在钮的下方，指针从钮移进面板的路上会短暂地
    /// 既不在钮上也不在面板上，立刻收起的话面板根本点不到。
    /// </summary>
    public TimeSpan SnapLayoutCloseDelay
    {
        get => GetValue(SnapLayoutCloseDelayProperty);
        set => SetValue(SnapLayoutCloseDelayProperty, value);
    }

    private Popup? _snapPopup;
    private SnapLayoutPicker? _picker;
    private DispatcherTimer? _openTimer;
    private DispatcherTimer? _closeTimer;

    private void WireSnapHover()
    {
        if (_maximize is null) return;

        _maximize.PointerEntered += OnMaximizeEntered;
        _maximize.PointerExited += OnMaximizePointerExited;
    }

    private void UnwireSnapHover()
    {
        if (_maximize is null) return;

        _maximize.PointerEntered -= OnMaximizeEntered;
        _maximize.PointerExited -= OnMaximizePointerExited;
    }

    private void OnMaximizeEntered(object? sender, PointerEventArgs e)
    {
        if (EffectiveSnapLayoutMode != SnapLayoutMode.Builtin) return;

        _closeTimer?.Stop();

        // 间隔为 0 时定时器下一轮就触发，等价于当场弹——触摸屏那种没有悬停的机器
        // 就该配成 0，不必为它单开一条分支。
        _openTimer ??= NewTimer(ShowSnapLayouts);
        _openTimer.Stop();
        _openTimer.Interval = SnapLayoutHoverDelay;
        _openTimer.Start();
    }

    private void OnMaximizePointerExited(object? sender, PointerEventArgs e)
    {
        _openTimer?.Stop();
        ScheduleClose();
    }

    private void ScheduleClose()
    {
        _closeTimer ??= NewTimer(CloseSnapLayouts);
        _closeTimer.Stop();
        _closeTimer.Interval = SnapLayoutCloseDelay;
        _closeTimer.Start();
    }

    private static DispatcherTimer NewTimer(Action tick)
    {
        var timer = new DispatcherTimer();
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            tick();
        };
        return timer;
    }

    /// <summary>
    /// 弹出自绘的贴靠布局面板。
    ///
    /// 公开出来是为了让使用方能绑快捷键：悬停是纯指针手势，而工业面板上
    /// 不一定有鼠标。只在 <see cref="SnapLayoutMode.Builtin"/> 生效时有效——
    /// 系统那个面板是 shell 弹的，我们叫不动它。
    /// </summary>
    public void ShowSnapLayouts()
    {
        if (EffectiveSnapLayoutMode != SnapLayoutMode.Builtin || _maximize is null) return;

        if (_snapPopup is null)
        {
            _picker = new SnapLayoutPicker();
            _picker.ZoneSelected += (_, _) => CloseSnapLayouts();
            _picker.PointerEntered += (_, _) => _closeTimer?.Stop();
            _picker.PointerExited += (_, _) => ScheduleClose();

            _snapPopup = new Popup
            {
                Child = _picker,
                PlacementTarget = _maximize,
                Placement = PlacementMode.BottomEdgeAlignedRight,
                IsLightDismissEnabled = true,
                // 面板要跟着钮走：窗口被拖动时不跟的话，面板会留在原地，
                // 指着一块和最大化钮已经没有关系的屏幕位置。
                InheritsTransform = true,
            };

            ((ISetLogicalParent)_snapPopup).SetParent(this);
        }

        _picker!.TargetWindow = _window;
        _snapPopup.IsOpen = true;
    }

    /// <summary>
    /// 自绘的贴靠布局面板现在是不是开着。
    ///
    /// 使用方需要它来避让：面板开着的时候不该再弹自己的菜单，
    /// 两个悬浮层叠在标题栏同一块地方，操作员分不清点的是哪个。
    /// </summary>
    public bool IsSnapLayoutsOpen => _snapPopup is { IsOpen: true };

    /// <summary>收起自绘的贴靠布局面板。没弹出来时是空操作。</summary>
    public void CloseSnapLayouts()
    {
        _openTimer?.Stop();
        _closeTimer?.Stop();
        if (_snapPopup is not null) _snapPopup.IsOpen = false;
    }

    // ---- 非 Windows 平台的按钮动作 -------------------------------------------
    //
    // Windows 上这三个处理器不会被调用：那几块像素已经是非客户区，指针事件根本
    // 不送到 Avalonia，系统自己处理了点击。其余平台没人读那个附加属性，
    // 于是走这里。两条路都得在，缺一个就是「某个平台上按钮点不动」。

    private void OnMinimize(object? sender, RoutedEventArgs e)
    {
        if (_window is not null) _window.WindowState = WindowState.Minimized;
    }

    private void OnMaximize(object? sender, RoutedEventArgs e)
    {
        if (_window is null) return;

        _window.WindowState = _window.WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
    }

    private void OnClose(object? sender, RoutedEventArgs e) => _window?.Close();

    static TitleBar()
    {
        IsMaximizeVisibleProperty.Changed.AddClassHandler<TitleBar>((x, _) => x.Refresh());
        TitleProperty.Changed.AddClassHandler<TitleBar>((x, _) => x.Refresh());
        SnapLayoutModeProperty.Changed.AddClassHandler<TitleBar>((x, _) => x.Refresh());
    }

    /// <summary>见 <see cref="Cobalt.Fluent.Automation.TitleBarAutomationPeer"/>。</summary>
    protected override AutomationPeer OnCreateAutomationPeer() => new TitleBarAutomationPeer(this);
}
