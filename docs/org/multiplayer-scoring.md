# Multiplayer scoring and match end, decoded from `crimson.exe`

What a network match counts and how it stops. The placement side of the same match is
[`multiplayer-spawn.md`](multiplayer-spawn.md), and every message the match puts on the wire is
[`multiplayer-messages.md`](multiplayer-messages.md).

## One signed score per pilot

Every scoring event goes through `FUN_0046e1b0(pilotId, delta)`, which adds `delta` to the pilot
record's `+0x1c` and returns the new total. There is no separate kill counter in the match record:
the number the scoreboard ranks, and the number the kill target is compared against, is that one
signed running total, so a penalty moves a pilot **away** from winning.

The per-event amounts are config keys read by `FUN_004735b0`, each with a hard-coded fallback the
shipped data does not have to supply:

| Key | Global | Default | Shipped `player.zrd` |
|---|---|---|---|
| `score_suicide` | `0071c824` | **-1** (stored at `00473fc8`) | -2 |
| `score_kill` | `0071c850` | **+1** (stored at `00473ff2`) | 2 |
| `score_turret_kill` | `0071c7b0` | +1 | not authored |
| `score_zep` | `0071c808` | +100 | 10 |
| `score_zep_kill` | `0071d1d8` | +1 | not authored |
| `score_gas_kill` | `0071c84c` | +10 | not authored |
| `score_my_gas_kill` | `0071c7ec` | **-10** | not authored |
| `score_return_flag` | `0071c7fc` | +1 | 8 |
| `score_enemy_flag` | `0071c804` | +5 | 10 |

⚠ [Evidence: decoded] The keys are read from the loaded `player.zrd` (`FUN_004735b0` opens it at
`0x4739fa` and keeps it in `ebp`, the node every `score_*` lookup from `0x473fb0` searches), and
[Evidence: data] the shipped file authors the five values in the last column. So the original plays
a kill for 2, a suicide for -2, a lost hull for 10 and the two flag events for 8 and 10; the
defaults are only fallbacks. [Evidence: decoded] Each fallback is stored only on a missing key
(`0x473fd0` to `0x47411b`), and each integer is the value node's `+0xc`. The default Score limit
of 40 is therefore twenty kills in the original.

`FUN_0046ecd0` dispatches an event id to one of three per-mode tables on the mode field at
`+0x5c`. **Dogfight (modes 1 and 2) reaches `FUN_0046ed40`, which honours exactly three events**:
1 (`score_suicide`), 2 (`score_kill`) and 6 (`score_turret_kill`). The flag and zeppelin events
belong to the other two tables and cannot fire in a dogfight. Capture the Flag (mode 3) reaches
`FUN_0046ed90`, which honours those three and events 4 (`score_return_flag`) and 5
(`score_enemy_flag`); Zeppelin vs Zeppelin (mode 4) reaches `FUN_0046ee20`, events 3, 7, 8 and 9.
Where the flag events are raised is [`multiplayer-ctf.md`](multiplayer-ctf.md), and the zeppelin
events [`multiplayer-zvz.md`](multiplayer-zvz.md).

## Who the death is charged to

A death is reported by the dying pilot's own client, as message type `0x12` built by
`FUN_00498a90`, and handled by `FUN_00498bf0`. The packet carries a killer id at `+4` and a cause
at `+0xc`:

| Cause | Built when | Scored |
|---|---|---|
| 1 | the killer argument is non-zero | `score_kill` to the killer, **or `score_suicide` to the killer** when killer and victim share the team slot at **remote record `+0x3c`**, the `0x1090`-byte record `FUN_00499d80` looks up, not the pilot record |
| 2 | no killer at all | `score_suicide` to the pilot who died |
| 3 | a zeppelin | event 9 with the zeppelin's team (`0x498e1c`): that team's `+0x14` term is set to `score_zep_kill`; the victim is charged nothing |
| 4 | a turret owner | event 6, `score_turret_kill`, to the owner, or `score_suicide` to the owner when it shares the victim's team slot (`0x498e89`..`0x498ea9`) |

