# Bot planes in Deathmatch

**ACTIVE PLAN** (written 2026-10-03), executed on the `bots` branch in
`.claude/worktrees/bots`. When every item lands, the closing
commit deletes this file, records the completion in its message, and clears the "Current status"
pointer; any live prose linking this file by path is unlinked in the same commit.

This plan adds computer-flown pilots ("bots") to Deathmatch, free-for-all and team, in a network
match and in a local split screen or solo match. A bot is a full match participant: it holds a seat
the host owns, scores, respawns, rearms and can win. The original game has no bots (its DirectPlay
lobby decode in `docs/org/multiplayer-messages.md` and `multiplayer-spawn.md` names no computer
player), so every behaviour here is a remake decision; nothing in `crimson.exe` is being matched,
and the original-data rule applies only to the inputs a bot reuses (pilot-name strings, Instant
Action personalities, stock loadouts, the skill offset).

Out of scope: Capture the Flag and Zeppelin vs Zeppelin (the AI has no flag or zeppelin-objective
behaviour), co-op campaign fill, bot voice, a dedicated server, and the seat colour palette at 16
pilots (see D32). No item is drawn from `backlog.md` or a `backlog` GitHub issue; a search of both
found no bot, skirmish or computer-player entry.

## Milestone goal

- A host adds bots in the Multiplayer Lobby one at a time or with Fill-to-N, sets each bot's plane,
  skill, team and callsign, and launches; guests see and fight the bots like remote humans.
- The local Dogfight join board offers the same bot rows, so one human can play against bots.
- Bots score, die, respawn, rearm and win under the same Deathmatch rules as humans, and the board
  shows them with a bot tag.
- A human who joins a full field takes a bot's seat in the lobby. A human who joins while a match
  runs waits in the lobby and takes the seat when the match is back in the lobby.

**Bots fly Deathmatch only, and are silent.** Objective modes need AI behaviour this plan does not
build, and fifteen chattering hostiles is a separate look-and-sound decision.

## Decisions (2026-10-03)

