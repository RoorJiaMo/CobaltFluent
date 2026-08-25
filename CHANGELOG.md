# Changelog

本项目遵循 [Semantic Versioning](https://semver.org/lang/zh-CN/)。

## 1.0.0

首个稳定版本。

### 控件

62 个控件，分 11 组。其中**第 7 组（工业 HMI）在 WinUI 与 FluentAvalonia 中均无对应实现**，
是本库的主要差异所在：

- `Readout` —— 数据过期时保留最后已知值并标注时长，而不是换成占位符。
  通信断开期间设备侧的反应还在跑，操作员需要断开前的那个值。
- `JogButton` —— 七条停止触发（松手、指针捕获丢失、指针离开、失焦、松键、卸载、看门狗）。
  只听 `PointerReleased` 不够：按住后把指针拖出控件再松开，事件可能到不了控件，轴就停不下来。
- `EStopButton` —— 自锁 + 长按复位，另有「急停指令未下发」第三态。
  指令没能下发时不能退回「就绪」，那是在说一个没有发生的事实。
- `AlarmBanner` —— 用呼吸不用闪烁（1.5s，opacity 1↔.62）。关掉动画时自动补安全黄描边，
  否则降级后 Alarm 与 Warning 分不出来。
- `ParameterRow` —— 下发成功后回显**设备接受的值**而不是输入值；失败回滚到上次成功值。
- 另有 `StatusIndicator`、`Heartbeat`、`NumericKeypad`、`DeviceStatusBar`、
  `ParameterTable`、`JogGroup`。

### 主题

- 四套变体：明、暗、**高对比明、高对比暗**。
  高对比度下表面纯色、层次交给描边、任何一处不许半透明，正文达到 WCAG AAA。
  安全色不变——ISO 13850 的红黄承载法规语义，不是主题的一部分。
- 81 个颜色键由 `tools/palette.json` 生成，控件层除 `BoxShadow` 外不含字面色值。
- `CobaltFluentTheme.FollowSystemContrast(app)` 可选地跟随系统的明暗与对比度设置。
  本库不会自己去写 `RequestedThemeVariant`。

### 自动化

- 每个控件都有 UI Automation 对等体。`Value` 只放机器可读的量（枚举名、原始数字），
  判读上下文放 `ItemStatus`——测试台读到 `"85.4"` 无从判断这是实时值还是五分钟前的死值。
- 危险动作不通过自动化模式暴露成一次调用：急停的 `Toggle()` 只触发不解锁。
- 装饰性元素主动退出自动化树。

### 本地化

- 控件内部生成的文字全部走 `CobaltStrings`，默认按 `CurrentUICulture` 选，
  中文环境给中文、其余给英文。整块可换：`CobaltStrings.Current = new MyPlantStrings()`。
- 不用 resx —— `ResourceManager` 靠反射查卫星程序集，会顶红 NativeAOT 闸口。

### 工程

- **NativeAOT 就绪**：控件层不含反射绑定，`TrimMode=full` + `PublishAot` 下本库零 IL 告警。
- 340 项回归测试；`tools/audit.py` 14 项静默失效审计；
  `tools/aot-gate.sh` 与 `tools/pack-gate.sh` 两道闸口都是「发布 + 真跑一遍」两半。
- CI 无头渲染全部 49 个章节共 98 张截图，渲染不出来即失败。

### 已知限制

- `TrendChart` 的 X 轴按采样下标排布，不是时间轴：采样不均匀时横轴不准，
  且没有缩放平移与历史回放。顶点数已按绘图区像素宽度封顶（min/max 抽稀）。
- 没有报警列表 / 报警历史控件，只有单条 `AlarmBanner`。
- XML 文档注释是中文的。签名与参数名是英文，对不读中文的使用方仍可用。
- 包图标未提供。放一个 128×128 的 `src/Cobalt.Fluent/Assets/icon.png` 即会自动带上。
