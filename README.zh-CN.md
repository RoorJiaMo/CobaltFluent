<div align="center">

<img src="docs/images/logo.png" width="128" alt="Cobalt.Fluent" />

# Cobalt.Fluent

**Windows 11 Fluent 设计语言的 Avalonia 实现**

62 个控件 · 11 个分组 · 明暗双主题 · 零第三方依赖 · 内置工业 HMI 控件组

[![build](https://github.com/RoorJiaMo/CobaltFluent/actions/workflows/build.yml/badge.svg)](https://github.com/RoorJiaMo/CobaltFluent/actions/workflows/build.yml)
[![license](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)
[![Avalonia](https://img.shields.io/badge/Avalonia-11.3-8b5cf6.svg)](https://avaloniaui.net)
[![.NET](https://img.shields.io/badge/.NET-8.0-512bd4.svg)](https://dotnet.microsoft.com)

[English](README.md) · **简体中文**

<img src="docs/images/gallery-dark.png" width="880" alt="Cobalt.Fluent 展柜 —— 工业 HMI 读数控件" />

</div>

---

## 概述

Cobalt.Fluent 在 Avalonia 11.3 上完整实现 Windows 11 Fluent 的视觉规格，并在此基础上提供一组面向过程控制界面的专用控件。

通用控件库在工业上位机场景中普遍存在以下不足：数值刷新引起布局抖动、报警依赖闪烁、点动按钮的停止条件不完备、参数下发后回显输入值而非设备实际接受值。这些问题直接影响操作安全，而非单纯的视觉问题。本库将相应的安全语义实现在控件内部，并以回归测试固定。

<table>
<tr>
<td width="25%" valign="top">

### 零第三方依赖

仅引用 Avalonia 本体、`Avalonia.Themes.Fluent` 与 `Avalonia.Controls.DataGrid` 三个框架自带包。图表自绘，图标为内置矢量路径。

</td>
<td width="25%" valign="top">

### 规格可验收

多数控件提供状态矩阵；CI 对 49 个章节逐页无头渲染共 98 张截图，渲染失败即构建失败。

</td>
<td width="25%" valign="top">

### 面向嵌入式

不依赖 Segoe Fluent Icons 字体；动效仅作用于 transform、opacity 与画刷过渡，不涉及布局；亚克力与微光效果均可关闭。

</td>
<td width="25%" valign="top">

### 安全语义有测试

急停自锁、点动多重停止、事件驱动心跳、读数过期保值等安全行为均由回归测试覆盖。

</td>
</tr>
</table>

---

## 快速开始

```bash
dotnet add package Cobalt.Fluent
```

```xml
<!-- App.axaml -->
<Application xmlns="https://github.com/avaloniaui"
             xmlns:fc="using:Cobalt.Fluent">
  <Application.Styles>
    <fc:CobaltFluentTheme />
  </Application.Styles>
</Application>
```

主题切换使用 `Application.Current.RequestedThemeVariant`（`Light` / `Dark` / `Default`）。
本库新增控件位于 `Cobalt.Fluent.Controls` 命名空间：

```xml
xmlns:fc="using:Cobalt.Fluent.Controls"
```

```xml
<fc:Readout Label="腔体温度" Value="85.5" Unit="°C"
            Setpoint="85.0" Tolerance="1.0" Size="Large" />

<fc:JogButton Content="X+ 点动" StopCommand="{Binding StopAxis}"
              WatchdogTimeout="0:0:0.5" />

<fc:TrendChart Series="{Binding Channels}" IsTrackballEnabled="True" />
```

---

## 展柜

展柜应用包含 49 个章节。控件章节由视觉规格说明、交互演示、资源键与伪类对照三部分组成，其中 22 个另附状态矩阵，将各伪类并排定格；另有 5 个总则性章节——设计基线、排版、图标清单，以及第 7、9 组的组内总则——为说明与参考材料，不含控件演示。

<table>
<tr>
<td width="50%"><img src="docs/images/gallery-light.png" alt="浅色主题 —— 趋势图" /></td>
<td width="50%"><img src="docs/images/gallery-dark.png" alt="深色主题 —— HMI 读数" /></td>
</tr>
<tr>
<td align="center"><sub>浅色主题 · 第 9 组 图表</sub></td>
<td align="center"><sub>深色主题 · 第 7 组 工业 HMI</sub></td>
</tr>
</table>

每一页均提供「本页源码」：当前页面的示例 XAML 与 C#，以及该章节控件在库中的 ControlTheme 与控件类实现，带行号与语法着色，支持一键复制。

<img src="docs/images/source-viewer.png" width="720" alt="源码查看器 —— 展示示例与 ControlTheme 原文" />

```bash
dotnet run --project samples/Cobalt.Fluent.Gallery
```

---

## 控件覆盖

11 个分组，桌面基准控件高度 32px。

<details>
<summary><b>完整清单（62 个控件）</b></summary>

<br>

| 组 | 控件 | 来源 |
|---|---|---|
| **2** 基础输入 | Button · ToggleButton · SplitButton · DropDownButton · HyperlinkButton · TextBox · NumberBox · ComboBox · CheckBox · RadioButton · ToggleSwitch · Slider | Avalonia 内置 + 本库 ControlTheme |
| **3** 容器 | Card · SettingsCard · SettingsGroup · Expander · TabControl · TabView · NavigationView | Card / SettingsCard / SettingsGroup / TabView / NavigationView 为本库实现 |
| **4** 集合 | ListBox · DataGrid · TreeView | ListBoxItem 重写模板（增加选中指示条） |
| **5** 反馈 | InfoBar · InfoBadge · ProgressBar · ProgressRing · ToolTip | InfoBar / InfoBadge / ProgressRing 为本库实现 |
| **6** 弹出 | Flyout · MenuFlyout · ContentDialog · TeachingTip · CommandBar | ContentDialog / TeachingTip / CommandBar 为本库实现 |
| **7** HMI 专用 | Readout · StatusIndicator · AlarmBanner · ParameterRow · JogButton · EStopButton · DeviceStatusBar · NumericKeypad | 全部为本库实现 |
| **8** 日期时间 | CalendarDatePicker · TimePicker · Calendar · RangeCalendar | 内置 + 本库 ControlTheme；RangeCalendar 为本库实现（补充区间端点伪类）。日期区间由两个 `CalendarDatePicker` 组合而成，并无 `DateRangePicker` 类型 |
| **9** 图表 | ChartFrame · TrendChart · Gauge · BarChart · Sparkline · ChartLegend | 全部为本库自绘实现 |
| **10** 表格增强 | DataGridToolbar · Pagination · EmptyState · Skeleton | 全部为本库实现 |
| **11** 常用补充 | AutoSuggestBox · BreadcrumbBar · SegmentedControl · Chip · Stepper · GridSplitter · Toast · PersonPicture | 除 AutoSuggestBox（即 Avalonia 内置 `AutoCompleteBox` 重做主题）与 GridSplitter 外，均为本库实现 |

完整 API 参见 [`docs/CONTROLS.md`](docs/CONTROLS.md)（77 个类型，由脚本从源码抽取）。

</details>

---

## 设计基线

以下四条规则贯穿全库。单个控件的画法存在多种合理选择；多个控件同屏时，只有统一的层级、圆角、阴影与字重才能维持可读的视觉秩序。

| 规则 | 内容 |
|---|---|
| **画面仅两层** | base layer（窗口底，承载导航与命令栏）与 content layer（内容区）。Card 是 content layer 内部的分区，不构成第三层。 |
| **圆角仅 8 / 4 / 0** | 8 用于弹出面板，4 用于控件，0 用于元素相接处。例外均为本就应当是圆形的形状——日历日期格、ToggleSwitch 轨道、StatusIndicator 与心跳圆点、Chip、PersonPicture、急停旋钮——以及按自身宽度取圆的选中指示条。 |
| **阴影仅用于悬浮层** | ToolTip / Flyout / MenuFlyout / ContentDialog / TeachingTip / Toast，以及 ComboBox、建议列表与日期选择器的弹出层。页面内唯一例外是 `EStopButton` 的旋钮：静止凸起、按下压平、锁定转内阴影，阴影本身就是可操作性的提示。其余页面内元素一律以描边区分层次。 |
| **字重仅 400 / 600** | 不使用 Bold 与斜体——中文无原生斜体，合成斜体在小字号下可读性差。 |

除 `BoxShadow` 外（其语法收的是颜色而非画刷），控件层不包含字面色值，颜色全部通过 `DynamicResource` 引用变量层中的 token。更换主题色、实现高对比度、按产品线定制外观时，仅需修改变量层，控件层无需改动。

---

## 工业 HMI 控件组（第 7 组）

该组在 WinUI 与 FluentAvalonia 中均无对应实现。以下约束涉及人身与设备安全，已实现在控件内部并由回归测试固定：

<details>
<summary><b>七条安全约束</b></summary>

<br>

- **安全色不是状态色。** 急停与 Alarm 级报警使用 `SafetyRedBrush`，而非 `SystemFillColorCriticalBrush`——后者在深色主题下为浅粉色 `#FF99A4`，不适用于需要立即处置的级别。`SafetyRed` 按主题分别调过对比度（浅色 `#C42B1C`、深色 `#E81123`），但两档都是不会认错的红。
- **报警采用呼吸而非闪烁**（1.5s，opacity 1↔.62）。高频闪烁易引起视觉疲劳，并存在光敏性癫痫风险。动画关闭后自动补充安全黄描边，保证降级后 Alarm 与 Warning 仍可区分。
- **`JogButton` 具备七重停止触发**——释放、指针捕获丢失、指针离开、失焦、按键抬起、控件摘除、看门狗超时。仅监听 `PointerReleased` 不充分：按住后将指针拖出按钮时，释放事件可能不在该控件上触发，导致设备持续运动。
- **心跳灯由显式调用 `Beat()` 驱动**，而非固定周期动画。无事件输入时自动转为停跳态——通信中断后仍在跳动的假心跳，比没有心跳指示更危险。
- **`Readout` 数据过期时保留最后已知值**，仅置灰并标注距上次更新的时长。替换为占位符是错误做法：通信中断时设备侧过程仍在进行，操作员需要知道中断前的最后数值。
- **`ParameterRow` 下发成功后回填设备回读值**而非输入值——设备可能对参数限幅或量化。下发失败时回滚至上一次成功值。
- **软件急停不能替代硬件急停回路。** `EStopButton.HardwareLocationHint` 用于在界面上标注硬件急停装置的物理位置。

</details>

---

## UI Automation

每个控件都提供自动化对等体。这件事在工业场景里比通常更要紧：**HMI 的验收普遍
用 UI Automation 驱动界面跑回归**，而没有对等体的控件在 Inspect 里只是一团没有
名字的 `Custom` 矩形，使用方的测试台根本抓不到——界面本身却完全看不出异常。

三条原则：

- **`Value` 只放机器可读的那个量，判读上下文放 `ItemStatus`。**
  测试台读到 `"85.4"` 无从判断这是实时值还是五分钟前的死值，而这两种情况在屏幕上
  也只差一个灰度。过期、偏离、写入中一律进 `ItemStatus`，单位进 `ItemType`，
  绝不拼进 `Value`。枚举状态给枚举名而不是显示文字——显示文字随本地化变，
  断言挂在它上面的话，界面一翻译脚本就全红。
- **危险动作不通过自动化模式暴露成一次调用。** 急停复位要求长按，理由就是防误碰；
  让客户端一次 `Toggle()` 就把自锁解掉，等于给它开了一条现场操作员都没有的近路。
  `EStopButtonAutomationPeer.Toggle()` 只触发不解锁，已锁定时抛
  `ElementNotEnabledException`。宿主的拒收闸同样不绕过：`AlarmBanner` 的 `Invoke()`
  走 `Acknowledge()`，宿主命令 `CanExecute == false` 时自动化照样确认不了。
- **装饰性元素主动退出自动化树。** 占位骨架、分隔线、图标进树只是噪音，会把真正
  要读的东西淹掉，因此返回一个「既不是控件元素也不是内容元素」的对等体。
  条件性的也一样：关掉的 `InfoBar`、没有名字的 `PersonPicture` 都会退出。

凭空出现的元素——报警、通知、浮层——都声明为活动区域，客户端不必轮询就能得知。
`AlarmSeverity.Alarm` 与 `Fault` 用 `Assertive`，`Warning` 用 `Polite`，`Info` 不播报。

---

## 高对比度

本库自带两套额外的主题变体：`CobaltFluentTheme.HighContrastLight` 与
`CobaltFluentTheme.HighContrastDark`。它们不是「把普通主题调得更狠一点」，
遵循的是另一套规则：

- **表面一律纯色，层次全部交给描边。** 所有背景收敛成纯黑或纯白，
  分层完全由描边承担，而描边一根都不许是淡的。
- **任何一处都不许半透明。** 半透明色的实际对比度取决于底下画了什么，
  而「保证对比度」正是这两套变体存在的全部理由。唯一的例外是模态遮罩——
  它必须透出底下的界面，否则遮的是什么就看不出来了。
- **正文达到 WCAG AAA（7:1）。** 失效文字刻意压在这条线以下：
  一并抬上去的话，「能点」和「不能点」在高对比度下就分不出来了。
- **安全色不变。** ISO 13850 的红和黄承载的是法规语义，不是主题的一部分；
  饱和红压白字或黑字都到不了 7:1，这是这个色相的物理上限，
  为了凑数字把它改成粉色等于把那层语义抹掉。它们保持 AA。
- **深色变体的强调色用青而不是惯用的黄**，为的是把黄留给警告态。
  一屏上「这是可点的」和「这要注意」撞成同一个颜色，两个意思都没了。

选哪套变体是应用的决定——**本库不会自己去写 `RequestedThemeVariant`**。
需要跟随系统的明暗与对比度设置，在启动时显式调一次：

```csharp
public override void OnFrameworkInitializationCompleted()
{
    CobaltFluentTheme.FollowSystemContrast(this);   // 返回 IDisposable，Dispose 掉即停止跟随
    base.OnFrameworkInitializationCompleted();
}
```

四套变体对应平台分开上报的两个设置——`PlatformThemeVariant`（明/暗）与
`ColorContrastPreference`（普通/高对比）。`tools/audit.py` 会把对照表里每一对
前景/背景在四套变体下逐一核对，并拒绝高对比度里出现的任何半透明值——
否则漏一个键就会静默继承回原来那个半透明值。

---

### 标签撕出成窗口

`TabView` 支持拖拽重排、把标签撕出成独立窗口、以及拖回任意窗口的标签栏并入。
撕出的窗口本身也是一个 `TabView`，所以可以继续往里拖。

**这是桌面专有能力。** `Avalonia.LinuxFramebuffer`（DRM/KMS，嵌入式面板走的那条路）、
移动端、浏览器都是单窗口的——它们的生命周期是 `ISingleViewApplicationLifetime`，
只有一个 `MainView`，没有窗口列表。`CanTearOut` 报的就是「当前进程到底能不能做到」，
做不到时不出那个视觉暗示，免得操作员拖了半天才发现没反应。重排在哪儿都能用。

两条被排除的路子值得记下来：

- **`Window.BeginMoveDrag`** 把窗口移动交给窗口管理器，此后进程收不到任何指针事件——
  于是「把撕出去的标签拖回来」无从检测。Chrome 在 Windows 上自己实现窗口拖动正是这个原因。
- **`DragDrop.DoDragDrop`** 是操作系统的剪贴板式拖放协议：载荷要能序列化，
  而这里要搬的是一个活着的控件实例；各平台行为差异也大。

实际走的是指针捕获：捕获之后即使光标离开窗口，`PointerMoved` 仍然送到源窗口。
坐标用 `PointToScreen` 换算，预览窗用 `Window.Position` 跟随，落点由自己拿屏幕坐标
去比对每个窗口的标签栏——不依赖操作系统的命中测试。全托管，裁剪与 NativeAOT 下都成立。

预览窗设 `ShowActivated = false`。这一行是整个方案的支点：预览窗一旦抢走激活，
源窗口的指针捕获就丢了，拖拽当场断在半路。

源和目标在不同窗口时，搬家分成两个调度轮次完成。同一轮里先摘后插会抛
`Attempt to call InvalidateArrange on wrong LayoutManager`——摘除产生的失效还排在
旧窗口的队列里，而控件已经挂到新窗口的布局管理器上了。同窗口内重排仍然同步完成。

键盘路径：`Ctrl+Shift+PageUp` / `PageDown` 把获得焦点的标签左右挪一格，
沿用浏览器与 VS Code 的既有约定。拖拽是纯指针手势，而工业面板上不一定有鼠标。

## NativeAOT 与裁剪

控件层不含反射绑定，使用方可以直接用 `PublishAot` + `TrimMode=full` 发布，
本库不会贡献任何一条 IL 告警。嵌入式目标上 AOT 不是可选项，这条是硬约束。

反射绑定——走 `ReflectionBindingExtension` 的 `{Binding}`、C# 里的
`new Binding { Path = "..." }`——按名字在运行时解析成员。完全裁剪下目标可能被移除，
而**绑定会静默失效：界面照样画出来，只是那一处不再更新。**
库里开了 `AvaloniaUseCompiledBindingsByDefault`，任何一处退回反射绑定都会在编译时
带出告警，而不是变成一个「在你接不上调试器的机器上悄悄不刷新了」的字段。

CI 跑 `tools/aot-gate.sh`，它有两半——因为光查告警不够：

1. **发布**一遍 NativeAOT。任何来自本仓库的 IL 告警都算失败。
   第三方程序集的告警登记在 `tools/aot-allow.txt`，每条写明理由；
   本仓库自己的代码不允许登记进去。
2. **跑**一遍产出的原生二进制。`tools/Cobalt.Fluent.AotProbe` 把四套主题变体、
   自动化对等体、以及几处改写过的绑定在真二进制上过一遍。
   编译绑定的路径解析到错误的成员时**一条告警都不会有**，只有真跑才看得出来；
   自定义 `ThemeVariant` 的字典键被裁掉也一样，而且它是在加载主题那一刻就炸，
   不是运行到某个页面才炸。

---

## 本地化

控件内部生成的文字全部走 `CobaltStrings`，默认按 `CultureInfo.CurrentUICulture` 选：
`zh*` 给中文，其余给英文。整块可换：

```csharp
CobaltStrings.Current = new CobaltStringsZhHans();   // 固定中文
CobaltStrings.Current = new MyPlantStrings();        // 厂内术语
```

不用 resx。`ResourceManager` 靠反射查卫星程序集，会顶红本仓库的 NativeAOT 闸口；
普通虚成员运行时零开销，而且能整块替换。

三类文字去三个地方，这条边界是要紧的：

| | 去哪 | 为什么 |
|---|---|---|
| 屏幕上的字、`Name` / `ItemStatus` / `HelpText` | `CobaltStrings` | 给人读的，按 UIA 约定要本地化 |
| `IValueProvider.Value` | **绝不本地化** | 这是测试台断言的锚点。本地化了，使用方的验收脚本会在界面翻译的那天全红 |
| 异常消息 | 英文字面量 | 给开发者和测试台看的，按库的惯例 |

作为属性默认值的文字（`AcknowledgeContent`、表头这些）在构造时取一次，
换语言不会改写已经建出来的实例——对 HMI 来说语言通常是部署期或换班时的决定，
为运行时热切换给每个属性加一层投影不划算。控件**内部算出来**的文字会跟着变：
这些控件在挂载期间订阅 `CobaltStrings.CurrentChanged`。

---

## NuGet 包

```bash
tools/aot-gate.sh      # NativeAOT 发布，然后真跑一遍原生二进制
tools/pack-gate.sh     # 打包，然后从装上的包里用一遍
```

包内带 XML 文档（说明是中文的，签名和参数名不是）、符号包和 SourceLink 元数据，
使用方能从自己的应用单步进本库。

`pack-gate.sh` 和 AOT 那道闸口一样有两半，理由相同：**控件库最经典的翻车方式，
是项目引用一路绿、包引用炸掉**——编译后的 XAML 没进程序集，模板全都套不上，
而仓库内部怎么测都看不见。所以闸口先打包，再装进一个一次性工程里真的用一遍。
它还刻意跑在固定区域性下，那是仓库里唯一走英文默认路径的地方——
回归测试为了自身结果确定，把语言钉死了。

---

## 嵌入式与触摸屏适配

针对 Mali GPU 等嵌入式图形环境提供以下配置项：

| 场景 | 配置 |
|---|---|
| 同屏多个运行指示灯 | `StatusIndicator.IsPulseEnabled="False"`（保留静态外环，形状编码不丢失） |
| 报警横幅呼吸动画 | `AlarmBanner.IsBreathingEnabled="False"`（自动补充安全黄描边） |
| 骨架屏微光 | `Skeleton.IsShimmerEnabled="False"` |
| 长时间加载 | 以 `ProgressBar` 的不定态代替 `ProgressRing`——仅驱动 transform |
| 亚克力（实时模糊） | 覆盖 `AcrylicBackgroundFillColorDefaultBrush` 为实色 |
| 触摸目标放大 | 覆盖 `ControlHeight` / `ControlCornerRadius` / `OverlayCornerRadius` |

长趋势不需要配置：`TrendChart` 与 `Sparkline` 的顶点数按绘图区像素宽度封顶。
一个 8 小时班次按 1 Hz 采样是 28800 点/通道，全量画的话 800 px 宽的绘图区上
每像素列摊到 36 个顶点。渲染前先做 min/max 抽稀——每列只留极大和极小两个点，
包络与全量一致，持续三个采样点的超调照样看得见。换成「每 N 个取一个」就会把
那次超调抹掉，而操作员调出趋势图正是为了看它。

图标不依赖 Segoe Fluent Icons：38 个字形全部实现为矢量路径，跨平台渲染一致，亦无需随应用分发字体。若目标环境已具备该字体，可通过 `SymbolIcon.UseGlyphFont="True"` 切换回字体渲染。

---

## 构建与开发

```bash
tools/check.sh                        # 编译控件库（复制至临时目录，避免与 IDE 争用 obj）
tools/check.sh --gallery              # 连同展柜一并编译
tools/check.sh --only Button.axaml    # 仅合并指定控件层文件（并行开发用）

python3 tools/audit.py                             # 控件层静默失效审计（14 项检查）
tools/aot-gate.sh                                  # NativeAOT 发布，然后真跑一遍原生二进制
tools/pack-gate.sh                                 # 打包，然后从装上的包里用一遍
dotnet test tests/Cobalt.Fluent.Tests               # 372 项回归测试
dotnet run  --project samples/Cobalt.Fluent.Gallery # 运行展柜
```

以下四个生成物必须与源码一同提交。CI 会重跑全部四个生成器，任一产物不同步即构建失败：

```bash
python3 tools/gen_tokens.py          # tools/palette.json → Themes/Tokens.axaml
python3 tools/gen_theme_index.py     # 控件层合并列表（编译前自动执行）
python3 tools/gen_gallery_pages.py   # 展柜目录与章节骨架
python3 tools/gen_api_docs.py        # docs/CONTROLS.md
```

无头渲染截图用于视觉验收，CI 中同样执行，渲染失败即构建失败：

```bash
dotnet run --project tools/Cobalt.Fluent.Shots -- artifacts/shots Button both
dotnet run --project tools/Cobalt.Fluent.Shots -- artifacts/shots "shell:Readout" dark
```

---

## 文档

| 文档 | 内容 |
|---|---|
| [`docs/CONVENTIONS.md`](docs/CONVENTIONS.md) | 开发约定——分层结构、资源键速查、伪类清单、不变量，以及若干编译期无法发现的问题 |
| [`docs/CONTROLS.md`](docs/CONTROLS.md) | 控件 API 参考（77 个类型，由 `tools/gen_api_docs.py` 从源码抽取） |
| 展柜 | 49 个章节：视觉规格 + 交互演示 + 资源键对照 + 源码查看，其中 22 个另附状态矩阵 |

修改控件前请先阅读 `CONVENTIONS.md`。提交 PR 前请在本地完成编译、测试与生成脚本——CI 执行相同的检查，并额外对全部章节进行无头渲染。

---

## 许可

[MIT](LICENSE)

依赖的 Avalonia（本体、`Avalonia.Themes.Fluent`、`Avalonia.Controls.DataGrid`）同为 MIT 许可，版权归 [AvaloniaUI OÜ](https://github.com/AvaloniaUI/Avalonia) 及其贡献者所有。本库仅通过 `PackageReference` 引用，不将其源码编入或随包分发。

设计语言参照 Microsoft Windows 11 Fluent Design System；实现为独立编写，不包含 Microsoft 的任何代码或资源。图标为独立绘制的矢量路径，非 Segoe Fluent Icons 字形文件。
