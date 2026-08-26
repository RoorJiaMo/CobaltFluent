using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.VisualTree;
using Cobalt.Fluent.Automation;

namespace Cobalt.Fluent.Controls;

/// <summary>贴靠面板里的一格。单开一个类型只为拿到一份自己的 ControlTheme——
/// 这些格子是代码建的，代码建的子元素接不到 <c>/template/</c> 选择器。</summary>
public class SnapZoneButton : Button
{
    protected override Type StyleKeyOverride => typeof(SnapZoneButton);
}

/// <summary>选中了某一块分区。</summary>
public class SnapZoneSelectedEventArgs(SnapLayout layout, SnapZone zone, int index) : EventArgs
{
    public SnapLayout Layout { get; } = layout;

    public SnapZone Zone { get; } = zone;

    /// <summary>分区在该布局里的序号，按阅读顺序。</summary>
    public int Index { get; } = index;
}

/// <summary>
/// 贴靠布局面板。**本库自己画的那一个。**
///
/// Windows 11 上悬停最大化钮弹出来的那个面板是 shell 的 UI，只有 Windows 11 有。
/// 这个控件把同一件事做在框架内部：布局表、几何、命中、执行全部是本库的代码，
/// 所以 Windows 10、Linux、macOS、嵌入式面板上拿到的是同一套东西。
///
/// 面板上画出来的每一格，就是按下去之后窗口真正会占的那块像素——
/// 预览和结果走的是同一个 <see cref="SnapGeometry.ZoneRect"/>，
/// 不存在「画的是一回事、贴过去是另一回事」。
///
/// <b>能摆的只有本进程自己的窗口。</b>Windows 的贴靠助手会在剩下的分区里列出
/// 别的应用的窗口，那需要系统级权限，本库做不到，也不假装做得到。
/// </summary>
[TemplatePart("PART_Layouts", typeof(Panel))]
public class SnapLayoutPicker : TemplatedControl
{
    /// <summary>
    /// 要摆的窗口。留空则取面板自己所在的窗口。
    ///
    /// 可用的布局是按这个窗口所在的屏幕算的——双屏机器上主屏和副屏的
    /// 分辨率、方向、缩放都可能不同，算错屏幕就会把三栏布局发到一块竖屏上。
    /// </summary>
    public static readonly StyledProperty<Window?> TargetWindowProperty =
        AvaloniaProperty.Register<SnapLayoutPicker, Window?>(nameof(TargetWindow));

    public Window? TargetWindow
    {
        get => GetValue(TargetWindowProperty);
        set => SetValue(TargetWindowProperty, value);
    }

    /// <summary>单个示意图的宽，逻辑像素。</summary>
    public static readonly StyledProperty<double> PreviewWidthProperty =
        AvaloniaProperty.Register<SnapLayoutPicker, double>(nameof(PreviewWidth), 108d);

    public double PreviewWidth
    {
        get => GetValue(PreviewWidthProperty);
        set => SetValue(PreviewWidthProperty, value);
    }

    /// <summary>单个示意图的高，逻辑像素。</summary>
    public static readonly StyledProperty<double> PreviewHeightProperty =
        AvaloniaProperty.Register<SnapLayoutPicker, double>(nameof(PreviewHeight), 68d);

    public double PreviewHeight
    {
        get => GetValue(PreviewHeightProperty);
        set => SetValue(PreviewHeightProperty, value);
    }

    private IReadOnlyList<SnapLayout> _layouts = [];

    public static readonly DirectProperty<SnapLayoutPicker, IReadOnlyList<SnapLayout>> LayoutsProperty =
        AvaloniaProperty.RegisterDirect<SnapLayoutPicker, IReadOnlyList<SnapLayout>>(
            nameof(Layouts), o => o._layouts);

