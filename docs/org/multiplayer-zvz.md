# Zeppelin vs Zeppelin, decoded from `crimson.exe`

How the original's Zeppelin vs Zeppelin runs its two hulls. It is match mode 4
([`multiplayer-spawn.md`](multiplayer-spawn.md), "Which mode uses teams"), lobby type 2, scored by
mode 4's table `FUN_0046ee20` ([`multiplayer-scoring.md`](multiplayer-scoring.md)), over the lobby
teams of [`multiplayer-messages.md`](multiplayer-messages.md) "Lobby teams". The map side is every
chapter's `MP3` mission: its `zeppelins.zrd` lays out `multiplayer1zep` and `multiplayer2zep`, five
400-point gas bags each with `num_healthy_required` 3, six 200-point broadside cannons bound to the
first three bags, and a loop net (`ZVZ1a`, `ZVZ2a`). Its `net.zrd` holds 48 entries, block 1 round
`multiplayer1zep` and block 2 round `multiplayer2zep`. The mission scripts carry no rules.

Every claim names the address it was read at. Evidence tags as elsewhere: decoded (read in the
code), data (extracted files), inferred (not checked in code).

## Function map

| Address | Role |
|---|---|
| `FUN_00413430` | At launch, records the first two teams in lobby order |
| `FUN_00496490` | Puts each zeppelin on its side, marks it friend or foe and engages its broadside |
| `FUN_0049b4c0` | Relabels a hull's target entry: its team's name, "Defend" or "Destroy" |
| `FUN_004c0680` region | A gas bag's damage; its death sends `0x1f` and scores |
| `FUN_004c0900` region | A broadside cannon's damage; its death sends `0x20` and scores its bag |
| `FUN_0049b740` | A gas bag's scoring event: the voice line, then the score on the host |
| `FUN_0049adf0` | The host's zeppelin tick: the `0x1e` hull state, and the lost hull's end |
| `FUN_0046ee20` | Mode 4's score table |
| `FUN_00498bf0` | The `0x12` death handler; its cause 3 is a pilot killed by a zeppelin |
| `FUN_004969b0` | The placement; its mode 4 branch is the return by the hull |
| `FUN_00495c40` | Loads the zeppelin and match voice lines |
| `FUN_00495310` | The net match start: the handlers, the mode setups, the ended state cleared |
| `FUN_00496d40` | After the end's wait: the net game torn down and the lobby opened |
| `FUN_00472c40` | The world's free, every zeppelin record deleted among it |
| `FUN_004bd110` | Reads `zeppelins.zrd` into new zeppelin records |

## Sides

[Evidence: decoded] `FUN_00413430` writes the first lobby row's team (row array `0x645390`, stride
`0x20`, team at `+0x10`) to `0x71c7b4` at `0x41343f`, then walks the rows for the first team that
differs and writes it to `0x71c7b8` at `0x413466`. A first row on no team is taken as it is.

[Evidence: decoded] `FUN_00496490` gives the zeppelin at list index `i` the team at
`0x71c7b4 + 4i` (`0x4964e5`), so the first record, `multiplayer1zep`, flies the first team and
`multiplayer2zep` the second. It marks each friend (`FUN_004830c0`, team 1) when that team is the
local pilot's and foe (`FUN_0045c260(0)`, team 2) otherwise. `FUN_0049bef0` keeps that value at the
zeppelin's `+0xe0` and `FUN_004bf030` hands it to every entity and turret under the hull's node
(`FUN_004bee80`).

[Evidence: decoded] `FUN_0049b4c0(node, team)` then relabels the hull's entry in the target list,
found by its node (`FUN_004a2850`). Its name `+0x10` becomes the team's name. Its category becomes
8001 "Defend" when the entry's team matches the local aircraft's or either is 0, and 8002 "Destroy"
otherwise (`0x49b50c`..`0x49b6d0`). The map's `targets.zrd` labels `multiplayer1zep`
`MSG_TRGT_ZEP_ENEMY` with "Destroy" and `multiplayer2zep` `MSG_TRGT_ZEP_FRIEND` with "Defend" on
every machine; the type line "Zeppelin" stays. So a pilot's own hull is blue and the other red
([`targeting.md`](targeting.md), "Friend or foe").

