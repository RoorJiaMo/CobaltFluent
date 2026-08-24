#!/usr/bin/env python3
"""控件层静默失效审计。

这个工具不做代码风格检查，只针对一类特定问题：**编译通过、测试也绿、
但某条判定在真实路径上整个不成立，且失效方向朝着「一切正常」**。

每一条检查都对应一个在本仓库真实发生过的缺陷（见各检查的 docstring）。
新加检查的门槛是：能举出一个它本该抓到的历史缺陷，且在当前代码上零误报。

用法：
    python3 tools/audit.py            # 有问题时退出码 1
    python3 tools/audit.py --list     # 只列出各检查覆盖的历史缺陷

例外走 tools/audit-allow.txt，每条必须写清楚为什么可以放行。
"""
import glob
import io
import json
import os
import re
import sys
import xml.etree.ElementTree as ET

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
LIB = os.path.join(ROOT, "src/Cobalt.Fluent")
CONTROLS = os.path.join(LIB, "Controls")
THEMES = os.path.join(LIB, "Themes")
ALLOW_FILE = os.path.join(ROOT, "tools/audit-allow.txt")

# Avalonia.Themes.Fluent 提供的资源键。本库引用但不定义它们是正常的；
# 列在这里而不是放宽检查，是为了让「本库到底依赖了外部哪些键」这件事可查。
AVALONIA_KEYS = {
    "ScrollBarSize",
    "StringDatePickerDayText", "StringDatePickerMonthText", "StringDatePickerYearText",
    "StringTimePickerHourText", "StringTimePickerMinuteText", "StringTimePickerSecondText",
    "StringTextFlyoutCopyText", "StringTextFlyoutCutText", "StringTextFlyoutPasteText",
}

findings = []


def report(check, path, line, key, message):
    """key 是这条发现的稳定标识（伪类名 / 属性名 / 部件名……）。

    例外表按 `检查名 路径 key` 匹配而不是按行号——行号一挪就失配、
    告警重新冒出来，那正是 lint 工具被人关掉的典型原因。
    """
    findings.append((check, os.path.relpath(path, ROOT), line, key, message))


def cs_files(where=CONTROLS):
    return sorted(glob.glob(os.path.join(where, "**/*.cs"), recursive=True))


def axaml_files(where=THEMES):
    return sorted(glob.glob(os.path.join(where, "**/*.axaml"), recursive=True))


def read(path):
    return io.open(path, encoding="utf-8").read()


def line_of(text, index):
    return text.count("\n", 0, index) + 1


# ---------------------------------------------------------------------------


def check_parts():
    """模板部件名对账。

    历史缺陷：ParameterRow.OnApplyTemplate 找 PART_Revert，而模板里只有
    PART_Apply——_revertButton 永远是 null，Revert() 与公开的 RevertCommand
    在默认主题下从界面上完全不可达。编译通过，测试全绿，界面上少一整个功能。
    """
    declared = set()
    for path in axaml_files():
        declared |= set(re.findall(r'x:Name="(PART_[^"]+)"', read(path)))

    for path in cs_files():
        text = read(path)
        for m in re.finditer(r'NameScope\.Find<[^>]+>\("(PART_[^"]+)"\)', text):
            if m.group(1) not in declared:
                report("parts", path, line_of(text, m.start()), m.group(1),
                       f"{m.group(1)} 在任何主题模板里都不存在，查找结果恒为 null")


def check_pseudo_declared():
    """PseudoClasses.Set 用到的伪类必须在 [PseudoClasses] 里声明。

    Avalonia 不校验这个，漏声明不会报错，但文档生成、展柜对照表和后来人
    都以那份声明为准——声明与实际置位脱节之后，没人知道还有哪些状态。
    """
    for path in cs_files():
        text = read(path)
        for cls in re.finditer(
                r'\[PseudoClasses\(([^\]]*)\)\]\s*\npublic\s+(?:sealed\s+|abstract\s+)?class\s+(\w+)',
                text):
            declared = set(re.findall(r'"(:[^"]+)"', cls.group(1)))
            body = text[cls.end():]
            nxt = re.search(r'\n\[PseudoClasses\(|\npublic\s+(?:sealed\s+|abstract\s+)?class\s', body)
            if nxt:
                body = body[:nxt.start()]
            for m in re.finditer(r'PseudoClasses\.Set\("(:[^"]+)"', body):
                if m.group(1) not in declared:
                    report("pseudo-declared", path, line_of(text, cls.end() + m.start()),
                           f"{cls.group(2)}{m.group(1)}",
                           f"{cls.group(2)} 置位了 {m.group(1)} 但没有在 [PseudoClasses] 里声明")


