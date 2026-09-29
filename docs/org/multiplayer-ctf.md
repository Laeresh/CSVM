# Capture the Flag, decoded from `crimson.exe`

How the original's Capture the Flag runs its flags. It is match mode 3
([`multiplayer-spawn.md`](multiplayer-spawn.md), "Which mode uses teams"), scored by mode 3's table
([`multiplayer-scoring.md`](multiplayer-scoring.md)), with the lobby teams of
[`multiplayer-messages.md`](multiplayer-messages.md) "Lobby teams". The map side is
[`../formats/interp.md`](../formats/interp.md) "Capture the Flag": the five chapters with an `MP2`
mission lay out `ctf_n`, `cs_flag_n` and `cs_flg_lightn`, and only `mp2.gw` leaves them on.

Every claim names the address it was read at. Evidence tags as elsewhere: decoded (read in the
code), script (a menu or anim script), data (extracted files), inferred (not checked in code).

## Function map

| Address | Role |
|---|---|
| `FUN_00495d40` | Builds one flag record per team |
| `FUN_00499e50` | The per-tick proximity check of the local aircraft, from `FUN_00496d60` in mode 3 only (`0x496db1`) |
| `FUN_0049a050` | Sends an ask, message `0x1c`, to the host |
| `FUN_0049a170` | The host's decision on an ask |
| `FUN_0049a240` | Broadcasts the flag table, message `0x1d` |
| `FUN_0049a300` | Applies a table: moves the flags, speaks, posts, scores |
| `FUN_0049a780` | A flag goes to held |
| `FUN_0049ab50` | A carrier's drop |
| `FUN_0049a210` | A floating flag's throw ended: home on the host |
| `FUN_00495c40` | Loads the six `snd_CTF*` voice lines |

## The flag record

[Evidence: decoded] `FUN_00495d40` builds one `0x48`-byte record per team `n = 1, 2, ...` while
`cs_flag_n` exists and a team numbered `n` exists (`FUN_0046e0f0(n)` walks the team list for a
record whose `+0x18` is `n`), onto the list at `0071c794`:

| Field | Holds |
|---|---|
| `+0x00` | the team number `n`, also the flag's id on the wire |
| `+0x04` | holder, a player id (0 none) |
| `+0x08` | state: 1 held, 2 at home, 3 floating (starts 2) |
| `+0x0c` | the `cs_flag_n` node, the flag at its base |
| `+0x10` | the `cs_flg_lightn` node, the flag carried and floating |
| `+0x14`, `+0x18`, `+0x1c` | three HUD markers: the base, the flag at base, the flag away |
| `+0x20` | the home position, the base node's world position |
| `+0x2c`, `+0x30` | the anims `turnon_flite_n`, `turnoff_flite_n` |
| `+0x34` | byte, the base light is on (starts 1) |
| `+0x38`, `+0x3c`, `+0x40` | the anims `flg_on_n`, `flg_drop_n`, `flg_off_n` |
| `+0x44` | the running throw animation while floating |

A team numbered past the flags a map lays out gets no flag, and the build stops at the first
number with no team.

## The ask: the client checks, the host decides

[Evidence: decoded] `FUN_00499e50(aircraft)` runs every tick for the local aircraft. Distances are
squared (`FUN_00538880`) against 625 at `0x6081c0`, so the reach is **25 m**, inclusive. The
aircraft's `+0x720` is the flag it carries and `+0x724` a cooldown clock, ready once it is at or
below the game clock `0x9ad748`. The first rule that holds asks, and the loop stops.

| Flag | Pilot | Near | Ask | Cooldown |
|---|---|---|---|---|
| at home, another team's | carries nothing, cooled | the flag's home | take (1) | 5 s (`0x6036bc`, `0x499fd6`) |
| floating, any team's | carries nothing, cooled | the floating flag | take (1) | 4 s (`0x603514`) |
| held by it, its own team's | cooled | the flag's home | home (2), a return | 4 s |
| held by it, another team's | cooled | its **own** flag's home (`FUN_0049a1e0`) | home (2), a capture | 4 s |

⚠ A capture does not need the capturing team's own flag at home. A pilot may catch its own team's
floating flag.

