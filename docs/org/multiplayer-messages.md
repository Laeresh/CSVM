# Multiplayer messages, decoded from `crimson.exe`

Every message the original puts on the wire: its type word, who builds it, who handles it, what
the payload holds, and whether DirectPlay is asked to guarantee it. What the same match counts
and how it ends is [`multiplayer-scoring.md`](multiplayer-scoring.md); where it puts a pilot is
[`multiplayer-spawn.md`](multiplayer-spawn.md).

The match half lives in `remote.cpp` (the source path string at `00628f50`) and the lobby half in
the `00413xxx` block; the DirectPlay wrapper under them is the `005b2xxx`/`005b4xxx` block.

## The framing is two words

Every packet opens with a 16-bit type at `+0` and a 16-bit total length, header included, at
`+2`. Nothing else is common: there is no sequence number, no checksum and no sender id in the
frame, because DirectPlay hands the handler the sender's player id as its own argument.

`FUN_005b4850(fromId, packet)` is the dispatcher. It walks the handler list headed at
`DAT_009c7874` and calls **every** entry whose registered type word equals the packet's first
word (`005b4851`…`005b4874`). A type with no entry is dropped without a word, and two entries for
one type both run. `FUN_005b4720(type, handler, 2)` adds one, `FUN_005b47a0(type, handler)`
removes it.

`FUN_00495310` registers the whole match set at session init and `FUN_004966c0` removes exactly
the same set at teardown, which is why a type that is live in a match is dead in the menus.

## The send call, and what "guaranteed" means here

`FUN_005b2640(data, size, guaranteed, toPlayer)` is the only send in the program. It builds the
flag word as `(guaranteed != 0)`, which is `DPSEND_GUARANTEED`, and adds `DPSEND_ASYNC` (`0x200`)
when `DAT_009c7869` is set, then calls `IDirectPlay4::SendEx` through vtable slot `+0xc4`.
`toPlayer` of 0 is `DPID_ALLPLAYERS`, so a 0 in the table below is a broadcast.

`DAT_009c7869` is read out of the session caps in `FUN_005b4930`: the flag is the caps bit
`0x10000`, and when it is clear the program puts up a "NOT Using Asynchronous Sends" message box
and sends synchronously. So asynchrony is a service-provider property, not a per-message choice;
the guarantee is the per-message choice, and it is what the remake's reliability classes model.

## The match message table

Sizes are the total the builder writes into the length word. "Guar." is the third argument at the
send call, and "To" is the fourth.

| Type | Builder | Handler | Bytes | Guar. | To | What it carries |
|---|---|---|---|---|---|---|
| `0x0d` | none | `LAB_00496ca0` | 4 | n/a | n/a | a peer left, synthesised locally (see below) |
| `0x0e` | none | `LAB_004978f0` | 4 | n/a | n/a | a peer arrived, synthesised locally |
| `0x0f` | `FUN_00496ee0` | `FUN_00497950` | `0x20` + `0xc` per hit, `0x2c` when a shot rides along | **no** | one peer | the aircraft state, below |
| `0x0f` | `FUN_004975d0` | `FUN_00497950` | `0x20` | yes | 0 | the same shape sent once at spawn, which is the one guaranteed state packet |
| `0x10` | `FUN_004988e0` | `LAB_00498950` | `0x1c` | yes | 0 | a weapon event: a word at `+4`, the two dwords at aircraft `+0x728`/`+0x72c`, and a three-float vector from `+0x294` |
| `0x11` | `FUN_0049bc00` | `LAB_0049bc80` | 4 + payload | yes | 0 | a console or log line relayed to every peer, gated on `DAT_0071d23d` |
| `0x12` | `FUN_00498a90` | `FUN_00498bf0` | `0x10` | yes | 0 | the death report, below |
| `0x13` | `FUN_00499270` | `FUN_004993f0` | `0xc` + 8 per row | yes | 0 | the whole score table: player count at `+4`, team count at `+8`, then one `(id, score)` pair per row |
| `0x14` | `FUN_00499490` | `FUN_00499530` | `0x10` | yes | 0 | the career counters: kills at `+4`, deaths at `+6`, then two dwords from `0071d2fc`/`0071d300` |
| `0x15` | `FUN_00499a50` | `LAB_00499b30` | 6 + text | see note | 0 or one peer | chat. An all-chat goes out **unguaranteed** to everybody; a team chat is sent guaranteed, once per teammate, to each peer whose `+0x18` matches the sender's team slot |
| `0x17` | `FUN_004996d0` | `FUN_00499730` | `0xc` | yes | 0 | the match end: a float clock at `+4` and the reason at `+8` |
| `0x18` | `FUN_00413170` | lobby | `0x18` + text | mode-dependent | 0, one peer, or each peer | a lobby notice with a string; the third argument selects broadcast, per-team or single |
| `0x1a` | `FUN_00413090`, `FUN_00413f70`, `FUN_004142c0`, `FUN_00414340`, `FUN_004143c0` | `FUN_00415b20` | `0xc`, or `0x38` for a create | yes | 0 | a team action; the subtype word at `+4` is 8 (disband), 8, 2 (leave), 1 (join) and 4 (create) in builder order, "Lobby teams" below |
| `0x1c` | `FUN_0049a050` | `FUN_0049a170` | `0xc` | yes | the host (`DAT_0071d228`) | an objective or flag request, which only the host answers |
| `0x1d` | `FUN_0049a240` | `FUN_0049a300` | 8 + `0xc` per row | yes | 0 | the objective table: a count at `+4`, then `(id, holder, state)` per row |
| `0x1e` | `FUN_0049adf0` | `FUN_0049b0b0` | 6 + `0x1c` per zeppelin + `0x10` per event | **no** | 0 | the zeppelin state, below |
| `0x1f` | `FUN_0049b320` | `FUN_0049b3e0` | `0xc` | yes | 0 | a turret or part event raised by the local aircraft, packed as `part & 3 \| (kind & 7) << 2` at `+4` and an owner id at `+8` |
| `0x20` | `FUN_0049b210` | `FUN_0049b2c0` | `0xc` | yes | 0 | the same shape raised for a remote owner |
| `0x21` | none found | `LAB_004990d0` | | | | receive-only in this build |
| `0x22` | `FUN_00499110` | `LAB_00499190` | `0x10` | yes | 0 | a damage attribution: victim id at `+4`, attacker id at `+8`, a dword from the weapon record `+0x10` at `+0xc` |
| `0x23` | `FUN_0049bd00` | `LAB_0049bd70` | `0xc` | yes | one peer | the ping: two `GetTickCount` stamps, sent only to a peer whose id is at or above ours, so one side of each pair pings |
| `0x24`, `0x25` | none found | `LAB_0049bde0` | | | | receive-only in this build, both on one handler |
| `0x26` | `FUN_004134e0` | lobby, `FUN_00415940` | `0x30` + the packed outlaw list | **no** | 0 | the lobby settings block, below |
| `0x27` | `FUN_004135f0` | lobby | 8 + `0x20` per team + the member ids | yes | 0 | the team roster, below |

⚠ **Types `0x02`, `0x03`, `0x05`, `0x06`, `0x07`, `0x0d` and `0x0e` never cross the wire.**
`FUN_005b2820` and `FUN_005b24a0` build them on the stack from DirectPlay's own system messages
and hand them straight to the dispatcher (the type and length pairs are written as immediates at
`005b2898`, `005b29ad`, `005b29db`, `005b2aa8`, `005b2ad0`, `005b2ad9` and `005b2618`). They are
4 or 8 bytes and they are local notifications, so a remake has no counterpart to serialise: the
transport's own connect and disconnect callbacks are the counterpart.

## The aircraft state packet

`FUN_00496ee0(force)` walks the remote list and sends one packet **per peer**, unguaranteed:

| Offset | Field |
|---|---|
| `+0x00` | type `0x0f`, then the total length |
| `+0x04`, `+0x08`, `+0x0c` | position, three dwords straight off the aircraft at `+0x204` |
| `+0x10` | orientation, three angles packed into one dword as `yaw << 0x15 \| pitch << 0xb \| roll` |
| `+0x14` | motion, packed the same way as `a << 0x14 \| b << 10 \| c` |
| `+0x18` | `GetTickCount` at build time |
| `+0x1c` | the health fraction × 255 in the low byte (vehicle `+0x2d0` over `+0x2cc`, clamped 0..255; the receiver `FUN_00498170` scales it back into `+0x2d0`), a 7-bit field at bits 8..14, and one flag at bit 15 |
| `+0x1e` | a 3-bit shot count at bits 4..6 and a 4-bit hit count at bits 0..3 |
| `+0x20` | when the shot count is above zero, three dwords of shot state |
| tail | the queued hit records, 12 bytes each |

⚠ **The original batches its hit reports onto this unguaranteed packet.** A hit is queued by
`FUN_004987d0` as a 12-byte record onto the list at remote record `+0x1088` (count at `+0x108c`),
appended here, and dropped by `FUN_00498760` the moment the packet is handed to the send. Nothing
resends it. The 4-bit count field also means the sixteenth queued hit of a tick is lost before it
is ever sent. The remake does not copy this: its hit is its own reliable message (the hit-authority
rule in [`../architecture/Net.md`](../architecture/Net.md)), which is a remake decision and not a
reading of this code.

The send rate is per peer and adaptive. `FUN_00497850(peerCount, distance)` returns the interval,
stored at remote `+0x106c`, and a peer is skipped entirely until its interval has passed unless
the caller forces it. The same walk pings a peer (type `0x23`) when its own 10-second timer at
`+0x1068` has expired.

## The death report

`FUN_00498a90(killer, param, source)` builds the 16-byte `0x12` once per death, behind the
one-shot latch `DAT_0071d1dd`, and calls the handler locally as well as sending it:

| Offset | Field |
|---|---|
| `+0x00` | type `0x12`, then length `0x10` |
| `+0x04` | the credited id, which is a remote record id and not a pilot id |
| `+0x08` | the caller's second argument, passed through untouched |
| `+0x0c` | the cause word, 1 to 4 |

The cause is chosen in the builder: a non-zero killer gives 1; no killer and no source gives 2
with the victim's own id at `+4`; a source that `FUN_0049b4a0` resolves gives 3; a source that
`FUN_00499de0` resolves gives 4. What each cause scores is in
[`multiplayer-scoring.md`](multiplayer-scoring.md).

### The team comparison is on the remote record, not the pilot record

`FUN_00498bf0`'s cause-1 arm compares `piVar5[0xf] == piVar3[0xf]`, and both pointers come from
`FUN_00499d80`, the lookup over the remote list headed at `DAT_0071c7a4`. That record is the
`0x1090`-byte object `FUN_00499c90` constructs, so the field is **remote record `+0x3c`**, and the
pilot record's `+0x08` is not read anywhere on this path.

What fills `+0x3c` is `FUN_00495310`: the pilot's own index (pilot record `+0x18`) when
`DAT_0071d89c` says the mode has no teams, and `FUN_0046f3c0(pilotRecord + 0x08)`'s `+0x18`, the
team's index, when it does. The colour table at `00628eb4` is indexed by the same slot, and
[`../formats/net-spawns.md`](../formats/net-spawns.md) reads it as the team half of the spawn slot
at `00496bba`. The team chat arm of `FUN_00499a50` compares the same field.

So the friendly-fire arm is correct as written and needs no team lookup: in an un-teamed match the
slot holds distinct per-pilot indices, two pilots never match, and the arm cannot fire.

### The kill lines

[Evidence: decoded] `FUN_00498bf0` posts the death's lines itself, so every peer posts them, the
dying pilot's own included, on every death, a crash included. The single-player kill line
(`vehicleDamage.md` "The kill message") is gated off in a network game and never adds to these.
A name is the remote record's CString at `+0x34`, which `FUN_00499c90` starts as row 6007
`MSG_UNKNOWN` ("Unknown"). Both callers overwrite it at once with the lobby player record's name
at `+0x10`, found by DirectPlay id through `FUN_0046f110`: `FUN_00495310` for this machine's own
pilot (the id is `FUN_005b4200`'s, so on the host the host's own) and `FUN_00497990` for each peer
that arrives. That name is the player's DirectPlay name: `FUN_00414640` copies it from the wrapper's
player entry `+0x44`, which `FUN_005b24a0` fills from the `pszCallsign` setting (`DAT_00642f0c`,
registered at `00401416`, "noname" at `0063b100` when unset) as both session-open paths make their
local player (`FUN_00412c30` at `00412d18`/`00412d52`, `FUN_00412e30` at `00412f22`/`00412f66`). So
the host's seat is named by its callsign exactly as every guest's is, in the kill lines and in the
lobby's player list (`FUN_00412530` reads the same `+0x10`), and the connection page refuses a blank
callsign (`FUN_00407060`, called at `0040820c`). The handler composes into two 0x31-byte stack buffers, a top line and a
second line, switched on the cause at `+0xc` through the jump table at `0x499028`:

