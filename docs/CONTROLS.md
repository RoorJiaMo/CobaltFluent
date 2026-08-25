# 控件 API

本文件由 `tools/gen_api_docs.py` 从源码抽取，**不要手改**——
改了控件重跑一次：`python3 tools/gen_api_docs.py`。

只收录本库新写的控件。Avalonia 内置控件（Button / TextBox / ComboBox 等）
只是换了 ControlTheme，API 没变，查 Avalonia 官方文档即可。

命名空间统一是 `Cobalt.Fluent.Controls`：

```xml
xmlns:fc="using:Cobalt.Fluent.Controls"
```

## 第 7 组 · 工业 HMI 专用

WinUI / FluentAvalonia 里不存在，全部新写。涉及人身和设备安全。

### `AlarmSeverity`

报警级别。Alarm 及以上要求立即处置。

| 取值 | 说明 |
|---|---|
| `Info` | 提示。跟随主题状态色。 |
| `Warning` | 警告。还能继续跑，但要注意。 |
| `Alarm` | 报警。需要立即处置，用安全红实底 + 呼吸。 |
| `Fault` | 故障。安全红实底 + 黄色左边条（ISO 13850 的红黄配）。 |

### `AlarmBanner` : `TemplatedControl`

报警横幅。 第 7 组的三条硬约束： 1. **Alarm / Fault 用安全红，不跟随主题。** 不能用 `SystemFillColorCriticalBrush`——那个在深色主题下是浅粉 `#FF99A4`， 对需要立即处置的级别是错的。 2. **用呼吸不用闪烁**（1.5s，opacity 1↔.62）。 高频闪烁引发疲劳，且有光敏性癫痫风险。 3. **`prefers-reduced-motion` 下关掉动画后必须用黄色描边补强**， 否则降级后 Alarm 和 Warning 就分不出来了。 见 `IsBreathingEnabled`。 确认（`IsAcknowledged`）之后停呼吸，但**横幅不消失**—— 报警条件还在，只是操作员表示看到了。

**伪类**：`:info` · `:warning` · `:alarm` · `:fault` · `:acknowledged` · `:breathing`

| 属性 | 类型 | 说明 |
|---|---|---|
| `Severity` | `AlarmSeverity` | — |
| `Title` | `string?` | — |
| `Detail` | `string?` | — |
| `Timestamp` | `DateTime?` | 报警发生时刻。复盘时这一列比什么都重要，所以要 tabular-nums。 |
| `IsAcknowledged` | `bool` | 已确认。停呼吸，但横幅不消失——报警条件还在。 `Severity` 变化不会复位这个状态。它是宿主拥有的状态 （默认 TwoWay），报警的生命周期归宿主管：同一条物理报警在容差带上下抖动时， 宿主往往刻意保持确认态，控件擅自写回 false 会把它覆盖掉。 因此「已确认 → 降级 → 再次升级回 Alarm」时宿主必须自己把它置回 false， 否则新的 Alarm 一诞生就是已确认态：不呼吸、确认按钮隐藏， 屏幕上和一条普通静态红条没有区别。 |
| `AdditionalCount` | `int` | — |
| `AcknowledgeCommand` | `ICommand?` | — |
| `DetailsCommand` | `ICommand?` | — |
| `IsBreathingEnabled` | `bool` | — |
| `Actions` | `object?` | — |
| `ActionsTemplate` | `IDataTemplate?` | — |
| `AcknowledgeContent` | `string` | — |
| `DetailsContent` | `string` | — |
| `TimeText` | `string?` | 格式化后的时间戳。复盘靠这一列，所以要 tabular-nums。 |
| `AdditionalText` | `string?` | 折叠提示文字。`AdditionalCount` 为 0 时是 null。 |
| `Glyph` | `Symbol` | — |
| `Acknowledged` | `event EventHandler<RoutedEventArgs>?` | — |

| 成员 | 说明 |
|---|---|
| `Acknowledge()` | 确认。幂等。 挂了 `AcknowledgeCommand` 而它 `CanExecute` 为 false （权限不足、通道断开、PLC 未就绪）时直接返回：确认没有真的下发出去， 界面就不该显示成已确认——那会让呼吸停止、确认按钮隐藏， 一条未被受理的 Alarm 从此和普通静态红条没有任何区别。 没挂命令时保持纯本地确认语义（只是操作员表示看到了）。 |

### `ConnectionState`

通信连接状态。

| 取值 | 说明 |
|---|---|
| `Connected` | 已连接，轮询正常。 |
| `Degraded` | 连着但不稳：偶发超时、重传。 |
| `Disconnected` | 断开。 |

### `DeviceStatusBar` : `TemplatedControl`

设备状态栏。常驻底栏，操作员靠它判断「系统还活着」。 心跳灯（`Heartbeat`）由**实际通信事件**驱动： 每次收到设备响应调一次 `Beat`。 不要用固定周期动画假装——通信断了心跳还在跳，比没有心跳灯更危险。

**伪类**：`:connected` · `:degraded` · `:disconnected`

| 属性 | 类型 | 说明 |
|---|---|---|
| `ConnectionState` | `ConnectionState` | — |
| `Endpoint` | `string?` | — |
| `LastResponse` | `DateTime?` | — |
| `PollRate` | `double` | — |
| `CurrentUser` | `string?` | — |
| `ShowClock` | `bool` | — |
| `Items` | `Avalonia.Controls.Controls` | — |
| `StateText` | `string?` | 连接状态的文字说明。颜色之外的第二重编码。 |
| `ClockText` | `string?` | 当前时间。工业现场记录操作时刻要用，所以默认显示到秒。 |
| `PollRateText` | `string?` | — |

| 成员 | 说明 |
|---|---|
| `Beat()` | 收到一次设备响应。每次通信成功调一下：心跳灯闪一次， `LastResponse` 跟着更新。 |

### `EStopButton` : `Button`

软件急停。 **它不能替代硬件急停回路。** 界面上建议同时标注硬件急停的物理位置， 见 `HardwareLocationHint`。软件路径要过 UI 线程、消息队列、通信链路， 任何一环卡住它就不响应；真正的安全回路是硬接线的。 行为： - **按下即触发**，不等松手。急停不能有一次松手的延迟。 - 触发后自锁：再点无效，必须显式复位。 - 复位默认要求长按（`RequireHoldToReset`），对应真实急停"拧一下才能弹起"的手感—— 防止误碰复位。 视觉上刻意打破 Fluent 的扁平：真实急停是物理蘑菇头，要有实体感。 颜色写死安全红 + 安全黄（ISO 13850 要求红钮黄衬）， **不跟随主题**，也不占"一屏一个强调色"的名额。

**伪类**：`:engaged` · `:resetting` · `:engagefailed`

| 属性 | 类型 | 说明 |
|---|---|---|
| `IsEngaged` | `bool` | 是否已触发并锁定。 |
| `EngageCommand` | `ICommand?` | 触发急停。**下游必须做成幂等且不可失败的。** |
| `ResetCommand` | `ICommand?` | — |
| `RequireHoldToReset` | `bool` | — |
| `ResetHoldDuration` | `TimeSpan` | — |
| `HardwareLocationHint` | `string?` | — |
| `Caption` | `string?` | — |
| `EngageFailedCaption` | `string?` | — |
| `EngagedCaption` | `string?` | — |
| `CaptionText` | `string?` | 当前该显示哪行字。模板绑它。 |
| `Engaged` | `event EventHandler<RoutedEventArgs>?` | — |
| `Released` | `event EventHandler<RoutedEventArgs>?` | 已复位。用急停的行话叫「释放」，避免和 `Reset` 方法重名。 |

