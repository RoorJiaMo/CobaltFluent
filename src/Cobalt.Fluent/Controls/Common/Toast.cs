using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Threading;
using Cobalt.Fluent.Automation;

namespace Cobalt.Fluent.Controls;

/// <summary>
/// 瞬时通知。和 <see cref="InfoBar"/> 的区别是它会自动消失、且**不占布局**——
/// 挂在 OverlayLayer 上。
///
/// 停留 4 秒；带操作按钮的延到 8 秒（人要先读懂再决定点不点）。
/// </summary>
[PseudoClasses(":informational", ":success", ":error")]
public class Toast : TemplatedControl
{
    public static readonly StyledProperty<InfoBarSeverity> SeverityProperty =
        AvaloniaProperty.Register<Toast, InfoBarSeverity>(
            nameof(Severity), InfoBarSeverity.Informational);

    public InfoBarSeverity Severity
    {
        get => GetValue(SeverityProperty);
        set => SetValue(SeverityProperty, value);
    }

    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<Toast, string?>(nameof(Title));

    public string? Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public static readonly StyledProperty<string?> MessageProperty =
        AvaloniaProperty.Register<Toast, string?>(nameof(Message));

    public string? Message
    {
        get => GetValue(MessageProperty);
        set => SetValue(MessageProperty, value);
    }

    public static readonly StyledProperty<object?> ActionContentProperty =
        AvaloniaProperty.Register<Toast, object?>(nameof(ActionContent));

    public object? ActionContent
    {
        get => GetValue(ActionContentProperty);
        set => SetValue(ActionContentProperty, value);
    }

    private Symbol _glyph = Symbol.Info;

    public static readonly DirectProperty<Toast, Symbol> GlyphProperty =
        AvaloniaProperty.RegisterDirect<Toast, Symbol>(nameof(Glyph), o => o._glyph);

    public Symbol Glyph
    {
        get => _glyph;
        private set => SetAndRaise(GlyphProperty, ref _glyph, value);
    }

    static Toast()
    {
        SeverityProperty.Changed.AddClassHandler<Toast>((x, _) => x.Refresh());
    }

    public Toast() => Refresh();

    private void Refresh()
    {
        var s = Severity;
        PseudoClasses.Set(":informational",
            s is InfoBarSeverity.Informational or InfoBarSeverity.Warning);
        PseudoClasses.Set(":success", s == InfoBarSeverity.Success);
        PseudoClasses.Set(":error", s == InfoBarSeverity.Error);

        Glyph = s switch
        {
            InfoBarSeverity.Success => Symbol.Completed,
            InfoBarSeverity.Warning => Symbol.Warning,
            InfoBarSeverity.Error => Symbol.Error,
            _ => Symbol.Info,
        };
    }

    /// <summary>见 <see cref="Cobalt.Fluent.Automation.ToastAutomationPeer"/>。</summary>
    protected override AutomationPeer OnCreateAutomationPeer() => new ToastAutomationPeer(this);
}

/// <summary>
/// Toast 的承载层。挂在窗口的 OverlayLayer 上，右下角堆叠，不进页面布局。
///
/// <code>
/// ToastHost.Show(this, new Toast { Title = "配方已保存" });
/// </code>
/// </summary>
public class ToastHost : ItemsControl
{
    /// <summary>无操作按钮时的停留时长。</summary>
    public static TimeSpan DefaultDuration { get; set; } = TimeSpan.FromSeconds(4);

    /// <summary>带操作按钮时的停留时长。要留出读懂再决定的时间。</summary>
    public static TimeSpan ActionDuration { get; set; } = TimeSpan.FromSeconds(8);

    /// <summary>在 <paramref name="owner"/> 所在窗口弹一条 Toast。</summary>
    public static void Show(Visual owner, Toast toast, TimeSpan? duration = null)
    {
        var layer = OverlayLayer.GetOverlayLayer(owner);
        if (layer is null) return;

        var host = layer.Children.OfType<ToastHost>().FirstOrDefault();
        if (host is null)
        {
            host = new ToastHost
            {
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(16),
            };
            layer.Children.Add(host);
        }

        host.Items.Add(toast);

        var stay = duration
                   ?? (toast.ActionContent is null ? DefaultDuration : ActionDuration);

        DispatcherTimer.RunOnce(() => host.Items.Remove(toast), stay, DispatcherPriority.Background);
    }

    protected override Type StyleKeyOverride => typeof(ToastHost);
}