| Cause | Top line | Second line |
|---|---|---|
| 1, a killer | the victim's name (`sprintf "%s"` at `0x498d0a`) | row 214 `MSG_DESTROYED_BY_X` "Destroyed by %1" with the killer's name, `FUN_0059cd70(buf, 0x31, 0xd6, ..)` at `0x498d20`, so cut at 48 characters |
| 2, no killer | row 7063 `MSG_MP_SUICIDE` "%1 Self-Destroyed" with the victim's name (`0x498d91`..`0x498da8`) | none |
| 3, a zeppelin part | the victim's name | row 7064 `MSG_MP_KILLED_X_ZEP` "Killed by %1 Zeppelin" with the team record's `+8` name (`FUN_0046e0f0`), at `0x498dfd` |
| 4, a turret owner | the victim's name | row 7065 `MSG_MP_KILLED_X_TURRET` "Killed by %1 Turret" with the owner's `+0x34`, at `0x498e70` |

The second line is posted first and only when non-empty (`0x498eae`..`0x498ecf`), then the top
line (`0x498ed7`..`0x498ee8`), so the victim's name reads above "Destroyed by". Both go through
`FUN_004588e0(line, 5.0, DAT_006eba60, DAT_006eba60)`: the HUD message stack, five seconds, in the
colour global the single-player kill line takes for a team above the player's. The Limited Lives
line (`0x498c52`..`0x498cba`, same colour) is posted before them and so reads below them. Row 7003
`MSG_DESTROYED_BY` ("was destroyed by") has no reference in the program: a search for the operand
`0x1b5b` finds none. After the posts the handler plays the combat voice (`FUN_004afd00`, codes
`0x14`..`0x17`), plays a remote victim's wreck, and on the host runs the score table.

The remake posts these lines from the host's decision rather than from each machine's copy of the
report, since a relayed report never returns to the machine that sent it. The host scores the
death, sends the scores, then sends `0x5C`, the death notice, and posts the lines itself; each
guest posts them from the notice, after the lives line the scores produced. A pilot is named by
its seat's callsign. A lobby host's first seat takes the host's own callsign (Player Information's,
carried on the wire as `NetLobby.LocalCallsign`), cut to the roster's width so every machine reads
the same name, and its player tag only when that callsign is empty. The advert's name is the
game's, not the host's, and never names a seat. A second local seat keeps its tag: the original's session-open paths make one local player
each, so it has no counterpart there. Cause 3 is a Zeppelin vs Zeppelin hull's broadside: the dying
client names the hull by placement index in the report's source field, and the notice carries that
hull's lobby team in its ninth byte, which names the line and sets the team's term on every guest
([`multiplayer-zvz.md`](multiplayer-zvz.md), "What the remake takes"). The host's `0x22` hit names
the hull in the byte after the part, where a round no seat fired came from a broadside.
Splitscreen Dogfight posts the same lines in every pane, named by player tag.

## The zeppelin state packet

`FUN_0049adf0` sends type `0x1e` unguaranteed on a fixed **0.5-second** cadence (`_DAT_0071d238`
is re-armed to `now + 0.5` after each send). It carries a zeppelin count in the byte at `+4`, then
`0x1c` per zeppelin (position, three fields from the object, and a packed word holding a 4-bit
event count), then `0x10` per pending event. Unlike the aircraft state it is a broadcast, and like
the aircraft state its event tail is dropped once sent.

The builder runs on the host alone (`FUN_005b4210`, my id `[0x9c7860]` against the host's
`[0x9c7864]`), once the clock `[0x9ad748]` reaches `_DAT_0071d238`; the half second is the float at
`0x006032e0`, re-armed at `0x0049b00b`. It walks the zeppelin vector `0x71df84`..`0x71df88` in
order, so a zeppelin is named by its index in that vector, and sends through
`FUN_005b2640(buf, len, 0, 0)`: unguaranteed, to everyone. One zeppelin's `0x1c` bytes:

| Offset | Field | Source |
|---|---|---|
| `+0x00` | position, three floats | the hull node's world position, `[[zep+0x1c]+0x38]+0x54` |
| `+0x0c` | speed, float | `zep+0xa4`, the throttle `FUN_004bf240` writes |
| `+0x10` | pitch, float radians | `zep+0x30` |
| `+0x14` | yaw, float radians | `zep+0x2c` |
| `+0x18` | part states, 2 bits per part | `FUN_004c0c20` over the list at `zep+0x5c` |
| `+0x1a` | event count in bits 0..3; above it per-part bits of the list at `zep+0x7c` | `[0x71c838] & 0xf`, `FUN_004c0d80` |

Each event is `0x10` bytes, a target point as three floats and a `u16` part index at `+0xc`, taken
from the global list `0x71c834` that `FUN_0049b030` fills when a zeppelin cannon shoots. After the
send, when `[0x71c190]` is 0 and a zeppelin's dead flag `zep+6` is set, the builder ends the
zeppelin match: `FUN_0046ecd0(index + 1, 3)`, `FUN_004996d0(3)` and the score table `FUN_00499270`.

The receiver, `FUN_0049b0b0`, writes zeppelin `i` of the packet into entry `i` of its own vector with
no check against its length. The position goes to a target at `zep+0xe4` and the speed to `zep+0xa4`.
The facing becomes a unit forward at `zep+0xf0`: `(-sin yaw cos pitch, sin pitch, -cos yaw cos pitch)`.
`FUN_004c0cb0` applies the part states (1 and 2 through `FUN_004455e0`, 0 and 3 through
`FUN_00445620`). Each event goes to `FUN_004c0d00(part, point)`, which fires `wep_28` (the string at
`0x62b828`) from that part toward the point through `FUN_005aef40`. The high bits of `+0x1a` are not
read.

A guest does not step its own path. `FUN_004bf9d0` asks `FUN_00470550`, which answers false on the
host and otherwise runs the chase:

- `k = FUN_0053e2e0(2 dt)`, which is `e^(-2 dt)` from a table of `exp(-i/51)` (the constants at
  `0x60912c` and `0x609128`, clamped at the 5.0 of `0x6036bc`);
- the hull position becomes `k * position + (1 - k) * target` (`FUN_00538c50`), written to `zep+0x20`;
- the forward from its own yaw and pitch is blended the same way toward `zep+0xf0` and normalised
  (`FUN_00538d20`, `FUN_00422690`); pitch is `asin(forward.y)` and yaw `atan2(-forward.x, -forward.z)`;
- the target is carried on by `speed * dt` along the blended forward, so between packets the target
  is dead-reckoned;
- `FUN_004bf930` writes the pose.

The guest's velocity query `FUN_004bf7f0` answers speed times the received forward. On a straight
leg a guest's hull trails the host's by the speed times the link's latency, plus the speed over the
chase rate, `v / 2` metres.

## The lobby settings block

[Evidence: decoded] The host builds type `0x26` in `FUN_004134e0` (gated on `FUN_005b4210`, the
block stored at `0x413533`). A receiver's `FUN_00415940` hands it to `FUN_00414040`, which copies 40
bytes back into `0x642f8c` and posts `0x3f7`. The block at `+4` is a copy of `0x642f8c`..`0x642fb3`:

| Block offset | Global | Carries |
|---|---|---|
| `+0x00` | `0x642f8c` | environment |
| `+0x04` | `0x642f90` | mission type |
| `+0x08` | `0x642f94` | victory kind (0 Time, 1 Score) |
| `+0x0c` | `0x642f98` | victory value |
| `+0x10` | `0x642f9c` | restrict-teams byte |
| `+0x14`, `+0x18` | `0x642fa0`, `0x642fa4` | team minimum and maximum |
| `+0x1c` | `0x642fa8` | Limited Lives byte |
| `+0x20` | `0x642fac` | lives count dword |
| `+0x24` | `0x642fb0` | Auto Respawn byte |
| `+0x25` | `0x642fb1` | Allow Custom Planes byte |
| `+0x26`, `+0x27` | | unmapped |

[Evidence: decoded] The outlaw list follows at message `+0x2c`, packed by `FUN_00410800` (below),
and the whole message is sent unguaranteed (`FUN_005b2640(.., 0, 0)`). `FUN_004134e0` is called
from a per-frame step at `0x415656` while the lobby object's byte `+4`, a dirty flag, is set.
[Evidence: undecoded] What sets that flag, and bytes `+0x26`..`+0x27`, are open. No message carries a pilot's remaining lives: each peer seeds them from
this block and counts them down on the `0x12` death reports
([`multiplayer-scoring.md`](multiplayer-scoring.md)). The remake's `0x53` carries the same lives
settings and nothing more.

## Custom planes

[Evidence: decoded] Allow Custom Planes is the byte at `0x642fb1`, written by the lobby's check at
`0x40dde5` and read at `0x40deeb`, `0x40f053` and `0x407f04`. At Ready (`0x407f02`) the program
copies it to `0x61f2ec` and calls `FUN_00414580(1)`.

[Evidence: decoded] **A host's lobby opens with Allow Custom Planes ticked.** The settings block
starts zeroed (it is zero in the file image, and `0x407190` clears its ten dwords with `rep stosd`),
and the host's open writes only the environment (`0x407d0f`). The block does not stay zero:
`MULTIPLAYERLOBBY_MISSION.SCRIPT`'s `gui_create` mails 2013 to itself, and on a host (`$$OX$$`) case
2013 mails 8 (live and ticked) to the Allow Custom Planes checkbox (`ZBA`, label 10112) and to Auto
Respawn, 2 (live and clear) to Outlaw Components, Restrict Number of Teams and Limited Lives, then 2010.
Case 2010 reads every widget and calls `$$E$$` 5007, which the jump table at `0x40f91c` sends to
`0x40dce3`, the handler whose `0x40dde5` writes the tick into `0x642fb1`. Only the Zone way
(`$$RY$$ == 0`) also runs case 2011, where a Zone-supplied setting (`$$A$$` 1035) may override it.
The getter 5009 (`0x40de6c`) hands the byte back to the script's refresh 1015 on every peer. The
remake ticks it on a host's first lobby screen; a Built-in host, which shows none, keeps it clear.

The outlaw list is one object at `0x64e168`, built by `FUN_00410560`, of 34 byte flags:

| Flags | Outlaws | Check |
|---|---|---|
| 0..10 | one airframe each, in airframe order | `FUN_00410cc0` |
| 11..15 | one gun calibre each; calibre 5, no gun, is always allowed | `FUN_00410c10` |
| 16..19 | one ammunition each; 4, none, is always allowed | `FUN_00410c40` |
| 20..30 | one rocket-table row each; row 11, none, is always allowed | `FUN_00410c80` |
| 31 | every rocket | `FUN_00410c80` |
| 32 | every ammunition | `FUN_00410c40` |
| 33 | nitro-boosted engines: engines 3 to 5 refused, engine 6 (none) always refused | `FUN_00410cf0` |

`FUN_00410800` packs the list into five bytes, lowest flag in bit 0 of the first byte, and
`FUN_00410930` unpacks it. `FUN_00410d30` checks a plane record's engine (`+0x30`), airframe
(`+0x2c`) and four guns (`+0x88`) against it.

The Ready check runs on each client, for its own pick only, in the Ready callback at `0x40f026`. A
custom plane (`0x645c10 == 1`) is refused while Allow Custom Planes is clear, and so is a record the
check fails; `FUN_0040fa30` then clears the pick. A mounted gun's outlawed ammunition refuses Ready
and `FUN_0040fa60(4)` sets all four guns' ammunition to none. A loaded pylon's outlawed rocket
refuses Ready and `FUN_0040fa90(11)` sets all eight pylons to none and both hardpoint counts to 0.
Outlaw All Ammo and Outlaw All Rockets make the same resets without refusing. A refusal shows langui
10517 followed by 10514 (plane), 10515 (ammunition) and 10516 (rockets) for each reason, and a
second Ready takes the reset fit. The host does not validate a remote pick.

A pilot's plane travels as DirectPlay player data: `FUN_00414470` writes 80 bytes through
`FUN_005b31f0` (SetPlayerData, guaranteed). It carries the airframe, the paint pattern, three decals,
three colours and each gun slot's weapon id. It does not carry the engine, the armour or the
hardpoints, so a remote copy of a custom plane flies on the stock template's. `FUN_004136e0` builds
the remote planes from it, and in a team game the team colours replace the paint.

The stock templates at `0x619f58` (stride `0xcc`) all carry engine 1. Their guns, at `+0x88`..`+0x94`,
are what the check reads for a stock pick:

