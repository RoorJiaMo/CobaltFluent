# 写法约定

写任何一个控件之前先读这一页，然后照着 `src/Cobalt.Fluent/Themes/Controls/Button.axaml` 的写法来。

## 分层

| 层 | 文件 | 规矩 |
|---|---|---|
| 变量层 | `Themes/Tokens.axaml` | **由 `tools/gen_tokens.py` 从 `tools/palette.json` 生成，不要手改。** 改颜色改 palette.json 再重跑 |
| 几何层 | `Themes/Metrics.axaml` | 圆角、尺寸、间距、动效时长 |
| 字体层 | `Themes/Typography.axaml` | 字阶 ControlTheme、字体栈 |
| 共用片段 | `Themes/Shared.axaml` | 聚焦框模板等 |
| 控件层 | `Themes/Controls/*.axaml` | 一个控件（或一组紧密相关的控件）一个文件 |

控件层**零裸色值**：颜色一律 `{DynamicResource ...Brush}`。分层的全部意义就在这条上——
换主题色、做高对比度、给不同产线定制外观，只动变量层，控件层一行不改。

## 资源键速查

键名沿用 WinUI 的约定，和 FluentAvalonia 通用。

| 用途 | 键 |
|---|---|
| 文字 | `TextFillColorPrimaryBrush` / `SecondaryBrush` / `TertiaryBrush` / `DisabledBrush` |
| accent 上的文字 | `TextOnAccentFillColorPrimaryBrush` / `SecondaryBrush`；链接文字 `AccentTextFillColorPrimaryBrush` |
| 控件填充 | `ControlFillColorDefaultBrush` / `SecondaryBrush`(hover) / `TertiaryBrush`(pressed) / `DisabledBrush` |
| 输入框激活底 | `ControlFillColorInputActiveBrush` |
| 强填充 | `ControlStrongFillColorDefaultBrush` / `DisabledBrush`；实色底 `ControlSolidFillColorDefaultBrush` |
| 备用填充 | `ControlAltFillColorSecondaryBrush` / `TertiaryBrush` / `QuarternaryBrush` |
| 透明底控件 | `SubtleFillColorSecondaryBrush`(hover) / `TertiaryBrush`(pressed) |
| 强调 | `AccentFillColorDefaultBrush` / `SecondaryBrush` / `TertiaryBrush` / `DisabledBrush` |
| 描边 | `ControlStrokeColorDefaultBrush` / `SecondaryBrush` · `ControlStrongStrokeColorDefaultBrush` |
| accent 上的描边 | `ControlStrokeColorOnAccentDefaultBrush` / `SecondaryBrush` |
| 卡片 | `CardBackgroundFillColorDefaultBrush` / `SecondaryBrush` · `CardStrokeColorDefaultBrush` |
| 层 | `LayerFillColorDefaultBrush` / `AltBrush` · `SolidBackgroundFillColorBaseBrush` / `SecondaryBrush` / `TertiaryBrush` / `QuarternaryBrush` |
| 弹出层 | `AcrylicBackgroundFillColorDefaultBrush` · `SurfaceStrokeColorFlyoutBrush` · 遮罩 `SmokeFillColorDefaultBrush` |
| 常驻面板描边 | `SurfaceStrokeColorDefaultBrush` · 分隔线 `DividerStrokeColorDefaultBrush` |
| 聚焦框 | `FocusStrokeColorOuterBrush` / `InnerBrush` |
| 状态 | `SystemFillColorSuccessBrush` / `CautionBrush` / `CriticalBrush` / `NeutralBrush`，各配一支 `...BackgroundBrush` |
| 安全（第 7 组） | `SafetyRedBrush` / `SafetyRedHighBrush` / `SafetyYellowBrush` / `TextOnSafetyFillColorPrimaryBrush` |
| 图表 | `ChartSeries1Brush` .. `ChartSeries8Brush` · `ChartGridLineBrush` · `ChartAxisLineBrush` · `ChartBandBrush` |

