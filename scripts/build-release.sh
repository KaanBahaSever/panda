#!/usr/bin/env bash
# Builds self-contained single-file releases into dist/.
#   scripts/build-release.sh                 # all platforms
#   scripts/build-release.sh linux-x64       # just one
set -euo pipefail
cd "$(dirname "$0")/.."

RIDS=("$@")
[ ${#RIDS[@]} -gt 0 ] || RIDS=(linux-x64 linux-arm64 win-x64 osx-arm64)

rm -rf dist
mkdir -p dist
for rid in "${RIDS[@]}"; do
	echo "==> $rid"
	out="build/$rid"
	rm -rf "$out"
	dotnet publish src/Panda/Panda.csproj -c Release -r "$rid" --self-contained \
		-p:PublishSingleFile=true \
		-p:IncludeNativeLibrariesForSelfExtract=true \
		-p:EnableCompressionInSingleFile=true \
		-p:DebugType=none \
		-o "$out" -nologo -v q
	cp LICENSE "$out/"
	case "$rid" in
		win-*)
			(cd "$out" && zip -q -9 "../../dist/panda-$rid.zip" panda.exe LICENSE) ;;
		*)
			cp deploy/panda.service "$out/"
			tar -czf "dist/panda-$rid.tar.gz" -C "$out" panda panda.service LICENSE ;;
	esac
done
(cd dist && sha256sum * > SHA256SUMS)
ls -lh dist