| Airframe | 0 | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9 | 10 |
|---|---|---|---|---|---|---|---|---|---|---|---|
| Guns | 0,5,5,5 | 2,1,5,2 | 2,2,0,0 | 1,0,5,5 | 3,0,5,0 | 2,1,0,5 | 4,0,5,0 | 4,0,5,5 | 3,2,5,1 | 2,1,5,5 | 4,2,5,5 |

### The outlaw list screen

[Evidence: decoded] The lobby's Select... (`BCA` in `MULTIPLAYERLOBBY_MISSION.SCRIPT`,
`mp_b_medium.png` at page +295,+277) reads langui 10103 "Select..." on the host and 10508 "View..."
on a guest. It is greyed (`mail(1)`) while Outlaw Components (`ACA`) is clear and live (`mail(2)`)
while it is ticked, on both ends and whether or not the host is Ready: the options refresh 1015 sets
it on the tick alone, and the Ready message 2012 leaves it alone. Every toggle of `ACA` calls uiData
5044, whose case at `0x40eca8` (entry 7 of the table at `0x40f98c`) loads `0x64e168`, calls
`FUN_004107a0`, which zeroes all 34 flags, and returns 0. It reads no argument and tests nothing, so
the list empties whether the tick was set or cleared. A press on Select...
runs `MULTIPLAYER_OUTLAW.SCRIPT`, greys the four lobby tabs and pauses the mission page; on the host
it first calls 5050 (`0x40ef3f`), where `FUN_004106c0` copies the list at `0x64e168` to `0x64df60`.

The pane is `mp_lobby_outlawed.png` at (314, 26), the tab page's place, titled 10136 "Outlawed
Components" at +15,+45. Five sub-tabs stand at y +63, four frames each, their labels black and the
picked one (142,0,0): Airframes (10130, `mp_lobby_tabmedium.png`, +46), then Engines, Guns, Ammo and
Rockets (10131..10134, `mp_lobby_tabsmall.png`, +127, +200, +268, +336). Airframes is picked on
open. Every box is `mp_b_readycheckbox8states.png` (eight frames of 48x26) with its label at +60,+4:

| Sub-tab | Boxes | Names | Read / write (uiData) |
|---|---|---|---|
| Airframes | 11 rows, 4 shown from +85,+120 at a 36 px pitch | langui 3000+i (`FUN_00410b60`, 5020) | 5063 `FUN_00410cc0` / 5025 byte +i |
| Engines | one at +100,+150 | 10135 "Outlaw Nitro-Boosted Engines" | 5033 third out (`0x64e189`) / 5043 byte +0x21 |
| Guns | 5 rows from +85,+110 at a 32 px pitch | langui 3320+i (`FUN_00410ab0`, 5017) | 5065 `FUN_00410c10` / 5022 byte +0xb+i |
| Ammo | Outlaw All Ammo, then 4 rows from +85,+120 at 36 px | 10137, then langui 3350+i (`FUN_00410ae0`, 5018) | 5033 first out (`0x64e188`) / 5028 byte +0x20; rows 5018 / 5023 byte +0x10+i |
| Rockets | Outlaw All Rockets, then 11 rows, 4 shown | 10138, then langui 3380+i (`FUN_00410b30`, 5019) | 5033 second out (`0x64e187`) / 5027 byte +0x1f; rows 5064 `FUN_00410c80` / 5024 byte +0x14+i |

The two Outlaw All boxes stand at +95,+86 drawn at half size (`scale(TD) = 50,50,100`), their labels
at +40,-2. While one is set its page's rows read ticked (5018 and `FUN_00410c80` answer outlawed)
and a click on a row is ignored. The long pages scroll under a control at +393,+104, 167 high
(`mp_b_scrollup.png`, `mp_b_scrolldown.png`, `mp_b_scrollbar.png`, colour `0xff282418`). The uiData
switch is at `0x40e3e5` (jump table `0x40f93c`, 5016..5035) and `0x40e7f7` (table `0x40f98c`,
5037..5075).

A box is live (`mail(8)`/`mail(2)`) only on the host while it is not Ready, and otherwise shows its
state disabled (`mail(5)`/`mail(1)`). Accept (10540, `mp_b_medium.png` at +200,+280) is removed
(`deactivate`) on a guest and greyed while the host is Ready; its press calls `$$A$$` 1028 on the
host and closes. Cancel (10541 at +300,+280) calls 5051 (`0x40ef5d`, the copy back from `0x64df60`)
on a host that is not Ready, then closes. So a guest's View... opens the list read-only. Closing
(91111) re-enables the lobby tabs, Game Scores only once a match has landed.

[Evidence: undecoded] What `$$A$$` 1028 does. It is the likely trigger of the `0x26` send above.

The remake builds this pane over the tab page (`OriginalOutlawList`). Each tick calls
`DogfightLobby.SetOutlawed`, which starts a new round, clears every Ready and sends `0x5E` to every
guest at once, so Accept only closes; Cancel restores the opening list the same way. A toggle of
Outlaw Components either way empties the list as 5044 does: `DogfightLobby.SetOutlawComponents`
writes the new tick and the empty list as one rules change, so one round starts, every Ready clears
and one `0x5E` reaches each guest. The model still takes a flag while the tick is clear, which
reaches every guest; the screen's Select... greys then, as the original's does.

## The team roster

[Evidence: decoded] `FUN_004135f0` sends type `0x27`, the widest message in the protocol. The count
at `+4` is the team container's `+0x28`, the team count. Then comes `0x20` per team: the team number
(`team+0x18`), the team's `+0x28`, the team name (`team+8`, 18 characters copied with `strncpy` and
zero-terminated at `+0x1a`) and the member count (`team+0x34`). A tail of member player ids (the
list at `team+0x30`) follows the teams, so the size is `(members + 8 * teams) * 4 + 8`. It is sent
guaranteed to everybody, whole, every time anything changes; there is no delta form. Guests learn
team membership from this message alone.

`0x27` is the highest type word the program uses, which is why the remake mints its own ids above
it. The remake's `0x27` is its seat roster, a different message under the original's id; its team
list is minted at `0x62` ("Lobby teams").

## What the remake takes

`Net/NetMessages.cs` keeps the two-word header and the original's id for every event that has a
counterpart here: `0x0f` aircraft state, `0x10` fire, `0x12` death with its four causes, `0x13`
score, `0x17` match state, `0x22` hit and `0x27` seat roster. Damage, spawn, the mission
director transition, the join handshake and a seat's ask to be spawned again have no
counterpart, so they are minted at `0x40`, `0x41`, `0x42`, `0x43` and `0x44`, above the ceiling
above. The host-owned world's four (AI state, AI fire, a guest's hit claim on an AI, and a world
event) are minted at `0x45` to `0x48`, below, the clock ping at `0x49`, the lobby's session advert at `0x4A`, the zeppelin path at `0x4B`, a generator's AI launch at `0x4C`, the surface-vehicle patrol at `0x4D`, a positional start at `0x4E`, the lobby's session closed at `0x4F`, and the lobby's co-op flow, co-op pick and co-op seat fit at `0x50` to `0x52`, the Dogfight lobby's options, roster and chat at `0x53` to `0x55`, the lobby's build version at `0x56`, a guest's destructible hit at `0x57`, a cutscene skip at `0x58`, the lobby's co-op wingman at `0x59`, the lobby's co-op film at `0x5A`, the start barrier's word at `0x5B`, a match's death notice at `0x5C`, the lobby's plane build and plane rules at `0x5D` and `0x5E`, the lobby's co-op hangar plane at `0x5F`, the lobby's join password at `0x60`, and the lobby's team action and team list at `0x61` and `0x62`, Capture the Flag's ask and table at `0x63` and `0x64`, Zeppelin
vs Zeppelin's placed return at `0x65`, the in-flight chat at `0x66`, the original's `0x15` with
the typist's seat added ("In-flight chat" below), and a stunt race's run report, race clock, racer
line and race call at `0x67` to `0x6A` ("Stunt race" below). The handshake carries the master seed, the host's clock and the seat the joining peer was
given, in 24 bytes: the seed as two 32-bit words at 4, the clock's double as two at 12, the seat at
20 and, at 21, how many seats after it the same machine flies (0 for one pilot, which is the byte a
one-seat join always carried), then two reserved bytes. The original needs none of the three, because it draws from no shared stream and hands
out no seat. The ask carries a seat and nothing else: the original's client takes its own
respawn, while here the host owns every placement and answers the ask with a spawn event.

`0x17` is the one original id the remake widens. The original's twelve bytes carry a clock and a
reason, which is all a client that runs its own countdown needs. The remake's twenty carry the
remaining time, both limits, the reason and the host's session clock, because a guest here runs
no countdown of its own: it is told the clock, and that field is also the reading its
`NetClockSlew` takes an offset from, since the periodic tick is the only message a running match
repeats. Both limits ride even though the original arms exactly one, which costs four bytes a
second and leaves an exclusive lobby nothing to change on the wire. The byte after the reason names
the winning lobby team under reason 3, a Zeppelin vs Zeppelin hull lost, and is 0 otherwise
([`multiplayer-zvz.md`](multiplayer-zvz.md)).

The clock ping is minted at `0x49` rather than taking `0x23`, though it has the original's shape:
twelve bytes, two stamps, one peer, a ten-second timer (`Net/NetClockPing.cs`). The stamps
differ in kind. The original's are `GetTickCount` milliseconds and it pings to learn a peer's
latency, one side of each pair asking; the remake's are session-clock seconds, and only a guest
asks, because the host's clock is the one being read. It is unreliable where the original's is
guaranteed, since a retransmitted question would measure the retransmission as link. The guest
reads the host's clock forward by half the round trip, and the first answer replaces the offset
the handshake opened on. It is also the periodic clock reading a campaign session has, where no
match state ticks.

Fire (`0x10`) is unreliable and unsequenced where the original's is guaranteed, and rides a
channel per seat apart from that seat's state (`Net/NetChannels.cs`). Sequenced beside state, a
burst sent between two samples was discarded as overtaken whenever jitter swapped them, and
rounds fired on one step would discard each other the same way. Unsequenced, a burst is spawned
whichever order it lands in; a reliable burst would stall behind a retransmission.

The remake does not take the batching: its hit and damage messages are reliable and separate, its
score message is one seat rather than the whole table, and its roster carries the match seed,
which the original has no need of because it never draws from a shared stream. The packed angle
and motion dwords are not taken either; the remake spends 8 bytes on a quantised quaternion and
12 on a float velocity, which is the trade `Net/NetMessages.cs`'s width budget exists to hold.

The damage report `0x40` carries the owner's whole damage ledger, sent in the step after any of
its pools moved, whatever moved it: a shot, a ram, a graze, a rearm or an airframe swap. Each pool
rides as a 16-bit fraction of its maximum: the whole armour and health pair, then each zone's pair
in def order, up to the four zones a player airframe has. Every other machine mirrors those into
its copy's ledger and plays the zones' and the hull's damage stages off it, so a remote aeroplane's
panels tear and burn where its owner's do. A reader whose copy counts different zones (the two
machines briefly flying different airframes around a swap) takes the whole pair alone. A copy out
of play mirrors the numbers and shows nothing, since its wreck is already playing. A copy that was
hurt and reads full again was restored, a rearm on the owner's machine
([`multiplayer-rearm.md`](multiplayer-rearm.md)), and takes its stages off; a respawn's full
ledger, which the copy's own respawn already matched, changes nothing. The original's `FUN_0049b970`
sends nothing for a rearm.

## The mission director

`0x42` carries one event of the host's objectives graph as a code, an id and the host's clock,
reliable, in the order the graph raised it. The host is the only sender; a guest's graph is
replicated and changes only by replaying these (`Session/Objectives/NetDirectorLink.cs`). The message is 16
bytes: the header, a `u16` code, two bytes of padding, an `i32` id, and an `f32` `HostClock`, the
host's session time when its graph raised the event. The codes are `NetDirectorEvent`:

| Code | Event | Id |
|---|---|---|
| 1 to 7 | Woke, Napped, Completed, Killed, Slept, Expired, Hidden | objective number in the low 16 bits, the objective whose completion caused it in the high 16 (0 for none) |
| 8 | Settled: a completion and every chain it ran are done | objective number |
| 9 | The mission countdown expired | 0 |
| 10 | Ending decided | outcome, plus `0x100` when the objectives-won or objectives-lost sound played |
| 11 | Mission ended, after the host's wrap-up | outcome |

A code the guest does not know is dropped. `Hidden` exists because `HIDE_OBJ` retires an objective
without the completion bookkeeping, and a guest that did not hear it would complete that objective
later by a rule of its own. Nap lengths are not sent: the guest reads them off the same script,
from the source objective's `NAP_OBJECTIVE_WHEN_I_COMPLETE` or the objective's own nap.

What a guest replays, and what it derives from what it replayed:

