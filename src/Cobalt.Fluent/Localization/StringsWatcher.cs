namespace Cobalt.Fluent;

/// <summary>
/// 让控件在 <see cref="CobaltStrings.Current"/> 换掉之后重算已经显示出来的文字。
///
/// <b>静态事件持有实例引用是典型的泄漏源。</b>一屏几十个 <c>Readout</c> 反复建销，
/// 忘了退订就一个都回收不掉——而这个泄漏在功能上完全看不出来，只是内存一路涨。
/// 所以订阅和退订都收在这里，控件只需在挂载/卸载时各调一次。
/// </summary>
internal sealed class StringsWatcher(Action onChanged)
{
    private EventHandler? _handler;

    /// <summary>挂到视觉树上时调。重复调用不会叠加订阅。</summary>
    public void Attach()
    {
        // 重入保护：Avalonia 在窗口重排、控件换父等场景下会多次触发挂载，
        // 不挡住的话同一个实例会订阅多次，换一次语言重算多次。
        if (_handler is not null) return;

        _handler = (_, _) => onChanged();
        CobaltStrings.CurrentChanged += _handler;
    }

    /// <summary>从视觉树上摘掉时调。</summary>
    public void Detach()
    {
        if (_handler is null) return;

        CobaltStrings.CurrentChanged -= _handler;
        _handler = null;
    }
}
