#!/bin/bash
# Test stand-in for yt-dlp: searches always find "Fake <words>" (90 s), SoundCloud's "SC <words>", YouTube
# Music's "Music <words>"; soundcloud.com links are "SC Linked"; every song is the test tone.
# YouTube's failures are acted out on request: the file $FAKE/fail holds "<mode> <count>" and the next
# <count> calls of that kind fail:
#   403 N      downloads fail at once with HTTP 403        cut N   downloads break off after a few seconds
#   search N   searches and lookups fail with HTTP 403     unavailable N   downloads fail: video unavailable
# Every call is appended to $FAKE/calls.
FAKE=${FAKE_YTDLP_DIR:-$HOME/panda-test-env/fake-yt-dlp}
TONE=$FAKE/tone.webm
echo "$*" >> "$FAKE/calls"
[ "${1:-}" = --version ] && { echo 2026.01.01; exit 0; }

url=${!#}
out=""; json=0; prev=""
for a in "$@"; do
	[ "$prev" = -o ] && out=$a
	[ "$a" = -J ] && json=1
	prev=$a
done

# Takes one failure of the given kind, if any are left.
fail() {
	read -r mode count < "$FAKE/fail" 2>/dev/null || return 1
	case " $* " in *" $mode "*) ;; *) return 1 ;; esac
	[ "${count:-0}" -gt 0 ] || return 1
	echo "$mode $((count - 1))" > "$FAKE/fail"
}
id_for() { printf 'fk%s' "$(printf '%s' "$1" | md5sum | cut -c1-9)"; }

if [ $json = 1 ]; then
	fail search && { echo "ERROR: [youtube] Unable to download API page: HTTP Error 403: Forbidden" >&2; exit 1; }
	case "$url" in
		ytsearch*:*)
			# "ytsearch5:words": "Fake words", "Fake words 2" ... "Fake words 5"
			q=${url#*:}; n=${url%%:*}; n=${n#ytsearch}; sep=""
			printf '{"entries":['
			for i in $(seq "${n:-1}"); do
				if [ "$i" = 1 ]; then id=$(id_for "$q"); t="Fake $q"; else id=$(id_for "$q|$i"); t="Fake $q $i"; fi
				printf '%s{"id":"%s","title":"%s","channel":"Fake","duration":90}' "$sep" "$id" "$t"; sep=,
				printf '%s\t%s\n' "$id" "$t" >> "$FAKE/titles"   # a later lookup of the video says the same title
			done
			printf ']}\n' ;;
		scsearch*:*)
			# SoundCloud: "SC words", "SC words 2"...
			q=${url#*:}; n=${url%%:*}; n=${n#scsearch}; sep=""
			printf '{"entries":['
			for i in $(seq "${n:-1}"); do
				if [ "$i" = 1 ]; then t="SC $q"; else t="SC $q $i"; fi
				slug=$(printf '%s' "$t" | tr ' ' '-' | tr -cd 'A-Za-z0-9-')
				printf '%s{"id":"55%s","title":"%s","uploader":"Fake SC","duration":90,"ie_key":"Soundcloud","webpage_url":"https://soundcloud.com/fake/%s"}' \
					"$sep" "$(printf '%s' "$t" | md5sum | cut -c1-8 | tr -cd '0-9')" "$t" "$slug"; sep=,
			done
			printf ']}\n' ;;
		https://music.youtube.com/search*)
			# YouTube Music only names the songs; their lookup (watch?v=) says the rest.
			q=$(python3 -c 'import sys,urllib.parse as u; print(u.parse_qs(u.urlparse(sys.argv[1]).query)["q"][0])' "$url")
			id=$(id_for "ytm|$q")
			printf '%s\t%s\n' "$id" "Music $q" >> "$FAKE/titles"
			printf '{"entries":[{"_type":"url","ie_key":"Youtube","id":"%s","title":"Music %s","url":"https://music.youtube.com/watch?v=%s"}]}\n' "$id" "$q" "$id" ;;
		https://soundcloud.com/*)
			t="SC Linked"; case "$url" in *fake/SC-*) t=$(printf '%s' "${url##*/}" | tr '-' ' ') ;; esac
			printf '{"id":"77%s","title":"%s","uploader":"Fake SC","duration":90,"extractor_key":"Soundcloud","webpage_url":"%s"}\n' \
				"$(printf '%s' "$url" | md5sum | cut -c1-8 | tr -cd '0-9')" "$t" "$url" ;;
		*list=RD*) echo '{"entries":[]}' ;;
		*v=*)
			id=${url##*v=}
			t=$(grep -m1 "^$id	" "$FAKE/titles" 2>/dev/null | cut -f2-); [ -n "$t" ] || t="Fake $id"
			printf '{"id":"%s","title":"%s","channel":"Fake","duration":90}\n' "$id" "$t" ;;
		*) echo "ERROR: unsupported URL: $url" >&2; exit 1 ;;
	esac
	exit 0
fi

send() { if [ "$out" = - ]; then cat; else cat > "$out"; fi; }
if fail 403; then echo "ERROR: unable to download video data: HTTP Error 403: Forbidden" >&2; exit 1; fi
if fail unavailable; then echo "ERROR: [youtube] x: Video unavailable" >&2; exit 1; fi
if fail cut; then head -c 30000 "$TONE" | send; echo "ERROR: unable to download video data: HTTP Error 403: Forbidden" >&2; exit 1; fi
send < "$TONE"