⚠ **There is no last-damager memory anywhere on this path.** The credited killer is the attacker of
the one damage event that took the hull to zero, read straight off the damage call's own argument
(`004ba1c6`…`004ba1de`, `killer = ((weapon[+0x10] == 0x10) - 1) & attacker`, so one damage class
reports as self-inflicted even though an attacker dealt it). The other reporter,
`FUN_0048b920`'s explode path, passes killer `0` unconditionally, and a one-shot latch at
`0071d1dd` means whichever fires first owns the death. So a pilot who is shot up, breaks off and
then flies into the ground scores a **suicide**, and the pilot who damaged them gets nothing.

## How a match ends

`FUN_004996d0(reason)` broadcasts the end (message `0x17`) and `FUN_00499730` turns the reason into
the end-screen line:

| Reason | Raised by | Meaning |
|---|---|---|
| 1 | `00496e28`, the network tick, when the remaining-time query `FUN_0046c580` drops below 1.0 | the clock ran out |
| 2 | `00499343` / `004993ac`, inside the score broadcast, when a pilot's `+0x1c` reaches the target | somebody hit the score target |
| 3 | `0049afc7` | a mode-specific objective, a Zeppelin vs Zeppelin hull lost |
| 4 | `0049900f` / `004996a2`, after a death or a drop, when `FUN_004999f0` finds no two live pilots on different sides | nobody left to fight |

⚠ **A time-out has no overtime, no sudden death and no tiebreak.** Reason 1 sets the ended flag,
prints its line and runs the five-second wait at `0071c19c`; nothing computes a winner, and two
pilots tied at the top are simply tied on the board. The one thing that follows a time-out is the
end screen.

## The two limits are exclusive

`FUN_004136e0` reads the lobby's limit kind from `00642f94` and arms exactly one of them: kind 0
writes the time limit to `0071c180` and sets the score-limit-off byte `0071c1a2`; kind 1 writes the
score target to `0071c17c` and sets the time-limit-off byte `0071c1a1`. Each check is gated on the
other's byte, so the original never runs a match that can end either way. The remake's lobby arms
Time, Score or both as the host chooses, and with both the first one reached ends the match. That
is a remake decision and not a reading of this code.

## Limited Lives

**The lobby's boxes.** [Evidence: decoded] `MULTIPLAYERLOBBY_MISSION.SCRIPT`'s "Lives" heading
(ui 10100) holds the Limited Lives checkbox (10110), a two-character count box (overflow error
10521, so 0..99 by width) and the Auto Respawn checkbox (10111). The count box is enabled only
while Limited Lives is ticked. A host lobby opens with Limited Lives clear, the count box disabled
and empty, and Auto Respawn ticked. The script callback 5007 reaches handler `0x40dce3`, which
writes the count (`atoi`) to `0x642fac`, the Limited Lives byte to `0x642fa8` and Auto Respawn to
`0x642fb0`.

**The match's count.** [Evidence: decoded] `FUN_004136e0` (`0x41389a`..`0x4138c0`) writes
`limited ? [0x642fac] : 10000` to `0x71c18c`, and every pilot record's `+0x40` is seeded from it
(`0x495889`, `0x497aff`). Nothing clamps it. An empty box parses as 0, and a pilot on 0 goes to -1
at its first death and still counts as alive, which is a defect in the original.

