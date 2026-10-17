# Panda tests

End-to-end tests against a real TeamSpeak 6 server and a real Panda build. They run on Linux, or
from Git Bash on Windows with the tests running in WSL.

```bash
tests/run.sh          # translations, chat commands, stop time
tests/run.sh all      # plus the panel update test (installs Panda in WSL as root)
```

## What runs

| Test | File | Checks |
|---|---|---|
| Translations | `i18n-check.mjs` | every panel language has every key; the chat languages match the panel and the settings |
| Chat | `wsl/chat.sh`, `chat/` | each scenario in `chat/scenarios/` against a fresh Panda with a generated test song (no YouTube) |
| Stop time | `wsl/stoptime.sh` | Panda stops within 5 s while a panel tab is open |
| Panel update | `wsl/upgrade.sh` | the latest release, installed with its own installer, updates from the panel to this build and keeps its settings, songs, playlists and the song playing |

## Setup (once)

- WSL (Ubuntu) with systemd, `ffmpeg`, `python3`, `curl`; on Windows also `node` and the .NET 8 SDK.
- A TeamSpeak 6 server in `~/ts6test/srv` in WSL (or set `PANDA_TS_DIR`): download it from
  teamspeak.com and accept its license yourself. `wsl/env.sh` starts it with the HTTP query on port
  10080 and a local-only test key, raises its flood limits and lets guests change their description.

## Chat scenarios

Each file in `chat/scenarios/` starts from the default settings and an empty queue:

```
# heading
s announce=false            change settings (bot 1)
c !play test tone           send to the channel (p = private message)
e Now playing               a reply to the last message contains this
x                           the last message got no reply
d ♪ Test Tone Song          the bot's description contains this (d ! = empty)
w 2                         wait 2 s
L Favorites 500             replace a playlist with 500 filler songs
```

The test password `testpass123` and the query key `testkey-local-only` are for these local test
instances only.
