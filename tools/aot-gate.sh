#!/usr/bin/env bash
# NativeAOT 闸口。
#
# 两半缺一不可：
#
#   1. **发布**一遍，任何来自本仓库的 IL 告警都算失败。
#      裁剪告警说明「编译器看见了反射」——那条路径裁剪之后可能整个不成立。
#   2. **跑**一遍产出的原生二进制。
#      告警干净不等于跑得起来：编译绑定按编译期类型解析路径，解错了不报错，
#      绑定只是不更新；自定义 ThemeVariant 的字典键要是被裁掉，
#      应用在加载主题那一刻就炸。这两种都只有真跑才看得见。
#
# 第三方程序集的告警走 tools/aot-allow.txt，每条必须写明理由。
#
#   tools/aot-gate.sh              # 默认 linux-x64
#   tools/aot-gate.sh win-x64      # 指定 RID
set -uo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
RID="${1:-linux-x64}"
PROBE="$ROOT/tools/Cobalt.Fluent.AotProbe"
LOG="$(mktemp)"
trap 'rm -f "$LOG"' EXIT

echo "== NativeAOT 发布（$RID）=="
dotnet publish "$PROBE/Cobalt.Fluent.AotProbe.csproj" \
  -c Release -r "$RID" --nologo > "$LOG" 2>&1
CODE=$?

if [ $CODE -ne 0 ]; then
  echo "发布失败："
  grep -E "error|IL[0-9]{4}" "$LOG" | head -40
  exit 1
fi

# 收告警，扣掉例外表里登记的第三方程序集
ALLOW="$ROOT/tools/aot-allow.txt"
WARNINGS="$(grep -E "warning IL[0-9]{4}" "$LOG" || true)"
LEFT="$WARNINGS"
while read -r asm; do
  case "$asm" in ''|\#*) continue ;; esac
  LEFT="$(printf '%s\n' "$LEFT" | grep -v "$asm" || true)"
done < "$ALLOW"

LEFT="$(printf '%s\n' "$LEFT" | grep -E "warning IL[0-9]{4}" || true)"
if [ -n "$LEFT" ]; then
  echo
  echo "有未登记的 IL 告警——本仓库自己的代码不允许带着裁剪告警发布："
  printf '%s\n' "$LEFT" | sed "s|$ROOT/||" | cut -c1-200
  echo
  echo "确属第三方且改不了的，登记到 tools/aot-allow.txt 并写明理由。"
  exit 1
fi

TOTAL="$(printf '%s\n' "$WARNINGS" | grep -c "warning IL" || true)"
echo "发布干净（$TOTAL 条告警全部来自例外表里登记的第三方程序集）"

echo
echo "== 跑产出的原生二进制 =="
BIN="$PROBE/bin/Release/net8.0/$RID/publish/Cobalt.Fluent.AotProbe"
[ -x "$BIN" ] || { echo "找不到产物 $BIN"; exit 1; }
"$BIN"
