using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Cobalt.Fluent.Gallery.Pages;

public partial class TextBoxPage : UserControl
{
    public TextBoxPage()
    {
        AvaloniaXamlLoader.Load(this);

        // :error 是真伪类，正常由 DataValidation 驱动。
        // 这里为了把校验态静态铺出来，直接强设一次。
        var box = this.FindControl<TextBox>("ErrorBox")!;
        ((IPseudoClasses)box.Classes).Set(":error", true);
    }
}
