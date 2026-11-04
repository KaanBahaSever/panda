#!/bin/bash
# WSL: the panel's own HTTPS (panel.https). A self-signed pair is made on the first start (the key private to
# Panda), the panel answers only on HTTPS with a Secure session cookie, HSTS only when asked, the pair is kept on
# the next start, a renewed certificate is read again with its chain, and a missing key or a mistyped file stops
# Panda with a message. Also: with panel.trustCloudflare, CF-Connecting-IP is believed only from Cloudflare.
# usage: https.sh <linux panda binary>
set -u
T=$HOME/panda-https-test
P=https://127.0.0.1:8094
fails=0
check() { if [ "$2" = "$3" ]; then echo "   PASS $1 ($2)"; else echo "   FAIL $1: got '$2', expected '$3'"; fails=$((fails + 1)); fi; }
start() {
	"$T/panda" --data "$T/data" > "$T/log.txt" 2>&1 & PID=$!
	for _ in $(seq 20); do curl -sk -o /dev/null $P/ && return; sleep 1; done
}
served() { echo | openssl s_client -connect 127.0.0.1:8094 2>/dev/null | openssl x509 -noout -fingerprint -sha256 | cut -d= -f2; }

rm -rf "$T"; mkdir -p "$T/data"
cp "$1" "$T/panda"; chmod +x "$T/panda"
printf '{ "bots": [ { "id": "1", "server": { "address": "127.0.0.1:1" } } ], "panel": { "port": 8094, "trustCloudflare": true, "https": { "enabled": true } } }\n' > "$T/data/config.json"
"$T/panda" --data "$T/data" set-password testpass123 admin >/dev/null
start
check "a self-signed pair is made" "$(ls "$T/data" | grep -c -E '^(cert|key)\.pem$')" 2
check "...its key only for Panda" "$(stat -c %a "$T/data/key.pem")" 600
made=$(served)
check "...its fingerprint is in the log" "$(grep -c "SHA-256 $made" "$T/log.txt")" 1
check "the panel on HTTPS" "$(curl -sk -o /dev/null -w '%{http_code}' $P/)" 200
hsts() { curl -sk -D - -o /dev/null $P/ | tr -d '\r' | grep -i '^strict-transport-security' | cut -d' ' -f2; }
check "no HSTS unless asked" "$(hsts)" ""
check "no plain HTTP on that port" "$(curl -s -m 5 -o /dev/null -w '%{http_code}' http://127.0.0.1:8094/)" 000
check "the session cookie is Secure" "$(curl -sk -D - -o /dev/null -H 'Content-Type: application/json' \
	-d '{"name":"admin","password":"testpass123"}' $P/api/login | grep -i '^set-cookie: panda_session' | grep -c -i '; secure')" 1
# This machine isn't Cloudflare: a CF-Connecting-IP from here names nobody, the five tries are this machine's.
for n in 1 2 3 4 5; do
	curl -sk -o /dev/null -H "CF-Connecting-IP: 203.0.113.$n" -H 'Content-Type: application/json' -d '{"name":"admin","password":"wrong"}' $P/api/login
done
check "CF-Connecting-IP is believed only from Cloudflare" "$(curl -sk -o /dev/null -w '%{http_code}' -H 'CF-Connecting-IP: 203.0.113.9' \
	-H 'Content-Type: application/json' -d '{"name":"admin","password":"wrong"}' $P/api/login)" 429
# From Cloudflare through a proxy on this machine (X-Forwarded-For names a Cloudflare address): the visitor is
# the one in CF-Connecting-IP. Five tries lock that visitor out, not the next one.
cf() { curl -sk -o /dev/null -w '%{http_code}' -H 'X-Forwarded-For: 173.245.48.1' -H "CF-Connecting-IP: $1" \
	-H 'Content-Type: application/json' -d '{"name":"admin","password":"wrong"}' $P/api/login; }