几何与时长：`ControlCornerRadius`(4) · `OverlayCornerRadius`(8) · `ControlHeight`(32) ·
`ControlMinWidth`(64) · `ControlPadding`(11,5,11,6) · `Space1..8` ·
`ControlFasterAnimationDuration`(83ms) · `ControlFastAnimationDuration`(167ms) ·
`ControlNormalAnimationDuration`(250ms) · `ControlSlowAnimationDuration`(333ms)

底边渐变描边（Win11 控件的签名细节）：`ControlElevationBorderBrush` ·
`AccentControlElevationBorderBrush` · `TextControlElevationBorderBrush`。
`:pressed` 时换成实色 `ControlStrokeColorDefaultBrush`，视觉上「按平」。

## 伪类

| 伪类 | 谁来打 |
|---|---|
| `:pointerover` · `:pressed` · `:disabled` · `:focus-visible` | Avalonia 内置 |
| `:checked` · `:indeterminate` · `:selected` · `:expanded` | Avalonia 内置 |
| `:error` | 本库，校验不通过 |
| `:dirty` · `:jogging` · `:engaged` 等第 7 组语义 | 本库，`PseudoClasses.Set(":dirty", v)` |
| `:rangestart` · `:inrange` · `:rangeend` | 本库，`RangeCalendar` 按选中区间的首尾补出 |

内置有的别另起炉灶：Avalonia 已经给了 `:pointerover`，就不要再自己维护一个 `IsHovered`
属性，两套状态迟早对不上。自定义的一律在类上用 `[PseudoClasses(...)]` 标出来，
否则下一个人只能翻实现才知道有哪些态。

## 几个反复踩的坑

- **XML 注释里不能出现 `--`。** 中文注释里想写破折号用「——」（U+2014），
  不要用两个 ASCII 减号，否则 `AVLN1001: An XML comment cannot contain '--'`。
- **`TemplateBinding` 没有 `StringFormat`。** 要格式化就在 C# 里算好一个只读属性再绑。
- **选择器匹配不到不会报编译错。** 改了模板里元素的类型（比如 TextBlock 换成
  `fc:SymbolIcon`），对应的 `/template/ TextBlock#PART_X` 选择器会静默失效。改类型必改选择器。
- **`ControlTheme` 里不允许出现子代/后代选择器**（`^ > X`、`^ X`）。
  要按位置区分子元素，就由容器在代码里给子元素打类，再用 `^.类名` 选。
- **`PseudoClasses.Set(...)` 需要 `using Avalonia.Controls;`**（它是 `IPseudoClasses` 上的扩展方法），
  少了这一行编译报「IPseudoClasses does not contain a definition for Set」。
- **`Grid.IsSharedSizeScope` 跨模板不生效。** 多行表格对齐要么用固定列宽，
  要么让所有行共用同一个 Grid。
- **`ResourceDictionary` 里放不下裸 `<Style>`**（报 `AVLN3000`）。状态样式要塞进对应的
  `ControlTheme` 里写成 `^:pseudo` / `^.class`。
- **子类的 `StyleKeyOverride` 决定用哪套 ControlTheme，而且不回退基类。**
  `ToggleSplitButton.StyleKeyOverride` 返回 `typeof(SplitButton)`，所以给
  `ToggleSplitButton` 单独写的 ControlTheme 是死代码，得并进 `SplitButton` 那套。
- **内置控件的伪类不够用时，写个子类把差的补上，别另起一套模板。**
  `Calendar` 在 `SelectionMode=SingleRange` 下只会把区间里每一天都标成 `:selected`，
  拿不到首尾。`fc:RangeCalendar` 的做法是继承 `Calendar`、`StyleKeyOverride` 返回
  `typeof(Calendar)`（这样沿用同一套 ControlTheme），在 `LayoutUpdated` 里按
  `SelectedDates` 的首尾给 `CalendarDayButton` 补三个伪类。
  `CalendarDayButton.DataContext` 就是那天的 `DateTime`。
- **生成器按目录白名单扫源码，新开一个子目录不登记就静默漏掉。**
  `tools/gen_api_docs.py` 的 `GROUPS` 是白名单，`Controls/` 下新建的目录不加进去，
  整批控件不会进 `docs/CONTROLS.md`，而且不报错。现在脚本会对账并直接失败，CI 也跟着查。