| 成员 | 说明 |
|---|---|
| `Engage()` | 触发并自锁。已经触发时是空操作。 下发失败时不回滚成「就绪」。挂了 `EngageCommand` 而它 `CanExecute` 为 false 时，指令没发出去、设备没停；但退回「就绪」 同样是假陈述——操作员确实按下了急停。这时进第三态 `:engagefailed`， 说明文字直接指向硬件急停：软件路径已经证明不通，唯一还能信的是硬接线那个。 |
| `Reset()` | 复位。用代码复位时绕过长按要求 —— 长按是防误触的界面手段， 不是安全联锁；真正的联锁在设备侧。 |

### `Heartbeat` : `TemplatedControl`

心跳灯。**由实际通信事件驱动，不是固定周期动画。** 这一条是安全要求不是风格偏好：用固定周期的动画假装心跳的话， 通信断了心跳还在跳，操作员会以为系统活着——**比没有心跳灯更危险**。 用法：每次收到设备响应就调一次 `Beat`。 超过 `Timeout` 没有新的 Beat，灯自动转成停跳（红色常亮）。

**伪类**：`:beating` · `:stopped`

| 属性 | 类型 | 说明 |
|---|---|---|
| `FlashDuration` | `TimeSpan` | — |
| `Timeout` | `TimeSpan` | — |
| `IsStopped` | `bool` | 是否已停跳。停跳 = 通信断了，比任何文字提示都快。 |

| 成员 | 说明 |
|---|---|
| `Beat()` | 收到一次设备响应。每次通信成功调一下，灯闪一次。 必须在 UI 线程调用。设备响应天然到达在通信 / IO 线程上， 请在调用方那一侧编组。这里不做隐式编组：`Post(Beat)` 会把时间戳 推迟到 UI 线程实际执行的时刻，UI 拥塞时会把超时判定一起带偏， 高频调用下还会淹没 dispatcher 队列。 |
| `Restore(TimeSpan sinceLastBeat)` | 按真实经过时间恢复心跳状态，用于模板应用晚于首次 Beat() 的场合。 不要用 `Beat` 代替：那会把时间戳盖成「现在」，超时窗口从恢复 那一刻重新起算，实际存活时间最长可达两倍 `Timeout`—— 也就是说链路早就断了，心跳灯还能再亮一个完整的超时周期。 |

### `JogDirection`

点动方向。纯语义，模板据此选箭头字形。

| 取值 | 说明 |
|---|---|
| `None` | — |
| `Forward` | — |
| `Backward` | — |
| `Up` | — |
| `Down` | — |
| `Left` | — |
| `Right` | — |
| `Open` | — |
| `Close` | — |

### `JogStopReason`

点动停止的原因。会记进日志，出事之后要靠它复盘。

| 取值 | 说明 |
|---|---|
| `PointerReleased` | 正常松手。 |
| `PointerCaptureLost` | 指针捕获丢失。按住后把指针拖出控件再松开，走的是这条。 |
| `PointerExited` | 指针离开了控件。 |
| `LostFocus` | 控件失焦（切窗口、弹对话框）。 |
| `KeyReleased` | 键盘松键。 |
| `Detached` | 控件被禁用或从视觉树上摘掉。 |
| `Watchdog` | 看门狗超时。前面几条都没触发时的兜底——通常意味着 UI 线程卡过。 |

### `JogStopFailedEventArgs`

停止指令没能下发出去。`StopFailed` 的载荷。 这不是「已停止」——设备很可能还在动。使用方收到它必须走升级路径 （报警、切断使能、提示操作员按硬件急停），不能当成一次普通的停止处理。

### `JogButton` : `Button`

点动按钮：按住动作，松开停止。 **这个控件的交互就是它的规格。** 视觉上必须和普通 Button 区别开 （描边更重、底边 2px），否则操作员会当成开关按一下就走。 安全要点（第 7 组硬约束）： 只监听 `PointerReleased` 是不够的 —— 按住后把指针拖出按钮， 释放事件可能根本不在这个控件上触发，设备会一直动。 所以这里挂了六个停止触发点： 松手 / 捕获丢失 / 指针离开 / 失焦 / 松键 / 摘除， 外加一个 `WatchdogTimeout` 看门狗兜底 —— 防止 UI 线程卡死时设备失控。 `StopCommand` 会被重复调用（多个触发点可能同时命中）， 所以下游必须做成幂等的。

**伪类**：`:jogging` · `:stopfailed`

| 属性 | 类型 | 说明 |
|---|---|---|
| `Direction` | `JogDirection` | — |
| `Glyph` | `Symbol` | `Direction` 对应的箭头字形，模板绑它。 此前 Direction 的文档写着「模板据此选箭头字形」，而模板里根本没有任何地方 读它——展柜里 Direction="Open" / "Forward" 一律不产生任何效果。 方向是点动按钮上最要紧的信息（按错方向就是撞机），只靠 Content 里的 文字承载不够：一屏多个点动键时，箭头是扫一眼就能分辨的那一路编码。 |
| `StartCommand` | `ICommand?` | — |
| `StopCommand` | `ICommand?` | — |
| `Speed` | `double` | — |
| `RequiresConfirm` | `bool` | — |
| `IsConfirmed` | `bool` | 操作员已经确认过这次点动。`RequiresConfirm` 为 true 时， 这个没置上就不许启动——启动请求会转成 `ConfirmRequired` 事件。 使用方在确认框点「确定」后置 true；什么时候清由使用方决定 （每次点动都要确认就在 `JogStopped` 里清掉）。 |
| `WatchdogTimeout` | `TimeSpan` | — |
| `IsJogging` | `bool` | 是否正在动作。 |
| `JogStarted` | `event EventHandler<RoutedEventArgs>?` | — |
| `JogStopped` | `event EventHandler<JogStoppedEventArgs>?` | 停止时抛出，带停止原因。工业场合建议把它记进操作日志。 |
| `StopFailed` | `event EventHandler<JogStopFailedEventArgs>?` | 停止指令没能下发（`StopCommand` 存在但 `CanExecute` 为 false）。 设备可能仍在动作。此时不会抛 `JogStopped`， 因为那个事件的语义是「已经停了」。 |
| `ConfirmRequired` | `event EventHandler<RoutedEventArgs>?` | `RequiresConfirm` 为 true 而 `IsConfirmed` 还没置上时， 启动被拒并抛出这个事件。使用方据此弹确认框，确认后把 `IsConfirmed` 置 true。 |

| 成员 | 说明 |
|---|---|
| `Stop(JogStopReason reason)` | 停止动作。幂等：不在动作中时调用是空操作。 七个触发点可能同时命中，第一个到的那个负责真正停下来。 停止指令没能下发时不会报「已停止」。挂了 `StopCommand` 而它的 `CanExecute` 返回 false（下游忙、通讯断、权限不足）， 指令实际上一个字节都没发出去，此时清掉 `:jogging` 并抛 `JogStopped` 就是在告诉操作员「已经停了」——而轴还在动。 这种情况改为进入 `:stopfailed`、保持 `IsJogging` 为 true、 抛 `StopFailed`，并且看门狗继续跑：它是最后一道防线， 恰恰在停不下来的时候最不该被关掉。 |

