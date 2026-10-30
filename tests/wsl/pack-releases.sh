#!/bin/bash
# WSL: packs the "newer" builds for the self update test as releases are packed (archive + SHA256SUMS).
# usage: pack-releases.sh <build dir with vX/ folders> <version>...
set -eu
B=$1; shift
for v in "$@"; do
	d=$B/v$v
	mkdir -p "$B/releases/v$v"
	chmod 755 "$d/panda"
	tar -czf "$B/releases/v$v/panda-linux-x64.tar.gz" -C "$d" panda panda.service install.sh LICENSE
	(cd "$B/releases/v$v" && sha256sum panda-linux-x64.tar.gz > SHA256SUMS)
done
