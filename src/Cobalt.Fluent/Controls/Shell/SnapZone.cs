using Avalonia;

namespace Cobalt.Fluent.Controls;

/// <summary>
/// 一块贴靠分区，用工作区的比例表示（0..1）。
///
/// 用比例而不是像素，是因为同一套布局要落到不同分辨率、不同缩放、不同显示器上。
/// 换算成像素是 <see cref="SnapGeometry.ZoneRect"/> 的事，那里才知道工作区有多大。
/// </summary>
/// <param name="X">左边界占工作区宽度的比例。</param>
/// <param name="Y">上边界占工作区高度的比例。</param>
/// <param name="Width">宽度占工作区宽度的比例。</param>
/// <param name="Height">高度占工作区高度的比例。</param>
public readonly record struct SnapZone(double X, double Y, double Width, double Height)
{
    // 比例是 1/3 这类除不尽的数，累加之后和 1 差个 1e-16 是常态。
    private const double Slack = 1e-6;

    /// <summary>
    /// 分区是否落在工作区之内、且有正的面积。
    ///
    /// 判定写成「不满足就是无效」而不是「满足就是有效」：NaN 参与的任何比较都是 false，
    /// 前者会把 NaN 判成无效，后者会把它判成有效然后一路传到取整那一步——
    /// <c>(int)double.NaN</c> 是 0，于是分区静默地缩到左上角。
    /// </summary>
    public bool IsValid =>
        !(!(Width > 0) || !(Height > 0)
          || !(X >= 0) || !(Y >= 0)
          || !(X + Width <= 1 + Slack) || !(Y + Height <= 1 + Slack));

    /// <summary>右边界比例。</summary>
    public double Right => X + Width;

    /// <summary>下边界比例。</summary>
    public double Bottom => Y + Height;
}

/// <summary>
/// 分区的语义分类。给朗读名用——读屏软件念「区域 2/4」没有意义，
/// 念「右上四分之一」操作员才知道按下去窗口会去哪。
/// </summary>
public enum SnapZoneKind
{
    /// <summary>不在下面这些常见形状里。朗读名退回百分比描述。</summary>
    Custom = 0,

    LeftHalf,
    RightHalf,
    TopHalf,
    BottomHalf,

    LeftThird,
    CenterThird,
    RightThird,

    LeftTwoThirds,
    RightTwoThirds,

    /// <summary>宽屏三栏里中间那块，占一半宽、通栏高。</summary>
    CenterHalf,

    LeftQuarter,
    RightQuarter,

    TopLeftQuarter,
    TopRightQuarter,
    BottomLeftQuarter,
    BottomRightQuarter,
}