### `JogGroup` : `StackPanel`

成对/成组的点动按钮容器（正转-反转、开阀-关阀）。 存在的意义只有一个：让相接处不圆、不双线。 实现上给子按钮打 `jog-first` / `jog-last` / `jog-only` 三个类， 圆角规则写在 JogButton 的 ControlTheme 里。 不用 `:nth-child` 选择器是因为 Avalonia 的 ControlTheme 里不允许出现子代选择器。

### `INumericInputTarget`

能被 `NumericKeypad` 挂接的数值输入宿主。 `ParameterRow` 实现了它。使用方自己的控件实现这个接口就能复用同一个键盘， 不必依赖本库的具体控件类型——这是「可选挂接」的全部含义。

| 属性 | 类型 | 说明 |
|---|---|---|
| `Label` | `string?` | 正在编辑什么。显示在键盘抬头。 |
| `Unit` | `string?` | 工程单位。跟在量程提示后面。 |
| `Minimum` | `double` | — |
| `Maximum` | `double` | — |
| `Format` | `string` | 量程提示的数字格式。不用来格式化正在输入的缓冲。 |
| `PendingText` | `string?` | 待下发的文本。键盘确认时写回这里。 |

| 成员 | 说明 |
|---|---|
| `CommitPending()` | 把 `PendingText` 真正下发出去。 返回 false 表示宿主此刻不受理（正在下发、只读、宿主自身判定不通过）， 键盘据此回滚并且不抛出确认事件——「界面说已下发、设备没收到」是本组要防的事故。 |

### `NumericKeypad` : `TemplatedControl`

数字键盘。触摸屏上位机的必需件——工业面板绝大多数没有物理键盘， 没有它，`ParameterRow` 这类控件在真实设备上根本改不了值。 三条硬约束，都不是外观问题： 1. **输入过程中不做量程拦截。** 把 5 改成 50 必然要经过中间态 5 → 50， 逐键校验会让一大批合法目标值根本输不进去。所以键盘允许自由输入， 只在提交那一刻闸住——`CanCommit` 实时反映能不能提交， 确认键随之禁用，但按键本身永远不拒收。 2. **超量程时拒绝，不静默限幅。** 上限 120 而操作员输了 150， 悄悄改成 120 提交是最危险的做法：他以为设备收到的是 150。 要么他自己改，要么不下发。 3. **首次按数字替换整个缓冲，不是追加。** 打开键盘时缓冲里是当前值 85.0， 要改成 9 却得先按五次退格，是触摸屏上典型的误操作来源。 首键替换是计算器沿用几十年的约定，退格与符号键则在既有缓冲上继续编辑。 独立使用时绑 `Text`、听 `Committed` 即可； 给 `Target` 赋一个 `INumericInputTarget` 则量程、单位、 格式与标签全部跟随宿主，确认时写回宿主并触发其下发。

**伪类**：`:empty` · `:invalid` · `:outofrange`

| 属性 | 类型 | 说明 |
|---|---|---|
| `Text` | `string?` | 正在输入的文本。外部赋值会重置「首键替换」状态。 |
| `Minimum` | `double` | — |
| `Maximum` | `double` | — |
| `Format` | `string` | 量程提示的数字格式。不用来格式化正在输入的缓冲—— 边输边格式化会把光标位置和小数点抢走。 |
| `Unit` | `string?` | — |
| `Label` | `string?` | 正在编辑什么。抬头必须写清楚——操作员面前经常同时开着几个参数。 |
| `AllowNegative` | `bool` | 允许负值。温度、偏差允许；转速、时长不允许。 |
| `AllowDecimal` | `bool` | 允许小数。整数参数（计数、序号）关掉，小数点键随之禁用。 |
| `MaxLength` | `int` | 缓冲最大字符数，含负号与小数点。防的是按住不放刷出一屏数字。 |
| `Target` | `INumericInputTarget?` | 挂接的输入宿主。赋值时量程、格式、单位、标签与当前文本一次性从宿主同步过来， 确认时写回宿主的 `PendingText` 并调用 `CommitPending`。为 null 时键盘完全独立。 |
| `Value` | `double?` | 缓冲解析出来的值。解析不出来时为 null。 |
| `CanCommit` | `bool` | 能否提交。空缓冲、解析失败、超量程时为 false，确认键随之禁用。 |
| `RangeText` | `string?` | 量程提示，如「20.0 – 120.0 °C」。两端都无界时为 null。 输入之前就把边界摆出来，比输完再报错省一次来回。 |
| `DecimalSeparatorText` | `string` | 小数点键的键面文字。按下去插入的是当前文化的小数点分隔符， 键面必须跟着走——触摸屏上键面是操作员唯一的可见线索。 |
| `ValidationText` | `string?` | 为什么不能提交。可以提交时为 null。 |
| `Committed` | `event EventHandler<NumericKeypadCommittedEventArgs>?` | 确认。只有 `CanCommit` 为 true 时才会触发。 |
| `Cancelled` | `event EventHandler<RoutedEventArgs>?` | 取消。缓冲不变，由使用方决定关闭还是复位。 |

| 成员 | 说明 |
|---|---|
| `LoadValue(string? text)` | 装入一个待编辑的值，并回到「首键替换」状态。外部重设缓冲一律走这里。 不能只写 `Text`：Avalonia 对相等的新值不发变更通知， 新值恰好等于当前缓冲时 `OnTextChanged` 根本不触发，_pristine 会停在上一次编辑的 false。 一块键盘轮流服务多个参数时，这会把上一个参数的残留缓冲带进下一个参数的设定值。 |
| `Append(string token)` | 追加一个字符。只认 0–9 与小数点分隔符，其余静默忽略。 不做量程校验——中间态必须能输进来。 |
| `Backspace()` | 退格一位。在既有缓冲上编辑，不触发首键替换。 |
| `Clear()` | 清空。和退格分开——戴手套连按退格是常见误操作，清空要独立一键。 |
| `ToggleSign()` | 正负号切换。是切换不是追加，按两次回到原样。负号取自当前文化 （瑞典语等用的是 U+2212 而不是 ASCII 连字符，写死 '-' 会拼出双负号）。 `AllowNegative` 为 false 时只禁止加负号；把已有的负值改成正值 永远允许——那是往合法方向走，挡住只会逼操作员退格重输。 |
| `Commit()` | 提交。`CanCommit` 为 false 时是空操作—— 不会把超量程的值限幅到边界后提交。 |
| `Cancel()` | 取消。不动缓冲，由使用方决定收起还是复位。 |

### `NumericKeypadCommittedEventArgs`

确认事件。`Value` 是解析并通过量程校验后的值。

### `ParameterWriteState`

参数下发状态机。

| 取值 | 说明 |
|---|---|
| `Clean` | 输入值和已生效值一致。 |
| `Dirty` | 已修改未下发。**这个态必须一眼看见**，否则操作员会以为已生效。 |
| `Writing` | 下发中，等设备回读。 |
| `Failed` | 下发失败，值已回滚到上次成功值。 |
| `OutOfRange` | 输入超量程，不允许下发。 |

### `ParameterRow` : `TemplatedControl, INumericInputTarget`