## 校验过的不变量（写代码时不要破坏）

- 控件层零裸色值，颜色全部走 `DynamicResource`
- 圆角只出现 8 / 4 / 0 三个值，相接处不圆（SplitButton 左半 `4,0,0,4`、右半 `0,4,4,0`，**写死不要绑 `ControlCornerRadius`**，否则改全局圆角时接缝会裂）
- 阴影只出现在 ToolTip / Flyout / MenuFlyout / ContentDialog / TeachingTip 五个悬浮层，页面内元素一律靠描边
- 字重只有 400 / 600，无 Bold、无斜体
- 全部读数、数值列、日期数字要等宽：`FontFeatures="{StaticResource TabularNumbers}"`
- 安全色不跟随主题：急停和 alarm 级报警用 `SafetyRedBrush`，**不要用 `SystemFillColorCriticalBrush`**（后者深色主题下是浅粉 `#FF99A4`）
- 动效只动 transform 和 opacity；嵌入式 GPU 上位移动画掉帧比没动画更伤

## 图标：不要用图标字体

Fluent 那套图标通常是 Segoe Fluent Icons 的码位（`&#xE70D;` 这类）。**本库不走字体** ——
嵌入式 Linux（RK3568 那类）上没有这套字体，会渲染成豆腐块，而那正是目标平台之一。
所以图标全部是矢量路径：

```xml
xmlns:fc="using:Cobalt.Fluent.Controls"
...
<fc:SymbolIcon Symbol="ChevronDown" FontSize="10"
               Foreground="{DynamicResource TextFillColorSecondaryBrush}" />
```

可用的 `Symbol` 值见 `src/Cobalt.Fluent/Controls/Symbol.cs`（38 个）。
手上确实有那套字体的话，`SymbolIcon.UseGlyphFont="True"` 切回字体渲染。
模板里给它起了名字的话，选择器要写 `fc|SymbolIcon#PART_Chevron`，
**不是** `TextBlock#PART_Chevron` —— 选择器匹配不到不会报编译错，只是静默不生效。

## 写法约定

- 内置控件用 `<ControlTheme x:Key="{x:Type Button}" TargetType="Button">`，
  自定义控件用 `<ControlTheme x:Key="{x:Type fc:Readout}" TargetType="fc:Readout">`
- 状态用嵌套 `<Style Selector="^:pointerover">`，不要写成平级的独立 Style
- 变体用 Classes：`<Style Selector="^.accent">`
- 模板里给可被样式选中的部件起名：`<Border x:Name="PART_Root">`，
  选择器写 `^:pointerover /template/ Border#PART_Root`
- 聚焦框统一用 `<Setter Property="FocusAdorner" Value="{StaticResource FluentFocusAdorner}" />`
  （圆形控件用 `FluentFocusAdornerRound`），不要各写各的

## 校验

```bash
tools/check.sh --only 你的文件.axaml    # 只编译你的文件，不受别人半成品影响
tools/check.sh                          # 整个控件库
tools/check.sh --gallery                # 连展柜
python3 tools/gen_theme_index.py        # 新增控件层文件后重建合并列表
dotnet run --project tools/Cobalt.Fluent.Shots -- artifacts/shots Button both
                                        # 渲染成 PNG，肉眼验收
```

Avalonia 的 XAML 编译器会报 `AVLN2000`（属性不存在）、`AVLN2200`（值转不过去）这类错，
所以**编译通过 ≠ 好看，但编译不过一定是错的**。写完必须编译。

## 自动化对等体

每个直接继承 `Control` / `TemplatedControl` 的控件都要覆写 `OnCreateAutomationPeer`
（`automation-peer` 检查会拦），对等体统一放在 `src/Cobalt.Fluent/Automation/`。

这件事在工业场景里不是可选的：**HMI 的验收普遍用 UI Automation 驱动界面跑回归**，
测试台要能读出读数、参数状态、急停有没有锁上。没有对等体时这些控件在 Inspect 里
只是一团没有名字的 `Custom` 矩形，使用方的脚本根本抓不到——而界面上完全看不出来。

三条原则：

**1. `Value` 只放机器可读的那个量，判读上下文放 `ItemStatus`。**