**A death.** [Evidence: decoded] `FUN_00498bf0`, the `0x12` death handler, decrements the dying
pilot's `+0x40` on every peer (`0x498c41`). For the local pilot, and only while lives are below
100, it posts a five-second HUD line (`0x498c52`..`0x498cba`) in the `DAT_006eba60` colour, ahead of the
kill lines ([`multiplayer-messages.md`](multiplayer-messages.md) "The kill lines"): row 210 `MSG_NUM_LIVES` ("You Have
%1!d! Lives Left!") above one, row 211 `MSG_ONE_LIFE` at one, and row 209 `MSG_NO_LIVES` at zero.
No message carries a pilot's lives: each peer counts them from the death reports.

**Out of lives.** [Evidence: decoded] `FUN_00470a10`, the return step, gives a pilot on 0 no
respawn. It sets camera mode 0 (chase, `FUN_0042c280(..., 0)`), and `FUN_00493eb0` binds the
camera's `+0x150` to the next aircraft in the vehicle list whose out-of-flight byte `+0x91d` is
clear. [Evidence: weak negative] No removal from the roster or the scores was found.

**Reason 4.** [Evidence: decoded] After every death (`0x499004`) and every drop (`0x499696`),
`FUN_004999f0` looks for two pilots with lives on different team slots (`+0x3c`). With none it
calls `FUN_004996d0(4)`, whose end-screen line is row 135 `MSG_STATE_GAMEOVER` ("Game Over:") with
row 7067 `MSG_MP_ALL_ALONE` ("No Enemies Left"), through `FUN_00499730`'s jump table at
`0x499910`. A free-for-all therefore ends when one pilot with lives is left. It runs beside the
armed Time or Score limit, and the first to trigger ends the match. With unlimited lives only a
drop can raise it.

## Auto Respawn

[Evidence: decoded] With Auto Respawn set (`0x71c1a0`), `FUN_00470a10` respawns the pilot as soon
as the step runs. With it clear, the step registers `FUN_004709f0` for input command `0x13` Fire
Guns through `FUN_00537bf0(0x13, 0x4709f0)`. The press sets `0x71c1a3`, unregisters the handler and
re-enters the step, which respawns through `FUN_004969b0`. [Evidence: weak negative] The message
table has no "press fire to respawn" string.

[Evidence: undecoded] The step runs from mission-script CALLBACK code 12 (`FUN_0047e080`, the
multiplayer branch at `0x47e1ed`), authored in the `RESET_STATE` of `player-player` and the three
`player_crash_*` defs. The wait before it is therefore the crash def's `RESET_TIME`, which those
defs author as 0.0 and which [`anim-definitions.md`](../formats/anim-definitions.md) lists as
undecoded. The real delay is open.

## Teams

A Deathmatch with teams formed is mode 2, and every team mode sets the team flag `0071d89c`
([`multiplayer-spawn.md`](multiplayer-spawn.md), "Which mode uses teams"). Three rules change with it.

**A teammate kill is a suicide.** [Evidence: decoded] The cause 1 row above: the killer is charged
`score_suicide` when killer and victim share the team slot at remote record `+0x3c`. The victim's
own score does not move.

**The Score limit is a team's total.** [Evidence: decoded] `FUN_0046ea40`, with `0071d89c` set,
walks the team list: each team's `+0x10` is set to its `+0x14` and then takes the `+0x1c` score of
every pilot whose `+8` names that team (`0x46eaa2`..`0x46eaf0`), mirrored into the team's display
record at `+0x38`. [Evidence: decoded, not re-read here] `FUN_00499270` then ends the match with
reason 2 when a team's `+0x10` reaches the target `0071c17c`, and skips the per-pilot check while
`0071d89c` is set. [Evidence: decoded] A team's own `+0x14` term is written only by mode 4's table
`FUN_0046ee20`: event 3 adds `score_zep` to every team whose `+0x18` differs from the lost hull's
(`0x46eeb3`..`0x46eed8`), and event 9 sets the named team's term to `score_zep_kill`
(`0x46ee99`..`0x46eead`, the team found by `FUN_0046e0f0`). No Deathmatch score event names a team.

**Reason 4 is one team left.** [Evidence: decoded] `FUN_004999f0` compares the team slots, so a
team match ends when every pilot with lives is on one team, however many of them there are.

## The in-flight scores

[Evidence: decoded] **The command.** Display Scores (command `0x23`, Tab, [`input.md`](input.md))
is registered by `FUN_004895a0` to `FUN_00489320`. The handler hides the chat panel at
`0x0071d8a8` (`FUN_004a8510`, its vtable `+0x64`) and hands the score list at `0x0071c13c` to
`FUN_00456400`, which calls `FUN_004565d0` on the HUD object at `0x00654234`. It does not test for
a network game. [Evidence: inferred] The list's entries are made by the multiplayer setup
`FUN_004136e0` and by each arriving player's record (`FUN_00414640`), so outside a network game the
list is empty and the press only hides the chat.

[Evidence: decoded] **A tap, not a hold.** The keyboard dispatch in `FUN_00535a80`
(`0x00535dfe`..`0x00535e24`) calls a code's handler only while its state word has bit 0 set. A press
writes 1 (3 if the word was already 1) and a release ORs 4; the next frame's `FUN_00535a00` turns 1
into 2 and any released word into 0. A key held across a frame is therefore released from 2 into 6
and calls nothing: the handler runs on the press, and on a press and release inside one frame.

[Evidence: decoded] **What it draws.** `FUN_004565d0` builds 18 rows once (the byte at `+0x10`),
each two text items in the `hudNetPlay` font: the line at x 50 and a flag column at x 40, rows at
y 30 to 200 every 10 (`0x004565f5`..`0x0045668e`), in the 640 by 480 frame the chat panel is placed
in. It hides all 36 items, then shows one line per list entry: the entry's text at `+0`, its colour
at `+0x28` when that is non-zero, a show with no timer (vtable `+0x60` with -1.0) and then
`FUN_005c55f0(4.0)`, which arms the item's timer at `+0x10` with flag bit 0, the field and bit the
chat panel's ten-second timer arms. An entry whose `+0x10` is non-zero also shows "F" (`0x0062507c`)
in the flag column, coloured by that index from the table `0xffffff`, `0xaaaa`, `0xaa`.
[Evidence: inferred] The timer's expiry hides each line as it hides the chat panel, so a press shows
the scores for four seconds. [Evidence: data] `hudNetPlay` is Courier New, height -12, width 8,
colour 255, 250, 66, shadowed, weight 600 (`fonts.zrd`).

[Evidence: decoded] **What else raises it.** The score table's handler `FUN_004993f0` (message
`0x13`, [`multiplayer-messages.md`](multiplayer-messages.md)) recomputes the team totals
(`FUN_0046ea40`), rewrites the lines (`FUN_0046e310(21, 7)`) and calls `FUN_00456400`, so every
score update shows the table, without hiding the chat. The console command `scorecolors`
(`FUN_0043d640`, `0x0043f06d`..`0x0043f0d0`) packs its three numbers as `a | b << 8 | c << 16` into
`0x0071c860`, recolours all 18 lines through `FUN_00456410` and shows the table. [Evidence: inferred]
That packing is a GDI colour word, which reads the flag table as team 1's flag in 170, 170, 0 and
team 2's in 170, 0, 0.

