#!/bin/bash
# WSL: starts the TeamSpeak 6 test server and a Panda (panel on port 8097) whose library holds one
# generated test song, with a yt-dlp stand-in (fake-yt-dlp.sh): the tests never reach YouTube.
# usage: env.sh <linux panda binary>      env.sh ts (only the TeamSpeak server)      env.sh stop
set -eu
T=$HOME/panda-test-env
TS=${PANDA_TS_DIR:-$HOME/ts6test/srv}
KEY=testkey-local-only    # query key of the local test server only
Q() { curl -s -H "x-api-key: $KEY" "http://127.0.0.1:10080/1/$1"; }

if [ "${1:-}" = stop ]; then
	pkill -f "$T/panda" || true
	pkill -f "tsserver --accept-license" || true
	exit 0
fi

[ -x "$TS/tsserver" ] || { echo "No TeamSpeak 6 server in $TS (see tests/README.md)."; exit 1; }
if ! pgrep -f "tsserver --accept-license" >/dev/null; then
	(cd "$TS" && setsid nohup ./tsserver --accept-license --query-http-enable --query-admin-api-key $KEY \
		> "$TS/../server.out" 2>&1 < /dev/null &)
	for _ in $(seq 30); do Q serverinfo 2>/dev/null | grep -q '"body"' && break; sleep 1; done
fi
# The test client types faster than people do; guests may change their description (for the description test).
Q "serveredit?virtualserver_antiflood_points_tick_reduce=1000&virtualserver_antiflood_points_needed_command_block=100000&virtualserver_antiflood_points_needed_ip_block=100000" >/dev/null
GUEST=$(Q serverinfo | python3 -c "import sys,json; print(json.load(sys.stdin)['body'][0]['virtualserver_default_server_group'])")
Q "servergroupaddperm?sgid=$GUEST&permsid=b_client_modify_own_description&permvalue=1&permskip=0&permnegated=0" >/dev/null
# A second channel, for "only people in the bot's channel" (an error when it's already there).
Q "channelcreate?channel_name=Lounge&channel_flag_permanent=1" >/dev/null
[ "$1" = ts ] && exit 0

pkill -f "$T/panda" || true
rm -rf "$T"; mkdir -p "$T/data/library"
cp "$1" "$T/panda"; chmod +x "$T/panda"
ffmpeg -loglevel error -f lavfi -i "sine=frequency=440:duration=90" -c:a libopus -b:a 48k -f webm "$T/data/library/tstTone0001.audio"
SIZE=$(stat -c %s "$T/data/library/tstTone0001.audio")
# yt-dlp stand-in: any other song is the same tone, and YouTube's failures can be acted out.
mkdir -p "$T/fake-yt-dlp"
cp "$T/data/library/tstTone0001.audio" "$T/fake-yt-dlp/tone.webm"
cp "$(dirname "$0")/fake-yt-dlp.sh" "$T/fake-yt-dlp/yt-dlp"; chmod +x "$T/fake-yt-dlp/yt-dlp"
cat > "$T/data/library/library.json" <<EOF
{ "songs": [ { "id": "tstTone0001", "title": "Test Tone Song", "channel": "Tester", "duration": 90, "size": $SIZE, "added": "2026-10-15T00:00:00Z" } ], "stats": {} }
EOF
printf '{ "bots": [ { "id": "1", "server": { "address": "127.0.0.1" } } ], "panel": { "port": 8097 }, "youTube": { "ytDlpPath": "%s" } }\n' \
	"$T/fake-yt-dlp/yt-dlp" > "$T/data/config.json"
"$T/panda" --data "$T/data" set-password testpass123 admin >/dev/null
setsid nohup "$T/panda" --data "$T/data" > "$T/log.txt" 2>&1 < /dev/null &
for _ in $(seq 40); do grep -q "Connected to" "$T/log.txt" 2>/dev/null && break; sleep 1; done
grep -m1 "Connected to" "$T/log.txt" || { echo "Panda did not connect"; tail -5 "$T/log.txt"; exit 1; }
# Another Panda on the test server (one left over from an interrupted test) would answer too.
PANDAS=$(Q clientlist | python3 -c "import sys,json; print(sum('Panda' in c['client_nickname'] for c in json.load(sys.stdin)['body']))")
[ "$PANDAS" = 1 ] || { echo "$PANDAS Pandas are on the test server; stop the others first (e.g. a test install: /opt/panda/install.sh --uninstall --purge)."; exit 1; }
