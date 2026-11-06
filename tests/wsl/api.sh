#!/bin/bash
# WSL: the panel API with API keys, against the running test Panda from env.sh (port 8097):
# a key logs in as its role, a "dj" key can't do admin things, a revoked key stops working, the
# live stream works with a key; the server's people and the bot's permissions come back; internet radio
# (the stand-in Radio Browser of fake-web.py) and the sources and YouTube settings.
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
check "two keys can't share a name" "$(code -b $J -H 'Content-Type: application/json' -d '{"name":"Script","role":"dj"}' $P/api/apikeys)" 400
check "no BBCode in key names" "$(code -b $J -H 'Content-Type: application/json' -d '{"name":"[b]x[/b]","role":"dj"}' $P/api/apikeys)" 400
check "admin key changes settings" "$(curl -s -H "Authorization: Bearer $ADMIN" $P/api/bots/1/settings | curl -s -o /dev/null -w '%{http_code}' -X PUT -H "Authorization: Bearer $ADMIN" -H 'Content-Type: application/json' -d @- $P/api/bots/1/settings)" 200
check "dj key adds a song" "$(code -H "Authorization: Bearer $DJ" -H 'Content-Type: application/json' -d '{"query":"test tone"}' $P/api/bots/1/play)" 200
check "dj key can't change settings" "$(code -H "Authorization: Bearer $DJ" $P/api/bots/1/settings)" 403
check "dj key can't make keys" "$(code -H "Authorization: Bearer $DJ" -H 'Content-Type: application/json' -d '{"name":"x","role":"admin"}' $P/api/apikeys)" 403
check "a key has no password to change" "$(code -H "Authorization: Bearer $DJ" -H 'Content-Type: application/json' -d '{"current":"x","new":"yyyyyy"}' $P/api/password)" 400
# The live stream with a key: it sends the state, and ends when the key is revoked.
curl -s -N -m 30 -H "Authorization: Bearer $DJ" $P/api/bots/1/events > /tmp/panda-api-sse.txt & SSE=$!
sleep 3
check "live stream with a key" "$(grep -c '^data: ' /tmp/panda-api-sse.txt | awk '{print ($1 > 0)}')" 1
check "the stream is open" "$(kill -0 $SSE 2>/dev/null && echo yes)" yes
DJID=$(curl -s -b $J $P/api/apikeys | json "[k['id'] for k in d if k['name']=='dj script'][0]")
check "revoke" "$(code -b $J -X DELETE $P/api/apikeys/$DJID)" 204
start=$(date +%s); wait $SSE; check "the stream ends at once when the key is revoked" "$(( $(date +%s) - start < 5 ))" 1
check "revoked key" "$(code -H "Authorization: Bearer $DJ" $P/api/bots)" 401
check "last used is noted" "$(curl -s -b $J $P/api/apikeys | json "[k['lastUsed'] is not None for k in d if k['name']=='script'][0]")" True

echo "== the server's people and the bot's permissions"
check "people: connected" "$(curl -s -b $J $P/api/bots/1/ts/people | json "d['connected']")" True
check "people: groups seen" "$(curl -s -b $J $P/api/bots/1/ts/people | json "len(d['groups']) > 0")" True
check "permissions: can be heard" "$(curl -s -b $J $P/api/bots/1/ts/permissions | json "[c['ok'] for c in d if c['key']=='talk'][0]")" True
# The bot's channel wants more talk power than the bot has: it can't be heard.
Q() { curl -s -H "x-api-key: testkey-local-only" "http://127.0.0.1:10080/1/$1"; }
CID=$(Q clientlist | python3 -c "import sys,json; print(next(c['cid'] for c in json.load(sys.stdin)['body'] if 'Panda' in c['client_nickname']))")
Q "channeledit?cid=$CID&channel_needed_talk_power=100" >/dev/null; sleep 1
check "permissions: can't be heard in a channel that needs talk power 100" "$(curl -s -b $J $P/api/bots/1/ts/permissions | json "[c['ok'] for c in d if c['key']=='talk'][0]")" False
Q "channeledit?cid=$CID&channel_needed_talk_power=0" >/dev/null
check "permissions: groups" "$(curl -s -b $J $P/api/bots/1/ts/permissions | json "[c['ok'] for c in d if c['key']=='groups'][0]")" True
check "removing a key that isn't there" "$(code -H "Authorization: Bearer $ADMIN" -X DELETE $P/api/apikeys/nope)" 404

