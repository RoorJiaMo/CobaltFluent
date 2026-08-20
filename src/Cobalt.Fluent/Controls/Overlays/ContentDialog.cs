using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;

namespace Cobalt.Fluent.Controls;

/// <summary>对话框关掉的原因。</summary>
public enum ContentDialogResult
{
    /// <summary>点了关闭按钮，或按了 Esc。</summary>
    None,

    Primary,
    Secondary,
}

/// <summary>哪个按钮是默认按钮（回车触发、accent 外观）。</summary>
public enum ContentDialogButton
{
    None,
    Primary,
    Secondary,
    Close,
}

/// <summary>
/// 模态对话框。Avalonia 本体没有这个控件。
///
/// 用 <c>await dialog.ShowAsync(owner)</c> 拿结果，天然适合 MVVM 的 await 流程。
/// 挂在 <see cref="OverlayLayer"/> 上，不进页面布局。
///
/// 视觉上它是**实色**（SolidBackgroundFillColorBase），不是亚克力——
/// 亚克力留给 Flyout / MenuFlyout / ToolTip 那几层。
/// 底部按钮等宽撑满，这是 Win11 的特征。
/// </summary>
public class ContentDialog : ContentControl
{
    private TaskCompletionSource<ContentDialogResult>? _completion;
    private Panel? _host;
    private Button? _primary;
    private Button? _secondary;
    private Button? _close;

    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<ContentDialog, string?>(nameof(Title));

    public string? Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public static readonly StyledProperty<string?> PrimaryButtonTextProperty =
        AvaloniaProperty.Register<ContentDialog, string?>(nameof(PrimaryButtonText));

    public string? PrimaryButtonText
    {
        get => GetValue(PrimaryButtonTextProperty);
        set => SetValue(PrimaryButtonTextProperty, value);
    }

    public static readonly StyledProperty<string?> SecondaryButtonTextProperty =
        AvaloniaProperty.Register<ContentDialog, string?>(nameof(SecondaryButtonText));

    public string? SecondaryButtonText
    {
        get => GetValue(SecondaryButtonTextProperty);
        set => SetValue(SecondaryButtonTextProperty, value);
    }

    public static readonly StyledProperty<string?> CloseButtonTextProperty =
        AvaloniaProperty.Register<ContentDialog, string?>(nameof(CloseButtonText), "取消");

    public string? CloseButtonText
    {
        get => GetValue(CloseButtonTextProperty);
        set => SetValue(CloseButtonTextProperty, value);
    }

    public static readonly StyledProperty<ContentDialogButton> DefaultButtonProperty =
        AvaloniaProperty.Register<ContentDialog, ContentDialogButton>(
            nameof(DefaultButton), ContentDialogButton.Primary);

    public ContentDialogButton DefaultButton
    {
        get => GetValue(DefaultButtonProperty);
        set => SetValue(DefaultButtonProperty, value);
    }

    /// <summary>
    /// 主操作是否危险（删除、清空、强制停机）。
    /// 打开后主按钮不走 accent 而走 critical —— 危险操作不该长得像「推荐操作」。
    /// </summary>
    public static readonly StyledProperty<bool> IsPrimaryDestructiveProperty =
        AvaloniaProperty.Register<ContentDialog, bool>(nameof(IsPrimaryDestructive));

    public bool IsPrimaryDestructive
    {
        get => GetValue(IsPrimaryDestructiveProperty);
        set => SetValue(IsPrimaryDestructiveProperty, value);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        Rewire(ref _primary, e.NameScope.Find<Button>("PART_PrimaryButton"), ContentDialogResult.Primary);
        Rewire(ref _secondary, e.NameScope.Find<Button>("PART_SecondaryButton"), ContentDialogResult.Secondary);
        Rewire(ref _close, e.NameScope.Find<Button>("PART_CloseButton"), ContentDialogResult.None);

        void Rewire(ref Button? field, Button? found, ContentDialogResult result)
        {
            if (field is not null) field.Click -= Handler;
            field = found;
            if (field is not null) field.Click += Handler;

            void Handler(object? _, RoutedEventArgs __) => Complete(result);
        }
    }

    /// <summary>
    /// 弹出并等结果。<paramref name="owner"/> 给视觉树上的任意一个元素即可，
    /// 用来找到所在窗口的 OverlayLayer。
    /// </summary>
    public Task<ContentDialogResult> ShowAsync(Visual owner)
    {
        if (_completion is { Task.IsCompleted: false })
            return _completion.Task;

        var layer = OverlayLayer.GetOverlayLayer(owner)
            ?? throw new InvalidOperationException("找不到 OverlayLayer：owner 还没挂到视觉树上。");

        _completion = new TaskCompletionSource<ContentDialogResult>();

        // 模态遮罩。铺满整层，吃掉底下的点击。
        var smoke = new Border
        {
            [!Border.BackgroundProperty] = this.GetResourceObservable("SmokeFillColorDefaultBrush")
                .ToBinding(),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
        };

        _host = new Panel { Children = { smoke, this } };
        HorizontalAlignment = HorizontalAlignment.Center;
        VerticalAlignment = VerticalAlignment.Center;

        layer.Children.Add(_host);

        // Esc 等于按关闭按钮。模态框不做 light dismiss——点外部不关，
        // 否则容易误触把「确认停机」这类对话框划掉。
        _host.KeyDown += OnHostKeyDown;
        _host.Focusable = true;
        Focus();

        return _completion.Task;
    }

    private void OnHostKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Complete(ContentDialogResult.None);
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Enter)
        {
            var result = DefaultButton switch
            {
                ContentDialogButton.Primary when !string.IsNullOrEmpty(PrimaryButtonText)
                    => ContentDialogResult.Primary,
                ContentDialogButton.Secondary when !string.IsNullOrEmpty(SecondaryButtonText)
                    => ContentDialogResult.Secondary,
                ContentDialogButton.Close => ContentDialogResult.None,
                _ => (ContentDialogResult?)null,
            };

            if (result is { } r)
            {
                Complete(r);
                e.Handled = true;
            }
        }
    }

    /// <summary>用代码关掉对话框。</summary>
    public void Hide(ContentDialogResult result = ContentDialogResult.None) => Complete(result);

    private void Complete(ContentDialogResult result)
    {
        if (_host is not null)
        {
            _host.KeyDown -= OnHostKeyDown;
            _host.Children.Remove(this);
            (_host.Parent as OverlayLayer)?.Children.Remove(_host);
            _host = null;
        }

        _completion?.TrySetResult(result);
    }
}
