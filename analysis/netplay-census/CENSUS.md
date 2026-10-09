# NetPlayFeature: field and member census

`CSVM/src/UI/Menu/NetPlayFeature.cs` is the multiplayer door. The earlier split moved router
access, the co-op host's flow and the guest's pick into their own types and left the door at 90
public members and 25 fields. This census groups what the door held before the second split by
the reason each part changes, and names the module each group became, or why it stays.

## How it is counted

- **Public members**: declarations in the class that start with `public` at member indent:
  constants, the constructor, properties and methods. This is the count the first split used
  (102 to 90).
- **Fields**: declared `private` fields with a leading underscore. Auto-properties that hold state
  (`{ get; private set; }`, `{ get; set; }`, `{ get; init; }`) are counted apart, since they are
  state the class owns without a named field.

| | public members | fields | stateful auto-properties | lines |
|---|---|---|---|---|
| before | 126 | 31 | 24 | 1812 |
| after | 93 | 23 | 7 | 1388 |

The 93 include the four new composition properties (`Identity`, `Internet`, `Reach`, `Lan`).

## The groups

Each group lists its fields, its public members and its private helpers, then the one reason they
change together.

### 1. The boxes' answers: moved to `NetIdentity`

- Fields: `_private`; state in `PlayerName`, `Voice`, `GameName`, `MaxPlayers`, `Password`.
- Public: `PlayerName`, `Voice`, `GameName`, `MaxPlayers`, `Password`, `Private`, `Take`,
  `ForgetAnswers` (8).
- Reason to change: what the Game and Player Information boxes ask, and how long an answer lasts.
  Callsigns, voices, the password and the Public or Private listing each arrived here for that
  reason alone.
- Seam: the door names its hosted kind through a delegate, which `Private` defaults from. The door
  reads the callsign, voice, password and game name when it builds a lobby, an advert or a pick.

### 2. The master server link: moved to `InternetDoor`

- Fields: `_listing`, `_joinCode`.
- Public: `Master`, `OpenCode`, `WebRtcReady`, `CodeFault`, `JoinCode`, `ListingFault`,
  `InternetFault`, `AwaitingCode`, `MasterOutdated`, `CodeJoinTimeoutSeconds` (10).
- Private: `JoinsByCode`.
- Reason to change: the master server protocol and the WebRTC carrier: the games list it serves,
  the join by code, and a host's listing with its code and faults.
- Seam: the door calls `Host` with its carrier as a host opens, `Join` for a join by code, `List`
  with each advert, and `Shut` on close and failure. `GuestCode` names a join by code. It reads
  whether the door is hosting through a delegate over `IsHost`.

### 3. The host's address: moved to `HostReach`

- State in `HostIpv6`, `HostLanIpv4`, `Copies`, `Copied`.
- Public: `StableIpv6`, `LanIpv4`, `CopyText`, `HostIpv6`, `HostLanIpv4`, `NamesHostAddress`,
  `Copies`, `Copied`, `GuestAddress`, `Dial`, `CopyGuestAddress`, `CopyForGuests` (12).