| # | Question | Decision |
|---|---|---|
| 1 | Which modes get bots first? | **Deathmatch (free-for-all and teams), network and local split screen/solo.** `VersusMatch`/`VersusDirector` are shared, so local costs little; CTF and ZvZ wait for objective AI. |
| 2 | Full participant or scenery? | **Full participant**: board row, scores with `player.zrd` values, respawns through the host's rotation, Limited Lives applies, can win, keeps "nobody left to fight" working. |
| 3 | How is a bot represented? | **A seat the host owns, flown by an `AiPilot`.** Hit authority, death reports, `ScoreMessage`, spawns, team, callsign and colour reuse the seat paths; a guest sees a remote seat rig. Not `NetWorldLink` host-world AI with a second participant table. |
| 4 | Ceiling? | **16 pilots total**, humans and bots, `NetSeats.MaxPlayers`. Seats 8 to 15 take the derived colours and the respawn fan wraps, as for humans. |
| 5 | How does the host add bots? | **Both**: an Add button per bot, and a Fill-to-N shortcut that creates editable, removable bot rows. |
| 6 | A human joins a full lobby, or leaves mid-match? | **The most recently added bot yields on join.** A guest who leaves mid-match is not replaced by a bot. |
| 7 | A human joins a running match whose field is full? | **The human lands in the lobby, never in the running match, and takes the newest bot's seat when the match is back in the lobby** (as Decision 6). A host Restart keeps the match out of the lobby, so it changes no seat: the restarted match flies the same field and the human waits on. No seat changes hands mid-match. |
| 8 | What does skill mean? | **Novice / veteran / ace** (`IDS_IA_DIFFICULTY`, langui 3695): the -2/0/+2 offset on the pilot's nine ratings, each bot rolling one of Instant Action's four authored personalities or the flat average. **No armour/health scaling**, so a bot's hull equals a human's in the same plane. |
| 9 | Plane and loadout? | **A stock plane per bot, Random by default** (Fill-to-N uses Random), stock loadout, stock livery. No hangar builds. |
| 10 | Who does a bot prefer to attack? | **Every pilot equal in a Dogfight**: the campaign's human preference (`AiTargetRanking.PlayerWeight`) does not apply. |
| 11 | Name and marking? | **Auto callsign from the shipped pilot-name strings, host-editable, 12-char limit; a bot tag in the lobby roster and on the board**; the in-flight marker shows only the callsign, as for a human (#116). |
| 12 | Team in a team Deathmatch? | **The smallest team at the moment the bot is added; the host can move it.** No automatic rebalance after that. |
| 13 | Local play? | **The join board gets the same bot rows and its two-pilot minimum counts bots**, so one human plus one bot can start. |
| 14 | Ammo and damage? | **Bots use the rearm bases**: a new standing order breaks off to the nearest serving base when ammo runs low or the hull is badly damaged. |
| 15 | Voice? | **Silent.** No radio lines, the same as a remote human. Revisit after playtesting. |
| 16 | Respawn with Auto Respawn off? | **A bot always respawns after the crash camera time**, since it has no Fire button to press. (Taken as a default during the grilling, confirmed with the summary.) |
| 17 | Who resolves Random? | **The host, at launch**; the roster carries the real plane, and a guest never parses bot arguments. (Default, confirmed with the summary.) |
| 18 | Rematch? | **The lobby keeps its bot rows between matches.** (Default, confirmed with the summary.) |
| 19 | Host leaves? | **The match ends as today and the bots go with it.** (Default, confirmed with the summary.) |

## ⚠ Read this before implementing anything

Nothing here was disproven, so there is no wrong-claims table. The evidence quality is uneven:

| Confidence | Items | What that means for you |
|---|---|---|
| **Traced to an exact mechanism in code, with the data that proves it** | none | The code facts cited below carry path and line from this session's exploration, but every item's design rests on discussion, so none is graded traced. |
| **Direction sound, magnitude a judgement call** | none | The rearm thresholds (B14) and the per-bot CPU cost (D33) are magnitudes with no measurement yet; they become TUNE once measured. |
| **Leads only, no mechanism yet** | all | Re-read each cited member before building on it; an item may find the seam is elsewhere. |

## Ground rules

- **Original-game data drives everything.** Read the reader/compiled JSON before writing a handler;
  never guess a value. Inventing content is the trap this project falls into most often.
- **Evidence is a lead to verify, not a finding to implement.** Confirm every claim against the
  data/code before building on it; **a correct disproof that lands no code is a success here**, not a
  failure. Mark each item's Evidence with its confidence (traced-to-code / direction-sound-magnitude-
  TUNE / lead-only).
- **`PROJECT_CONTEXT.md` + the module's entry in `docs/architecture/<Namespace>.md` (plus its index
  bullet in `docs/architecture.md`) / `docs/formats/` are updated in the same turn** as each landed
  item; a landed item gets its record in the landing commit's message and is **deleted** from
  `backlog.md` (not marked FIXED there). New decodes land with their `docs/formats/` page.
- **Read `docs/verification.md` before measuring anything**, the instruments here mislead; cite the
  rule that bites per item.
- **Verify against a full 8-chapter `--freecam --chapter=<X>` regression** (zero errors, same
  mesh/node counts unless the change is meant to add coverage) plus a targeted capture at the
  location the report came from.
- **Read the module's entry in `docs/architecture/<Namespace>.md` (found through the index in
  `docs/architecture.md`) before modifying it,** then the comments on the members you touch; dead
  ends are in the landing commits (`git log --grep=<ID>`), so search those before re-chasing one.

## Checklist

Statuses: ☐ open · ◐ in progress · ☑ done · ❌ closed/disproven. **Keep this in sync as items land.**

### Wave A, seat model and match bookkeeping

