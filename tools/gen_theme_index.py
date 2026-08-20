#!/usr/bin/env python3
"""
扫描 src/Cobalt.Fluent/Themes/Controls/*.axaml，重写 CobaltFluentTheme.axaml 的合并列表。

为什么要生成：控件层会长到几十个文件，手工维护 ResourceInclude 列表必漏。
顺序有意义 —— ControlTheme 用 BasedOn 引用另一个文件里的主题时，
被引用的那个必须先合并进来，否则 StaticResource 解析不到。ORDER 里排在前面的先合并。
"""
import pathlib
import re

ROOT = pathlib.Path(__file__).resolve().parent.parent
CONTROLS = ROOT / "src/Cobalt.Fluent/Themes/Controls"
INDEX = ROOT / "src/Cobalt.Fluent/Themes/CobaltFluentTheme.axaml"

# 被别人 BasedOn 的放前面。其余按文件名排序，保证结果可复现。
ORDER = ["Button", "TextBox", "ListBox"]

HEADER = '''<!-- 控件库入口。Themes/Controls 下的合并列表由 tools/gen_theme_index.py 生成。 -->
<Styles xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:sty="using:Avalonia.Themes.Fluent"
        x:Class="Cobalt.Fluent.CobaltFluentTheme">

  <!-- 基座。ScrollBar / ScrollViewer / ContextMenu 这些本库不重做模板的控件靠它出模板。
       Avalonia 自带的 FluentTheme 属于框架本体，不是第三方依赖。
       它排在最前：后面 Styles.Resources 里的 token 会整体覆盖它的同名键，
       所以连没被重做模板的控件也会跟着换成本库的配色。 -->
  <sty:FluentTheme />

  <Styles.Resources>
    <ResourceDictionary>
      <ResourceDictionary.MergedDictionaries>

        <!-- 变量层（Tokens.axaml 由 tools/gen_tokens.py 从 tools/palette.json 生成） -->
        <ResourceInclude Source="avares://Cobalt.Fluent/Themes/Tokens.axaml" />
        <ResourceInclude Source="avares://Cobalt.Fluent/Themes/Metrics.axaml" />
        <ResourceInclude Source="avares://Cobalt.Fluent/Themes/Typography.axaml" />
        <ResourceInclude Source="avares://Cobalt.Fluent/Themes/Shared.axaml" />

        <!-- 控件层 -->
'''

FOOTER = '''
      </ResourceDictionary.MergedDictionaries>
    </ResourceDictionary>
  </Styles.Resources>

</Styles>
'''


def main():
    CONTROLS.mkdir(parents=True, exist_ok=True)
    names = sorted(p.stem for p in CONTROLS.glob("*.axaml"))
    ordered = [n for n in ORDER if n in names] + [n for n in names if n not in ORDER]

    lines = [
        f'        <ResourceInclude Source="avares://Cobalt.Fluent/Themes/Controls/{n}.axaml" />'
        for n in ordered
    ]
    INDEX.write_text(HEADER + "\n".join(lines) + FOOTER, encoding="utf-8")
    print(f"合并 {len(ordered)} 个控件层文件：{', '.join(ordered)}")


if __name__ == "__main__":
    main()