    /// <summary>
    /// 当前这块屏幕上提供的布局。竖屏、窄屏、带鱼屏拿到的不是同一套，
    /// 见 <see cref="SnapGeometry.LayoutsFor"/>。拿不到屏幕信息时是空表。
    /// </summary>
    public IReadOnlyList<SnapLayout> Layouts
    {
        get => _layouts;
        private set => SetAndRaise(LayoutsProperty, ref _layouts, value);
    }

    /// <summary>
    /// 选中了一块分区。
    ///
    /// 控件<b>不会</b>自己去摆窗口——和 TabView 的关闭请求一样，
    /// 真正的动作留给使用方：正经场景里这一下可能要先确认、要记住布局、
    /// 要顺带安排别的窗口。<see cref="SnapSelectedWindow"/> 是现成的默认做法。
    /// </summary>
    public event EventHandler<SnapZoneSelectedEventArgs>? ZoneSelected;

    /// <summary>
    /// 选中分区后是否直接把 <see cref="TargetWindow"/> 贴过去。默认开。
    ///
    /// 关掉它就只发 <see cref="ZoneSelected"/>，由使用方决定怎么摆。
    /// </summary>
    public static readonly StyledProperty<bool> SnapSelectedWindowProperty =
        AvaloniaProperty.Register<SnapLayoutPicker, bool>(nameof(SnapSelectedWindow), true);

    public bool SnapSelectedWindow
    {
        get => GetValue(SnapSelectedWindowProperty);
        set => SetValue(SnapSelectedWindowProperty, value);
    }

    private readonly StringsWatcher _strings;

    public SnapLayoutPicker() => _strings = new StringsWatcher(Rebuild);

