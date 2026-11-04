#!/usr/bin/env bash
# Sets up a fresh Ubuntu 22.04/24.04 VPS to run the CMU game server with voice chat.
#
# Run as root on the VPS, from the folder holding SS14.Server_linux-x64.zip:
#   sudo bash setup-server.sh <domain> "<server name>" [duckdns-token]
# The domain must already point at this server (an A record). Examples:
#   sudo bash setup-server.sh voice.example.com "CMU Main"
#   sudo bash setup-server.sh cmu-voice.duckdns.org "CMU Main" 1234abcd-...
# The DuckDNS token is only needed for a duckdns.org name; it keeps that name pointed at this server.
#
# Safe to run again: it updates the server files and services but keeps an existing server_config.toml.
set -euo pipefail

if [[ $EUID -ne 0 ]]; then
    echo "Run this as root (sudo bash setup-server.sh ...)." >&2
    exit 1
fi

if [[ $# -lt 1 ]]; then
    echo "Usage: sudo bash setup-server.sh <domain> [\"server name\"] [duckdns-token]" >&2
    exit 1
fi

DOMAIN="$1"
SERVER_NAME="${2:-CMU}"
DUCKDNS_TOKEN="${3:-}"
SUBDOMAIN="${DOMAIN%.duckdns.org}"

if [[ "$DOMAIN" == *.duckdns.org && -z "$DUCKDNS_TOKEN" ]]; then
    echo "A duckdns.org name needs your DuckDNS token as the third argument." >&2
    exit 1
fi
PACKAGE="$(pwd)/SS14.Server_linux-x64.zip"
INSTALL_DIR=/opt/cmu-server
SERVICE_USER=cmu
DOTNET_DIR=/usr/share/dotnet

if [[ ! -f "$PACKAGE" ]]; then
    echo "SS14.Server_linux-x64.zip was not found in $(pwd). Upload it next to this script first." >&2
    exit 1
fi

echo "==> Installing system packages"
apt-get update
apt-get install -y curl unzip ufw debian-keyring debian-archive-keyring apt-transport-https gnupg

echo "==> Installing the .NET 10 runtime"
if [[ ! -x "$DOTNET_DIR/dotnet" ]] || ! "$DOTNET_DIR/dotnet" --list-runtimes | grep -q "Microsoft.NETCore.App 10\."; then
    curl -sSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh
    bash /tmp/dotnet-install.sh --channel 10.0 --runtime dotnet --install-dir "$DOTNET_DIR"
fi

echo "==> Installing Caddy"
if ! command -v caddy >/dev/null; then
    curl -1sLf 'https://dl.cloudsmith.io/public/caddy/stable/gpg.key' | gpg --dearmor -o /usr/share/keyrings/caddy-stable-archive-keyring.gpg
    curl -1sLf 'https://dl.cloudsmith.io/public/caddy/stable/debian.deb.txt' > /etc/apt/sources.list.d/caddy-stable.list
    apt-get update
    apt-get install -y caddy
fi

if [[ -n "$DUCKDNS_TOKEN" ]]; then
echo "==> Keeping ${DOMAIN} pointed at this server"
mkdir -p /opt/duckdns
cat > /opt/duckdns/update.sh <<EOF
#!/usr/bin/env bash
curl -s "https://www.duckdns.org/update?domains=${SUBDOMAIN}&token=${DUCKDNS_TOKEN}&ip=" -o /opt/duckdns/last.log
EOF
chmod 700 /opt/duckdns/update.sh
/opt/duckdns/update.sh
if [[ "$(cat /opt/duckdns/last.log)" != "OK" ]]; then
    echo "DuckDNS refused the update. Check the subdomain and token on duckdns.org." >&2
    exit 1
fi
echo "*/5 * * * * root /opt/duckdns/update.sh" > /etc/cron.d/duckdns
fi

echo "==> Opening the firewall"
ufw allow OpenSSH
ufw allow 80/tcp     # Caddy: certificate checks
ufw allow 443/tcp    # Caddy: the voice chat page (HTTPS)
ufw allow 1212/tcp   # game server status and downloads
ufw allow 1212/udp   # game connections
# 1213 (the voice socket) stays closed: only Caddy on this machine talks to it.
ufw --force enable

echo "==> Installing the game server into ${INSTALL_DIR}"
id -u "$SERVICE_USER" >/dev/null 2>&1 || useradd --system --create-home --home-dir "$INSTALL_DIR" --shell /usr/sbin/nologin "$SERVICE_USER"
systemctl stop cmu-server 2>/dev/null || true
mkdir -p "$INSTALL_DIR/bin"
rm -rf "$INSTALL_DIR/bin/"*
unzip -q "$PACKAGE" -d "$INSTALL_DIR/bin"

if [[ ! -f "$INSTALL_DIR/server_config.toml" ]]; then
    cat > "$INSTALL_DIR/server_config.toml" <<EOF
[game]
hostname = "${SERVER_NAME}"

[status]
bind = "*:1212"

[cmu.voice]
enabled = true
ws_bind = "127.0.0.1:1213"
public_url = "https://${DOMAIN}/voice/"
EOF
else
    echo "    Keeping the existing server_config.toml."
fi
chown -R "$SERVICE_USER:$SERVICE_USER" "$INSTALL_DIR"

cat > /etc/systemd/system/cmu-server.service <<EOF
[Unit]
Description=CMU game server
After=network-online.target
Wants=network-online.target

[Service]
User=${SERVICE_USER}
WorkingDirectory=${INSTALL_DIR}/bin
Environment=DOTNET_ROOT=${DOTNET_DIR}
ExecStart=${DOTNET_DIR}/dotnet ${INSTALL_DIR}/bin/Robust.Server.dll --config-file ${INSTALL_DIR}/server_config.toml --data-dir ${INSTALL_DIR}/data
Restart=on-failure
RestartSec=10

[Install]
WantedBy=multi-user.target
EOF

echo "==> Configuring Caddy for https://${DOMAIN}/voice/"
cat > /etc/caddy/Caddyfile <<EOF
${DOMAIN} {
	# Live voice audio (WebSocket) goes to the game server's voice socket.
	handle /voice/ws* {
		reverse_proxy 127.0.0.1:1213
	}
	# The voice page itself is served by the game server's status port.
	handle /voice* {
		reverse_proxy 127.0.0.1:1212
	}
	# Nothing else on the game server is exposed through here.
	handle {
		respond 404
	}
}
EOF

systemctl daemon-reload
systemctl enable --now cmu-server
systemctl reload caddy || systemctl restart caddy

echo
echo "Done."
echo "  Game server:      connect to ${DOMAIN}:1212 (or this server's IP)"
echo "  Voice chat page:  https://${DOMAIN}/voice/"
echo "  Server logs:      journalctl -u cmu-server -f"
echo "  Caddy logs:       journalctl -u caddy -f"
echo "  Server settings:  ${INSTALL_DIR}/server_config.toml (then: systemctl restart cmu-server)"
