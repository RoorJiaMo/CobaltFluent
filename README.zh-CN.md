<div align="center">

<img src="docs/images/logo.png" width="128" alt="Cobalt.Fluent" />

# Cobalt.Fluent

**Windows 11 Fluent 设计语言的 Avalonia 实现**

61 个控件 · 11 个分组 · 明暗双主题 · 零第三方依赖 · 内置工业 HMI 控件组

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

多数控件提供状态矩阵；CI 对 48 个章节逐页无头渲染共 96 张截图，渲染失败即构建失败。

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
# 尚未发布到 nuget.org。首个版本发布前，直接引用工程：
dotnet add reference path/to/src/Cobalt.Fluent/Cobalt.Fluent.csproj
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

展柜应用包含 48 个章节。控件章节由视觉规格说明、交互演示、资源键与伪类对照三部分组成，其中 22 个另附状态矩阵，将各伪类并排定格；另有 5 个总则性章节——设计基线、排版、图标清单，以及第 7、9 组的组内总则——为说明与参考材料，不含控件演示。

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
<summary><b>完整清单（61 个控件）</b></summary>

<br>

| 组 | 控件 | 来源 |
|---|---|---|
| **2** 基础输入 | Button · ToggleButton · SplitButton · DropDownButton · HyperlinkButton · TextBox · NumberBox · ComboBox · CheckBox · RadioButton · ToggleSwitch · Slider | Avalonia 内置 + 本库 ControlTheme |
| **3** 容器 | Card · SettingsCard · SettingsGroup · Expander · TabControl · TabView · NavigationView | Card / SettingsCard / SettingsGroup / TabView / NavigationView 为本库实现 |
| **4** 集合 | ListBox · DataGrid · TreeView | ListBoxItem 重写模板（增加选中指示条） |
| **5** 反馈 | InfoBar · InfoBadge · ProgressBar · ProgressRing · ToolTip | InfoBar / InfoBadge / ProgressRing 为本库实现 |
| **6** 弹出 | Flyout · MenuFlyout · ContentDialog · TeachingTip · CommandBar | ContentDialog / TeachingTip / CommandBar 为本库实现 |
| **7** HMI 专用 | Readout · StatusIndicator · AlarmBanner · ParameterRow · JogButton · EStopButton · DeviceStatusBar | 全部为本库实现 |
| **8** 日期时间 | CalendarDatePicker · TimePicker · Calendar · RangeCalendar | 内置 + 本库 ControlTheme；RangeCalendar 为本库实现（补充区间端点伪类）。日期区间由两个 `CalendarDatePicker` 组合而成，并无 `DateRangePicker` 类型 |
| **9** 图表 | ChartFrame · TrendChart · Gauge · BarChart · Sparkline · ChartLegend | 全部为本库自绘实现 |
| **10** 表格增强 | DataGridToolbar · Pagination · EmptyState · Skeleton | 全部为本库实现 |
| **11** 常用补充 | AutoSuggestBox · BreadcrumbBar · SegmentedControl · Chip · Stepper · GridSplitter · Toast · PersonPicture | 除 AutoSuggestBox（即 Avalonia 内置 `AutoCompleteBox` 重做主题）与 GridSplitter 外，均为本库实现 |

完整 API 参见 [`docs/CONTROLS.md`](docs/CONTROLS.md)（73 个类型，由脚本从源码抽取）。

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

图标不依赖 Segoe Fluent Icons：37 个字形全部实现为矢量路径，跨平台渲染一致，亦无需随应用分发字体。若目标环境已具备该字体，可通过 `SymbolIcon.UseGlyphFont="True"` 切换回字体渲染。

---

## 构建与开发

```bash
tools/check.sh                        # 编译控件库（复制至临时目录，避免与 IDE 争用 obj）
tools/check.sh --gallery              # 连同展柜一并编译
tools/check.sh --only Button.axaml    # 仅合并指定控件层文件（并行开发用）

dotnet test tests/Cobalt.Fluent.Tests               # 74 项回归测试
dotnet run  --project samples/Cobalt.Fluent.Gallery # 运行展柜
```

以下四个生成物必须与源码一同提交。CI 会重跑 `gen_tokens.py` 与 `gen_api_docs.py`，`Themes/Tokens.axaml` 或 `docs/CONTROLS.md` 不同步即构建失败；另外两个 CI 未做比对，提交 PR 前四个都要在本地跑一遍：

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
| [`docs/CONTROLS.md`](docs/CONTROLS.md) | 控件 API 参考（73 个类型，由 `tools/gen_api_docs.py` 从源码抽取） |
| 展柜 | 48 个章节：视觉规格 + 交互演示 + 资源键对照 + 源码查看，其中 22 个另附状态矩阵 |

修改控件前请先阅读 `CONVENTIONS.md`。提交 PR 前请在本地完成编译、测试与生成脚本——CI 执行相同的检查，并额外对全部章节进行无头渲染。

---

## 许可

[MIT](LICENSE)

依赖的 Avalonia（本体、`Avalonia.Themes.Fluent`、`Avalonia.Controls.DataGrid`）同为 MIT 许可，版权归 [AvaloniaUI OÜ](https://github.com/AvaloniaUI/Avalonia) 及其贡献者所有。本库仅通过 `PackageReference` 引用，不将其源码编入或随包分发。

设计语言参照 Microsoft Windows 11 Fluent Design System；实现为独立编写，不包含 Microsoft 的任何代码或资源。图标为独立绘制的矢量路径，非 Segoe Fluent Icons 字形文件。