[Evidence: decoded] **The lines.** `FUN_0046e310(21, 7)` writes each pilot's line into the display
entry at pilot record `+0x2c` and sorts the list by entry `+0x24`, highest first:

| Line | Text |
|---|---|
| header | `Left(MSG_MPHUD_PLAYER, 21) + " " + Left(MSG_MPHUD_SCORE, 7)`, `MSG_MPHUD_PLAYER_TEAM` in a team match: "  Player" padded to 21, a space, "score  " |
| pilot, free for all | `Left(name + 24 spaces, 21) + " " + Left(score + 13 spaces, 7)` (`0x00627344`, `0x00627364`) |
| team, team match | `"%s (Team Score: %d)"` (`0x00627300`) of the team's name and its `+0x10` total, unpadded |
| pilot, team match | `Left(" " + name + 19 spaces, 21) + " " + Left(score + 13 spaces, 7)`, one character in |

The header entry is made by `FUN_004136e0` (`0x004139eb`..`0x00413a64`) with every key field -1, so
it sorts first. The name is the pilot record's `+0x10` and the score its `+0x1c`; a record whose
byte `+0x28` is set gets no line. Only the team branch writes a member's entry `+0x10`, from the
flag its aircraft carries (aircraft `+0x720`, [`multiplayer-ctf.md`](multiplayer-ctf.md)). The keys
(`FUN_0046ec40`) put teams in total order, a team's line over its members and the members by score;
in a free-for-all the pilots go by score, then by the join counter at record `+0x18`, higher first.

