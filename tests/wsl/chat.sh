#!/bin/bash
# WSL: runs the chat scenarios against a fresh test Panda. Exit code = failed checks.
# usage: chat.sh <linux panda binary> <linux chattest binary> [scenario...]   (default: all scenarios)
set -u
HERE=$(cd "$(dirname "$0")" && pwd)
PANDA=$1; CHAT=$2; shift 2
[ $# -gt 0 ] || set -- "$HERE"/../chat/scenarios/*.txt
bash "$HERE/env.sh" "$PANDA" || exit 1
cp "$CHAT" /tmp/panda-chattest && chmod +x /tmp/panda-chattest
PANDA_TEST_QUERY="http://127.0.0.1:10080 testkey-local-only" /tmp/panda-chattest 127.0.0.1 http://127.0.0.1:8097 "$@"
failed=$?
echo; echo "--- warnings and errors in Panda's log:"
grep -E " (warn|fail|crit): " "$HOME/panda-test-env/log.txt" -A1 | cut -c1-220 || echo "(none)"
[ "${KEEP:-0}" = 1 ] || bash "$HERE/env.sh" stop
exit $failed
