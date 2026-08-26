using System.Globalization;
using System.Reflection;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Cobalt.Fluent.Controls;
using Xunit;

namespace Cobalt.Fluent.Tests;

/// <summary>
/// 本地化。
///
/// 这一批要挡住的是**「新写了一句话，忘了翻译」**——那种缺陷编译得过、测试也绿，
/// 只有当那一档状态真的发生在一台英文机器上时才露出来，而它露出来的方式是
/// 屏幕上突然冒出一句中文。逐句人工核对是挡不住的，所以这里靠反射把
/// <see cref="CobaltStrings"/> 的每一个成员都过一遍。
/// </summary>
public class LocalizationTests
{

    // ---- 一、每一句都得有中文 ------------------------------------------------

    [Fact]
    public void 每个成员在中文实现里都被覆写()
    {
        var missing = Members()
            .Where(m => Resolve(m, typeof(CobaltStringsZhHans)).DeclaringType != typeof(CobaltStringsZhHans))
            .Select(m => m.Name)
            .ToArray();

        Assert.True(missing.Length == 0,
            $"CobaltStringsZhHans 漏了这些成员，英文会漏到中文界面上：{string.Join("、", missing)}");
    }

    [Fact]
    public void 中文实现给出的确实是中文()
    {
        // 只覆写不翻译（原样抄英文回来）比不覆写更难发现：上一条测试会绿。
        var en = new CobaltStrings();
        var zh = new CobaltStringsZhHans();

        var untranslated = new List<string>();
        foreach (var m in Members())
        {
            var a = Invoke(m, en);
            var b = Invoke(m, zh);
            if (a == b) untranslated.Add($"{m.Name}={a}");
        }

        Assert.True(untranslated.Count == 0,
            $"这些成员中英文一模一样，多半是覆写了但没翻：{string.Join("、", untranslated)}");
    }

    [Fact]
    public void 英文实现里没有中文()
    {
        var en = new CobaltStrings();
        var leaked = Members()
            .Select(m => (m.Name, Text: Invoke(m, en)))
            .Where(x => x.Text.Any(c => c >= '一' && c <= '鿿'))
            .Select(x => $"{x.Name}={x.Text}")
            .ToArray();

        Assert.True(leaked.Length == 0, $"默认（英文）实现里混进了中文：{string.Join("、", leaked)}");
    }

    // ---- 二、按语言挑实现 ----------------------------------------------------

    [Theory]
    [InlineData("zh-CN", true)]
    [InlineData("zh-Hans", true)]
    [InlineData("zh-TW", true)]
    [InlineData("zh", true)]
    [InlineData("en-US", false)]
    [InlineData("de-DE", false)]
    [InlineData("ja-JP", false)]
    [InlineData("", false)]        // 固定区域性
    public void 按界面语言挑实现(string culture, bool chinese)
    {
        // 判据是 TwoLetterISOLanguageName 而不是精确匹配 zh-CN——
        // zh-Hans / zh-SG / zh-TW 都该拿到中文，掉进英文的话繁体用户会拿到一屏英文。
        var picked = CobaltStrings.ForCulture(new CultureInfo(culture));

        Assert.Equal(chinese, picked is CobaltStringsZhHans);
    }

    [Fact]
    public void 赋空值被拒绝()
    {
        // 允许赋 null 的话，下一次读 Current 会按当时的 locale 重新挑一套，
        // 语言在运行中途自己变了——比抛异常难查得多。
        Assert.Throws<ArgumentNullException>(() => CobaltStrings.Current = null!);
    }

    // ---- 三、换语言之后，已经显示出来的文字要跟着变 --------------------------

    [AvaloniaFact]
    public void 换语言会重算已经显示出来的文字()
    {
        var readout = Mount(new Readout
        {
            Value = 85.4,
            Format = "F1",
            StaleAfter = TimeSpan.FromSeconds(1),
            LastUpdated = DateTime.Now.AddMinutes(-5),
        });

        Assert.Equal(new CobaltStringsZhHans().DataStale, readout.StaleText);

        using (Language(new CobaltStrings()))
        {
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(new CobaltStrings().DataStale, readout.StaleText);
        }
    }

