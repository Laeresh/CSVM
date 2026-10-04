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

1. ☑ A seat knows whether a human or a bot flies it, and the roster carries that across the wire
2. ❌ `VersusMatch` keys its scores by pilot, so a row survives its seat being reused
3. ☑ The host flies a bot seat with an `AiPilot`; guests see it as a remote seat
4. ☑ CLI twin: `--vs-bots=` / `--vs-bot=` build bot seats in a scripted host

### Wave B, bot behaviour in a Dogfight

11. ☑ Equal target weights for every pilot in a Dogfight
12. ☑ Bots respawn through the host's rotation and follow Limited Lives
13. ☑ Skill tiers, personalities, stock plane with Random, and the callsign pool
14. ☐ Rearm standing order: a bot breaks off to a base when low or badly damaged

### Wave C, lobby and local setup

20. ☑ A local match runs off a seat roster with no wire, so it can hold bot seats
21. ☐ Multiplayer Lobby: Add bot, Fill-to-N, per-row plane/skill/team/callsign, Remove
22. ☐ A joining human takes the newest bot's seat in the lobby, a late joiner once the match is back there
23. ☐ Local join board: bot rows, and the two-pilot minimum counts bots

### Wave D, presentation and measurement

31. ☐ Bot tag on the lobby roster and the board; a bot's callsign on the target marker
32. ☐ Crowded free-for-all playtest at the controls
33. ☐ Host cost of fifteen bots, measured

## Dependency and parallelism notes