def check_pseudo_styled():
    """声明的伪类必须至少有一条主题选择器用到。

    历史缺陷：加了伪类、控件也正确置位，主题里却没有对应样式——
    「开关」在代码层面完全正常，界面上毫无变化。本轮修复中新增的
    :stopfailed / :engagefailed / :unknownage / :invalid 全部属于这一类风险。
    """
    selectors = "\n".join(read(p) for p in axaml_files())
    for path in cs_files():
        text = read(path)
        for cls in re.finditer(
                r'\[PseudoClasses\(([^\]]*)\)\]\s*\npublic\s+(?:sealed\s+|abstract\s+)?class\s+(\w+)',
                text):
            for pseudo in re.findall(r'"(:[^"]+)"', cls.group(1)):
                if pseudo not in selectors:
                    report("pseudo-styled", path, line_of(text, cls.start()),
                           f"{cls.group(2)}{pseudo}",
                           f"{cls.group(2)} 声明了 {pseudo}，但没有任何主题选择器用到它")


def check_resources():
    """引用的资源键必须有定义。

    键不存在时 Avalonia 静默回落，不报错也不提示——颜色、尺寸悄悄变成
    默认值，只有肉眼比对才可能发现。
    """
    defined = set()
    for path in axaml_files():
        defined |= set(re.findall(r'x:Key="([^"]+)"', read(path)))
    defined |= AVALONIA_KEYS

    for path in axaml_files():
        text = read(path)
        for m in re.finditer(r'\{(?:Dynamic|Static)Resource\s+([A-Za-z0-9_.]+)\}', text):
            if m.group(1) not in defined:
                report("resources", path, line_of(text, m.start()), m.group(1),
                       f"资源键 {m.group(1)} 没有定义，运行时会静默回落到默认值")


def _strip_ns(tag):
    return tag.split("}")[-1] if "}" in tag else tag


def _walk(node, ancestors, fn):
    for child in node:
        fn(child, ancestors)
        _walk(child, ancestors + [_strip_ns(child.tag)], fn)


def check_transform_setter():
    """Transform 子属性的 Setter 只有 KeyFrame 认。

    历史缺陷：StatusIndicator 的静态外环用
    <Setter Property="ScaleTransform.ScaleX" Value="1.3" /> 写在普通 Style 里，
    不报错、不生效，外环一直停在模板字面值 0.7——比圆点还小，藏在里面看不见。
    """
    for path in axaml_files():
        text = read(path)
        try:
            tree = ET.fromstring(text)
        except ET.ParseError as e:
            report("transform-setter", path, 0, "parse", f"XML 解析失败：{e}")
            continue

        def visit(node, ancestors):
            if _strip_ns(node.tag) != "Setter":
                return
            prop = node.get("Property", "")
            if not re.match(r"^[A-Za-z]+Transform\.", prop):
                return
            if "KeyFrame" not in ancestors:
                snippet = f'Setter Property="{prop}"'
                idx = text.find(snippet)
                report("transform-setter", path, line_of(text, idx) if idx >= 0 else 0, prop,
                       f'{prop} 写在普通 Setter 里不生效（只有 KeyFrame 认），'
                       f"应整体替换 RenderTransform")

        _walk(tree, [_strip_ns(tree.tag)], visit)


def check_animation_override():
    """动画会盖掉同目标上更具体选择器里的 Setter。

    历史缺陷：StatusIndicator 的 ^:running 挂着无限循环动画，
    ^:running[IsPulseEnabled=False] 只是再设一次静态值——Avalonia 里动画优先级
    高于 Setter，只要 :running 匹配，动画就一直跑，「关掉脉冲」实际没关掉。
    """
    for path in axaml_files():
        text = read(path)
        try:
            tree = ET.fromstring(text)
        except ET.ParseError:
            continue

        animated = []
        plain = []

        def visit(node, ancestors):
            if _strip_ns(node.tag) != "Style":
                return
            sel = node.get("Selector")
            if not sel:
                return
            has_anim = any(_strip_ns(c.tag) == "Style.Animations" for c in node)
            (animated if has_anim else plain).append(sel)

        _walk(tree, [_strip_ns(tree.tag)], visit)

        for a in animated:
            for p in plain:
                # ^:running  vs  ^:running[IsPulseEnabled=False]
                if p.startswith(a.split(" /template/")[0]) and p != a and "[" in p:
                    idx = text.find(f'Selector="{a}"')
                    report("animation-override", path, line_of(text, idx) if idx >= 0 else 0, a,
                           f'"{a}" 带动画，会盖掉 "{p}" 里的 Setter——'
                           f"动画所在选择器需要一并限定该属性")


