using Avalonia.Media;

namespace Cobalt.Fluent.Controls;

/// <summary>
/// 图标路径表。全部画在 16×16 的格子里，和 Segoe Fluent Icons 的设计尺寸一致。
///
/// Fluent 的图标是线型的，所以绝大多数用描边（<c>Stroked = true</c>，1.2px 圆头圆角），
/// 只有 Play / Pause / Stop / More 这类实心字形用填充。
/// </summary>
internal static class SymbolGeometry
{
    /// <summary>路径 + 画法。Stroked 为 true 时描边，false 时填充。</summary>
    internal readonly record struct Entry(string Path, bool Stroked, string Glyph);

    private static readonly Dictionary<Symbol, Geometry> Cache = new();

    /// <summary>Glyph 是对应的 Segoe Fluent Icons 码位，留给装了那套字体的用户。</summary>
    internal static readonly IReadOnlyDictionary<Symbol, Entry> Table = new Dictionary<Symbol, Entry>
    {
        [Symbol.ChevronDown]  = new("M3.5,6 L8,10.5 L12.5,6", true, "\uE70D"),
        [Symbol.ChevronUp]    = new("M3.5,10 L8,5.5 L12.5,10", true, "\uE70E"),
        [Symbol.ChevronLeft]  = new("M10,3.5 L5.5,8 L10,12.5", true, "\uE76B"),
        [Symbol.ChevronRight] = new("M6,3.5 L10.5,8 L6,12.5", true, "\uE76C"),

        [Symbol.Add]      = new("M8,3 L8,13 M3,8 L13,8", true, "\uE710"),
        [Symbol.Subtract] = new("M3,8 L13,8", true, "\uE738"),
        [Symbol.Cancel]   = new("M4,4 L12,12 M12,4 L4,12", true, "\uE8BB"),

        [Symbol.More] = new(
            "M2.6,8 A1.2,1.2 0 1 0 5,8 A1.2,1.2 0 1 0 2.6,8 Z " +
            "M6.8,8 A1.2,1.2 0 1 0 9.2,8 A1.2,1.2 0 1 0 6.8,8 Z " +
            "M11,8 A1.2,1.2 0 1 0 13.4,8 A1.2,1.2 0 1 0 11,8 Z", false, "\uE712"),

        [Symbol.Settings] = new(
            "M8,5.6 A2.4,2.4 0 1 0 8,10.4 A2.4,2.4 0 1 0 8,5.6 Z " +
            "M8,1.6 L8,3.2 M8,12.8 L8,14.4 M1.6,8 L3.2,8 M12.8,8 L14.4,8 " +
            "M3.5,3.5 L4.6,4.6 M11.4,11.4 L12.5,12.5 " +
            "M12.5,3.5 L11.4,4.6 M4.6,11.4 L3.5,12.5", true, "\uE713"),

        // 停止是实心方块；急停是 ISO 13850 的八角形
        [Symbol.Stop] = new("M4,4 H12 V12 H4 Z", false, "\uE71A"),
        // 八角形 = STOP 牌。占满 16 格，小尺寸下才看得出是八角不是圆。
        [Symbol.EmergencyStop] = new(
            "M5.2,0.8 H10.8 L15.2,5.2 V10.8 L10.8,15.2 H5.2 L0.8,10.8 V5.2 Z", false, "\uE711"),

        [Symbol.Filter] = new("M2.5,3.5 H13.5 L9.2,8.4 V13 L6.8,11.6 V8.4 Z", true, "\uE71C"),
        [Symbol.Search] = new(
            "M7,2.6 A4.4,4.4 0 1 0 7,11.4 A4.4,4.4 0 1 0 7,2.6 Z M10.3,10.3 L13.6,13.6",
            true, "\uE721"),
        [Symbol.Refresh] = new(
            "M13.2,8 A5.2,5.2 0 1 1 11.3,3.97 M11.6,1.6 L11.6,4.3 L8.9,4.3",
            true, "\uE72C"),
        [Symbol.Blocked] = new(
            "M8,2.4 A5.6,5.6 0 1 0 8,13.6 A5.6,5.6 0 1 0 8,2.4 Z M4.2,4.2 L11.8,11.8",
            true, "\uE733"),

        [Symbol.Checkbox]  = new("M3,3 H13 V13 H3 Z", true, "\uE739"),
        [Symbol.CheckMark] = new("M3.2,8.4 L6.4,11.6 L12.8,4.6", true, "\uE73E"),

        [Symbol.Save] = new(
            "M3,3 H11.2 L13,4.8 V13 H3 Z M5.4,3 V6.6 H10.2 V3 M5,9 H11 V13 H5 Z",
            true, "\uE74E"),

        [Symbol.Play]  = new("M5,3.2 L12.5,8 L5,12.8 Z", false, "\uE768"),
        [Symbol.Pause] = new("M5,3.4 H7 V12.6 H5 Z M9,3.4 H11 V12.6 H9 Z", false, "\uE769"),

        [Symbol.Contact] = new(
            "M8,3 A2.4,2.4 0 1 0 8,7.8 A2.4,2.4 0 1 0 8,3 Z " +
            "M3,13.4 C3,10.6 5.2,9.2 8,9.2 C10.8,9.2 13,10.6 13,13.4", true, "\uE77B"),

        [Symbol.Calendar] = new(
            "M2.8,4.2 H13.2 V13 H2.8 Z M2.8,7 H13.2 M5.4,2.6 V5.2 M10.6,2.6 V5.2",
            true, "\uE787"),

        // 惊叹号那一点用零长线段配圆头画出来
        [Symbol.Warning] = new(
            "M8,2.6 L14.2,13.2 H1.8 Z M8,6.4 V9.8 M8,11.4 L8,11.45", true, "\uE7BA"),

        [Symbol.Home] = new(
            "M2.6,7.8 L8,2.8 L13.4,7.8 M4.4,6.6 V13.2 H11.6 V6.6", true, "\uE80F"),
        [Symbol.Download] = new(
            "M8,2.8 V10.6 M4.8,7.4 L8,10.6 L11.2,7.4 M2.8,13.2 H13.2", true, "\uE896"),
        [Symbol.Document] = new(
            "M4,2.4 H9.6 L12,4.8 V13.6 H4 Z M9.4,2.4 V5 H12", true, "\uE8A5"),
        [Symbol.Sort] = new("M3,4.2 H13 M3,8 H10 M3,11.8 H7", true, "\uE8B7"),
        [Symbol.Folder] = new("M2.4,4.4 H6.6 L8,6 H13.6 V12.6 H2.4 Z", true, "\uE8F1"),

        [Symbol.Speed] = new(
            "M8,4 A5,5 0 1 0 8,14 A5,5 0 1 0 8,4 Z M8,6.6 V9.2 L9.9,10.4 M6.2,2.2 H9.8",
            true, "\uE916"),
        [Symbol.Completed] = new(
            "M8,2.4 A5.6,5.6 0 1 0 8,13.6 A5.6,5.6 0 1 0 8,2.4 Z M5.4,8.2 L7.2,10 L10.6,6.2",
            true, "\uE930"),
        [Symbol.Info] = new(
            "M8,2.4 A5.6,5.6 0 1 0 8,13.6 A5.6,5.6 0 1 0 8,2.4 Z M8,7.4 V11.4 M8,4.9 L8,4.95",
            true, "\uE946"),
        [Symbol.Tune] = new(
            "M3,4.6 H13 M3,11.4 H13 M6.4,3.2 V6 M10,10 V12.8", true, "\uE9D9"),
        [Symbol.Pulse] = new(
            "M2,8 H5 L6.6,4.4 L9.2,11.6 L10.8,8 H14", true, "\uE9E9"),
        [Symbol.Diagnostic] = new(
            "M2.6,3.4 H13.4 V11 H2.6 Z M4.6,7.4 H6.1 L7.2,5.2 L8.8,9.6 L9.7,7.4 H11.4 " +
            "M6.4,13 H9.6", true, "\uE9F5"),
        [Symbol.Error] = new(
            "M8,2.4 A5.6,5.6 0 1 0 8,13.6 A5.6,5.6 0 1 0 8,2.4 Z " +
            "M5.9,5.9 L10.1,10.1 M10.1,5.9 L5.9,10.1", true, "\uEA39"),

        [Symbol.GlobalNav] = new("M2.8,4.4 H13.2 M2.8,8 H13.2 M2.8,11.6 H13.2", true, "\uE700"),
        [Symbol.Brightness] = new(
            "M8,5.4 A2.6,2.6 0 1 0 8,10.6 A2.6,2.6 0 1 0 8,5.4 Z " +
            "M8,1.8 V3.2 M8,12.8 V14.2 M1.8,8 H3.2 M12.8,8 H14.2 " +
            "M3.6,3.6 L4.6,4.6 M11.4,11.4 L12.4,12.4 " +
            "M12.4,3.6 L11.4,4.6 M4.6,11.4 L3.6,12.4", true, "\uE706"),

        // 退格：左指五边形轮廓 + 里面一个叉。尖端指向删除方向，
        // 小尺寸下光靠箭头分不出是「返回」还是「删除」，叉是必需的。
        [Symbol.Backspace] = new(
            "M6,3.4 H13.4 V12.6 H6 L2.2,8 Z " +
            "M8.4,6.2 L11.6,9.8 M11.6,6.2 L8.4,9.8", true, "\uE750"),

        // 窗口按钮三件套。Windows 的标题栏字形是 10×10 的，所以这三个只占中间
        // 10 格，边上留 3 格 —— 和系统标题栏并排时大小才对得上。
        // 关闭按钮直接用 Cancel（它的码位本来就是 ChromeClose \uE8BB）。
        [Symbol.Minimize] = new("M3,8 H13", true, "\uE921"),
        [Symbol.Maximize] = new("M3,3 H13 V13 H3 Z", true, "\uE922"),
        // 还原：前面一个 8×8 的方块，后面错开 2 格露出一个「⌐」形。
        [Symbol.Restore] = new("M3,5 H11 V13 H3 Z M5,5 V3 H13 V11 H11", true, "\uE923"),
    };

    internal static Geometry? Get(Symbol symbol)
    {
        if (symbol == Symbol.None) return null;
        if (Cache.TryGetValue(symbol, out var cached)) return cached;
        if (!Table.TryGetValue(symbol, out var entry)) return null;

        var geometry = Geometry.Parse(entry.Path);
        Cache[symbol] = geometry;
        return geometry;
    }

    internal static bool IsStroked(Symbol symbol) =>
        Table.TryGetValue(symbol, out var e) && e.Stroked;

    internal static string? GlyphOf(Symbol symbol) =>
        Table.TryGetValue(symbol, out var e) ? e.Glyph : null;
}
