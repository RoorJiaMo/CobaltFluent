using Avalonia;
using Avalonia.Data.Converters;

namespace Cobalt.Fluent.Controls;

/// <summary>模板里用得到的几个 <see cref="Symbol"/> 转换器。</summary>
public static class SymbolConverters
{
    /// <summary>是否设了图标。<see cref="Symbol.None"/> 时返回 false，模板据此收起图标槽。</summary>
    public static readonly IValueConverter IsNotNone =
        new FuncValueConverter<Symbol, bool>(s => s != Symbol.None);

    /// <summary>线宽的一半，用作圆环的内缩边距——描边是骑在路径上的，不缩会被裁掉一半。</summary>
    public static readonly IValueConverter HalfThickness =
        new FuncValueConverter<double, Thickness>(t => new Thickness(t / 2));
}
