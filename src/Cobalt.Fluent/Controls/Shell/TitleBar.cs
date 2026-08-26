using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Platform;
using Avalonia.VisualTree;
using Cobalt.Fluent.Automation;
using Avalonia.Automation;
using Avalonia.Automation.Peers;

namespace Cobalt.Fluent.Controls;

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

    private bool _snapLayouts;

    public static readonly DirectProperty<TitleBar, bool> SupportsSnapLayoutsProperty =
        AvaloniaProperty.RegisterDirect<TitleBar, bool>(
            nameof(SupportsSnapLayouts), o => o._snapLayouts);

    /// <summary>
    /// 这个窗口的最大化钮会不会触发 Windows 11 的贴靠布局。
    ///
    /// 三个条件缺一不可：跑在 Windows 11（内部版本 22000 起）、最大化钮可见、
    /// 窗口可缩放——**不可缩放的窗口 shell 不弹面板**，因为那些布局都要改窗口尺寸。
    ///
    /// 报出来是为了让使用方能查：贴靠布局不出来的时候，先看这里是不是 false，
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

        ApplyHitTestRoles(e.NameScope);
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
        Set(_maximize, Win32Properties.Win32HitTestValue.MaxButton);
        Set(_close, Win32Properties.Win32HitTestValue.Close);

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

        // Windows 11 起才有贴靠布局；Windows 10 会忽略 HTMAXBUTTON 的悬停，
        // 点击行为仍然正常，所以这里只影响「报出来的能力」，不影响功能。
        SupportsSnapLayouts =
            OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000)
            && IsMaximizeVisible
            && _window is { CanResize: true };
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
    }

    /// <summary>见 <see cref="Cobalt.Fluent.Automation.TitleBarAutomationPeer"/>。</summary>
    protected override AutomationPeer OnCreateAutomationPeer() => new TitleBarAutomationPeer(this);
}
