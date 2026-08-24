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
