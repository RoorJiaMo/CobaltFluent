# Changelog

本项目遵循 [Semantic Versioning](https://semver.org/lang/zh-CN/)。

## 未发布

### 新增

- `TitleBar` —— 自绘标题栏，第 12 组「窗口外壳」。**它存在的主要理由是 Windows 11 的
  贴靠布局（Snap Layouts）**：用系统装饰时那个功能本来就是好的，真正会把它弄坏的
  正是自绘标题栏——自己画的按钮在 Windows 眼里只是客户区里一块像素，shell 不知道
  那是最大化钮，悬停面板于是再也不弹。本控件在最大化钮上让 `WM_NCHITTEST` 返回
  `HTMAXBUTTON` 把它接回去，空白处标 `Caption`（拖动移窗、双击最大化、右键系统菜单
  全部由 shell 提供），左右内容区标回 `Client`（否则放上去的菜单点了没反应）。
  走的是 `Avalonia.Controls.Win32Properties` 附加属性，不需要 P/Invoke、不按平台分支
  编译，非 Windows 后端天然是空操作。
- **贴靠布局的自绘实现。**上面那条是把活交给 Windows shell，只在 Windows 11 上有；
  这一条是把同一件事做在框架内部——布局表、分区几何、命中判定、窗口摆放全部是本库的代码，
  所以 Windows 10、Linux、macOS、嵌入式面板上拿到的是同一套东西。
  - `SnapLayoutPicker` —— 自绘的布局面板，悬停最大化钮弹出。面板上画的每一格
    就是窗口真正会占的那块像素：预览和执行走的是同一个 `SnapGeometry.ZoneRect`。
  - `SnapGeometry` —— 纯几何，不碰窗口不碰平台。**取整取的是分区边界而不是尺寸**：
    1919 像素三等分，各自把 639.67 取整会得到 1920，最右边那栏溢出屏幕一像素；
    反过来取 639 就在右边留一条缝。两条边界各自从比例取整，相邻分区共用同一个值，
    拼起来严丝合缝。
  - `WindowSnap` —— 把分区落到窗口上，带还原点。已经贴靠的窗口再贴到别处，
    还原点保持第一次贴靠之前的那个——每贴一次就更新的话，来回换几次布局
    就再也回不到原始尺寸了。
  - 布局按屏幕挑：窄屏不给三栏（1366 宽三等分每栏 455，摆不下一个正常表单）；
    带鱼屏才给「窄-宽-窄」；竖屏换成上下切分。**门槛按逻辑像素判**，
    所以 4K 屏 200% 缩放算的是 1920 而不是 3840。
  - 键盘可达：Tab 在布局之间跳，方向键在布局内部走。悬停是纯指针手势，
    而工业面板上不一定有鼠标。
  - 每一格的朗读名说的是方位（「右上四分之一」）而不是序号（「区域 2/4」）——
    后者等于没说。归不了类的形状退回百分比描述，不硬套方位词。
- `TitleBar.SnapLayoutMode` 决定面板由谁来出：`Auto`（默认，Windows 11 用系统的、
  其余平台用自绘的）/ `System` / `Builtin` / `None`。**两套机制只能二选一**：
  `System` 要把最大化钮标成非客户区，而标了之后指针事件不再送到 Avalonia，
  自绘面板的悬停就永远触发不了。运行时改模式会重标一遍。
- `TitleBar.EffectiveSnapLayoutMode`（只读）报出解析 `Auto`、核对过能力之后
  实际生效的模式。面板不出来时先看这里。
  `SupportsSnapLayouts` 的含义相应改成「悬停最大化钮会不会出现面板，不论谁画的」。
- `TitleBar.ShowSnapLayouts()` / `CloseSnapLayouts()` —— 悬停之外的入口，供绑快捷键。

  **做不到的要说清楚**：能摆的只有本进程自己的窗口。Windows 的贴靠助手会在剩下的
  分区里列出**别的应用**的窗口，那需要系统级权限，本库做不到，也不假装做得到。
- `TitleBar.ApplyTo(Window)` 把三条窗口提示一次设齐。漏掉任何一条的表现都不一样：
  不扩展客户区则被系统标题栏挤在下面；不设 `NoChrome` 则系统按钮和自绘按钮同时出现；
  不设高度提示则顶部留一条系统预留的空白。
- 字形新增 `Symbol.Minimize` / `Maximize` / `Restore`，共 41 个。和全库一致是矢量路径，
  不是 Segoe Fluent Icons 的码点——嵌入式 Linux 上没有那套字体，用码点会渲染成豆腐块。
  画在 16 格里只占中间 10 格，Windows 的标题栏字形就是 10×10，画满会比系统按钮大一圈。
- `TabView` 支持拖拽重排、撕出成独立窗口、拖回并入。撕出是桌面专有能力，
  `CanTearOut` 报出当前进程能不能做到——单窗口平台（嵌入式 framebuffer、移动端、
  浏览器）上不出那个视觉暗示，而不是让操作员拖了才发现没反应。
- 键盘重排：`Ctrl+Shift+PageUp` / `PageDown`。拖拽是纯指针手势，
  而工业面板上不一定有鼠标。
- `TabView.TabAddRequested` 事件。「+」的三条路依次尝试：事件 → `AddCommand` →
  内置空标签兜底——画出来的按钮不能是死按钮。
- `TabView.TabTearOutRequested` / `TabMoved` 事件。绑了 `ItemsSource` 时控件不改集合，
  事件没人处理就拒绝搬，不留下一个既不在这边也不在那边的标签。

### 修复

- 在两个**都已显示**的窗口之间搬标签会抛
  `ArgumentException: Attempt to call InvalidateArrange on wrong LayoutManager`。
  摘除产生的布局失效还排在源窗口队列里，控件却已经挂到目标窗口的布局管理器上。
  和标签有没有内容无关，空标签一样抛。改为跨视觉根时把插入推迟一个调度轮次；
  同窗口内重排仍然同步完成。

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