- **Replayed, presentation.** Each transition runs the same bookkeeping on the guest as on the
  host: `WAKE_ANIM` and `SLEEP_ANIM`, the wake, completed, class-complete and ending sound groups,
  `STOP_QUEUED_SOUNDS`, the objective and other target lists, the help labels, the countdown's
  reset, adjust and end actions, and the display rows. The countdown's display runs locally
  between events, pinned at zero, and only the host's code 9 expires it.
- **Replayed for now, world.** `WAKEUP_ENEMIES`, `WAKEUP_TURRETS`, `WAKEUP_ZEP_TURRETS`,
  `WAKEUP_GENERATOR`, `SET_AI_TEAM`, `SET_AI_NET`, `SET_AI_ATTACK_RADIUS`,
  `COMPLETED_ZEPCANNONS`, `COMPLETED_STOPPOINT` and `START_TAXI` run through the guest's own world
  seam. A guest's AI aircraft is a replicated airframe (below), so a warp, a net or a team set on
  it is overwritten by the host's next sample. `WAKEUP_ENEMIES` does not wake it: the host's wake
  arrives as a presence event (below), which is what takes it out of `Inert` so the samples show.
  `WAKEUP_GENERATOR` credits the guest's own cycles, whose aircraft launches are refused (below).
  Turrets still act on these locally. A guest's zeppelin or surface vehicle takes the net but walks
  nothing, since its path is the host's samples.
- **Drawn by the host, world.** `WARP_VEHICLE` picks one of its waypoints on the world stream, and
  its only authored use hides an aircraft that stays `Inert`, which the host sends no samples for.
  The host sends the pick as world event 4, and a guest's directive draws nothing: it places the
  aircraft on the host's pick, when the directive runs or when the pick arrives, whichever is later.
  `DEDG`'s engagement widening is a side effect of testing a condition, so a guest never runs it.
- **Derived, cutscene codes.** The presentation codes (20, 2, 11, 1, 10, 913 and 914, 666 and 667,
  951, 86) are raised on the guest by its own animation runtime, playing the definitions its
  replayed `WAKE_ANIM` or the shared start list started. Sending them as well would apply each one
  twice.
- **Refused on a guest.** Code 13, the docking's mission completion, is refused by a replicated
  graph; the host's own code 13 decides the ending and codes 10 and 11 carry it. The condition
  hooks (a player lost, a danger zone completed) and a direct wake are refused the same way.
- **Not the director's.** 801 to 803 (a Black Hat launch), 968 (a wingman taken out) and 800 (a
  generator's credit) change AI world state and still run locally on each end. A Black Hat launch
  reactivates a block built dormant with its ordinal, so on a guest it waits for the host's presence
  event like `WAKEUP_ENEMIES`; 800's credit launches nothing there of its own. 965 to 967 (the
  airframe swap) belong to the episode's owner and run on every end (below). Definitions started
  by a player's position are not graph events: the landing rows and the ladder switch cross as
  `0x4E` (below), as does the one `PlayerRange` gate whose definitions raise a code; the other
  `PlayerRange` conditions run on each end over the whole field.
  The escorting wingman is a roster block spawned at build, not a director event.

A guest applies each event on arrival and then catches up on it (`Session/Objectives/NetDirectorCatchUp.cs`).
The lateness is the guest's shared clock minus the stamp, never negative. What the event started
is advanced by that much:
- the objective's private timer, its nap and a countdown it set;
- the cutscene instances it started and their motions, stepped at the authored frame so their
  timed events and codes fire in order (a code the host raised during that time is raised on
  arrival);
- a one-shot, started that far into its clip, or skipped when the clip is already over;
- a radio call, whose start delay is shortened by the lateness and which, past it, joins at the
  line and offset the host's is at.
Particle emitters, light animations and a music cue start at their own beginning on arrival. None
of them has a position to seek, and none is timed against the rest.
The shared clock is `Net/NetClockSlew.cs`'s. Its one-way readings are read forward by half the
round trip `0x49` measures (below), so a lateness read against it is the whole time the event
spent on the link, not only the excess over the average.
A guest's mission end holds the world and builds the result without writing a profile, a
photograph or an award. A guest that joins late has missed every earlier event.

## The host-owned world

The host flies every AI aircraft and decides every world hit; a guest replicates the state and
replays nothing that draws from the AI stream (`Session/World/NetWorldLink.cs`). The same seed is not the
same AI: the mode machine rolls on the AI stream every step, so two ends running one AI would part
on the first roll that landed differently. An AI is named on the wire by its admission ordinal, its
index in the roster's append-only AI list, which both ends grow in the same order for the aircraft
built with the world. The one source that grows it later is a generator's aircraft launch, and a
guest builds those only from the host's `0x4C`, so the two lists cannot part on a launch timed
differently or credited on one end alone. Black Hat launches and `WAKEUP_ENEMIES` add nothing to
the list: they reactivate blocks built dormant, whose ordinals already stand.

| Id | Message | Class | Carries |
|---|---|---|---|
| `0x45` | AI state | unreliable, on the event channel | ordinal, per-AI sequence, pose, velocity, lever, surfaces, nitro (48 bytes) |
| `0x46` | AI fire | unreliable | ordinal, weapon index, muzzle, aim (28 bytes) |
| `0x47` | AI hit | reliable, guest to host | ordinal, shooter seat, weapon, damage share, part, impact in the AI's body space (28 bytes) |
| `0x48` | World event | reliable, host to all | code, subject, argument, value (16 bytes) |
| `0x4B` | Zeppelin state | unreliable, host to all | placement index, per-zeppelin sequence, position, speed, pitch, yaw (32 bytes) |
| `0x4C` | AI spawn | reliable, host to all | admission ordinal, launch counter, generator index, net index, flags, lever, position, drop direction, velocity (44 bytes) |
| `0x4D` | Surface vehicle state | unreliable, host to all | spawn index, per-hull sequence, name hash, position, speed, yaw (32 bytes) |
| `0x57` | Destructible hit | reliable, guest to host | registration index, name hash, health damage (16 bytes) |

`0x4C` is one generator aircraft launch. The host admits the aircraft as it launches and sends the
ordinal it got; the guest builds the launch only when that ordinal is its own next one, which
reliable ordered delivery makes the normal case, and drops it otherwise. The guest builds with the
host's net pick, pose, launch velocity and lever, and forms the name from the host's launch counter,
so the mission script names the same aircraft on both ends. A guest's own cycles still run their
timers and doors, but its spawner refuses every aircraft launch they come due for. No take-off run
is started on the guest, since the host's samples fly the copy along it. A surface hull launch is
not carried here.

`0x4B` is one zeppelin of the original's `0x1e`, its first `0x18` bytes in the original's order, on the
same half second. A zeppelin is named by its placement index, which both ends build from the same
records. The part-state word and the cannon-shot tail are not carried: a part's death arrives as a
pool event, and each end's cannons still fire on their own. A guest runs the original's chase
(`Flight/Airframe/ZeppelinReplica.cs`) in place of its follower, so the two ends cannot part on a branch
pick. A hull the host holds, has not woken or has lost is not sent, and the guest's copy stays where
the last sample left it.

`0x4D` has no counterpart in the original, which has no campaign across a link and so no patrol
boat to send. It takes `0x4B`'s half second and its chase, with the pitch dropped because a hull
rides the water. A hull is named by its index in the surface runtime's spawn list and by the name
hash `0x48`'s code 3 uses; a guest checks the hash and searches by it when a generator's launch has
shifted the index. A patrol replayed on each end diverges without a branch draw: the wake arrives
late, a `SET_AI_NET` starts its route from wherever the hull stands, and a launch spawns on each
end's own timer. The roster and `SET_AI_NET` nets of C1B/M03 and C2/M01 carry no node with three
or more neighbours, and C2/M01's launch net carries one, so the branch pick is the smallest of the
four.

AI state is plain unreliable rather than sequenced because every AI shares one channel, and a
transport sequence would drop one AI's sample against another's; each AI's own pose buffer drops a
stale one by the per-AI sequence. It rides the seat stream's cadence. The world event codes are
`NetWorldEvent`: 1 an AI downed (the argument is the killer's seat or -1), 2 an AI's hull (the
value its health fraction, the argument its whole armour as a 16-bit fraction, sent in the step
after either moved and mirrored into the guest's copy; an AI airframe has no zones),
3 a destructible pool's health (the subject is its registration
index, the argument a hash of its definition and anchor names, which the guest checks before
applying and searches by when the index has shifted), 4 a `WARP_VEHICLE` pick (the subject is the
drawn waypoint index, the argument the hash of the warped vehicle's name), 5 an AI's presence (the argument is 1 for in
play and 0 for deactivated). The host sends 5 whenever an AI's `Inert` changes outside a cutscene
park, which covers a script wake, a Black Hat launch, a wingman taken out and an aircraft a
`TRAVELERS ... DELETE_ON_SUCCESS` removes. Only the host's graph evaluates that condition, so the
removal is decided there and a guest's copy leaves on the 0. A cutscene park (913)
is not sent, because each end's own cutscene parks its own copy. 6 a seat left (the subject is
the seat of a guest whose link dropped mid-mission): the host sends it to the guests still flying,
every end removes that aeroplane and tells its players "<name> left", and the mission goes on.
7 an AI's combat-voice raise (the subject is the ordinal of the AI the line is about, the argument
packs the trigger in bits 0..7, a broadcast flag in bit 15 and the broadcast's team in bits
16..31); see "AI voice across the link" below.

Code 3 goes out at once for a stage change or a kill. A hit that lowers a pool without either is
held and sent on the seat stream's next tick, one sample per pool however many hits landed, because
three guest-visible rules read the health between stages: the target bar's fraction
(`Flight/Weapons/TargetPool.cs`), a surface hull's injure ladder (`Flight/Ai/SurfaceVehicle.cs`), and every
`ANIM_HEALTH` condition. A sample waiting when a stage change goes out is dropped, since the stage
event carries the same health. Chip samples ride the reliable class with the stage events: a guest
only ever lowers a pool, so an old sample arriving late changes nothing, and a lost last one would
leave the guest's copy high until the next hit.

A hit on an AI is decided once: by the host for its own rounds and for every round no seat fired,
and by a guest for its own seat's rounds, which it claims with `0x47`. A world pool is spent only
on the host, which simulates every round, a guest's included, from the fire events; a guest's
world runtime reports a struck pool as hit and spends nothing. A ram on an aeroplane flown
elsewhere spends nothing on it either, since its owner's own sweep resolves that half.

The debug kill key (F17) on a guest kills nothing locally. On an aeroplane it offers the victim's
hit router one lethal round from the guest's own fit, so an AI is claimed with `0x47` and another
seat's aeroplane with the seat hit `0x22`, and the owner's death reaches the guest as `0x48` code 1
or `0x12`. On a pool it sends `0x57`, which names the pool as code 3 does; the host spends
it through its own damage path, and the kill comes back as code 3. `0x57` has no counterpart in the
original, and a guest's rounds never use it, since the host replays them from the fire events.

Each simulation phase, as a guest runs it:

| Phase | On a guest |
|---|---|
| Ending hold, radio, smoke screens, beeper tags, incoming fire | Local presentation or per-pane rules, no world authority. |
| Landing approaches, ladder switch | **Host-decided**: a row start and a ladder holder arrive as `0x4E`; the guest offers the auto-land prompt to its own humans and sends their held button. |
| World animation | Local, except a `PlayerRange` gate whose definitions raise a code, which is **host-decided** and read from `0x4E`'s range gate verdict. |
| Capture AI aircraft | Local membership; new AI are admitted by ordinal before any is stepped. |
| Projectiles | Local on every end, spawned from fire events; the guest's rounds spend nothing on the world or on an AI. |
| Human aircraft | Replicated per seat (`0x0f`, `0x10`, `0x22`, `0x40`, `0x12`). |
| Captured AI aircraft | **Replicated**: each AI flies from `0x45` samples; fire arrives as `0x46`, hull and death as `0x48`. |
| Zeppelins | **Replicated path**: each hull chases the host's `0x4B` samples. Part and cannon deaths arrive as pool events; the broadside still fires locally. |
| Turret emplacements | Replayed locally and cosmetic: a guest's turret round spends nothing, the host's decides. Deaths arrive as pool events. |
| Generators | **Host-owned launches**: each aircraft launch arrives as `0x4C` at the host's ordinal. The guest's cycles and doors run on its own timers and are cosmetic; their aircraft launches are refused. |
| Surface vehicles | **Replicated patrol**: each hull chases the host's `0x4D` samples. A hull's death arrives as a pool event; its gun still fires locally. |
| Instant action | Not run in a network match. |
| Campaign | The director replay above. |
| AI voice | **Host-raised**: a replicated AI's attack pair, shake taunt, gloat and ally distress arrive as `0x48` code 7 and pass this end's own gate; its DI tiers and death cry are derived from codes 2 and 1. |
| Versus | The match state above. |

