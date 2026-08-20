#!/usr/bin/env bash
# 隔离编译校验。把控件库复制到一个临时目录再编译，多个人同时改不同文件时不会抢 obj/ 锁。
#
#   tools/check.sh                       校验整个控件库
#   tools/check.sh --gallery             连展柜一起校验（慢一些）
#   tools/check.sh --only A.axaml B.axaml
#                                        只把这几个控件层文件合并进来编译。
#                                        并行开发时用这个：别人半成品的文件不会污染你的编译结果。
set -uo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
MODE="all"
ONLY=()

case "${1:-}" in
  --gallery) MODE="gallery" ;;
  --only)    MODE="only"; shift; ONLY=("$@") ;;
esac

PROBE="$(mktemp -d "${TMPDIR:-/tmp}/cobalt-check-XXXXXX")"
trap 'rm -rf "$PROBE"' EXIT

mkdir -p "$PROBE/src" "$PROBE/samples" "$PROBE/tools"
cp "$ROOT/Directory.Build.props" "$ROOT/NuGet.config" "$PROBE/" 2>/dev/null
cp -r "$ROOT/src/Cobalt.Fluent" "$PROBE/src/"
cp "$ROOT/tools/gen_theme_index.py" "$PROBE/tools/"
rm -rf "$PROBE/src/Cobalt.Fluent/obj" "$PROBE/src/Cobalt.Fluent/bin"

if [ "$MODE" = "only" ]; then
  # 只留下点名的控件层文件，其余移走
  KEEP="$PROBE/keep"; mkdir -p "$KEEP"
  for f in "${ONLY[@]}"; do
    base="$(basename "$f")"
    [ -f "$PROBE/src/Cobalt.Fluent/Themes/Controls/$base" ] && \
      mv "$PROBE/src/Cobalt.Fluent/Themes/Controls/$base" "$KEEP/"
  done
  rm -f "$PROBE/src/Cobalt.Fluent/Themes/Controls/"*.axaml
  cp "$KEEP/"*.axaml "$PROBE/src/Cobalt.Fluent/Themes/Controls/" 2>/dev/null
fi

( cd "$PROBE" && python3 tools/gen_theme_index.py >/dev/null )

TARGET="$PROBE/src/Cobalt.Fluent/Cobalt.Fluent.csproj"
if [ "$MODE" = "gallery" ]; then
  cp -r "$ROOT/samples/Cobalt.Fluent.Gallery" "$PROBE/samples/"
  rm -rf "$PROBE/samples/Cobalt.Fluent.Gallery/obj" "$PROBE/samples/Cobalt.Fluent.Gallery/bin"
  TARGET="$PROBE/samples/Cobalt.Fluent.Gallery/Cobalt.Fluent.Gallery.csproj"
fi

OUT="$(dotnet build "$TARGET" -v q --nologo 2>&1)"
CODE=$?

# 把临时路径还原成仓库内路径，报错行才点得开
echo "$OUT" | grep -E 'error|warning AVLN|Build (succeeded|FAILED)' | sed "s|$PROBE/|$ROOT/|g" | sort -u | head -60
exit $CODE