echo "== internet radio"
UUID=a0000000-0000-4000-8000-000000000001
post() { curl -s -b $J -H 'Content-Type: application/json' -d "$2" "$P$1"; }
# Radio and local links on (a chat scenario may have left them off).
curl -s -b $J $P/api/bots/1/settings | python3 -c "
import sys, json
d = json.load(sys.stdin); d['sources']['radio'] = True; d['sources']['radioNames'] = True; d['sources']['localLinks'] = True; print(json.dumps(d))" \
	| curl -s -b $J -X PUT -H 'Content-Type: application/json' -d @- $P/api/bots/1/settings -o /dev/null
check "radio search" "$(curl -s -b $J "$P/api/radio/search?name=test" | json "d[0]['name']")" "Test Station"
check "a search needs something to look for" "$(curl -s -b $J "$P/api/radio/search" | json "len(d)")" 0
check "countries" "$(curl -s -b $J $P/api/radio/countries | json "d[0]['code']")" TL
check "favorite a station" "$(code -b $J -H 'Content-Type: application/json' -d "{\"uuid\":\"$UUID\"}" $P/api/radio/favorites)" 200
check "favorites" "$(curl -s -b $J $P/api/radio/favorites | json "[s['name'] for s in d]")" "['Test Station']"
check "what the saved stations play right now" "$(curl -s -b $J $P/api/radio/onair | json "d.get('$UUID')")" "Test Artist - Song A"
check "play a station" "$(post /api/bots/1/fm "{\"uuid\":\"$UUID\"}" | json "d['source']")" radio
# Stations are played by their id only: an address in the request is never used.
check "no station for an unknown id (an address is ignored)" "$(code -b $J -H 'Content-Type: application/json' -d '{"uuid":"nope","url":"http://10.0.0.1/"}' $P/api/bots/1/fm)" 404
check "remove the favorite" "$(code -b $J -X DELETE $P/api/radio/favorites/$UUID)" 204
# A station that only sends track numbers: what it plays comes from radio.net (the stand-in in fake-web.py).
# When radio.net answers in a shape Panda doesn't know, there's no name (and no error), it's said once in the
# log, and the names come back by themselves after a minute's rest when it answers again.
NUM=a0000000-0000-4000-8000-000000000002
onair() { curl -s -b $J $P/api/radio/onair | json "d.get('$NUM')"; }
curl -s "http://127.0.0.1:8096/rn-mode?m=ok" -o /dev/null
# What a chat scenario's bot heard from radio.net for the same station is kept 20 s.
sleep 21
check "favorite a station that sends numbers" "$(code -b $J -H 'Content-Type: application/json' -d "{\"uuid\":\"$NUM\"}" $P/api/radio/favorites)" 200
check "what it plays, from radio.net" "$(onair)" "Gino Vannelli - Black Cars"
curl -s "http://127.0.0.1:8096/rn-mode?m=odd" -o /dev/null; sleep 31
check "radio.net answers in another shape: no name" "$(onair)" None
check "said once in the log" "$(curl -s -b $J $P/api/system/logs.txt | grep -c "radio.net didn't answer as expected")" 1
curl -s "http://127.0.0.1:8096/rn-mode?m=ok" -o /dev/null; sleep 70
check "radio.net is fine again: the name is back by itself" "$(onair)" "Gino Vannelli - Black Cars"
check "and the log says so" "$(curl -s -b $J $P/api/system/logs.txt | grep -c "radio.net answers again")" 1
check "remove that favorite" "$(code -b $J -X DELETE $P/api/radio/favorites/$NUM)" 204
check "the panel takes any link" "$(post /api/bots/1/play '{"query":"http://127.0.0.1:8096/radio"}' | json "d['title']")" "Test FM"
check "a Spotify playlist says how many more come" "$(post /api/bots/1/play '{"query":"https://open.spotify.com/playlist/abcdefghij1234567890"}' | json "(d['more'], d['list'])")" "(2, 'Test Mix')"
curl -s -b $J -X POST $P/api/bots/1/control/stop -o /dev/null

