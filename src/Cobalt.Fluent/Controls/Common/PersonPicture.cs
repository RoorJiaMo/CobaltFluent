using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Media;

namespace Cobalt.Fluent.Controls;

/// <summary>头像尺寸档。</summary>
public enum PersonPictureSize
{
    Small,
    Medium,
    Large,
}

/// <summary>
/// 头像。有图用图，没图用姓名首字缩写。
/// 中文名取姓（第一个字），英文名取首字母，最多两位。
/// </summary>
[PseudoClasses(":small", ":medium", ":large", ":neutral")]
public class PersonPicture : TemplatedControl
{
    public static readonly StyledProperty<string?> DisplayNameProperty =
        AvaloniaProperty.Register<PersonPicture, string?>(nameof(DisplayName));

    public string? DisplayName
    {
        get => GetValue(DisplayNameProperty);
        set => SetValue(DisplayNameProperty, value);
    }

    public static readonly StyledProperty<IImage?> SourceProperty =
        AvaloniaProperty.Register<PersonPicture, IImage?>(nameof(Source));

    public IImage? Source
    {
        get => GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    public static readonly StyledProperty<PersonPictureSize> SizeProperty =
        AvaloniaProperty.Register<PersonPicture, PersonPictureSize>(
            nameof(Size), PersonPictureSize.Medium);

    public PersonPictureSize Size
    {
        get => GetValue(SizeProperty);
        set => SetValue(SizeProperty, value);
    }

    /// <summary>中性配色。用于「未指派」「系统」这类非具体人。</summary>
    public static readonly StyledProperty<bool> IsNeutralProperty =
        AvaloniaProperty.Register<PersonPicture, bool>(nameof(IsNeutral));

    public bool IsNeutral
    {
        get => GetValue(IsNeutralProperty);
        set => SetValue(IsNeutralProperty, value);
    }

    private string _initials = "";

    public static readonly DirectProperty<PersonPicture, string> InitialsProperty =
        AvaloniaProperty.RegisterDirect<PersonPicture, string>(nameof(Initials), o => o._initials);

    /// <summary>姓名缩写。模板在没有图片时显示它。</summary>
    public string Initials
    {
        get => _initials;
        private set => SetAndRaise(InitialsProperty, ref _initials, value);
    }

    static PersonPicture()
    {
        DisplayNameProperty.Changed.AddClassHandler<PersonPicture>((x, _) => x.Refresh());
        SizeProperty.Changed.AddClassHandler<PersonPicture>((x, _) => x.Refresh());
        IsNeutralProperty.Changed.AddClassHandler<PersonPicture>((x, _) => x.Refresh());
    }

    public PersonPicture() => Refresh();

    private void Refresh()
    {
        PseudoClasses.Set(":small", Size == PersonPictureSize.Small);
        PseudoClasses.Set(":medium", Size == PersonPictureSize.Medium);
        PseudoClasses.Set(":large", Size == PersonPictureSize.Large);
        PseudoClasses.Set(":neutral", IsNeutral);

        Initials = ComputeInitials(DisplayName);
    }

    /// <summary>中文取姓（第一个字），拉丁字母取每段首字母，最多两位。</summary>
    internal static string ComputeInitials(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "";

        var trimmed = name.Trim();

        // CJK 统一表意文字：取第一个字就够了，取两个反而不像称呼
        if (trimmed[0] >= '一' && trimmed[0] <= '鿿')
            return trimmed[..1];

        var parts = trimmed.Split(new[] { ' ', '\t', '.', '-' }, StringSplitOptions.RemoveEmptyEntries);
        return parts.Length switch
        {
            0 => "",
            1 => parts[0][..Math.Min(2, parts[0].Length)].ToUpperInvariant(),
            _ => $"{parts[0][0]}{parts[^1][0]}".ToUpperInvariant(),
        };
    }
}
