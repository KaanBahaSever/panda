#!/bin/bash
# WSL: the panel API with API keys, against the running test Panda from env.sh (port 8097):
# a key logs in as its role, a "dj" key can't do admin things, a revoked key stops working, the
# live stream works with a key; the server's people and the bot's permissions come back.
# usage: api.sh      Exit code = failed checks.
set -u
P=http://127.0.0.1:8097
J=/tmp/panda-api-cookies
failed=0
check() { if [ "$2" = "$3" ]; then echo "   PASS $1 ($2)"; else echo "   FAIL $1: \"$2\", expected \"$3\""; failed=$((failed + 1)); fi; }
code() { curl -s -m 10 -o /dev/null -w '%{http_code}' "$@"; }
json() { python3 -c "import sys,json; d=json.load(sys.stdin); print($1)"; }

curl -s -c $J -H 'Content-Type: application/json' -d '{"name":"admin","password":"testpass123"}' $P/api/login -o /dev/null
ADMIN=$(curl -s -b $J -H 'Content-Type: application/json' -d '{"name":"script","role":"admin"}' $P/api/apikeys | json "d['secret']")
DJ=$(curl -s -b $J -H 'Content-Type: application/json' -d '{"name":"dj script","role":"dj"}' $P/api/apikeys | json "d['secret']")
check "keys look right" "$(echo "$ADMIN $DJ" | grep -cE '^pk_[A-Za-z0-9_-]{40,} pk_[A-Za-z0-9_-]{40,}$')" 1
check "the list never shows a key" "$(curl -s -b $J $P/api/apikeys | grep -c pk_)" 0

check "no key: not logged in" "$(code $P/api/bots)" 401
check "wrong key" "$(code -H 'Authorization: Bearer pk_wrong' $P/api/bots)" 401
check "admin key reads" "$(code -H "Authorization: Bearer $ADMIN" $P/api/bots)" 200
check "admin key, X-Api-Key header" "$(code -H "X-Api-Key: $ADMIN" $P/api/me)" 200
check "admin key is admin" "$(curl -s -H "Authorization: Bearer $ADMIN" $P/api/me | json "d['role']")" admin
check "admin key changes settings" "$(curl -s -H "Authorization: Bearer $ADMIN" $P/api/bots/1/settings | curl -s -o /dev/null -w '%{http_code}' -X PUT -H "Authorization: Bearer $ADMIN" -H 'Content-Type: application/json' -d @- $P/api/bots/1/settings)" 200
check "dj key adds a song" "$(code -H "Authorization: Bearer $DJ" -H 'Content-Type: application/json' -d '{"query":"test tone"}' $P/api/bots/1/play)" 200
check "dj key can't change settings" "$(code -H "Authorization: Bearer $DJ" $P/api/bots/1/settings)" 403
check "dj key can't make keys" "$(code -H "Authorization: Bearer $DJ" -H 'Content-Type: application/json' -d '{"name":"x","role":"admin"}' $P/api/apikeys)" 403
check "a key has no password to change" "$(code -H "Authorization: Bearer $DJ" -H 'Content-Type: application/json' -d '{"current":"x","new":"yyyyyy"}' $P/api/password)" 400
# The live stream with a key: it sends the state, and ends when the key is revoked.
curl -s -N -m 30 -H "Authorization: Bearer $DJ" $P/api/bots/1/events > /tmp/panda-api-sse.txt & SSE=$!
sleep 3
check "live stream with a key" "$(grep -c '^data: ' /tmp/panda-api-sse.txt | awk '{print ($1 > 0)}')" 1
DJID=$(curl -s -b $J $P/api/apikeys | json "[k['id'] for k in d if k['name']=='dj script'][0]")
check "revoke" "$(code -b $J -X DELETE $P/api/apikeys/$DJID)" 204
start=$(date +%s); wait $SSE; check "the stream ends at once when the key is revoked" "$(( $(date +%s) - start < 5 ))" 1
check "revoked key" "$(code -H "Authorization: Bearer $DJ" $P/api/bots)" 401
check "last used is noted" "$(curl -s -b $J $P/api/apikeys | json "[k['lastUsed'] is not None for k in d if k['name']=='script'][0]")" True

echo "== the server's people and the bot's permissions"
check "people: connected" "$(curl -s -b $J $P/api/bots/1/ts/people | json "d['connected']")" True
check "people: groups seen" "$(curl -s -b $J $P/api/bots/1/ts/people | json "len(d['groups']) > 0")" True
check "permissions: can be heard" "$(curl -s -b $J $P/api/bots/1/ts/permissions | json "[c['ok'] for c in d if c['key']=='talk'][0]")" True
check "permissions: groups" "$(curl -s -b $J $P/api/bots/1/ts/permissions | json "[c['ok'] for c in d if c['key']=='groups'][0]")" True
check "removing a key that isn't there" "$(code -H "Authorization: Bearer $ADMIN" -X DELETE $P/api/apikeys/nope)" 404

echo; echo "api: $failed failed"
exit $failed
