#!/usr/bin/env bash
# Panda 🐼 installer / updater for Linux (systemd).
#
#   curl -fsSL https://raw.githubusercontent.com/KaanBahaSever/panda/main/scripts/install.sh | sudo bash
#
# Run it again to update. Options (all optional):
#   --server <host[:port]>   TeamSpeak server address (default: 127.0.0.1)
#   --port <port>            web panel port (default: 8080)
#   --version <tag>          install a specific release instead of the latest
#   --file <panda.tar.gz>    install from a local release archive
#   --uninstall              remove Panda (keeps /var/lib/panda unless --purge)
#   --purge                  with --uninstall: also delete settings and data
set -euo pipefail

REPO="KaanBahaSever/panda"
APP_DIR=/opt/panda
DATA_DIR=/var/lib/panda
SERVICE=/etc/systemd/system/panda.service

SERVER=""
PORT=""
VERSION="latest"
FILE=""
UNINSTALL=0
PURGE=0

green() { printf '\033[32m%s\033[0m\n' "$*"; }
bold()  { printf '\033[1m%s\033[0m\n' "$*"; }
info()  { printf '  %s\n' "$*"; }
die()   { printf '\033[31merror:\033[0m %s\n' "$*" >&2; exit 1; }

while [ $# -gt 0 ]; do
	case "$1" in
		--server) SERVER="$2"; shift 2 ;;
		--port) PORT="$2"; shift 2 ;;
		--version) VERSION="$2"; shift 2 ;;
		--file) FILE="$2"; shift 2 ;;
		--uninstall) UNINSTALL=1; shift ;;
		--purge) PURGE=1; shift ;;
		-h|--help) sed -n '2,14p' "$0"; exit 0 ;;
		*) die "unknown option: $1" ;;
	esac
done

[ "$(id -u)" -eq 0 ] || die "please run as root (sudo)"
command -v systemctl >/dev/null || die "systemd is required"

if [ "$UNINSTALL" = 1 ]; then
	systemctl disable --now panda.service panda-update.timer 2>/dev/null || true
	rm -f "$SERVICE" /etc/systemd/system/panda-update.service /etc/systemd/system/panda-update.timer
	systemctl daemon-reload
	rm -rf "$APP_DIR"
	if [ "$PURGE" = 1 ]; then rm -rf "$DATA_DIR"; userdel panda 2>/dev/null || true; fi
	green "Panda removed."
	[ "$PURGE" = 1 ] || info "Settings are still in $DATA_DIR (use --uninstall --purge to delete them)."
	exit 0
fi

# Ask only when there is a terminal (curl | bash still has /dev/tty).
ask() {
	local prompt="$1" default="$2" answer=""
	if [ -r /dev/tty ] && [ -w /dev/tty ]; then
		printf '%s [%s]: ' "$prompt" "$default" > /dev/tty
		read -r answer < /dev/tty || true
	fi
	printf '%s' "${answer:-$default}"
}

case "$(uname -m)" in
	x86_64|amd64) RID=linux-x64; YTDLP=yt-dlp_linux; DENO=deno-x86_64-unknown-linux-gnu.zip ;;
	aarch64|arm64) RID=linux-arm64; YTDLP=yt-dlp_linux_aarch64; DENO=deno-aarch64-unknown-linux-gnu.zip ;;
	*) die "unsupported CPU: $(uname -m) (x86_64 and arm64 are supported)" ;;
esac

bold "🐼 Installing Panda"

FIRST_INSTALL=0
[ -f "$DATA_DIR/config.json" ] || FIRST_INSTALL=1

info "Installing system packages (ffmpeg, curl, unzip)…"
if command -v apt-get >/dev/null; then
	export DEBIAN_FRONTEND=noninteractive
	apt-get update -qq
	apt-get install -y -qq ffmpeg curl unzip ca-certificates >/dev/null
elif command -v dnf >/dev/null; then
	dnf install -y -q ffmpeg-free curl unzip ca-certificates >/dev/null || dnf install -y -q ffmpeg curl unzip >/dev/null
elif command -v pacman >/dev/null; then
	pacman -Sy --noconfirm --needed ffmpeg curl unzip >/dev/null
else
	command -v ffmpeg >/dev/null || die "please install ffmpeg, curl and unzip first"
fi

