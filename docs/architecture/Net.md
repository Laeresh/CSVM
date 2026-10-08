# Net

The network seam: what carries bytes between peers, the in-process carrier the suites run on, the
ENet carrier a match ships over, the WebRTC carrier internet play rides, and the one place a build
picks between them. Only the carriers and the port mapping name an engine type beyond Godot's plain math structs, and nothing
here names a socket API, which is what lets one session run over the loopback in a plain unit test
and over ENet in a match; the boundary and its exemptions are asserted over compiled metadata by
`CSVM.Tests/NetNamespaceDependencyTests.cs`. Nothing about the world crosses this seam, and the
message vocabulary sits entirely above it.

One `## src/...` entry per module, body at most 8 lines.

Traps do not live here; the rule is in `docs/architecture.md`. What the original sends, with its
type ids, payloads and guarantees, is in [../org/multiplayer-messages.md](../org/multiplayer-messages.md).

## The rules a network session keeps

These are the design rules every module below is shaped by, and every multiplayer change keeps.

- **Owner-authoritative aircraft, host-authoritative match and world.** Each machine simulates and
  sends its own aeroplanes. The host owns everything shared: the match clock, the scores, spawns and
  the rotation, the AI, zeppelins, turrets, surface vehicles, destructibles and the campaign's
  mission director. It is the original's shape (a dying pilot's own client reports the death,
  [../org/multiplayer-scoring.md](../org/multiplayer-scoring.md)) and needs no cross-machine
  determinism, which Godot's physics and collision queries do not give; there is no lockstep and
  no server-side prediction.
- **Nothing about the world crosses the wire.** Every peer builds the same world from its own
  extraction and the handshake's master seed. Only pilot states, fire, hits and deaths, the
  host-owned world's spawns, states and damage, director transitions, the match clock and the
  roster are sent; a mesh, a node or an animation never is.
- **Modes.** Dogfight and campaign co-op. Co-op is the host's campaign with guests flying as its
  human field, the local splitscreen campaign's shape: a guest has no profile or progression of its
  own, its save is never touched, and it flies stock planes, since its own machine simulates the
  airframe it flies. A Dogfight pilot may fly a custom plane when the host's Allow Custom Planes and
  outlaw list admit it, and a co-op host's own custom planes fly; every machine builds a custom plane
  from its owner's `NetPlaneBuild`. The lobby forms free-form named teams (`NetTeams.cs`) and each
  seat carries its team into a team Deathmatch, Capture the Flag or Zeppelin vs Zeppelin.
- **Listen server.** One player hosts; there is no dedicated headless host.
- **The player ceiling is 16.** `NetSeats.MaxPlayers`, with every seat-indexed table built
  `SeatCapacity` wide. The original has no coded cap (its pilot list is never counted against a
  maximum, [../org/multiplayer-spawn.md](../org/multiplayer-spawn.md)) and its lobby reads
  `Players (1 of 16)`. The authored colour table and the 45-degree respawn fan serve eight, so seats
  8 to 15 take the remake's own derived colours and the fan wraps. Co-op caps at four humans (`n/4`),
  the campaign's P1 to P4 field, counting every player at a guest's machine; a player past the cap
  sits out with a line saying so, and never takes a seat already flying.
- **The carrier is a flag.** ENet ships, by LAN search or direct IP with an IPv4 UPnP mapping or an
  IPv6 pinhole. With a master server set (`--master-server=`, `server/README.md`), a host also lists
  its game there and takes WebRTC guests, who join by code with STUN and a TURN fallback. A Steam
  carrier (Networking Sockets, relay, lobbies) would be one more `INetTransport` chosen in
  `NetCarrier.cs`, with no session edit; listing the game on Steam is a distribution and legal
  decision, not the code's.
- **Two seams above the transport.** On the session side `INetTransport`; on the aircraft side a
  `FlightController` fed a `RemotePoseBuffer` in place of an `IFlightInputSource`. A remote human is
  a pose that arrives late, never a stick that arrives late. Godot's `MultiplayerApi`,
  `MultiplayerSynchronizer` and `[Rpc]` are not used: they replicate node properties with no
  interpolation and would put a Godot type in every session.
- **Hit authority.** The shooter's machine decides a hit, the victim's owner applies the damage and
  reports its own death, and the host scores. There is no lag compensation; the original has none.
  A hit is its own reliable message, where the original batches hits onto its unreliable aircraft
  state, because a lost hit is a lost kill.
- **Star topology.** A guest connects to the host alone, and the host relays every guest's states
  and events to the other guests (`NetSession`), so one port and one router mapping serve a match
  and a guest-to-guest packet costs one extra hop. Guests never connect to each other. The host
  relays and applies a guest's report only for a seat that guest flies, and takes none of the
  decisions it alone sends (a spawn, a score, a death notice) from a guest at all.