def check_keydown_modifiers():
    """OnKeyDown 覆写必须检查 e.KeyModifiers。

    历史缺陷：JogButton / EStopButton / NumericKeypad 三处都不查修饰键，
    Ctrl+Space 直接让轴运动、Ctrl+Enter 触发急停与解锁、Ctrl+Enter 向设备下发，
    同时把应用级快捷键无声吞掉。
    """
    for path in cs_files():
        text = read(path)
        for m in re.finditer(r'protected\s+override\s+void\s+OnKeyDown\s*\(', text):
            body = text[m.start():m.start() + 2000]
            end = body.find("\n    }")
            if end > 0:
                body = body[:end]
            if "KeyModifiers" not in body:
                report("keydown-modifiers", path, line_of(text, m.start()), "OnKeyDown",
                       "OnKeyDown 不检查 e.KeyModifiers：组合键会触发控件动作，"
                       "同时吞掉应用级快捷键")


def check_timer_cleanup():
    """持有 DispatcherTimer 的控件必须覆写 OnDetachedFromVisualTree。

    历史缺陷：EStopButton 是第 7 组里唯一带定时器却不做卸载清理的控件，
    长按复位定时器在控件离开可视树后仍然存活并到点执行 DoReset()——
    急停会在界面上已经看不到它的时候自行解锁。
    """
    for path in cs_files():
        text = read(path)
        if not re.search(r'\bDispatcherTimer\??\s+_\w+', text):
            continue
        if "OnDetachedFromVisualTree" in text:
            continue
        m = re.search(r'\bDispatcherTimer\??\s+_\w+', text)
        report("timer-cleanup", path, line_of(text, m.start()), "DispatcherTimer",
               "持有 DispatcherTimer 但不覆写 OnDetachedFromVisualTree："
               "定时器会在控件卸载后继续触发")


def check_wallclock():
    """控件层不应拿墙钟 DateTime.Now 做差值判定。

    历史缺陷：Readout 的过期判定与 Heartbeat 的停跳判定都用 DateTime.Now 相减。
    墙钟不单调——无 RTC 的嵌入式 HMI 开机后 NTP 校时、夏令时切换、手工改表
    都会让差值变负，判定条件恒不成立：回拨多久，就有多久所有读数被判成新鲜。
    超时判定应当用 Environment.TickCount64。
    """
    for path in cs_files():
        text = read(path)
        for m in re.finditer(r'DateTime\.Now\s*-|-\s*DateTime\.Now', text):
            # key 用所在方法名：行号会挪，方法名不会
            method = "?"
            for dm in re.finditer(r"\n\s+(?:private|public|protected|internal)[^\n(]*?(\w+)\s*\(", text[:m.start()]):
                method = dm.group(1)
            report("wallclock", path, line_of(text, m.start()), method,
                   "用墙钟 DateTime.Now 做差值：系统时间回拨会让判定失效，"
                   "超时类判定应改用 Environment.TickCount64")


def check_unread_property():
    """公开属性必须至少有一处读取。

    历史缺陷：JogButton.RequiresConfirm 有声明、有 XML 文档、在 docs/CONTROLS.md
    和展柜属性表里都作为公开 API 列出，而全库没有任何一处读它——危险轴上
    打开它跟没打开一样，比没有这个属性更糟。EStopButton.HardwareLocationHint
    同理：属性在、文档写了，模板里没有任何地方显示它。
    """
    blobs = [read(p) for p in cs_files(LIB) + axaml_files(LIB)]
    all_text = "\n".join(blobs)

    decl = re.compile(r'^\s{4}public\s+(?:static\s+readonly\s+)?[\w<>?\[\], .]+?\s+(\w+)\s*$', re.M)
    for path in cs_files():
        text = read(path)
        for m in decl.finditer(text):
            name = m.group(1)
            if name.endswith("Property") or name.endswith("Event"):
                continue
            # CLR 事件包装器（public event ... { add; remove; }）天然只给使用方订阅，
            # 内部不读是正常的，不属于本检查的目标。
            if re.match(r"\s{4}public\s+event\b", m.group(0)):
                continue

            region = _own_region(text, m.start(), name)

            # 同时数裸名和 XxxProperty：属性也可能只通过
            # XxxProperty.Changed.AddClassHandler 驱动伪类（Chip.IsAccent 就是），
            # 那条读取路径里不出现裸名。声明区已经把字段声明本身扣掉了。
            pattern = r"\b" + re.escape(name) + r"(?:Property)?\b"
            total = len(re.findall(pattern, all_text))
            own = len(re.findall(pattern, region))
            if total - own <= 0:
                report("unread-property", path, line_of(text, m.start()), name,
                       f"公开属性 {name} 在全库没有任何读取点：声明、文档、"
                       f"对照表都有，而它不影响任何行为")


