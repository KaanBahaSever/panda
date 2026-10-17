#!/bin/bash
# Builds Panda and the chat test client for Linux, then runs the tests: translations, chat (WSL TS6
# server), stop time, and with "all" also the panel update test (installs Panda in WSL as root).
# Runs from Git Bash on Windows (tests in WSL) or on Linux.
# usage: tests/run.sh            translations, chat, stop time
#        tests/run.sh all        and the panel update test
#        tests/run.sh chat 12 13 only these chat scenarios (by number)
set -u
R=$(cd "$(dirname "$0")/.." && pwd)
B=$R/build/test
results=()
run() { local name=$1; shift; echo; echo "##### $name"; if "$@"; then results+=("PASS $name"); else results+=("FAIL $name"); fi; }

# On Windows the tests run in WSL; paths become /mnt/c/...
if command -v wsl.exe >/dev/null; then
	in_linux() { MSYS_NO_PATHCONV=1 wsl.exe "$@"; }
	lp() { printf '/mnt/%s' "$(echo "$1" | sed -E 's#^/([a-zA-Z])/#\L\1/#')"; }
else
	in_linux() { "$@"; }
	lp() { printf '%s' "$1"; }
fi

MODE=${1:-}
SCENARIOS=()
if [ "$MODE" = chat ]; then
	shift
	for n in "$@"; do for f in "$R"/tests/chat/scenarios/"$n"-*.txt; do SCENARIOS+=("$(lp "$f")"); done; done
fi

run "translations" node "$R/tests/i18n-check.mjs"

echo; echo "##### build"
rm -rf "$B"
dotnet publish "$R/src/Panda/Panda.csproj" -c Release -r linux-x64 --self-contained -p:PublishSingleFile=true \
	-p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=none -o "$B/panda" -nologo -v q || exit 1
dotnet publish "$R/tests/chat/ChatTest.csproj" -c Release -r linux-x64 --self-contained -p:PublishSingleFile=true \
	-p:DebugType=none -o "$B/chat" -nologo -v q || exit 1

run "chat" in_linux bash "$(lp "$R/tests/wsl/chat.sh")" "$(lp "$B/panda/panda")" "$(lp "$B/chat/chattest")" ${SCENARIOS[@]+"${SCENARIOS[@]}"}
[ "$MODE" = chat ] || run "stop time" in_linux bash "$(lp "$R/tests/wsl/stoptime.sh")" "$(lp "$B/panda/panda")"

if [ "$MODE" = all ]; then
	# The release archive, as build-release.sh makes it.
	mkdir -p "$B/tar"; cp "$B/panda/panda" "$R/deploy/panda.service" "$R/scripts/install.sh" "$R/LICENSE" "$B/tar/"
	tar -czf "$B/panda-linux-x64.tar.gz" -C "$B/tar" panda panda.service install.sh LICENSE
	in_linux bash "$(lp "$R/tests/wsl/env.sh")" ts
	if command -v wsl.exe >/dev/null; then
		run "panel update" in_linux -u root bash "$(lp "$R/tests/wsl/upgrade.sh")" "$(lp "$B/panda-linux-x64.tar.gz")"
	else
		run "panel update" sudo bash "$R/tests/wsl/upgrade.sh" "$B/panda-linux-x64.tar.gz"
	fi
	in_linux bash "$(lp "$R/tests/wsl/env.sh")" stop
fi

echo; echo "##### results"
printf '%s\n' "${results[@]}"
! printf '%s\n' "${results[@]}" | grep -q '^FAIL'
