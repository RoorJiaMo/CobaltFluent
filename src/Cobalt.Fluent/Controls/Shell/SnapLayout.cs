namespace Cobalt.Fluent.Controls;

/// <summary>布局种类。名字要本地化，用枚举而不是字符串，拼错在编译期就露出来。</summary>
public enum SnapLayoutKind
{
    /// <summary>左右等分。</summary>
    Halves,

    /// <summary>左 2/3 + 右 1/3。主视图配一条侧栏，是上位机最常用的一种。</summary>
    WideLeft,

    /// <summary>三栏等分。工作区够宽才给。</summary>
    Thirds,

    /// <summary>四宫格。</summary>
    Quadrants,

    /// <summary>左半屏 + 右侧上下两块。</summary>
    LeftAndStack,

    /// <summary>窄-宽-窄三栏（25/50/25）。带鱼屏才给。</summary>
    WideCenter,

    /// <summary>上下等分。竖屏用。</summary>
    StackedHalves,

    /// <summary>上半屏 + 下方左右两块。竖屏用。</summary>
    TopAndSplit,
}

/// <summary>
/// 一套贴靠布局：几块分区拼满整个工作区。
///
/// 布局表是本库自己的，不问系统要——这正是「框架内部自己做贴靠」的意思：
/// Windows 10、Linux、macOS、嵌入式面板上拿到的是同一套布局、同一个几何。
/// </summary>
public sealed class SnapLayout
{
    private SnapLayout(SnapLayoutKind kind, params SnapZone[] zones)
    {
        Kind = kind;
        Zones = zones;
    }

    public SnapLayoutKind Kind { get; }

    /// <summary>分区，按阅读顺序（左到右、上到下）。</summary>
    public IReadOnlyList<SnapZone> Zones { get; }

    private const double Half = 1d / 2;
    private const double Third = 1d / 3;
    private const double TwoThirds = 2d / 3;
    private const double Quarter = 1d / 4;

    public static readonly SnapLayout Halves = new(
        SnapLayoutKind.Halves,
        new SnapZone(0, 0, Half, 1),
        new SnapZone(Half, 0, Half, 1));

    public static readonly SnapLayout WideLeft = new(
        SnapLayoutKind.WideLeft,
        new SnapZone(0, 0, TwoThirds, 1),
        new SnapZone(TwoThirds, 0, Third, 1));

    public static readonly SnapLayout Thirds = new(
        SnapLayoutKind.Thirds,
        new SnapZone(0, 0, Third, 1),
        new SnapZone(Third, 0, Third, 1),
        new SnapZone(TwoThirds, 0, Third, 1));

    public static readonly SnapLayout Quadrants = new(
        SnapLayoutKind.Quadrants,
        new SnapZone(0, 0, Half, Half),
        new SnapZone(Half, 0, Half, Half),
        new SnapZone(0, Half, Half, Half),
        new SnapZone(Half, Half, Half, Half));

    public static readonly SnapLayout LeftAndStack = new(
        SnapLayoutKind.LeftAndStack,
        new SnapZone(0, 0, Half, 1),
        new SnapZone(Half, 0, Half, Half),
        new SnapZone(Half, Half, Half, Half));

    public static readonly SnapLayout WideCenter = new(
        SnapLayoutKind.WideCenter,
        new SnapZone(0, 0, Quarter, 1),
        new SnapZone(Quarter, 0, Half, 1),
        new SnapZone(Quarter + Half, 0, Quarter, 1));

    public static readonly SnapLayout StackedHalves = new(
        SnapLayoutKind.StackedHalves,
        new SnapZone(0, 0, 1, Half),
        new SnapZone(0, Half, 1, Half));

    public static readonly SnapLayout TopAndSplit = new(
        SnapLayoutKind.TopAndSplit,
        new SnapZone(0, 0, 1, Half),
        new SnapZone(0, Half, Half, Half),
        new SnapZone(Half, Half, Half, Half));

    /// <summary>横屏上恒定提供的四套。三栏与宽屏三栏按宽度另加，见 <see cref="SnapGeometry.LayoutsFor"/>。</summary>
    public static readonly IReadOnlyList<SnapLayout> LandscapeSet =
        [Halves, WideLeft, Quadrants, LeftAndStack];

    /// <summary>
    /// 竖屏上的三套。竖屏不给分栏——把一块 1080×1920 的面板左右切开，
    /// 每半只有 540 宽，摆不下一个正常的表单。
    /// </summary>
    public static readonly IReadOnlyList<SnapLayout> PortraitSet =
        [StackedHalves, TopAndSplit, Quadrants];

    /// <summary>全部布局。展柜和测试用。</summary>
    public static readonly IReadOnlyList<SnapLayout> All =
        [Halves, WideLeft, Thirds, Quadrants, LeftAndStack, WideCenter, StackedHalves, TopAndSplit];
}
