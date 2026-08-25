#!/usr/bin/env bash
# 打包闸口。
#
# 两半，和 AOT 闸口同一个道理：
#
#   1. **打包**。dotnet pack 出错、缺元数据、缺 README/LICENSE 都算失败。
#   2. **装上跑**。包看着对不等于装上能用——项目引用一路绿、包引用炸掉，
#      是控件库最经典的翻车方式：AXAML 没编进 dll，模板全都套不上，
#      而在仓库里怎么测都测不出来。
#
#   tools/pack-gate.sh
set -uo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
WORK="$(mktemp -d "${TMPDIR:-/tmp}/cobalt-pack-XXXXXX")"
trap 'rm -rf "$WORK"' EXIT

VERSION="$(grep -oPm1 '(?<=<Version>)[^<]+' "$ROOT/src/Cobalt.Fluent/Cobalt.Fluent.csproj")"
echo "== 打包 $VERSION =="
if ! dotnet pack "$ROOT/src/Cobalt.Fluent/Cobalt.Fluent.csproj" \
     -c Release -o "$WORK/feed" --nologo > "$WORK/pack.log" 2>&1; then
  grep -E "error|warning NU" "$WORK/pack.log" | head -20
  exit 1
fi

# 包里该有的东西。少一样使用方就少一块功能，而这在仓库里看不出来。
cd "$WORK" && unzip -o -q "feed/Cobalt.Fluent.$VERSION.nupkg" -d unpacked
MISSING=0
for f in "Cobalt.Fluent.nuspec" "README.md" "LICENSE" \
         "lib/net8.0/Cobalt.Fluent.dll" "lib/net8.0/Cobalt.Fluent.xml"; do
  [ -f "unpacked/$f" ] || { echo "  包里缺 $f"; MISSING=1; }
done
[ -f "feed/Cobalt.Fluent.$VERSION.snupkg" ] || { echo "  没有产出符号包"; MISSING=1; }
grep -q "<repository" unpacked/Cobalt.Fluent.nuspec \
  || { echo "  nuspec 里没有 repository（SourceLink 断了，使用方单步不进来）"; MISSING=1; }
[ $MISSING -eq 0 ] || exit 1
echo "包内容齐全"

echo
echo "== 从装上的包里用一遍 =="
mkdir -p "$WORK/consume"
cp "$ROOT/tools/Cobalt.Fluent.PackProbe/Program.cs" "$WORK/consume/"
AVALONIA="$(grep -oPm1 '(?<=<AvaloniaVersion>)[^<]+' "$ROOT/Directory.Build.props")"

cat > "$WORK/consume/nuget.config" <<XML
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <add key="local" value="$WORK/feed" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
</configuration>
XML

cat > "$WORK/consume/consume.csproj" <<XML
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <!-- 固定区域性：这里要走的正是「非中文 locale 拿到英文」那条默认路径 -->
    <InvariantGlobalization>true</InvariantGlobalization>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Cobalt.Fluent" Version="$VERSION" />
    <PackageReference Include="Avalonia.Headless" Version="$AVALONIA" />
  </ItemGroup>
</Project>
XML

# 用干净的包缓存，免得本地已有的同版本包把新打的这个盖掉
export NUGET_PACKAGES="$WORK/packages"
cd "$WORK/consume" && dotnet run -c Release --nologo
