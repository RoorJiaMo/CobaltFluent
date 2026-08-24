#!/usr/bin/env python3
"""
从 src/Cobalt.Fluent/Controls/**.cs 抽出公开 API，生成 docs/CONTROLS.md。

手写 API 文档一定会和代码漂移，所以这里直接从源码抽：
类的 <summary>、依赖属性的类型和说明、公开方法、[PseudoClasses] 里的伪类清单。
改了控件重跑一次即可。
"""
import pathlib
import re

ROOT = pathlib.Path(__file__).resolve().parent.parent
SRC = ROOT / "src/Cobalt.Fluent/Controls"
OUT = ROOT / "docs/CONTROLS.md"

# 分组和 Controls/ 下的目录一一对应
GROUPS = [
    ("Hmi", "第 7 组 · 工业 HMI 专用", "WinUI / FluentAvalonia 里不存在，全部新写。涉及人身和设备安全。"),
    ("Charts", "第 9 组 · 图表", "自绘，零第三方依赖。视觉规格直接实现，不是喂给别的库的配置。"),
    ("Feedback", "第 5 组 · 反馈", "Avalonia 本体没有的那几个。"),
    ("Layout", "第 3 组 · 容器", ""),
    ("Navigation", "第 3 组 · 导航", ""),
    ("Overlays", "第 6 组 · 弹出", "悬浮层，全库仅有的能用阴影的地方。"),
    ("Commands", "第 6 组 · 命令栏", "命令条与条上的按钮。"),
    ("DateTimeControls", "第 8 组 · 日期时间", "Avalonia 内置控件补不出来的那部分行为。"),
    ("Common", "第 10 / 11 组 · 表格增强与常用补充", ""),
    (".", "基础", "图标系统等。"),
]


def strip_doc(lines):
    """把 /// <summary> 里的正文抽出来，压成一段。"""
    text = " ".join(
        re.sub(r"^\s*///\s?", "", line).strip() for line in lines
    )
    m = re.search(r"<summary>(.*?)</summary>", text, re.S)
    if not m:
        return ""
    body = m.group(1)
    body = re.sub(r"<see cref=\"([^\"]+)\"\s*/>", lambda x: f"`{x.group(1).split('.')[-1]}`", body)
    body = re.sub(r"<c>(.*?)</c>", r"`\1`", body)
    body = re.sub(r"<code>(.*?)</code>", r"`\1`", body, flags=re.S)
    body = re.sub(r"<[^>]+>", "", body)
    body = re.sub(r"\s+", " ", body)
    return body.strip()


def parse_file(path):
    """返回文件里每个公开类型的 {name, kind, base, summary, props, methods, pseudo}。"""
    source = path.read_text(encoding="utf-8")
    lines = source.split("\n")

    types = []
    pending_doc = []
    pending_pseudo = []

    i = 0
    while i < len(lines):
        line = lines[i]
        stripped = line.strip()

        if stripped.startswith("///"):
            pending_doc.append(line)
            i += 1
            continue

        m = re.match(r"\[PseudoClasses\((.*?)\)\]", stripped)
        if m:
            pending_pseudo = re.findall(r'"([^"]+)"', m.group(1))
            i += 1
            continue

        m = re.match(
            r"public (?:sealed |abstract )?(class|enum|interface) (\w+)(?:\s*:\s*([\w<>, .]+))?", stripped)
        if m:
            types.append({
                "kind": m.group(1),
                "name": m.group(2),
                "base": (m.group(3) or "").strip(),
                "summary": strip_doc(pending_doc),
                "pseudo": pending_pseudo,
                "props": [],
                "methods": [],
                "values": [],
            })
            pending_doc, pending_pseudo = [], []
            i += 1
            continue

        if types:
            current = types[-1]

            # 接口成员没有 public 修饰符，属性体又写在一行（string? Label { get; }），
            # 下面那两条按 public 起头的规则一条都匹配不到，只会产出一个空条目。
            # 「使用方要自己实现这个接口」正是它存在的理由，成员表不能是空的。
            if current["kind"] == "interface":
                m = re.match(r"([\w<>?\[\], .]+?) (\w+)\s*\{\s*get", stripped)
                if m:
                    current["props"].append(
                        (m.group(2), m.group(1).strip(), strip_doc(pending_doc)))
                    pending_doc = []
                    i += 1
                    continue

                m = re.match(r"([\w<>?\[\], .]+?) (\w+)\((.*?)\);", stripped)
                if m:
                    current["methods"].append(
                        (m.group(2), m.group(1).strip(), m.group(3), strip_doc(pending_doc)))
                    pending_doc = []
                    i += 1
                    continue

            if current["kind"] == "enum":
                m = re.match(r"(\w+)\s*(?:=\s*\d+\s*)?,", stripped)
                if m and not stripped.startswith("//"):
                    current["values"].append((m.group(1), strip_doc(pending_doc)))
                    pending_doc = []
                    i += 1
                    continue

            # 属性：public <类型> <名字>  后面跟 { get
            m = re.match(r"public ([\w<>?\[\], .]+?) (\w+)\s*$", stripped)
            if m and i + 1 < len(lines) and lines[i + 1].strip().startswith("{"):
                if m.group(2) not in ("Item",):
                    current["props"].append(
                        (m.group(2), m.group(1).strip(), strip_doc(pending_doc)))
                pending_doc = []
                i += 1
                continue

            # 方法
            m = re.match(r"public (?:override |static |new )?([\w<>?\[\], .]+?) (\w+)\((.*?)\)", stripped)
            if m and m.group(2) not in ("Equals", "GetHashCode", "ToString"):
                current["methods"].append(
                    (m.group(2), m.group(1).strip(), m.group(3), strip_doc(pending_doc)))
                pending_doc = []
                i += 1
                continue

            # 事件
            m = re.match(r"public event ([\w<>?, .]+?) (\w+)$", stripped)
            if m:
                current["methods"].append(
                    (m.group(2) + "（事件）", m.group(1).strip(), "", strip_doc(pending_doc)))
                pending_doc = []
                i += 1
                continue

        if stripped and not stripped.startswith("//"):
            pending_doc = []

        i += 1

    return types