参数行。过程控制的主力控件。 **核心是把「我改了」和「设备收到了」区分开** —— 这两者之间的空档是事故高发区： 操作员改完数字就走，以为已生效，实际上还躺在输入框里。 状态机：`Clean → Dirty →（下发）→ Writing →（回读）→ Clean` 或 `Writing →（失败）→ Failed`，超量程时进 `OutOfRange` 且禁止下发。 两个容易写错的地方： 1. **下发成功后填回读值，不是输入值。** 设备可能做了限幅或量化—— 你写 85.3，它按 0.5 步进量化成 85.5。显示输入值就是在骗人。 所以 `CompleteWrite` 收的是设备回读回来的值。 2. **失败要回滚到上次成功值**，不能把失败的输入留在框里。 界面上 `:dirty` 要三重提示（整行淡黄底 + 输入框底边变色 + 行尾徽章）—— 一屏二十行参数时，只改 2px 边框根本看不见。 整张表的列宽用 `Grid.IsSharedSizeScope` + SharedSizeGroup 对齐，别每行各算各的。

**伪类**：`:dirty` · `:writing` · `:failed` · `:outofrange` · `:readonly`

| 属性 | 类型 | 说明 |
|---|---|---|
| `Label` | `string?` | — |
| `Unit` | `string?` | — |
| `ActualValue` | `double?` | — |
| `Setpoint` | `double` | — |
| `PendingText` | `string?` | — |
| `Minimum` | `double` | — |
| `Maximum` | `double` | — |
| `Format` | `string` | — |
| `IsReadOnly` | `bool` | — |
| `ApplyCommand` | `ICommand?` | — |
| `RevertCommand` | `ICommand?` | — |
| `WriteState` | `ParameterWriteState` | — |
| `StateText` | `string?` | 行尾徽章的文字。 |
| `ActualText` | `string?` | 格式化后的读值。必须过 `Format`，否则 85.0 会显示成 85， 一列数字的小数位数对不齐——数值列对不齐就失去了 tabular-nums 的意义。 |
| `IsInputLocked` | `bool` | 输入框是否该锁住。只读，或正在等回读时都要锁—— Evaluate() 在 Writing 态直接 return，此时改框里的字不会被重新判定， 「写入中」的徽章下面可以并排显示一个从未下发、也从未校验过的数字。 |
| `CanApply` | `bool` | 下发按钮是否可用。超量程、下发中、只读、没改动时都不可用。 |
| `WriteRequested` | `event EventHandler<RoutedEventArgs>?` | — |
| `ApplyContent` | `string?` | — |
| `RevertContent` | `string?` | — |

| 成员 | 说明 |
|---|---|
| `LoadSetpoint(double value)` | 装入一个新的设定值并把它作为新基准。外部重设设定值一律走这里。 不能只写 `Setpoint`：Avalonia 对相等的新值不发变更通知， 新值恰好等于当前设定值时 `OnSetpointChanged` 根本不触发， 「切了配方之后输入框跟着走」在这条路径上静默失效——框里会留着上一个配方 编辑到一半的值。对应 `LoadValue`。 正在等回读时只更新 `Setpoint` 本身，不动基准与输入框。 |
| `ParsePending()` | 解析当前输入。解析不出来时返回 null。 |
| `Apply()` | 请求下发。超量程或没改动时是空操作。 进 `Writing` 之后就等着应用侧回调 `CompleteWrite` / `FailWrite`。 |
| `CompleteWrite(double readbackValue)` | 下发成功。 必须是**设备回读回来的值**， 不是刚才写下去的值——设备可能做了限幅或量化，显示输入值等于骗人。 |
| `FailWrite(string? message = null)` | 下发失败。值回滚到上次成功值，让操作员看到设备上真实生效的是什么。 |
| `CommitPending()` | `INumericInputTarget` 的下发入口。转调 `Apply`—— 键盘不该绕过这里的量程判定与状态机自己写值。 正在下发（等回读）、只读、或本行自身判定不通过时返回 false， 由键盘负责回滚并且不报「已确认」。 |
| `Revert()` | 放弃修改，回到上次成功值。 |

### `ParameterTable` : `ItemsControl`

参数表。`ParameterRow` 的容器，负责表头和列宽对齐。 列宽在 `ParameterRow` 的模板里写死成同一组 （标签 * / 读值 96 / 设定 140 / 单位 56 / 状态 auto）， 同一个容器里的行拿到的可用宽度相同，所以自然对齐 —— 不用每行各算各的。

| 属性 | 类型 | 说明 |
|---|---|---|
| `LabelHeader` | `string` | — |
| `ActualHeader` | `string` | — |
| `SetpointHeader` | `string` | — |
| `UnitHeader` | `string` | — |
| `StateHeader` | `string` | — |

### `ReadoutSize`

读数字号档。桌面基准分别是 24 / 40 / 72。

| 取值 | 说明 |
|---|---|
| `Small` | — |
| `Medium` | — |
| `Large` | — |

### `Readout` : `TemplatedControl`

数值读数。过程界面上出现频率最高的控件。 第 7 组的两条硬约束： 1. **刷新时布局绝对不能跳动。** 等宽数字（tnum）+ 按最大位数预留 `ValueMinChars`。 比例数字下 84.6 → 84.9 会让整行横移，一屏二十个读数就是一片抖动。 2. **`:stale` 时保留最后已知值**，只变灰 + 标注多久没更新。 换成"—"是错的：通信断了，但设备上的反应还在跑，操作员需要知道断开前的最后一个值。 `:stale` 由内部定时器驱动，不等下一次数据到达才判断 —— 数据不来正是要报的那种情况，等它等不到。

**伪类**：`:deviating` · `:stale` · `:unknownage` · `:invalid` · `:nodata` · `:small` · `:medium` · `:large`

| 属性 | 类型 | 说明 |
|---|---|---|
| `Label` | `string?` | — |
| `Value` | `double?` | — |
| `Unit` | `string?` | — |
| `Format` | `string` | — |
| `Setpoint` | `double?` | — |
| `Tolerance` | `double` | — |
| `Size` | `ReadoutSize` | — |
| `LastUpdated` | `DateTime?` | — |
| `StaleAfter` | `TimeSpan` | — |
| `ValueMinChars` | `int` | — |
| `DisplayValue` | `string` | 格式化后的数值文本。`:nodata` 时是长破折号。 |
| `StatusText` | `string?` | 值下面那行小字：正常时是"目标 x · 偏差 ±y"，过期时是"最后更新 n 秒前"。 |
| `ValueMinWidth` | `double` | 按 `ValueMinChars` 和字号折算出的预留宽度，模板绑到数值区的 MinWidth。 |
| `StaleText` | `string?` | 过期标记，跟在标签后面。不过期时为 null。 |
| `IsStale` | `bool` | 数据是否已过期。只读，由 `LastUpdated` 和 `StaleAfter` 推出来。 |

### `DeviceState`

设备状态。顺序即严重程度递增。

| 取值 | 说明 |
|---|---|
| `Offline` | 未连接。空心圈——「没有信息」和「一切正常」必须长得不一样。 |
| `Idle` | 待机。实心灰点。 |
| `Running` | 运行中。绿点 + 脉冲环。 |
| `Warning` | 参数偏离，还能继续跑。 |
| `Fault` | 故障停机。 |

### `StatusIndicator` : `TemplatedControl`