1. ☐ A seat knows whether a human or a bot flies it, and the roster carries that across the wire
2. ☐ `VersusMatch` keys its scores by pilot, so a row survives its seat being reused
3. ☐ The host flies a bot seat with an `AiPilot`; guests see it as a remote seat
4. ☐ CLI twin: `--vs-bots=` / `--vs-bot=` build bot seats in a scripted host

### Wave B, bot behaviour in a Dogfight

11. ☐ Equal target weights for every pilot in a Dogfight
12. ☐ Bots respawn through the host's rotation and follow Limited Lives
13. ☐ Skill tiers, personalities, stock plane with Random, and the callsign pool
14. ☐ Rearm standing order: a bot breaks off to a base when low or badly damaged

### Wave C, lobby and local setup

21. ☐ Multiplayer Lobby: Add bot, Fill-to-N, per-row plane/skill/team/callsign, Remove
22. ☐ A joining human takes the newest bot's seat in the lobby, a late joiner once the match is back there
23. ☐ Local join board: bot rows, and the two-pilot minimum counts bots

### Wave D, presentation and measurement

31. ☐ Bot tag on the lobby roster and the board; seat-style in-flight markers for bots
32. ☐ Crowded free-for-all playtest at the controls
33. ☐ Host cost of fifteen bots, measured

## Dependency and parallelism notes