echo "== the sources and YouTube settings"
curl -s -b $J $P/api/bots/1/settings > /tmp/panda-api-settings.json
check "the settings have the sources" "$(json "d['sources']['search']" < /tmp/panda-api-settings.json)" youtube
# Each change starts from the settings read above.
put() { python3 -c "
import sys, json
d = json.load(open('/tmp/panda-api-settings.json')); exec(sys.argv[1]); print(json.dumps(d))" "$1" | curl -s -b $J -X PUT -H 'Content-Type: application/json' -d @- $P/api/bots/1/settings; }
check "an unknown search is left as it was" "$(put "d['sources']['search'] = 'bing'" | json "d['sources']['search']")" youtube
check "yt-dlp clients with other characters are refused" "$(put "d['youTube']['retryPlayerClients'] = 'tv;rm -rf'" | json "d['youTube']['retryPlayerClients']")" "android_vr,tv,web_safari"
check "only known SponsorBlock categories" "$(put "d['youTube']['sponsorBlockCategories'] = ['sponsor', 'bogus']" | json "d['youTube']['sponsorBlockCategories']")" "['sponsor']"
check "settings without the sources (an older panel) keep them" "$(put "d['sources']['radio'] = False; d['sources']['localLinks'] = True" >/dev/null; put "del d['sources']; del d['youTube']" | json "(d['sources']['radio'], d['youTube']['sponsorBlockCategories'])")" "(False, ['music_offtopic'])"
put "pass" >/dev/null

echo "== sound"
sound() { curl -s -b $J -X PUT -H 'Content-Type: application/json' -d "$1" $P/api/bots/1/sound; }
check "an equalizer preset" "$(sound '{"equalizer":"rock"}' | json "(d['equalizer'], d['gains'][0])")" "('rock', 4)"
check "own bands are kept to ±12 dB" "$(sound '{"equalizer":"custom","bands":[20,-20,1,2,3,4,5,6,7,8]}' | json "d['bands'][:2]")" "[12, -12]"
check "the speed goes from 0.5 to 2" "$(code -b $J -X PUT -H 'Content-Type: application/json' -d '{"speed":3}' $P/api/bots/1/sound)" 400
check "ten bands" "$(code -b $J -X PUT -H 'Content-Type: application/json' -d '{"bands":[1,2]}' $P/api/bots/1/sound)" 400
check "unknown effect" "$(code -b $J -X PUT -H 'Content-Type: application/json' -d '{"effect":"reverse"}' $P/api/bots/1/sound)" 400
check "the state shows the sound" "$(curl -s -b $J $P/api/bots/1/state | json "d['player']['sound']['equalizer']")" custom
check "a field left out stays" "$(sound '{"eightD":true}' | json "(d['equalizer'], d['eightD'])")" "('custom', True)"
sound '{"equalizer":"off","eightD":false}' >/dev/null

echo "== importing playlists"
check "a YouTube playlist comes in at once" "$(post /api/playlists/import '{"url":"https://www.youtube.com/playlist?list=PLfake"}' | json "(d['name'], d['added'], d['coming'])")" "('Fake List', 3, 0)"
check "a Spotify playlist comes song by song" "$(post /api/playlists/import '{"url":"https://open.spotify.com/playlist/abcdefghij1234567890","name":"From Spotify"}' | json "(d['name'], d['coming'])")" "('From Spotify', 3)"
sleep 8
check "...and is all there after a while" "$(curl -s -b $J "$P/api/playlists/From%20Spotify" | json "len(d['songs'])")" 3
check "a link that isn't a playlist" "$(code -b $J -H 'Content-Type: application/json' -d '{"url":"not a link at all"}' $P/api/playlists/import)" 400

echo "== schedule (the server's time)"
AT=$(date -d '+1 min' +%H:%M)
put "d['schedule'] = [{'time': '$AT', 'days': [], 'action': 'playlist', 'target': 'Fake List'}, {'time': '25:00', 'days': [], 'action': 'stop'}]" >/dev/null
check "a valid entry is kept, a wrong time isn't" "$(curl -s -b $J $P/api/bots/1/settings | json "[e['time'] for e in d['schedule']]")" "['$AT']"
for _ in $(seq 40); do now=$(curl -s -b $J $P/api/bots/1/state | json "(d['player']['current'] or {}).get('track', {}).get('title', '')"); [ "${now#List Song}" != "$now" ] && break; sleep 3; done
check "at $AT the schedule played the list" "$(echo "$now" | cut -c1-9)" "List Song"
put "pass" >/dev/null
curl -s -b $J -X POST $P/api/bots/1/control/stop -o /dev/null

echo "== the public page and the app"
check "the public page is off unless turned on" "$(code $P/api/public/now)" 404
put "d['publicPage'] = True" >/dev/null
post /api/bots/1/play '{"query":"test tone"}' >/dev/null
check "on: anyone sees the song" "$(curl -s $P/api/public/now | json "d[0]['title']")" "Test Tone Song"
check "...but not who asked" "$(curl -s $P/api/public/now | grep -c 'requestedBy\|admin')" 0
check "the page itself" "$(code $P/now.html)" 200
check "the app manifest" "$(code $P/manifest.json)" 200
check "the service worker" "$(code $P/sw.js)" 200
put "pass" >/dev/null
curl -s -b $J -X POST $P/api/bots/1/control/stop -o /dev/null

echo "== playlists and the library"
check "playlists take songs from other sites" "$(post /api/playlists '{"name":"Sites","songs":[{"id":"soundcloud:123","title":"SC Song","channel":"x","duration":90,"source":"link","link":"https://soundcloud.com/a/b"}]}' | json "len(d['songs'])")" 1
check "but not with a link that isn't a web page" "$(post /api/playlists '{"name":"Sites","songs":[{"id":"x:1","title":"Bad","channel":"x","duration":9,"source":"link","link":"javascript:alert(1)"}]}' | json "len(d['songs'])")" 1
check "nor a station" "$(post /api/playlists '{"name":"Sites","songs":[{"id":"radio:x","title":"FM","channel":"x","duration":0,"source":"radio","link":"http://x.y/"}]}' | json "len(d['songs'])")" 1
check "the library deletes songs only by their id" "$(code -b $J -X DELETE "$P/api/library/..%2F..%2Fconfig")" 409

echo "== the song in the bot's name"
nick() { Q clientlist | python3 -c "import sys,json; print(next(c['client_nickname'] for c in json.load(sys.stdin)['body'] if c['client_nickname'].startswith('Panda')))"; }
playing() { curl -s -b $J $P/api/bots/1/state | json "(d['player']['current'] or {}).get('track', {}).get('title', '-')"; }
OWN=$(nick)
post /api/bots/1/play '{"query":"test tone"}' >/dev/null; sleep 4
check "off: the name stays while a song plays" "$(playing) / $(nick)" "Test Tone Song / $OWN"
# Renames come together, 10 s apart at the most often.
put "d['songInNickname'] = True" >/dev/null; sleep 4
check "on: the name shows the song" "$(nick)" "$OWN: Test Tone Song"
put "d['songInNickname'] = True; d['songNicknameFormat'] = '{name} ♪ {song} and a long tail'" >/dev/null; sleep 12
check "its own format, cut to TeamSpeak's 30 characters at most" "$(nick | python3 -c "import sys; n=sys.stdin.read().strip(); print(len(n) <= 30, n.endswith('…'), n.startswith('$OWN ♪ Test'))")" "True True True"
check "a format without the bot's name is refused" "$(put "d['songInNickname'] = True; d['songNicknameFormat'] = '{song}'" | json "d['songNicknameFormat']")" "{name} ♪ {song} and a long tail"
check "a panel that doesn't know the option leaves it on" "$(put "d['songInNickname'] = True; d.pop('songInNickname')" | json "d['songInNickname']")" True
put "pass" >/dev/null; sleep 12
check "turned off while playing: its own name again" "$(playing) / $(nick)" "Test Tone Song / $OWN"
put "d['songInNickname'] = True" >/dev/null; sleep 12
curl -s -b $J -X POST $P/api/bots/1/control/stop -o /dev/null; sleep 12
check "nothing plays: its own name again" "$(nick)" "$OWN"
put "pass" >/dev/null

echo "== the bot's picture"
Q() { curl -s -H "x-api-key: testkey-local-only" "http://127.0.0.1:10080/1/$1"; }
# The flag (md5 of the picture) the TeamSpeak server has for bot 1.
server_avatar() {
	local nick clid
	nick=$(curl -s -b $J $P/api/bots/1/settings | json "d['nickname']")
	clid=$(Q clientlist | NICK="$nick" python3 -c "import sys,json,os; print([c['clid'] for c in json.load(sys.stdin)['body'] if c['client_nickname']==os.environ['NICK']][0])")
	Q "clientinfo?clid=$clid" | python3 -c "import sys,json; print(json.load(sys.stdin)['body'][0].get('client_flag_avatar',''))"
}
avatar_kind() { curl -s -b $J -D - -o /dev/null $P/api/bots/1/avatar | tr -d '\r' | sed -n 's/^X-Avatar: //Ip'; }
check "Panda's picture to begin with" "$(avatar_kind)" panda
check "...and the server has it" "$(server_avatar)" "$(curl -s -b $J $P/api/bots/1/avatar | md5sum | cut -c1-32)"
python3 -c "
import struct, zlib
def chunk(t, d): return struct.pack('>I', len(d)) + t + d + struct.pack('>I', zlib.crc32(t + d) & 0xffffffff)
raw = b''.join(b'\x00' + bytes([200, 40, 90]) * 64 for _ in range(64))
open('/tmp/panda-avatar.png', 'wb').write(b'\x89PNG\r\n\x1a\n' + chunk(b'IHDR', struct.pack('>IIBBBBB', 64, 64, 8, 2, 0, 0, 0)) + chunk(b'IDAT', zlib.compress(raw)) + chunk(b'IEND', b''))"
MINE=$(md5sum < /tmp/panda-avatar.png | cut -c1-32)
check "a new picture goes to TeamSpeak" "$(curl -s -b $J -X PUT -H 'Content-Type: image/png' --data-binary @/tmp/panda-avatar.png $P/api/bots/1/avatar | json "d['applied']")" True
check "...the panel shows it" "$(avatar_kind) $(curl -s -b $J $P/api/bots/1/avatar | md5sum | cut -c1-32)" "custom $MINE"
check "...and the server has it" "$(server_avatar)" "$MINE"
check "not a picture" "$(code -b $J -X PUT -H 'Content-Type: image/png' --data-binary 'hello' $P/api/bots/1/avatar)" 400
check "too large" "$(head -c 1100000 /dev/zero | code -b $J -X PUT -H 'Content-Type: image/png' --data-binary @- $P/api/bots/1/avatar)" 400
check "a dj key can't change it" "$(code -X PUT -H "Authorization: Bearer $DJ" -H 'Content-Type: image/png' --data-binary @/tmp/panda-avatar.png $P/api/bots/1/avatar)" 401
check "not from another site" "$(code -b $J -X PUT -H 'Sec-Fetch-Site: cross-site' -H 'Content-Type: image/png' --data-binary @/tmp/panda-avatar.png $P/api/bots/1/avatar)" 403
curl -s -b $J $P/api/backup -o /tmp/panda-backup.zip
check "the picture is in the backup" "$(python3 -c "import zipfile; print('bots/1/avatar' in zipfile.ZipFile('/tmp/panda-backup.zip').namelist())")" True
check "back to Panda's" "$(curl -s -b $J -X DELETE $P/api/bots/1/avatar | json "d['applied']") $(avatar_kind)" "True panda"
check "...on the server too" "$(server_avatar)" "$(curl -s -b $J $P/api/bots/1/avatar | md5sum | cut -c1-32)"
# The custom one again, for the backup below.
curl -s -b $J -X PUT -H 'Content-Type: image/png' --data-binary @/tmp/panda-avatar.png $P/api/bots/1/avatar -o /dev/null

echo "== login behind a proxy (X-Forwarded-For from this machine is believed)"
for i in 1 2 3 4 5; do code -H 'X-Forwarded-For: 203.0.113.7' -H 'Content-Type: application/json' -d '{"name":"admin","password":"wrong"}' $P/api/login >/dev/null; done
check "5 wrong passwords lock that visitor out" "$(code -H 'X-Forwarded-For: 203.0.113.7' -H 'Content-Type: application/json' -d '{"name":"admin","password":"testpass123"}' $P/api/login)" 429
check "...not the others behind the same proxy" "$(code -H 'X-Forwarded-For: 203.0.113.8' -H 'Content-Type: application/json' -d '{"name":"admin","password":"testpass123"}' $P/api/login)" 204

echo "== backup (last: restoring one stops Panda)"
curl -s -b $J $P/api/backup -o /tmp/panda-backup.zip
check "a backup holds the settings and the identity" "$(python3 -c "import zipfile; n=zipfile.ZipFile('/tmp/panda-backup.zip').namelist(); print('config.json' in n, 'bots/1/identity.json' in n)")" "True True"
python3 -c "
import zipfile
with zipfile.ZipFile('/tmp/panda-evil.zip', 'w') as z:
    z.writestr('config.json', '{}'); z.writestr('../../evil.txt', 'x')"
check "a backup with other files is refused" "$(code -b $J -H 'Content-Type: application/zip' --data-binary @/tmp/panda-evil.zip $P/api/restore)" 400
check "not a zip" "$(code -b $J -H 'Content-Type: application/zip' --data-binary 'hello' $P/api/restore)" 400
check "a dj key can't restore" "$(code -H "Authorization: Bearer $DJ" -H 'Content-Type: application/zip' --data-binary @/tmp/panda-backup.zip $P/api/restore)" 401
check "restoring a backup" "$(code -b $J -H 'Content-Type: application/zip' --data-binary @/tmp/panda-backup.zip $P/api/restore)" 202
sleep 4
check "...and Panda restarts (here: stops; the service manager starts it again)" "$(code -m 2 $P/api/version)" 000
check "the backup waits to be put in place at the next start" "$([ -f "$HOME/panda-test-env/data/.restore/config.json" ] && echo yes)" yes

echo; echo "api: $failed failed"
exit $failed