def _own_region(text, decl_start, name):
    """属性自身的声明区：XxxProperty / XxxEvent 字段那条语句 + 属性体本身。

    收集范围必须按**语句**取而不是按「包含 XxxProperty 的行」取——
    注册语句常常折行，`nameof(Xxx)` 落在第二行上，那一行并不含 XxxProperty，
    于是被算成外部引用，属性看起来「有人读」。这个 off-by-one 曾经让本检查
    漏掉 JogButton.RequiresConfirm（声明、文档、对照表俱全，全库无人读取）。
    """
    region = ""

    for suffix in ("Property", "Event"):
        fm = re.search(
            r"public\s+static\s+readonly\s+[^;]*?\b" + re.escape(name) + suffix + r"\b[^;]*;",
            text, re.S)
        if fm:
            region += fm.group(0) + "\n"

    # 属性 / 事件体：从声明行往后花括号配对
    brace = text.find("{", decl_start)
    if brace >= 0 and text[decl_start:brace].count(";") == 0:
        depth, i = 0, brace
        while i < len(text):
            if text[i] == "{":
                depth += 1
            elif text[i] == "}":
                depth -= 1
                if depth == 0:
                    break
            i += 1
        region += text[decl_start:i + 1]
    else:
        region += text[decl_start:text.find("\n", decl_start) + 1]

    return region


def _class_body(text, start):
    """从类声明处按花括号配对取出类体。返回 (body, end_index)。

    只能配对，不能用「下一个 `^}`」——嵌套类和文件末尾的枚举都会让后者错位。
    """
    brace = text.find("{", start)
    if brace < 0:
        return "", len(text)
    depth, i = 0, brace
    while i < len(text):
        if text[i] == "{":
            depth += 1
        elif text[i] == "}":
            depth -= 1
            if depth == 0:
                return text[brace:i + 1], i
        i += 1
    return text[brace:], len(text)


def check_automation_peer():
    """直接继承 Control / TemplatedControl 的控件必须覆写 OnCreateAutomationPeer。

    历史缺陷：本库 25 个直接继承 Control / TemplatedControl 的控件全部没有对等体，
    在 Inspect 和 UI Automation 客户端里退化成一团没有名字的 Custom 矩形。
    工业 HMI 的验收脚本普遍用 UI Automation 驱动界面跑回归，使用方的脚本
    根本抓不到这些控件——而界面上完全看不出来。

    只查直接继承这两个基类的：从 Button / RangeBase / TabItem 派生的控件
    继承到的是 Avalonia 自己的对等体，本来就有名字有类型，不在本检查范围。

    装饰性元素也要覆写——用 DecorativeAutomationPeer 显式退出自动化树，
    比默认落到无名 Custom 节点更明确。
    """
    decl = re.compile(
        r"^public\s+(?:sealed\s+)?(?:abstract\s+)?class\s+(\w+)\s*:\s*"
        r"(?:Avalonia\.Controls\.(?:Primitives\.)?)?(TemplatedControl|Control)\b", re.M)
    for path in cs_files():
        text = read(path)
        for m in decl.finditer(text):
            name = m.group(1)
            body, _ = _class_body(text, m.end())
            if "OnCreateAutomationPeer" in body:
                continue
            report("automation-peer", path, line_of(text, m.start()), name,
                   f"{name} 直接继承 {m.group(2)} 却没有覆写 OnCreateAutomationPeer："
                   f"自动化客户端只能看到一个没有名字的 Custom 矩形")


# ---------------------------------------------------------------------------
# 对比度
# ---------------------------------------------------------------------------