A1 blocks everything. A2 is closed (see A2). A3 needs A1 (A3 owns `SessionNet`,
`FlightRoster`/`AiFlightAssembler` and the bot rig). A4 needs A3, and every later item's tests lean on A4. Wave B needs A3 and A4; B11,
B12 and B13 touch different files and can run in parallel, B14 after B12 (a rearm trip and a respawn
share the bot's standing-order state). Wave C needs A1;
C21 and C23 both edit `DogfightLobby.cs`, so they run in sequence, C21 first, and C22 follows C21.
C20 (added on A4's finding) needs A4 and blocks C23; it touches `VersusDirector`, so it does not
run beside B12.
D31 needs A1, A3 and C21. D32 and D33 run last, on the merged tree.

---

# Wave A, seat model and match bookkeeping

## A1 ☑ A seat knows whether a human or a bot flies it, and the roster carries that across the wire

**Goal.** A `NetSeat` can be a bot seat owned by the host's peer, with a skill; the seat roster a
guest receives says which seats are bots; and "this machine flies the seat" is no longer the same
claim as "this seat has a pane".

**Evidence (confidence: traced-to-code).** `NetSeat` (`CSVM/src/Net/NetSeat.cs`) held `PeerId,
SeatIndex, TeamId, IsLocal, Callsign, Unnamed, PlaneNode, Livery, Voice, Score` and had no bot flag.
`IsLocal` picked a pane in `SessionNet.FillSeatRigs` (the cited `:612-614` had drifted to the
method's rig line), and `HumanFlightAdapter` read one `remote` flag for both "no pane" and "pose
from the wire". `NetSeats.Validate` and `NetSession.WellFormed` reject more than 16 seats or gaps
and require seat 0 to be the host peer. The roster entry is 20 bytes (seat, team, flags, plane,
callsign 16 bytes); its flags byte held the host bit, the voice in bits 1 to 3 and the nameless bit
4, leaving bits 5 to 7 free. The repo has no protocol version: peers compare the build's
MAJOR.MINOR (`NetBuildVersion`), and the voice and nameless bits were added the same way, inside
the byte. Two peer-to-seat derivations beyond the plan's list assumed one person per peer:
`NetSession.FliesOnTeam` (a host's bot on team 2 would have shown the host team 2's chat) and
`SessionNet.OnPeerLeft`/`TakeSeatLeft`.

**Approach.** `NetSeat` gains `Pilot` (`NetPilot.Human`/`Bot`), `Skill` (`NetBotSkill`
Novice/Veteran/Ace, values 0/1/2), `IsBot`, and `IsLocal` is renamed `FlownHere` with a derived
`HasPane => FlownHere && Pilot == Human`. The rename made the compiler list every reader, each of
which now names the claim it means. `NetSeats.Bot(hostPeer, seat, callsign, plane, skill, team)`
makes a bot seat (flown here, no pane, callsign cut by `SeatRosterMessage.Carried`). The roster
keeps its 20-byte entry: bit 5 is the bot, bits 6 and 7 the tier; a reader refuses tier 3 or skill
bits on a person, and a patch-older build reads a bot as a host-owned seat. No version bump, since
the wire stays readable by the old layout. `Validate` now requires at least one seat with a pane
(was: flown here), a person at seat 0, and every bot owned and flown where seat 0 is (the host on
every copy); `WellFormed` refuses a bot at seat 0 or a bot entry without the host bit; a guest
never flies a bot entry whatever its handshake run says. `LocalOrdinal` and `LocalSeatCount`
count panes, `FliesOnTeam` counts persons only, and `NetSeats.LeavingWith`/`LeavesWithPeer` (a
person flown elsewhere) decide a guest's leave, so no bot seat ever leaves with a peer. Bots are
skipped by `SessionVoices` (Decision 15). `MaxPlayers = 16` unchanged.

**Model recommendation.** Opus: a wide consumer audit across `Net`, `Launch` and `Session` where a
missed reader fails silently rather than at the compiler.

**Verify.** `NetMessagesTests.SeatRosterCarriesBotsAndTheirSkillBesideTheOtherFlags` (mixed round
trip, flag bits, refusals); `NetSeatTests` (bot seat claims, host roster and guest copy with bots,
bot at 0 or off the host refused, pane ordinal skips a bot, a guest's leave never takes a bot);
`NetSessionTests` (a guest reads the host's bots as host seats it does not fly, a bot does not put
its host on its team's chat, a bot at seat 0 or off the host is malformed). `net-seats` now seats a
host bot beside the pane and two guests and checks it is built paneless with no menu pick and no
pose buffer. The guest side needs no suite of its own: its copy reads the bot as a seat flown
elsewhere (unit), which is the remote-seat path `net-seats` already covers. `FillSeatRigs` itself
is reached only through a session build; its pane test is `HasPane`, the rule `LocalOrdinal`'s
unit covers, and A3's first bot rig is where a live session shows it.

**⚠ Traps.** `IsLocal` has many readers; a missed one silently gives a bot a pane or a human no
input. The `NetNamespaceDependency` test confines engine types to the carriers
(`docs/architecture/Net.md:261-264`), so the pilot kind stays engine-free in `Net/`.

**Verified.** Full `RunTests.ps1` on the merged Wave A tree (0d45b43c): build, units 6228 passed / 0 failed / 3 skipped, engine 515 passed / 0 failed / 2 skipped (6 shards, engine errors clean), goldens 24 hash-identical; exit 0.

## A2 ❌ `VersusMatch` keys its scores by pilot, so a row survives its seat being reused

**Closed as not needed.** Under Decision 7 the seats are fixed for the life of a `VersusMatch`:
`VersusDirector.TryCreate` builds it once per session from the session's seat rigs,
`SessionNet.Seats` is fixed when the session is built and nothing compacts it, a host Restart
zeroes the rows in place, and a guest who leaves keeps a row marked `Left`. Every reader that
outlives the match takes strings before the lobby can swap a seat: `Launcher.ExitSession` calls
`LobbyLanding` while the session is alive, and `DogfightLobby.ScoresOf` bakes names into
`DogfightScore` there. A bot seat carries the host's peer id, so `OnPeerLeft` never matches it.
`OutOfLives` and `CheckAlone` read seat rows, which are stable for the match. No reader needs a
pilot id.

The real defect the re-check found is naming, and it moves to D31: `ScoresOf` reads
`names[seat]`, but `LaunchNames` holds one entry per lobby row (`DogfightLobby.HostRows`, the
host then each seated peer), so names and seats agree only when every peer flies one seat in row
order. Bot seats break that.

The original item text follows for reference.

**Goal (original).** The match keeps one score row per pilot who has flown in it. When a bot leaves and a
human takes its seat, the bot's row stays, marked as left, and the human gets a new row.

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

## A3 ☑ The host flies a bot seat with an `AiPilot`; guests see it as a remote seat

**Goal.** On the host, a bot seat is a `FlightController` driven by an `AiPilot`, sending its state
on its seat channel at the owner cadence. On a guest it is a remote seat rig fed by
`AircraftStateMessage`. Hits on it and by it, its death and its score take the seat paths.

**Evidence (confidence: traced-to-code).** The lead's seam, the AI assembler, was the wrong one.
`AiFlightAssembler.Assemble` loads `PlaneStats.LoadForAi` (the AI def's zone-less armour/health pair
and its `weapons` block), scales the hull by `Difficulty.FactorForSpawn` (Decision 8 refuses that),
jitters it (`WithAiSpawnJitter`) and adds the aircraft to `FlightRoster._ai`. Every guest builds the
same seat through `HumanFlightAdapter` on the player chain, so the two ends would fly different
damage models, and `SessionNet.Mirror` would take only the whole pair. Membership of `_ai` puts it
in `GameSession.CaptureAiAircraft`, which steps it a second time and hands it to
`NetWorldLink.Admit`. Most of the seat path already held after A1, and the lead understated it:
`SessionNet.ReportDeath` gates on `FlownHere`, not on a local human; `VersusDirector.Wire` subscribes
every seat rig's `Downed` to it; `WireSeatCombat`, `BroadcastAircraftState` (on
`NetChannels.ForSeat`), `RouteHit`, `TakeHit` and `SendChangedDamage` all key on `FlownHere`; and
`SeatOfShooter` reads `PlayerIndex` off the seat rigs, so a bot whose `PlayerIndex` is its seat is
charged its kills with no change to scoring. A roster AI's kill stays a no-killer death. The gunner
sweeps `ProjectilePool.CollectVehicleList`, where every seat rig, local and remote, is registered,
through the team gate. `AiFlightAssembler.PreparePilot` arms a gunner only when a rating exists.
An AI-force-path aircraft flies `FlightModel`'s far-field speed-hold plant past 1000 m from the
nearest position its `HumanPositions` gives, and the session's binding (`PlayerPositionsSnapshot`)
reads the host's own panes alone, so a bot instead measures against every person's seat in the field
(`HumanFlightAdapter.PersonSeatPositions`, `net-bot-seat` places one beside the guest 3 km from the
host's plane and reads the full plant).

**Approach (landed).** The seam is `HumanFlightAdapter`, with an AI pilot swapped in. The bot
stays a member of the seat list (`FlightRoster.Humans`, `SessionNet._seatRigs`), which keeps the
spawn walk, the `VersusMatch` row, `VersusHud.Rigs`, markers, colours, the paint stream and every
seat path treating it as a seat, and it is never in `FlightRoster.AiAircraft`, so `NetWorldLink`
never admits it and no opposition count sees it. Its paneless build already skips HUD, camera,
audio, pads and pause key. A bot seat is AI-piloted on every machine (`IsHumanPiloted = !seat.IsBot`),
so both ends play its hits, AI shakes and `ai_crash_*` wreck alike. On the host it binds a new
`AiPilot`, holding its spawn course, armed by `AiFlightAssembler.ArmSeatPilot` (gunner, ordnance,
mode machine, the airframe's ranges) at `SeatRating` 5 and `Difficulty.Hard`, drawn from a new
`Rng.Bots` stream so a host-only draw moves no stream both ends share. Its `PlayerIndex` is its
seat. Team is the seat rule `HumanFlightAdapter` already had: `AimAssist.LobbyTeam(N)` in a team
match, `TeamOfPilot(seat)` in free-for-all. Bots stay silent: `SessionVoices` skips them (A1) and
no AI voice registration reaches a seat. A scripted host adds a bot by putting `NetSeats.Bot(...)`
in the roster its `LauncherContext.NetSeats` carries; the A3 suites do exactly that, and A4's
flags need no other seam. For later items: on both ends `TargetHud.NearestHostile` tracks a bot as
an AI hostile (its fallback marker reads the node name `player{seat+1}`) beside `VersusHud`'s seat
marker, which D31 settles; a bot, like a remote human, has no engine or gun audio on any machine.

**Model recommendation.** Opus: choosing the seam meant reading both assembly paths, the session's
step phases and the world link together; the code change itself is small.

**Verify.** `net-bot-seat` (clean loopback) and `net-bot-seat-lossy` (30 ms, 25 per cent loss),
`CSVM/src/Testing/NetBotSuites.cs`: a host pane, a guest seat and a host bot. The host flies the
bot with an armed pilot under seat index 2 and admits no world AI on either end; the guest builds a
remote, pilotless, AI-flown copy whose path traces the host's (0.00 m clean, 1.42 m lossy mean at
the best lag, against 3600 m for the other aeroplane); the gunner ranks a hostile seat as its quarry;
a bot round on the host's copy of the guest lands on the guest's own aeroplane, a guest round on
its copy of the bot lands on the host's bot and is mirrored back; the bot's lethal claim on the
guest scores the bot and posts "Destroyed by bot", the guest's on the bot scores the guest, both
boards agree, and the downed bot is granted its return on both ends and flies on its pilot. Every
lossy wait reads every condition its checks read (DET-17). `net-seats` checks the bot's armed pilot,
seat index, team and engagement range, and that the roster's AI list stays empty.

**Verified.** Full `RunTests.ps1` on the merged Wave A tree (0d45b43c): build, units 6228 passed / 0 failed / 3 skipped, engine 515 passed / 0 failed / 2 skipped (6 shards, engine errors clean), goldens 24 hash-identical; exit 0.

**⚠ Traps.** DET-17 (`docs/verification.md:283`): a lossy loopback suite must wait on every
condition its checks read. ⚠ Never assemble a bot through `FlightRoster.SpawnAi`: it would carry the
AI def's damage model, be stepped twice and be replicated again through `AiStateMessage`.

## A4 ☑ CLI twin: `--vs-bots=` / `--vs-bot=` build bot seats in a scripted host

**Goal.** A scripted `--vs --net-host` run can add bots, so suites and probes drive the feature
without the menu. A local `--vs` seats none (see Approach).

**Evidence (confidence: traced-to-code).** `SessionSpec.Parse` is last-occurrence-wins for every
flag, and its one list-valued AI flag, `--ai=`, is a comma list of entries with colon-separated
`key=value` parts, so the proposed repeatable `--vs-bot=` became a comma list instead. A flag in
the wrong mode is warned and dropped in `Resolve` (`--ctf`, `--zvz`), and an unreadable word keeps
the default and is named (`--difficulty=`, `--ai-targeting=`). `Launcher.BuildCliNetRoster` seats
this machine's panes, then one seat per linked peer, each `break`ing at `NetSeats.MaxPlayers`; the
menu's `VersusLaunchField` is a separate path the menu alone takes, so menu bots stay C21's. Every
wire launch hands the session `StockAirframes.Nodes` as its airframe list, and the roster carries a
plane as an index into it, so a bot's plane must be a stock node. A local match has no seat roster:
`LauncherContext.NetSeats` is null and `SessionNet.Seats` empty. `VersusDirector` equates a seat
roster with a wire: with seats and no wire it builds no rotation, asks a null wire for every return
(`AskSpawn`), reports deaths to a `ReportDeath` that needs a link, refuses the rematch
(`RematchIsTheHosts`) and posts no split-screen kill lines (`TakeKillLine`).

**Approach (landed).** `--vs-bots=N` adds N bots at the defaults: the host's own plane
(`SessionSpec.PlaneName`, the rule a command-line guest's seat already takes), veteran, no team, and
the callsign `Bot <n>` by place in the field. `--vs-bot=<plane>[:skill=<tier>][:team=<n>][:name=<callsign>][,...]`
names bots that take the first places. `<plane>` is a stock node or empty, `skill=` reads
`Difficulty.Parse`'s words onto `NetBotSkill`, `team=` is a lobby team 0 to `NetTeamBook.MaxTeams`,
and `name=` is the callsign. Random waits for B13. An unreadable part keeps its default with a
warning. `SessionSpec.ResolveBots` drops the flags with a warning without `--vs`, on a guest
(`--net-join`) and in a local match, and cuts the field to the seats `Players` leaves. The resolved
list is `SessionSpec.VsBots`. `BuildCliNetRoster` appends it after the guests through
`NetSeats.AddBots`, which leaves a bot out once guests fill the 16 seats and says so in the log;
the roster then carries the bots to every guest. Command-line pilots fly on lobby team 0, so a
`team=` bot puts the match in team mode beside unteamed people until a flag sets a human's team.
Local `--vs` with bots is not built. The seat model works for it (`HumanFlightAdapter` and
`SessionNet.FillSeatRigs` take any roster), but `VersusDirector` must first tell "has a seat roster"
from "has a wire" at the places Evidence lists; the smaller route is a host with no guests on an
in-process wire, untried, whose chat, pause and start gate would need checking. C23 depends on one
of the two.

**Model recommendation.** Sonnet would do for the parser and roster; Opus was used because the
local-match finding needed `VersusDirector`'s wire assumptions read whole.

**Verify.** `VsBotFlagTests` (18 units): field order and defaults, the skill vocabulary, refusals
of an unreadable skill, team, plane, part or count, last flag wins, guest and local scope, the
16-seat cut, and `NetSeats.AddBots` seating after the guests and leaving bots out of a full field.
Two processes on loopback, each through `RunProbe.ps1` on the hidden desktop, from one PowerShell
call (`$r` the worktree, the guest started 4 s after the host, both waited on):
`RunProbe.ps1 -TimeoutSec 180 --vs --mission=MP1 --mute --debug-net-trace --net-host=127.0.0.1:47731 --vs-bots=1 --vs-bot=player_fury:skill=ace:name=Ace --screenshot=$r\.scratch\a4-host.png --frames=900`
and `RunProbe.ps1 -TimeoutSec 180 --vs --mission=MP1 --mute --debug-net-trace --net-join=127.0.0.1:47731 --screenshot=$r\.scratch\a4-guest.png --frames=900`.
Both exit 0. The host logs "host roster of 4 seat(s), 2 of them bots" and its bot's gunner takes P1;
the guest, given no bot flag, joins "as seat 1 of 4", and its trace shows both bot seats as copies
moving with the host's own pose (seat 3 about 1600 m over the run, within 5 m of the host's).
`net-bot-seat`, `net-bot-seat-lossy`, `net-seats` and the whole `net-` filter stay green.

**Verified.** Full `RunTests.ps1` on the merged Wave A tree (0d45b43c): build, units 6228 passed / 0 failed / 3 skipped, engine 515 passed / 0 failed / 2 skipped (6 shards, engine errors clean), goldens 24 hash-identical; exit 0.

**⚠ Traps.** Name `127.0.0.1` in a scripted run; a wildcard bind puts a firewall dialog on the
user's screen. A bot's plane outside `StockAirframes.Nodes` reaches a guest as no plane at all.

# Wave B, bot behaviour in a Dogfight

## B11 ☑ Equal target weights for every pilot in a Dogfight

**Goal.** In a Deathmatch a bot ranks a human and another bot with the same base weight, so the
choice comes down to the ranking's other terms (Decision 10).

**Evidence (confidence: traced-to-code).** `AiTargetRanking.Score` picks the base weight as
`c.IsPlayer ? PlayerWeight : BaseWeight` (0.7 against 1.0, 360 rank units at the 1200 scale), and
that is the only place the weight reads `IsPlayer`. The ranking recognises a player through
`RankedTargetCandidate.IsPlayer`, which `GunnerAcquisition.Select` fills from the candidate's
`FlightController.IsHumanPiloted`, so after A3 a person seat (local or remote) carries it and a bot
seat does not, as the lead said. The other places a human reads differently in an AI's choice were
checked and none needs the gate: the `"player"` role in `primary_target` and `rating_biases`
(`GunnerAcquisition.Select`, `EnemyAircraftRanks`, `AiGunner.IsPrimaryTarget`) is reached only
through a roster's fields, and `ArmSeatPilot` sets neither (only `CampaignRoster` and
`InstantActionDirector` write them); the wingman +0.4 reads `Pilot.Escort`, which a bot has not;
deconfliction (`AlliedAttackers`) counts team members, never humans; `AircraftFirst` is by class;
the hold (`KeepsStandingTarget`) and the withdrawal walk (`AnyAircraftRanks`, `EnemyAircraftRanks`)
compare against `NotRanked` only, which no weight reaches. `TargetPool` is the player's HUD pool
and holds no human term. `SurfaceGunner` and `TurretController` are world gunners, not pilots, and
keep the decoded weight. One human-specific behaviour stays, and it is not target choice: the mode
machine's lay-off assist (`AiModeMachine.UpdateLayOff`, fed `PursuitQuarry.IsHumanPiloted`) lets a
bot ease off while the human it targets is chasing it and falling behind, and `PreparePilot` arms a
bot with `AssistEnabled = !--no-assist` like any other AI. A bot fighting another bot never lays off.

**Approach (landed).** The switch is a per-pilot setting given at arming time,
`AiGunner.PlayersPreferred` (default true). `AiTargetRanking.Score` and `SelectBest` take
`playersPreferred` (default true, the decoded constant), and the weight is `PlayerWeight` only when
the candidate is a player AND the shooter prefers players. `GunnerAcquisition.Select` passes the
gunner's setting to `SelectBest` and to the primary-target log's `Score`. The candidate's `IsPlayer`
and `IsHumanPiloted` stay truthful, so the aim assist, the force path and the `"player"` role are
untouched. The hook is three lines in `AiFlightAssembler.ArmSeatPilot`, right after
`PreparePilot(spawn, stats, Rng.Bots);`: `if (pilot.Gunner is { } gunner) { gunner.PlayersPreferred
= false; }`. The gate is on the bot, not on the session: Decision 10 asks whom a bot prefers, and a
bot exists only in a Deathmatch (Decision 1). `--ai=` aircraft in a `--vs` session are not match
participants (no seat, no board row, no score) and are a development instrument for flying the
decoded AI, so they keep the preference; a session-wide gate would also have needed the mode in
`AiFlightAssembler`'s policy for no player-facing gain. A local bot built for C20 arms through
`ArmSeatPilot` and so takes the setting with no further change.

**Model recommendation.** Sonnet would do: the change is a parameter and one arming line. The audit
of what else reads a human in target choice is the part that wants care, and it is recorded above.

**Verify.** `AiTargetRankingTests.AShooterThatDoesNotPreferPlayersRanksAPersonAndABotAlike`: a
person and a bot at equal geometry score one weight and one rank with the preference off, and the
same pair stays 360 apart under it (able-to-fail control); a bot 100 m nearer loses the pick under
the preference and wins it without. `ai-gunnery` (live acquisition) gains the same A/B on real
aircraft: with the preference a human at 800 m out-ranks an AI rival at 700 m, with it off the rival
wins. `net-seats` checks the host's bot is armed with `PlayersPreferred` false. Units 6229 passed /
0 failed / 3 skipped (`RunTests.ps1 -SkipEngine -SkipGoldens`); engine 69 passed / 0 failed with
`-Filter 'ai-,instant-action,wingman,target,campaign-roster,net-bot-seat,net-seats' -Shards 4
-SkipUnits -SkipGoldens`, engine errors clean.

**Verified.** Full `RunTests.ps1` on the merged Wave B tree (faa4c0f4, B11 to B13): build, units 6245 passed / 0 failed / 3 skipped, engine 517 passed / 0 failed / 2 skipped (6 shards, engine errors clean), goldens 24 hash-identical; exit 0.

**⚠ Traps.** The campaign and Instant Action keep the preference; only a bot seat's gunner turns it
off. ⚠ Do not gate on `IsHumanPiloted`: the aim assist, the AI force path and the `"player"` role
read it. Whether the lay-off assist should also treat a bot quarry like a human one is a separate
question Decision 10 does not answer; it is left for the D32 playtest.

## B12 ☑ Bots respawn through the host's rotation and follow Limited Lives

**Goal.** A downed bot respawns after the crash camera time at a point the host's
`VersusSpawnRotation` grants, whatever Auto Respawn says (Decision 16), and stays down when out of
lives.

**Evidence (confidence: traced-to-code).** A seat's return reaches the rotation the same way for a
person and a bot. `VersusDirector.Wire` arms every seat rig with `AutoRespawnAfter = RespawnDelay`
(3 s, the remake's own value, `docs/org/multiplayer-scoring.md`) and, on a wire, a
`RespawnRequest` of `AskSpawn(seat)`. The crashed branch of `FlightController`'s sim step calls it
once `AircraftLifecycle.TickAutoRespawn` is due, and on the host `AskSpawn` calls `GrantSpawn`
directly, which walks the rotation, broadcasts the `SpawnMessage` and places the aeroplane through
`TakeSpawn` and `RespawnAt` on every peer. With Auto Respawn on, A3's reading held. With it off,
`Wire` set `RespawnOnFire` on every rig, and `TickAutoRespawn` then waits on `FirePressed`, which on
a paneless bot reads an empty keyboard and pad binding and never fires: with the exemption removed
the bot was still down 420 steps after a 180-step crash camera. The pilot was not reset either:
`FlightController.Respawn` only calls `AiPilot.ClearStun`, so the gunner's quarry (held 20 s by
`TakeTarget`), the mode machine's mode, pursuit anchor, dwell stamp, reaction and evade flag, the
rocketeer's lockouts and the course orders all outlived the aeroplane (the reset removed, the
respawned bot read quarry P1, mode pursue, and a course of 0° against a nose of 122°). Limited
Lives needed no change: the score rows are per seat and a bot seat is a row, `CheckAlone` counts
every row neither `Left` nor `OutOfLives` as a pilot on its own team (a teamless seat is a team of
one), `GrantSpawn` refuses a spent seat, and `HoldSpentPilots` sets `Spectating`, which turns off
`RespawnOffered` so a spent bot never asks. A guest reads the same deaths off the host's scores.

**Approach (landed).** `VersusDirector.Wire` sets `RespawnOnFire = !VsAutoRespawn && !IsBot(seat)`,
so a bot seat takes the auto path on the same crash camera time as a person's auto-respawn. The
reset is one method, `AiPilot.ResetForSpawn(pos, lookAt, throttle)`: it drops the gunner's quarry,
rank and hold, resets the launcher (`AiRocketeer.Reset`, both lockouts) and the mode machine
(`AiModeMachine.Reset`: patrol, no anchor, reaction, stun, climb-out or wait, keeping the clock and
the maneuver history), releases any danger-zone run, and holds the new placement's course and
lever. Orders a mission or a launch set (net, escort, primary target, `AutoTarget`, ratings) stay.
It runs off a new `FlightController.Respawned` hook, invoked at the end of every `Respawn` once the
new pose stands, which `HumanFlightAdapter` sets for a bot seat on the host beside A3's arming; the
hook also re-arms the AI collision window (`ArmSpawnTimers`) the first spawn took. The hook rather
than `Respawn` itself, because `Activate` (wave and generator launches) and every mission AI also
respawn through it and must keep their orders; the hook rather than `VersusDirector`'s grant,
because the rematch's opening grant, a local match's `RespawnPlacement` (C20) and a suite's
`RespawnAt` all reach the bot through `Respawn`. For B14: the rearm standing order clears in
`ResetForSpawn`.

**Model recommendation.** Sonnet would do the code, which is small. Opus was used because the
reset's placement needed every caller of `FlightController.Respawn` read (grants, rematch,
`Activate`, the suites' placements) and the lives logic traced across `VersusMatch` and
`VersusDirector` to show it needed no change.

**Verify.** New suite `net-bot-respawn` (`CSVM/src/Testing/NetBotSuites.cs`, clean loopback,
weight 15.3): two host and guest pairs, each with two bots. Under `--vs-no-respawn` the bot and the
guest's own seat go down on one step; the bot is back after 180 steps (the 3 s crash camera) on the
entry the host granted, standing on it, with one grant, and the guest places its copy on the same
entry; its pilot, given a quarry, a pursuit and a stale course before the death (checked as a
control), comes back with no quarry, in patrol with no anchor, reaction or stun, holding the new
placement's heading, altitude and lever, and flies on the same pilot. The guest's seat, downed on
the same step, is still down on both machines (control). Under `--vs-lives=1` the first bot's
death leaves it down and spectating on both machines with no grant while the match runs; the
guest's death leaves the host and the second bot and the match still runs, which is a living bot
counted as an opponent; the second bot's death ends the match on `NobodyLeft` on both machines.
Deaths there are unattributed, since three kills to the host reach the default kill target and end
on the score first. With the bot exemption removed the suite fails eight checks; with the reset
removed it fails the three pilot-state checks and nothing else. Clean link only: every reading is a
grant, score or match-state message on the reliable ordered channel, and `net-bot-seat-lossy`
already carries a bot's grant over loss (DET-17 does not bite). Units: `AiModeMachineTests.AResetLeavesNoChaseStunOrWaitBehind`,
`AiPilotTests.AResetForSpawnHoldsTheNewPlacementWithNoEngagementLeft`,
`AiRocketeerTests.AResetLauncherCarriesNoLockoutOrSelection`. Run in `bots-b12`: `RunTests.ps1
-Filter net- -Shards 4 -SkipUnits -SkipGoldens -SkipHitch` 57 passed / 0 failed; `-Suite
versus-spawn-rotation,versus-spawn-net-table,flight-live-respawn-gate,death-respawn-rest-pose,splitscreen-listeners`
5 passed; units 6231 passed / 0 failed / 3 skipped.

**Verified.** Full `RunTests.ps1` on the merged Wave B tree (faa4c0f4, B11 to B13): build, units 6245 passed / 0 failed / 3 skipped, engine 517 passed / 0 failed / 2 skipped (6 shards, engine errors clean), goldens 24 hash-identical; exit 0.

**⚠ Traps.** Never move the reset into `FlightController.Respawn`: a mission AI's `Activate` goes
through it and would lose its net and orders. `ResetForSpawn` keeps `AutoTarget`, which
`net-bot-seat` turns off and relies on through its placements. A match ending reads `ScoreTarget`,
not `NobodyLeft`, when the death that empties the field also reaches the kill target, because
`CheckAlone` returns once the match is complete.

## B13 ☑ Skill tiers, personalities, stock plane with Random, and the callsign pool

**Goal.** A bot flies a stock plane (Random resolved by the host) with its stock loadout and livery,
at novice/veteran/ace, with a rolled Instant Action personality and a callsign from the shipped
pilot names (Decisions 8, 9, 11).

**Evidence (confidence: traced-to-code).** The tier offset is -2/0/+2 on the nine ratings, clamped
to [0, 9]; the original's 0.75/1.0/1.25 hull scale is not taken (`docs/formats/instant-action.md`
"What novice / veteran / ace becomes"). The personalities are `InstantActionRuntime.RandomPilotStats`,
Instant Action's own roll, `rand() % 5` per aircraft over four authored rows and the flat row of
fours, so equal odds (`instant-action.md` "A wave enemy's nine pilot stats"). `IDS_IA_DIFFICULTY`
3695 to 3697 read novice, veteran, ace, which `Difficulty.Parse` already takes. **The lead's pilot
names were wrong:** langui 500 to 599 holds `IDS_DEFAULTPLAYERNAME` "Nathan Zachary" (500), the
quality words Poor to Excellent (501 to 505), the campaign's named planes (511 to 517, "Gypsy
Magic") and format strings; no pilot-name pool and no skill ratings. The shipped pilot names are the
message table's character rows, ids 13000 to 13036 (`MSG_JACK_NAME` and the rest), which
`docs/formats/missions.md` and `strings.md` now record. The trap does not bite: a bot never flies
team id 1 or 0, since a free-for-all seat flies `TeamOfPilot(seat)` = 10 + seat with seat 0 always a
person, and a team seat `LobbyTeam(N)` = 40 + N; `net-bot-skill` puts a bot on lobby team 1 (id 41)
and reads `Difficulty.AppliesTo` true. The ace exemption (`spawn.Ace`, roster slot 67) defaults
false and no bot sets it. The Dogfight picker offers all eleven stock airframes
(`InstantActionFeature.Airframes` through `PlanePickerRoster`), so Random draws over
`StockAirframes.Nodes`. The launcher had no string table loaded at roster time; it holds
`_messagesPath`, and one `Messages.Load` there is the cheapest correct path.

**Approach (landed).** New engine-free `Session/Roster/BotSeats.cs`. `Ratings(personality, tier)`
shifts each slot through the new `Difficulty.ShiftRating` (the ungated half of
`SkillRatingForSpawn`). `AiFlightAssembler.ArmSeatPilot` takes the seat's `NetBotSkill`, rolls
`Personality(Rng.Bots)` first, and passes the shifted vector as `RosterSkills` with `Difficulty.Hard`
(k 0), so the team gate cannot reach the tier at all; `--ai-attack=N` still pins every slot. It logs
`bot: '<plane>' <tier> on ratings ...`. `HumanFlightAdapter`'s call line passes `seat!.Skill`. The
hull is the seat path's `PlaneDamage.For(stats)`, which no tier reaches. `SessionSpec`: a `--vs-bot=`
plane of `random` or empty is Random (null), an unknown one warns and is Random, `--vs-bots=N`
defaults to Random, and an unnamed bot's callsign stays empty for the host to draw.
`Launcher.BuildCliNetRoster` resolves through `BotSeats.Resolve` on a new `Rng.BotField` stream (read
through `IntSeedFor`, a function of the master alone): Random over the eleven nodes, then a callsign
per unnamed bot from `CallsignPool`, shuffled once, skipping every seat's callsign and every
`name=`, falling back to `Bot <n>` when spent. The pool is 28 people (13001 to 13036 less the
player's "Zachary" and six rows naming an aircraft or role), brought within the Callsign box's 12
characters by dropping whole words from the front, the user's ruling over keeping words from the
front (`Winthrop`, `Crawford`, `Black Swan`, `Von Beck`, `John Howard`), 28 distinct. A `name=` is
cut hard at 12, as the box cuts typing. Loadout and livery are
the seat path's stock ones already: a bot has no menu pick, and `SchemeFor` paints it the Fortune
Hunters default as a stock human pick. `NetSeats.Bot`/`AddBots` are unchanged. For C21: the lobby's
Random and callsign rows reuse `BotSeats.Resolve` (or its two halves) on the host; the personality
is rolled at arming, so it needs no field.

**Model recommendation.** Sonnet would do; the data reading (which strings are names) was the only
judgement.

**Verify.** `BotSeatsTests` (10 units): every row's ace ratings are +2 and novice -2 clamped (row 0
worked through), a tier is `Difficulty`'s number and takes no team gate, 10000 draws split 2000 per
row, Random lands on stock nodes and a named plane is kept, one seed seats one field, the pool's
cut and exclusions, callsigns unique and at most 12 with the people's names skipped and the `Bot <n>`
fallback, a `name=` cut, the shipped pool (27 names), and a loopback guest reading the host's
Random-resolved plane, callsign and tier off the roster while its own spec seats nothing.
`VsBotFlagTests` (21) updated for Random and the empty callsign. New engine suite `net-bot-skill`
(`NetBotSkillSuites.cs`): a pane and three bots on one airframe; each bot is armed on exactly the
values a personality row at its tier gives (novice row 1 at 2 1 2 3 5 3 1, ace row 2 at 5 6 5 9 5
8 6, veteran the flat row), the novice and ace match no unshifted row (control), every bot passes
the hostility gate, each bot's armour, health and part maxima and stock loadout equal the pane's,
and the novice enemy scale would have cut that hull (control). Two processes on loopback through
`RunProbe.ps1` (host `--vs --mission=MP1 --mute --net-host=127.0.0.1:47743 --vs-bots=3
'--vs-bot=random:skill=ace,player_fury:skill=novice:name=Sir Charles Emmett' --frames=600`, guest
`--net-join=127.0.0.1:47743`, 4 s apart): both exit 0, the host seats Cabbie, Sir Charles, Graham
Kays, Ilsa and DK on bhawk, fury, kestrel, peacemaker and balmoral, and the guest builds the same
seven airframes seat for seat. Units 6241 passed / 0 failed / 3 skipped; `net-` 57/57 on 4 shards.

**Verified.** Full `RunTests.ps1` on the merged Wave B tree (faa4c0f4, B11 to B13): build, units 6245 passed / 0 failed / 3 skipped, engine 517 passed / 0 failed / 2 skipped (6 shards, engine errors clean), goldens 24 hash-identical; exit 0.

**⚠ Traps.** `Difficulty` treats team 1 as exempt; a bot never flies it today, and the shifted
vector with `Difficulty.Hard` keeps the tier off that gate should one ever do so. An earlier probe
pair passed `--debug-net-trace` and both ends died at quit in `EnetTransport.Serve`
(`ObjectDisposedException` on `ENetMultiplayerPeer.GetConnectionStatus`, the service thread polling
a peer Godot had already disposed); a rerun exited 0, so it is an intermittent shutdown race in code
this item does not touch.

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

## C20 ☑ A local match runs off a seat roster with no wire, so it can hold bot seats

**Goal.** A local split-screen or solo Deathmatch can carry a seat roster (its panes plus bot
seats) and run without any network link: bots respawn, score and rematch exactly as in a network
match. This is the user's ruling on A4's finding (decouple roster from wire, rather than running a
local bot match as a guestless network host).

**Evidence (confidence: traced-to-code).** A4's reading held in full and was reproduced: with
`VersusDirector`'s wire test put back to "the roster is not empty", the new suite fails 15 checks
(every return asked of a null host, no kill scored, no death line, no return in 420 steps, no
rematch placement, a spent bot never spent and the one-life match never ending). The seat model
needed nothing: `SessionNet` takes `Seats` from `ctx.NetSeats` with no transport, `FillSeatRigs`
builds the paneless rigs, and `HumanFlightAdapter` arms a bot's pilot and its `Respawned` hook for
any seat it flies. One reader beyond A4's list: `KillLines` hooks the panes alone, so a bot's death
reached no death line even once `TakeKillLine` posted with no wire.

**Approach (landed).** The reader list, each now asking the question it means. `VersusDirector`:
`Wired` (a link and a roster) and `Guest` (wired, not host) replace `NetSeats.Count > 0`;
`RematchIsTheHosts` is `Guest`; `Wire` builds the rotation unless `Guest`, sets `RespawnRequest =
AskSpawn` only when `Wired` and otherwise `RespawnPlacement` off the local rotation, and its Downed
handler reports to the host only when `Wired`, otherwise scoring the death and posting the death
lines itself for every seat (a bot is named by its callsign, a pane by its player tag);
`TakeKillLine(victim)` only says the seat is the match's, and `KillLines` no longer passes a
killer; `Restart`'s wire branch runs only when `Wired`, and its local branch respawns every seat
rig rather than the panes. Already wire-gated and unchanged: `WireSpawns`, `WireMatchState`,
`WireFlags`, `WireZeppelinVersus`, `ScoreDeath`, `TakeScore`, `SendScore`, `SendMatchState`,
`TakeMatchState`, `AskSpawn`, `GrantSpawn`. Roster readings that are right with no wire and
unchanged: `WireRearmBases`' `IsLocal`, `PostLivesLeft`, `IsBot`/`IsLocal`/`HasPane`,
`SeatTeams`/`SpawnTeams`. `SessionNet`: every wire step, `ReportDeath`, `RouteHit`, `SendFire`,
`SendChangedDamage`, `BroadcastAircraftState` and `TraceStep` gate on `Link`; `FillSeatRigs`
reads the roster alone; doc change only. `SessionVoices.RegisterPlayers` branched on the roster's
size and now takes `onWire` from `GameSession`, so local panes stay voiceless with a roster and a
bot stays silent. `GameSession`: `RestartOffered` and the pause overlay already read `Link`; the
cutscene's scripted seat (`Seats.Count > 0 ? _seatRigs[0]`) is the first pane either way;
`FieldPositionsSnapshot` now counts a local bot, as a network host counts its own; the seat-voice
prewarm reads voice 0 as none. `SessionSpec.ResolveBots` no longer refuses a local match.
`Launcher.LocalVersusField` builds a local `--vs` roster right after `OpenCliNet` when no wire
opened (so a `--net-host` whose socket failed flies its bots locally): `NetSeats.LocalPanes` (P1
upward, no plane named, so each pane flies its own pick as without a roster), then
`NetSeats.AddBots` over `Launcher.ResolveBots` (the host's resolution on `Rng.BotField`), then
`Validate`. With no transport there is no peer id; every local seat carries
`NetSeats.OfflinePeer` (`NetSession.NoPeer`), so seat 0 is the first pane and the host-owned-bot
rule holds. **A local match with no bots keeps no roster**, the cheaper of the two: a roster for
every local match would move pane-visible readings (each pane's `PilotName` becomes `P1`/`P2`,
`FieldPositionsSnapshot` switches source) and put the seat's plane ahead of Instant Action's
override in `HumanFlightAdapter`, all for no gain. A local match never sets `End` (the wire's
reason byte), so "No Enemies Left" is still not posted locally; the match does end on
`AllAlone`. For C23: a menu launch runs `StartSessionFromMenu`, whose `TakeNetLaunch` sets
`_netRoster = null` and returns when there is no wire; after it, with bot rows on the join board,
set `_netRoster` to `NetSeats.LocalPanes(planes.Count)` plus `NetSeats.AddBots(seats,
NetSeats.OfflinePeer, rows)` and `Validate`, the rows' Random planes and blank callsigns resolved
through `BotSeats.Resolve` (as `Launcher.ResolveBots` does) unless the board already holds real
ones. `SessionSpec.FromMenu` carries no `VsBots`, so `LocalVersusField` (which reads the spec) does
not serve a menu launch as it stands. Nothing else moves: pane seats name no plane, and pads, fits
and custom planes index by `LocalOrdinal`, which counts panes alone. `CloseNetLaunch` clears
`_netRoster` only when a wire existed, so a local roster survives a Restart and the next menu
launch's `TakeNetLaunch` clears it.

**Model recommendation.** Opus: the reader audit across four files, where a missed one fails
silently in a wired session.

**Verify.** New suite `versus-local-bot` (`CSVM/src/Testing/LocalBotSuites.cs`, weight 13.0, MP1,
roster from `Launcher.LocalVersusField`, opened through `NetCombatSuites.Ends` with no transport):
auto respawn, one pane and one bot: no link, two seat rigs, the bot armed with `PlayersPreferred`
off, both seats' returns local (control: the pane is a person's); the bot's kill of the pane and the
pane's of the bot each score the killer and post a line naming the bot's callsign; each comes back
after the crash camera on a table entry, the bot on the same pilot with its quarry dropped; a
`Restart` with the bot down and the pane lifted zeroes the board and places both on their opening
entries. Auto Respawn off: the bot returns after 180 steps without Fire Guns while the pane downed
on the same step still waits (control). One life, two bots: the first bot stays down spectating and
the match runs (control), the second's death ends it with `AllAlone`. Two panes and no bots: no
roster, the seats are the panes, a kill scores and names `P2`, the victim returns on an entry.
Able-to-fail: the wire test as before the change fails 15 checks (listed under Evidence); with the
local death-line post removed, the three death-line checks fail. Units: `VsBotFlagTests` 22 (a
guest still refused, a local match seats its bots and is cut by its panes, a local roster's panes
then bots on `OfflinePeer` with a stray-peer bot refused). Probe through `RunProbe.ps1 --vs
--mission=MP1 --mute --vs-bots=2 --vs-bot=player_fury:skill=ace:name=Ace --frames=900`: exit 0,
"local roster of 4 seat(s), 3 of them bots", three bots armed. Runs: `RunTests.ps1 -SkipEngine
-SkipGoldens` units 6246 passed / 0 failed / 3 skipped; `-Filter net- -Shards 4 -SkipUnits
-SkipGoldens` 58 passed / 0 failed; `-Filter 'vs-,versus-,splitscreen,hud-kill-line,air-to-air,
incoming-fire-cues,death-respawn-rest-pose,menu-launch-return,menu-player-setup-journey,
menu-original-lobby,pause-sheet,voice-runtime,ai-voice,flight-live-respawn-gate' -Shards 4`
24 passed / 0 failed, engine errors clean.

**Verified.** <pending orchestrator run>

**⚠ Traps.** Never read the roster's size as "on a wire" again: a local match with bots holds a
roster (`VersusDirector.Wired` carries the warning). A local roster's panes must name no plane, or
the seat's plane overrules the pane's own pick.

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
Bot rows count toward the team-launch rule (`NetTeamBook.Check`, fed by `DogfightLobby.LaunchRefusal`
with each row's team): a host plus bots all on one team is "one standing team", which LAUNCH!
must refuse with langui 10519. GitHub issue #140 reports a Deathmatch that launched with every
player on one team through some route round that check; read its state before wiring bot rows
into the refusal, so the bot path does not add a second route round it.

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

## D31 ☐ Bot tag on the lobby roster and the board; a bot's callsign on the target marker

**Goal.** The lobby roster and the results board show a bot tag beside a bot's callsign; in flight
a bot is marked as any aircraft is, and the target marker's name line shows its callsign
(Decision 11).

**Evidence (confidence: lead-only).** `VersusHud` iterates human seat rigs only and draws
`P{n}` tags in `SplitScreen.PlayerColor` (`CSVM/src/Flight/Modes/VersusHud.cs:76-78,106-118`). AI
planes appear only through `TargetPool` (`TargetPool.cs:243`, `MarkerName` else `PilotName`).

**Re-aimed at `TargetHud` by GitHub issue #141.** That issue deletes `VersusHud` in every versus
mode: its permanent `P1`/`P2` seat markers predate `TargetHud`, which now marks players as the
original does, and the match status line moves out into its own element. So a bot does not get a
seat marker; it is found and marked through `TargetHud` like any other aircraft, and Decision 11's
"the in-flight marker shows only the callsign" means `TargetHud`'s name line shows the bot's
callsign while it is the selected target. A3 found that `TargetHud.NearestHostile` already tracks
a bot on both ends, but its fallback name reads the node name `player{seat+1}`, not the callsign:
set the bot controller's `PilotName`/`MarkerName` from the seat's callsign on both ends. If #141
has not landed when D31 starts, do not build anything on `VersusHud`.

**Approach.** The tag's look on the lobby roster and the board is `<TODO: the user's call, from a
capture>`.

Name the finished match's Game Scores rows from the session's seat roster, not the lobby rows
(taken over from A2's re-check). `DogfightLobby.ScoresOf` reads `names[seat]` from `LaunchNames`,
which holds one entry per lobby row, so a bot seat, or a host flying split-screen seats (a lead,
not verified), misnames rows. Take the snapshot in `Launcher.LobbyLanding`, which runs while the
session is alive: build each row from the seat's callsign and A1's pilot kind, keep `LaunchNames`
as the fallback for a launch with no seat roster, and add a bot flag to `DogfightScore` for the
Game Scores tag. The in-flight board (`VersusBoard.Build`, which has only the match today) reads
the kind from the session's seat list by `PlayerIndex`. Test: a three-seat match whose rows are
named off a seat roster ordered differently from the lobby rows.

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