A1 blocks everything. A2 and A3 both need A1 and can run in parallel (A2 owns `VersusMatch`,
`VersusDirector` scoring and `VersusBoard`; A3 owns `SessionNet`, `FlightRoster`/`AiFlightAssembler`
and the bot rig). A4 needs A3, and every later item's tests lean on A4. Wave B needs A3 and A4; B11,
B12 and B13 touch different files and can run in parallel, B14 after B12 (a rearm trip and a respawn
share the bot's standing-order state). Wave C needs A1 (and A2 only if A2 survives its re-check, see A2);
C21 and C23 both edit `DogfightLobby.cs`, so they run in sequence, C21 first, and C22 follows C21.
D31 needs A2 and C21. D32 and D33 run last, on the merged tree.

---

# Wave A, seat model and match bookkeeping

## A1 ☐ A seat knows whether a human or a bot flies it, and the roster carries that across the wire

**Goal.** A `NetSeat` can be a bot seat owned by the host's peer, with a skill; the seat roster a
guest receives says which seats are bots; and "this machine flies the seat" is no longer the same
claim as "this seat has a pane".

**Evidence (confidence: lead-only).** `NetSeat` (`CSVM/src/Net/NetSeat.cs`) holds `PeerId,
SeatIndex, TeamId, IsLocal, Callsign, PlaneNode, Livery, Voice, Score` and has no bot flag.
`IsLocal` creates a pane in `SessionNet.FillSeatRigs` (`CSVM/src/Launch/SessionNet.cs:612-614`), and
`HumanFlightAdapter` assumes a local seat is human-flown. `NetSeats.Validate` and
`NetSession.WellFormed` (`NetSession.cs:390-409`) reject more than 16 seats or gaps and require seat
0 to be the host peer. `SeatRosterMessage.MaxSeats = 16`, entry 20 bytes (seat, team, plane,
IsHost, callsign 16 bytes, voice). Seat-wide tables are 16 wide already (`NetChannels.Count`,
`AircraftStateCadence._sequence`, `SessionNet._fireSequence`, `NetInstruments`, `NetTeams.MaxTeams`,
`NetDogfightMessages.MaxRows`).

**Approach.** Add a pilot kind (human, bot) and a skill tier to `NetSeat`. Split `IsLocal` into
"flown here" and "has a pane" at every consumer; a bot seat is flown on the host and has no pane.
Carry kind and skill in the seat roster entry; whether that fits the 20-byte entry or needs a wider
one is `<TODO: decide after reading SeatRosterMessage's layout and its version handling>`. Keep
`MaxPlayers = 16` as the shared ceiling (Decision 4). Use "team id", never "faction" or "side"
(`CONTEXT.md`).

**Model recommendation.** `<TODO: not settled in the session>`

**Verify.** A unit round-trip of the roster message with mixed human and bot seats; `net-seats`
still green; `<TODO: the engine suite that proves a bot seat builds no pane on host or guest>`.

**⚠ Traps.** `IsLocal` has many readers; a missed one silently gives a bot a pane or a human no
input. The `NetNamespaceDependency` test confines engine types to the carriers
(`docs/architecture/Net.md:261-264`), so the pilot kind stays engine-free in `Net/`.

## A2 ☐ `VersusMatch` keys its scores by pilot, so a row survives its seat being reused

**Goal.** The match keeps one score row per pilot who has flown in it. When a bot leaves and a
human takes its seat, the bot's row stays, marked as left, and the human gets a new row.
`<TODO: re-check whether this item is still needed. It was written for a seat changing hands
mid-match, which Decision 7 no longer allows: a seat now changes hands only in the lobby, between
matches. Name any reader left that needs a pilot key (the lobby's Game Scores tab for the match
just finished, D31's bot tag), or close this item as not needed>`

**Evidence (confidence: lead-only).** `VersusMatch` (`CSVM/src/Flight/Modes/VersusMatch.cs:36,53`)
sizes its score array by seat count (`PlayerCount`). `VersusDirector.ScoreDeath`
(`CSVM/src/Session/World/VersusDirector.cs:386-437`, killer test at `:395`) scores per seat and sends
`ScoreMessage` (seat, score, kills, deaths). `VersusBoard` (`CSVM/src/UI/Screens/VersusBoard.cs:66,79,129`)
and the lobby's Game Scores tab (`DogfightLobby.Land`/`ScoresOf`) are seat-indexed and labelled
`SplitScreen.PlayerTag`. `VersusMatch.Leave` exists for a guest who leaves.

**Approach.** Introduce a pilot id distinct from the seat index; the seat maps to its current pilot.
`ScoreMessage` and the guest mirror (`ApplyScore`) name the pilot, or the row is re-keyed on the
seat-change event; `<TODO: pick after reading ScoreMessage and the SeatLeft path>`. Standings,
team standings, "nobody left to fight" and Limited Lives read pilots, not seats.

**Model recommendation.** `<TODO: not settled in the session>`

**Verify.** `net-match-state`, `net-versus-lives`, `net-versus-host-left`, `net-team-deathmatch` stay
green; `<TODO: a unit test where a seat's pilot changes between matches and the finished match's rows survive, if the re-check keeps this item>`.

**⚠ Traps.** This is the data change most likely to cause trouble: every seat-indexed score reader
must move, and a guest's mirror must agree with the host's after a seat changes hands.

## A3 ☐ The host flies a bot seat with an `AiPilot`; guests see it as a remote seat

**Goal.** On the host, a bot seat is a `FlightController` driven by an `AiPilot`, sending its state
on its seat channel at the owner cadence. On a guest it is a remote seat rig fed by
`AircraftStateMessage`. Hits on it and by it, its death and its score take the seat paths.

**Evidence (confidence: lead-only).** Remote seats: `SessionNet.BuildSeatRigs`
(`SessionNet.cs:180,596-624`), samples to `_seatRigs[seat].Controller.RemotePoses.Receive`
(`:630-637`), `AircraftStateMessage` 44 bytes UnreliableSequenced (`NetMessages.cs:453-466`), cadence
`AircraftStateCadence.SendStepInterval = 3` (`CSVM/src/Net/AircraftStateCadence.cs:18`). Deaths:
`SessionNet.ReportDeath` (`SessionNet.cs:530-556`) runs only for local human seats (`:532`); killer
by `SeatOfShooter` (`:575-591`). Hits: `SessionNet.RouteHit` (`:727-763`) has the host stand in for
every round no seat fired. AI creation: `FlightRoster.SpawnAi(AiSpawn)`
(`CSVM/src/Session/Roster/FlightRoster.cs:246`, record at `:31-44`), AI `PlayerIndex = 100 + index`
(`:52`, `AiFlightAssembler.cs:162`). An AI kill on a seat is scored today as a no-killer death
(`SessionNet.cs:537-547`). The co-op path (`NetWorldLink`, `AiStateMessage` 0x45) is the precedent for
host-flown AI but is not the path this item takes (Decision 3).

**Approach.** Assemble the bot's aircraft through the AI assembler but register it as the seat's
controller, with the seat's `PlayerIndex` rather than 100+n, so `SeatOfShooter` resolves a bot's
rounds to its seat. Send its state through the same cadence and seat channel a human owner uses.
Run `ReportDeath` for bot seats too. The host applies damage to a bot as the bot's owner. Team id
is `AimAssist.LobbyTeam(N)` in a team match (`AimAssist.cs:185-195`), never a raw lobby team.

**Model recommendation.** `<TODO: not settled in the session>`

**Verify.** A new loopback suite on the `NetWorldSuites` template (`CSVM/src/Testing/NetWorldSuites.cs:620-650`,
`LoopbackTransport.Mesh`): a bot kills the guest's seat and the host scores the bot; the guest
kills the bot and the host scores the guest; both ends' standings agree. Run it lossless and over
`LoopbackConditions` with loss.

**⚠ Traps.** DET-17 (`docs/verification.md:283`): a lossy loopback suite must wait on every
condition its checks read. A bot's controller must not also be admitted by `NetWorldLink.Admit`,
which walks the roster's AI list and would replicate it twice.

## A4 ☐ CLI twin: `--vs-bots=` / `--vs-bot=` build bot seats in a scripted host

**Goal.** A scripted `--vs` run, local or `--net-host`, can add bots, so suites and probes drive the
feature without the menu. Flag names are a proposal: `<TODO: confirm names against docs/cli.md's
conventions>`.

**Evidence (confidence: lead-only).** Today's `--ai=` (`SessionSpec.cs:75-82,512-516,1364-1396`,
built in `Launch/OppositionStage.cs:78-180`) spawns AI per machine from each machine's own spec, so a
real guest would not build them; the loopback suite works only because both ends parse the same
args (`NetWorldSuites.cs:33,90-94`).