    [AvaloniaFact]
    public void 摘下来的控件不再跟着换语言()
    {
        // 静态事件持有实例引用是典型的泄漏源。一屏几十个 Readout 反复建销，
        // 忘了退订就一个都回收不掉——而这个泄漏在功能上完全看不出来。
        var readout = new Readout
        {
            Value = 1,
            StaleAfter = TimeSpan.FromSeconds(1),
            LastUpdated = DateTime.Now.AddMinutes(-5),
        };
        var window = new Window { Width = 400, Height = 200, Content = readout };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        window.Content = null;
        Dispatcher.UIThread.RunJobs();

        var before = readout.StaleText;

        using (Language(new CobaltStrings()))
        {
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(before, readout.StaleText);
        }
    }

    [AvaloniaFact]
    public void 重复挂载不会叠加订阅()
    {
        // Avalonia 在换父、窗口重排时会多次触发挂载。不挡住的话同一个实例
        // 订阅多次，换一次语言重算多次——功能上看不出来，CPU 上看得出来。
        //
        // 这条直接测 StringsWatcher 而不是测控件：叠加订阅在控件那一侧
        // **从外面观察不到**——多跑一次 Refresh 算出的是同一个值，
        // 而 Avalonia 对相等的新值不触发 PropertyChanged。守卫在哪一层，
        // 就在哪一层测。
        // 标 AvaloniaFact 而不是 Fact：换语言会触发 CurrentChanged，
        // 前面测试里遗留的、仍挂在树上的控件会跟着重算文本，
        // 而重算要用到字体栈——在会话外做这件事会撞上 'fonts:SystemFonts'。
        var runs = 0;
        var watcher = new StringsWatcher(() => runs++);

        watcher.Attach();
        watcher.Attach();
        watcher.Attach();

        using (Language(new CobaltStrings())) { }

        watcher.Detach();

        // 换进去一次、还原一次，各触发一轮；订阅要是叠了三份，这里会是 6。
        Assert.Equal(2, runs);
    }

    [AvaloniaFact]
    public void 退订之后不再收到通知()
    {
        var runs = 0;
        var watcher = new StringsWatcher(() => runs++);

        watcher.Attach();
        watcher.Detach();
        watcher.Detach();          // 重复退订不该炸

        using (Language(new CobaltStrings())) { }

        Assert.Equal(0, runs);
    }

    // ---- 四、使用方写的字优先 ------------------------------------------------

    [AvaloniaFact]
    public void 显式设置压过语言默认值()
    {
        var banner = Mount(new AlarmBanner { Title = "超温", AcknowledgeContent = "OK" });

        Assert.Equal("OK", banner.AcknowledgeContent);
    }

    [AvaloniaFact]
    public void 没设置时用当前语言的默认值()
    {
        var banner = Mount(new AlarmBanner { Title = "超温" });

        Assert.Equal(new CobaltStringsZhHans().Acknowledge, banner.AcknowledgeContent);
    }

    // ---- 五、自动化：机器可读的那半不许本地化 --------------------------------

    [AvaloniaFact]
    public void 自动化的Value不随语言变而ItemStatus随()
    {
        // 这条边界是整套设计的支点：脚本断言 Value，人读 ItemStatus。
        // Value 一旦本地化，使用方的验收脚本会在界面翻译的那天全红。
        var indicator = Mount(new StatusIndicator { State = DeviceState.Fault });
        var jog = Mount(new JogButton { Content = "X 轴" });

        var indicatorValue = Provider<IValueProvider>(indicator).Value;
        var jogValue = Provider<IValueProvider>(jog).Value;
        var jogStatusZh = Peer(jog).GetItemStatus();

        using (Language(new CobaltStrings()))
        {
            Assert.Equal(indicatorValue, Provider<IValueProvider>(indicator).Value);
            Assert.Equal(jogValue, Provider<IValueProvider>(jog).Value);
            Assert.NotEqual(jogStatusZh, Peer(jog).GetItemStatus());
        }
    }

