#!/bin/bash
# WSL: failure cases and a long run against the test Panda from env.sh.
#  1. The TeamSpeak server goes away for a minute and comes back: Panda reconnects and plays on.
#  2. YouTube refuses everything for a while: songs are skipped with a message, Panda stays up and recovers.
#  3. The library can't be written (a full or read-only disk): songs still play.
#  4. Long run: a looped playlist plays for SOAK_MIN minutes (default 30); memory, threads, open files
#     and child processes are sampled every minute and must not keep growing.
# usage: resilience.sh <linux panda binary>     Exit code = failed checks.
set -u
HERE=$(cd "$(dirname "$0")" && pwd)
T=$HOME/panda-test-env
P=http://127.0.0.1:8097
J=/tmp/panda-resilience-cookies
TS=${PANDA_TS_DIR:-$HOME/ts6test/srv}
failed=0
check() { if [ "$2" = "$3" ]; then echo "   PASS $1 ($2)"; else echo "   FAIL $1: \"$2\", expected \"$3\""; failed=$((failed + 1)); fi; }
api() { curl -s -m 10 -b $J -c $J -H 'Content-Type: application/json' "$@"; }
playing() { api $P/api/bots/1/state | python3 -c "import sys,json; c=json.load(sys.stdin)['player']['current']; print('yes' if c else 'no')" 2>/dev/null || echo "no answer"; }
connected() { api $P/api/bots/1/state | python3 -c "import sys,json; print(json.load(sys.stdin)['bot']['state'])" 2>/dev/null || echo "no answer"; }
wait_for() { for _ in $(seq "$1"); do [ "$($2)" = "$3" ] && return 0; sleep 1; done; return 1; }

bash "$HERE/env.sh" "$1" || exit 1
PID=$(pgrep -f "$T/panda --data" | head -1)
api -d '{"name":"admin","password":"testpass123"}' $P/api/login -o /dev/null
# A playlist of 20 songs (the yt-dlp stand-in plays the test tone for each), looped.
python3 -c "
import json
json.dump({'name': 'Soak', 'songs': [{'id': 'soak%07d' % i, 'title': 'Soak %d' % i, 'channel': 'test', 'duration': 90} for i in range(20)]}, open('/tmp/panda-soak.json', 'w'))"
api -d @/tmp/panda-soak.json $P/api/playlists -o /dev/null
api -d '{"name":"Soak"}' $P/api/bots/1/playlist -o /dev/null
api -d '{"mode":"all"}' $P/api/bots/1/loop -o /dev/null
sleep 5
check "playing" "$(playing)" yes

echo "== 1. the TeamSpeak server is gone for a minute"
pkill -f "tsserver --accept-license"
sleep 60
check "Panda still answers" "$(curl -s -m 5 -o /dev/null -w '%{http_code}' $P/api/version)" 200
(cd "$TS" && setsid nohup ./tsserver --accept-license --query-http-enable --query-admin-api-key testkey-local-only \
	>> "$TS/../server.out" 2>&1 < /dev/null &)
wait_for 120 connected connected; check "reconnected" "$(connected)" connected
sleep 5
check "playing after the reconnect" "$(playing)" yes

echo "== 2. YouTube refuses everything for a while"
echo "403 30" > $T/fake-yt-dlp/fail
api -X POST $P/api/bots/1/control/skip -o /dev/null
sleep 25
check "Panda still answers" "$(curl -s -m 5 -o /dev/null -w '%{http_code}' $P/api/version)" 200
echo "" > $T/fake-yt-dlp/fail
api -X POST $P/api/bots/1/control/skip -o /dev/null
wait_for 30 playing yes; check "plays again when YouTube works" "$(playing)" yes

echo "== 3. the library can't be written"
chmod 555 $T/data/library
api -d '{"query":"a song that is not saved yet"}' $P/api/bots/1/play -o /dev/null
api -X POST $P/api/bots/1/control/skip -o /dev/null
sleep 8
check "plays with a read-only library" "$(playing)" yes
chmod 755 $T/data/library

echo "== 4. long run: ${SOAK_MIN:-30} min, with the equalizer, 8D and crossfade on"
# The songs are saved after their first play, so later rounds crossfade from file to file.
check "sound settings" "$(api -X PUT -d '{"equalizer":"rock","eightD":true,"crossfade":4}' $P/api/bots/1/sound -o /dev/null -w '%{http_code}')" 200
TICKS=$(getconf CLK_TCK)
cpu() { awk '{print $14 + $15}' /proc/$PID/stat; }
sample() {
	local rss threads fds kids
	rss=$(awk '/VmRSS/ {print $2}' /proc/$PID/status)
	threads=$(awk '/Threads/ {print $2}' /proc/$PID/status)
	fds=$(ls /proc/$PID/fd | wc -l)
	kids=$(pgrep -P $PID | wc -l)
	echo "$rss $threads $fds $kids"
}
first=""; samples=(); cpu0=$(cpu); t0=$(date +%s)
for m in $(seq 0 "${SOAK_MIN:-30}"); do
	s=$(sample); samples+=("$s")
	[ -n "$first" ] || first=$s
	printf '   %3d min  RSS %s KB  threads %s  files %s  child processes %s  playing %s\n' $m $s "$(playing)"
	[ $m -lt "${SOAK_MIN:-30}" ] && sleep 60
done
# Growth from the second half's first sample to the end; warm-up (JIT, first downloads) is left out.
mid=${samples[$(( ${#samples[@]} / 2 ))]}; last=${samples[-1]}
read -r r1 t1 f1 k1 <<< "$mid"; read -r r2 t2 f2 k2 <<< "$last"
check "memory steady (second half grows < 15 %)" "$(( r2 * 100 / r1 < 115 ))" 1
check "threads steady" "$(( t2 - t1 <= 5 ))" 1
check "open files steady" "$(( f2 - f1 <= 10 ))" 1
check "child processes steady (yt-dlp/ffmpeg are not left behind)" "$(( k2 <= 4 ))" 1
check "still playing" "$(playing)" yes
check "played songs in the history" "$(( $(wc -l < $T/data/bots/1/history.jsonl) > 10 ))" 1
# Panda's own CPU (not ffmpeg's) over the run, in % of one core.
CPU=$(( ($(cpu) - cpu0) * 100 / TICKS / ($(date +%s) - t0) ))
echo "   Panda used $CPU % of one core on average"
check "CPU under 25 % of one core with the equalizer and 8D" "$(( CPU < 25 ))" 1
check "crossfades happened" "$(( $(grep -c 'Crossfading from' $T/log.txt) > 0 ))" 1

echo; echo "--- crashes or errors in Panda's log:"
grep -E " (fail|crit): |Unhandled|Exception" "$T/log.txt" | grep -v "Could not play\|did not start\|broke off" | head -10 || echo "(none)"
[ "${KEEP:-0}" = 1 ] || bash "$HERE/env.sh" stop
echo; echo "resilience: $failed failed"
exit $failed
