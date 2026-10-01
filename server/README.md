# CSVM master server

The master server lets two players meet over the internet without touching a router. It does three
things, and nothing else:

- **The games list.** A host that has a master server set lists its game (name, mode, players, cap,
  whether it asks a password, its build version). The list shows up in the game's games list
  beside the LAN search's answers. A game that stops repeating itself for 45 seconds, or whose host
  disconnects, leaves the list.
- **Join by code.** Every listed game gets a six-character code such as `K7Q-X3M`. A guest picks
  the game from the list, or types the code into the Internet address box.
- **Signalling.** While a guest connects, the server relays the WebRTC offer, answer and ICE
  candidates between the guest and the host. Once the link stands, the guest's connection to the
  server closes; game traffic never passes through the master server.

Beside it runs **coturn**, which answers STUN (each player learns its public address, so the two
can punch a hole through their NATs) and, when the NATs refuse that, relays the traffic as TURN.
TURN credentials are minted per join by the master server and expire, so no password ships in
the game. **Caddy** in front gives the master server HTTPS with an automatic certificate.

No accounts, no database, nothing stored on disk. A restart forgets every listing; hosts relist on
their own within ten seconds.

## What you need

- A small Linux VPS with a public IPv4 address: 1 vCPU and 1 GB of RAM is plenty (the three
  processes use under 200 MB together). Debian 12 or Ubuntu 24.04 are what these steps assume.
- A DNS name you control, such as `csvm.example.org`.
- Traffic: a punched link costs the VPS nothing. A relayed guest costs roughly what one player
  sends and receives, on the order of tens of kB/s each way; check your provider's monthly quota if
  many players end up relayed.

## 1. DNS

Create an `A` record for your name pointing at the VPS's IPv4 address, for example
`csvm.example.org -> 203.0.113.10`. Wait until `nslookup csvm.example.org` (or `dig`) answers with
that address from your own machine; Caddy cannot get a certificate before it does. An `AAAA`
record is optional; add it only if the VPS has a public IPv6 address and its firewall opens the
same ports for IPv6.

## 2. Firewall

Open these ports, and nothing else besides SSH:

| Port | Protocol | For |
|---|---|---|
| 22 | TCP | SSH (yours) |
| 80 | TCP | Caddy, the certificate challenge and the redirect to HTTPS |
| 443 | TCP | Caddy, HTTPS for the games list and the WebSocket |
| 3478 | UDP and TCP | coturn, STUN and TURN |
| 49160 to 49200 | UDP | coturn's relay range (`min-port`/`max-port` in `coturn/turnserver.conf`) |

With `ufw`:

```sh
sudo ufw allow 22/tcp
sudo ufw allow 80/tcp
sudo ufw allow 443/tcp
sudo ufw allow 3478/udp
sudo ufw allow 3478/tcp
sudo ufw allow 49160:49200/udp
sudo ufw enable
```

If your provider also has a firewall in its web panel (Hetzner, AWS, and others do), open the same
ports there. The master server's own port, 8080, stays closed: only Caddy reaches it.

## 3. Install Docker and get the code

```sh
curl -fsSL https://get.docker.com | sudo sh
sudo usermod -aG docker "$USER"     # log out and back in after this
git clone https://github.com/Laeresh/CSVM.git
cd CSVM/server
```

The image builds the server from this checkout (`server/` and the one shared file
`CSVM/src/Net/MasterProtocol.cs`). No game data is needed or used.

## 4. Secrets and settings

```sh
cp .env.example .env
openssl rand -hex 32          # copy the output into TURN_SECRET
nano .env
```

Fill in all three values:

- `CSVM_DOMAIN`: the DNS name from step 1.
- `PUBLIC_IP`: the VPS's public IPv4 address (`curl -4 ifconfig.me` prints it).
- `TURN_SECRET`: the 64 hex characters you just generated. The master server signs TURN
  credentials with it and coturn checks them with it; nobody else ever needs it.

Then `chmod 600 .env`.

## 5. Start

```sh
docker compose up -d --build
docker compose ps             # all three should read "running" / "Up"
docker compose logs -f        # Ctrl+C to stop following
```

The first start builds the image (a minute or two) and Caddy fetches the certificate (look for
`certificate obtained successfully` in `docker compose logs caddy`). The master server logs one
line naming its STUN and TURN settings; it should say `secret set`.

To update later: `git pull`, then `docker compose up -d --build` again.

## 6. Check each piece

**The games list.** From your own machine:

```sh
curl https://csvm.example.org/api/health     # {"ok":true,"games":0}
curl https://csvm.example.org/api/games      # {"games":[]}
```

A certificate error here means DNS or ports 80/443 are not right yet (step 1 and 2).

**STUN.** Open <https://webrtc.github.io/samples/src/content/peerconnection/trickle-ice/> in a
browser. Remove the default server, add `stun:csvm.example.org:3478`, and press *Gather
candidates*. A row of type `srflx` showing your home's public address means STUN works.

**TURN.** TURN needs a credential, which you can mint yourself with the secret, exactly as the
master server does. With bash and openssl (on the VPS, or WSL):

```sh
SECRET=paste-your-TURN_SECRET
USER="$(( $(date +%s) + 3600 )):check"
PASS=$(printf '%s' "$USER" | openssl dgst -sha1 -hmac "$SECRET" -binary | base64)
echo "username: $USER"; echo "password: $PASS"
```

Or in Windows PowerShell:

```powershell
$secret = "paste-your-TURN_SECRET"
$user = "$([DateTimeOffset]::UtcNow.ToUnixTimeSeconds() + 3600):check"
$hmac = New-Object System.Security.Cryptography.HMACSHA1 (,[Text.Encoding]::UTF8.GetBytes($secret))
$pass = [Convert]::ToBase64String($hmac.ComputeHash([Text.Encoding]::UTF8.GetBytes($user)))
"username: $user"; "password: $pass"
```