    // ---- 辅助 ----------------------------------------------------------------

    /// <summary>CobaltStrings 上所有公开的、返回字符串的虚成员。</summary>
    private static IEnumerable<MemberInfo> Members()
    {
        const BindingFlags Flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly;

        foreach (var p in typeof(CobaltStrings).GetProperties(Flags))
            if (p.PropertyType == typeof(string) && p.GetMethod is { IsVirtual: true })
                yield return p;

        foreach (var m in typeof(CobaltStrings).GetMethods(Flags))
            if (m.ReturnType == typeof(string) && m.IsVirtual && !m.IsSpecialName)
                yield return m;
    }

    private static MemberInfo Resolve(MemberInfo m, Type type) => m switch
    {
        PropertyInfo p => type.GetProperty(p.Name)!.GetMethod!,
        MethodInfo mi => type.GetMethod(mi.Name, mi.GetParameters().Select(x => x.ParameterType).ToArray())!,
        _ => throw new ArgumentOutOfRangeException(nameof(m)),
    };

    /// <summary>调一次，拿到实际文字。带参数的喂一组固定样本，结果要能复现。</summary>
    private static string Invoke(MemberInfo m, CobaltStrings target) => m switch
    {
        PropertyInfo p => (string)p.GetValue(target)!,
        MethodInfo mi => (string)mi.Invoke(target, mi.GetParameters().Select(Sample).ToArray())!,
        _ => throw new ArgumentOutOfRangeException(nameof(m)),
    };

    private static object Sample(ParameterInfo p) => p.ParameterType switch
    {
        var t when t == typeof(string) => "42",
        var t when t == typeof(int) => 7,
        var t when t == typeof(double) => 1.5,
        var t when t == typeof(TimeSpan) => TimeSpan.FromSeconds(30),

        // 贴靠布局的两个枚举取「归不了类」那一档：Custom / 未定义值走的是
        // 兜底分支，而兜底分支正是最容易混进英文或中文硬编码的地方。
        var t when t == typeof(SnapLayoutKind) => (SnapLayoutKind)(-1),
        var t when t == typeof(SnapZoneKind) => SnapZoneKind.Custom,
        var t when t == typeof(SnapZone) => new SnapZone(0.25, 0, 0.5, 1),

        _ => throw new NotSupportedException($"{p.ParameterType.Name} 还没给样本"),
    };

    /// <summary>
    /// 临时换语言，出作用域自动还原。
    ///
    /// 还原**必须发生在 Avalonia 会话里**：换语言会触发 CurrentChanged，
    /// 订阅中的控件随即重算文本，而重算要用到字体栈——在会话外做这件事
    /// 会撞上 'fonts:SystemFonts' 找不到。
    /// </summary>
    private static IDisposable Language(CobaltStrings strings)
    {
        var saved = CobaltStrings.Current;
        CobaltStrings.Current = strings;
        return new Restore(saved);
    }

    private sealed class Restore(CobaltStrings saved) : IDisposable
    {
        public void Dispose() => CobaltStrings.Current = saved;
    }

    private static AutomationPeer Peer(Control c) => ControlAutomationPeer.CreatePeerForElement(c);

    private static T Provider<T>(Control c) where T : class
    {
        var provider = Peer(c).GetProvider<T>();
        Assert.NotNull(provider);
        return provider!;
    }

    private static T Mount<T>(T control) where T : Control
    {
        var window = new Window { Width = 900, Height = 400, Content = new StackPanel { Children = { control } } };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        window.Measure(new Size(900, 400));
        window.Arrange(new Rect(0, 0, 900, 400));
        Dispatcher.UIThread.RunJobs();
        return control;
    }
}
