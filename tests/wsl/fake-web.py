#!/usr/bin/env python3
# Test stand-in for the web Panda reaches besides YouTube, on 127.0.0.1:8096 (the tests never leave the machine):
#   /radio              an old Shoutcast station ("ICY 200 OK") sending the test tone with the song title in between
#   /radio.m3u, .pls    playlist files that name /radio            /song.mp3   an audio file with title tags
#   /page.html          a page titled "test tone"                  /rb/...     Radio Browser (one "Test Station")
#   /spotify/embed/...  Spotify's embed pages (a song, a playlist)  /deezer/... Deezer's API (a song)
#   /sb/api/...         SponsorBlock (the test song: 0-5 s and 20-30 s are "music_offtopic")
# usage: fake-web.py <folder with radio.mp3 and song.mp3>
import json, os, sys, urllib.parse
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer

DIR = sys.argv[1]
METAINT = 8192
STATION = {
    "stationuuid": "a0000000-0000-4000-8000-000000000001", "name": "Test Station",
    "url": "http://127.0.0.1:8096/radio", "url_resolved": "http://127.0.0.1:8096/radio",
    "homepage": "", "favicon": "", "tags": "testing,lofi", "country": "Testland", "countrycode": "TL",
    "codec": "MP3", "bitrate": 128, "votes": 1, "hls": 0,
}


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
            return self.send(json.dumps([STATION] if "test" in name else []))
        if path.startswith("/rb/json/stations/byuuid/"):
            return self.send(json.dumps([STATION] if path.endswith(STATION["stationuuid"]) else []))
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

    # The test tone over and over, with "StreamTitle='Test Artist - Song A';" every METAINT bytes when asked for.
    def radio(self):
        meta = self.headers.get("Icy-MetaData") == "1"
        head = "ICY 200 OK\r\nicy-name: Test FM\r\nicy-genre: Testing\r\ncontent-type: audio/mpeg\r\n"
        if meta:
            head += f"icy-metaint: {METAINT}\r\n"
        self.wfile.write((head + "\r\n").encode())
        audio = open(os.path.join(DIR, "radio.mp3"), "rb").read()
        title = b"StreamTitle='Test Artist - Song A';"
        block = bytes([(len(title) + 15) // 16]) + title.ljust(((len(title) + 15) // 16) * 16, b"\0")
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
                    self.wfile.write(block)
        except (BrokenPipeError, ConnectionResetError):
            pass


ThreadingHTTPServer.daemon_threads = True
ThreadingHTTPServer(("127.0.0.1", 8096), Handler).serve_forever()
