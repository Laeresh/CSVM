# CSVM master server

The master server lets two players meet over the internet without touching a router. It does three
things, and nothing else:

- **The games list.** A host that has a master server set lists its game (name, mode, players, cap,
  whether it asks a password, its build version), unless the host chose Private. The list shows up
  in the game's games list beside the LAN search's answers. A game that stops repeating itself for
  45 seconds, or whose host disconnects, leaves the server.
- **Join by code.** Every hosted game, Public or Private, gets a six-character code such as
  `K7Q-X3M`. A guest picks a Public game from the list, or types the code into the Connection
  page's Join by code box, which is the only way in to a Private game from the internet.
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
curl https://csvm.example.org/api/health     # {"ok":true,"games":0,"protocol":1,"oldest":1,"seen":{}}
curl https://csvm.example.org/api/games      # {"games":[],"protocol":1,"oldest":1}
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
From the VPS itself, `docker compose exec -T coturn turnutils_uclient -c -u "$USER" -w "$PASS" -y
<PUBLIC_IP>` runs coturn's own client against it as well, and should end with `Total lost packets 0`.
Keep the `-c`: without it the client opens an RTCP relay beside each one, five relays for one
credential, and coturn's quota of four answers `486 Allocation Quota Reached`.

**Signalling.** The game itself is the check: see the next section.

## 7. Point the game at it

The game uses the project's own server, `https://csvm.gunmuessig.de` (`MasterAddress.Default` in
`CSVM/src/Utils/MasterAddress.cs`), unless told otherwise. A server of your own replaces it in
two ways:

- For one launch: `.\RunGame.ps1 --master-server=https://csvm.example.org` (or pass the same flag
  to the exported `CSVM.exe`).
- For good: add `"netMasterServer": "https://csvm.example.org"` to `options.json`, which on Windows
  is `%APPDATA%\Godot\app_userdata\CSVM\options.json` (on Linux
  `~/.local/share/godot/app_userdata/CSVM/options.json`). The flag beats the file;
  `--master-server=` with nothing after it turns the master server off for that launch, and LAN
  search and direct IP work as before.

A release build carries the WebRTC library, so a player only unpacks it and starts the game. A
checkout does not, since the library is not in the repository: run `.\InstallWebRtc.ps1` once in
the checkout the game runs from. Without it the games list still shows
the master server's games, but hosting for internet guests and joining by code are refused with a
message saying so: a host reads `No internet code: WebRTC is missing or failed to start` where its
code would stand. The game's log (`.scratch/logs/`, or `logs/` beside an exported build) says at
startup whether the master server is set and the library loaded.

Then: the host opens Multiplayer, Host, and answers GAME INFORMATION. Its Listing chooser picks
**Public** (the default for a Dogfight: the game is in the games list) or **Private** (the default
for campaign co-op, which the cabin's HOST CO-OP asks through the same box: the game stays off the
list, and internet guests need its code). A Private game still answers LAN searches, and a guest
who types the host's address still joins; only the internet list leaves it out. The password is a
separate, optional extra on either.

Where the host finds its code: a Dogfight host's lobby pins `Internet code ABC-DEF, public, on the
games list. Ctrl+C copies it.` (or `private, not on the games list`) as the top line of its chat,
and a co-op host's NETWORK OPEN band reads `CODE ABC-DEF  Ctrl+C` with PUBLIC or PRIVATE under it.
Ctrl+C on any menu copies the code to the clipboard to paste into a message; without a code it
copies the host's address instead. While the server has not answered the host reads `Asking the
master server for a join code ...`, and when it refused or cannot be reached the line says why
and the band shows the address guests can type instead.

A guest opens Multiplayer, LAN TCP/IP, Connect: a Public game appears in the list beside any LAN
games. Pick it and Join Game, or, for either kind, pick **Join by code**, type or paste (Ctrl+V) the
code into its Join code box, with or without its dash and in either case, and Connect. With no
master server set, or without the WebRTC library, that way stands greyed and says which. The
Internet IP address box is for addresses, though it still joins a code typed with its dash.

A server older than this listing mark ignores it and lists a Private game anyway, so update the
server before relying on Private. An older game build sends no mark, and its games stay listed.

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
| `Master__MaxGames` | `500` | games hosted at once, Private ones included |
| `Master__MaxGamesPerAddress` | `4` | games one address may host at once |
| `Master__MaxSocketsPerAddress` | `16` | open sockets per address |
| `Master__MaxPendingGuests` | `16` | guests negotiating with one game at once |
| `Master__MaxPendingGuestsPerAddress` | `2` | guests from one address negotiating with one game at once; a guest stops counting once its link stands, so players behind one NAT joining one after another are not held by it |
| `Master__TurnMintsPerHour` | `10` | TURN credentials one address may cause to be minted per hour; a join takes two (the guest's, and the host's for that guest, both charged to the guest), so the default is five joins an hour. Not counted while no TURN entry is handed out |
| `Master__MessageBurst` | `60` | messages a socket may send in a burst |
| `Master__MessagesPerSecond` | `5` | messages per second after the burst |
| `Master__ListPerMinute` | `60` | games list and health requests per address per minute |
| `Master__SocketsPerMinute` | `30` | sockets opened per address per minute |
| `Master__GuestSocketSeconds` | `120` | the longest a guest's negotiating socket may stay open |
| `Master__OldestProtocol` | `1` | the oldest wire protocol served; a host or join below it is told to update CSVM. Raise it only once a game release speaking the newer protocol is out; `seen` in `/api/health` counts the versions still in use since the server started |

A message over 16 KiB closes its socket; so does a socket silent for 90 seconds. A join naming a
code no game is hosted under is answered `no game is listed under that code` and its socket
closes, so guessing at a Private game's code costs a socket per guess, which `SocketsPerMinute`
bounds.

Each TURN credential lets its holder take up to coturn's `user-quota` (4) relay ports until it
expires, and the relay range holds 41, so `TurnMintsPerHour` slows one address down rather than
keeping it below the whole range: at the defaults one address can still pin most of the ports
within its first hour. `TurnCredentialMinutes` is not the lever either, since the game is handed
its credential once per join and coturn refuses a refresh past its expiry, so a shorter one ends
long relayed matches.

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
   `static-auth-secret=...`, `realm=csvm.example.org`, `external-ip=203.0.113.10`,
   `listening-ip=203.0.113.10`, `listening-ip=127.0.0.1` and `relay-ip=203.0.113.10` (unbound,
   coturn also relays on every other address the host has, such as Docker's bridges, while
   `external-ip` names the public one in every allocation, so those relays never connect); set
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
WebRTC link between two game peers was negotiated and carried traffic inside one process. The
Docker path (steps 1 to 6) has run on a Debian 13 VPS: the certificate, the games list over HTTPS,
and TURN over UDP and TCP through the public address all answered. The STUN check from a home
network, the path without Docker and the systemd unit have not been run, and no game link has
crossed two real NATs or a TURN relay yet.