# 要检查的前景/背景对，以及各自的门槛。
#
# 这张表是手写的而不是从主题里自动抽的：自动抽出来的对里绝大多数是装饰性的
# （分隔线压在底色上本来就该淡），真问题会淹在几百条噪音里。手写意味着每一条
# 都能说出「这两个颜色为什么会同时出现在屏幕上、看不清会怎样」。
#
# 门槛按 WCAG 2.1：正文 4.5（1.4.3 AA），图形与控件边界 3.0（1.4.11）。
CONTRAST_PAIRS = [
    # --- 正文 -------------------------------------------------------------
    ("TextFillColorPrimary", "SolidBackgroundFillColorBase", 4.5),
    ("TextFillColorSecondary", "SolidBackgroundFillColorBase", 4.5),
    ("TextFillColorStale", "SolidBackgroundFillColorBase", 4.5),
    ("TextFillColorPrimary", "CardBackgroundFillColorDefault", 4.5),
    ("TextFillColorSecondary", "CardBackgroundFillColorDefault", 4.5),
    ("TextFillColorPrimary", "ControlFillColorDefault", 4.5),
    ("TextOnAccentFillColorPrimary", "AccentFillColorDefault", 4.5),
    ("AccentTextFillColorPrimary", "SolidBackgroundFillColorBase", 4.5),

    # --- 状态色配自己的底 --------------------------------------------------
    ("SystemFillColorSuccess", "SystemFillColorSuccessBackground", 4.5),
    ("SystemFillColorCaution", "SystemFillColorCautionBackground", 4.5),
    ("SystemFillColorCritical", "SystemFillColorCriticalBackground", 4.5),
    ("SystemFillColorSuccess", "SolidBackgroundFillColorBase", 4.5),
    ("SystemFillColorCaution", "SolidBackgroundFillColorBase", 4.5),
    ("SystemFillColorCritical", "SolidBackgroundFillColorBase", 4.5),

    # --- 安全色 -----------------------------------------------------------
    #
    # 这一组的每一条都对应一个真实缺陷。全库对比度最差的几处恰好都在这里，
    # 而这里正是最不能看不清的地方：
    #
    #   白字压 SafetyRedHigh（深色 3.42）—— 已触发的急停，钮面上的图标和字；
    #   安全黄压 SafetyRed（深色 2.69）—— 关掉呼吸动画后用来区分 Alarm 与
    #     Warning 的那圈补偿描边，降级态的全部依据；
    #   白字压 SafetyYellow（1.72）—— JogButton :stopfailed 的标签，
    #     那句话是「停止指令没能下发」；
    #   SafetyYellow 压页面底（浅色 1.55）—— EStopButton :engagefailed 的黄环，
    #     整个库里最不能看不见的那个提示。
    ("TextOnSafetyFillColorPrimary", "SafetyRed", 4.5),
    ("TextOnSafetyFillColorPrimary", "SafetyRedHigh", 4.5),
    ("TextOnSafetyYellowFillColorPrimary", "SafetyYellow", 4.5),
    ("SafetyRed", "SolidBackgroundFillColorBase", 3.0),
    ("SafetyRedHigh", "SolidBackgroundFillColorBase", 3.0),
    ("SafetyYellow", "SafetyRed", 3.0),
    ("SafetyYellow", "SafetyRedHigh", 3.0),

    # --- 图形 -------------------------------------------------------------
    ("ControlStrongStrokeColorDefault", "SolidBackgroundFillColorBase", 3.0),
    ("FocusStrokeColorOuter", "SolidBackgroundFillColorBase", 3.0),
] + [(f"ChartSeries{i}", "SolidBackgroundFillColorBase", 3.0) for i in range(1, 9)]


def _srgb(hex8):
    """#AARRGGBB → (a, r, g, b)，各 0..255。"""
    return (int(hex8[1:3], 16), int(hex8[3:5], 16),
            int(hex8[5:7], 16), int(hex8[7:9], 16))


def _over(top, bottom):
    """半透明色合成到不透明底上。返回 (r, g, b)。

    调色板里大量的键是半透明的（#B2FFFFFF 这种），不合成就算不出对比度。
    """
    a, r, g, b = _srgb(top)
    _, br, bg, bb = _srgb(bottom)
    t = a / 255
    return (round(r * t + br * (1 - t)),
            round(g * t + bg * (1 - t)),
            round(b * t + bb * (1 - t)))


