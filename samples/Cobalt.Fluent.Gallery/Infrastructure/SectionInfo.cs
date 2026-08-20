using Avalonia.Controls;

namespace Cobalt.Fluent.Gallery.Infrastructure;

/// <summary>目录里的一项。Group 是左侧目录的 11 组分类。</summary>
public sealed record SectionInfo(string Group, string Title, Func<Control> Create)
{
    public override string ToString() => Title;
}