- Private: `ForgetHostAddress`, `Copy`.
- Reason to change: how a host names itself to guests outside its network, which address wins
  (IPv6, the router's mapping, the LAN), and the clipboard copy.
- Seam: built by the door over its `RouterAccess`, its `InternetDoor` (whose join code the copy
  prefers), its port and a delegate over `IsHost`; told `Open` as a host opens and `Forget` on
  close and failure.

### 4. LAN discovery: moved to `LanDoor`

- Fields: `_lan`, `_responder`, `_search`; state in `SearchFault`.
- Public: `SearchAddress`, `LanNetworks` (now `Networks`), `BroadcastAddress`, `SearchRounds`,
  `SearchFault`, `Answering` (6).
- Private: `SearchTargets`, `OpenResponder`.
- Reason to change: the LAN broadcast protocol, its targets and its socket: asking for open doors
  and answering other machines' searches.
- Seam: the door hands it the bind address, the version and each advert.

### 5. Admission: moved to `NetAdmission`

- Fields: `_admitted`, `_granted`, `_refused`.
- Public: `RefuseGraceSeconds` (1). `Boot` stays on the door as the boards' verb.
- Private: `RefuseOverCap`, `Admit`, `Grant`, `GrantedTo`, `RefuseClashing`, `RefuseTurnedAway`,
  `HangUpRefused`, `Refused`, `Contains`, and `Boot`'s body.
- Reason to change: who a host seats, the cap, the version and password refusals, the boot ban and
  the grace before a hang-up, and a co-op host's further seats per machine.
- Seam: private to the door, handed the wire, the host's own seat count and the cap on each call.
  The door reads `Admitted`, `GrantedTo` and `Refused` for its peer count, advert and co-op seats.

### 6. The wire's lifecycle: stays

- Fields: `_openHost`, `_openJoin`, `_transport`, `_link`, `_hostPeer`, `_released`, `_joining`,
  `_seen`; state in `Stage`, `Revision`, `Fault`, `Port`, `Address`.
- Public: the constants `DefaultPort`, `DefaultAddress`, `AddressLimit`, `JoinTimeoutSeconds`; the
  constructor, `Version`, `BindAddress`, `Port`, `Address`, `JoinTarget`, `JoinName`, `LinkedTo`,
  `Router`, `Stage`, `Revision`, `Fault`, `Peers`, `Link`, `HostStarted`, `IsHost`, `CanLaunch`,
  `Released`, `Advert`, `Advertising`, `AwaitingAdmission`; `StepPort`, `TypeAddress`,
  `PasteAddress`, `EraseAddress`, `OpenHost`, `OpenJoin`, `JoinByCode`, `JoinGame`, `PlaysWith`,
  `Step`, `BuildLaunch`, `Reclaim`, `Close`, `Discard`.
- Reason to change: how a socket opens, joins, steps, launches and closes. This is the door.

### 7. The close linger: stays

- Fields: `_closing`, `_lingered`. Public: `LingerSeconds`.
- Reason to change: the carriers' close, which discards what is queued. Two fields and three
  private methods that only `Close`, `Fail` and the opens call; no board reads them.

### 8. The session advert: stays

- Fields: `_kind`, `_missionSeq`, `_hostName`, `_localPlayers`.
- Public: `CoopHumans`, `HostKind`, `SessionCap`, `Offer`, `OpenCoopHost`, `OpenDogfightHost`.
- Reason to change: what a host advertises. The advert is built from the wire's peers, admission's
  further seats, the lobby's environment, the co-op flow's board and the identity's name and cap,
  so the door, which holds all of them, is where it composes.

### 9. The games list: stays

- Public: `CanSearch`, `Searching`, `Games`, `Search`, `StopSearch`.
- Reason to change: how the LAN's and the master server's lists merge for a board. Each member is
  one line over `Lan` and `Internet.Master`.

### 10. The co-op host's round: stays

- Fields: `_epoch`. Public: `HostFlow`, `CoopEpoch`, `CoopAllReady`, `CoopGuests`,
  `CoopGuestPlanes`, `ShowCoop`, `OfferCoopHangar`, `TellSeatFits`, `TellSeatBuilds`,
  `TellCoopWingman`, `ShowCoopFilm`, `EndCoopFilm`, `PickedVoice`.
- Reason to change: the round of picks and what goes out before a co-op launch. The first split
  kept the round in the door and moved the flow's state to `CoopHostFlow`; every remaining member
  reads the wire's picks and admission's seats in the same step.

### 11. The co-op guest's view: stays

- Fields: `_picks`, `_localSeats`, `_flownFlow`, `_flightEpoch`.
- Public: `Pick`, `PickOf`, `LocalSeats`, `CoopSeats`, `CoopSeatsShort`, `CoopHangar`, `CoopFlow`,
  `CoopFlows`, `CoopSeatFits`, `SeatBuilds`, `CoopWingman`, `CoopFilm`, `CoopReady`, `CoopReadyAt`,
  `CoopSeatsReady`, `CoopLaunchDue`, `CoopFlightOver`, `LeaveCoopMission`, `IsCoopHost`,
  `IsCoopGuest`, `CoopSeatName`.
- Reason to change: what a co-op guest hears from its host and sends back. Most members are one
  line reading the wire, gated on `IsCoopGuest`, which is the door's stage and the host's advert.
  A module would take the wire, the stage and the released flag on every read and keep only the
  four fields and `FollowHost` as its own; that is the next candidate if this group grows.

### 12. The Dogfight lobby's standing: stays

- Fields: `_dogfight`, `_flownEpoch`, `_launchEpoch`.
- Public: `Dogfight`, `DogfightLaunchDue`, `IsDogfightGuest`.
- Reason to change: when a Dogfight guest's launch is due and when a match's tail is dropped. The
  lobby itself is `DogfightLobby`; these are the door's three facts about it.

## The new modules and their reasons to change

| module | owns | changes when |
|---|---|---|
| `NetIdentity` | the boxes' answers | the Game or Player Information boxes ask something new, or an answer's lifetime changes |
| `InternetDoor` | the master server link | the master server protocol or the WebRTC carrier changes |
| `HostReach` | the address a guest types and the copy | the way a host names its address, or the clipboard way, changes |
| `LanDoor` | the LAN search and responder | the LAN discovery protocol or its targets change |
| `NetAdmission` | who a host seats | a refusal, the cap, the boot or a co-op seat grant changes |

Each has its own state behind a narrow interface: the door-facing calls are `internal`, and a
board reads the public readouts through the door's property for it.

## Behaviour

Unchanged. The moved code keeps its order inside `Close`, `Fail`, the opens and the step. The
readouts that were gated on `IsHost` (`JoinCode`, `ListingFault`, `InternetFault`, `AwaitingCode`,
`GuestAddress`, the copy) still read it, through the delegate each module is built with. The
seams that were `init` on the door (`Master`, `OpenCode`, `WebRtcReady`, `StableIpv6`, `LanIpv4`,
`CopyText`, `LanNetworks`) are settable on their modules, since C# refuses an `init` setter in a
nested object initializer; each says it is set once, before the door opens.
