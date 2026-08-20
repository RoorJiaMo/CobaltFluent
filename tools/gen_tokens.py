#!/usr/bin/env python3
"""
tools/palette.json  →  src/Cobalt.Fluent/Themes/Tokens.axaml

调色板是这套设计系统的单一事实来源：79 个键 × 明暗两套。生成器把每个键摊成
一个 Color 加一个同名 +Brush 的 SolidColorBrush，再补三支底边渐变描边——
158 个 Color、158 个 Brush，手抄必错，所以这一步交给脚本。

改颜色改 palette.json，然后重跑：  python3 tools/gen_tokens.py
"""
import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
PALETTE = ROOT / "tools/palette.json"
OUT = ROOT / "src/Cobalt.Fluent/Themes/Tokens.axaml"

# --- 底边渐变描边 ------------------------------------------------------------
# Win11 控件的签名细节：描边上浅下深，底边深一档。被按下时渐变拍平成实色，
# 视觉上"陷进去"。XAML 的 BorderBrush 是四边共用一支笔，所以用竖直渐变——
# 最后 8% 切到深色，等价于单独加深底边。
#
# 这几支笔必须和颜色写在同一个 ThemeDictionary 里：StaticResource 只在本字典内
# 解析，跨字典引用主题色会解析不到。
ELEVATION_BRUSHES = [
    '      <LinearGradientBrush x:Key="ControlElevationBorderBrush" StartPoint="0%,0%" EndPoint="0%,100%">',
    '        <GradientStop Color="{StaticResource ControlStrokeColorDefault}" Offset="0" />',
    '        <GradientStop Color="{StaticResource ControlStrokeColorDefault}" Offset="0.92" />',
    '        <GradientStop Color="{StaticResource ControlStrokeColorSecondary}" Offset="1" />',
    "      </LinearGradientBrush>",
    '      <LinearGradientBrush x:Key="AccentControlElevationBorderBrush" StartPoint="0%,0%" EndPoint="0%,100%">',
    '        <GradientStop Color="{StaticResource ControlStrokeColorOnAccentDefault}" Offset="0" />',
    '        <GradientStop Color="{StaticResource ControlStrokeColorOnAccentDefault}" Offset="0.92" />',
    '        <GradientStop Color="{StaticResource ControlStrokeColorOnAccentSecondary}" Offset="1" />',
    "      </LinearGradientBrush>",
    "      <!-- 输入框底边比普通控件更明显：rest 就用 control-strong-stroke -->",
    '      <LinearGradientBrush x:Key="TextControlElevationBorderBrush" StartPoint="0%,0%" EndPoint="0%,100%">',
    '        <GradientStop Color="{StaticResource ControlStrokeColorDefault}" Offset="0" />',
    '        <GradientStop Color="{StaticResource ControlStrokeColorDefault}" Offset="0.92" />',
    '        <GradientStop Color="{StaticResource ControlStrongStrokeColorDefault}" Offset="1" />',
    "      </LinearGradientBrush>",
]


def load():
    """读调色板，顺带把明显写错的值挡在生成之前。"""
    groups = json.loads(PALETTE.read_text(encoding="utf-8"))
    keys, bad = [], []
    for group, entries in groups.items():
        for key, variants in entries.items():
            if key in keys:
                bad.append(f"{key} 重复登记")
            keys.append(key)
            for theme in ("light", "dark"):
                value = variants.get(theme)
                if value is None:
                    bad.append(f"{key} 缺 {theme} 那一档")
                elif not (
                    isinstance(value, str)
                    and len(value) == 9
                    and value[0] == "#"
                    and all(c in "0123456789ABCDEF" for c in value[1:])
                ):
                    bad.append(f"{key}.{theme} = {value!r} 不是 #AARRGGBB（大写）")
    if bad:
        sys.exit(f"{PALETTE.name} 有问题：\n  " + "\n  ".join(bad))
    return groups


def emit(groups):
    lines = [
        "<!-- 本文件由 tools/gen_tokens.py 从 tools/palette.json 生成，不要手改。 -->",
        "<!-- 改颜色请改 palette.json，然后重跑：python3 tools/gen_tokens.py       -->",
        '<ResourceDictionary xmlns="https://github.com/avaloniaui"',
        '                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">',
        "",
        "  <ResourceDictionary.ThemeDictionaries>",
    ]

    for theme in ("light", "dark"):
        lines.append(f'    <ResourceDictionary x:Key="{theme.capitalize()}">')
        for group, entries in groups.items():
            lines.append(f"      <!-- {group} -->")
            for key, variants in entries.items():
                lines.append(f'      <Color x:Key="{key}">{variants[theme]}</Color>')
        lines.append("")
        for entries in groups.values():
            for key in entries:
                lines.append(
                    f'      <SolidColorBrush x:Key="{key}Brush" '
                    f'Color="{{StaticResource {key}}}" />'
                )
        lines.append("")
        lines += ELEVATION_BRUSHES
        lines.append("    </ResourceDictionary>")

    lines += ["  </ResourceDictionary.ThemeDictionaries>", "", "</ResourceDictionary>", ""]
    return "\n".join(lines)


def main():
    groups = load()
    OUT.parent.mkdir(parents=True, exist_ok=True)
    OUT.write_text(emit(groups), encoding="utf-8")
    n = sum(len(entries) for entries in groups.values())
    print(
        f"写出 {OUT.relative_to(ROOT)}："
        f"{n} 个颜色键 × 2 套主题 = {n * 2} 个 Color + {n * 2} 个 Brush"
    )


if __name__ == "__main__":
    main()