**Approach.** The flags add bot seats to the host's launch roster (`Launcher.cs:2813-2851`), and
the roster carries them to guests (Decision 17). `docs/cli.md` gains the flag bullets within its
600-character cap.

**Model recommendation.** `<TODO: not settled in the session>`

**Verify.** `--net-host=127.0.0.1` with bots and a `--net-join=127.0.0.1` guest with no bot flags:
the guest flies against the host's bots. `<TODO: the exact RunProbe.ps1 invocation>`

**⚠ Traps.** Name `127.0.0.1` in a scripted run; a wildcard bind puts a firewall dialog on the
user's screen.

# Wave B, bot behaviour in a Dogfight

## B11 ☐ Equal target weights for every pilot in a Dogfight

**Goal.** In a Deathmatch a bot ranks a human and another bot with the same base weight, so the
choice comes down to the ranking's other terms (Decision 10).

**Evidence (confidence: lead-only).** `AiTargetRanking.PlayerWeight = 0.7` against `BaseWeight`
(`CSVM/src/Flight/Ai/AiTargetRanking.cs:123,187`), wingman +0.4, score minimised.
`GunnerAcquisition.cs:187,220-235` sweeps the whole vehicle list through `AimAssist.Hostile`
(`AimAssist.cs:325`).

**Approach.** Gate the human preference on the session mode, off in a Dogfight. Whether a bot seat
counts as a "player" in the ranking after A3 is `<TODO: check how the ranking recognises a player>`.