id panda >/dev/null 2>&1 || useradd --system --home-dir "$DATA_DIR" --shell /usr/sbin/nologin panda
mkdir -p "$APP_DIR/bin" "$DATA_DIR"
chown panda:panda "$DATA_DIR"
chmod 750 "$DATA_DIR"

TMP=$(mktemp -d)
trap 'rm -rf "$TMP"' EXIT

if [ "$VERSION" = latest ]; then
	URL="https://github.com/$REPO/releases/latest/download/panda-$RID.tar.gz"
else
	URL="https://github.com/$REPO/releases/download/$VERSION/panda-$RID.tar.gz"
fi
if [ -n "$FILE" ]; then
	cp "$FILE" "$TMP/panda.tar.gz"
else
	info "Downloading Panda ($RID)…"
	curl -fsSL "$URL" -o "$TMP/panda.tar.gz" || die "download failed: $URL"
fi
tar -xzf "$TMP/panda.tar.gz" -C "$TMP"
install -m 755 "$TMP/panda" "$APP_DIR/panda"

# yt-dlp talks to YouTube; deno lets it solve YouTube's JavaScript challenges.
info "Downloading yt-dlp and deno…"
curl -fsSL "https://github.com/yt-dlp/yt-dlp/releases/latest/download/$YTDLP" -o "$TMP/yt-dlp"
install -m 755 "$TMP/yt-dlp" "$APP_DIR/bin/yt-dlp"
curl -fsSL "https://github.com/denoland/deno/releases/latest/download/$DENO" -o "$TMP/deno.zip"
unzip -o -q "$TMP/deno.zip" -d "$TMP"
install -m 755 "$TMP/deno" "$APP_DIR/bin/deno"

PANEL_PASSWORD=""
if [ "$FIRST_INSTALL" = 1 ]; then
	[ -n "$SERVER" ] || SERVER=$(ask "TeamSpeak server address" "127.0.0.1")
	[ -n "$PORT" ] || PORT=$(ask "Web panel port" "8080")
	case "$PORT" in ''|*[!0-9]*) die "invalid port: $PORT" ;; esac
	# Plain JSON; quotes and backslashes in the address are not valid anyway.
	case "$SERVER" in *[\"\\]*) die "invalid server address" ;; esac
	cat > "$DATA_DIR/config.json" <<-EOF
	{
	  "server": { "address": "$SERVER" },
	  "panel": { "port": $PORT },
	  "youTube": { "ytDlpPath": "$APP_DIR/bin/yt-dlp" }
	}
	EOF
	chown panda:panda "$DATA_DIR/config.json"
	chmod 600 "$DATA_DIR/config.json"
	PANEL_PASSWORD=$(tr -dc 'A-Za-z0-9' < /dev/urandom | head -c 14 || true)
	runuser -u panda -- env HOME="$DATA_DIR" "$APP_DIR/panda" --data "$DATA_DIR" set-password "$PANEL_PASSWORD" >/dev/null
fi

info "Setting up the service…"
install -m 644 "$TMP/panda.service" "$SERVICE"

# Keep yt-dlp fresh: YouTube changes often and old versions stop working.
cat > /etc/systemd/system/panda-update.service <<EOF
[Unit]
Description=Update yt-dlp for Panda

[Service]
Type=oneshot
ExecStart=$APP_DIR/bin/yt-dlp -U
EOF
cat > /etc/systemd/system/panda-update.timer <<EOF
[Unit]
Description=Update yt-dlp for Panda daily

[Timer]
OnCalendar=daily
RandomizedDelaySec=1h
Persistent=true

[Install]
WantedBy=timers.target
EOF

systemctl daemon-reload
systemctl enable --now panda-update.timer >/dev/null 2>&1
systemctl enable panda.service >/dev/null 2>&1
systemctl restart panda.service

PORT=$(grep -o '"port": *[0-9]*' "$DATA_DIR/config.json" | grep -o '[0-9]*$' || echo 8080)
IP=$(hostname -I 2>/dev/null | awk '{print $1}')
echo
green "🐼 Panda is running!"
info "Panel:     http://${IP:-your-server-ip}:${PORT}"
if [ -n "$PANEL_PASSWORD" ]; then
	info "Password:  $PANEL_PASSWORD   (change it in Settings)"
fi
info "Logs:      journalctl -u panda -f"
info "Update:    run this installer again"
echo
