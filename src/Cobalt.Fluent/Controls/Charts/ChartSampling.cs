namespace Cobalt.Fluent.Controls;

/// <summary>
/// 曲线抽稀。
///
/// 一个 8 小时班次按 1 Hz 采样是 28800 点/通道。全量画进 <c>StreamGeometry</c> 的话，
/// 800 px 宽的绘图区上每个像素列摊到 36 个顶点——多出来的 35 个在屏幕上落在同一列，
/// 画不出第三个 y 值，只是让每一帧多传一遍。RK3568 这类板子上这是致命的。
/// </summary>
internal static class ChartSampling
{
    /// <summary>
    /// 有效值的上下界。返回参与统计的有效点个数。
    ///
    /// 不能用 <c>values.Min()</c> / <c>values.Max()</c>：数组里有一个 NaN，
    /// 两者都返回 NaN，接着 <c>range = max - min</c> 是 NaN，而 <c>range &lt;= 0</c>
    /// 又放它过去（NaN 和任何数比较都为 false），于是每个点的 y 都算成 NaN——
    /// 一个坏点让整条线消失，而旁边几十条看着都正常。
    /// </summary>
    public static int FiniteExtent(IReadOnlyList<double> values, out double min, out double max)
    {
        min = double.PositiveInfinity;
        max = double.NegativeInfinity;
        var count = 0;

        foreach (var v in values)
        {
            if (!double.IsFinite(v)) continue;
            if (v < min) min = v;
            if (v > max) max = v;
            count++;
        }

        return count;
    }

    /// <summary>
    /// 一条曲线在屏幕上实际占据的像素列数。
    ///
    /// 必须按**这条曲线自己的跨度**算，不是整个绘图区的宽度。长短不一的曲线
    /// 共用同一条时间轴（<paramref name="maxCount"/> 是最长的那条），短曲线只占
    /// 左边一小段：1000 点的曲线在 28800 点的时间轴上只有 27 px，
    /// 按整幅 800 列去分桶的话它根本不会被抽稀，一个像素列里照样挤 37 个点。
    /// </summary>
    public static int ColumnsFor(int seriesCount, int maxCount, double plotWidth)
    {
        if (maxCount < 2 || seriesCount < 2 || !double.IsFinite(plotWidth)) return 1;

        var span = plotWidth * (seriesCount - 1) / (maxCount - 1.0);
        return span >= 1 ? (int)span : 1;
    }

    /// <summary>
    /// min/max 抽稀：每个像素列只保留最小值和最大值。
    ///
    /// <b>不能用「每 N 个取一个」。</b>趋势图上真正要看的就是尖峰——8 小时里持续
    /// 3 个采样点的一次超调，隔 36 取 1 有 92% 的概率整个丢掉，画出来是一条平静的线，
    /// 而操作员正是为了看这个才调出趋势图。min/max 抽稀保留的是**包络**：
    /// 垂直方向上的极值一个不少，渲染结果和全量画在像素级上一致。
    ///
    /// 写进 <paramref name="into"/> 的下标是**原数组下标**——十字线、末值标注
    /// 都还挂在原数组上，抽稀纯粹是渲染层的事。
    ///
    /// 值为 <see cref="double.NaN"/> 的条目表示断点（通信中断），调用方应当在那里
    /// 断开图形而不是连过去：一条从中断前直连到恢复后的直线，看上去是一段平稳过程。
    /// </summary>
    /// <param name="values">原始采样。</param>
    /// <param name="columns">这条曲线在屏幕上实际占据的像素列数。</param>
    /// <param name="into">输出缓冲。调用方复用它，避免每帧分配。</param>
    public static void Decimate(
        IReadOnlyList<double> values, int columns, List<(int Index, double Value)> into)
    {
        into.Clear();
        if (values.Count == 0) return;

        columns = Math.Max(1, columns);

        // 每像素列至多两个点。低于这个密度分桶没有意义，原样抄过去更快也更准。
        if (values.Count <= columns * 2)
        {
            for (var i = 0; i < values.Count; i++)
            {
                var v = values[i];
                into.Add((i, double.IsFinite(v) ? v : double.NaN));
            }

            return;
        }

        for (var col = 0; col < columns; col++)
        {
            // 用 long 算边界。28800 点 × 4000 列在 int 上会溢出成负数，
            // 那样桶边界会往回跑，曲线画成一团。
            var from = (int)((long)col * values.Count / columns);
            var to = (int)((long)(col + 1) * values.Count / columns);
            if (to <= from) continue;

            int minIndex = -1, maxIndex = -1;
            double min = 0, max = 0;

            for (var i = from; i < to; i++)
            {
                var v = values[i];

                // 非有限值不参与极值。Math.Min(x, NaN) 返回 NaN——放进来的话
                // 一个坏点会把整个像素列的包络抹成 NaN。
                if (!double.IsFinite(v)) continue;

                if (minIndex < 0 || v < min) { min = v; minIndex = i; }
                if (maxIndex < 0 || v > max) { max = v; maxIndex = i; }
            }

            // 整桶都没有有效值 = 这一像素列确实断了。
            //
            // 注意判据是「整桶都没有」而不是「桶里有一个 NaN」：36:1 的压缩下，
            // 一次单点丢包在屏幕上是亚像素的，为它断线的话，1% 丢包率的链路上
            // 整张图会变成一排虚线——那比不画更误导。要看单点丢包得放大时间轴。
            if (minIndex < 0)
            {
                into.Add((from, double.NaN));
                continue;
            }

            // 按出现先后写入。反过来会给曲线引入原始数据里没有的折返方向。
            if (minIndex <= maxIndex)
            {
                into.Add((minIndex, min));
                if (maxIndex != minIndex) into.Add((maxIndex, max));
            }
            else
            {
                into.Add((maxIndex, max));
                into.Add((minIndex, min));
            }
        }
    }
}