**Model recommendation.** `<TODO: not settled in the session>`

**Verify.** `<TODO: a unit test on the ranking with a human and a bot at equal geometry>`; the
campaign AI suites stay green.

**⚠ Traps.** The campaign and Instant Action keep the preference; only the Dogfight path changes.

## B12 ☐ Bots respawn through the host's rotation and follow Limited Lives

**Goal.** A downed bot respawns after the crash camera time at a point the host's
`VersusSpawnRotation` grants, whatever Auto Respawn says (Decision 16), and stays down when out of
lives.

**Evidence (confidence: lead-only).** The host's single `VersusSpawnRotation` grants returns as
`SpawnMessage`/`SpawnAtMessage` (`VersusDirector.cs:158,205`); it relaxes its one-living-seat-per-point
rule for a field larger than the table (`docs/architecture/Session.md:207`). The remake's 3 s crash
camera time in a match is not a reading of the original (`docs/org/multiplayer-scoring.md:193-195`).
`VersusMatch.OutOfLives` exists.

**Approach.** The host's bot rig asks the rotation for a point as a local human seat does, on the
auto-respawn path. `<TODO: read how a local seat's respawn request reaches the rotation>`

**Model recommendation.** `<TODO: not settled in the session>`

**Verify.** Extend the A3 suite: a downed bot returns at a rotation point on both ends; with
`--vs-lives=1` it stays down and the match ends on "nobody left to fight" when it should.

**⚠ Traps.** The respawned bot's AI state (target, standing order, rearm trip) must reset.

## B13 ☐ Skill tiers, personalities, stock plane with Random, and the callsign pool

**Goal.** A bot flies a stock plane (Random resolved by the host) with its stock loadout and livery,
at novice/veteran/ace, with a rolled Instant Action personality and a callsign from the shipped
pilot names (Decisions 8, 9, 11).

**Evidence (confidence: lead-only).** The tier offset is -2/0/+2 on the nine ratings, clamped to
[0, 9], and the original also scales a hostile's armour and health 0.75/1.0/1.25
(`docs/formats/instant-action.md:547-572`); this plan takes the rating offset only. The four
personalities plus the flat average are the table ending at `instant-action.md:540-545`. Skill strings are
`IDS_IA_DIFFICULTY` (langui 3695, `instant-action.md:162`). Pilot names are langui 500 to 599
(`docs/formats/strings.md:105`). `Difficulty` applies only to teams other than `PlayerTeam` and
`NeutralTeam` (`CSVM/src/Flight/Hangar/Difficulty.cs:41-57`). `AiSpawn` carries `RosterSkills`,
`Difficulty`, `PilotName`, `PlaneName`, `Fit`.

**Approach.** Map the tier onto the existing offset without the armour multiplier; pass the rolled
personality as `RosterSkills`. Draw callsigns without repeats from the pilot-name strings,
truncated to the 12-character limit. `<TODO: confirm which of 500-599 are names and which are skill
ratings>`; `<TODO: the stock plane list a bot may draw from>`.

**Model recommendation.** `<TODO: not settled in the session>`

**Verify.** `<TODO: a unit test that an ace bot's ratings and hull match the expected values, and
that Random resolves on the host only>`

**⚠ Traps.** `Difficulty` treats team 1 as exempt; in a team Deathmatch bots fly `LobbyTeam(N)`, so
check the exemption does not reach them or a human's team by accident.

## B14 ☐ Rearm standing order: a bot breaks off to a base when low or badly damaged

**Goal.** A bot whose ammo runs low or whose hull is badly damaged flies to the nearest base that
serves it, is restored there, and rejoins the fight (Decision 14).