状态指示灯。 **三重编码：颜色 + 形状/动效 + 文字。任何一种单独都不够。** 男性约 8% 有色觉障碍，强光下的工业屏幕颜色也会失真； 所以 offline 是空心圈、running 带脉冲环、warning/fault 各有自己的字形， 不能只靠红黄绿区分。 嵌入式注意：一屏十几个 running 指示灯就是十几个并发动画， Mali 这类 GPU 上会掉帧。那种场合把 `IsPulseEnabled` 关掉， 换成静态外环——形状编码还在，只是不动。

**伪类**：`:offline` · `:idle` · `:running` · `:warning` · `:fault`

| 属性 | 类型 | 说明 |
|---|---|---|
| `State` | `DeviceState` | — |
| `Label` | `string?` | — |
| `ShowLabel` | `bool` | — |
| `IsPulseEnabled` | `bool` | — |
| `Glyph` | `Symbol` | 该状态的非颜色信号：warning 是三角感叹号，fault 是圈叉，其余没有字形。 |

## 第 9 组 · 图表

自绘，零第三方依赖。视觉规格直接实现，不是喂给别的库的配置。

### `BarSeries` : `AvaloniaObject`

一组柱子。

| 属性 | 类型 | 说明 |
|---|---|---|
| `Name` | `string?` | — |
| `Values` | `IReadOnlyList<double>` | — |
| `PaletteIndex` | `int` | — |

### `BarChart` : `Control`

柱状图。分组柱，横轴是类别。自绘，不依赖图表库。

| 属性 | 类型 | 说明 |
|---|---|---|
| `Series` | `AvaloniaList<BarSeries>` | — |
| `Categories` | `IReadOnlyList<string>` | — |
| `YMaximum` | `double` | — |
| `YTickCount` | `int` | — |

| 成员 | 说明 |
|---|---|
| `Render(DrawingContext context)` | — |

### `ChartFrame` : `ContentControl`

图表外框：卡片底 + 抬头 + 绘图区。 **坐标轴单位放在抬头（`Subtitle`），不要画进绘图区** —— 画进去的话 °C 会压在最上一条刻度上、秒会压在末位刻度上。

| 属性 | 类型 | 说明 |
|---|---|---|
| `Title` | `string?` | — |
| `Subtitle` | `string?` | — |
| `Actions` | `object?` | — |
| `Legend` | `object?` | — |

### `ChartLegend` : `TemplatedControl`

图表图例。 **当前值由图例承载，不逐线标在曲线末端** —— 四个通道末值可能非常接近 （84.6 / 84.6 / 84.9），右边缘逐线标注必然叠在一起。 图例跟着十字线实时更新：没有十字线时显示末值，有十字线时显示该时刻的值。 点图例项可以把对应曲线隐藏。

| 属性 | 类型 | 说明 |
|---|---|---|
| `Chart` | `TrendChart?` | — |
| `ValueFormat` | `string` | — |

### `ChartLineStyle`

曲线之外的信息只能靠线型分层 —— 四种颜色已经被通道占满了。 线型层级：实线 → 虚线 → 点线，同色不同型，黑白打印和色觉障碍下都分得开。

| 取值 | 说明 |
|---|---|
| `Solid` | 通道曲线：1.5px 实线，用系列色。 |
| `Setpoint` | 设定值：1px 虚线 4-3，tertiary 色。 |
| `Limit` | 报警上下限：1px 虚线 3-3，critical 色，70% 不透明。 |

### `ChartSeries` : `AvaloniaObject`

一条曲线。 `PaletteIndex` 指向 `ChartSeries1..8` 那八个 token， **刻意避开纯红纯绿**：HMI 里绿=运行、红=故障已经是语义色， 一条绿色曲线会被操作员读成「这条正常」，而它可能正在超温。

| 属性 | 类型 | 说明 |
|---|---|---|
| `Name` | `string?` | — |
| `Values` | `IReadOnlyList<double>` | — |
| `PaletteIndex` | `int` | — |
| `LineStyle` | `ChartLineStyle` | — |
| `IsHidden` | `bool` | — |
| `Brush` | `IBrush?` | — |

### `GaugeZone` : `AvaloniaObject`

仪表外圈的一段阈值区带。

| 属性 | 类型 | 说明 |
|---|---|---|
| `From` | `double` | — |
| `To` | `double` | — |
| `Kind` | `GaugeZoneKind` | — |

### `GaugeZoneKind`

| 取值 | 说明 |
|---|---|
| `Ok` | — |
| `Caution` | — |
| `Critical` | — |

### `Gauge` : `RangeBase`

环形仪表。WinUI 没有，工业上很常见。270° 扫掠。 读数**绝对居中**在环心：用负 margin 是按某个字号手算出来的，字号一变就错位。

**伪类**：`:deviating` · `:critical`

| 属性 | 类型 | 说明 |
|---|---|---|
| `Unit` | `string?` | — |
| `Format` | `string` | — |
| `Caption` | `string?` | — |
| `Zones` | `AvaloniaList<GaugeZone>` | — |
| `CautionThreshold` | `double?` | — |
| `CriticalThreshold` | `double?` | — |
| `DisplayValue` | `string` | — |

### `GaugeArcs` : `Control`

仪表的弧线部分。单独抽出来是为了让读数用普通的居中布局压在上面 —— 读数如果也画在这里，就得手算基线，字号一变就错位。

| 属性 | 类型 | 说明 |
|---|---|---|
| `Owner` | `Gauge?` | — |
| `Thickness` | `double` | — |

| 成员 | 说明 |
|---|---|
| `Render(DrawingContext context)` | — |

### `SparklineTrend`

迷你趋势的语义着色。

| 取值 | 说明 |
|---|---|
| `Neutral` | 中性，用系列色 1。 |
| `Up` | 上行，绿色。 |
| `Down` | 下行，红色。 |

### `Sparkline` : `Control`

嵌在表格单元格里的迷你趋势。72×20，**无轴无标签** —— 它的作用是让人一眼看出形状，不是读数。要读数就该用 Readout。

**伪类**：`:up` · `:down`

| 属性 | 类型 | 说明 |
|---|---|---|
| `Values` | `IReadOnlyList<double>` | — |
| `Trend` | `SparklineTrend` | — |
| `ShowArea` | `bool` | — |

| 成员 | 说明 |
|---|---|
| `Render(DrawingContext context)` | — |

### `TrendChart` : `Control`

趋势图。自绘，不依赖图表库 —— 单通道 / 少通道的 strip chart 这样最轻， RK3568 这类板子上值得这么做。 **顶点数按绘图区宽度封顶。** 一个 8 小时班次按 1 Hz 采样是 28800 点/通道， 全量画的话 800 px 宽的绘图区上每像素列摊到 36 个顶点。渲染前走 min/max 抽稀， 每像素列只留极大和极小两个点——包络与全量一致，尖峰一个不丢， 见 `Decimate`。真要做缩放平移、多轴、上万通道时 再换 ScottPlot / LiveCharts2。 **十字线是 trackball 模式**：跟随指针的 X 坐标，同时给出所有系列在该时刻的值， 不是 hover 最近点。触摸屏上没有 hover，鼠标场景逐点 hover 也太累。 坐标轴单位不画在绘图区里 —— 画进去的话 °C 会压在最上一条刻度上、 秒会压在末位刻度上。单位放抬头（`ChartFrame` 的副标题）。

