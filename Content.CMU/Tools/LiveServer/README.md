# Live server setup (game server + voice chat)

Puts a CMU server online on a Linux VPS, with voice chat served over HTTPS from a free DuckDNS name.
You do steps 1–2 in your browser; the scripts do the rest.

## 1. Rent a VPS

Any provider that gives you a plain **Ubuntu 24.04** server with a public IP and root SSH access works
(Hetzner, OVH, DigitalOcean, Vultr, ...). For a CMU server pick at least **4 vCPU and 8 GB RAM**.
Note the server's **IP address** and **root password or SSH key**.

## 2. Get a DuckDNS name

1. Sign in at <https://www.duckdns.org>.
2. Create a subdomain, e.g. `cmu-voice` (gives `cmu-voice.duckdns.org`). Put your VPS IP in its box.
3. Copy your **token** from the top of the page. Keep it private: it controls your DuckDNS names.

## 3. Build the server package (on this PC)

```
powershell -ExecutionPolicy Bypass -File Content.CMU\Tools\LiveServer\build-server.ps1
```

This produces `release\SS14.Server_linux-x64.zip`.

## 4. Upload and run the setup (on the VPS)

```
scp release\SS14.Server_linux-x64.zip Content.CMU\Tools\LiveServer\setup-server.sh root@YOUR_VPS_IP:~/
ssh root@YOUR_VPS_IP
sudo bash setup-server.sh cmu-voice YOUR_DUCKDNS_TOKEN "CMU Main"
```

The script installs the .NET runtime and Caddy, keeps the DuckDNS name pointed at the VPS,
opens the firewall (SSH, 80, 443, 1212), installs the game server as the `cmu-server` service and
configures Caddy to serve `https://cmu-voice.duckdns.org/voice/`.

## 5. Check it

1. Open `https://cmu-voice.duckdns.org/voice/`: padlock, and the page asks for a code.
2. Connect the game to `cmu-voice.duckdns.org:1212`.
3. In game: **Esc → Voice Chat**. The page opens with your code and says *Connected as ...*.
4. Allow the microphone, hold **CapsLock** to talk, **J** for headset radio.

## Updating the server later

Rebuild (step 3), upload the new zip and run the same `setup-server.sh` command again.
It replaces the server files and keeps `/opt/cmu-server/server_config.toml`.

## Useful commands on the VPS

| What | Command |
|---|---|
| Server log | `journalctl -u cmu-server -f` |
| Restart server | `systemctl restart cmu-server` |
| Caddy log | `journalctl -u caddy -f` |
| Server settings | `nano /opt/cmu-server/server_config.toml` |

## Troubleshooting

| Symptom | Likely cause |
|---|---|
| Voice page certificate error | DuckDNS doesn't point at the VPS yet, or port 80/443 is blocked by the provider's own firewall panel. |
| Page loads, never connects | The game server isn't running (`journalctl -u cmu-server`), or `ws_bind` doesn't match the Caddyfile. |
| "That code didn't work" | Codes reset when you leave the server: press Voice Chat in game again. |
| Mic blocked | The page must be opened via `https://...duckdns.org`, not the IP. |
