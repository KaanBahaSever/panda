#!/bin/bash
# WSL: how long Panda takes to stop (SIGTERM) while a panel tab has its live streams open.
# It used to wait for the 30 s host timeout. usage: stoptime.sh <linux panda binary>   Fails above 5 s.
set -u
T=$HOME/panda-stop-test
rm -rf "$T"; mkdir -p "$T/data"
cp "$1" "$T/panda"; chmod +x "$T/panda"
printf '{ "bots": [ { "id": "1", "server": { "address": "127.0.0.1:1" } } ], "panel": { "port": 8099 } }\n' > "$T/data/config.json"
"$T/panda" --data "$T/data" set-password testpass123 admin >/dev/null
"$T/panda" --data "$T/data" > "$T/log.txt" 2>&1 & PID=$!
sleep 4
login=$(curl -s -c "$T/c" -H 'Content-Type: application/json' -d '{"name":"admin","password":"testpass123"}' http://127.0.0.1:8099/api/login -o /dev/null -w '%{http_code}')
[ "$login" = 204 ] || { echo "   FAIL Panda did not start or log in ($login)"; kill $PID 2>/dev/null; exit 1; }
curl -s -N -b "$T/c" http://127.0.0.1:8099/api/bots/1/events -o /dev/null & SSE=$!
curl -s -N -b "$T/c" http://127.0.0.1:8099/api/system/logs -o /dev/null & LOGS=$!
sleep 2
kill -0 $PID $SSE $LOGS 2>/dev/null || { echo "   FAIL the panel streams are not open"; kill $PID 2>/dev/null; exit 1; }
start=$(date +%s%N)
kill -TERM $PID
# A Panda that hangs must not hang the test.
for _ in $(seq 60); do kill -0 $PID 2>/dev/null || break; sleep 1; done
kill -0 $PID 2>/dev/null && { echo "   FAIL still running after 60 s"; kill -9 $PID; exit 1; }
ms=$(( ($(date +%s%N) - start) / 1000000 ))
kill $SSE $LOGS 2>/dev/null
rm -rf "$T"
if [ $ms -le 5000 ]; then echo "   PASS stopped in $ms ms"; else echo "   FAIL stopped in $ms ms"; exit 1; fi
