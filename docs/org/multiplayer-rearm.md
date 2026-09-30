# Multiplayer rearm bases, decoded from `crimson.exe`

How the original's multiplayer rearm works. It is the only thing in the retail build that re-arms
an aircraft in flight ([`weaponFire.md`](weaponFire.md), "Nothing re-arms in flight outside the
multiplayer rearm base"). The mission types are the match modes of
[`multiplayer-spawn.md`](multiplayer-spawn.md): 1 Deathmatch, 2 team Deathmatch, 3 Capture the Flag,
4 Zeppelin vs Zeppelin ([`multiplayer-zvz.md`](multiplayer-zvz.md)).

Evidence tags as elsewhere: decoded (read in the code), script (an anim script), data (extracted
files), inferred (not checked in code).

## Function map

| Address | Role |
|---|---|
| `FUN_00495980` | Builds the rearm-node list at `0x71c7c4`, called at `0x495718` during the match setup |
| `FUN_0049b970` | The per-frame check of the local aircraft, called at `0x496dac` from `FUN_00496d60` |
| `FUN_0049b920` | A list record's position this frame, or none |
| `FUN_00480480` | The restore: health, armour, damage stages and loadout |
| `FUN_004735b0` | Reads `rearm_rad` from `player.zrd` |

## The list

[Evidence: decoded] `FUN_00495980` formats `rearm_node_%d` (`0x628f84`) for `i = 1, 2, ...`, or
`zep_rearm_node_%d` (`0x628f70`) in mode 4, looks the node up (`FUN_004d0280` at `0x4959f0`) and
stops at the first name the world lacks (`0x495a00`). Each hit becomes a record: `+0` the team,
`+4` the node, `+8` a position buffer. The team is `i` itself (`0x495a35`) outside mode 4, so
`rearm_node_n` belongs to lobby team `n`, as `cs_flag_n` does in Capture the Flag
([`multiplayer-ctf.md`](multiplayer-ctf.md)). In mode 4 it is the dword at `0x71c7b4 + 4(i - 1)`
(`0x495a1c`), the team zeppelin `i - 1` flies, so `zep_rearm_node_1` is the first side's base and
`zep_rearm_node_2` the second's. Mode 4 also relabels the base's target entry (`FUN_004a2850` at
`0x495a91`): the name line becomes the team's name (`FUN_0046e0f0`'s record `+8`) and the category
id `+0x48` becomes `0xc0`, whose text, row 192 "Rearm", goes into `+0x38` (`0x495b3e`..`0x495bee`).

[Evidence: decoded] `FUN_0049b920` answers the node's world translation (`FUN_004952a0`: node `+0x38`
then `+0x54`) outside mode 4. In mode 4 it finds the record's team's zeppelin (`FUN_0049b470`) and
answers nothing when there is none or its dead byte `+6` is set (`0x49b93a`..`0x49b94b`). Otherwise
it writes the node's world position into the record's `+8` (`FUN_004cf2c0`), so the base moves with
its hull.

[Evidence: data] Every chapter's gamez carries `rearm_node_1` and `rearm_node_2` except C1C and
C2B, and `zep_rearm_node_1` and `_2` in all eight. On C1, C1B, C2, C3 and C4 the two `rearm_node`
translations are identical, one base in `rabaset1`'s building with two nodes. C5 has no `rabaset1`
and its gamez stands the two nodes about 4.7 km apart, which is where its Capture the Flag map keeps
them, one base per team. Its `mp1.gw` moves `rearm_node_2` onto `rearm_node_1`
(`Object3DTranslate -9536 95 -3647`), so every Deathmatch map has one base with two nodes. `mp1.gw`
and `mp2.gw` leave the plain nodes on, `mp3.gw`, `ia1.gw` and every campaign mission switch them
off ([`../formats/interp.md`](../formats/interp.md)).

## The check

[Evidence: decoded] `FUN_00496d60` calls `FUN_0049b970` every multiplayer frame while the local
aircraft (`0x71c298`) lives. `FUN_0049b970` returns at once on the aircraft's dead byte `+0x91d`
(`0x49b981`). It then branches on the mode `0x71c194` (`0x49b98c`):

- Modes 1 and 2, both Deathmatch types: every record serves the pilot (`0x49bad2`..`0x49bbe7`). A
  team Deathmatch is no different here.