- **The doors are the Original presentation's.** The campaign cabin's Host Co-op button opens the
  network and reads Close Network while open; the NETWORK OPEN band shows the address and guest
  count, and a chip per guest its Ready mark. Close Network, or leaving the cabin for the main menu,
  returns every guest to the Connection screen with "Host closed the game". The game is advertised
  under the name Game Information gives it, with an optional password the host checks before a
  guest is admitted, and the host can boot a guest from the lobby or the cabin. A guest joins from
  the Multiplayer Connection screen, by LAN search into the games list or by Internet IP address,
  and with a master server set by its games in the same list or a typed join code.
  The Connection screen's Host opens the Multiplayer Lobby, where Dogfight is the one live mode;
  co-op has no lobby. The Built-in menu, off by default, keeps its own network boards.
- **A pause halts nothing.** In a network session the pause sheet is an overlay (`PauseState`):
  the world, the AI, the director, the net ticks and the pauser's own aeroplane run on, the
  aeroplane flying trimmed on a centred stick with its commands swallowed until the sheet closes.
  Offline play, splitscreen included, still freezes the clock. Only the host's sheet offers
  Restart: a co-op restart relaunches the mission on every machine through the door's next round,
  and a Dogfight restart reruns the match in place for everyone.
- **Everyone starts together.** No machine's simulation runs until every machine flying a seat has
  built its world: the mission clock, the AI and the world events wait with the aeroplanes, behind
  the load screen (`NetStartGate`). A drop or a timeout releases the wait.

## src/Net/INetTransport.cs
The seam itself, and the types it is spoken in. `NetReliability` is the three classes a payload
can be sent under, `INetTransportListener` what a transport tells its owner (a peer joined, a peer
left, a payload landed), and `INetTransport` the carrier: the peer roster, `Send` of a byte span
with its class and channel, `Bind` of the one listener, `Disconnect`, and `Step`, the only place a
payload is delivered. `INetClassedListener` hears each arrival's class too, which ENet and the
loopback name and `ShapedTransport` needs; `INetPeerAddress` is the address a boot bans by,
`INetListing` a host's master-server listing and join code. A session holds the interface and
constructs neither implementation. Read `LoopbackTransport.cs` for the carrier the suites use.

## src/Net/LoopbackConditions.cs
One direction's wire conditions as a value, for the loopback and `ShapedTransport` alike: a
latency, a symmetric jitter half-width about it, and a loss probability, the times in seconds and
all validated at construction. `Delay` and `Drops` are the two draws, taken from a caller-supplied
`Random` rather than an ambient one, so a seeded suite replays the same network exactly and a
reliable stream costs no loss draw. Which classes loss may touch is the transport's rule, not
this value's. The soak's three cells are `SoakCells.cs`.

## src/Net/SoakCells.cs
The soak's three shaped link cells as values: 50 ms with 10 ms of jitter and 5 per cent loss,
100/20/10 and 200/40/20, the same both ways. `NetSoakSuites`, the surface-vehicle and zeppelin
suites fly their matrices through them, keeping their own bars, and `--net-shape` takes them by
the names in `Named` through `ShapedTransport.TryParse`, so a real link flown under a cell meets
the bar its loopback twin set.

## src/Net/LoopbackTransport.cs
Transports wired to each other in one process through delivery queues, one `LoopbackConditions`
per direction, changeable through `SetConditions` so a suite makes a reorder instead of waiting
for the jitter. `Mesh` builds and links the set; nothing arrives until `Step` advances that end's
own clock, which gives a test delivery time. The guarantees are enforced: loss is drawn only for
the unreliable classes, a reliable stream's deadlines stay monotonic per sender, and a sequenced
payload at or below the newest on its channel is discarded. `Lost` (per channel in `LostOn`) and
`DiscardedStale` are the truth `NetInstruments`' gap count is checked against, less the events
channel, which no sequence stream rides. Read `LoopbackTransportTests.cs`.