| 属性 | 类型 | 说明 |
|---|---|---|
| `Series` | `AvaloniaList<ChartSeries>` | — |
| `YMinimum` | `double` | — |
| `YMaximum` | `double` | — |
| `YTickCount` | `int` | — |
| `YFormat` | `string` | — |
| `XLabels` | `IReadOnlyList<string>` | — |
| `Setpoint` | `double?` | — |
| `Tolerance` | `double` | — |
| `AlarmHigh` | `double?` | — |
| `AlarmLow` | `double?` | — |
| `AlarmHighLabel` | `string?` | — |
| `IsTrackballEnabled` | `bool` | — |
| `TrackballIndex` | `int?` | 十字线当前落在第几个采样点上。没有十字线时是 null。图例绑它显示实时值。 |

| 成员 | 说明 |
|---|---|
| `MoveTrackballTo(Point point)` | 把十字线挪到某个点（控件坐标系）。点在绘图区外就清掉十字线。 单拎出来是为了：一是可测（不用伪造指针事件）， 二是键盘/编码器也能驱动十字线 —— 工业面板上不一定有鼠标。 |
| `ClearTrackball()` | 清掉十字线。 |
| `Render(DrawingContext context)` | — |

## 第 5 组 · 反馈

Avalonia 本体没有的那几个。

### `InfoSeverity`

徽章语义。和 `InfoBar` 用同一套。

| 取值 | 说明 |
|---|---|
| `Informational` | 中性提示，走强调色。 |
| `Success` | — |
| `Caution` | — |
| `Critical` | — |
| `Neutral` | 灰底，用于「无状态」「未启用」。 |

### `InfoBadge` : `TemplatedControl`

小徽章。默认「浅底 + 状态色文字」，明暗两套主题下对比度都成立。 `IsSolid` 是实底变体，用于导航计数这类需要强提示的场景。 实底变体的前景复用 `TextOnAccentFillColorPrimaryBrush` （浅色主题白 / 深色主题黑）—— 状态色和强调色遵循同一套明暗翻转逻辑， 写死白色的话深色主题下会糊在一起。

**伪类**：`:informational` · `:success` · `:caution` · `:critical` · `:neutral` · `:dot` · `:solid`

| 属性 | 类型 | 说明 |
|---|---|---|
| `Severity` | `InfoSeverity` | — |
| `Text` | `string?` | — |
| `IsDot` | `bool` | — |
| `IsSolid` | `bool` | — |

### `InfoBar` : `TemplatedControl`

页面内的持久提示条。Avalonia 本体没有这个控件。 和 `Toast` 的区别是它**占布局、不自动消失**—— 用于「这条信息在条件解除前一直成立」的场景。 需要立即处置的设备级报警用 `AlarmBanner`，那个走安全色。

**伪类**：`:informational` · `:success` · `:warning` · `:error`

| 属性 | 类型 | 说明 |
|---|---|---|
| `Severity` | `InfoBarSeverity` | — |
| `Title` | `string?` | — |
| `Message` | `string?` | — |
| `IsClosable` | `bool` | — |
| `IsOpen` | `bool` | 关掉之后整条不占布局（IsVisible=false），不是只隐藏内容。 |
| `ActionContent` | `object?` | — |
| `CloseCommand` | `ICommand?` | — |
| `Glyph` | `Symbol` | — |
| `Closed` | `event EventHandler<RoutedEventArgs>?` | — |

| 成员 | 说明 |
|---|---|
| `Close()` | 关闭。幂等。 |

### `InfoBarSeverity`

InfoBar 的四个级别：informational / success / warning / error。

| 取值 | 说明 |
|---|---|
| `Informational` | — |
| `Success` | — |
| `Warning` | — |
| `Error` | — |

### `ProgressRing` : `RangeBase`

转圈。Avalonia 本体没有这个控件。 嵌入式注意：这是个常驻动画，在 Mali 这类 GPU 上一直转是持续开销。 长时间加载建议改用 ProgressBar 的 indeterminate —— 那个只动 transform，代价低得多。

**伪类**：`:indeterminate` · `:determinate`

| 属性 | 类型 | 说明 |
|---|---|---|
| `IsIndeterminate` | `bool` | — |
| `StrokeThickness` | `double` | — |
| `EffectiveThickness` | `double` | 实际用的线宽。模板绑它。 |
| `SweepAngle` | `double` | 确定进度时值弧扫过的角度。模板绑它。 |

## 第 3 组 · 容器

### `Card` : `ContentControl`

卡片。半透明底 + 1px 描边 + 4px 圆角，**不加阴影**—— 阴影只留给悬浮层（ToolTip / Flyout / MenuFlyout / ContentDialog / TeachingTip）， 页面内元素一律靠描边分层。 `IsClickable` 打开后会响应悬停/按下。 可点击的卡片在高对比度下必须有可见描边，否则操作员看不出能点——别只靠背景色区分。

**伪类**：`:clickable`

| 属性 | 类型 | 说明 |
|---|---|---|
| `IsClickable` | `bool` | — |

### `SettingsCard` : `ContentControl`

设置项。左图标 + 标题/描述 + 右侧控件，min-height 68。 成组时放进 `SettingsGroup`：2px 缝隙、首尾圆角、中间不圆——Win11 设置的做法。

| 属性 | 类型 | 说明 |
|---|---|---|
| `Icon` | `Symbol` | — |
| `Header` | `string?` | — |
| `Description` | `string?` | — |

### `SettingsGroup` : `StackPanel`

设置项分组容器。缝隙 2px，首尾圆角、中间方角。 和 `JogGroup` 一样，靠给子项打类实现—— ControlTheme 里不允许出现子代选择器。

### `TabView` : `TabControl`

浏览器式标签页：每个标签自带关闭按钮，选中的那个「浮」到内容区上。 和 `TabControl` 的区别是语义：TabControl 的标签是**固定的视图切换**， TabView 的标签是**用户开出来的文档**，数量不定、可关闭、可能很多。

| 属性 | 类型 | 说明 |
|---|---|---|
| `IsAddButtonVisible` | `bool` | — |
| `AddCommand` | `ICommand?` | — |
| `TabCloseRequested` | `event EventHandler<TabCloseRequestedEventArgs>?` | 某个标签请求关闭。是否真的移除由使用方决定（可能要先提示保存）。 |

### `TabViewItem` : `TabItem`

TabView 里的一个标签。

**伪类**：`:closable`

| 属性 | 类型 | 说明 |
|---|---|---|
| `IsClosable` | `bool` | — |
| `Icon` | `Symbol` | — |

## 第 3 组 · 导航

### `NavigationViewPaneDisplayMode`

导航面板形态。

| 取值 | 说明 |
|---|---|
| `Left` | 展开，280 宽，图标 + 文字。 |
| `LeftCompact` | 收起，48 宽，只有图标。 |

### `NavigationView` : `TemplatedControl`

左侧导航。 项高 40，对齐 WinUI。 选中项左侧是 3×16 的指示条，不是整行变色。

**伪类**：`:compact`

| 属性 | 类型 | 说明 |
|---|---|---|
| `PaneDisplayMode` | `NavigationViewPaneDisplayMode` | — |
| `OpenPaneLength` | `double` | — |
| `CompactPaneLength` | `double` | — |
| `PaneLength` | `double` | 当前实际面板宽度。模板绑它。 |
| `MenuItems` | `AvaloniaList<Control>` | — |
| `FooterItems` | `AvaloniaList<Control>` | — |
| `Header` | `object?` | — |
| `Content` | `object?` | — |
| `SelectedItem` | `NavigationViewItem?` | — |
| `SelectionChanged` | `event EventHandler<RoutedEventArgs>?` | — |

