# Live server setup (game server + voice chat)

Puts a CMU server online on a Linux VPS, with voice chat served over HTTPS from your own domain
(or a free DuckDNS name).
You do steps 1–2 in your browser; the scripts do the rest.

## 1. Rent a VPS

Any provider that gives you a plain **Ubuntu 24.04** server with a public IP and root SSH access works
(Hetzner, OVH, DigitalOcean, Vultr, ...). For a CMU server pick at least **4 vCPU and 8 GB RAM**.
Note the server's **IP address** and **root password or SSH key**.

## 2. Point a domain at the VPS

**Your own domain:** in your DNS provider, add an **A record** such as `voice.example.com` pointing at the VPS IP.
On Cloudflare, set it to **DNS only** (grey cloud).

**No domain:** sign in at <https://www.duckdns.org>, create a subdomain (e.g. `cmu-voice.duckdns.org`) with your
VPS IP, and copy your **token**. Keep it private: it controls your DuckDNS names.

## 3. Build the server package (on this PC)

```
powershell -ExecutionPolicy Bypass -File Content.CMU\Tools\LiveServer\build-server.ps1
```

This produces `release\SS14.Server_linux-x64.zip`.

## 4. Upload and run the setup (on the VPS)

```
scp release\SS14.Server_linux-x64.zip Content.CMU\Tools\LiveServer\setup-server.sh root@YOUR_VPS_IP:~/
ssh root@YOUR_VPS_IP
sudo bash setup-server.sh voice.example.com "CMU Main"
```

For DuckDNS, add the token: `sudo bash setup-server.sh cmu-voice.duckdns.org "CMU Main" YOUR_DUCKDNS_TOKEN`.

The script installs the .NET runtime and Caddy, keeps a DuckDNS name pointed at the VPS if you gave a token,
opens the firewall (SSH, 80, 443, 1212), installs the game server as the `cmu-server` service and
configures Caddy to serve `https://voice.example.com/voice/`.

## 5. Check it

1. Open `https://voice.example.com/voice/`: padlock, and the page asks for a code.
2. Connect the game to `voice.example.com:1212`.
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
| Voice page certificate error | The domain doesn't point at the VPS yet (or is proxied by Cloudflare), or port 80/443 is blocked by the provider's own firewall panel. |
| Page loads, never connects | The game server isn't running (`journalctl -u cmu-server`), or `ws_bind` doesn't match the Caddyfile. |
| "That code didn't work" | Codes reset when you leave the server: press Voice Chat in game again. |
| Mic blocked | The page must be opened via the `https://` domain, not the IP. |