测试台读到 `"85.4"` 无从判断这是实时值还是五分钟前的死值，而这两种情况在屏幕上
也只差一个灰度。过期、偏离、写入中这些一律进 `ItemStatus`，混进 `Value` 会让断言
没法写。同理，枚举状态给枚举名而不是显示文字——显示文字会随本地化变，
断言挂在它上面的话，界面一翻译脚本就全红。

```csharp
public string Value => Control.DisplayValue ?? "";           // "85.4"
protected override string? GetItemTypeCore() => Control.Unit; // "°C"，不拼进 Value
protected override string? GetItemStatusCore() => PeerText.Join(
    Control.StaleText, Control.Classes.Contains(":deviating") ? "偏离设定值" : null);
```

**2. 危险动作不通过自动化模式暴露成一次调用。**

急停复位默认要求长按，那道门存在的理由就是防误碰。让自动化客户端一次
`Toggle()` 就把自锁解掉，等于给它开了一条现场操作员都没有的近路。
`EStopButtonAutomationPeer.Toggle()` 因此只触发、不解锁，已锁定时抛
`ElementNotEnabledException`；需要复位请显式调 `EStopButton.Reset()`。

宿主的拒收闸同样不能绕过：`AlarmBannerAutomationPeer.Invoke()` 走
`Acknowledge()`，宿主 `CanExecute` 为 false 时照样确认不了。

**3. 装饰性元素主动退出自动化树。**

占位骨架、分隔线、图标进树只是噪音，会把真正要读的东西淹掉。这类控件返回
`DecorativeAutomationPeer`（`NoneAutomationPeer` 的别名），`IsControlElement` 与
`IsContentElement` 都答否，客户端遍历时整个跳过。条件性的也一样——关掉的
`InfoBar`、没有名字的 `PersonPicture` 都靠 `IsControlElementCore()` 退出。

**活动区域**：凭空出现的元素（报警、通知、浮层）必须覆写 `GetLiveSettingCore`，
否则读屏软件与自动化客户端都要靠轮询才发现它，而报警的价值全在于第一时间被察觉。
`AlarmSeverity.Alarm` / `Fault` 用 `Assertive`（打断当前朗读），`Warning` 用
`Polite`，`Info` 不播报。

## 高对比度变体

调色板有四列：`light` / `dark` / `highContrastLight` / `highContrastDark`。
`tools/gen_tokens.py` 把四列各摊成一个 `ResourceDictionary`，
内置两套用 `x:Key="Light"` / `"Dark"`，自定义两套必须走
`x:Key="{x:Static fc:CobaltFluentTheme.HighContrastDark}"`——
Avalonia 的 `ThemeVariant` 类型转换器只支持内置变体，写成字符串会在**加载主题
那一刻**抛 `NotSupportedException`，不是编译期。

**四列每一列都必须写全。** Avalonia 在本变体里找不到键时会静默回落到
`InheritVariant`，高对比度变体漏一个键，回落到的就是原来那个半透明值，
屏幕上还看着挺正常——而「保证对比度」正是这两套变体存在的全部理由。
`gen_tokens.py` 的 `load()` 把缺列当错误挡在生成之前。

改颜色的流程不变：改 `tools/palette.json` → 跑 `python3 tools/gen_tokens.py`
→ 跑 `python3 tools/audit.py`。审计会把对照表里每一对在四套变体下逐一核对
（高对比度下正文抬到 AAA 7:1），并拒绝高对比度里出现的任何半透明值。

## 静默失效审计

```bash
python3 tools/audit.py          # 有问题时退出码 1，CI 第一步就跑它
python3 tools/audit.py --list   # 列出各项检查覆盖的历史缺陷
```

编译器和测试都有一类共同的盲区：**代码写了、编译过了、测试也绿了，但那段判定
在真实路径上整个不成立，而且失效方向朝着「一切正常」**。这一类缺陷在本库里
成片出现过——一轮对抗性审查确认了 55 条，没有一条是编译器或既有测试能发现的。

`tools/audit.py` 把其中可机械检测的模式固化了下来，12 项检查，每一项都对应
一个真实发生过的缺陷：