## Broadsides

[Evidence: decoded] The same loop of `FUN_00496490` walks the zeppelin roster's pointer vector
(`0x71df84` to `0x71df88`, the list object at `0x71df80`) and, for every record, writes 1 to the
engage byte `+0xc` (`mov eax, 1` at `0x4965e2`, `mov byte ptr [esi+0xc], al` at `0x4965e9`). That
is the byte `COMPLETED_ZEPCANNONS` writes through `FUN_0046a0b0`, which looks the record up in the
same `0x71df80` list and stores its flag at `+0xc` (`0x46a0c9`). So in Zeppelin vs Zeppelin both
hulls fire their broadsides with no script: the per-frame pass reads that byte
([`../formats/mission-entities.md`](../formats/mission-entities.md), "Broadside firing").
[Evidence: data] Every `MP3` `zeppelins.zrd` gives each hull six cannons, `targets` the other hull,
`cannon_fire_delay` 15 s and `cannon_fire_range` 15000. The zeppelin-vs-zeppelin arm aims at the
other hull's live gas bags; a round's own `RANGE` is 3500 m (`wep_28`).

### ⚠ "No hull fires, since no `MP3` authors `COMPLETED_ZEPCANNONS`" — RETIRED (2026-09-29)

This page read the directive's absence as both broadsides staying off in every shipped match, and
so called event 9 unreachable. The side setup engages them itself, above. Do not read a
`COMPLETED_ZEPCANNONS` census as the whole of the engage flag's writers again.

## Scoring

[Evidence: decoded] A gas bag's record keeps two bytes: `+0x10`, its death sent, and `+0x11`, its
score spent. At zero health (`0x4c06c5`) the bag sends `0x1f` once and, unless `+0x11` is set, calls
`FUN_0049b740(attacker, zeppelin)` and sets it (`0x4c074a`). The attacker is the one hit that took
the part to zero (`FUN_005ad440`), as for an aircraft ([`multiplayer-scoring.md`](multiplayer-scoring.md)).

[Evidence: decoded] A broadside cannon at zero (`0x4c0915`) sends `0x20` and then, unless its
bound bag's `+0x11` is set, calls the same `FUN_0049b740` with the bag's zeppelin and sets the bag's
`+0x11` (`0x4c0984`..`0x4c099f`). So a cannon kill scores its bound gas bag, and the bag's own death
later scores nothing.

[Evidence: decoded] `FUN_0049b740` compares the zeppelin's team with the local pilot's team slot
`+0x3c`. On a match it plays `snd_Zep_GBlost` (`0x71d204`), otherwise `snd_Zep_GBdest`
(`0x71d1f8`), on every machine that runs it. Then on the host alone (`FUN_005b4210`) it scores the
attacker's record: event 8 (`score_my_gas_kill`, -10) when the zeppelin's team is the attacker's,
else event 7 (`score_gas_kill`, +10), and runs the limit check `FUN_00499270`. Neither key is in the
shipped `player.zrd`, so both are the executable's fallbacks.

[Evidence: decoded] `FUN_0049adf0`, the host's tick, walks the zeppelins by ordinal from 1. For one
whose dead byte `+6` is set while the match runs (`0x71c190` clear), it raises event 3 with that
ordinal, ends the match with reason 3 and runs `FUN_00499270` (`0x49afb4`..`0x49afcf`). Event 3
adds `score_zep` to the `+0x14` term of every team whose `+0x18` is not the lost hull's
(`0x46eeb3`..`0x46eed8`). [Evidence: data] The shipped `player.zrd` authors `score_zep` 10; the
executable's fallback is 100 ([`multiplayer-scoring.md`](multiplayer-scoring.md)).