for _ in 1 2 3 4 5; do cf 198.51.100.1 >/dev/null; done
check "from Cloudflare, five tries lock out that visitor" "$(cf 198.51.100.1)" 429
check "...not the next one" "$(cf 198.51.100.2)" 401
check "...an IPv6 visitor is counted by its /64" "$(for n in 1 2 3 4 5; do cf 2001:db8:1:2::$n >/dev/null; done; cf 2001:db8:1:2::99)" 429
kill $PID; wait $PID 2>/dev/null

sed -i 's/"hsts": false/"hsts": true/' "$T/data/config.json"
start
check "the next start keeps the pair" "$(served)" "$made"
check "HSTS when asked (panel.https.hsts)" "$(hsts)" max-age=31536000

# A certificate with its intermediate in one file (as Let's Encrypt's fullchain.pem), put in place while Panda runs.
C=$T/ca; mkdir -p "$C"
openssl req -x509 -newkey rsa:2048 -nodes -keyout "$C/root.key" -out "$C/root.pem" -days 2 -subj /CN=TestRoot 2>/dev/null
openssl req -newkey rsa:2048 -nodes -keyout "$C/inter.key" -out "$C/inter.csr" -subj /CN=TestIntermediate 2>/dev/null
printf 'basicConstraints=critical,CA:TRUE\nkeyUsage=critical,keyCertSign,cRLSign\n' > "$C/ca.ext"
openssl x509 -req -in "$C/inter.csr" -CA "$C/root.pem" -CAkey "$C/root.key" -CAcreateserial -days 2 -extfile "$C/ca.ext" -out "$C/inter.pem" 2>/dev/null
openssl req -newkey rsa:2048 -nodes -keyout "$C/leaf.key" -out "$C/leaf.csr" -subj /CN=localhost 2>/dev/null
printf 'subjectAltName=DNS:localhost,IP:127.0.0.1\n' > "$C/leaf.ext"
openssl x509 -req -in "$C/leaf.csr" -CA "$C/inter.pem" -CAkey "$C/inter.key" -CAcreateserial -days 2 -extfile "$C/leaf.ext" -out "$C/leaf.pem" 2>/dev/null
cat "$C/leaf.pem" "$C/inter.pem" > "$C/fullchain.pem"
cp "$C/leaf.key" "$T/data/key.pem"; cp "$C/fullchain.pem" "$T/data/cert.pem"
renewed=$(openssl x509 -in "$C/leaf.pem" -noout -fingerprint -sha256 | cut -d= -f2)
for _ in $(seq 20); do [ "$(served)" = "$renewed" ] && break; sleep 5; done
check "a renewed certificate is read again (within a minute)" "$(served)" "$renewed"
check "...sent with its intermediate" "$(echo | openssl s_client -connect 127.0.0.1:8094 -showcerts 2>/dev/null | grep -c 'BEGIN CERTIFICATE')" 2
check "...which a client trusting the root accepts" "$(curl -s --cacert "$C/root.pem" -o /dev/null -w '%{http_code}' https://localhost:8094/)" 200
kill $PID; wait $PID 2>/dev/null

rm "$T/data/key.pem"
timeout 20 "$T/panda" --data "$T/data" > "$T/log.txt" 2>&1
check "a missing key stops Panda" "$?" 1
check "...and says so" "$(grep -c 'key.pem is missing' "$T/log.txt")" 1

# A file named in the settings that isn't there is a mistake: no self-signed pair in its place.
rm -f "$T/data/cert.pem"
sed -i 's#"certFile": "cert.pem"#"certFile": "letsencrypt/fullchain.pem"#; s#"keyFile": "key.pem"#"keyFile": "letsencrypt/privkey.pem"#' "$T/data/config.json"
timeout 20 "$T/panda" --data "$T/data" > "$T/log.txt" 2>&1
check "a mistyped certificate file stops Panda" "$?" 1
check "...says which" "$(grep -c 'fullchain.pem is missing or can' "$T/log.txt")" 1
check "...and makes nothing there" "$(ls "$T/data/letsencrypt" 2>/dev/null | wc -l)" 0

rm -rf "$T"
echo; echo "https: $fails failed"
[ $fails = 0 ]