**The remake.** `UI/Overlays/ScoresOverlay.cs` draws the table while a seat holds Display Scores
and drops it on release, the user's ruling over the decoded four-second tap; a score update does not
raise it. Under the Original presentation the lines are `OriginalScoresText`'s, built by the table
above, drawn at the decoded positions three times over in the HUD's 1440-line reference, every
character on the 8-pixel cell, in Courier New at weight 600 in `hudNetPlay`'s ink with its shadow,
and the flag column marks a Capture the Flag carrier in the colours above. Not carried: the entry
colour, `scorecolors`, and the join-order tie break (ties keep `VersusMatch.Standings` order). A seat
on no lobby team in a team match, which the original has no counterpart for, follows the teams as a
plain line. A stunt race borrows the grid for its own columns, remake text in the header's style:
place and callsign in the name column, then the aircraft (12), best (10), gap (9) and runs. Under
Built-in the same standings stand as a chrome table in the results board's columns. Either look
stands in the holding seat's pane alone, as the original's stands in its one screen's HUD, so a
split screen seat's table never covers another pilot's flight. While it stands, that pane's chat
lines step aside; the original instead hides the panel at once on the press until the next line.

## What the remake takes

`Flight/Modes/MatchScores.cs` reads the nine keys from `player.zrd` at session build, each with
the fallback above, so every mode, the free-for-all Dogfight included, scores the shipped values.
`Flight/Modes/VersusMatch.cs` keeps the same signed score: `score_kill` per kill, `score_suicide`
for a death with no killer, `score_turret_kill` to a turret's owner, the kill-target row compared
against the score rather than against raw kills, and standings ranked by score with a tie at the
top rendered as a draw. Kills and deaths stay as separate display counters, which the original
keeps in the pilot's career record rather than in the match.

A lobby launch with teams is a team match (`VersusMatch.AssignTeams`, from each seat's
`NetSeat.TeamId`). A teammate kill costs the killer `score_suicide` and counts no kill. A team's
total is the sum of its members' scores, and the Score limit is read against that total alone.
Reason 4 asks for pilots with lives on two teams, a teamless seat counting as a team of its own.
Every machine derives the totals from the per-seat scores, so the remake's `0x13`, one seat's
score, carries no team count, unlike the original's table
([`multiplayer-messages.md`](multiplayer-messages.md)). The team's own term (`TeamTermOf`) is
written only by Zeppelin vs Zeppelin: `VersusMatch.EndOnHullLoss` adds `score_zep` to every other
team's term and `RegisterZeppelinKill` sets the term of a side whose hull downed a pilot. Both show
on the board's total (`TeamTotalOf`), never in the number the Score limit reads (`TeamScoreOf`).
A hull loss ends the match as `NetMatchEnd.Objective` with the winning team named in the state
([`multiplayer-zvz.md`](multiplayer-zvz.md)).

The lives rule follows the decode above, with three differences. The count is clamped to 1..99
(`DogfightLobby.ClampLives`), so the empty-box defect cannot launch. Ticking Limited Lives puts 3
in the box, the one invented number in the rule, since the original leaves it empty. A guest's
match counts deaths from the host's reliable score messages rather than from the death reports
themselves. Reason 4 is `VersusMatch.AllAlone`, checked after each death and each drop on the
host, which sends it as `NetMatchEnd.NobodyLeft`. An out-of-lives pilot stays on the scoreboard and
watches the next flying seat in chase view (`VersusMatch.NextWatched`). With Auto Respawn off the
crash camera runs its usual time and then waits for Fire Guns; R still respawns at any time. The
remake's crash camera time (3 s in a match) is not a reading of the original.