**Evidence (confidence: lead-only).** AI planes spend ammo: `AiFlightAssembler.cs:222` builds
`PylonOrdnance` with the controller's `InfiniteAmmo`, which the AI path never sets.
`RearmRuntime` lists the bases and runs them in any Dogfight, each machine checking only the seats
it flies, and `FlightController.Rearm` restores parts, damage stages and every slot
(`docs/org/multiplayer-rearm.md:98-115`). `AiPilot` has standing orders (heading, altitude, throttle,
`Patrol`, `Gunner`, `Machine`, `Escort`; `docs/architecture/Flight.md:468-583`).

**Approach.** A new standing order that steers to the base's node and hands back to combat once
`RearmRuntime` restores the seat. Thresholds are TUNE: `<TODO: ammo and hull thresholds, measured
in a playtest, then added to backlog.md's TUNE list>`.

**Model recommendation.** `<TODO: not settled in the session>`

**Verify.** `<TODO: a suite where a bot with an emptied magazine reaches a base on MP1 and is
restored>`

**⚠ Traps.** A base the mission switched off offers nothing (`IA1` has both plain nodes off), so
test on `MP1`. The rearm latch is one per seat and releases only outside every serving base.

# Wave C, lobby and local setup

## C21 ☐ Multiplayer Lobby: Add bot, Fill-to-N, per-row plane/skill/team/callsign, Remove

**Goal.** The host adds a bot row with Add, or creates rows with Fill-to-N; edits each row's plane,
skill, team and callsign; removes a row; new bots join the smallest team (Decisions 5, 12); rows
survive a rematch (Decision 18). Guests see the rows.

**Evidence (confidence: lead-only).** Lobby state is `UI/Menu/DogfightLobby.cs`, the original-
presentation screen `OriginalLobbyScreen.cs` (`docs/architecture/UI.md:1301-1305,1460-1465`). The
host's options and roster reach guests as `DogfightOptionsMessage`, `DogfightRosterMessage`,
`LobbyTeamActionMessage`, `LobbyTeamsMessage` (`NetDogfightMessages.cs`). Team weighting already
counts each row's seats (`DogfightLobby.cs:270-279`).

**Approach.** Bot rows live in the host's lobby roster and ride the existing roster message with
A1's pilot kind. `<TODO: where the controls sit on the original-presentation screen; this is a
remake addition with no original layout to follow>`.

**Model recommendation.** `<TODO: not settled in the session>`

**Verify.** `<TODO: a lobby unit or suite test, plus a look at the controls by the user>`

**⚠ Traps.** The screen layout is a look judgement; bring a capture to the user before settling it.

## C22 ☐ A joining human takes the newest bot's seat in the lobby, a late joiner once the match is back there

**Goal.** A guest who joins a full lobby takes the most recently added bot's seat (Decision 6). A
guest who joins while a match runs lands in the lobby, not the match, and takes the newest bot's
seat when the match is back in the lobby (Decision 7); a host Restart keeps the match running and
changes no seat. A guest who leaves is not replaced.

**Evidence (confidence: lead-only).** A late joiner lands in the lobby, never in a running match
(the user's correction to Decision 7). `WorldEventMessage` code 6 is `SeatLeft`. ENet host peers are
`MaxPlayers - 1` (`Launcher.cs:2758`), so a full field of bots must not block the ENet accept, and a
waiting late joiner holds a peer while every seat is still flown.

**Approach.** On a join into a full lobby, retire the newest bot seat and seat the guest in it. On a
join while a match runs, hold the guest in the lobby as a waiting pilot; when the match returns to
the lobby, retire the newest bot seat and seat the waiting guest, one bot per waiting guest, in join
order. `<TODO: read how a late joiner is held in the lobby today and where "the match is back in
the lobby" is signalled; and what a waiting guest sees in the roster>`.

**Model recommendation.** `<TODO: not settled in the session>`

**Verify.** A loopback suite: host with 15 bots, a guest joins mid-match and stays in the lobby
while the match runs; a host Restart leaves the field unchanged; when the match returns to the
lobby, the newest bot's row leaves the roster and the guest holds its seat on both ends.