### `NavigationViewItem` : `ContentControl`

导航项。

**伪类**：`:selected` · `:compact`

| 属性 | 类型 | 说明 |
|---|---|---|
| `Icon` | `Symbol` | — |
| `IsSelected` | `bool` | — |
| `Badge` | `object?` | — |

### `NavigationViewItemHeader` : `ContentControl`

导航分组标题。紧凑模式下隐藏。

### `NavigationViewItemSeparator` : `TemplatedControl`

导航分隔线。

## 第 6 组 · 弹出

悬浮层，全库仅有的能用阴影的地方。

### `ContentDialogResult`

对话框关掉的原因。

| 取值 | 说明 |
|---|---|
| `None` | 点了关闭按钮，或按了 Esc。 |
| `Primary` | — |
| `Secondary` | — |

### `ContentDialogButton`

哪个按钮是默认按钮（回车触发、accent 外观）。

| 取值 | 说明 |
|---|---|
| `None` | — |
| `Primary` | — |
| `Secondary` | — |
| `Close` | — |

### `ContentDialog` : `ContentControl`

模态对话框。Avalonia 本体没有这个控件。 用 `await dialog.ShowAsync(owner)` 拿结果，天然适合 MVVM 的 await 流程。 挂在 `OverlayLayer` 上，不进页面布局。 视觉上它是**实色**（SolidBackgroundFillColorBase），不是亚克力—— 亚克力留给 Flyout / MenuFlyout / ToolTip 那几层。 底部按钮等宽撑满，这是 Win11 的特征。

| 属性 | 类型 | 说明 |
|---|---|---|
| `Title` | `string?` | — |
| `PrimaryButtonText` | `string?` | — |
| `SecondaryButtonText` | `string?` | — |
| `CloseButtonText` | `string?` | — |
| `DefaultButton` | `ContentDialogButton` | — |
| `IsPrimaryDestructive` | `bool` | — |

| 成员 | 说明 |
|---|---|
| `ShowAsync(Visual owner)` | 弹出并等结果。 给视觉树上的任意一个元素即可， 用来找到所在窗口的 OverlayLayer。 |
| `Hide(ContentDialogResult result = ContentDialogResult.None)` | 用代码关掉对话框。 |

### `TeachingTipPlacement`

指向目标的方向。beak 画在对侧。

| 取值 | 说明 |
|---|---|
| `Top` | — |
| `Bottom` | — |
| `Left` | — |
| `Right` | — |

### `TeachingTip` : `TemplatedControl`

引导提示。带 beak（小尖角）指向目标控件。 和 `ToolTip` 的区别：ToolTip 是悬停即现、移开即走的补充说明； TeachingTip 是主动弹出、要用户确认的一次性引导，可以带操作按钮。

**伪类**：`:top` · `:bottom` · `:left` · `:right`

| 属性 | 类型 | 说明 |
|---|---|---|
| `Title` | `string?` | — |
| `Subtitle` | `string?` | — |
| `ActionButtonContent` | `string?` | — |
| `CloseButtonContent` | `string?` | — |
| `ActionCommand` | `ICommand?` | — |
| `Placement` | `TeachingTipPlacement` | 相对目标的位置。Bottom 表示提示在目标下方，beak 朝上。 |
| `IsOpen` | `bool` | — |
| `Closed` | `event EventHandler<RoutedEventArgs>?` | — |

| 成员 | 说明 |
|---|---|
| `Close()` | — |

## 第 6 组 · 命令栏

命令条与条上的按钮。

### `CommandBarLabelPosition`

AppBarButton 的标签位置。

| 取值 | 说明 |
|---|---|
| `Bottom` | 图标上、文字下。CommandBar 的默认形态。 |
| `Right` | 图标左、文字右。 |
| `Collapsed` | 只有图标。紧凑工具栏用。 |

### `CommandBar` : `ItemsControl`

命令栏。高 48，属于 base layer，所以**背景透明**—— 给它上底色会把两层结构（base / content）打乱。

| 属性 | 类型 | 说明 |
|---|---|---|
| `DefaultLabelPosition` | `CommandBarLabelPosition` | 子项没单独设置时用这个。设成 Collapsed 就是纯图标工具栏。 |

### `AppBarButton` : `Button`

命令栏里的按钮。无底色，靠 subtle 悬停反馈。

**伪类**：`:label-bottom` · `:label-right` · `:label-collapsed`

| 属性 | 类型 | 说明 |
|---|---|---|
| `Icon` | `Symbol` | — |
| `Label` | `string?` | — |
| `LabelPosition` | `CommandBarLabelPosition` | — |

### `AppBarSeparator` : `TemplatedControl`

命令栏里的竖分隔线，1×24。

## 第 8 组 · 日期时间

Avalonia 内置控件补不出来的那部分行为。

### `RangeCalendar` : `Calendar`

支持区间选择视觉的日历。 Avalonia 的 `Calendar` 在 `SelectionMode=SingleRange` 下会把区间里 每一天都标成 `:selected`，于是渲染出一串互不相连的圆点。 而规格要的是「中间段方角连成一条」：首尾各圆一边，中间方角、底色更淡。 这里按 `SelectedDates` 的首尾给日期格补三个伪类： `:rangestart` · `:inrange` · `:rangeend`，样式写在 DateTime.axaml 里。 单选（区间只有一天）时三个伪类都不加，走原本的 `:selected` 圆点。

## 第 10 / 11 组 · 表格增强与常用补充

### `BreadcrumbBar` : `ItemsControl`

面包屑。最后一项是当前位置，不可点。

| 属性 | 类型 | 说明 |
|---|---|---|
| `ItemClicked` | `event EventHandler<BreadcrumbClickedEventArgs>?` | — |

| 成员 | 说明 |
|---|---|
| `RefreshStates()` | 最后一项标记为当前位置。 |

### `BreadcrumbItem` : `ContentControl`

面包屑里的一段。

**伪类**：`:current`

| 属性 | 类型 | 说明 |
|---|---|---|
| `IsCurrent` | `bool` | — |

### `Chip` : `ContentControl`

可删除的筛选标签。高 24，胶囊形。 `IsClosable` 关掉就是纯展示标签（右侧内边距会补齐）。

**伪类**：`:closable` · `:accent`

| 属性 | 类型 | 说明 |
|---|---|---|
| `IsClosable` | `bool` | — |
| `IsAccent` | `bool` | — |
| `CloseCommand` | `ICommand?` | — |
| `Closed` | `event EventHandler<RoutedEventArgs>?` | — |

### `DataGridToolbar` : `TemplatedControl`

表格工具条。左侧动作、右侧计数。 计数要 tabular-nums —— 筛选时数字一直在变，比例数字会让整条右端抖动。

| 属性 | 类型 | 说明 |
|---|---|---|
| `Items` | `AvaloniaList<Control>` | — |
| `CountText` | `string?` | — |

### `EmptyState` : `TemplatedControl`

空状态。列表没有数据、筛选没有命中时用。 必须给出**下一步动作**（`ActionContent`）——只说「没有数据」是把问题丢回给操作员。

