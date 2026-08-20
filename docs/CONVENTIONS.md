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

可用的 `Symbol` 值见 `src/Cobalt.Fluent/Controls/Symbol.cs`（36 个）。
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