[Evidence: decoded] The dying client reports cause 3 when its killer resolves to a zeppelin: in
`FUN_00498a90`, `FUN_0049b4a0` maps the killer to its node and `FUN_0049b440` finds the roster
record with that node at `+0x1c`, whose team `+0` goes in the report's killer field. The handler's
cause 3 finds that team (`FUN_0046e0f0`, `0x498dc2`), posts row 7064 "Killed by %1 Zeppelin" with that team's
name, and raises event 9 with the team's `+0x18` (`0x498e1c`); it charges the victim nothing. The
dispatcher `FUN_0046ecd0` runs on the host alone (`FUN_005b4210`). Event 9 **sets** that team's
`+0x14` to `score_zep_kill` (`0x46eea7`..`0x46eead`), the only event of the table that assigns
rather than adds. [Evidence: data] `player.zrd` does not author `score_zep_kill`, so the fallback 1
(`0x47411b`) is in play. The term is 0 until a hull is lost, so the first pilot a team's zeppelin
kills gives that team 1, and later ones change nothing.

## The end

[Evidence: decoded] `FUN_00499730`'s reason 3 arm (`0x4997c1`) builds row 135 "Game Over:" with row
7066 `MSG_MP_ZEP_DESTROYED` ("Zeppelin Destroyed") and plays `snd_Zep_dest` (`0x71d210`) on every
machine. `snd_Zep_lost` (`0x71d214`), `snd_MP_mis_Lost` (`0x71d218`) and `snd_MP_mis_Won`
(`0x71d21c`) are loaded by `FUN_00495c40` and read nowhere else. [Evidence: data] `sounds.zrd`
defines `snd_Zep_lost` (`VO_id47_ZZ-Zep-Lost.wav`) but neither `snd_MP_mis_Won` nor
`snd_MP_mis_Lost`, and the sound archives hold no such recording.

## The next match

[Evidence: decoded] The original has no rematch. The net tick `FUN_00496d60`, on every machine,
once the ended state `0x71c190` is set, waits until the clock passes `0x71c19c` (the five-second
wait of [`multiplayer-scoring.md`](multiplayer-scoring.md) "How a match ends"), sets the state to 5
(`0x496e9c`..`0x496ebe`) and jumps to `FUN_00496d40` (`0x496ecf`). The `afteraction` command
(`FUN_0043d640`, `0x43ef9e`) reaches the same function. It unregisters the net handlers (`FUN_004966c0`) and opens the lobby
(`FUN_00419750` writes 10 to `0x64b348` and pushes `0x71d6b4`, the lobby object `FUN_00413430`
reads the teams from), so every
machine lands back in the lobby at the end.

[Evidence: decoded] Every launch from the lobby then loads the mission whole. The load
(`FUN_0046b8c0` and `FUN_0046b950`) first runs `FUN_00464970`, whose `FUN_00472c40` (`0x464a37`)
frees the world: it clears the zeppelin list `0x71df80` through `FUN_004bd300` (`0x472c72`), which
destructs and deletes every record. It then runs `FUN_00464680`, which reads `zeppelins.zrd` again
through `FUN_004bd110` (`0x46489c`), each record a new `0xfc`-byte object (`0x4bd16d`). The net
match start `FUN_00495310` runs the side setup `FUN_00496490` in mode 4 (`0x495733`) and clears the
ended state (`0x495905`). So a second match on the same map flies two new hulls, every gas bag and
cannon whole; no zeppelin record outlives its match.

## The return

[Evidence: decoded] `FUN_004969b0`, past the opening placement and with two or more pilots, sums the
x and z of every other pilot's aircraft over `n` of them (`0x496a3c`..`0x496a5b`). In mode 4 with
the pilot's team zeppelin found (`FUN_0049b470`) it adds `n` times the hull's x and z and doubles
the divisor (`0x496a79`..`0x496ab8`): the point halfway between the others' centroid and the hull.
It adds `cos` and `sin` of `pilotIndex × 0.7854` (`0x608168`) times `respawn_rad`, takes the height
of the ground there, adds `respawn_el` above 900 m (`0x603730`) and stands at a flat 900 m
otherwise, and faces the yaw `1.5708` (`0x608160`) less the bearing (`0x496b7f`).