## src/Net/EnetTransport.cs
The shipped carrier over Godot's ENet peer, and the one type under `CSVM/` allowed to name a Godot
networking type. `Host` opens a listen server on every address `ListenAddresses` names (for `*`:
IPv4's wildcard, the stable global IPv6 address and `::1`) as one roster, so a reply leaves from the
address a guest dialled; a guest drops a reply from a temporary one. `Join` reports the host (peer 1)
joining. Every roster change and payload comes out of `Step`. A service thread polls ENet when the
main thread has not stepped for `ServiceGapSeconds`, up to `Keepalive.CeilingSeconds`, so a
blocking mission load keeps acknowledging. `INetLink` is where a board reads the socket, and a
socket with no listener holds what lands and replays it on `Bind`. Read `NetLink.cs` next.

## src/Net/NetLink.cs
`NetLinkState`, where one end's link stands (connecting, up, down) in words a board can show
without learning which carrier holds it, and `INetLink`, the optional face a real socket (ENet,
WebRTC, a merged host) shows a board: its state, the payloads held while no listener is bound, and
a fault as a player reads it. `HeldPayloads` is the one depth every carrier holds to before
`Bind`. A carrier without it, the loopback, is read through its peer roster instead.

## src/Net/NetCarrier.cs
Which carrier a match runs over, chosen once: the menu door's registration in `Launcher.cs` and
the command line's own open both come through `Host` and `Join`, so a build changes carrier with
no edit above the seam. `Name` is the log word. `PortMap`/`PortUnmap` are
the router door a direct-IP host asks for, `Pinhole` (given the address) and `PinholeClose` its
IPv6 pinhole, and `StableIpv6`/`LanIpv4` the addresses it names (`Utils/HostAddress.cs`). All are
null for a carrier reachable without them. `HostListed` adds a listed WebRTC host beside ENet as a
`MergedTransport` when a master server is set and the extension loaded; `JoinCode` joins by code.

## src/Net/WebRtcTransport.cs
The carrier for internet play through the master server, over Godot's `WebRtcMultiplayerPeer` and
the webrtc-native extension (`Available` is the test for it; `InstallWebRtc.ps1` fetches it). `Host`
lists through `MasterRegistration` and offers each guest the server announces; `Join` asks for a
code, answers the host's offer and closes its socket once linked. The server relays SDP and ICE
candidates only; STUN and TURN come with each announcement. Three default data channels carry the
classes, the session channel rides `WebRtcFraming`. A host reopens a dropped socket after
`ReopenSeconds`; a guest that has not linked in `JoinTimeoutSeconds` is down with a `LinkFault`.
An unlinked guest the server says left is kept `LeftGraceSeconds`. Read `Testing/WebRtcTransportSuites.cs`.

## src/Net/WebRtcFraming.cs
The four-byte header every WebRTC payload rides in: the session channel, a flags byte and, for a
sequenced payload, its 16-bit sequence on that channel to that peer. A WebRTC data channel has one
delivery mode, so the carrier sends both unreliable classes unordered and the receiver discards a
sequenced payload not newer than the newest from that sender on that channel, wrap included.
Engine-free. Read `WebRtcFramingTests.cs`.

## src/Net/MergedTransport.cs
Several host carriers as one `INetTransport`, which is how a host with a master server takes ENet
guests (LAN, direct IP) and WebRTC guests in one session. Each carrier numbers its own guests, so
this gives every guest an id from 2 up and maps both ways; the host stays peer 1. The link answers
from the first carrier, the addresses and the listing from whichever carrier has them.

## src/Net/ShapedTransport.cs
A real carrier with `LoopbackConditions` laid over both its directions at one end, which is what
`--net-shape` puts on the command line's socket so a two-machine match flies a soak cell. A send
waits before it reaches the carrier and an arrival before the session sees it, each drawing its own
loss from the wrapper's own generator, on the wall clock. The loopback's rules hold: loss only on
the unreliable classes, reliable order per peer, an overtaken sequenced payload discarded, and an
arrival whose carrier names no class is carried as reliable. A departure waits behind the reliable
arrivals still owed, and a hang-up or close hands the waiting reliable sends on unshaped.
`TryParse` reads the flag's value and `SoakCells`' names, `Describe` writes the launch log line.

## src/Net/MasterProtocol.cs
The master server's wire, one file compiled by the game and by `server/MasterServer` alike, so it
names no other engine file. `MasterWire` holds the paths, the type words, the limits both ends keep
(16 KiB a message, the 15 s heartbeat, the 45 s expiry), the join code's form (`TryCode`) and the
JSON; `MasterGame` is one listing (`Unlisted` keeps it off the games list, reached by its code
alone), `MasterMessage` one socket message either way, `MasterIceServer` one STUN or TURN entry.
`ProtocolVersion` is the wire's own version, sent on host, update and join; the games list answers
the oldest the server serves (`IsServedBy`). Add a word or field, never rename one. Read
`MasterWireTests.cs` and `server/MasterServer.Tests/`.

## src/Net/MasterSocket.cs
`IMasterSocket`, the master server's socket as the WebRTC carrier speaks it: whole messages queued
out and polled in from the frame, a state and a fault. Nothing here names a socket API; the shipped
one is `Launch/MasterServerLink.cs`'s and the suites pass `Testing/LoopbackMaster.cs`'s.

## src/Net/MasterRegistration.cs
A host's listing on the master server, engine-free: the first listing goes as `host` once the socket
opens, a changed one goes at once as `update`, an unchanged one every `MasterWire.HeartbeatSeconds`.
`Take` reads the code the server gave, or its refusal into `Fault`. Read `MasterDirectoryTests.cs`.

## src/Net/MasterDirectory.cs
The games list's master-server half, engine-free: `Ask` fetches the list through a delegate at most
every `RefreshSeconds`, `Poll` takes a finished answer, and every listed game becomes a `LanGame`
row carrying its code in place of an address, unless the list says the server no longer serves
this build's protocol (`Outdated`), when it lists none. `ListingOf` turns a host's advert into the listing
it carries, unlisted for a Private host. The door, `UI/Menu/NetPlayFeature.cs`, holds one when a
master server is set.
## src/Net/NetEndpoint.cs
A host and the port a join opens on, as one value. `Parse` splits an address as a player types it
or `--net-join` names it: a port follows a closing bracket or a lone colon, so a bare IPv6 address
is all host, and a missing or out-of-range port takes the fallback. `ToString` writes it back with
an IPv6 host bracketed. The menu door's `JoinTarget` and `SessionSpec.ParseJoin` both parse through
it; the carriers' `Join` still takes the host and the port apart.

## src/Net/NetPorts.cs
The ports this process opens by default. `Game` is where a menu door's port row starts and what a
bare `--net-host` or `--net-join` takes; `Lan` is where a LAN responder listens and a search asks.
Both stand at the shipped pair (`ShippedGame`, one above it for LAN) until the launcher calls `Use`
with `--net-port-base`, which `RunTests.ps1` passes each engine shard. `Block` is the span a base
reserves, which `Testing/SuitePorts.cs` divides among the suites that open sockets. The shipped
constants stay where a port every build fills in is named: `NetPlayFeature.DefaultPort` for a bare
address and `LanDiscovery.Port`.

## src/Net/UpnpPortMap.cs
A best-effort port mapping through Godot's UPnP client, so a host behind a router is reachable
from outside it. `Map` returns one of five outcomes a board can show (mapped, no gateway, refused,
timed out, no public address) with the external address and lease, `Unmap` takes it back down, and
neither throws. No gateway means no device answered. Godot calls a gateway invalid when its
connection check fails without saying why, so for such a device this file asks the description's
connection services for the external address itself, over Godot's `HttpClient`, whose `Get` and
`Soap` exchanges `UpnpPinholeMap.cs` shares. Both calls block for the gateway search, so they
belong on `RouterAccess.cs`'s own thread. The rules are `UpnpLease.cs`'s; this is the only file naming `Upnp`.

## src/Net/IgdAddress.cs
A gateway's external address read without the engine. `Kind` sorts an IPv4 address into public,
private (10/8, 172.16/12, 192.168/16), shared (100.64/10, a provider's carrier-grade NAT) and
reserved; the documentation ranges read as public. `Connections`, `ExternalAddressRequest` and
`ExternalAddressOf` are the device description and SOAP text of a GetExternalIPAddress question,
which `UpnpPortMap.cs` sends; `IgdPinhole.cs` reads its descriptions through the same parse. A
malformed answer parses to nothing. Read `IgdAddressTests.cs`.

## src/Net/UpnpLease.cs
The router mapping's rules, engine-free behind `IUpnpGateway`. A gateway that answered, usable or
not, is asked its external address first; a private, shared or reserved one returns
`NoPublicAddress` with no delete and no add, since no mapping behind a carrier's NAT is reachable.
A mapping asks a finite lease of `LeaseSeconds` (TUNE), and one that draws error 725
gets a permanent one. A fresh add first deletes the stale mapping on the port and on the port the
last run remembered, by exact port only; a renewal only adds again. `NextRenewal` says when
`RouterAccess.cs` asks next: half the lease after a grant, an eighth after a failed renewal, never for a permanent
lease; the IPv6 pinhole renews on the same schedule. Read `UpnpLeaseTests.cs`.

## src/Net/UpnpPortMemory.cs
The one port this machine last mapped, kept in `upnp_port.txt` under the user directory so a run
after a crash can delete the mapping it left. Apart from `options.json` because the mapping thread
writes it. A missing or unreadable file recalls no port, and a failed write costs only the stale
clear, so nothing here throws.

## src/Net/UpnpPinholeMap.cs
A best-effort IPv6 pinhole in the host's router, for a line whose IPv4 has no public address.
Godot's UPnP client maps IPv4 only, so `Open` finds the IGD v2 `WANIPv6FirewallControl:1` service
by an SSDP search (through `LanDiscoverySocket.cs`) and speaks SOAP to the control URL its
description names, over `UpnpPortMap.cs`'s HTTP exchange. `Open` renews the pinhole this process
holds, `Close` deletes it by UniqueID; neither throws and both block, so they run on
`RouterAccess.cs`'s thread. The FRITZ!Box 7590 names the service in both `igddesc.xml` and `igd2desc.xml`, at
`/igd2upnp/control/WANIPv6Firewall1`. The rules are `UpnpPinhole.cs`'s; the memory is
`UpnpPinholeMemory.cs`.

## src/Net/UpnpPinhole.cs
The IPv6 pinhole's rules, engine-free behind `IPinholeGateway`. Each missing piece stops before
any add with its own outcome: no global address (`NoAddress`, no gateway call), no service,
a firewall that is off, a `GetFirewallStatus` that allows no inbound pinhole (`Disallowed`). A
pinhole takes `UpnpLease.LeaseSeconds`, is renewed by UniqueID (`UpdatePinhole`) and re-added when
the router forgot it; `RouterAccess.cs` renews on `UpnpLease.NextRenewal`'s schedule. A fresh add first
deletes the pinhole an earlier run remembered, only while its lease runs, since the router frees
the number after. Read `UpnpPinholeTests.cs`.

## src/Net/IgdPinhole.cs
The IPv6 firewall service's text without the engine: the SSDP M-SEARCH and the LOCATION of an
answer for that search target, the service's control URL in a device description, the
GetFirewallStatus, AddPinhole (wildcard remote host and port, protocol 17), UpdatePinhole and
DeletePinhole envelopes, and their answers and UPnP faults (606 and 703 read as a refusal).
`IsGlobalUnicast` is the plain-text 2000::/3 check a pinhole address must pass. A malformed answer
parses to nothing. Read `UpnpPinholeTests.cs`.

## src/Net/UpnpPinholeMemory.cs
The IPv6 pinhole this machine last opened, kept in `upnp_pinhole.txt` under the user directory as
its UniqueID, port, address and lease end, so a run after a crash can delete it while its lease
still runs. A missing or unreadable file recalls none, and a failed write costs only that clear.

## src/Net/RouterAccess.cs
A host's hold on its router, engine-free: the UPnP IPv4 mapping and the IGD v2 IPv6 pinhole, asked
through the calls it is built with (`NetCarrier.cs`'s, or a suite's stubs). `Open` starts each lease
on a dedicated thread, never the pool, since each call blocks for a gateway search, and each thread
renews on `UpnpLease.NextRenewal`'s schedule. `Poll` takes a landed answer onto `PortMap` and
`Pinhole`. `Close` stops the renewals and waits for them before it gives back whatever was granted,
so a renewal in flight cannot put a mapping back. The door, `UI/Menu/NetPlayFeature.cs`, opens it as
a host opens and closes it on the way out. Read `RouterAccessTests.cs`.

## src/Net/NetLobby.cs
A carrier's first listener and itself the `INetTransport` the session later binds. A host's
`Advertise` reaches every peer on connect and on change; a guest keeps `Advert` and `Closed`. Lobby
messages stay here (a host's `Picks`, each seat's by `PickAt` with `SeatsWanted`, `PickBuilds` and team actions, a guest's flow, fits, builds,
rules, teams, wingman and film); others are held up to `HeldPayloads` until a session binds. ⚠ A new round seen while bound
marks `FlightOver` until the next bind, so the opener survives both unbinds a restart makes. It is
the host's admission: a clashing build, a peer `AwaitingPassword`, and one `TurnedAway` (a banned
address after `Boot`, a wrong password) stand off every peer list. Read `NetLobbyTests.cs` and
`NetLobbyAdmissionTests.cs`.

## src/Net/NetBuildVersion.cs
MAJOR.MINOR of the build's SemVer string, which two peers compare before they play: builds a patch
apart play, and `Unknown`, a string that does not parse, plays only with another unknown. The
caller hands in the string, so a unit parses without an engine. `BuildVersionMessage` (`0x56`) is
the lobby's first word on connect. ⚠ Its layout is frozen, since an older build must still read a
newer one's version to name the mismatch. Wire: [../org/multiplayer-messages.md](../org/multiplayer-messages.md).

## src/Net/LanDiscovery.cs
The LAN search's datagram pair, apart from the carrier: `LanDiscovery` writes and reads a query
and a reply of one fixed width on `Port`, `LanGame` is one answer (the reply's source address, the
game port it names, the host's advert and its build version), and `ILanSocket` is the datagram seam the responder and
the search are handed. ⚠ The query is padded to the reply's width, so a responder never sends more
than it was sent and cannot amplify a forged-source flood. Layout:
[../org/multiplayer-messages.md](../org/multiplayer-messages.md).

## src/Net/LanResponder.cs
An open door's answer to a search, over a socket bound on `NetPorts.Lan` only while the door
hosts. `Poll` answers each well-formed query with the door's advert and game port, to the address
the query came from, and reads at most `QueriesPerPoll` a frame so a flood costs bounded work.
Anything that is not a whole query of this version is read and dropped unanswered.

## src/Net/LanSearch.cs
A guest's search for open doors. `Ask` starts a round with a fresh token, sent to every address
its seam yields that round (`LanBroadcast.Targets` in a shipped door, one address in a suite).
`Poll` keeps the replies carrying that token, and `Games` lists what answered the current or the
last round in first-answer order, so a host that closed leaves on the next round. Replies to an
older round and foreign datagrams are dropped. Read `LanDiscoveryTests.cs`.

## src/Net/LanBroadcast.cs
Where a search asks each round: `Limited` (255.255.255.255) first, then the directed broadcast
(address with every host bit set) of each IPv4 network the machine sits on, once each, loopback and
single-host masks skipped. ⚠ The limited broadcast alone is not enough: Windows sends it out of one
adapter only, so a machine with several adapters can miss its host's network. The networks come
from `Utils/LocalNetworks.cs` through `NetPlayFeature.LanNetworks`.

## src/Net/LoopbackLan.cs
The in-process datagram network the suites and the screenshot aids run the LAN search on, the
discovery counterpart of `LoopbackTransport`. A send to `Broadcast` reaches every socket on its
port and any other send reaches one socket; delivery is immediate and lossless. It opens no real
socket, so no run of it raises a firewall dialog.

## src/Net/LanDiscoverySocket.cs
The shipped `ILanSocket` over Godot's UDP peer with broadcast sends allowed, polled from the menu
frame. ⚠ Besides `EnetTransport`, the only type under `CSVM/` that may name a Godot networking
type, asserted by `NetNamespaceDependencyTests.cs`. A suite binds it on the loopback address,
since a wildcard bind is what raises a firewall dialog.

## src/Net/NetMessages.cs
The vocabulary: `NetMessageType` (one word per message), the death, spawn and match-end enums
taken from the original's own values, `NetDirectorEvent` (the director message's codes and id
layouts), `NetWorldEvent` (the world event's codes), `NetPositionalStart` (the positional start's kinds), `NetSessionKind`, and the message structs, the host's spawn grant, a seat's ask, the host's death notice, the clock ping, a typed line in flight (`FlightChatMessage`) and the lobby's `SessionAdvertMessage` among them. Each is a value type implementing `INetMessage<TSelf>`,
which carries its type word and its `INetTransport.cs` reliability class as static abstracts, so
a sender reads the class off the type without constructing anything. `NetMessage` holds what they
share: the four-byte header, the no-seat and no-spawn-entry markers, the aircraft-state width
budget, `ReliabilityOf`, `IsOriginalId`, and `TryReadHeader`, the one call a receiver makes
before it knows which deserialiser to run. Read `NetMessageWriter.cs` next.

## src/Net/NetWorldMessages.cs
The host-owned world's messages, beside the vocabulary rather than in it: `AiStateMessage`
(an AI's pose by admission ordinal, plain unreliable because every AI shares one channel, with
`AsAircraftState` for the pose buffer), `AiFireMessage`, `AiHitMessage` and `DestructibleHitMessage`
(a guest's claim on an AI or a pool, to the host alone), `ZeppelinStateMessage` (one zeppelin of the
original's `0x1e`, by placement index), `AiSpawnMessage` (a host generator launch at its ordinal),
`SurfaceVehicleStateMessage` (a hull's patrol), `CutsceneSkipMessage` (one episode's skip, by name
key and ordinal) and `WorldEventMessage`, whose `NetWorldEvent` code says what its fields carry.
Ids and phase mapping: [../org/multiplayer-messages.md](../org/multiplayer-messages.md).

## src/Net/NetCoopMessages.cs
The campaign co-op boards' six messages, all reliable and all kept in `NetLobby`, not a session.
`CoopFlowMessage` is the host's boards as one guest follows them: the screen, the mission, the
round of picks (`Epoch`), the guest's player number and further seats (`Extra`), the Ready mask, the hangar and the debrief's
result. `CoopPickMessage` is one seat's airframe, `CoopFit`, name, Ready, Left and hangar `Plane`
under its round, with its `Local` place at the guest's machine and whether `More` follow. `CoopHangarMessage` is one host hangar plane with its fit, build, name and holding
seat. Before the opener `CoopSeatFitMessage` gives a seat's fit and `CoopWingmanMessage` the
wingman's; `CoopFilmMessage` names a film. [Layout](../org/multiplayer-messages.md).

## src/Net/NetDogfightMessages.cs
The Multiplayer Lobby's five messages, all reliable and kept in `NetLobby` rather than a session.
`DogfightOptionsMessage` is the host's Mission Options under their round, its type byte the lobby's four (Stunt Race 3), `DogfightRosterMessage`
the whole player list with each row's team, a bot row's tier and Random plane, and the reading
guest's own row marked, and `LobbyChatMessage` one typed line, which the host relays.
`LobbyTeamActionMessage` is a guest's team action to its host and `LobbyTeamsMessage` the host's
team names. A guest's plane and Ready ride `CoopPickMessage`. Capture the Flag's two, in the
session: `FlagRequestMessage`, a pilot's ask of its host, and `FlagTableMessage`, the host's flags.
Zeppelin vs Zeppelin's placed return is `NetMessages.cs`'s `SpawnAtMessage`. Layout: [../org/multiplayer-messages.md](../org/multiplayer-messages.md).

## src/Net/NetRaceMessages.cs
A network stunt race's four session messages, all reliable. `RaceCallMessage` (`NetRaceCall`) is the window's end one machine decides for the rest: the host's rerun and return to the lobby, a guest's leaving. `RaceRunMessage` is one event of a
seat's own run (`NetRaceRun`: started, a zone split, finished, abandoned) from the machine flying
it to the host, under the owner's run number and the window's number. Every message names its `Window`. `RaceStateMessage` is the
host's clock (`NetRacePhase`, how far into it, the window, the host's session clock), and
`RaceStandingMessage` one racer's line of its leaderboard with the ranking run's splits and whether its pilot left.
`Session/World/NetRaceLink.cs` sends and takes them. Layout: [../org/multiplayer-messages.md](../org/multiplayer-messages.md).

## src/Net/NetTeams.cs
The team core every Dogfight team mode shares, engine-free: `NetTeamBook`, a host's free-form named
teams, each with a number minted lowest-free from 1, a captain and its members. `Create`, `Join`,
`Leave`, `Disband` and `Keep` (members no longer seated) return the `NetTeamEvent`s a lobby posts as
notices; a captain's leave or drop disbands its team. `Check` is the launch refusal every team mode
asks (`TeamLaunchRefusal`): Restrict Number of Teams' bounds, two players and two teams, nobody
teamless, and sizes within one, splitscreen seats weighed as players. `NetTeamAction` is the
original's `0x1a` subtypes. Decode: [../org/multiplayer-messages.md](../org/multiplayer-messages.md).

## src/Net/NetPlaneMessages.cs
Custom planes on the wire. `NetPlaneBuild` is one plane's airframe, engine, armour, hardpoints, guns
and paint as the saved record holds them, and `PlaneBuildMessage` carries one: a guest's pick, or a
seat's build at launch. `NetPlaneRules` is the host's Allow Custom Planes, Outlaw Components and the
original's 34-flag outlaw list; `Refuses` and `Enforce` are the original's Ready check, so every end
judges a plane alike. `LobbyPlaneRulesMessage` sends the rules. Decode:
[../org/multiplayer-messages.md](../org/multiplayer-messages.md).

## src/Net/NetPositionalMessages.cs
`PositionalStartMessage`, the one message a position-started definition crosses as: a landing row
the host's trigger started and the seat that flew it, the ladder switch's holder, and a guest's
held auto-land button, told apart by `NetPositionalStart`. Reliable, since each is a decision sent
once. Kinds and the replay mapping: [../org/multiplayer-messages.md](../org/multiplayer-messages.md).

## src/Net/NetMessageWriter.cs
The two cursors every serialiser and deserialiser runs on, `NetMessageWriter` and
`NetMessageReader`, kept in one file because they are one pair and drift apart if they are not.
Little-endian primitives over the caller's span, plus the two quantised forms the layouts need: a
unit-range field as a 16-bit integer, and a fixed-width UTF-8 field that truncates on a whole
character. The writer opens with the header and patches the total length in on `Close`; the
reader reads the header in its constructor, so `Type`, `Length` and `Valid` answer first, and a
NaN or infinite float clears `Valid`. A text field decodes on the stack and allocates only its string.
`NetMessageFuzzTests.cs` feeds every reader random, truncated and mislabelled bytes.

## src/Net/NetClockSlew.cs
A guest's session clock against the host's, as one offset walked rather than written:
`HostTime(guest) = guest + Offset`. `Observe` sets a target the offset converges on over
`ConvergeSeconds`, at most `MaxRateOffset` of real time, never overshooting; a reading past
`SnapSeconds` is applied at once and counted in `Snaps`, the signal that the window is wrong.
`ObserveRoundTrip` keeps `RoundTrip`, half of which every one-way reading is read forward by; the
first is applied at once. The constants are accepted as measured on a real link (corrections of
tens of milliseconds, no snap, the rate bound unreached, `analysis/net-real-link/`). The pose
buffers keep their own timeline; co-op's director catch-up is `HostTime`'s one reader.

## src/Net/NetClockPing.cs
The round trip a guest's `NetClockSlew` takes the link latency from, modelled on the original's
`0x23` ping. `Follow` makes a guest ask the host's clock from its first `Step`, again every
`IntervalSteps` (the original's ten seconds) after an answer and every `RetrySteps` (the
remake's own second) without one;
`Answer` makes the host reply at once with its clock. An answer overtaken by a newer one, or
stamped later than the guest's clock reads, is dropped. `AskSoon` owes an ask at the next step, a
race's opening asking once both clocks run. `Asked` and `Answered` are the counters a suite reads,
the host's `Answered` being the arrivals its relay leaves alone.

## src/Net/NetStartGate.cs
The start barrier of a network flight, and its `0x5B` word. A host's gate takes a fresh `Round`
and names it in `Hold`; a built guest sends `Loaded` under the first round it heard. The host opens
when the last one arrives, drops, or after `TimeoutSeconds` (TUNE, under the ENet keepalive
ceiling), then broadcasts `Start` and answers a later `Loaded` with one of its own. ⚠ A `Loaded`
under another round opens nothing and is answered with `Hold`: a restart reuses the link, and the
old flight's word would start the new one early. A guest opens on `Start` under its round or on its
host's link dropping. Pure state: `GameSession` holds `GameClock.StartHeld` and steps the wire.

## src/Net/NetHandshake.cs
What a host hands a joining guest before either flies: the master seed and the host's session
clock at send. The seed reaches `GameSession`'s constructor through `LauncherContext`, where it
replaces the launch's own master before `Rng.Reset` runs, which is what makes both peers draw the
same liveries, the same spawn walk and the same dice. The clock becomes the opening offset of the
guest's `NetClockSlew`. The record is what a session hands to and takes from the wire; the bytes
that carry it are the message vocabulary's.

## src/Net/NetSeat.cs
One pilot's place in a match, shaped like the record the original allocates per player: the peer it
is addressed by, its team, its callsign, airframe, paint, voice, seat index and signed score, and
who flies it where. `Pilot` says a person or a host's bot (`NetPilot`), with a bot's `Skill` tier
(`NetBotSkill`). `FlownHere` (this machine simulates and reports it) and `HasPane` (a person sits
at it here) are two claims: a bot is flown on its host with no pane. The seat index is the whole
identity, so a remote pilot or a bot indexes spawns, scores, markers and colours as a splitscreen
pane does. Read `docs/architecture/Session.md`'s `GameSession.cs` entry for where a seat becomes an
aeroplane without a pane.

## src/Net/NetSeats.cs
The roster's rules: `MaxPlayers = 16` pilots admitted, the count the original's lobby shows and
its data holds, every seat-indexed table built `SeatCapacity = 16` wide, each seat's identity
colour, and `Validate`: seats numbered from zero with no gap, at least one with a pane here, a
person at seat 0, and every bot owned and flown where seat 0 is. `Field` builds a host's roster from its local planes and the peers on its wire; `CoopField` does so for co-op, a guest's several seats side by side, each named by its pick. `Bot` makes a host's bot seat and `AddBots` appends several after the guests, leaving out any past the field, `LocalPanes` opens a local match's roster with its panes on `OfflinePeer`, `LocalOrdinal` a seat's pane ordinal, and `LeavingWith` the seats a dropped guest takes with it, never a bot.
Seats 0 to 7 take the original's authored dwords at `00628eb4` in order, low byte red as the
original's one reader takes them (the remake's index is 0-based where the original's was 1-based
and its eighth pilot read past the table); seats 8 to 15 take the channel-wise complement of seat
minus 8, the remake's own. The decode is `docs/org/multiplayer-spawn.md`.

## src/Net/RemotePoseBuffer.cs
One remote aircraft's received history and the pose to draw it at now. A sample sits on the
sender's timeline by its sequence, never its arrival; a playout clock walks it `BufferDelaySeconds`
behind the newest arrival at the fitted sender clock rate plus a damped lead correction, and
re-anchors only past `SnapSeconds`. `Clear` (a respawn) drops the samples but keeps the timeline,
since the sender's sequence runs on. Past the newest sample it rides velocity for at most
`ExtrapolationCapSeconds`, then holds; `RemotePoseFeed` names the case. Stale sequences drop, wrap
included. `Tally` counts reads by feed, stale drops, and each sample's miss against its
predecessor's velocity (past twice its reach, a jump). `RemotePosePlaybackTests` measures playback.

## src/Net/AircraftStateCadence.cs
The send half of aircraft replication, and the only thing in it that is not the session's own
step: when an owner puts its aeroplane on the wire, counted in simulation steps, and what
sequence each sample carries, counted per seat because a receiver decides staleness by it. What a
sample holds is the session's to fill and what happens to it is `RemotePoseBuffer.cs`'s, so this
module knows neither. `SendStepInterval` is accepted as measured; at the fixed step it puts two send
intervals inside the buffer's own read-behind, which is what lets one lost sample still leave a
pair to read between. `SampleSeconds` is that interval in seconds, what a sequence gap is worth.

## src/Net/MatchStateCadence.cs
When a host repeats the match state, counted in simulation steps. It exists only for the clock:
every change that matters (the limits at the build, the rematch, the ending) is sent where it
happens, and the tick is what refreshes the remaining time and gives `NetClockSlew` the one
reading a running match repeats. `TickStepInterval` is 60, a second at the fixed step, which is
the rate the versus HUD's whole-second readout can show a difference at: a guest's readout moves
one second per tick, and a slower tick would make it skip. The first step ticks, so a guest holds the host's limits inside one step of its build. What the
message carries is `GameSession`'s to fill. Read `docs/architecture/Session.md`'s entry for it.

## src/Net/NetChannels.cs
Which channel a message rides. Sequenced discard is per sender and channel, and a relayed sample
carries the host's peer id rather than its sender's, so two guests sharing one channel would
discard each other by sequence number: `ForSeat` gives every seat its own, and `Events` carries
the join and everything reliable, where nothing is discarded. `ForFire` gives each seat's fire a
channel above the whole state range, since a burst judged against the pose samples around it
would be discarded as overtaken. `Count` is the layout's width. A seat past the roster's ceiling
falls back to `Events`, which costs ordering rather than delivery.

## src/Net/NetSession.cs
The one object a session owns to talk to its peers: it holds the transport, sends a typed message
under the class the type declares, and routes an arrival to the handler on its type word. The only meaning it knows is the join, a host answering each
peer with the handshake (which names its first seat and the run after it, `LocalSeatCount`) and then the roster; `On` refuses those two types,
and a guest refuses a join `NetSeats.Validate` would throw on. The star's relay: `SendToSeat`
addresses a seat through whoever owns it, and a host's `RelayToOthers`, `RelayToSeatOwner` and
`RelayToPeers` (each machine a predicate admits, once) forward an arrival's own bytes, never back to
its sender; `FliesOnTeam` answers a team line's addressing by the persons a machine seats, never its bots. A host's `RequireSeatOwner` drops, before relay and handler, a type whose seat the sender does not fly. A suite reads the counters (`Sent`,
`Received`, `Relayed`, `DroppedUnknown`, `Malformed`, `Forged`) and `Instruments`, fed before any handler.

## src/Net/NetInstruments.cs
One machine's desync counters over its own traffic, engine-free, read by `net-soak` and the
`--debug-net` readout. Rules: a seat's first state or fire sample sets its ladder, and each later
gap counts as dropped; an arrival at or below the newest is stale, except a burst filling a fire
gap inside the last 64, which rides unsequenced and is counted reordered; a hit or a burst for a
seat reported dead and not placed again is late; a score line adding deaths nobody reported, or a
respawn for a known seat nobody reported dead, is out of order, and a seat's first score line or a
falling one (a rematch) sets the baseline. Position error needs the owner's path (the soak's, or two
machines' `--debug-net-trace` logs). `Describe` writes the readout line, plus a guest's clock.
