using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;

namespace Cobalt.Fluent.Controls;

/// <summary>
/// 加载占位条。比转圈好——它预示了内容的形状。
///
/// 嵌入式注意：微光是常驻动画，一屏十几条会有开销。
/// RK3568 这类设备上把 <see cref="IsShimmerEnabled"/> 关掉（变静态灰条），
/// 或者只给前几行开。
/// </summary>
[PseudoClasses(":shimmer")]
public class Skeleton : TemplatedControl
{
    public static readonly StyledProperty<bool> IsShimmerEnabledProperty =
        AvaloniaProperty.Register<Skeleton, bool>(nameof(IsShimmerEnabled), true);

    public bool IsShimmerEnabled
    {
        get => GetValue(IsShimmerEnabledProperty);
        set => SetValue(IsShimmerEnabledProperty, value);
    }

    static Skeleton()
    {
        IsShimmerEnabledProperty.Changed.AddClassHandler<Skeleton>(
            (x, e) => x.PseudoClasses.Set(":shimmer", e.NewValue is true));
    }

    public Skeleton() => PseudoClasses.Set(":shimmer", true);
}