### AI voice across the link

The original sends no voice trigger and no mode code. Its one send, `FUN_005b2640`, is called only
by the message builders, and the addressed gate `FUN_004afd00` (the speaker in `ECX`, the trigger
and the force flag on the stack) is reached from no receive handler: its callers are
`FUN_0041d9f0`, `FUN_0041f420`, `FUN_0043d640`, `FUN_00470750`, `FUN_00498170`, `FUN_00498bf0`,
`FUN_004b82d0`, `FUN_004b86a0`, `FUN_004b9770` and `FUN_004b9bc0`, and none of the `0x10` fire
handler (`LAB_00498950`), the `0x21` handler (`LAB_004990d0`) or the `0x24`/`0x25` handler
(`LAB_0049bde0`) calls it. The broadcast helper `FUN_004b86a0` is reached from `FUN_0041d9f0`,
`FUN_00446990`, `FUN_00470750`, `FUN_004aabb0` and `FUN_004b9bc0`, none a network handler. Each peer
derives its call-outs from what it already receives, with no mode machine for a remote aircraft:

- `FUN_00470750`, run per remote vehicle from the remote update `FUN_00470210` (called by
  `FUN_004897c0`), skips a remote on the local player's side (`FUN_004952f0`, remote record `+0x3c`
  against the local record's) and one farther than 1695.0 from the local player (`0x00607e78`), and
  otherwise raises the taunt 26 or 25 off the geometry (`0x00470822`, `0x00470849`) and the bearing
  broadcast (`0x004708e0`).
- `FUN_00498170` applies a `0x0f` state: the low byte of `+0x1c` becomes the vehicle's health, and
  when it falls on a remote of the local side the gate raises DI 19, 18 or 17 against 0.3, 0.5 and
  0.7 of the vehicle's whole (`0x004985cf`).
- The death handler `FUN_00498bf0` plays the dying pilot's cry itself (`0x00498f67`, `0x00498f9a`).

The remake cannot derive the same way, because its raises read what only the flying end holds: the
gunner's target behind the attack pair, the mode machine's evade flag behind the shake taunt 27,
the killer behind the gloats 22 and 23, and the shooter behind the ally distress 28. A guest's copy
of an AI steps none of those, and stepping them would voice transitions the host's AI never made.
So the host relays each such raise as code 7, reliable and to every guest, and each guest runs it
through its own gate: the mute window, the speaker's aliveness and radio, the slot cooldown, the
talker roll, and for a broadcast the election among its own speakers on the named team. The raise
is per AI and each session has one voice runtime, so a splitscreen host's panes send it once. The
DI tiers stay derived, as the original derives them, from code 2's hull fraction, and the death cry
from code 1's death. A guest's own copies raise nothing locally.