On the Trickle ICE page, add `turn:csvm.example.org:3478` with that username and password, set
*ICE transports* to *relay*, and gather. A row of type `relay` showing the VPS's address means
TURN works. An `authentication` error means the secret in `.env` and the one you typed differ.
From the VPS itself, `docker compose exec coturn turnutils_uclient -u "$USER" -w "$PASS" -y
127.0.0.1` runs coturn's own client against it as well.

**Signalling.** The game itself is the check: see the next section.

## 7. Point the game at it

The setting is off by default, and with it off nothing changes: LAN search and direct IP work as
before. Two ways to turn it on:

- For one launch: `.\RunGame.ps1 --master-server=https://csvm.example.org` (or pass the same flag
  to the exported `CSVM.exe`).
- For good: add `"netMasterServer": "https://csvm.example.org"` to `options.json`, which on Windows
  is `%APPDATA%\Godot\app_userdata\CSVM\options.json` (on Linux
  `~/.local/share/godot/app_userdata/CSVM/options.json`). The flag beats the file;
  `--master-server=` with nothing after it turns it off for that launch.

Each player also needs the WebRTC library, which is not in the repository: run
`.\InstallWebRtc.ps1` once in the checkout the game runs from (an exported build carries the
library if it was installed when the build was exported). Without it the games list still shows
the master server's games, but hosting for internet guests and joining by code are refused with a
message saying so. The game's log (`.scratch/logs/`, or `logs/` beside an exported build) says at
startup whether the master server is set and the library loaded.

Then: the host opens Multiplayer, Host, and its lobby chat shows `Internet guests join with code
ABC-DEF.` once the server listed it. A guest opens Multiplayer, LAN TCP/IP, Connect: the game
appears in the list beside any LAN games. Pick it and Join Game, or type the code (with its dash)
into the Internet IP address box and Connect. A campaign co-op host shows the code in its NETWORK
OPEN band.

## Settings

Every setting is an environment variable on the `master` service (docker-compose.yml sets the
first four from `.env`). Defaults suit one small VPS.

| Variable | Default | Meaning |
|---|---|---|
| `Master__Stun` | none | STUN URLs, comma-separated |
| `Master__Turn` | none | TURN URLs, comma-separated; ignored without a secret |
| `Master__TurnSecret` | none | coturn's `static-auth-secret`; empty hands out no TURN entry |
| `Master__TrustProxy` | `false` | take the client address from `X-Forwarded-For`; only behind a proxy, with 8080 closed |
| `Master__TurnCredentialMinutes` | `720` | how long a TURN credential lasts; bounds the longest relayed match |
| `Master__MaxGames` | `500` | games listed at once |
| `Master__MaxGamesPerAddress` | `4` | games one address may host at once |
| `Master__MaxSocketsPerAddress` | `16` | open sockets per address |
| `Master__MaxPendingGuests` | `16` | guests negotiating with one game at once |
| `Master__MessageBurst` | `60` | messages a socket may send in a burst |
| `Master__MessagesPerSecond` | `5` | messages per second after the burst |
| `Master__ListPerMinute` | `60` | games list and health requests per address per minute |
| `Master__SocketsPerMinute` | `30` | sockets opened per address per minute |
| `Master__GuestSocketSeconds` | `120` | the longest a guest's negotiating socket may stay open |

A message over 16 KiB closes its socket; so does a socket silent for 90 seconds.

## Without Docker

The same three pieces, installed from the distribution:

1. `sudo apt install coturn caddy` (Caddy's own apt repository: <https://caddyserver.com/docs/install>)
   and the .NET 8 runtime (`sudo apt install aspnetcore-runtime-8.0` on Ubuntu 24.04; on Debian,
   Microsoft's package feed: <https://learn.microsoft.com/dotnet/core/install/linux-debian>).
2. Build on any machine with the .NET 8 SDK: `dotnet publish server/MasterServer/MasterServer.csproj
   -c Release -o publish`, and copy `publish/` to `/opt/csvm-master/` on the VPS.
3. `sudo useradd --system --no-create-home csvm-master`, then write `/etc/csvm-master.env` (mode
   600, owner root) with `Master__Stun=stun:csvm.example.org:3478`,
   `Master__Turn=turn:csvm.example.org:3478?transport=udp,turn:csvm.example.org:3478?transport=tcp`,
   `Master__TurnSecret=...` and `Master__TrustProxy=true`, one per line.
4. Copy `systemd/csvm-master.service` to `/etc/systemd/system/`, then
   `sudo systemctl daemon-reload && sudo systemctl enable --now csvm-master`.
5. Copy `coturn/turnserver.conf` to `/etc/turnserver.conf` and append
   `static-auth-secret=...`, `realm=csvm.example.org` and `external-ip=203.0.113.10`; set
   `TURNSERVER_ENABLED=1` in `/etc/default/coturn` if your distribution has that file, then
   `sudo systemctl restart coturn`.
6. Put this in `/etc/caddy/Caddyfile` and `sudo systemctl reload caddy`:

   ```
   csvm.example.org {
       reverse_proxy 127.0.0.1:8080
   }
   ```

Then the checks in step 6.

## What was not verified

The server, its socket and the game's client code are covered by tests that run in memory, and a
WebRTC link between two game peers was negotiated and carried traffic inside one process. These
steps were written without a VPS: Docker, Caddy and coturn were not run, so the compose file,
the Caddyfile, the coturn configuration and the systemd unit are untested as written, and no link
has crossed two real NATs or a TURN relay yet.