[Evidence: decoded] `FUN_0049a050(flag, ask)` sends `0x1c` (flag id at `+4`, ask at `+8`)
guaranteed to the host `DAT_0071d228`. A guest asking to take applies it at once, state 1 with
itself the holder, through `FUN_0049a780`. `FUN_0049a170`, on the host: a take while the flag is
not held makes it held by the asker, first asker winning; a home makes it state 2 with no holder;
anything else re-sends the unchanged row. `FUN_0049a240` then broadcasts `0x1d`: a count, then
`(id, state, holder)` per flag, the changed row from the arguments and the others as they stand.

## Applying a row, and scoring

[Evidence: decoded] `FUN_0049a300`, for each row whose state changed:

- **To held** (`FUN_0049a780`). From floating it stops the throw (`+0x44`) and, when the flag is the
  local team's, plays `snd_CTFscoreCapt`. From home it hides `cs_flag_n`, runs
  `turnoff_flite_n`, clears `+0x34`, and plays `snd_CTFlost` to the flag's own team or
  `snd_CTFstolen` to the others. It runs `flg_on_n` on the holder, sets its `+0x720`, names its
  tag with row 198 "%1 Holds %2 flag", sets the away marker to row 199, and posts row 200 "%1 Flag
  Captured" with the team name for 5 s.
- **To home.** Row 201 "%1 Flag Is At Home Base" for 5 s, `flg_off_n` on the holder,
  `cs_flag_n` shown and `turnon_flite_n` run. **Only from held:** a holder on the flag's own team
  scores event 4 (`score_return_flag`, +1) and its team hears `snd_CTFscoreRec`; any other holder
  scores event 5 (`score_enemy_flag`, +5) and the teams hear `snd_CTFscoreFlag` or
  `snd_CTFscoreEnemy` by side. `FUN_00499270` then sends the score table.
- A holder change strips the previous holder's flag. After the rows, `FUN_0046e310(0x15, 7)`
  rebuilds the HUD score text.

A floating flag sent home scores nothing, since its previous state is 3.

## The drop and the throw

[Evidence: decoded] `FUN_0049ab50(aircraft)`, when the aircraft carries a flag: `flg_drop_n` on
the aircraft, the throw's instance kept in `+0x44`, `+0x720` cleared, state 3 with no holder, the
away marker set to row 193/194 "Your/Enemy Flag Floating", and row 195 "%1's Flag is Floating"
posted for 5 s. It is not sent: every peer runs it, from the death handler `FUN_00498bf0`
(`0x498fac`), from the peer-left handler `FUN_004995a0`, and from the console command `ejectflag`
(`0x43f516`..`0x43f52c`), each in mode 3 only. State 3 is a state each machine reaches on its own.

[Evidence: data] `player-flg_throw_n` moves the flag from the carrier's `cf_light` to `world1`,
then flies it by `OBJECT_MOTION`: gravity -7 (`COMPLEX`), elevation 75..85, speed 35..40, azimuth
0, `RUN_TIME` 15 s, then `Callback 700 + n` ([`objectMotion.md`](objectMotion.md) reads those
keys). [Evidence: decoded] `FUN_0047e080` routes 701..704 to `FUN_0049a210(n)` (`0x47e376`), which
on the host alone clears `+0x44` and broadcasts state 2 with no holder. An uncaught flag therefore
goes home 15 s after the drop, unscored.

## Win condition

[Evidence: decoded] Capture the Flag has no flag count of its own. It ends on the lobby's Time or
Score limit, the Score limit read against a team's total, or on reason 4 when one team is left
([`multiplayer-scoring.md`](multiplayer-scoring.md), "Teams").

## What the player sees and hears

- [Evidence: decoded] HUD lines, 5 s each (`FUN_004587d0`): rows 195, 200 and 201 with the team
  name. [Evidence: weak negative] Rows 7068..7076 are not referenced by the functions read here.
- Three markers per flag and the carrier's tag, below.
- [Evidence: decoded] Voice: `FUN_00495c40` loads `snd_CTFlost`, `snd_CTFstolen`,
  `snd_CTFscoreRec`, `snd_CTFscoreEnemy`, `snd_CTFscoreFlag` and `snd_CTFscoreCapt` (strings
  `00628f94`..`00628fe8`).
- [Evidence: data] The carried flag is the billboard `cs_flg_lightn`, hung under the aircraft's
  `cf_light` by `player-flg_on_n`, with `flg_glow_n`'s white point light (range 4..14, ambient 0.3,
  diffuse 1). Every airframe model carries `cf_light`.

## Markers

