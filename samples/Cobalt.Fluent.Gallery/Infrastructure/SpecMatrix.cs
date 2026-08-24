using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;

namespace Cobalt.Fluent.Gallery.Infrastructure;

/// <summary>
/// 状态矩阵 —— 把 rest / :pointerover / :pressed / :disabled / :focus-visible 并排冻结铺开。
///
/// 这是展柜的「规格」那一层：伪类直接设进样品，样品因此不响应操作。
/// 活样品一次只能看到一个态，当不了规格。
///
/// 关键在于它不画假图：每个格子造一个真控件，然后对它调
/// <c>((IPseudoClasses)c.Classes).Set(":pointerover", true)</c>。
/// 也就是说矩阵里显示的，就是 ControlTheme 里真正写的那条规则——写错了立刻露馅。
///
/// 用法（XAML）：
/// <code>
/// &lt;g:SpecMatrix States="rest,pointerover,pressed,disabled"&gt;
///   &lt;g:SpecMatrix.Sample&gt;
///     &lt;DataTemplate&gt;&lt;Button Content="按钮" /&gt;&lt;/DataTemplate&gt;
///   &lt;/g:SpecMatrix.Sample&gt;
/// &lt;/g:SpecMatrix&gt;
/// </code>
/// </summary>
public sealed class SpecMatrix : Decorator
{
    private static readonly (string Key, string Label, string? Pseudo)[] Known =
    [
        ("rest",        "rest",           null),
        ("pointerover", ":pointerover",   ":pointerover"),
        ("pressed",     ":pressed",       ":pressed"),
        ("checked",     ":checked",       ":checked"),
        ("indeterminate", ":indeterminate", ":indeterminate"),
        ("selected",    ":selected",      ":selected"),
        ("expanded",    ":expanded",      ":expanded"),
        ("error",       ":error",         ":error"),
        ("disabled",    ":disabled",      null),   // 走 IsEnabled=false，比强设伪类更真
        ("focus",       ":focus-visible", null),   // 走 FocusRingHost，见该类注释
    ];

    public static readonly StyledProperty<IDataTemplate?> SampleProperty =
        AvaloniaProperty.Register<SpecMatrix, IDataTemplate?>(nameof(Sample));

    /// <summary>样品模板。每个格子 Build 一次，五个格子是五个独立实例。</summary>
    public IDataTemplate? Sample
    {
        get => GetValue(SampleProperty);
        set => SetValue(SampleProperty, value);
    }

    public static readonly StyledProperty<string> StatesProperty =
        AvaloniaProperty.Register<SpecMatrix, string>(
            nameof(States), "rest,pointerover,pressed,disabled,focus");

    /// <summary>逗号分隔，取值见 <see cref="Known"/>。顺序即展示顺序。</summary>
    public string States
    {
        get => GetValue(StatesProperty);
        set => SetValue(StatesProperty, value);
    }

    public static readonly StyledProperty<double> CellSpacingProperty =
        AvaloniaProperty.Register<SpecMatrix, double>(nameof(CellSpacing), 24d);

    public double CellSpacing
    {
        get => GetValue(CellSpacingProperty);
        set => SetValue(CellSpacingProperty, value);
    }

    static SpecMatrix()
    {
        SampleProperty.Changed.AddClassHandler<SpecMatrix>((x, _) => x.Rebuild());
        StatesProperty.Changed.AddClassHandler<SpecMatrix>((x, _) => x.Rebuild());
    }

    private void Rebuild()
    {
        if (Sample is not { } template)
        {
            Child = null;
            return;
        }

        var panel = new WrapPanel { Orientation = Orientation.Horizontal };

        foreach (var key in States.Split(',', StringSplitOptions.RemoveEmptyEntries
                                              | StringSplitOptions.TrimEntries))
        {
            // 表里没有的当自定义伪类处理 —— 第 7 组那些控件（:jogging / :dirty /
            // :engaged …）都是自己定义的伪类，写法和内置伪类完全一样。
            var spec = Array.Find(Known, k => k.Key == key);
            if (spec.Key is null)
                spec = (key, ":" + key, ":" + key);

            Control sample;
            try
            {
                sample = template.Build(null) ?? new TextBlock { Text = "模板返回 null" };
            }
            catch (Exception ex)
            {
                sample = new TextBlock { Text = "构造失败: " + ex.Message, TextWrapping = TextWrapping.Wrap };
            }

            if (spec.Key == "disabled")
                sample.IsEnabled = false;
            else if (spec.Pseudo is { } pseudo)
                ((IPseudoClasses)sample.Classes).Set(pseudo, true);

            Control cell = spec.Key == "focus" ? new FocusRingHost { Child = sample } : sample;
            panel.Children.Add(Cell(cell, spec.Label));
        }

        Child = panel;
    }

    private Control Cell(Control content, string label) => new StackPanel
    {
        Spacing = 6,
        Margin = new Thickness(0, 0, CellSpacing, 16),
        Children =
        {
            new Border
            {
                Child = content,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center,
            },
            new TextBlock
            {
                Text = label,
                FontSize = 12,
                HorizontalAlignment = HorizontalAlignment.Left,
                // 用属性索引器而不是 new Binding { Path = nameof(...) }：后者按名字
                // 反射解析，TrimMode=full / NativeAOT 下目标成员可能被裁掉，
                // 绑定在运行时静默失效——标签照样显示，只是颜色不跟主题走了。
                [!TextBlock.ForegroundProperty] = this[!LabelBrushProperty],
            },
        },
    };

    /// <summary>标签色。由 Gallery.axaml 里的样式绑到 TextFillColorTertiaryBrush。</summary>
    public static readonly StyledProperty<IBrush?> LabelBrushProperty =
        AvaloniaProperty.Register<SpecMatrix, IBrush?>(nameof(LabelBrush));

    public IBrush? LabelBrush
    {
        get => GetValue(LabelBrushProperty);
        set => SetValue(LabelBrushProperty, value);
    }
}