No message carries the speaker's pilot either. The original hands a mission vehicle the local
player's own voice (`FUN_00499c60`, the lobby record's `+0x78`, the player's chosen voice, at
`0x0047531e`), so its peers do not agree on it. The remake deals each AI a pilot from its accent's pool in registration order on every
end, the single-player rule, and the ends agree because they register the same AI in the same order
([`../formats/combat-voice.md`](../formats/combat-voice.md), "Which pilot of a pool an aircraft
takes").

## Positional starts

A landing row and the ladder switch start definitions off where a human is, and in a campaign
across a link that human may be flying on another machine. The host decides both over the whole
field, its own panes and each guest's copy, and sends the decision; a guest's trigger and switch
are replicated and decide nothing (`Session/Campaign/NetPositionalStartLink.cs`). Deciding on each end
instead would start a row twice or not at all, since each end reads a guest's aeroplane a buffer
delay apart.

| Id | Message | Class | Carries |
|---|---|---|---|
| `0x4E` | Positional start | reliable | kind, seat, flags (bit 0 held), row index in the bound table (12 bytes) |

The kinds are `NetPositionalStart`:

| Kind | Sender | Meaning |
|---|---|---|
| 1 Landing row | host to all | the row the host's trigger started, and the seat whose flying started it |
| 2 Ladder holder | host to all | the seat now holding the switch, or no seat |
| 3 Auto-land held | guest to host | the guest's own seat has the auto-land button down while offered the prompt, or has let it go |
| 4 Range gate | host to all | the host's verdict on a code-raising `PlayerRange` gate, sent when it changes: the row is the gate name's hash, the held flag the verdict, no seat |

A guest starts the named row for the named seat's rig, which makes that seat the episode's owner
on the guest as on the host. An `auto` row's prompt is per pane, so a guest still tests its own
humans against the row and draws the prompt; a press is held from the button until the prompt goes
away, because the host's copy reaches the sphere a buffer delay later and a one-frame press would
be over by then. The host accepts a held button only from the machine that owns the seat, and its
copy of that seat answers the auto-land test with it.

What the row then plays is derived on each end from its own playback, like the director's
cutscene codes. The airframe swap (965 to 967) runs on every end for the owner's seat: the owner's
machine rebuilds its own aeroplane, every other end rebuilds its copy as a copy again, fed by the
same seat's samples and wired for combat as a fresh seat is. The captured aircraft's group and
hidden hull are applied on every end. The hand-over to the wingman activates a block built with
the world, so it names an ordinal both ends already hold, and the host's samples fly it on a
guest. A guest's scripted player, which an episode claimed by nobody
belongs to, is the host's first seat.

A `PlayerRange` gate is the host's when its definition, or one it starts through
`CALL_ANIMATION`, raises a `CALLBACK` (`Mech3/Anim/RangeGateAuthority.cs`). The host tests it
over its whole field and sends kind 4 when the verdict changes; a guest answers the gate with the
host's last verdict and never tests it against its own field. A pass the guest's gate has not read
yet is held until it is read, so a host pass shorter than the gate's poll still starts the
definition once on the guest. The gate is named by its definition, anchor and radius in metres
squared (`blacke_drop/blacke_marker/4096`), and the wire carries that name's FNV-1a hash.

Of the 216 shipped animation names carrying a `PlayerRange` condition (1016 definitions over the
shared, chapter and mission sources), C4/M03's `blacke_drop` is the only one whose closure raises a
code: its 64 m gate on `blacke_marker` starts `bdplayer`, `bdchute` and the two drop cameras, which
raise cutscene codes 11, 951, 1 and 2, and it reaches `dropped_blacke`, which the mission's
objectives read. Every other gate stays on each end over the whole field, so its start can differ
between ends by the link delay:
- the ordnance washes (the gun hits, ground effects, flak, muzzle bursts), which each machine draws
  for its own viewer;
- the zeppelin props at 270 m, the MP-map and repair-base `call_door` at 25 m, C1's lightning,
  C1/M02's `pure_panic` and C1B/M03's `foghorn_toot`;
- C4/M03's `blacke_drop_east` on `blk_e_marker`, which picks which drop camera leg plays. Both
  legs raise the same codes, so the choice is presentation, but the two ends can show different
  legs;
- C5/M02's `arcadia_start` at 1400 m, which burns the Workers' Voyage's gas bags and so sets the
  `panels` nodes its objectives count inactive. A node state, not a code: the guest's objectives
  are the host's replicated graph, and the burn is drawn on each end.

Every end draws the episode owner on the staged `player` marker, the owner's own machine and every
other one alike. On an end where that seat is a copy fed by samples, the copy is in no pane, and
the cutscene host poses it on the marker each frame over the pose its samples write, then hands it
back to its feed at the handoff. A guest's docking therefore shows the guest's aeroplane on the
host, and the host's docking shows the host's on each guest.

## In-flight chat

[Evidence: decoded] **The keys.** Commands `0x6d` Chat to Everyone and `0x6e` Chat to Team (DIK
GRAVE and Shift+GRAVE, [`input.md`](input.md)) share one handler, `FUN_00489570`, registered at
`0x0048972c` and `0x00489738`. It does nothing unless `FUN_00440ad0` reports a network game, and
otherwise calls `FUN_004a8340` on the flight UI object at `0x0071d3a0` with 1 for `0x6d` and 0 for
`0x6e`. That call opens an all-chat whatever the key when the byte at `0x0071d89c` (the object's
`+0x4fc`, the "mode has teams" flag the death report reads) is clear, so a match without teams has
no team line.

[Evidence: decoded] **The entry.** `FUN_004a8470` builds a `0x2328`-byte edit object
(`FUN_0046d740`, vtable `0x00607c2c`) as a child of the chat panel. Its caption is langui 7049
`MSG_GLOBAL_MESSAGE` "To All:" or 7050 `MSG_SQUADRON_MESSAGE` "To Team:" (`0x0046d7a2`,
`0x0046d7a9`), and its text buffer is sized 60 bytes (`FUN_005cc050(0x3c)` at `0x0046d797`). The
text opens as "> " (`0x0063f93c`, written by `FUN_005cbe40`), which leaves 57 typed characters
before the terminator. The submit handler `FUN_0046d800` (vtable slot at `0x00607cfc`) takes the
text after the ">", trims both ends, and closes the entry without sending when nothing is left
(`0x0046d898`). Otherwise it posts the sender's own line, the caption and `" %s"` of the text
joined by `"%s %s"` (`0x0046d8db`, `0x0046d911`), so "To All:  hello" with two spaces, and sends
`0x15` through `FUN_00499a50`. Opening the entry also sets the panel's ten-second timer.

[Evidence: decoded] **The wire.** `FUN_00499a50(text, all)` writes the type, the length (text + 8),
the text length at `+4` and the text at `+6`. An all-chat is one unguaranteed broadcast; a team line
is sent guaranteed, to the DirectPlay id at `+4`, for each entry of the list at `0x0071c150` whose
`+0x18` equals the team slot at `+0x3c` of the record `0x0071c7ac` points to (`0x00499af5`). Nothing echoes the send, since the submit handler posted it.
The handler `LAB_00499b30` keeps at most 80 characters (`0x00499b5f`), finds the sender's remote
record by DirectPlay id (`FUN_00499d80`) and names it by that record's `+0x34`, or by langui 6007
`MSG_UNKNOWN` "Unknown" when there is none, and posts `"%s: %s"` (`0x0062918c`).

[Evidence: decoded] **The panel.** `FUN_004a8390` builds it once per flight at `+0x508` of the UI
object (`0x0071d8a8`): a five-line text list (`FUN_005c5950(5)`), font `mpChat` (Arial 12, green
0, 255, 0, shadow, weight 500 in `fonts.zrd`), placed at 10, 10 and sized 300 by 100 in 640-by-480
display pixels (`0x004a8404` to `0x004a842d`). `FUN_004a8570` formats a line into it (vtable
`+0xa0`, `FUN_005c5e10`, which writes the newest line with no timer of its own) and then hands the
whole panel a 10.0 s timer (vtable `+0x60`, `FUN_005c54a0`), after which the panel hides with its
lines kept. Display Scores (command `0x23`, `FUN_00489320`) hides it at once
([`multiplayer-scoring.md`](multiplayer-scoring.md) "The in-flight scores").

**The remake.** `Session/World/NetChatLink.cs` routes by lobby team, `NetSeat.TeamId`, never by a
team id. A guest sends its line to the host alone; the host forwards an all-chat to every other
machine and a team line only to a machine that flies a seat on the typist's team, once per machine
however many seats it flies, and shows a team line itself only when it flies that team. A line is
reliable either way, where the original sends an all-chat unguaranteed, since the star's relay
would otherwise lose a line on either leg. The seat rides in the message because a relayed line
reaches its reader from the host, not from the typist; the host takes a line only from the machine
flying the seat it names. The panel draws in every local pane (`Flight/Hud/ChatPanel.cs`), and the
entry only in the pane whose seat reads the keyboard: the splitscreen seats beyond the first are
pad-only, so they read the chat and type nothing. While a line is open, and until every key pressed
into it is released, that seat's flight keys read idle. The panel stays up while its typist types.
While a pane's seat holds Display Scores the panel steps aside in that pane and comes back on the
release ([`multiplayer-scoring.md`](multiplayer-scoring.md) "The in-flight scores").

| Id | Message | Class | Carries |
|---|---|---|---|
| `0x66` | Flight chat | reliable, guest to host and host to each admitted machine | the typist's seat at 4, flags at 5 (bit 0 a team line), two reserved bytes, the line in 81 bytes UTF-8 at 8 (89 bytes) |

## Cutscene skip

Every end plays each shared episode (an intro, a landing row, a director-started film), and any
player's skip ends it on every machine: the splitscreen rule, where any seat's skip ends the one
film for every pane, carried across the link. The original has no counterpart, since it runs no
campaign across a link and its cutscene code 914 is the one it skips in multiplayer. The host
decides (`Session/World/NetCutsceneLink.cs`).

| Id | Message | Class | Carries |
|---|---|---|---|
| `0x58` | Cutscene skip | reliable, guest to host (an ask) and host to all (the skip) | skipper's seat, a reserved byte, the episode's ordinal, the episode's key (12 bytes) |

A guest's skip input ends nothing locally: it sends the ask and the guest plays on. The host takes
an ask only from the machine that owns the seat, skips its own episode and broadcasts the skip,
and a host's own skip is broadcast the same way. The asking guest ends its episode on that
broadcast like every other guest.

An episode is named by its definition's name key (the FNV-1a hash the world messages use) and by
how many episodes of that definition this end has played, counting this one. Both ends play the
same episodes of a definition in the same order, so the pair names one film on every machine, and
a late or repeated skip names an episode that is already over there and is dropped rather than
ending the next one. A skip for an episode an end has not started yet, or has started but not yet
armed for skipping, is held and applied the frame that episode can take it, since a guest's replay
can start a film after the host's skip of it arrives.

The skip notice (`SplitScreen.NoteSkip`) is the pressing machine's own: it names the local skipper
on the machine where the input landed, and no machine names a skipper from another.

## The start barrier

Every machine holds its flight on its load screen until every machine flying a seat has built its
world, and then all of them start (`Net/NetStartGate.cs`). The original has no counterpart. The
word is a session message: it is sent only once the sender's handlers stand, so it never waits in
the lobby.

| Id | Message | Class | Carries |
|---|---|---|---|
| `0x5B` | Start gate | reliable, guest to host (loaded) and host to guests (hold, start) | word at 4 (loaded 1, start 2, hold 3), round at 5, two reserved bytes (8 bytes) |

Each host flight takes a round, 1 to 255, that the flight before it on the same process did not.
Once its own world is built, the host sends hold under that round to every machine it waits on.
A guest keeps the first round it hears. It sends loaded under that round as the last act of its
build (round 0 when no hold has reached it yet), and answers a hold that arrives after its build
with loaded again. A loaded under another round than the host's opens nothing, and the host
answers it with hold under its own. A restart binds the next flight on the same link, so a loaded
from the flight before can reach the new one, and taking it would start the host before that
guest's new world is built. The host broadcasts start under the round when the last machine
reports, drops, or does not answer within the timeout. A loaded under the round that reaches a
host already started is answered with a start to that machine alone. A guest takes only a start
under the round it kept. While a machine holds, its clock takes no step: the
mission clock, the AI, the director and the world events wait with the aeroplanes, and only the
link and the clock ping are stepped. The host-owned world therefore sends nothing to a guest
before that guest's world can apply it, unless the wait gave up on that guest.

## Stunt race

The remake's own Stunt Race (mission type 3, "Dogfight lobby" below) has no counterpart in the
original. Each machine times its own seats' runs on its own sim clock, as it owns their aircraft
state and deaths, and reports them to the host. The host keeps the window clock, the leaderboard
of best runs, the final-run stretch and the ending, and sends the leaderboard and its clock to
every guest (`Session/World/NetRaceLink.cs`, the race itself `Flight/Modes/StuntRace.cs`).

| Id | Message | Class | Carries |
|---|---|---|---|
| `0x67` | Race run | reliable, guest to host | seat at 4, kind at 5 (started 1, zone 2, finished 3, abandoned 4), course zone at 6 (`0xFF` for none), window at 7, the owner's run number at 8, two reserved bytes, the run time at 12 (16 bytes) |
| `0x68` | Race state | reliable, host to each guest | phase at 4 (opening 0, open 1, final run 2, ended 3), window at 5, two reserved bytes, seconds into the phase's clock at 8 (the opening's while it runs, the window's after), the window's length at 12, the host's session clock at 16 (20 bytes) |
| `0x69` | Race standing | reliable, host to each guest | seat at 4, flags at 5 (bit 0 in a run, bit 1 a completed run, bit 2 the pilot left), window at 6, split count at 7, runs started at 8, runs finished at 10, best time at 12 (0 without a completed run), time to the most zones at 16, most zones at 20, the run in progress's zones at 21, two reserved bytes, then 24 splits of 4 bytes, the ranking run's, -1 for a zone it never cleared (120 bytes) |
| `0x6A` | Race call | reliable, host to each guest (rerun, lobby) and guest to host (leave) | call at 4 (rerun 1, lobby 2, leave 3), window at 5, two reserved bytes (8 bytes) |

**The reports.** A guest sends one report per event of its own seat's run: the run clock's start on
the step after its count's GO, each zone's first clearing with its run time, the finish, and a
rerun that throws the run away. The owner numbers its runs from 1 within a window, and the window
number is 0 for the first, one more for each rerun. The host takes a report only from the machine
flying its seat and only under its own window. A start must carry a run number above the newest it
heard from that seat, and any other report must name that newest run, so a repeated start or a
report of a run already superseded counts nothing. A zone or finish with no finite run time is dropped. The race
then applies its own rules to what passes: a start counts only in the open window, by when it
reaches the host, a finish after time up counts while the race runs, and nothing counts after the
end. The host's own seats feed its race directly.

**The leaderboard and the clock.** The host sends a racer's line whenever its record of that racer
changes, then its clock once a second (`MatchStateCadence`'s tick) and at once on every change of
phase. The lines go first on the one ordered channel, so a guest's board, which wakes on the
ending, already holds every line the host decided before it. The race does not reuse `0x17`: its
fields are a Dogfight's score target and end reasons, and a race has an opening and a final run
where a match has neither. A guest's race is a replica. It takes each line whole and takes the
host's phase, which only moves on; its opening and window clock run between readings, so its own
time up refuses a rerun there, but it never ends of its own accord and ends on the host's
ending. A guest records its own pilot's best off the host's line, so a finish the host refused is
never saved. The final-run cap is the host's alone.

**The opening.** Every machine holds on the start barrier, and the host's start word reaches a
guest a link later, so the guest's opening count would start that much behind. The host's reading
carries its session clock; the guest reads it forward by its lateness, its own clock plus the
newest offset reading less the stamp, and catches its race's opening and every local seat's count
up in whole steps. It counts the step it is about to take, rounds down and stops one step short of
GO, so it errs a step behind: a guest that opened first would send a run start into the host's
still-closed window, which refuses it. A round trip measured across the held start reads short,
since neither clock moves while held, so a guest asks the host's clock again on its first flown
step.

**Wire compatibility.** A build without these ids drops them as unknown. Builds share a lobby only
on the same MAJOR.MINOR (`BuildVersionMessage`), and these ship with type 3 in a minor release.

**A new window.** Only the host's board offers Restart; a guest's reads "Waiting for the host" in
its place, and a guest's own rerun opens nothing. The host's Restart sends a rerun call naming
the new window, at the rerun itself and so ahead of that window's lines and clock on the one
ordered channel. A guest takes it only when the window is the one after its own, and opens the same
window: every local seat back on the shared spawn behind a fresh opening count, its race cleared.
The new window's clock then catches the guest's opening up to the host's instant exactly as the
first window's does. A guest drops a line or a clock under another window than its own, which is an
old window's.

**Leaving.** A guest that leaves a race keeps its link when it walks back to the lobby, so it sends
a leave call first; a dropped link reads the same. The host takes each seat of that machine out of
play as for any guest leaving a mission: the aeroplane goes inert, which takes its ghost and its
label, and every other guest hears it through the world link's seat-left event. The racer stays on
every board with its record, ranked as it stood and marked left: the scores page's grey row on the
Original board and in the lobby, a "(left)" suffix on the Built-in board, the Built-in held scores
table and the live leaderboard line. A run it had in progress stops, nothing more counts for it, and a new
window leaves it out. A race left with one pilot runs on to the window's end, since a race builds no
Dogfight match and so has no "fewer than two pilots" ending. The host leaving mid-race closes its
door, which ends every guest's flight as a Dogfight's host leaving does.

**The lobby.** The host's board names its exit Lobby. Taken from an ended race it sends a lobby call
and lands the host on the lobby's Game Scores, its door kept; each guest's launcher follows the call
the same way. Game Scores then shows the race's table in the race board's own columns, a pilot who
left on the grey row, where a Dogfight's lines stand after a match. A guest's board names its exit
Leave, which lands it on the same page.

**Joining late.** A peer that connects while a race is in flight waits on the lobby with the advert,
as for every network match: the lobby passes a bound session only the peers present when it bound,
so no line reaches the newcomer and no report of its counts.

## The lobby

Before any session binds the carrier, a `Net/NetLobby.cs` stands on it. A host sends the advert
there, to each peer as it connects and again whenever the offer changes, and sends session closed
to every guest before it closes the socket or to a guest it has no seat for. A guest's lobby keeps
the latest of each and never passes either to the session, so the session's own vocabulary never
sees them.

| Id | Message | Class | Carries |
|---|---|---|---|
| `0x4A` | Session advert | reliable, host to each guest | session kind (Dogfight 1, campaign co-op 2) at 4, campaign mission sequence or `0xFF` for none at 5, player count at 6, status at 7 (unknown 0, waiting 1, in mission 2, full 3), seat cap at 8, flags at 9 (bit 0 the host asks a password), two reserved bytes, the game's name in 16 bytes UTF-8 zero padded at 12 (28 bytes) |
| `0x4F` | Session closed | reliable, host to each guest | reason at 4 (unknown 0, closed 1, full 2, version mismatch 3, booted 4, wrong password 5), three reserved bytes, the host's build version at 8, the guest's as the host heard it at 12 (16 bytes) |
| `0x56` | Build version | reliable, each end to each peer on connect | the build version at 4 (8 bytes) |

A guest that reads a closed reason tells the player the host closed the game, that the game was
full, or which two versions kept them apart, instead of reading the dropped link as a lost
connection. An unknown status or reason reads as unknown rather than failing the message, so a
newer host's value does not strand an older guest.

### The build version check

A build version is MAJOR.MINOR of the SemVer string in `project.godot`, as `Net/NetBuildVersion.cs`
parses it: the major and the minor, 16 bits little endian each. Both words at `0xFFFF` are unknown,
the version of a build whose string does not parse, and a version with one word at `0xFFFF` fails
the message. Two builds play together when their majors and minors match, so builds a patch apart
play and builds a minor apart do not. Unknown plays only with unknown: a build that cannot name its
version never joins a build that can.

Each lobby sends its build version first on every connect, before the advert. A lobby that hears a
version that does not play with its own takes that peer off every peer list and drops everything
it sends except a session closed. A host door then sends that guest session closed with the
version mismatch reason and both versions, and hangs up after the same grace as a guest refused
as full. A guest door refuses a host of another version itself, so a host that never says why is
still refused with both versions named. A peer that sends no build version is not refused. The
command line's `--net-host` and `--net-join` stand no lobby on the carrier and check nothing.

⚠ The layouts of `0x56`, `0x4F` and the LAN discovery reply do not change. They are how two builds
of different minors recognise each other, so a change makes an older build read a newer one as
silent or foreign instead of naming the mismatch.

### Game and Player Information

Before a network game opens the original asks two things, in `MULTIPLAYERHOSTMODAL.SCRIPT` (GAME
INFORMATION, string 10032) and `MULTIPLAYERPLAYERMODAL.SCRIPT` (PLAYER INFORMATION, 10036). The
Connection page's Host and the games list's Create Game open Game Information, whose OK opens
Player Information; Join Game opens Player Information alone. Player Information's OK is the
join or the lobby, and either box's Cancel goes back to the page under it.

- **Game Name** (10027): an edit box of at most 14 characters (`SZ.FD`), prefilled from the saved
  game name (setting callback `2142`, index 2). An empty name greys OK (`gui_execute` mails 1 to
  the button). A refused name raises 10511, "Invalid game name.".
- **Password (optional)** (10028): a masked box (`UZ.SC = 1`) with no length set in the script.
- **Maximum # of Players** (10029): a spinner whose floor is `VZ.VF = 2`. Both doors set its
  ceiling and value as they open it: `VZ.WF = 16`, `VZ.YF = 8`, or 2 and 2 for Modem-to-Modem
  (`MULTIPLAYERMAIN.SCRIPT` and `MULTIPLAYERGAMESLIST.SCRIPT`, the `mail(1108)` refresh). It is not
  saved.
- **Callsign** (10033): at most 12 characters (`UGA.FD`), prefilled from the saved callsign
  (index 1). An empty callsign greys OK, and a refused one raises 10510, "Invalid Callsign.".
- **Voice** (10034): a drop-down of seven voices, strings 10039 to 10045, each row with a value in
  `WGA.LG[R].SF`: Nathan Zachary 48, Jack 2, Black Swan 24, Paladin Blake 29, Loyle Crawford 44,
  Gruff Male 26, Texan Male 31. The picked row is saved (index 4); its value goes to `$$AHA$$`.
  [Evidence: decoded] The value is a pilot VO id, held in the `nVoice` setting (`0x00642f14`). It
  travels as DirectPlay player data at `+0x4c`, written as the session opens (`FUN_00412b60`,
  `0x00412bf2`) and copied into each peer's lobby record `+0x78` (`FUN_00414640`, `0x004147aa`), and
  every aircraft of that player speaks as that pilot
  ([`../formats/combat-voice.md`](../formats/combat-voice.md), "A player's own voice").
- **Password** (10035): the join's password, greyed unless callback 5003 answers that the picked
  game needs one (`gui_init`); the capture of a LAN TCP/IP game shows it greyed. OK copies it to
  `$$ZGA$$` before the join (message 1074), and the join hands it to DirectPlay (below, "Boot and
  the password"). Which field 5003 reads is not traced; the games list's own password mark is.

[Evidence: decoded] The name test is `FUN_00407060`: `GetStringTypeExA` with `CT_CTYPE1` over the
text, and the name passes as soon as one character carries a class outside space, blank and
control (mask `0xff97`). So an empty name or one of spaces alone is refused. `FUN_00407670` calls it
three times, at `0040820c` (the callsign), `0040824f` and `00408290`; the second is read as the
game name's callback and the third is not tied to a box.

The remake follows both boxes in the Original presentation (`UI/Menu/Original/OriginalNetInfoBox.cs`)
and offers the same choices as rows of the Built-in Network board. The cabin's remake-only HOST
CO-OP asks both boxes too. The cap is held to the kind's own: four humans for campaign co-op, sixteen
for a Dogfight. The game name is the advert's name, and the games list shows it and the chosen cap
as they are. A host past its cap refuses a guest with the full notice, a Dogfight host as a co-op
host does. The callsign names this machine's player in every roster, list and line. The voice
rides the pick's flags byte, so the pick keeps its 36 bytes and a build a patch older reads the
same pick without it. At launch the host writes each seat's voice into bits 1 to 3 of that seat's
flags byte in the seat roster `0x27`, beside the host bit, so every machine speaks every seat in its
chosen voice; the entry keeps its 20 bytes, and a build a patch older reads the host bit alone.
Bit 4 marks a machine's own player who gave no name, whose callsign field holds only a stand-in, so
every machine's marker reads that seat as "Unknown" ([`targeting.md`](targeting.md)). A
co-op host's first seat carries Nathan Zachary's, the scripted player's, and a splitscreen seat
carries none. The callsign, the voice and the game name are remembered in `options.json`
for the next session; a password never is. Game Information's password is the one the host asks,
and Player Information's is the one a join answers with. That box is live for a join to a game the
list marks Need Password and for a join by typed address, whose advert is not known yet, and greyed
on a host's own box.

### Boot and the password

[Evidence: decoded] **Boot.** The lobby's Boot is `KDA` in `MULTIPLAYERLOBBY_READY.SCRIPT` (string
10054, `mp_b_small.png` at 19, 325), created greyed. The script's refresh (message 1301) makes it
live only on the host (`$$OX$$`) while the picked player-list row is a player (row kind 0 or 2 from
callback 5002; a team row is kind 1), and greys it otherwise. Its press calls `$$A$$` 1005 with the
picked row's player id, then presses the picked row again, which lets the pick go. The screen-flow
dispatcher `FUN_00407670` sends 1005 to `FUN_00413090`, which acts only on the host
(`FUN_005b4210`) and never on the host's own player id (`this+0x160`). It posts lobby notice 1
naming the player (`FUN_00413270`, message `0x18`, sent to everybody), sends a `0x1a` setting
change of subtype 8 for each entry of the table at `0x6453a8` that the player holds, and destroys
the player through `IDirectPlay4::DestroyPlayer` (`FUN_005b2480`, vtable `+0x24`). Every end shows
notice 1 in its chat as langui 10500, "[%1!s! was booted from the game.]" (`FUN_00414120` posts
message 1018, whose handler in `FUN_004084a0` formats `0x2903` plus the notice code). The original
keeps no ban: DirectPlay would admit the booted player again. What a booted guest's own screen shows
is not traced.

[Evidence: decoded] **The games list's mark.** `FUN_00414b40` turns each enumerated session's state
into the list's status: state 1 reads Ready (10089), 3 In Progress (10090), 2 **Need Password**
(10141) with the entry's password flag set (`DAT_00642f40`), and anything else "???" (10091).
`FUN_00402f40` writes the Status cell and puts Game Full (10140) over any of them once the player
count reaches the cap.

[Evidence: decoded] **The join's password.** `$$A$$` 1004 joins through `FUN_00412e30`, which puts
`DAT_00642f10`, the Player Information password, into the join's session descriptor. `FUN_005b3bd0`
copies it into `lpszPassword` when the session's flags carry `DPSESSION_PASSWORDREQUIRED` (`0x400`)
and calls `IDirectPlay4::Open` (vtable `+0x60`) with `DPOPEN_JOIN | DPOPEN_RETURNSTATUS` (`0x81`).
DirectPlay checks the password on the host. A refusal returns to 1004 as `DPERR_INVALIDPASSWORD`,
which `FUN_005b1770` names `MSG_DPERR_INVALIDPASSWORD`, "Invalid Password" (messages 7041). Player
Information shows that in a messagebox and goes back to the games list. Langui 10509, "The password
is incorrect. Please try again.", has no reference as an immediate in the executable.

The remake follows the original where it speaks and fills in what DirectPlay did for it:

- **The mark.** The advert's byte 9 is a flags byte, bit 0 set when the host asks a password. It was
  a reserved zero, so a build a patch older reads the rest unchanged. The games list reads Need
  Password in the Status column unless the game is full, which reads Full.
- **The check.** A host with a password holds every new peer off every peer list until it answers.
  A guest answers an advert that asks a password with `0x60`, its typed password, once per
  connection. The host compares it ordinally; a match is answered with `0x60` marked admitted, and
  only then does the lobby list, seat and tell the guest anything. A wrong answer, or none within 10
  seconds (a build a patch older sends none), is sent session closed reason 5 and hung up on after
  the full notice's grace. The guest reads Invalid Password over the Connection page. A session with
  no password sends no `0x60` at all.
- **Boot.** Boot is live on the host while it has picked a guest's row; the host's own row is never
  offered. The guest is sent session closed reason 4 and hung up on, and the lobby's chat on every
  other end reads 10500, a chat line `0x55` under no name. The cabin's remake-only BOOT asks about
  each co-op guest in player order, since a campaign board has no chat. The booted guest reads "You
  were booted from the game" over the Connection page.
- **The ban.** The remake adds one the original lacks: a boot bans the address the guest connected
  from until the session closes, and a later connection from it is refused with reason 4 before its
  password is asked. It is an address, so another player behind the same router is refused too, and
  a carrier that names no address bans nothing.

| Id | Message | Class | Carries |
|---|---|---|---|
| `0x60` | Join password | reliable, guest to host (the answer) and host to guest (the admission) | flags at 4 (bit 0 admitted), three reserved bytes, the password in 48 bytes UTF-8 zero padded at 8 (56 bytes) |

### Campaign co-op boards

A co-op guest follows the host's boards through six more lobby messages, minted at `0x50` to
`0x52`, `0x59`, `0x5A` and `0x5F` since the original has no campaign across a link. None reaches a
session.

| Id | Message | Class | Carries |
|---|---|---|---|
| `0x50` | Co-op flow | reliable, host to each guest | screen at 4 (unknown 0, cabin 1, briefing 2, flight check 3, in mission 4, debrief 5), mission sequence at 5, round at 6, the guest's player number at 7, Ready mask by player number at 8, humans at 9, host's campaign progress at 10, flags at 11 (bit 0 won), hangar airframe mask at 12, the host's own seats at 14, how many seats after its player number the guest was given at 15 (0 for one), objectives mask at 16, cash at 20 (24 bytes) |
| `0x51` | Co-op pick | reliable, guest to host | round at 4, flags at 5 (bit 0 Ready, bit 1 left the flight, bits 2 to 4 the pilot voice's place in the Voice list plus one, 0 for none, bits 5 and 6 the seat's place among its machine's own, bit 7 another seat's pick follows), airframe at 6, the picked plane at 7 (0 none, `0xFF` the stock Devastator, else its place in the host's hangar plus one), the fit at 8, the player name at 20 (16 bytes, zero-padded; 36 bytes) |
| `0x52` | Co-op seat fit | reliable, host to each guest | seat at 4, three reserved bytes, the fit at 8 (20 bytes) |
| `0x59` | Co-op wingman | reliable, host to each guest | wingman airframe at 4 (`0xFF` none), three reserved bytes, the fit at 8 (20 bytes) |
| `0x5A` | Co-op film | reliable, host to each guest | ordinal at 4, playing at 5, film at 6 (chapter 1, closing 2), chapter at 7 (8 bytes) |
| `0x5F` | Co-op hangar plane | reliable, host to each guest | the plane's place in the hangar at 4, the hangar's plane count at 5, the seat holding it at 6 (`0xFF` none), airframe at 7, flags at 8 (bit 0 a custom build), the stored fit at 9, the 26 build bytes and 16-byte build name as `0x5D`'s at 21, the plane's name in 33 bytes at 63 (96 bytes) |

A fit is twelve bytes: the four gun slots' ammunition, then the eight ordnance cells, one byte
each, each the profile's stored value plus one so that zero reads as unset (stock).

Every co-op seat picks its own plane from the host's hangar, and no two seats fly one plane, a
plane counting once by name as the flight check's "Pilot and Wingman must fly different planes."
compares. The stock Devastator is the exception: any number of seats fly it, and it holds no
hangar plane. The host settles the picks in seat order (its own seat, its splitscreen seats, then
each guest in player order): a seat keeps a free pick, and a seat whose pick an earlier seat holds,
or that has picked nothing, takes the first free plane, else the stock Devastator. A hangar plane
word is one plane of the host's hangar with its stored fit, build, name and the seat that holds
it. The host sends each guest every word before the flow, and again whenever one changes, and the
lobby keeps them by place, so every machine refuses the same held planes. A plain join with no
campaign board up sends none. A guest's roster is a copy of each word's plane, then a stock
Devastator. It opens on its remembered pick unless an earlier seat holds it, else on the plane
the host settled for it, and CHANGE PLANE and CHANGE AMMO stay on its check. A pick of a plane an
earlier seat holds is refused with a modal naming that seat. Two guests picking one plane at once
are settled by the host, and the later seat moves to what the next word gives it. The pick carries
the plane's place, and the host seats each guest on its settled plane with the pick's fit when the
guest flies the plane it picked, else the word's own fit. The flow's hangar airframe mask stays on
the wire but a guest does not read it. The wingman is held only on a mission that flies one, after
every human: its saved plane while no seat holds it, else the first free plane, else the stock
Devastator at rest, which the host names in the launch's co-op wingman. Allow Custom Planes and
Outlaw Components are Dogfight rules and do not apply here. At its launch the host sends every
guest a seat fit for each seat, its own seats included, before the session's opener on the same
ordered channel. Every machine then builds each seat flown elsewhere with its own pilot's fit.
After the seat fits and before the opener the host also sends each guest a co-op wingman naming
its profile's wingman airframe and fit. A guest's director has no profile of its own, so it binds
the campaign's `wingman_1` from that word, and both machines build the host-owned wingman in the
same def with the same damage parts. A guest with no word flies the block's own def and says so in
a warning; it never substitutes its own default. `0xFF` names no pick, which also flies the block's
def. A guest a patch older drops `0x59` as unknown and flies the old default.
A co-op film names the campaign film the host's board has up: a chapter's opening film before the
cabin, or the closing film before the book. The host sends it playing when the film starts and
again with the same ordinal and playing clear when it ends, played out or skipped, and it goes
out at once rather than on the next flow, so a guest reads the end before the board that follows.
A guest plays the film it names and ends it on the host's end. A guest's own skip ends only its
own film, since the host drives the boards. A guest joining while a film plays sees none, and a
guest back from flight joins a film still playing. A launch ends a guest's film still up, so the
guest flies with its host. A guest a patch older drops `0x5A` as unknown and plays no film.
The name is the guest's callsign from Player Information, else its last-played pilot read without
writing, and the host's roster calls the guest by it; a guest with neither is called by its player
number. The host's own first seat takes the host's callsign the same way. A guest learns every
other seat's callsign from the Dogfight roster `0x54`, which the host also sends each co-op guest
before its flow and again whenever a name changes: one row per human in player order, carrying the
name alone, the host's row marked and the reading guest's first seat as its own row, with no Ready,
no airframe and round 0, since the flow carries the Ready marks. A build a patch older keeps the
list unread, since its co-op guest stands no Dogfight lobby, and a guest of a host that sends none
calls those seats by their player tags. A guest leaving the flight
through its pause sheet sets the left flag, and the host takes its seat out at once, as it does
for a dropped link.

The host sends a flow to each guest whenever its boards change, since the player number differs
per guest. The round advances when the mission changes, and on any screen change other than
between the briefing and the flight check, and it clears every Ready. A pick counts only under the
host's current round, so a Ready given before the host backed out never launches the next mission.
The host's launch waits until every guest seat's latest pick is Ready. While the host flies, the flow
says in mission and the advert's status is in mission, so a guest joining then waits in the cabin.
The debrief flow carries the host's result, which every guest's scrapbook shows.

A guest with several players at its machine sends one pick per player, each with its place among
the machine's own and the mark that another follows, so a one-seat guest's pick is the byte it
always was. The host seats a machine's players side by side from its first seat, and each counts
against the four humans: a guest is admitted on one seat while one is free, and its further players
are given seats in arrival order only from what every seated guest's first seat leaves, so a pad
plugged in late never unseats a player already flying. The flow's byte 15 tells the guest how many
it got, and a player left out reads "The game is full" on its band until a seat frees. Ready is per
player: the guest's check walks its players one at a time, its READY marking the one showing, and
the host's launch waits on every mark. The handshake then names the guest's whole run, so every
machine builds each of those seats as that guest's, and the guest flies one pane per seat.

### Dogfight lobby

The Multiplayer Lobby runs over five more lobby messages, minted at `0x53` to `0x55`, `0x5D` and `0x5E`, and the two of "Lobby teams" below, with a
guest's plane and Ready riding the co-op pick at `0x51` under the lobby's own round. None reaches a
session.

| Id | Message | Class | Carries |
|---|---|---|---|
| `0x53` | Dogfight options | reliable, host to each guest | round at 4, environment at 5, mission type at 6 (Capture the Flag 0, Deathmatch 1, Zeppelin vs Zeppelin 2, the remake's Stunt Race 3), flags at 7 (bit 0 Score rather than Time, bit 1 Limited Lives, bit 2 Auto Respawn, bit 3 both Time and Score, bit 4 Restrict Number of Teams, bit 5 Capture the Flag's own flag home to capture), minutes at 8, lives at 9, score at 10, team minimum at 12, team maximum at 13, two reserved bytes (16 bytes) |
| `0x54` | Dogfight roster | reliable, host to each guest | round at 4, row count at 5, the reading guest's own row at 6, one reserved byte, then sixteen rows of 20 bytes: flags (bit 0 Ready, bit 1 host, bit 2 team captain), airframe, team number (0 for none), one reserved byte, the name in 16 bytes (328 bytes) |
| `0x55` | Lobby chat | reliable, guest to host and host to each guest | the speaker's name in 16 bytes at 4, the line in 84 bytes at 20 (104 bytes) |
| `0x5D` | Plane build | reliable, guest to host (its pick, seat `0xFF`) and host to each guest (every seat, at launch) | seat at 4, flags at 5 (bit 0 custom), then 26 bytes: airframe, engine, four armour presses (nose, tail, left, right), left and right hardpoints, four gun calibres (5 empty), twin mask, paint pattern, three colours, three shades, three decals, three spare; the name in 16 bytes (48 bytes) |
| `0x5E` | Plane rules | reliable, host to each guest | round at 4, flags at 5 (bit 0 Allow Custom Planes, bit 1 Outlaw Components), the outlaw list in the original's five-byte packing at 6, one reserved byte (12 bytes) |

Any option change advances the round and clears every Ready, the host's own included, so a player
is never launched on options it did not see. A guest's changed pick clears that guest's own Ready,
and the pick drops any session payload the host still holds from that guest's last match. The
launch waits until every row is Ready. A guest's
chat line goes to the host, which adds it to its own list and relays it to every other guest under
the name the guest's pick gave, so each end shows the line once. The advert's mission sequence
carries the environment index for a Dogfight, which is what the games list reads.

**Stunt Race, the remake's fourth type.** The original's Type box lists three types; the remake
appends Stunt Race as mission type 3 in the same byte, so the message keeps its 16 bytes and its id.
A launch under it flies the chapter's `IA1` course with only the Time box carried, as the race
window. No wire version guards the value: two builds play together only on the same MAJOR.MINOR
(`BuildVersionMessage`), so a build that knows type 3 never shares a lobby with one that does not,
provided the type ships in a minor release. A build without it, given type 3, would name no type,
describe the lobby as Zeppelin vs Zeppelin and fly a Deathmatch on `MP1` while its host races, so
type 3 must not reach a patch release of a minor that lacks it.

A custom plane crosses whole, where the original's player data leaves out the engine, the armour
and the hardpoints. The shooter decides a hit here, so every copy of a plane needs its owner's hit
volumes and armour. A guest sends its pick's build before the pick; at launch the host sends every
seat's build and fit before the session's opener, on the same ordered channel, so a guest has built
every custom plane before it reports loaded. The plane rules change the round as an option does.
The original's Ready check runs on each end for its own pick, and the host also checks every guest's
pick against its rules before counting it Ready, which the original does not. An outlawed
ammunition or rocket refuses Ready once and resets the fit, as the original does; the hardpoint
counts are kept, since the pylons stay empty either way. A co-op launch sends the host's own custom
planes, and its guests fly stock.

### Lobby teams

[Evidence: decoded] **The actions.** Every team action rides `0x1a` with a subtype word at `+4`,
sent guaranteed to everybody; only the host acts on it, in `FUN_00415b20`:

| Subtype | Builder | Bytes | Payload | Host action |
|---|---|---|---|---|
| 4 create | `FUN_004143c0` | `0x38` | creator id at `+0x10`, name at `+0x18` | `FUN_0046f250` mints the number, `FUN_00413c30` makes the team, the creator joins |
| 1 join | `FUN_00414340` | `0xc` | team number at `+8` | `FUN_00413db0`: player `+8` takes the team, member list `+0x30` grows, lobby notice 4 (10503) |
| 2 leave | `FUN_004142c0` | `0xc` | team number at `+8` | `FUN_00413e50` |
| 8 disband | `FUN_00413f70` | `0xc` | team number at `+8` | a subtype 2 for every member, then `FUN_00413ef0` |

After any of them the host sets the lobby's dirty bytes (`[0x64e724]+4`, `+5`), which resend `0x26`
and the `0x27` team roster. `FUN_0046f250` mints the lowest number no team holds, counting from 1.
The screen dispatcher `FUN_00407670` takes 1007 (create), which copies 16 characters of the name to
`0x64316e` and calls `FUN_00413400`; a blank name there becomes "Default team name" (`0x61f614`).
1008 is join (`FUN_004133e0`), and 1009 leave (`FUN_00413390`), which sends disband instead when the
player record's byte `+0xd`, the captain flag, is set. Boot sends a disband for every team the
booted player holds (`FUN_00413090`). In flight a pilot carries its team number
(`FUN_0046f3c0(pilot+8)+0x18`, remote record `+0x3c`).

[Evidence: script] **The screen.** `MULTIPLAYERLOBBY_READY.SCRIPT`'s team button (at 105, 325)
reads Create Team (10056) when its pilot is on no team and no team row is picked, Join Team (10057)
with a team row picked, and Leave Team (10058) on a team. It is live only while its pilot is not
Ready. The player list has team rows (kind 1 from callback 5002) and player rows. Create Team runs
`MULTIPLAYERTEAMMODAL.SCRIPT`: `mp_createteambackground.png` at 234, 99, the title 10552, one name
box (label 10551, `FD` 12 characters) with OK and Cancel. OK refuses a blank name with 10512 through
`$$A$$` 1042. `MULTIPLAYERLOBBY_MISSION.SCRIPT`'s LAUNCH! refuses 10519 while Restrict Number of
Teams is ticked with fewer teams than the minimum box, and 10518 with more than the maximum, before
it calls `$$A$$` 1013. The minimum box opens at 2 and the maximum at 4 (`SBA.XF`, `TBA.XF`, read as
the opening values), a Deathmatch ranges them over 0 to 16, and each bounds the other. Capture the
Flag and Zeppelin vs Zeppelin tick Restrict and hold both boxes at 2.

[Evidence: weak negative] Langui 10520 ("There are not enough players in the game.") and 10547
("You are not on a team.") have no reference in the scripts or as an immediate in the executable.

**The remake.** `Net/NetTeams.cs` is the host's team book and every team mode's launch check. A
guest's action goes to the host alone as `0x61`; the host acts on it against its own book and sends
the outcome in the player list's team byte and the team list `0x62`, never in an answer to the
guest. A captain's Leave Team disbands its team, and a captain who leaves the game or is booted
takes its team with it. Each action posts the original's notices 10503 to 10505 as a chat line under
no name. The launch refusals, in order:

1. Restrict Number of Teams ticked with fewer teams than the minimum: 10519. With more than the
   maximum: 10518. With no team standing the match is a free-for-all, and only this check applies.
2. A team match with fewer than two players: 10520.
3. A team match with one team: 10519.
4. A player on no team in a team match: the remake's own line.
5. Two teams whose sizes differ by more than one player: the remake's own line. A machine's
   splitscreen seats fly on its team and count as players.

| Id | Message | Class | Carries |
|---|---|---|---|
| `0x61` | Team action | reliable, guest to host | the action at 4 (the original's subtypes: join 1, leave 2, create 4), the team number at 5, two reserved bytes, the new team's name in 39 bytes UTF-8 at 8 (47 bytes) |
| `0x62` | Team list | reliable, host to each guest | the team count at 4, three reserved bytes, then sixteen entries of 40 bytes: the team number and its name in 39 bytes (648 bytes) |

A Capture the Flag match adds a sixth refusal: a team numbered above 2, which would fly with no
flag, under 10519.

### Capture the Flag

The original's flag ask `0x1c` and flag table `0x1d` (above, and
[`multiplayer-ctf.md`](multiplayer-ctf.md)) are minted anew, naming seats rather than player ids.
Their ids stay the original's objective messages.

| Id | Message | Class | Carries |
|---|---|---|---|
| `0x63` | Flag ask | reliable, guest to host; an eject also host to every guest | the flag's team number at 4, the ask at 5 (take 1, home 2, eject 3), the asking seat at 6, one reserved byte (8 bytes). Eject is the console's `ejectflag`, which the host relays as the drop every machine then runs |
| `0x64` | Flag table | reliable, host to every guest | the flag count at 4, three reserved bytes, then four rows of 4 bytes: the team number, the state (1 held, 2 at home, 3 floating) and the holding seat (`0xFF` none), one reserved byte (24 bytes) |

Zeppelin vs Zeppelin's return is a point rather than a table entry (`FUN_004969b0`'s mode 4 branch,
[`multiplayer-zvz.md`](multiplayer-zvz.md)), so the host's answer to `0x44` there is minted anew
beside `0x41`. The original's `0x1e` hull state is the remake's `0x4B` path and `0x48`'s pool
events, and its `0x1f` and `0x20` part deaths ride the pool events as every destructible's do.

| Id | Message | Class | Carries |
|---|---|---|---|
| `0x65` | Spawn at | reliable, host to all | the seat at 4, three reserved bytes, the position as three floats at 8, the heading in degrees at 20 (24 bytes) |

### LAN discovery

The games list finds hosts on the local network through `Net/LanDiscovery.cs`, a datagram pair on
UDP port 47501, the game port plus one. It is outside the carrier: no peer is connected, and no
message id is spent.

| Offset | Query | Reply |
|---|---|---|
| 0 | `CSLD` | `CSLD` |
| 4 | version, 2 | version, 2 |
| 5 | kind, 1 | kind, 2 |
| 6 | two reserved bytes | two reserved bytes |
| 8 | the asker's token, 32-bit little endian | the token it answers |
| 12 | 36 zero bytes | game port, 16-bit little endian, then two reserved bytes |
| 16 | | the host's build version |
| 20 | | the host's 28-byte session advert, header included |

Both are 48 bytes. The query is padded to the reply's width so a reply is never larger than the
query that asked for it, which keeps a responder from amplifying a forged-source flood. A responder
answers only a datagram that is exactly 48 bytes with the magic, this version and the query kind,
and answers it to the address it came from. A search sends one query a round with a fresh token
and keeps only replies carrying it. The round sends that query to the limited broadcast
255.255.255.255 and to the directed broadcast (address with every host bit set) of each IPv4
network the machine sits on, once per address. Windows sends a limited broadcast out of one adapter
only, so on a machine with several adapters it can miss the host's network, while a directed
broadcast is routed out of the adapter on that network. The responder binds every interface, so a
directed broadcast on any of the host's networks reaches it. A game is listed at the reply's source address and the
port the reply names, and it leaves the list after two rounds without a reply. A game whose build
version does not play with the searcher's is listed in grey with its version in the Status
column, and Join Game refuses it with both versions named before any socket opens. The discovery
version counts layouts, not builds, so it does not change with the build version.
