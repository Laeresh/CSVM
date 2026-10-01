# Multiplayer spawn, decoded from `crimson.exe`

Where the original puts a pilot in a network match: the opening placement off the mission's
`net.zrd` table, and the different rule a respawn takes. The table's own file format is
[`formats/net-spawns.md`](../formats/net-spawns.md); this page is what the executable does with it.
What the same match counts and how it ends is
[`multiplayer-scoring.md`](multiplayer-scoring.md), and every message it puts on the wire is
[`multiplayer-messages.md`](multiplayer-messages.md).

All of it lives in `remote.cpp` (the source path string at `00628f50`): `FUN_00495310` is the
session init and `FUN_004969b0` is the placement it ends with.

## The two placements are different rules

`FUN_004969b0(aircraft)` branches on the aircraft's byte at `+0x6f0`, which the session init sets
to 1 immediately before calling it and which the placement clears at `00496c04`. So the flag means
"this is the opening placement", consumed once per match.

| Condition | Position | Heading |
|---|---|---|
| flag set, **or** fewer than two pilots in the match | the `net.zrd` entry for the pilot's slot | the entry's own heading |
| flag clear and two or more pilots | the centroid of the other live aircraft, displaced on a bearing | that bearing's complement |

**The opening placement is the table.** The slot is the pilot's index, plus `team << 4` when the
match has teams, and the list is walked from ordinal 1 with the first entry as the fallback
(`00496bae`…`00496bd9`). The entry's heading goes in as yaw only, pitch and roll zero.

**A respawn does not read the table at all.** It sums the other live aircraft's `+0x204`/`+0x20c`
(x and z), divides by their count, and adds a displacement of `cos`/`sin` of
`pilotIndex × 0.7853982` (the quarter-turn constant at `00608168`) times a radius global. Altitude
comes from a terrain query (`FUN_004c76e0`): the sampled height plus a margin when it is above
900 m, and a flat 900 m when the query fails or the ground is lower (`00496b52`…`00496b75`).
⚠ The radius and the margin (`00628f08`, `00628f0c`) are written at runtime by two other sites
each, so their initialised values are not the values in play and are not recorded here.

The bearing steps by exactly 45 degrees per pilot index, and the per-pilot colour table at
`00628eb4`, indexed by the same slot in a match without teams, has exactly eight entries, so the
authored data serves eight pilots. ⚠ **That is not a player cap.** No coded bound exists: the pilot list is an STL list
(head `0071c150`, count `0071c154`, walked by `FUN_0046f110`) whose count is never compared against
a maximum. The only gate is DirectPlay's `dwMaxPlayers`, filled from the lobby's `nMaxPlayers`
(string `006192e0`, global `00642f08`), which the code only ever resets to 0. The shipped lobby
reads `Players (1 of 16)`, and the lobby's player and team arrays (`00645390`, `00645590`) hold 16
entries each.

## The opening spawn does not use PLAYER_INIT

Every other mode reaches the player placement `FUN_0047f740` through `FUN_0047f1f0`, which takes
the mission's throttle and speed from `objectives.zrd`'s `PLAYER_INIT`
([`formats/spawns.md`](../formats/spawns.md)). The multiplayer opening placement calls
`FUN_0047f740` itself with two immediates instead:

| Argument | Value | Meaning |
|---|---|---|
| speed | `0x41cd999a` (`00496be6`) | **25.7 m/s** (57 mph), negated onto the nose axis exactly as the story spawn's is |
| throttle | `0x3f59999a` (`00496be1`) | **0.85**, written to the lever at aircraft `+0x124` |

A multiplayer mission still ships a `PLAYER_INIT` record, and it is still applied on mission load;
the session init then re-places the pilot over it. So the record's spawn is visible in the data and
is not the pose a match starts from.

## Which mode uses teams

`FUN_004136e0` turns the lobby's game-type setting into the mode global at `0071c194`, and then
sets the team flag at `0071d89c` to "the mode is not 1":

| Lobby setting | Mode | Teams |
|---|---|---|
| 1, with no teams formed | 1 | no, the slot is the pilot index alone |
| 1, with teams formed | 2 | yes |
| 0 | 3 | yes |
| 2 | 4 | yes |

