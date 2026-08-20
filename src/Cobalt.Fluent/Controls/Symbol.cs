namespace Cobalt.Fluent.Controls;

/// <summary>
/// 图标名。全库用到的 36 个字形，全部画成矢量路径。
///
/// 为什么不直接用字体：目标平台里有嵌入式 Linux（RK3568 那类），
/// 上面没有 Segoe Fluent Icons，用字体会渲染成豆腐块。
/// 所以本库的图标全部是矢量路径，跨平台像素一致，也不用往安装包里塞字体。
/// 手上确实有那套字体的话，用 <see cref="SymbolIcon.Glyph"/> 直接给码位。
/// </summary>
public enum Symbol
{
    None = 0,
    ChevronDown,
    ChevronUp,
    ChevronLeft,
    ChevronRight,
    Add,
    Subtract,
    Cancel,
    More,
    Settings,
    Stop,
    EmergencyStop,
    Filter,
    Search,
    Refresh,
    Blocked,
    Checkbox,
    CheckMark,
    Save,
    Play,
    Pause,
    Contact,
    Calendar,
    Warning,
    Home,
    Download,
    Document,
    Sort,
    Folder,
    Speed,
    Completed,
    Info,
    Tune,
    Pulse,
    Diagnostic,
    Error,
    GlobalNav,
    Brightness,
}