[Evidence: decoded] `FUN_00495d40` draws no marker of its own. It finds three entries the map's
`targets.zrd` already lists (`MSG_TRGT_FLAGBASE` on `ctf_n`, `MSG_TRGT_FLAG` on `cs_flag_n` and
`cs_flg_lightn`) by node name in the target list `0071d338` (`FUN_004a2880`, `0x495e23`,
`0x49608d`, `0x496270`) and rewrites each one's name `+0x10` and category `+0x38` (with its id at
`+0x48`) for this machine. "Your" or "Enemy" below is whether the local pilot is on the flag's team
(`FUN_0046f300`). The name line is the team's name on all three.

| Marker | Record | Category | Selectable |
|---|---|---|---|
| `ctf_n`, the base | `+0x14` | 7060 "Your Base" / 7061 "Enemy Base" | always, from the table |
| `cs_flag_n`, the flag at base | `+0x18` | 7056 "Your Flag At Base" / 7057 "Enemy Flag At Base" | while home |
| `cs_flg_lightn`, the flag away | `+0x1c` | held: row 199 "%1 Flag Captured by %2" with Your/Enemy and the holder; floating: 193 "Your Flag Floating" / 194 "Enemy Flag Floating" | while away |

Selectable is the entry's `+0x4d`. The build clears it on the away marker (`0x496242`); a take sets
it there and clears it on the flag at base (`0x49aaad`..`0x49aacb`); a drop sets it again with the
floating line (`0x49ad65`); a flag home swaps back (`0x49a5ee`..`0x49a60a`). The away marker's
name is row 7055 formatted with the team name (`0x4960ae`), a row the shipped message table lacks.
None of the categories is one `GetColor` draws red, so every flag marker is blue
([`targeting.md`](targeting.md), "Colour").

[Evidence: decoded] The carrier's tag is the carrier aircraft's own name line. A take names it row
198 "%1  Holds %2 flag" with the pilot's name and Your/Enemy (`0x49a930`), and the flag leaving the
aircraft puts the pilot's name back (`0x49a47b` on a holder change, `0x49abc1` on a drop).

## The lobby

[Evidence: script] Choosing Capture the Flag in `MULTIPLAYERLOBBY_MISSION.SCRIPT` ticks Restrict
Number of Teams, fixes and greys both count boxes, and greys Above the Clouds and NW Lighthouse,
the two environments with no `MP2`. Langui 10519 reads "Each player must be on one of two teams to
play.", and 10124 describes the mode. 10127 "Engine Nacelles Regeneration" and 10128 "Instant Flag
Return" exist, but no lobby script creates a control for them.

## What the remake takes

`Flight/Modes/FlagMatch.cs` holds the rules above engine-free: the reach, the two cooldowns, first
asker wins, the take ahead of the host's answer, the table's rows, the drop, the 15 s throw and the
points. `Session/World/FlagRuntime.cs` runs them in a network match. The differences:

- The ask is message `0x63` and the table `0x64`, naming seats rather than player ids
  ([`multiplayer-messages.md`](multiplayer-messages.md)). A floating row in the table is ignored,
  since each machine reaches that state on its own.
- The flags are moved in code rather than by running the `player`-rooted anims. The carried flag,
  a library root outside the world tree, is built once per machine and reparented under the
  holder's `cf_light` (its model when the node is absent), and the throw is
  `FlagMatch`'s own integration of the `flg_throw_n` motion. It rests on the ground under the drop
  point with the column contact's landing response. The base light still runs the mission's own
  `turnon_flite_n` and `turnoff_flite_n`, and the glow is a world light of `flg_glow_n`'s values.
- A downed or inert aircraft asks for nothing, so a wreck cannot catch the flag it dropped. Whether
  the original's tick skips a dead aircraft was not read.
- The host may require the capturing team's own flag at home, an option the original lacks.
- The lobby refuses a Capture the Flag launch with a team numbered above 2, under 10519, since
  such a team would fly with no flag.
- The markers are the mission's own site entries labelled per pane rather than per machine
  (`Flight/Modes/FlagMarkers.cs`), so splitscreen panes on two sides each read their own. The away
  marker, which the world index cannot find, stands where the carried flag is. Its name line is
  the team's name in place of the missing row 7055. The carrier's tag replaces the airframe name
  the remake's aircraft marker prints, and names the pilot by its callsign.
- Not built: `ejectflag` and `score_turret_kill`.
