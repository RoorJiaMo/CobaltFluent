using Avalonia;

namespace Cobalt.Fluent.Controls;

/// <summary>
/// 标签拖拽的落点判定。
///
/// **纯函数，全部按屏幕坐标算。** 这样拆出来是因为真正的撕出/并回没法在无头环境里测——
/// 那需要真的开窗口、真的移动光标。而判定错了的后果（拖回来插错位置、明明在标签栏上却
/// 撕成了新窗口）恰恰是最需要钉住的部分。控件层只负责把矩形量出来喂进来。
/// </summary>
internal static class TabDrop
{
    /// <summary>
    /// 屏幕上这个点落在哪个标签栏里。返回下标，都不在时返回 -1（也就是要撕出去）。
    ///
    /// 从后往前找：窗口列表大致按 Z 序，重叠时应当命中在上面的那个。
    /// 从前往后找的话，被压在下面的窗口会把落点抢走——而操作员看到的是上面那个。
    /// </summary>
    public static int StripAt(PixelPoint point, IReadOnlyList<PixelRect> strips)
    {
        for (var i = strips.Count - 1; i >= 0; i--)
            if (strips[i].Contains(point))
                return i;

        return -1;
    }

    /// <summary>
    /// 在这条标签栏里应当插到第几个位置。
    ///
    /// 判据是**标签的中线**，不是标签的边界：光标越过中线才算换位。用边界的话，
    /// 拖到两个标签交界处时插入点会在两个值之间来回跳，落地位置取决于最后一帧
    /// 光标停在哪一侧——手抖一像素结果就不同。
    ///
    /// 返回值是插入下标，范围 [0, tabs.Count]（末尾追加时等于 Count）。
    /// </summary>
    public static int InsertIndexAt(PixelPoint point, IReadOnlyList<PixelRect> tabs)
    {
        for (var i = 0; i < tabs.Count; i++)
        {
            var tab = tabs[i];
            if (point.X < tab.X + tab.Width / 2)
                return i;
        }

        return tabs.Count;
    }

    /// <summary>
    /// 从旧位置搬到新位置之后，这一项实际落在第几个。
    ///
    /// 同一条标签栏内重排时，插入下标是**在原项还在列表里时**算出来的，
    /// 而移除原项会让它后面的所有下标前移一位。不减这一格的话，
    /// 「往右拖一格」会变成原地不动——看起来就是拖拽没生效。
    /// </summary>
    public static int NormalizeMoveIndex(int from, int insertAt)
    {
        if (from < 0) return insertAt;

        return insertAt > from ? insertAt - 1 : insertAt;
    }

    /// <summary>
    /// 撕出的窗口该摆在哪。
    ///
    /// 让标签停在光标下面它被抓住的那个相对位置，而不是让窗口左上角对齐光标——
    /// 后者会让窗口在松手瞬间「跳」一下，跳的距离正好是你抓握的偏移量。
    /// </summary>
    public static PixelPoint TearOutOrigin(PixelPoint pointer, PixelPoint grabOffset) =>
        new(pointer.X - grabOffset.X, pointer.Y - grabOffset.Y);
}
