using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Cobalt.Fluent.Controls;

namespace Cobalt.Fluent.Gallery.Pages;

public partial class JogButtonPage : UserControl
{
    private DispatcherTimer? _ticker;
    private double _elapsed;

    public JogButtonPage()
    {
        AvaloniaXamlLoader.Load(this);

        var output = this.FindControl<TextBlock>("Output")!;

        foreach (var name in new[] { "Fwd", "Rev" })
        {
            var jog = this.FindControl<JogButton>(name)!;

            jog.JogStarted += (_, _) =>
            {
                _elapsed = 0;
                _ticker?.Stop();
                _ticker = new DispatcherTimer(
                    TimeSpan.FromMilliseconds(100), DispatcherPriority.Background, (_, _) =>
                    {
                        _elapsed += 0.1;
                        output.Text = $"▶ {jog.Content} 动作中… {_elapsed:F1} s";
                    });
                _ticker.Start();
                output.Text = $"▶ {jog.Content} 动作中… 0.0 s";
            };

            jog.JogStopped += (_, e) =>
            {
                _ticker?.Stop();
                _ticker = null;
                output.Text = $"■ 已停止 — {Describe(e.Reason)}";
            };
        }

        DetachedFromVisualTree += (_, _) => { _ticker?.Stop(); _ticker = null; };
    }

    private static string Describe(JogStopReason reason) => reason switch
    {
        JogStopReason.PointerReleased => "PointerReleased（正常松手）",
        JogStopReason.PointerCaptureLost => "PointerCaptureLost（指针捕获丢失）",
        JogStopReason.PointerExited => "PointerExited（指针离开控件）",
        JogStopReason.LostFocus => "LostFocus（控件失焦）",
        JogStopReason.KeyReleased => "KeyReleased（键盘松键）",
        JogStopReason.Detached => "Detached（控件被摘除或禁用）",
        JogStopReason.Watchdog => "Watchdog（看门狗超时 5 s 强制停止）",
        _ => reason.ToString(),
    };
}