    private Panel? _host;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        _host = e.NameScope.Find<Panel>("PART_Layouts");
        Rebuild();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _strings.Attach();
        Rebuild();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _strings.Detach();
        base.OnDetachedFromVisualTree(e);
    }

    static SnapLayoutPicker()
    {
        TargetWindowProperty.Changed.AddClassHandler<SnapLayoutPicker>((x, _) => x.Rebuild());
        PreviewWidthProperty.Changed.AddClassHandler<SnapLayoutPicker>((x, _) => x.Rebuild());
        PreviewHeightProperty.Changed.AddClassHandler<SnapLayoutPicker>((x, _) => x.Rebuild());
    }

    /// <summary>
    /// 要摆的窗口：显式指定的优先，否则往上找面板所在的窗口。
    ///
    /// 不能只看 <c>GetVisualRoot() as Window</c>：这个面板天生就是放在弹出层里的，
    /// 而弹出层的可视根是 <c>PopupRoot</c> 而不是 <c>Window</c>。只看一层的话，
    /// 放进 Flyout 的面板会得到一张空布局表——不报错，就是什么都不显示。
    /// 所以顺着宿主链往上走，直到走到真正的窗口。
    /// </summary>
    private Window? ResolveWindow()
    {
        if (TargetWindow is { } explicitTarget) return explicitTarget;

        var root = this.GetVisualRoot();

        // 弹出层可以套弹出层（面板里再开一个菜单），所以是循环不是一次。
        // 给个上限：宿主链理论上不会成环，但真成环了这里会挂死整个界面。
        for (var hop = 0; hop < 8 && root is not null; hop++)
        {
            if (root is Window window) return window;
            if (root is not IHostedVisualTreeRoot { Host: { } host }) return null;
            root = host.GetVisualRoot();
        }

        return null;
    }

    private void Rebuild()
    {
        var window = ResolveWindow();
        Layouts = window is null ? [] : WindowSnap.LayoutsFor(window);

        if (_host is null) return;
        _host.Children.Clear();

        var strings = CobaltStrings.Current;

        foreach (var layout in Layouts)
        {
            var preview = new SnapZonePanel
            {
                Width = PreviewWidth,
                Height = PreviewHeight,
            };

            for (var i = 0; i < layout.Zones.Count; i++)
            {
                var zone = layout.Zones[i];
                var cell = new SnapZoneButton();

                SnapZonePanel.SetZone(cell, zone);

                // 朗读名要说清「按下去窗口会去哪」。念「区域 2/4」等于没说。
                AutomationProperties.SetName(
                    cell, strings.SnapZoneName(SnapGeometry.Classify(zone), zone));

                var captured = (Layout: layout, Zone: zone, Index: i);
                cell.Click += (_, _) => Select(captured.Layout, captured.Zone, captured.Index);

                preview.Children.Add(cell);
            }

            // 一台「显示器」。不套这一圈的话，格子之间的缝直接透出面板底色，
            // 看着像四个孤立的方块，认不出是同一块屏幕被切开。
            var frame = new Border
            {
                Margin = new Thickness(6),
                Padding = new Thickness(3),
                CornerRadius = new CornerRadius(4),
                Background = null,
                Child = preview,
            };

            frame.Bind(Border.BackgroundProperty,
                this.GetResourceObservable("SolidBackgroundFillColorTertiaryBrush"));
            frame.Bind(Border.BorderBrushProperty,
                this.GetResourceObservable("CardStrokeColorDefaultBrush"));
            frame.BorderThickness = new Thickness(1);

            AutomationProperties.SetName(frame, strings.SnapLayoutName(layout.Kind));

            // Tab 在布局之间跳，方向键在布局内部走。一套四宫格四个格子，
            // 六套布局就是十几个 Tab 站点——全塞进 Tab 序列的话，
            // 键盘用户想跳过这个面板要按十几下。
            KeyboardNavigation.SetTabNavigation(frame, KeyboardNavigationMode.Once);

            _host.Children.Add(frame);
        }
    }

    /// <summary>
    /// 方向键在面板里走。
    ///
    /// 左右在同一套布局的格子之间走，上下换布局并停在同序号的格子上。
    /// Avalonia 11.3 没有可用的二维焦点导航，与其赌框架行为，不如自己接管——
    /// 这几格没有文字，键盘用户全靠焦点框判断当前选的是哪一块。
    /// </summary>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        // 只接裸方向键。不看修饰键的话，Ctrl+Left 之类的应用级快捷键会被这里吞掉，
        // 而且还顺手把焦点挪走——两个后果都难查。
        if (e.Handled || e.KeyModifiers != KeyModifiers.None || _host is null) return;

        var cells = _host.Children.OfType<Border>()
            .Select(b => b.Child as SnapZonePanel)
            .Where(p => p is not null)
            .Select(p => p!.Children.OfType<SnapZoneButton>().ToArray())
            .Where(a => a.Length > 0)
            .ToArray();

        if (cells.Length == 0) return;

        var (row, column) = Locate(cells);
        if (row < 0) return;

        var (nextRow, nextColumn) = e.Key switch
        {
            Key.Left => (row, column - 1),
            Key.Right => (row, column + 1),
            Key.Up => (row - 1, column),
            Key.Down => (row + 1, column),
            Key.Home => (0, 0),
            Key.End => (cells.Length - 1, int.MaxValue),
            _ => (-1, -1),
        };

        if (nextRow < 0 || nextRow >= cells.Length) return;

        var target = cells[nextRow][Math.Clamp(nextColumn, 0, cells[nextRow].Length - 1)];
        target.Focus(NavigationMethod.Directional);
        e.Handled = true;
    }

    /// <summary>焦点当前落在第几套布局的第几格。都没有就是 (-1, -1)。</summary>
    private (int Row, int Column) Locate(SnapZoneButton[][] cells)
    {
        for (var r = 0; r < cells.Length; r++)
        for (var c = 0; c < cells[r].Length; c++)
            if (cells[r][c].IsFocused)
                return (r, c);

        return (-1, -1);
    }

    private void Select(SnapLayout layout, SnapZone zone, int index)
    {
        ZoneSelected?.Invoke(this, new SnapZoneSelectedEventArgs(layout, zone, index));

        if (SnapSelectedWindow && ResolveWindow() is { } window)
            WindowSnap.Snap(window, zone);
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new SnapLayoutPickerAutomationPeer(this);
}