| 检查 | 它抓的那个历史缺陷 |
|---|---|
| `parts` | `ParameterRow` 找 `PART_Revert`，而模板里根本没有——撤销功能在界面上不可达 |
| `pseudo-declared` | 置位了却没在 `[PseudoClasses]` 里声明，文档与对照表随之脱节 |
| `pseudo-styled` | 伪类加了、控件也正确置位，主题里没有对应样式——开关在界面上毫无变化 |
| `resources` | 资源键不存在时 Avalonia 静默回落，颜色尺寸悄悄变成默认值 |
| `transform-setter` | `Setter Property="ScaleTransform.ScaleX"` 只有 KeyFrame 认，普通 Setter 里静默失败 |
| `animation-override` | 动画优先级高于 Setter，`^:running` 上的动画盖掉了 `^:running[IsPulseEnabled=False]` 的静态值 |
| `keydown-modifiers` | `OnKeyDown` 不查修饰键：`Ctrl+Space` 让轴运动、`Ctrl+Enter` 触发急停与解锁 |
| `timer-cleanup` | `EStopButton` 不做卸载清理，长按定时器在控件离开可视树后仍会解锁急停 |
| `wallclock` | 墙钟 `DateTime.Now` 相减：系统时间回拨多久，就有多久所有读数被判成新鲜 |
| `unread-property` | `JogButton.RequiresConfirm` 声明、文档、对照表俱全，全库无人读取——危险轴上开了等于没开 |
| `automation-peer` | 25 个直接继承 `Control` / `TemplatedControl` 的控件没有对等体，在 UI Automation 里只是一团没有名字的 `Custom` 矩形 |
| `contrast` | 安全色一组五处不达标：已触发急停的白字 3.42、降级态那圈安全黄压红 2.69、「停止指令没能下发」的白字压黄 1.72、`:engagefailed` 的黄环压浅底 1.55。同时守住高对比度变体的 AAA 门槛与「不许半透明」 |

**新加检查的门槛**：能举出一个它本该抓到的历史缺陷，且在当前代码上零误报。
写完拿历史版本验一遍是必须的——抓不到它本该抓的 bug，这检查就是摆设：

```bash
TMP=$(mktemp -d); git archive <修复前的提交> | tar -x -C "$TMP"
mkdir -p "$TMP/tools" && cp tools/audit.py tools/audit-allow.txt "$TMP/tools/"
python3 "$TMP/tools/audit.py"      # 应当报出那个缺陷
```

### 对比度对照表为什么是手写的

`contrast` 检查里的前景/背景对（`CONTRAST_PAIRS`）是逐条手写的，不是从主题 XAML
里自动抽的。试过自动抽——「同一个 Style 里同时设了 `Background` 和 `Foreground`」
能抽出 107 对、报出 34 条不达标，但其中只有 1 条是真的：

- **14 条是 disabled 态。** WCAG 1.4.3 明确豁免失效控件，本来就不该按 4.5 要求。
- **8 条是 accent 按钮的 pressed 态。** 瞬时反馈，且用的是 WinUI 自己的值。
- **2 条是 Slider。** 它的 `Foreground` 是轨道填充，不是文字——抽取器不知道
  「Foreground 未必是文字」。
- **2 条是 `SafetyOverlay*`。** 那是压在安全红横幅上的半透明白，抽取器按页面底色
  去合成，算的是一个屏幕上不存在的组合。

也就是说噪音比 33:1。真问题会淹掉，而淹掉的后果就是这检查被人关掉。手写对照表
意味着每一条都能说出「这两个颜色为什么会同时出现在屏幕上、看不清会怎样」——
新增控件时把新的组合加进去，比维护一个抽取器的例外表便宜得多。

（那一条真的是：危险主操作按钮用 `SystemFillColorCriticalBrush` 做底压白字，
深色主题下是浅粉压白，2.03:1。已改用 `SafetyRedBrush`。）

确属可接受的例外登记到 `tools/audit-allow.txt`，**每条必须写明理由**。
例外按「检查名 路径 稳定标识」匹配而不是按行号——行号一挪就失配、告警重新冒出来，
那正是 lint 工具被人关掉的典型原因。