- Modes 3 and 4: a record serves the pilot only when its team equals the local pilot record's team
  `+0x3c` (`[0x71c7ac]`, compared at `0x49b9d3`). A record that does not serve it is skipped
  without touching the latch.

[Evidence: decoded] For each record with a position it takes the squared distance
(`FUN_00538880`, a plain `dx² + dy² + dz²`) from the aircraft (`+0x204` in modes 1 and 2, `+0x240`
in 3 and 4) and compares it with `0x628f10` (`fcomp` at `0x49b9f4`, `0x49bb13`; `test ah, 0x41`, so
a distance equal to the radius is inside). Inside, and with the latch byte `0x71d23c` clear, it
rearms and sets the latch (`0x49baaf`). Outside, it clears the latch (`0x49bab7`). Nothing else
writes `0x71d23c`.

[Evidence: decoded] `0x628f10` is initialised to 625.0 in `.data`, 25 m squared. `FUN_004735b0`
writes `rearm_rad` squared there only when `player.zrd` has the key (`0x473f8c`..`0x473fa8`), and
the console's `rearmrad` writes it at `0x43f356`. [Evidence: data] The shipped `player.zrd` has no
`rearm_rad`, so the radius in play is 25 m.

[Evidence: decoded] One latch serves every record, so two records that both serve the pilot would
clear each other's latch whenever the aircraft were inside one and outside the other, and the rearm
would repeat every frame. [Evidence: data] No shipped map lays that out: in a Deathmatch the two
nodes share one position and are inside together, and in Capture the Flag and Zeppelin vs Zeppelin
only one record serves a pilot. The rearm is therefore once per entry on every shipped map.

## The restore

[Evidence: decoded] `FUN_0049b970` saves the selected gun group `+0x604` and pylon `+0x608`, calls
`FUN_00480480`, puts both back and re-selects them (`FUN_004b20d0`). On a living aircraft
`FUN_00480480` plays `player_destruction_reset` on it (`0x48051f`), restores health from the stored
maximum `+0x2c4` (`FUN_004b80a0`) and armour from `+0x2cc` (`FUN_004b8180`), and re-applies the
loadout through `FUN_00472e70`, which reaches `FUN_004b24d0` in multiplayer and fills every gun group
and pylon to its def's full count (`FUN_004b2550(.., -1, ..)`, [`weaponFire.md`](weaponFire.md)).
Fuel and nitro are not touched. It then posts row 7077, `MSG_MP_REARMED` "Rearmed!", for five
seconds (`0x40a00000`) in the `DAT_006eba60` colour through `FUN_004588e0`
(`0x49ba94`..`0x49baa7`, `0x49bbb4`..`0x49bbc7`). No message is sent: every machine checks only its
own aircraft.

[Evidence: script] The base's door is data: `rabase.zrd` gives `rearm_node_1` a looping `call_door`
that runs `rabaset1`'s `rearm_door_close` whenever a player is within 25 m (`PLAYER_RANGE 25`, 625
compiled). The door `rabdr` rises 27.5 over 2 s and comes back down 15 s later. It is not tied to
the rearm in the code.

## What the remake takes

`Flight/Modes/RearmBases.cs` holds the rule, the radius and each seat's latch engine-free, and
`Session/World/RearmRuntime.cs` lists the bases and runs them in any Dogfight, split screen or on
the wire. Each machine checks only the seats it flies, and `FlightController.Rearm` restores parts,
damage stages and every slot, keeping the selected pylon. The restored seat's owner sends its full
hull in the existing `0x40` damage report, and every other machine takes a full hull as the stages
coming off again ([`multiplayer-messages.md`](multiplayer-messages.md)). Zeppelin vs Zeppelin reads
the hull's side from `ZeppelinVersus` and offers a base only while its hull lives, and each base's
marker reads its team's name over "Rearm". A base the mission switched off offers nothing, which is
why every menu Deathmatch flies the chapter's `MP1` (`SessionSpec.DeathmatchMission`), as the
original's does ([`loading-screen.md`](loading-screen.md)): on `IA1` both plain nodes are off. The door
is the mission's own anim data, which the world runtime plays like any other range poll
([`../formats/anim-definitions.md`](../formats/anim-definitions.md)); the rearm adds nothing to it.

The latch is one per seat, since a split-screen machine flies several, and it is released only
outside every base that serves the seat. On the shipped maps that is the original's behaviour; on
a map laying two serving bases apart it would still rearm once per entry where the original repeats.
