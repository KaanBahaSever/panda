#!/usr/bin/env python3
# Test stand-in for the web Panda reaches besides YouTube, on 127.0.0.1:8096 (the tests never leave the machine):
#   /radio2             the same, its song changes every 25 s of audio (Song A, Song B)
#   /radio3             the same, its titles are track numbers ("2044 - 2044", as Nostalgie's)
#   /radio4             the same, named "TESTFM SAT MP3 - RK" (as Nostalgie's own links): Radio Browser knows it as Test FM
#   /radio              an old Shoutcast station ("ICY 200 OK") sending the test tone with the song title in between
#   /radio.m3u, .pls    playlist files that name /radio            /song.mp3   an audio file with title tags
#   /page.html          a page titled "test tone"                  /rb/...     Radio Browser (one "Test Station")
#   /spotify/embed/...  Spotify's embed pages (a song, a playlist)  /deezer/... Deezer's API (a song)
#   /sb/api/...         SponsorBlock (the test song: 0-5 s and 20-30 s are "music_offtopic")
#   /rn/...             radio.net: its search finds "Test FM" (whose stream is /radio3) and a decoy of the same name
#                       elsewhere; now-playing names Test FM's songs: Black Cars for its first two asks, then Musica
#                       (the count starts again after 40 s without one). /rn-mode?m=ok|odd|down: as it is, an
#                       answer of another shape, or an error (and the count starts again).
# usage: fake-web.py <folder with radio.mp3 and song.mp3>
import json, os, sys, time, urllib.parse
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer

DIR = sys.argv[1]
METAINT = 8192
STATION = {
    "stationuuid": "a0000000-0000-4000-8000-000000000001", "name": "Test Station",
    "url": "http://127.0.0.1:8096/radio", "url_resolved": "http://127.0.0.1:8096/radio",
    "homepage": "", "favicon": "", "tags": "testing,lofi", "country": "Testland", "countrycode": "TL",
    "codec": "MP3", "bitrate": 128, "votes": 1, "hls": 0,
}
# A station whose stream only sends track numbers (as Nostalgie's): its names come from radio.net.
NUMBERS = dict(STATION, stationuuid="a0000000-0000-4000-8000-000000000002", name="Numbers FM",
               url="http://127.0.0.1:8096/radio3", url_resolved="http://127.0.0.1:8096/radio3")
# A pasted link radio.net doesn't list: Radio Browser knows it (without "?from=..."), as Test FM of Testland.
LINKED = dict(STATION, stationuuid="a0000000-0000-4000-8000-000000000004", name="Test FM",
              url="http://127.0.0.1:8096/radio4", url_resolved="http://127.0.0.1:8096/radio4")
RN = {"mode": "ok", "asks": 0, "last": 0.0}
RN_STATIONS = [
    {"id": "wrongfm", "name": "Test FM", "country": "Elsewhere", "streams": [{"url": "http://elsewhere.example/live", "contentFormat": "MP3"}]},
    {"id": "testfm", "name": "TEST FM", "country": "Testland", "streams": [{"url": "http://127.0.0.1:8096/radio3?origine=radio.net", "contentFormat": "MP3"}]},
]


def embed(entity):
    data = json.dumps({"props": {"pageProps": {"state": {"data": {"entity": entity}}}}})
    return f'<html><body><script id="__NEXT_DATA__" type="application/json">{data}</script></body></html>'


