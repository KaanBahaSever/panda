#!/bin/bash
# WSL, as root: installs a released Panda with its own installer, gives it data (settings, a saved
# song, a playlist, a song playing with a queue), updates it from the panel to a new build and checks
# that everything is still there. Removes any Panda installed on this machine first: WSL test machines only.
# The TeamSpeak test server should be running (env.sh ts, as the normal user).
# usage: upgrade.sh <new panda-linux-x64.tar.gz> [old release tag, default: the latest release]
set -u
grep -qi microsoft /proc/version || { echo "Only for a WSL test machine."; exit 1; }
[ "$(id -u)" = 0 ] || { echo "Run as root."; exit 1; }
# A Panda this test didn't install is somebody's real one: leave it alone.
if [ -d /var/lib/panda ] && [ ! -f /var/lib/panda/.panda-test-install ]; then
	echo "There is a Panda here that this test didn't install (/var/lib/panda); not touching it."; exit 1
fi
NEW=$(realpath "$1"); OLD=${2:-latest}
W=/root/panda-upgrade-test
P=http://127.0.0.1:8098
J=$W/cookies
failed=0
check() { if [ "$2" = "$3" ]; then echo "   PASS $1 ($2)"; else echo "   FAIL $1: \"$2\", expected \"$3\""; failed=$((failed + 1)); fi; }
api() { curl -s -m 10 -b $J -c $J -H 'Content-Type: application/json' "$@"; }
login() { api -d '{"name":"admin","password":"testpass123"}' $P/api/login -o /dev/null -w '%{http_code}'; }
state() { api $P/api/bots/1/state | python3 -c "import sys,json; p=json.load(sys.stdin)['player']; c=p['current']; print(c['track']['title'] if c else '-', len(p['queue']), p['loop'], p['volume'])"; }
wait_up() { for _ in $(seq 60); do curl -s -m 2 $P/api/version >/dev/null && return 0; sleep 1; done; return 1; }

echo "== clean"
[ -x /opt/panda/install.sh ] && bash /opt/panda/install.sh --uninstall --purge >/dev/null
pkill -f "http.server 8777" || true
rm -rf $W; mkdir -p $W/serve $W/new

echo "== install $OLD with its own installer"
URL=https://github.com/KaanBahaSever/panda/releases/latest/download/panda-linux-x64.tar.gz
[ "$OLD" = latest ] || URL=https://github.com/KaanBahaSever/panda/releases/download/$OLD/panda-linux-x64.tar.gz
curl -fsSL "$URL" -o $W/old.tar.gz && mkdir -p $W/old && tar -xzf $W/old.tar.gz -C $W/old || { echo "download failed"; exit 1; }
bash $W/old/install.sh --file $W/old.tar.gz --server 127.0.0.1 --port 8098 < /dev/null > $W/install.log 2>&1 || { tail -20 $W/install.log; exit 1; }
touch /var/lib/panda/.panda-test-install
FROM=$(/opt/panda/panda --version)
tar -xzf "$NEW" -C $W/new panda && chmod +x $W/new/panda && TO=$($W/new/panda --version)
echo "   $FROM -> $TO"
# The panel sees an update finish when the version changes.
[ "$FROM" != "$TO" ] || { echo "   FAIL the new build has the same version ($TO); raise it in src/Panda/Panda.csproj"; exit 1; }

