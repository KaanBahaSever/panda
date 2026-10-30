#!/usr/bin/env bash
# Builds self-contained single-file releases into dist/ (on this machine: nothing is built on GitHub).
#   scripts/build-release.sh                 # all platforms
#   scripts/build-release.sh linux-x64       # just one
# On Windows (Git Bash) the archives are packed in WSL, so the Linux and macOS programs keep their
# executable bit. Upload: gh release create vX.Y.Z dist/* --title ... --notes-file ...
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
		win-*) ;;
		*) cp deploy/panda.service scripts/install.sh "$out/" ;;
	esac
done

pack() {
	set -euo pipefail
	for rid in "$@"; do
		out="build/$rid"
		case "$rid" in
			win-*)
				(cd "$out" && zip -q -9 "../../dist/panda-$rid.zip" panda.exe LICENSE) ;;
			*)
				chmod 755 "$out/panda" "$out/install.sh"
				chmod 644 "$out/panda.service" "$out/LICENSE"
				tar -czf "dist/panda-$rid.tar.gz" --owner=0 --group=0 -C "$out" panda panda.service install.sh LICENSE ;;
		esac
	done
	(cd dist && sha256sum * > SHA256SUMS)
}

if command -v wsl.exe >/dev/null; then
	# Copied to WSL's own disk to pack: files on /mnt/c have no Unix permissions of their own.
	MSYS_NO_PATHCONV=1 wsl.exe bash -c "$(declare -f pack); set -euo pipefail
		src=\$(wslpath '$(pwd -W)'); w=\$(mktemp -d); mkdir -p \$w/build \$w/dist
		for rid in ${RIDS[*]}; do cp -r \"\$src/build/\$rid\" \$w/build/; done
		cd \$w && pack ${RIDS[*]} && cp dist/* \"\$src/dist/\" && rm -rf \$w"
else
	pack "${RIDS[@]}"
fi
ls -lh dist