| 属性 | 类型 | 说明 |
|---|---|---|
| `Icon` | `Symbol` | — |
| `Title` | `string?` | — |
| `Description` | `string?` | — |
| `ActionContent` | `object?` | 下一步动作。别只说「没有数据」，要给一条出路。 |
| `ActionCommand` | `ICommand?` | — |

### `Pagination` : `TemplatedControl`

分页。 一句话提醒：**数据量大时别做客户端分页。** 一两千条还行，上万条要走服务端分页 + 虚拟化。

| 属性 | 类型 | 说明 |
|---|---|---|
| `PageCount` | `int` | — |
| `CurrentPage` | `int` | 当前页，从 1 开始。 |
| `TotalItems` | `int` | — |
| `SiblingCount` | `int` | — |
| `InfoText` | `string?` | 左侧那句「共 N 条 · 第 X / Y 页」。 |
| `Pages` | `IReadOnlyList<int?>` | 要渲染的页码序列。`null` 表示一个省略号。 |

### `PersonPictureSize`

头像尺寸档。

| 取值 | 说明 |
|---|---|
| `Small` | — |
| `Medium` | — |
| `Large` | — |

### `PersonPicture` : `TemplatedControl`

头像。有图用图，没图用姓名首字缩写。 中文名取姓（第一个字），英文名取首字母，最多两位。

**伪类**：`:small` · `:medium` · `:large` · `:neutral`

| 属性 | 类型 | 说明 |
|---|---|---|
| `DisplayName` | `string?` | — |
| `Source` | `IImage?` | — |
| `Size` | `PersonPictureSize` | — |
| `IsNeutral` | `bool` | — |
| `Initials` | `string` | 姓名缩写。模板在没有图片时显示它。 |

### `SegmentedControl` : `SelectingItemsControl`

互斥模式选择。比 RadioButton 醒目，适合 手动 / 自动 / 维护 这种一屏常驻的模式切换。 就是一个 `SelectingItemsControl`，SelectionMode=Single； 外观差异全在 ControlTheme 里。

### `SegmentedItem` : `ListBoxItem`

SegmentedControl 里的一段。

### `Skeleton` : `TemplatedControl`

加载占位条。比转圈好——它预示了内容的形状。 嵌入式注意：微光是常驻动画，一屏十几条会有开销。 RK3568 这类设备上把 `IsShimmerEnabled` 关掉（变静态灰条）， 或者只给前几行开。

**伪类**：`:shimmer`

| 属性 | 类型 | 说明 |
|---|---|---|
| `IsShimmerEnabled` | `bool` | — |

### `StepState`

步骤状态。

| 取值 | 说明 |
|---|---|
| `Pending` | 还没走到。 |
| `Done` | 已完成，显示对勾。 |
| `Current` | 当前步。 |
| `Error` | 这一步失败了。 |

### `Stepper` : `ItemsControl`

多步流程指示（配方向导、标定流程）。 `CurrentIndex` 一改，各步的状态自动推出来：之前的 Done、当前 Current、之后 Pending。 某一步失败就把它的 `IsError` 打开。

| 属性 | 类型 | 说明 |
|---|---|---|
| `CurrentIndex` | `int` | — |

| 成员 | 说明 |
|---|---|
| `RefreshStates()` | 按 `CurrentIndex` 推出每一步的状态。失败态由各步自己声明，优先级最高。 |

### `StepperItem` : `ContentControl`

Stepper 里的一步。

**伪类**：`:pending` · `:done` · `:current` · `:error` · `:last`

| 属性 | 类型 | 说明 |
|---|---|---|
| `State` | `StepState` | — |
| `IsError` | `bool` | 这一步失败了。优先于 Stepper 推出来的状态。 |
| `StepNumber` | `int` | — |
| `IsLast` | `bool` | 最后一步不画后面的连接线。 |

### `Toast` : `TemplatedControl`

瞬时通知。和 `InfoBar` 的区别是它会自动消失、且**不占布局**—— 挂在 OverlayLayer 上。 停留 4 秒；带操作按钮的延到 8 秒（人要先读懂再决定点不点）。

**伪类**：`:informational` · `:success` · `:error`

| 属性 | 类型 | 说明 |
|---|---|---|
| `Severity` | `InfoBarSeverity` | — |
| `Title` | `string?` | — |
| `Message` | `string?` | — |
| `ActionContent` | `object?` | — |
| `Glyph` | `Symbol` | — |

### `ToastHost` : `ItemsControl`

Toast 的承载层。挂在窗口的 OverlayLayer 上，右下角堆叠，不进页面布局。 ` ToastHost.Show(this, new Toast { Title = "配方已保存" }); `

| 成员 | 说明 |
|---|---|
| `Show(Visual owner, Toast toast, TimeSpan? duration = null)` | 在 所在窗口弹一条 Toast。 |

## 基础

图标系统等。

### `Symbol`

图标名。全库用到的 38 个字形，全部画成矢量路径。 为什么不直接用字体：目标平台里有嵌入式 Linux（RK3568 那类）， 上面没有 Segoe Fluent Icons，用字体会渲染成豆腐块。 所以本库的图标全部是矢量路径，跨平台像素一致，也不用往安装包里塞字体。 手上确实有那套字体的话，用 `Glyph` 直接给码位。

| 取值 | 说明 |
|---|---|
| `None` | — |
| `ChevronDown` | — |
| `ChevronUp` | — |
| `ChevronLeft` | — |
| `ChevronRight` | — |
| `Add` | — |
| `Subtract` | — |
| `Cancel` | — |
| `More` | — |
| `Settings` | — |
| `Stop` | — |
| `EmergencyStop` | — |
| `Filter` | — |
| `Search` | — |
| `Refresh` | — |
| `Blocked` | — |
| `Checkbox` | — |
| `CheckMark` | — |
| `Save` | — |
| `Play` | — |
| `Pause` | — |
| `Contact` | — |
| `Calendar` | — |
| `Warning` | — |
| `Home` | — |
| `Download` | — |
| `Document` | — |
| `Sort` | — |
| `Folder` | — |
| `Speed` | — |
| `Completed` | — |
| `Info` | — |
| `Tune` | — |
| `Pulse` | — |
| `Diagnostic` | — |
| `Error` | — |
| `GlobalNav` | — |
| `Brightness` | — |
| `Backspace` | 退格。数字键盘用；线型轮廓内嵌一个叉，和 Fluent 的画法一致。 |

### `SymbolIcon` : `Control`

图标。默认画矢量路径，不依赖 Segoe Fluent Icons —— 嵌入式 Linux 上没有那套字体， 靠字体会渲染成豆腐块。 ` &lt;fc:SymbolIcon Symbol="Save" /&gt; &lt;fc:SymbolIcon Symbol="Warning" FontSize="20" Foreground="{DynamicResource SystemFillColorCautionBrush}" /&gt; ` 确实装了那套字体、想用字体渲染的话，把 `UseGlyphFont` 打开； 或者给 `Glyph` 一个自定义码位。

| 属性 | 类型 | 说明 |
|---|---|---|
| `Symbol` | `Symbol` | — |
| `Glyph` | `string?` | — |
| `UseGlyphFont` | `bool` | — |
| `FontSize` | `double` | 图标边长。16 是控件内的默认档，CommandBar 用 16，SettingsCard 用 20。 |
| `Foreground` | `IBrush?` | — |
| `StrokeThickness` | `double` | — |

| 成员 | 说明 |
|---|---|
| `Render(DrawingContext context)` | — |
