#!/bin/bash
# WSL: a Panda that isn't installed by install.sh (as on Windows or macOS) updates itself from the panel,
# from a stand-in of GitHub's releases (port 8099): a download that doesn't match its checksum is refused
# and the old Panda keeps running; a good one replaces the program, the new Panda starts by itself and the
# song plays on; with automatic updates on, Panda installs the next version by itself when nothing plays.
# usage: selfupdate.sh <linux panda binary> <releases dir with v9.0.1/ and v9.0.2/>     Exit code = failed checks.
set -u
HERE=$(cd "$(dirname "$0")" && pwd)
T=$HOME/panda-test-env
P=http://127.0.0.1:8097
J=/tmp/panda-self-cookies
REL=/tmp/panda-releases
failed=0
check() { if [ "$2" = "$3" ]; then echo "   PASS $1 ($2)"; else echo "   FAIL $1: \"$2\", expected \"$3\""; failed=$((failed + 1)); fi; }
api() { curl -s -m 10 -b $J -c $J -H 'Content-Type: application/json' "$@"; }
json() { python3 -c "import sys,json; d=json.load(sys.stdin); print($1)"; }
login() { api -d '{"name":"admin","password":"testpass123"}' $P/api/login -o /dev/null; }
version() { curl -s -m 3 $P/api/version | json "d['version']" 2>/dev/null || echo "-"; }
latest() { printf '{"tag_name":"v%s","body":"test notes","html_url":"http://127.0.0.1:8099/v%s"}' "$1" "$1" > $REL/latest.json; }
start() { PANDA_TEST_RELEASES=http://127.0.0.1:8099 setsid nohup "$T/panda" --data "$T/data" >> "$T/log.txt" 2>&1 < /dev/null & }
wait_version() { for _ in $(seq "$2"); do [ "$(version)" = "$1" ] && return 0; sleep 1; done; return 1; }
cleanup() { pkill -f "http.server 8099" || true; bash "$HERE/env.sh" stop >/dev/null 2>&1; }
trap cleanup EXIT

bash "$HERE/env.sh" "$1" >/dev/null || { echo "env failed"; exit 1; }
FROM=$("$T/panda" --version)
# The stand-in of GitHub: latest.json says which version is newest; vX/ holds its archive and SHA256SUMS.
rm -rf $REL; cp -r "$2" $REL
mkdir -p $REL/v9.0.0; cp $REL/v9.0.1/panda-linux-x64.tar.gz $REL/v9.0.0/
echo "0000000000000000000000000000000000000000000000000000000000000000  panda-linux-x64.tar.gz" > $REL/v9.0.0/SHA256SUMS
pkill -f "http.server 8099" || true
(cd $REL && setsid nohup python3 -m http.server 8099 --bind 127.0.0.1 > /tmp/panda-releases.log 2>&1 < /dev/null &)
# The Panda from env.sh again, now asking the stand-in.
pkill -f "$T/panda --data"; sleep 2; start
wait_version "$FROM" 30; login

echo "== this Panda can update itself"
latest 9.0.0
check "it knows how" "$(api "$P/api/update?fresh=true" | json "(d['install'], d['latest'], d['newer'])")" "('self', '9.0.0', True)"

echo "== a download that doesn't match its checksum"
check "the update starts" "$(curl -s -m 10 -b $J -o /dev/null -w '%{http_code}' -X POST $P/api/update)" 202
for _ in $(seq 30); do s=$(api $P/api/update/progress | json "d['state']"); [ "$s" = running ] || break; sleep 1; done
check "...and fails" "$s" failed
check "the old Panda keeps running" "$(version)" "$FROM"
check "its program is untouched" "$("$T/panda" --version) $(ls "$T" | grep -c 'panda\.\(new\|old\)')" "$FROM 0"

echo "== a good update, while a song plays"
api -d '{"query":"test tone"}' $P/api/bots/1/play -o /dev/null
api -d '{"mode":"one"}' $P/api/bots/1/loop -o /dev/null
sleep 3
latest 9.0.1
api "$P/api/update?fresh=true" -o /dev/null
check "the update starts" "$(curl -s -m 10 -b $J -o /dev/null -w '%{http_code}' -X POST $P/api/update)" 202
wait_version 9.0.1 90; check "the new Panda answers" "$(version)" 9.0.1
login
for _ in $(seq 30); do [ "$(api $P/api/bots/1/state | json "d['bot']['state']")" = connected ] && break; sleep 1; done
sleep 3
check "the song plays on, still on repeat" "$(api $P/api/bots/1/state | json "(d['player']['current']['track']['title'], d['player']['loop'])")" "('Test Tone Song', 'One')"
check "the program was replaced" "$("$T/panda" --version)" 9.0.1
sleep 3
check "the old program is gone, one Panda runs" "$(ls "$T" | grep -c 'panda\.\(new\|old\)') $(pgrep -fc "$T/panda --data")" "0 1"

echo "== automatic updates: the next version comes by itself once nothing plays"
api -X PUT -d '{"on":true}' $P/api/update/auto -o /dev/null
check "the setting is kept" "$(api "$P/api/update" | json "d['auto']")" True
latest 9.0.2
api "$P/api/update?fresh=true" -o /dev/null
sleep 70
check "not while a song plays" "$(version)" 9.0.1
api -X POST $P/api/bots/1/control/stop -o /dev/null
wait_version 9.0.2 150; check "installed by itself when nothing plays" "$(version)" 9.0.2

echo; echo "self update: $failed failed"
exit $failed
