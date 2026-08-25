using System.Globalization;

namespace Cobalt.Fluent;

/// <summary>
/// 简体中文。中文环境下 <see cref="CobaltStrings.Current"/> 默认就是它。
///
/// 措辞上有几处是刻意的，改的时候别改掉：
///
/// - <see cref="RangeInvalid"/> 说的是「量程配置错了」，不是「你输错了」。
///   这一档对应的是组态错误，用「超量程」的措辞等于把配置问题栽给操作员。
/// - <see cref="MatchesWithinPrecision"/> 不说「已生效」。回读值和下发值只在
///   显示精度内一致时，控件没有资格替设备说它生效了。
/// - <see cref="EStopCommandNotSent"/> 必须指向硬件急停。这一句出现的时候，
///   设备很可能还在动，而软件这条路已经证明不通了。
/// </summary>
public class CobaltStringsZhHans : CobaltStrings
{
    public override string DataStale => "数据过期";

    public override string AgeUnknown => "新鲜度未知";

    public override string InvalidReading => "无效";

    public override string InvalidDisplayFormat => "显示格式无效";

    public override string LastUpdated(TimeSpan ago) => $"最后更新 {Age(ago)}前";

    public override string Age(TimeSpan ago) => ago.TotalSeconds switch
    {
        < 60 => $"{(int)ago.TotalSeconds} 秒",
        < 3600 => $"{(int)ago.TotalMinutes} 分",
        _ => $"{(int)ago.TotalHours} 小时",
    };

    public override string DeviationAtDisconnect(string signedDelta) =>
        $" · 断开时偏差 {signedDelta}";

    public override string Target(string setpoint) => $"目标 {setpoint}";

    public override string TargetWithDeviation(string setpoint, string signedDelta) =>
        $"目标 {setpoint} · 偏差 {signedDelta}";

    public override string TargetWithoutDeviationWatch(string setpoint, double tolerance) =>
        $"目标 {setpoint} · 偏差监视不可用（容差 {tolerance}）";

    public override string RangeInvalid => "量程无效，禁止下发";

    public override string OutOfRange(string minimum, string maximum) =>
        $"超量程 {minimum}–{maximum}";

    public override string Applied => "已生效";

    public override string MatchesWithinPrecision => "显示精度内一致";

    public override string PendingWrite => "待下发";

    public override string ReadOnlyState => "只读";

    public override string Writing => "写入中";

    public override string WriteFailed => "下发失败";

    public override string Apply => "下发";

    public override string Revert => "撤销";

    public override string ColumnParameter => "参数";

    public override string ColumnActual => "读值";

    public override string ColumnSetpoint => "设定";

    public override string ColumnUnit => "单位";

    public override string ColumnState => "状态";

    public override string Connected => "已连接";

    public override string Degraded => "通信不稳";

    public override string Disconnected => "通信中断";

    public override string PollRate(double hz) =>
        $"轮询 {hz.ToString("0.#", CultureInfo.CurrentCulture)} Hz";

    public override string NotANumber => "无法解析";

    public override string BelowMinimum(string minimum) => $"低于下限 {minimum}";

    public override string AboveMaximum(string maximum) => $"高于上限 {maximum}";

    public override string PageInfo(int totalItems, int currentPage, int pageCount) =>
        $"共 {totalItems.ToString("N0", CultureInfo.CurrentCulture)} 条 · 第 {currentPage} / {pageCount} 页";

    public override string PageInfoWithoutTotal(int currentPage, int pageCount) =>
        $"第 {currentPage} / {pageCount} 页";

    public override string AdditionalAlarms(int count) => $"另有 {count} 条同类报警";

    public override string Acknowledge => "确认";

    public override string Details => "详情";

    public override string Acknowledged => "已确认";

    public override string Unacknowledged => "未确认";

    public override string EStopReady => "就绪";

    public override string EStopEngaged => "已触发 · 需复位";

    public override string EStopCommandNotSent => "急停指令未下发 · 立即使用硬件急停";

    public override string Cancel => "取消";

    public override string GotIt => "知道了";

    public override string On => "开";

    public override string Off => "关";

    public override string HasUpdates => "有更新";

    public override string PaginationName => "分页";

    public override string NavigationName => "导航";

    public override string LegendName => "图例";

    public override string TrendChartName => "趋势图";

    public override string BarChartName => "柱状图";

    public override string SparklineName => "迷你趋势线";

    public override string TrendName => "趋势";

    public override string SeriesCount(int count) => $"{count} 条曲线";

    public override string CategoryCount(int count) => $"{count} 个分类";

    public override string TrackballAt(int index) => $"轨迹球位于第 {index} 点";

    public override string DeviatingFromSetpoint => "偏离设定值";

    public override string ReadingInvalid => "读值无效";

    public override string NoData => "无数据";

    public override string HeartbeatName => "通信心跳";

    public override string RangeHelp(double minimum, double maximum) =>
        $"量程 {minimum} – {maximum}";

    public override string StopCommandNotSent => "停止指令未下发";

    public override string EngageCommandNotSent => "急停指令未下发";

    public override string DeviceStatusName => "设备状态";

    public override string NumericKeypadName => "数字键盘";

    public override string CanCommit => "可提交";

    public override string CannotCommit => "不可提交";

    public override string Jogging => "点动中";

    public override string Idle => "空闲";

    public override string Engaged => "已锁定";

    public override string Ready => "就绪";
}