class Handler(BaseHTTPRequestHandler):
    protocol_version = "HTTP/1.0"

    def log_message(self, *args):
        pass

    def send(self, body, type="application/json", status=200):
        data = body.encode() if isinstance(body, str) else body
        self.send_response(status)
        self.send_header("Content-Type", type)
        self.send_header("Content-Length", str(len(data)))
        self.end_headers()
        self.wfile.write(data)

    def do_GET(self):
        url = urllib.parse.urlparse(self.path)
        q = urllib.parse.parse_qs(url.query)
        path = url.path
        if path == "/radio2":
            return self.radio(changing=True)
        if path == "/radio3":
            return self.radio(numbers=True)
        if path == "/radio":
            return self.radio()
        if path == "/radio.m3u":
            return self.send("#EXTM3U\n#EXTINF:-1,Test FM\nhttp://127.0.0.1:8096/radio\n", "audio/x-mpegurl")
        if path == "/radio.pls":
            return self.send("[playlist]\nNumberOfEntries=1\nFile1=http://127.0.0.1:8096/radio\nTitle1=Test FM\n", "audio/x-scpls")
        if path == "/song.mp3":
            return self.send(open(os.path.join(DIR, "song.mp3"), "rb").read(), "audio/mpeg")
        if path == "/page.html":
            return self.send("<html><head><title>test tone</title></head><body>a page</body></html>", "text/html; charset=utf-8")
        if path.startswith("/spotify/embed/track/"):
            return self.send(embed({"type": "track", "name": "test tone", "artists": [{"name": "Tester"}]}), "text/html")
        if path.startswith("/spotify/embed/playlist/"):
            return self.send(embed({"type": "playlist", "name": "Test Mix", "trackList": [
                {"title": "test tone", "subtitle": "Tester"}, {"title": "Second", "subtitle": "Fake"}, {"title": "Third", "subtitle": "Fake"}]}), "text/html")
        if path.startswith("/deezer/track/"):
            return self.send(json.dumps({"title": "Deezer Song", "artist": {"name": "Fake"}}))
        if path.startswith("/rb/json/stations/search"):
            name = q.get("name", [""])[0].lower()
            return self.send(json.dumps([STATION] if "test" in name else [NUMBERS] if "numbers" in name else []))
        if path.startswith("/rb/json/stations/byuuid/"):
            return self.send(json.dumps([s for s in (STATION, NUMBERS) if path.endswith(s["stationuuid"])]))
        if path == "/rn-mode":
            RN.update(mode=q.get("m", ["ok"])[0], asks=0)
            return self.send("{}")
        if path.startswith("/rn/"):
            return self.radio_net(path, q)
        if path == "/rb/json/stations/byurl":
            return self.send(json.dumps([LINKED] if q.get("url", [""])[0] == LINKED["url"] else []))
        if path == "/radio4":
            return self.radio(numbers=True, name="TESTFM SAT MP3 - RK")
        if path.startswith("/rb/json/url/"):
            return self.send("{}")
        if path.startswith("/rb/json/countries"):
            return self.send(json.dumps([{"name": "Testland", "iso_3166_1": "TL", "stationcount": 1}]))
        if path.startswith("/rb/json/tags"):
            return self.send(json.dumps([{"name": "testing", "stationcount": 1}]))
        if path.startswith("/sb/api/skipSegments/"):
            return self.send(json.dumps([{"videoID": "tstTone0001", "segments": [
                {"category": "music_offtopic", "actionType": "skip", "segment": [0, 5]},
                {"category": "music_offtopic", "actionType": "skip", "segment": [20, 30]}]}]))
        self.send('{"error":"not found"}', status=404)

    def radio_net(self, path, q):
        if RN["mode"] == "down":
            return self.send('{"error":"down"}', status=503)
        if RN["mode"] == "odd":
            return self.send(json.dumps({"unexpected": True}))
        if path == "/rn/stations/search":
            return self.send(json.dumps({"totalCount": len(RN_STATIONS), "playables": RN_STATIONS}))
        if path == "/rn/stations/now-playing":
            ids = q.get("stationIds", [""])[0].split(",")
            out = []
            if "testfm" in ids:
                if time.time() - RN["last"] > 40:
                    RN["asks"] = 0
                RN["asks"] += 1
                RN["last"] = time.time()
                # The second one ends in radio.net's own number, as RTL2's do ("… §7713783"): it isn't shown.
                out.append({"title": "Gino Vannelli - Black Cars" if RN["asks"] <= 2 else "Michel Sardou - Musica §7713783", "stationId": "testfm"})
            if "wrongfm" in ids:
                out.append({"title": "Wrong Artist - Wrong Song", "stationId": "wrongfm"})
            return self.send(json.dumps(out))
        self.send('{"error":"not found"}', status=404)

    # The test tone over and over, with "StreamTitle='Test Artist - Song A';" every METAINT bytes when asked for.
    def radio(self, changing=False, numbers=False, name="Test FM"):
        meta = self.headers.get("Icy-MetaData") == "1"
        head = f"ICY 200 OK\r\nicy-name: {name}\r\nicy-genre: Testing\r\ncontent-type: audio/mpeg\r\n"
        if meta:
            head += f"icy-metaint: {METAINT}\r\n"
        self.wfile.write((head + "\r\n").encode())
        audio = open(os.path.join(DIR, "radio.mp3"), "rb").read()
        sent = [0]
        def block_now():
            # /radio2: Song A and Song B take turns every 50 blocks (25 s of the 128 kbps audio). Counted in what
            # was sent, not by the clock: the connection holds many seconds of audio the bot hasn't played yet.
            sent[0] += 1
            song = "Song B" if changing and (sent[0] // 50) % 2 else "Song A"
            title = ("StreamTitle='2044 - 2044';" if numbers else f"StreamTitle='Test Artist - {song}';").encode()
            return bytes([(len(title) + 15) // 16]) + title.ljust(((len(title) + 15) // 16) * 16, b"\0")
        pos = 0
        try:
            while True:
                chunk = b""
                while len(chunk) < METAINT:
                    take = audio[pos:pos + METAINT - len(chunk)]
                    chunk += take
                    pos = (pos + len(take)) % len(audio)
                self.wfile.write(chunk)
                if meta:
                    self.wfile.write(block_now())
        except (BrokenPipeError, ConnectionResetError):
            pass


ThreadingHTTPServer.daemon_threads = True
ThreadingHTTPServer(("127.0.0.1", 8096), Handler).serve_forever()