[Evidence: decoded] `respawn_rad` and `respawn_el` are `player.zrd` keys read by `FUN_004735b0` into
`0x628f08` and `0x628f0c` (`0x473f50`..`0x473f86`). [Evidence: data] The shipped `player.zrd`
authors 1200 and 100.

[Evidence: decoded] A rearm is offered only at the pilot's own team's node (`zep_rearm_node_1` for
the team at `0x71c7b4`, `_2` for the one at `0x71c7b8`, the `MP3` targets' `other_target` entries)
and only while that hull lives: the node lookup `FUN_0049b920` answers nothing in mode 4 once the
team's zeppelin has its dead byte `+6` set (`0x49b92e`..`0x49b94b`). Each base's marker reads its
team's name over "Rearm". The whole rearm is [`multiplayer-rearm.md`](multiplayer-rearm.md).

## What the remake takes

`Flight/Modes/ZeppelinVersus.cs` holds the sides, the gas bag rule and the return engine-free, and
`Session/World/ZeppelinVersusRuntime.cs` runs them over `ZeppelinRuntime` and the host-owned world
link. The sides are the first two lobby teams in seat order, a seat on no team skipped. Each side
opens in the `net.zrd` block round its own hull, block 1 or 2 by side rather than by team number,
so two teams numbered 2 and 3 still open by their hulls. A dead part's shooter is the pool's
`LastShooter`, and a hull loss ends the match with `NetMatchEnd.Objective`, the `0x17` state
carrying the winning team. Every value is the match's `MatchScores`, read from `player.zrd`, so a
lost hull's `score_zep` is 10. It goes to every other team's term on the board's total and never to
the Score limit, and the winner is the side whose hull survived, both maintainer decisions. The end
posts the original's two lines. The return is the decoded point, computed on the host and sent as
`SpawnAtMessage`. The next match is the original's: the results board's Restart takes each
machine, host and guest alike, to the lobby (`GameSession.RestartMatch`), whose next launch builds
every machine's session and world afresh, both hulls whole. It never reruns in place, which would
restore no world pool. A launch outside the lobby has no next match. Each hull's marker is
relabelled per pane rather than per machine (`ZeppelinVersus.HullSide`), so splitscreen panes on
two sides each read their own hull as "Defend". The rearm is taken at each hull's own node while
the hull lives (`Session/World/RearmRuntime.cs`).

Both broadsides are engaged on every machine as the side setup engages them
(`ZeppelinRuntime.SetCannonsEngaged`), and each round carries its hull's shooter id
(`ZeppelinVersus.BroadsideShooter`, below `ProjectilePool.NoShooter`). Every machine fires its own
copy of the volleys, and only the host's spend anything, as the host-owned world has it: a guest's
world pools spend nothing, and a round no seat fired is decided on the host. A hit the host sends
names the hull in `HitMessage`, so the victim's machine reports its death as cause 3 with the hull's
placement index. The host then runs event 9 (`VersusMatch.RegisterZeppelinKill`): the victim takes
a death and no score, and the side's term is set to `score_zep_kill`. The death notice `0x5C`
carries that side's team, so every guest sets the same term and posts "Killed by %1 Zeppelin" with
the team's name. A broadside cannon a seat destroys scores its bound gas bag, as above; a gas bag a
hull's round destroys is spent and scores nobody, since no seat fired it.

**Remake choice, the end's sounds.** The original plays `snd_Zep_dest` to every machine at reason 3,
and `snd_MP_mis_Won`/`snd_MP_mis_Lost` have no recording. The remake plays `snd_Zep_dest` to the
winning side and `snd_Zep_lost`, which the original loads and never plays, to the side whose hull
was lost.
