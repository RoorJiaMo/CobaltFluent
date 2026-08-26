using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace Cobalt.Fluent.Controls;

/// <summary>
/// 一次标签拖拽。从按下超过阈值开始，到松手或失去捕获结束。
///
/// **为什么不用 <c>Window.BeginMoveDrag</c>。** 窗口移动一旦交给窗口管理器，
/// 进程就收不到任何指针事件了——于是「拖回来并入」无从检测。
/// Chrome 在 Windows 上自己实现窗口拖动正是这个原因。
///
/// **为什么不用 <c>DragDrop.DoDragDrop</c>。** 那是操作系统的剪贴板式拖放协议：
/// 载荷要能序列化，而这里要传的是一个活着的控件实例；各平台的行为差异也大。
///
/// 所以走指针捕获：捕获之后即使光标离开窗口，<c>PointerMoved</c> 仍然送到源窗口。
/// 坐标用 <c>PointToScreen</c> 换算到屏幕系，预览窗用 <c>Window.Position</c> 跟随，
/// 落点由自己拿屏幕坐标去比对每个窗口的标签栏——不依赖操作系统的命中测试。
/// 全托管、零反射，AOT 闸口过得去，各桌面后端行为一致。
/// </summary>
internal sealed class TabViewDrag
{
    /// <summary>超过这么多像素才算拖拽。低于它是点击——按下即开拖会让选标签变得很难点。</summary>
    private const double Threshold = 6;

    /// <summary>当前正在进行的那一次。同一时刻只可能有一个指针在拖标签。</summary>
    public static TabViewDrag? Active { get; private set; }

    private readonly TabView _source;
    private readonly TabViewItem _item;
    private readonly PixelPoint _grabOffset;
    private readonly PixelPoint _origin;

    private Window? _preview;
    private TabView? _target;
    private int _insertAt = -1;
    private bool _moved;

    private TabViewDrag(TabView source, TabViewItem item, PixelPoint pointer)
    {
        _source = source;
        _item = item;
        _origin = pointer;

        // 记下光标抓在标签内的哪个位置。撕出时按这个偏移摆窗口，
        // 否则窗口会在松手瞬间「跳」一下，跳的距离正好是抓握偏移。
        var topLeft = item.PointToScreen(new Point(0, 0));
        _grabOffset = new PixelPoint(pointer.X - topLeft.X, pointer.Y - topLeft.Y);
    }

    public static TabViewDrag Begin(TabView source, TabViewItem item, PixelPoint pointer) =>
        Active = new TabViewDrag(source, item, pointer);

    /// <summary>按下之后还没超过阈值时用它判断要不要真的开始。</summary>
    public static bool PastThreshold(PixelPoint from, PixelPoint to) =>
        Math.Abs(to.X - from.X) > Threshold || Math.Abs(to.Y - from.Y) > Threshold;

    public void Update(PixelPoint pointer)
    {
        _moved = true;
        Resolve(pointer, out _target, out _insertAt);

        if (_target is null)
        {
            // 不在任何标签栏上：预览窗跟着光标走，各标签栏上的落点指示器都撤掉。
            ClearIndicators();
            ShowPreview(pointer);
        }
        else
        {
            HidePreview();
            ClearIndicators();
            _target.ShowDropIndicator(_insertAt);
        }
    }

    public void Complete(PixelPoint pointer)
    {
        try
        {
            if (!_moved) return;

            Resolve(pointer, out var target, out var insertAt);

            if (target is not null)
                target.AcceptTab(_source, _item, insertAt);
            else
                _source.TearOut(_item, TabDrop.TearOutOrigin(pointer, _grabOffset));
        }
        finally
        {
            Dispose();
        }
    }

    public void Cancel() => Dispose();

    private bool _disposed;

    /// <summary>
    /// 收尾。**必须可重入。**落地过程中开窗口可能触发失焦、进而触发捕获丢失，
    /// 于是 Cancel 会在 Complete 的 finally 之前先跑一遍——第二次进来时
    /// 预览窗已经关掉了，再 Close 一次会抛。
    /// </summary>
    private void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        ClearIndicators();
        _preview?.Close();
        _preview = null;

        if (ReferenceEquals(Active, this)) Active = null;
    }

    /// <summary>屏幕上这个点落在哪个 TabView 的标签栏里，以及该插到第几个。</summary>
    private void Resolve(PixelPoint pointer, out TabView? target, out int insertAt)
    {
        target = null;
        insertAt = -1;

        var views = OpenTabViews();
        if (views.Count == 0) return;

        var strip = TabDrop.StripAt(pointer, views.Select(v => v.StripBoundsOnScreen()).ToArray());
        if (strip < 0) return;

        target = views[strip];
        insertAt = TabDrop.InsertIndexAt(pointer, target.TabBoundsOnScreen());

        // 同一条标签栏内重排：移除原项会让它后面的下标前移一位。
        if (ReferenceEquals(target, _source))
            insertAt = TabDrop.NormalizeMoveIndex(_source.IndexFromContainer(_item), insertAt);
    }

    /// <summary>所有打开窗口里能接收标签的 TabView。单窗口平台上只有当前这一个。</summary>
    private static IReadOnlyList<TabView> OpenTabViews()
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
            return [];

        return desktop.Windows
            .SelectMany(w => w.GetVisualDescendants().OfType<TabView>())
            .Where(v => v.IsReorderEnabled || v.IsTearOutEnabled)
            .ToArray();
    }

    private static void ClearIndicators()
    {
        foreach (var v in OpenTabViews()) v.HideDropIndicator();
    }

    // ---- 跟随光标的预览窗 ----------------------------------------------------

    private void ShowPreview(PixelPoint pointer)
    {
        if (!_source.CanTearOut) return;

        _preview ??= CreatePreview();
        _preview.Position = TabDrop.TearOutOrigin(pointer, _grabOffset);
        if (!_preview.IsVisible) _preview.Show();
    }

    private void HidePreview()
    {
        if (_preview is { IsVisible: true }) _preview.Hide();
    }

    /// <summary>
    /// 预览窗必须 <c>ShowActivated = false</c>。
    ///
    /// 这一条是整个方案的支点：预览窗一旦抢走激活，源窗口的指针捕获就丢了，
    /// 后面的移动事件全都收不到，拖拽当场断在半路。
    /// </summary>
    private Window CreatePreview() => new()
    {
        SystemDecorations = SystemDecorations.None,
        ShowActivated = false,
        ShowInTaskbar = false,
        Topmost = true,
        CanResize = false,
        SizeToContent = SizeToContent.WidthAndHeight,
        Background = Brushes.Transparent,
        TransparencyLevelHint = [WindowTransparencyLevel.Transparent],
        Content = new Border
        {
            Background = _item.FindResource("SolidBackgroundFillColorQuarternaryBrush") as IBrush
                         ?? Brushes.White,
            BorderBrush = _item.FindResource("CardStrokeColorDefaultBrush") as IBrush
                          ?? Brushes.Gray,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(12, 6),
            Opacity = 0.9,
            Child = new TextBlock
            {
                Text = _item.Header?.ToString() ?? string.Empty,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            },
        },
    };
}