def _luminance(rgb):
    """WCAG 2.1 相对亮度。"""
    def channel(c):
        c /= 255
        return c / 12.92 if c <= 0.03928 else ((c + 0.055) / 1.055) ** 2.4
    r, g, b = rgb
    return 0.2126 * channel(r) + 0.7152 * channel(g) + 0.0722 * channel(b)


def _ratio(fg, bg, colors, theme):
    """fg 压在 bg 上的对比度。两者都先合成到页面底色。"""
    page = colors["SolidBackgroundFillColorBase"][theme]
    bg_rgb = _over(colors[bg][theme], page)
    bg_hex = "#FF%02X%02X%02X" % bg_rgb
    fg_rgb = _over(colors[fg][theme], bg_hex)

    hi, lo = sorted((_luminance(fg_rgb), _luminance(bg_rgb)), reverse=True)
    return (hi + 0.05) / (lo + 0.05)


def check_contrast():
    """成对出现的颜色必须达到 WCAG 的对比度门槛。

    历史缺陷：安全色一组有五处不达标，而那正是最不能看不清的地方——
    已触发急停的钮面白字在深色主题下 3.42:1；关掉呼吸动画后用来区分 Alarm 与
    Warning 的安全黄描边压在安全红上只有 2.69:1（降级态就靠这一圈）；
    「停止指令没能下发」的标签是白字压安全黄，1.72:1；而 EStopButton
    :engagefailed 的黄环在浅色主题下对页面底只有 1.55:1，等于不存在。

    这一类缺陷编译得过、测试也绿、在开发机的好屏幕上也看得见——
    到车间的强光屏上才露出来，而那时它挡的是安全信息。
    """
    palette = os.path.join(ROOT, "tools/palette.json")
    groups = json.loads(read(palette))
    colors = {k: v for entries in groups.values() for k, v in entries.items()}

    for fg, bg, need in CONTRAST_PAIRS:
        missing = [k for k in (fg, bg) if k not in colors]
        if missing:
            report("contrast", palette, 1, fg,
                   f"对照表引用了不存在的颜色键：{', '.join(missing)}")
            continue

        for theme in ("light", "dark"):
            got = _ratio(fg, bg, colors, theme)
            if got + 0.005 < need:
                report("contrast", palette, 1, f"{fg}/{bg}/{theme}",
                       f"{theme} 主题下 {fg} 压在 {bg} 上只有 {got:.2f}:1，"
                       f"低于 {need}:1")


CHECKS = [
    ("parts", check_parts),
    ("pseudo-declared", check_pseudo_declared),
    ("pseudo-styled", check_pseudo_styled),
    ("resources", check_resources),
    ("transform-setter", check_transform_setter),
    ("animation-override", check_animation_override),
    ("keydown-modifiers", check_keydown_modifiers),
    ("timer-cleanup", check_timer_cleanup),
    ("wallclock", check_wallclock),
    ("unread-property", check_unread_property),
    ("automation-peer", check_automation_peer),
    ("contrast", check_contrast),
]


def load_allow():
    """例外表。每行 `检查名 路径 key`，# 后面写理由（必填）。"""
    allow = set()
    if not os.path.exists(ALLOW_FILE):
        return allow
    for raw in io.open(ALLOW_FILE, encoding="utf-8"):
        line = raw.split("#")[0].strip()
        if not line:
            continue
        parts = line.split()
        if len(parts) != 3:
            print(f"audit-allow.txt 格式错误（应为「检查名 路径 key」）：{raw.strip()}",
                  file=sys.stderr)
            continue
        allow.add(tuple(parts))
    return allow


def main():
    if "--list" in sys.argv:
        for name, fn in CHECKS:
            doc = (fn.__doc__ or "").strip().split("\n")[0]
            print(f"{name:20s} {doc}")
        return 0

    for _, fn in CHECKS:
        fn()

    allow = load_allow()
    kept = []
    for check, path, line, key, msg in findings:
        if (check, path, key) in allow:
            continue
        kept.append((check, path, line, key, msg))

    if not kept:
        print(f"控件层审计通过（{len(CHECKS)} 项检查，"
              f"放行 {len(findings) - len(kept)} 处已登记例外）。")
        return 0

    print(f"控件层审计发现 {len(kept)} 处问题：\n")
    for check, path, line, key, msg in sorted(kept):
        print(f"  [{check}] {path}:{line}  ({key})")
        print(f"      {msg}\n")
    print("确属可接受的，登记到 tools/audit-allow.txt 并写明理由。")
    return 1


if __name__ == "__main__":
    sys.exit(main())