echo "== give it data"
systemctl stop panda
runuser -u panda -- env HOME=/var/lib/panda /opt/panda/panda --data /var/lib/panda set-password testpass123 admin >/dev/null
mkdir -p /var/lib/panda/library
ffmpeg -loglevel error -y -f lavfi -i "sine=frequency=440:duration=90" -c:a libopus -b:a 48k -f webm /var/lib/panda/library/tstTone0001.audio
SIZE=$(stat -c %s /var/lib/panda/library/tstTone0001.audio)
printf '{ "songs": [ { "id": "tstTone0001", "title": "Test Tone Song", "channel": "Tester", "duration": 90, "size": %s, "added": "2026-10-15T00:00:00Z" } ], "stats": {} }\n' "$SIZE" > /var/lib/panda/library/library.json
chown -R panda:panda /var/lib/panda/library
systemctl start panda; wait_up || { echo "Panda did not start"; exit 1; }
sleep 5
check "login" "$(login)" 204
api -d '{"volume":37}' $P/api/bots/1/volume -o /dev/null
# Settings of the old kind: "only these people and groups may use the bot" (before 0.5).
api $P/api/bots/1/settings | python3 -c "
import sys, json
d = json.load(sys.stdin); d['allowedUsers'] = ['oldTestUid0000000000000000='];  d['allowedServerGroups'] = [6]
print(json.dumps(d))" > $W/old-settings.json
api -X PUT -d @$W/old-settings.json $P/api/bots/1/settings -o /dev/null
api -d '{"name":"Upgrade test","songs":[{"id":"tstTone0001","title":"Test Tone Song","channel":"Tester","duration":90}]}' $P/api/playlists -o /dev/null
check "play" "$(api -d '{"query":"test tone"}' $P/api/bots/1/play -o /dev/null -w '%{http_code}')" 200
check "queue" "$(api -d '{"query":"test tone"}' $P/api/bots/1/play -o /dev/null -w '%{http_code}')" 200
api -d '{"mode":"all"}' $P/api/bots/1/loop -o /dev/null
sleep 3
BEFORE=$(state)
check "before the update: song, queue, loop, volume" "$BEFORE" "Test Tone Song 1 All 37"

echo "== update from the panel (the installer downloads the new build from a local server)"
cp "$NEW" $W/serve/panda-linux-x64.tar.gz
(cd $W/serve && setsid nohup python3 -m http.server 8777 --bind 127.0.0.1 >/dev/null 2>&1 < /dev/null &)
sed -i 's#^\(\s*\)URL="https://github.com/$REPO/releases/latest/download/panda-$RID.tar.gz"#\1URL="http://127.0.0.1:8777/panda-$RID.tar.gz"#' /opt/panda/install.sh
grep -q 127.0.0.1:8777 /opt/panda/install.sh || { echo "could not point the installer at the local server"; exit 1; }
check "update starts" "$(api -X POST $P/api/update -o /dev/null -w '%{http_code}')" 202
start=$(date +%s); now=$FROM
while [ $(( $(date +%s) - start )) -lt 960 ]; do
	sleep 3
	v=$(curl -s -m 3 $P/api/version | python3 -c "import sys,json; print(json.load(sys.stdin)['version'])" 2>/dev/null) || continue
	now=$v; [ "$v" = "$FROM" ] || break
	[ "$(api $P/api/update/progress | grep -c failed)" = 0 ] || { echo "   the update failed"; break; }
done
echo "   took $(( $(date +%s) - start )) s"
check "version" "$now" "$TO"

echo "== after the update"
sleep 5
check "login with the same password" "$(login)" 204
check "song, queue, loop, volume" "$(state)" "$BEFORE"
check "allowed users became the members, everyone else shut out" "$(api $P/api/bots/1/settings | python3 -c "
import sys, json
d = json.load(sys.stdin)
print(d.get('everyoneCanUse'), [p['uid'] for p in d.get('members', [])], d.get('memberGroups'), d.get('djs'), d.get('djGroups'))")" "False ['oldTestUid0000000000000000='] [6] [] []"
check "playlist" "$(api "$P/api/playlists/Upgrade%20test" | python3 -c "import sys,json; print(len(json.load(sys.stdin)['songs']))")" 1
check "saved song" "$(api "$P/api/library" | grep -c tstTone0001)" 1
check "panda.service" "$(systemctl is-active panda)" active
check "update watchers" "$(systemctl is-active panda-upgrade.path panda-update.path | sort -u | tr '\n' ' ')" "active "
check "upgrade timeout" "$(grep -c '^TimeoutStartSec=15min' /etc/systemd/system/panda-upgrade.service)" 1
echo "--- installer log:"; journalctl -u panda-upgrade --no-pager -o cat --since "-20 min" | grep -vE 'LANG|locale' | tail -8

pkill -f "http.server 8777" || true
[ "${KEEP:-0}" = 1 ] || bash /opt/panda/install.sh --uninstall --purge >/dev/null
echo; echo "upgrade test: $failed failed"
exit $failed