**⚠ Traps.** No seat changes hands mid-match, so a swap must never be triggered by Restart. The
seat's channels and sequence counters (`AircraftStateCadence._sequence`, `SessionNet._fireSequence`,
`NetInstruments`) must reset when the seat changes hands, or the guest's first samples in the next
match read as stale.

## C23 ☐ Local join board: bot rows, and the two-pilot minimum counts bots

**Goal.** The local Dogfight setup offers the same bot rows as the lobby, and one human plus one
bot can start a match (Decision 13).

**Evidence (confidence: lead-only).** The menu requires two joined pilots before it starts a
match (`docs/cli.md:166-167`). `DogfightLobby.LocalSeats` counts the host machine's split screen
seats (`DogfightLobby.cs:152-155`). The join board is `UI/Menu/Original/OriginalJoinBoard.cs`.

**Approach.** Reuse C21's bot rows on the local path; the minimum counts humans and bots.
`<TODO: read how the local join board hands its roster to the launch>`.

**Model recommendation.** `<TODO: not settled in the session>`

**Verify.** `<TODO: a test that one human and one bot launch a local match>`

**⚠ Traps.** Shares `DogfightLobby.cs` with C21; run after it, never in parallel.

# Wave D, presentation and measurement

## D31 ☐ Bot tag on the lobby roster and the board; seat-style in-flight markers for bots

**Goal.** The lobby roster and the results board show a bot tag beside a bot's callsign; in flight
a bot wears the same seat marker a human does, with its callsign (Decision 11).

**Evidence (confidence: lead-only).** `VersusHud` iterates human seat rigs only and draws
`P{n}` tags in `SplitScreen.PlayerColor` (`CSVM/src/Flight/Modes/VersusHud.cs:76-78,106-118`). AI
planes appear only through `TargetPool` (`TargetPool.cs:243`, `MarkerName` else `PilotName`).

**Approach.** After A3 a bot is a seat rig, so `VersusHud` may cover it already; `<TODO: check
whether A3's rig lands in VersusHud's Rigs>`. The tag's look is `<TODO: the user's call, from a
capture>`.

**Model recommendation.** `<TODO: not settled in the session>`

**Verify.** A capture of the board and of a bot's marker in flight, shown to the user.

**⚠ Traps.** `SplitScreen.PlayerColor` is a 4-colour palette taken modulo 4
(`CSVM/src/UI/Boards/SplitScreen.cs:76-82,142`), so colours repeat at 16 pilots. That predates bots
(16 humans repeat too) and stays a separate change; file it if D32 shows it confuses play.

## D32 ☐ Crowded free-for-all playtest at the controls

**Goal.** The user flies a full-field Deathmatch against bots and judges whether the bots fight
convincingly in a crowded arena, rearm sensibly and do not pile into terrain.

**Evidence (confidence: lead-only).** The AI's targeting and manoeuvres have been exercised only in
player-centred fights; no suite flies a dozen hostile AI against each other.

**Approach.** A `playtest.md` entry (minted with `New-ItemId.ps1`) once Waves A to C land.

**Model recommendation.** `<TODO: not settled in the session>`

**Verify.** The user's verdict at the controls.

**⚠ Traps.** Check which build is running before reading a symptom as a bot defect.

## D33 ☐ Host cost of fifteen bots, measured

**Goal.** A number for the host's step cost with fifteen bots, and a judgement whether it holds on
the host machines that matter (the user's rig, the Steam Deck).

**Evidence (confidence: lead-only).** Each bot adds an `AiPilot`, a physics body and a 44-byte
state send at 20 Hz per receiver. No measurement exists.

**Approach.** `<TODO: the instrument; read docs/verification.md first, since sim time lags wall
time on the user's rig>`.

**Model recommendation.** `<TODO: not settled in the session>`

**Verify.** `<TODO: the measured baseline and the threshold>`

**⚠ Traps.** Read the sim-clock cadence, not `physics_ms`, on the user's rig.