def render(types):
    out = []
    for t in types:
        if not t["summary"] and not t["props"] and not t["values"]:
            continue

        out.append(f"### `{t['name']}`" + (f" : `{t['base']}`" if t["base"] else ""))
        out.append("")
        if t["summary"]:
            out.append(t["summary"])
            out.append("")

        if t["kind"] == "enum" and t["values"]:
            out.append("| 取值 | 说明 |")
            out.append("|---|---|")
            for name, doc in t["values"]:
                out.append(f"| `{name}` | {doc or '—'} |")
            out.append("")
            continue

        if t["pseudo"]:
            out.append("**伪类**：" + " · ".join(f"`{p}`" for p in t["pseudo"]))
            out.append("")

        if t["props"]:
            out.append("| 属性 | 类型 | 说明 |")
            out.append("|---|---|---|")
            for name, kind, doc in t["props"]:
                out.append(f"| `{name}` | `{kind}` | {doc or '—'} |")
            out.append("")

        if t["methods"]:
            out.append("| 成员 | 说明 |")
            out.append("|---|---|")
            for name, ret, args, doc in t["methods"]:
                sig = f"{name}({args})" if not name.endswith("（事件）") else name
                out.append(f"| `{sig}` | {doc or '—'} |")
            out.append("")

    return out


def main():
    doc = [
        "# 控件 API",
        "",
        "本文件由 `tools/gen_api_docs.py` 从源码抽取，**不要手改**——",
        "改了控件重跑一次：`python3 tools/gen_api_docs.py`。",
        "",
        "只收录本库新写的控件。Avalonia 内置控件（Button / TextBox / ComboBox 等）",
        "只是换了 ControlTheme，API 没变，查 Avalonia 官方文档即可。",
        "",
        "命名空间统一是 `Cobalt.Fluent.Controls`：",
        "",
        "```xml",
        'xmlns:fc="using:Cobalt.Fluent.Controls"',
        "```",
        "",
    ]

    # 新加的子目录如果没进 GROUPS，下面这个循环会直接跳过它，文档里静悄悄地少一批控件。
    # 这里先对一遍账，漏了就喊出来——生成器是 CI 跑的，只有喊出来才会被看见。
    known = {folder for folder, _, _ in GROUPS if folder != "."}
    missing = sorted(
        d.name for d in SRC.iterdir()
        if d.is_dir() and d.name not in known and any(d.glob("*.cs"))
    )
    if missing:
        raise SystemExit(
            f"{SRC.relative_to(ROOT)} 下有没登记的子目录：{'、'.join(missing)}。"
            " 把它们加进 gen_api_docs.py 的 GROUPS 再跑。"
        )

    total = 0
    for folder, title, blurb in GROUPS:
        directory = SRC if folder == "." else SRC / folder
        if not directory.exists():
            continue

        files = sorted(directory.glob("*.cs"))
        if folder == ".":
            files = [f for f in files if f.parent == SRC]

        body = []
        for path in files:
            body += render(parse_file(path))

        if not body:
            continue

        doc.append(f"## {title}")
        doc.append("")
        if blurb:
            doc.append(blurb)
            doc.append("")
        doc += body
        total += sum(1 for line in body if line.startswith("### "))

    OUT.parent.mkdir(parents=True, exist_ok=True)
    OUT.write_text("\n".join(doc), encoding="utf-8")
    print(f"写出 {OUT.relative_to(ROOT)}：{total} 个类型")


if __name__ == "__main__":
    main()