Mode 3 is Capture the Flag and mode 4 Zeppelin vs Zeppelin. Lobby setting 0 is the Type box's
first row, 10555 Capture the Flag (`0x413938` writes mode 3), and setting 2 its third, 10557
Zeppelin vs Zeppelin (`0x413916` writes mode 4). Every flag path gates on mode 3 (the proximity
tick at `0x496db1`, the death handler's drop at `0x498f9f`, the peer-left drop at `0x4995c2`), and
mode 3's score table `FUN_0046ed90` is the one that honours `score_return_flag` and
`score_enemy_flag` ([`multiplayer-ctf.md`](multiplayer-ctf.md)). The respawn's extra mode 4 branch
therefore belongs to Zeppelin vs Zeppelin. Team ids are handed out from 1, which is what leaves
block 0 of the table to the un-teamed match.

## The per-pilot colour table

`00628eb4` holds eight dwords and then zeros from `00628ed4`. The stored bytes are `81 2d 2d 00`,
`2d 2d 81 00`, `2d 81 2d 00`, `81 81 2d 00`, `81 2d 64 00`, `66 81 2d 00`, `45 7c 81 00`,
`66 2d 81 00`. The table is indexed with no bound check at `00495893`
(`MOV ECX, dword ptr [EDX*0x4 + 0x628eb4]`) and again at `00497ae6`, by the slot at `+0x3c` of the
**remote-pilot record**, not of the aircraft: the `0x1090`-byte object `FUN_00499c90` constructs
(zeroing `+0x1060` at `00499cda`) and `FUN_00497990` links into the list headed at `0071c7a4`. That
slot is the pilot's own index (pilot record `+0x18`) in a match without teams and the team's index
(`FUN_0046f3c0`'s `+0x18`) when the team flag `0071d89c` is set (`0049584f`..`0049587c`), so a team
match colours by team. Each entry is stored to the record's `+0x1060`.

**The channel order is red, green, blue in stored byte order.** The one reader of `+0x1060` is
`FUN_004b3660` (`MOV EAX, dword ptr [ESI + 0x1060]` at `004b367c`), code Ghidra's analysis had left
undefined, which is why a search of the analysed listing found only the writers. It takes an
aircraft, finds that aircraft's record through `FUN_00499db0` (the record whose `+0x30` is it),
and builds a float triple from the dword: the low byte (`AND ECX, 0xff`) first, `AH` second and
bits 16 to 23 third, each times `1/255` (`0x3b808081` at `006081b8`). It hands the triple to
`FUN_004d14e0` (`zclass\Object3d`) for the record's `+0x24` node with an amount: `0.3`
(`0x3e99999a`, pushed at `004b372a`) normally, and while the aircraft's no-damage byte `+0x920` is
set a triangle wave of the clock global `0071c470` with a 2-second period (`fmod` by 2.0 at
`00607be0`, folded above 1.0 by `2 - x` at `006076b8`) times `0.7` (`006035b0`) plus `0.3`
(`006034ac`), so 0.3 to 1.0. `FUN_004d14e0` stores the triple at class data `+0x08`..`+0x10` and
the amount at `+0x14`; the `Object3d` draw `FUN_004d39c0` pushes those four through `FUN_0054e080`
into `00a06fa0`..`00a06fac`, and `FUN_00551d90` adds that triple to the light colour triples it
sums (its first float beside each light's `+0xa4`). The sum goes to `FUN_0058b140`, whose hardware
branch `FUN_0059e730` packs the first float into bits 16 to 23, the second into 8 to 15 and the
third into 0 to 7 of a `D3DCOLOR`, which `FUN_005a30c0` sets as render state `0x22` through the
device's `+0x58` slot. A `D3DCOLOR` keeps red in bits 16 to 23, so the low stored byte is red. The
set is dark red, blue, green, yellow, magenta, lime, teal and violet.

⚠ **No caller of `FUN_004b3660` was found.** No analysed instruction calls it and its address
(`60 36 4b 00`) appears nowhere in the image, so the tint may be dead code in the shipped build
and the colour never seen. That does not reopen the order, which the reader fixes either way; it
means the original shows no capture of these colours to judge them by, and where the remake shows
a seat colour (lobby, markers) is the remake's choice.

The original's pilot index is 1-based here, so its eighth pilot reads the first zero dword past the
table. `Net/NetSeats.cs` does not reproduce that: every seat gets a colour, seats 0 to 7 from the
eight dwords in the decoded order, and seats 8 to 15 from the channel-wise complement of seat minus
8, which is the remake's own, since the original has no colour past its table.

## What the remake takes

The remake takes the opening rule and the opening throttle and speed (`SpawnPicker.LoadSpawnList`
and `SpawnPicker.StartState`, over `SpawnPoints.LoadNetFreeForAll`), and in a team Dogfight the
team's block of the whole table (`SpawnPoints.TeamBlocks`), walked by the seat's place in its team
rather than by the original's pilot index. It does **not** take the respawn rule: a centroid displacement
puts a returning pilot next to the pack, which is the camping problem
`Flight/Modes/VersusSpawnRotation.cs` exists to solve, and that rotation stays as it is. Zeppelin vs
Zeppelin is the exception: its mode 4 branch pulls the return halfway toward the pilot's own hull,
and the remake takes it whole there, `respawn_rad` and `respawn_el` read from `player.zrd` (1200 and
100 shipped), as the maintainer decided ([`multiplayer-zvz.md`](multiplayer-zvz.md)). The stunt
race's abreast starting grid is selected only when a race exists and never touches a Dogfight:
four dogfighters 60 m apart on one heading is a head-on merge every round.

Over a wire it departs from the original a second time, in who picks. The original has each
client place its own pilot; here the host owns the rotation and a downed pilot asks for its
return, because two rotations diverge on the first death. Only the return crosses the wire: the
opening placement is the shared seed walking every peer onto the same entry, so a match start
sends no spawn event at all. The block holds sixteen entries and the match admits at most
sixteen pilots, so a full field still opens one seat per point and the 45-degree fan above has
nothing to wrap past. A list shorter than the field is answered by the rotation relaxing its
one-living-seat-per-point rule, never by computing a bearing.

## Retired and superseded readings

### ⚠ "Mode 4 is capture the flag" — RETIRED (2026-09-29)
 page read mode 4 as Capture the Flag and tied the `snd_CTF*` and flag score keys to it. The box's row order (10555 Capture the Flag first) and the mode 3 gates on every flag path decode other way round, above. Do not read the respawn's mode 4 branch as a flag rule again.
