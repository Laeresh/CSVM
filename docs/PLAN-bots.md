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
14. ☑ Rearm standing order: a bot breaks off to a base when low or badly damaged
15. ☑ A bot takes a person's world-contact rule: it grazes and bounces, never destroyed outright
16. ☑ A bot's world bounce matches a person's in the same plane
17. ☑ A bot rearms when its rockets run out, not its guns
18. ☑ A bot fires rockets at the wingman's rate, a failed roll still launching

### Wave C, lobby and local setup

20. ☑ A local match runs off a seat roster with no wire, so it can hold bot seats
21. ☑ Multiplayer Lobby: Add bot, Fill-to-N, per-row plane/skill/team/callsign, Remove
22. ☑ A joining human takes the newest bot's seat in the lobby, a late joiner once the match is back there
23. ☑ Local join board: bot rows, and the two-pilot minimum counts bots

### Wave D, presentation and measurement

31. ☑ Bot tag on the lobby roster and the board; a bot's callsign on the target marker
32. ☑ Crowded free-for-all playtest at the controls
33. ☑ Host cost of fifteen bots, measured

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

## B14 ☑ Rearm standing order: a bot breaks off to a base when low or badly damaged

**Goal.** A bot whose ammo runs low or whose hull is badly damaged flies to the nearest base that
serves it, is restored there, and rejoins the fight (Decision 14).

**Evidence (confidence: traced-to-code for the seams; direction-sound-magnitude-TUNE for the two
thresholds and the approach geometry).** The lead's ammo line was the wrong path:
`AiFlightAssembler`'s `PylonOrdnance` is the world AI's, and a bot is built on the seat path, where
`HumanFlightAdapter` sets `InfiniteAmmo` from `--infinite-ammo` (off by default). Its gun groups
carry the stock loadout's finite `Capacity` and `Ammo`, which `FireControl` draws down, so a bot runs
dry like a person. `RearmRuntime.Step` checks the seats `IsLocal` names, a bot's is the host's, so
the host checks its bots with no change, and `FlightController.Rearm` refills its guns and hull as a
person's. The gunner holds its quarry 20 s and re-acquires whenever `AutoTarget` is on, and guns and
rockets fire only in `Pursue`, so keeping a bot out of new fights needs a gate on acquisition.
Measured on C1's MP1 (`docs/org/multiplayer-rearm.md`, "Bots at a base"): the base is a
fly-through bay, walls 42 m and 47 m to either side, roof 28 m above, floor 16 m below, about 75 m
long and open at both ends; no landing is needed, the restore is the 25 m radius. The AI's
avoid-crash probe (4.5 s along the velocity, about 500 m) reads a descent near the bay, or a line
into the hall off its axis, as an obstacle and climbs out, so a run must arrive level and on the
axis. A `--hold=` script outranks an AI pilot, so a bot under one never runs its pilot (INSTR-101).

**Approach (landed).** New engine-free `Flight/Ai/AiRearmOrder.cs`, held as `AiPilot.RearmOrder`
and given only to a bot this machine flies: `HumanFlightAdapter.cs:573-574`, beside the arming, with
the controller's `WorldBlocksLine` as its world probe. `RearmRuntime.Step` updates it after the
restore check with the guns' `LoadShare` (over `FirableGuns`, 1 with infinite ammo), the hull's
`SummaryHealthFraction`, the nearest serving base (new `RearmBases.NearestServing`) and whether
the base restored it on this step. Thresholds (TUNE, remake values, one named constant each):
`AiRearmOrder.LowAmmoShare = 0.2`, a fifth of the forward guns' full load (pylons are not counted,
a bot fights with its guns); `AiRearmOrder.DamagedHullShare = 0.35` of whole-vehicle health, the
pool the death test reads. A run plans the bay's open side once: on 24 bearings a level line out to
700 m (the bay) and a line from there up to the gate (the leg), taking the clear leg nearest the bot
centred on the bay's clear arc (heading 180 on MP1 from every start). It flies to the gate, 2500 m
out and 120 m up, joins the final leg inside a 10 degree corridor only with its track within 60
degrees of inbound, aims 400 m ahead along the leg, and is level at the node's height for the last
700 m. A pass 150 m beyond the node unrestored plans afresh; a base that offers nothing ends the
run. Restored, it holds the node's height 60 m past, climbs, and hands back to combat 150 m from the
node, which is past the latch radius and the bay's 37 m half-length. While a run stands
`AiPilot.Next` sets the new `AiGunner.Disengaged`, takes no quarry (the machine reverts any chase)
and flies the order's aim on the cruise table in place of patrol; a stun, avoid crash and an evasive
maneuver still come first. `GunnerAcquisition.Step` drops and takes nothing for a disengaged gunner.
The gunner object stays armed. **Return fire on the way: no.** Guns and rockets fire only in
`Pursue`, a chase would turn the bot off its leg, and an emptied bot has nothing to fire.
`AiPilot.ResetForSpawn` clears the run and the flag, so a bot shot down on the way comes back with
none. For D32: file the two thresholds on the TUNE list; a bot on the bay's closed side flies
2.5 km past the base to the gate and back, up to about 95 s, which the playtest should judge. For
C20: a local bot built through the same bot block gets the order, and `RearmRuntime` already checks
every seat of a local match.

**Model recommendation.** Opus: the code is moderate, but the approach had to be measured in the
engine and redesigned three times (a 1500 m gate with a 600 m level probe failed four of eight
starts) before every start reached the base.

**Verify.** New suite `net-bot-rearm` (`CSVM/src/Testing/NetBotRearmSuites.cs`, clean loopback, host
pane, guest seat and one bot on MP1, weight 13.1): with half its guns and half its hull the bot keeps
a quarry and starts no run over 300 steps (able-to-fail control); its guns emptied, it breaks off in
2 steps and drops its quarry; set down 3 km out on the bay's closed side it plans the gate on the
open side; with the host's aeroplane put 800 m ahead in its path it takes no quarry and chases
nothing; it reaches the base (24 m, 5268 steps) and `RearmRuntime` restores its guns to full and its
hull from half to 100/100; it flies the run without going down, hands back 152 m out, and takes a
quarry and chases it 65 steps later. With its hull at 0.30 and full guns it breaks off in one step;
120 steps on the run stands with the gunner disengaged (control); shot down, it comes back with no
run and its gunner on duty, and starts none over 300 steps. With the `ResetForSpawn` clear and the
disengage removed, five checks fail. A temporary probe flew runs from eight starts 3 km out at
every 45 degrees: all eight restored with no return, in 2044 to 5611 steps. Units
`AiRearmOrderTests` (8: thresholds, load share, open bearing, the world probe, the legs and the aim,
a missed pass and a lost base, no run without a base or need, `NearestServing`) and
`AiPilotTests.AResetForSpawnHoldsTheNewPlacementWithNoEngagementLeft` (a standing run is cleared).
Run in `bots-b14`: `RunTests.ps1 -Suite 'net-bot-rearm,net-bot-seat,net-bot-seat-lossy,net-bot-respawn,net-bot-skill,net-seats,net-rearm-deathmatch,net-rearm-zeppelins,net-lobby-deathmatch-mp1,ai-engine-rearm' -Shards 4 -SkipGoldens -SkipHitch`:
units 6253 passed / 0 failed / 3 skipped, engine 10 passed; `-Filter 'ai-,net-,wingman,campaign-roster,instant-action' -Shards 4 -SkipUnits -SkipGoldens -SkipHitch`: 103 passed / 0 failed, engine errors clean.

**Verified.** Full `RunTests.ps1` on the merged tree (4a179350, B14, C20, C21 and C22 with main merged in): build, units 6265 passed / 0 failed / 3 skipped, engine 521 passed / 0 failed / 2 skipped (6 shards, engine errors clean), goldens 24 hash-identical; exit 0.

**⚠ Traps.** A base the mission switched off offers nothing (`IA1` has both plain nodes off), so
test on `MP1`. The rearm latch is one per seat and releases only outside every serving base. Never
launch a bot session with `--hold=` (INSTR-101). A suite that places a bot with `RespawnAt` clears
its run and refills it, so empty it again after the placement. Do not gate the run on fire instead
of acquisition: a held quarry keeps the machine chasing it.

## B15 ☑ A bot takes a person's world-contact rule: it grazes and bounces, never destroyed outright

**Goal.** A bot that touches world geometry grazes, bounces and takes contact damage as a person
does, so it dies to a world contact only when a person in the same plane would. The user's ruling
on the first D32 sortie, by extension of Decision 8 (a bot's hull equals a person's).

**Evidence (confidence: traced-to-code; the bot's rebound magnitude is direction-sound, judged at
the controls in D32).** The user's first crowded sortie (`vs-20261005-231643.log`, local `--vs
--mission=MP1 --vs-bots=15`): P3 spawned on MP1's point #8, 52 m from P4 on #9 at the same height;
both bots took each other as their first target, P3 opened fire at 51 m, and at t = 2.75 s logged
`AI ram into g140/col, destroyed outright (the decoded local_11 rule)`. The plan's second lead
was misread: P16's "embedded in terrain after a graze, destroyed" was a mid-air at 1144 m, not
terrain. P16 rammed P8 (`rammed P8 by P16 (tail)`), its contact reported part `center`, and the
un-embed test, which reads aeroplanes as well as the world, found its hull boxes still inside P8.
`center` is the AI probe sweep's label: a world-AI rig sweeps its def's collision probes, so the
point contact came with the boxes already inside the other aeroplane, and an AI response has no
push-out. Every contact-path reader of the split, and the decision on each:
- `AircraftContactResolver` doom rule (`Dooms`, `local_11`): **moved** to the new question.
- `FlightController`'s sweep shape (`SweepAirframe` against `SweepProbes`): **moved**. A person's
  hull sweep stops at first touch, and the un-embed test reads those same hulls.
- `ContactEffects.ApplyResponse` into `FlightModel.Collide` (the 0.03 m push-out and the
  normal impulse): **moved**; `Collide`'s flag is renamed `personsRule`.
- The 0.2 entity cut (`entityImpact`): **kept** on `IsHumanPiloted`. It prices a mid-air, not a
  world contact, so a bot's ram into an aeroplane still deals and takes a fifth. Open for the user:
  as a person, a bot's mid-air would cost five times as much.
- The camera kick (`ShakeMagnitude`) and the AI shake (`AiShake`): **kept** (A3's AI shakes).
- The contact pad rumble (`PerformContact`, `Crash`): **kept**, a bot has no pad.
- `groundBlowReady` (the AI ground blow off during a collision grace) and the danger-zone rail
  pose: **kept**, both are the AI force path and its crash avoidance.
- `TakeCollisionHit` (the struck half) and the un-embed loop read no split; the embed line now names
  what it grazed (`embedded in <collider> after a graze, destroyed`).

A guest never resolves a contact for a bot: its copy is `RemoteOwned`, `SimStep` takes
`StepRemotePose` in place of the model step, the sweep and the resolver, and `TakeCollisionHit`
returns for a remote airframe. The guest's copy shows the host's bounce through the pose samples
(`net-bot-seat` traces the bot's path at 0.00 m clean and 1.42 m lossy) and its death through the
host's report.

**Approach (landed).** A new field `FlightController.IsBotSeat`, carried by
`FlightControllerBuild.IsBotSeat` and set by `HumanFlightAdapter`'s seat path (`IsBotSeat = bot`)
on every machine. The question is `FlightController.TakesPersonsContactRule => IsHumanPiloted ||
IsBotSeat`, mirrored as `ContactConditions.TakesPersonsContactRule` for the resolver.
`IsHumanPiloted` stays truthful for everything else (aim assist, the AI force path, the `"player"`
role, the shakes, the pad). World AI and every campaign path never set the flag and keep the decoded
rule. Finding for D32: the bot's rebound is the decoded impulse applied to the AI plant's own body
rates, and the impulse reads the contact point's velocity, rotation included. A bot that lands while
its pilot and AI ground blow pitch it up rebounds harder than a person in the same plane. Staged into
MP1's ground (Fury, 25 degrees down at 80 m/s), across runs the pane measured e 0.56 to 0.76 and the bot 1.07 to 1.36; one
random-plane staging went from 31.7 m/s down to 124 m/s up. A 15-bot local probe (`RunProbe.ps1 --vs
--mission=MP1 --mute --vs-bots=15`, about 280 s) logged no `AI ram into` line and 12 grazes. It also
logged two embed deaths, which are the person's rule: one into the airstrip (`col_soil8`) at severity
0, a contact facing away from its normal, and one into another bot's airframe at 3 m/s. For D33: a bot
now sweeps hull shape casts where it swept probe rays, as a person does; D33's measurement predates it.

**Model recommendation.** Opus: the change is a few lines, but the reader audit had to tell world
contact from mid-air and the user's log had to be re-read to find the second death's real cause.

**Verify.** New suites in `CSVM/src/Testing/BotContactSuites.cs`. `graze-bounce-bot` (weight 1.4):
on `graze-bounce`'s floor trajectory a person rig and a bare bot rig (AI force path, `IsBotSeat`)
both graze and survive, 20 steps on too, the bot rebounding at e 0.52 against the person's 0.52 and
spending the same pair; world AI on it is destroyed outright with its ledger unspent (control); a
bot rammed into a parked aeroplane's tail bounces off (-64.9 to +39.0 m/s) and flies on, where world
AI on that ram spends the pair and gains no rebound (control). `versus-local-bot-graze` (weight 4.5,
local MP1 match, `--plane=player_fury --vs-bot=player_fury`): the bot is AI-piloted on a person's
rule and the pane is no bot seat (control); the pane and then the bot are staged 4 m over a flat
patch of MP1's ground, nosed 25 degrees down at 80 m/s; both graze and survive, the bot rebounds and
spends the pair; staged again with `IsBotSeat` cleared the bot is destroyed outright with its ledger
unspent (control). Able-to-fail: with `TakesPersonsContactRule` reduced to `IsHumanPiloted`, eight
checks fail across the two suites. `graze-bounce` unchanged and green. Unit
`AircraftContactResolverTests.ABotSeatIsSparedTheDoomRuleAndKeepsTheEntityCut` (graze, not doomed,
no camera kick, AI shake, the cut on a mid-air). Runs in `bots-b15`: `RunTests.ps1 -SkipEngine
-SkipGoldens -SkipHitch` units 6356 passed / 0 failed / 3 skipped; `-Filter
'ai-,graze,contact,collide,crash,bounce,airframe' -Shards 4 -SkipUnits -SkipGoldens -SkipHitch` 46
passed / 0 failed; `-Filter 'net-,versus-' -Shards 4 -SkipUnits -SkipGoldens -SkipHitch` 66 passed /
0 failed (every `net-bot-*`, `versus-local-bot`, `versus-local-bot-graze`); engine errors clean on
every run. `CheckCommentCaps.ps1`, `CheckDocEntries.ps1` and `CheckEncoding.ps1` clean.

**Verified.** Full `RunTests.ps1` on the plan tree (837c345d, B15 and B16 with the profile-sweep fix): build, engine 538 passed / 0 failed / 2 skipped (6 shards, engine errors clean), goldens 24 hash-identical; units 6356 passed / 1 failed / 3 skipped, the one `MasterServerLinkTests` relay test timing out at 15 s with another session's probes on the machine, then green alone (2/2 in 10 ms) and in a re-run of the unit stage, 6357 passed / 0 failed / 3 skipped.

**⚠ Traps.** `graze-bounce` pins the decoded AI rule for world AI; it must stay green unchanged. Never
widen the question to `IsHumanPiloted` itself: that flag also picks the force path, the aim assist
and the shakes. A piloted bot near the ground cannot be read with a 0.5 m/s rebound test, since its
own pull-up moves the normal speed by up to 0.8 m/s a step.

**The user's ruling on the mid-air cut:** a bot keeps the AI's 0.2 entity cut, so it deals and takes
a fifth of a person's ram damage; a person's full rule would make bot-on-bot mid-airs in a crowd kill
both too often.

## B16 ☑ A bot's world bounce matches a person's in the same plane

**Goal.** A bot that grazes the world rebounds as a person in the same plane on the same contact
would, never faster than it came in. The user's ruling on B15's finding.

**Evidence (confidence: traced-to-code, measured term by term).** B15's reading reproduces: in the
MP1 staging (Fury, 25 degrees down at 80 m/s) the pane reads e 0.56 and the bot 1.07 over the
contact (1.10 at the impulse itself). The decoded impulse (`FlightModel.Collide`,
docs/org/flightModel.md "Only the player bounces") uses the velocity at the contact point, rotation
included. A per-step log of the staged bot named the rotation's source: the AI ground blow
(`GroundBlowTerm`'s AI law, `0x0048c317`) writes a fixed push straight into `BodyRates` each tick,
3.37, 3.01 and 1.78 rad/s (half-angle) in the three steps before the contact, against 0.055 a step
from the pilot's full pull. The bot turns from 25 degrees nose-down to 28.7 nose-up, its tail strikes
first, and the rotation adds 12.3 m/s to the contact point's closing speed. The bot's restitution
on the MP1 contact with each term switched off in turn:
- nothing off: 1.10;
- the AI pilot's command off: 1.10 (the pilot is not the cause);
- the AI ground blow off: 0.52;
- both off: 0.56, the pane's;
- the impulse read with zero rotation: 0.56;
- the ground blow held out of the contact step only: 1.10. The contact step carries no blow (the
  nose is already up, so the probe misses); the whole deposit lands in the steps before.
- the far-field plant was not in play (the bot was inside 1 km of the pane).
Across every stock airframe with the ground blow on (bare rigs, `graze-bounce`'s 15 degrees at
60 m/s and the MP1 staging's 25 degrees at 80 m/s), the bot's restitution spanned -0.4 to 7.26
(the Balmoral 14.2 m/s down to 103.5 up) against the person's 0.52 to 0.57. That covers B15's
random-plane case of 31.7 m/s down to 124 up.

**Approach (landed).** Option (a), in the form the measurement allowed: the AI ground blow's
rotation is held out of the impulse that resolves the contact. `FlightModel` keeps the blow's
deposit as its own share of the body rates (`_aiGroundBlowRates`, added where the AI law adds to
`BodyRates`, decayed by the same factor, zeroed by `Reset`). The rates are linear in their deposits,
so the split is exact. `Collide` hands `BounceImpulse` `BodyRates - _aiGroundBlowRates`; the rate kick
it returns still adds to the full rates, so the bot keeps flying the rotation it had. Why this one:
- The cause is one AI term, and it is wrong only at the contact. The original's AI never takes the
  impulse, so the blow's per-tick rotation never meets it there. Away from contact the blow is the
  bot's crash avoidance, which B15 kept, and this leaves its flying untouched.
- The literal option (a), holding the blow out of the contact step, changes nothing (1.10 above).
  Holding it out of "the few steps of a graze" would need to know a contact is coming.
- Option (b), a cap at the person's restitution, would also clip rotation a person's own stick can
  put there, and would leave the rate kick computed on the blow's rotation. The excess is not
  inherent to the AI force path: with the blow's share out, the bot reads 0.43 to 0.56 everywhere.
- A person's share is always zero (the player path deposits none), so a person's impulse is the
  decoded one bit for bit. World AI takes no impulse. `graze-bounce` is unchanged and green.
The piloted bot's own pull-up stays in the impulse, as a person's does.

**Model recommendation.** Opus: the fix is a few lines, but choosing it needed the per-term
measurement, and the obvious reading of option (a) is inert.

**Verify.** `graze-bounce-bot` (`CSVM/src/Testing/BotContactSuites.cs`, weight 4.7) gains a bot rig
with the AI ground blow on, flown down `graze-bounce`'s floor trajectory: it rebounds at e 0.55
against the person's 0.52, checked at most 1 and within 0.1. A second check sweeps every stock
airframe on both trajectories (22 pairs): every bot at e at most 1 and within 0.15 of the person in
the same plane (the worst pair, the Hoplite entering at 6 m/s, stands 0.09 apart). The reading is
over the contact: `FlyInto` takes the normal speed entering the first rebound and leaving the last,
where a later rebound is one inside 6 steps of the one before. `versus-local-bot-graze` turns its
note into a check, bot at most 1 and within 0.1 of the pane on MP1: 0.56 against 0.56. Able-to-fail:
with the impulse reading the full `BodyRates`, three checks fail (1.12 on the floor; 22 of 22 pairs
outside, worst 7.26; 1.07 on MP1), and pass with the fix. Unit
`CollideResponseTests.ABotsImpulseLeavesOutTheRotationItsAiGroundBlowDeposited` (an AI plant stepped
once with a ground-blow hit rebounds as a still plant in the same pose; a person carrying the same
rates keeps the rotation term and rebounds harder), which fails with the fix reverted. Runs in
`bots-b16`: `RunTests.ps1 -Suite 'graze-bounce-bot,versus-local-bot-graze,graze-bounce' -SkipUnits
-SkipGoldens -SkipHitch` 3 passed; `-Filter 'ai-,graze,contact,collide,crash,bounce,airframe'
-Shards 4 -SkipUnits -SkipGoldens -SkipHitch` 46 passed / 0 failed; `-Filter 'net-,versus-' -Shards 4
-SkipUnits -SkipGoldens -SkipHitch` 66 passed / 0 failed; `-SkipEngine -SkipGoldens -SkipHitch` units
6357 passed / 0 failed / 3 skipped; engine errors clean on every run.
`CheckCommentCaps.ps1`, `CheckDocEntries.ps1` and `CheckEncoding.ps1` clean.

**Verified.** Full `RunTests.ps1` on the plan tree (837c345d, B15 and B16 with the profile-sweep fix): build, engine 538 passed / 0 failed / 2 skipped (6 shards, engine errors clean), goldens 24 hash-identical; units 6356 passed / 1 failed / 3 skipped, the one `MasterServerLinkTests` relay test timing out at 15 s with another session's probes on the machine, then green alone (2/2 in 10 ms) and in a re-run of the unit stage, 6357 passed / 0 failed / 3 skipped.

**⚠ Traps.** B15's trap holds: a piloted bot near the ground moves its normal speed by up to 0.8 m/s
a step, so read restitution over the contact, not off one step. Never zero the bot's whole rotation
in the impulse or cap its rebound: the pilot's own pull-up is a person's state. Anything that writes
`FlightModel.BodyRates` from outside the plant leaves the ground blow's share stale; nothing in
production does.

## B17 ☑ A bot rearms when its rockets run out, not its guns

**Goal.** A bot breaks off to a base when every rocket pylon it carries is empty, or when its hull is
badly damaged, and never for its guns. The user's ruling on D32's rearm reading: asked what should
send a bot back, "Rockets out + damage"; asked when a bot is out of rockets, "All pylons empty". In
the user's own play, "Gun ammo is plenty but i would only return to rearm rockets."

**Evidence (confidence: traced-to-code; the rocket trigger's rate in play is
direction-sound-magnitude-TUNE).** In D32's second sortie (`vs-20261006-193354.log`) the rearm order
fired 18 times, every time on the hull (0.02 to 0.34) and never on ammunition; the guns read 0.91 to
1.00 at every break-off. B14 read only the forward guns: `RearmRuntime.StepOrder` passed
`AiRearmOrder.LoadShare(pilot.Loadout?.FirableGuns ...)` against `LowAmmoShare = 0.2`, and left the
pylons out on the reading that a bot fights with its guns. A pylon's count is `Hardpoint.Ammo` of
`Hardpoint.Capacity` (the weapon's `CLUSTER_SIZE`, or the fit's own rounds) on
`Loadout.Hardpoints`, the list `FireControl` launches from and the AI rocketeer walks.
`FlightController.Rearm` calls `RestockWeapons`, which tops every gun group and every hardpoint back
to `Capacity` and refills `FireControl`, so a base restores the racks as well as the guns.
- **Planes with no rocket pylons.** None among the stock fits: all eleven in `stock_loadouts.json`
  hang `wep_06` (high explosive, three rounds) on 2 (Autogyro) to 8 (Balmoral, Warhawk) pylons, and a
  bot flies its plane's stock fit, since the lobby's bot row picks no fit. A loadout with no loaded
  pylon (a `none` pick binds no hardpoint, a race pilot carries no loadout) never trips the trigger,
  and neither does infinite ammunition.
- **Pylons carrying something other than rockets.** None exist. Every ordnance a pylon can carry
  (`selectable.pylon_ordnance`, `wep_05` to `wep_15`) is flagged `ROCKET` in `weapons.zrd`, the
  torpedo (`wep_14`), smoke (`wep_13`) and flare (`wep_15`) included; there are no pylon bombs. The
  game's own screens name every pylon's ordnance a rocket (the ROCKETS gauge, `IDS_ROCKETSHORTNAME`),
  and a bot's rocketeer launches any loaded pylon (`DriveAiRocketeer` views every hardpoint). So every
  loaded pylon counts, whatever it carries; only `--rocket=` and `--loadout=` change what a bot hangs.
- **How often the rocket trigger can fire.** A bot's rocketeer stamps a 30 s vehicle-wide lockout
  per launch (`AiRocketeer.RefireSeconds`, since a stock fit authors none) and launches only on its
  ordnance roll (0.14 and 0.18 per attempt in D32's log). A stock fit's 6 to 24 rounds therefore take
  at least 2.5 to 11.5 minutes alive and in pursuit to empty. In a short match the hull stays the
  trigger that fires. B18 answers this on the user's ruling: a bot launches on the wingman's 20 s
  with a failed roll launching anyway, at most three rockets a minute (1.6 to 1.9 measured), so its
  racks can run out in play.

**Approach (landed).** `AiRearmOrder.LowAmmoShare` and `AiRearmOrder.LoadShare` are removed.
`AiRearmOrder.RocketsOut(pylons, infinite)` is true when every pylon with a capacity holds 0 rounds;
it is false with no such pylon or with infinite ammunition. `Wants(rocketsOut, hullShare)` and
`Update(at, velocity, rocketsOut, hullShare, nearestBase, restored)` take the boolean in place of the
guns' share; `DamagedHullShare = 0.35` is unchanged. `RearmRuntime.StepOrder` reads
`pilot.Loadout?.Hardpoints` and no longer reads the guns. `Reason`, which the
`rearm: bot seat N breaks off ... (...)` line prints, names the trigger that fired: `rockets out`,
`hull 0.30`, or `rockets out, hull 0.20` when both did. `AiPilot.ResetForSpawn` still clears the run,
and a return or a base refills the pylons, so a bot that rearmed for rockets comes back with full
racks. Docs: `docs/org/multiplayer-rearm.md` "Bots at a base" (the trigger, the pylon reading, the
rate), the `AiRearmOrder.cs` and `RearmRuntime.cs` architecture entries, and the index bullet. B14's
section stays as the record of what landed then.

**Model recommendation.** Sonnet: a small change to one engine-free order and its one caller, on an
existing suite. The one non-obvious fact is the rocketeer's 30 s lockout, which a suite control that
leaves the bot in combat has to allow for.

**Verify.** Units in `AiRearmOrderTests`: every loaded pylon empty is out, and a pylon with no
capacity is skipped; one rocket left on one pylon is not out, nor is an empty rack with infinite
ammunition; a plane with no rocket pylons never runs out and starts no run; the hull alone still
starts a run, and `Reason` reads `hull 0.30`, `rockets out` or `rockets out, hull 0.20`; the
threshold control (rockets left, hull just above 0.35, no run); and an extracted-data tripwire that
every selectable pylon ordnance is a loaded `ROCKET` and every stock fit hangs at least two loaded
rocket pylons. `AiPilotTests`' reset test feeds the new argument. Suite `net-bot-rearm`
(`CSVM/src/Testing/NetBotRearmSuites.cs`, MP1, host, guest and bot on a clean loopback): with half
its guns, one round on each of its four pylons (4/12) and half its hull the bot keeps a quarry and
starts no run over 300 steps (control); restored and with its guns emptied (0/7200), racks full and
hull sound, it starts no run over 300 steps (the control for the removal); every pylon emptied with
full guns, it breaks off in 2 steps on `rockets out` and drops its quarry; set down 3 km out on the
bay's closed side with its pylons emptied again and its hull halved, it flies to the gate, takes no
quarry with a hostile put 800 m ahead, and the base restores it in 5268 steps to rockets 12/12, guns
7200/7200 and health 100/100; it hands back 150 m out and chases a quarry 97 steps later; with its
hull at 0.30 and full racks it breaks off in one step on `hull 0.30`; shot down on the way it comes
back with no run. Able-to-fail: with the gun trigger put back in `StepOrder` the removal control
fails (run True at guns 0/7200); with the rocket reading forced false four checks fail (no break-off,
no gate, no restore, no lure). Runs in `bots-b17`: `RunTests.ps1 -Filter
'net-bot-,versus-local-bot,rearm' -Shards 4 -SkipGoldens -SkipHitch` units 6360 passed / 0 failed / 3
skipped (`AiRearmOrderTests` 11), engine 11 passed / 0 failed (every `net-bot-*`, `versus-local-bot`,
`versus-local-bot-graze`, `net-rearm-deathmatch`, `net-rearm-zeppelins`, `ai-engine-rearm`); `-Filter
'ai-' -Shards 4 -SkipUnits -SkipGoldens -SkipHitch` 32 passed / 0 failed; engine errors clean on
every run. `CheckCommentCaps.ps1`, `CheckDocEntries.ps1`, `CheckEncoding.ps1` and `CheckItemIds.ps1`
clean.

**Verified.** Full `RunTests.ps1` on the plan tree (aa70eec5, with the respawn fixes 84bad29a, 193e0c74 and 932d4d3b): build, units 6370 passed / 0 failed / 3 skipped, engine 543 passed / 0 failed / 0 skipped (6 shards, engine errors clean), goldens 24 hash-identical; exit 0.

**⚠ Traps.** Never put the guns back into the trigger: the user flies back only for rockets. A pylon
built with no capacity carries nothing; counting it as empty would send a bot whose real pylons are
loaded. A suite that keeps a bot in combat with rockets left must leave it more rounds than it can
launch in the window: one launch per 30 s, so one round on each pylon holds through 300 steps. A
suite that places a bot with `RespawnAt` refills its pylons and mends its hull, so empty them again
after the placement.

## B18 ☑ A bot fires rockets at the wingman's rate, a failed roll still launching

**Goal.** A bot launches rockets at about the rate a person does, so its racks empty in play and
B17's rocket trigger sends it to a base. The user's ruling, asked "Which rocket rate should bots use
in a Dogfight? (Instant Action and the campaign stay as decoded either way.)": option C, "the
wingman rule, no roll". A bot seat's rocketeer takes the original's hangar-fit wingman rule (20 s
plane-wide refire, a 1 to 900 m band) in place of the 30 s and 200 to 800 m fallback, and the
original's `Network` override, so a failed quick-draw roll still launches.

**Evidence (confidence: traced-to-code; the measured rate in play is direction-sound-magnitude-TUNE).**
Every lead held. `AiRocketeer.RefireSeconds = 30f` is the fallback "for a pylon whose fit authored
none", and `Solve` takes a pylon's own interval and band only when they are above 0.
`FlightController.DriveAiRocketeer` passes `hp.RefireSeconds`, `hp.MinRangeM` and `hp.MaxRangeM`; a
bot's fit is bound by the seat path's `Loadout.Bind`, which writes none of the three (only
`Loadout.BindAi` and `Loadout.BindWingman` do), so every bot flew the fallback. The 30 is the
militia data's value, not a decode for a hangar fit. `PreparePilot` sets only the cone and the roll
chance, and `ArmSeatPilot` changed nothing on the rocketeer. The decode
(`docs/org/aiPilot/aiWeapons.md`): both lockouts are stamped before the roll (`0x004b6b36`,
`0x004b6b3b`), the roll is at `0x004b6b59`, and only a failed roll reaches `0x004b6b84`, whose
`Network` flag fires the round anyway. The original's only AI on a hangar fit is `wingman_1`, built
by `FUN_00444300` with 1 to 900 m and 20 s (`Loadout.WingmanMinRangeM`, `WingmanMaxRangeM`,
`WingmanOrdnanceRefireS`). `ArmSeatPilot` has one caller, `HumanFlightAdapter`'s bot block, so the
campaign, Instant Action (wingmen and waves) and every world AI never reach it. Before the change,
the user's match log read 21 launches over 15 veteran bots in about 3.7 minutes, 0.38 per bot per
minute. Measured on the hidden desktop, before (the two arming lines removed) and after, with a
local `--vs --mission=MP1 --vs-bots=15 --mute` under `--screenshot`'s fixed clock (seed 1, nobody
flying the pane), counting `rocket: playerN launched` lines for seats 2 to 16 and
`rearm: bot seat N breaks off` lines by reason:

| Run | Rule | Sim span | Launches | Per bot per minute | Rearms, rockets out / hull |
|---|---|---|---|---|---|
| 3600 frames | 30 s, 200-800 m, roll (before) | 60 s | 7 | 0.47 | 0 / 0 |
| 3600 frames | 20 s, 1-900 m, no roll | 60 s | 28 | 1.87 | 0 / 1 |
| 10800 frames | before | 180 s | 13 | 0.29 | 0 / 9 |
| 10800 frames | after | 140.8 s (the default kill target ended it) | 64 | 1.82 | 0 / 5 |
| 21600 frames, `--vs-kills=0 --vs-time=0` | after | 360 s | 147 | 1.63 | 1 / 22 |

The rate rose four to six times, to about 1.6 to 1.9 per bot per minute, all fifteen bots launching
over the six minutes. It stays under the three a minute the 20 s interval allows, because a bot
launches only in `Pursue`, inside the quick-draw cone and within 5° of the lead. The rocket trigger fired once in six minutes
(seat 6): a death and a hull rearm both refill the racks, and the hull trigger, at 22, still fires
far more often while no person is flying.

**Approach (landed).** `AiRocketeer` gains two named switches. `UseWingmanRule()` sets `MinRangeM`,
`MaxRangeM` and `RefireSeconds` from the three `Loadout.Wingman*` constants and raises `WingmanRule`,
under which `Solve` reads the rocketeer's own window and interval for every pylon, whatever the
pylon carries; that mirrors `FUN_00444300` writing its literals over every slot, and a bot's stock
fit authors 0 today in any case. `FiresOnFailedRoll` (default false, the `Network` override at
`0x004b6b84`): the dice are still drawn after the lockout is stamped, so the bot's ordnance stream
advances as before, and a failed draw launches. The verdict reads `dice fail, launched anyway`.
`AiFlightAssembler.ArmSeatPilot`, after `PreparePilot` and beside B11's `PlayersPreferred = false`,
calls both on the bot's rocketeer and logs `bot: '<plane>' launches every 20 s over 1-900 m, a
failed roll launching anyway`. A local bot (C20) arms through the same method. The remake-only
Pursue-only firing hold (`FlightController.DriveAiRocketeer`, "Only Pursue shoots") is unchanged
and applies to a bot as to every AI, so a bot launches only while it pursues. Docs:
`docs/org/aiPilot/aiWeapons.md` (a bot-seat paragraph under "What `AiGunner` runs", and a pointer
from the wingman's `Network` gate), `docs/org/multiplayer-rearm.md` (the bot's rate), the
`AiRocketeer.cs` and `AiFlightAssembler.cs` architecture entries, and B17's rate sentence.

**Model recommendation.** Sonnet: two switches on an engine-free class and one arming block. The
part that wants care is proving the world AI untouched, which the `net-seats` control does.

**Verify.** Units in `AiRocketeerTests` (10 new, 38 in the class): a failed roll launches with
`FiresOnFailedRoll` and does not without it, on the same stamped lockout (the control); the override
still draws the dice once; under `UseWingmanRule` 50 m, 150 m and 850 m launch and 950 m does not,
on a 20 s lockout; the rule stands over a pylon carrying 350 to 800 m and 5 s (a 300 m shot launches
on 20 s); a fresh launcher, which is what `PreparePilot` builds for a world AI, keeps 30 s,
200 to 800 m, the roll and no rule; and the rule with the override launches 15 times over 299 s at
60 Hz. Engine: `net-seats` reads the host bot's rocketeer on the rule (20 s, 1 to 900 m) with the
override, and a world AI spawned through the same `FlightRoster.SpawnAi` and assembler keeps
30 s, 200 to 800 m and the roll (control); `versus-local-bot` reads the rule and the override on
the local bot in all four cells. Able-to-fail: with the two lines removed from `ArmSeatPilot`,
`net-seats` fails its bot check (30 s over 200-800 m, rule False, override False) and
`versus-local-bot` fails four checks. The measurement above, through `RunProbe.ps1`. Runs in
`bots-b18`: `RunTests.ps1 -Filter
'net-bot-,versus-local-bot,rearm,ai-,instant-action,campaign-roster,net-seats' -Shards 4 -SkipUnits
-SkipGoldens -SkipHitch` 55 passed / 0 failed (every `net-bot-*`, `versus-local-bot`,
`versus-local-bot-graze`, `net-rearm-deathmatch`, `net-rearm-zeppelins`, `ai-engine-rearm`, the
`ai-` suites, `instant-action*`, `campaign-roster*`, `net-seats`); `-Filter
'wingman,warhawk,torpedo' -Shards 4 -SkipGoldens -SkipHitch` units 6370 passed / 0 failed / 3
skipped, engine 8 passed / 0 failed; engine errors clean on every run. `CheckCommentCaps.ps1`,
`CheckDocEntries.ps1`, `CheckEncoding.ps1` and `CheckItemIds.ps1` clean.

**Verified.** Full `RunTests.ps1` on the plan tree (aa70eec5, with the respawn fixes 84bad29a, 193e0c74 and 932d4d3b): build, units 6370 passed / 0 failed / 3 skipped, engine 543 passed / 0 failed / 0 skipped (6 shards, engine errors clean), goldens 24 hash-identical; exit 0.

**⚠ Traps.** Never set the override or the wingman rule in `PreparePilot`: Instant Action and the
campaign fly the decoded single-player gate, the roll. Keep drawing the dice under the override, or
the bot's ordnance stream shifts and a seeded run reads differently. A suite that keeps a bot in
combat with rockets left must allow one launch per 20 s with no roll. The Pursue-only hold is the
remake's, not the original's; lifting it raises the rate for every AI at once, guns included.

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

**Verified.** Full `RunTests.ps1` on the merged tree (4a179350, B14, C20, C21 and C22 with main merged in): build, units 6265 passed / 0 failed / 3 skipped, engine 521 passed / 0 failed / 2 skipped (6 shards, engine errors clean), goldens 24 hash-identical; exit 0.

**⚠ Traps.** Never read the roster's size as "on a wire" again: a local match with bots holds a
roster (`VersusDirector.Wired` carries the warning). A local roster's panes must name no plane, or
the seat's plane overrules the pane's own pick.

## C21 ☑ Multiplayer Lobby: Add bot, Fill-to-N, per-row plane/skill/team/callsign, Remove

Landed in code and tests; left: the user's look at the layout (`c21-lobby.png`, below).
The user approved the layout with two fixes, both landed: the bot editor's boxes, lists and plane
caption take the Mission Options dropdowns' face (its labels their titles' face) in place of the
Select Plane typewriter face, and the Skill list raises the first letter of langui 3695 to 3697,
which ship in lower case, to read Novice, Veteran and Ace.

**Goal.** The host adds a bot row with Add, or creates rows with Fill-to-N; edits each row's plane,
skill, team and callsign; removes a row; new bots join the smallest team (Decisions 5, 12); rows
survive a rematch (Decision 18). Guests see the rows.

**Evidence (confidence: traced-to-code).** The leads held. `DogfightLobby.HostRows` builds the list
the host and every guest read (host, then each seated peer), `LaunchRefusal` weighs each row (the
host's by `LocalSeats`), and `Teamed` reads every row, so a bot row in that list is counted by the
team rule with no second path. The roster row (`DogfightLobbySeat`, 20 bytes) had flag bits 3 to 7
and a reserved byte free, so the bot needs no new message and no width change. `NetTeamBook` keys
members by peer and mints the lowest free team number, so a bot cannot be a book member: its team
is held on its row, and a disband must clear it or a later team taking the same number would
inherit it. The menu launch built its field in `Launcher.VersusLaunchField` with no bots;
`BuildCliNetRoster` was the only `AddBots` caller. Three findings beyond the plan: the Built-in
presentation has no lobby screen at all (its host's `DogfightLobby` is never shown), so it needs no
controls and can add no bots; `DogfightLobby.LocalSeats` is never set in production, which is
harmless because the Original lobby launches one local seat; and Decision 1 (bots fly Deathmatch
only) was not enforced anywhere a lobby could reach. GitHub issue #140 is still open with no
comment. Reading the routes found one that skips the team check, not the one #140 describes: a
Built-in host's launch (`CheckBuiltInLaunch`) asks no team rule, yet Original guests can still
form teams on its book and `TakeNetLaunch` seats them with those teams. The bot path adds no
route: bots exist only through the Original lobby, and its LAUNCH! asks `LaunchRefusal` over the
same rows.

**Approach (landed).** `DogfightLobby` holds `Bots` (`DogfightBot`: an id that outlives the row's
place, callsign, airframe or `DogfightLobbySeat.RandomAirframe` 0xFF, `NetBotSkill`, lobby team),
listed and seated after the guests and untouched by `Launched`/`Land`, so they survive a rematch.
`AddBot` takes a Random plane at veteran, a callsign drawn from `CallsignPool` (the screen loads
`BotSeats.CallsignPool` from the message table) that no row holds, else `Bot n`, and the team with
the fewest players at that moment (the first created of a tie, 0 with no team standing); nothing
rebalances it later. **Fill-to-N's N is a total pilot count**, people (the host's splitscreen seats
included) and bots together: `FillTo(n)` adds rows until `FieldSeats` reaches `min(n, 16)` and
removes none. `BotRoom` stops Add at `NetSeats.MaxPlayers`. `RemoveBot` and the per-row setters
(`SetBotAirframe`, `SetBotSkill`, `SetBotTeam` to a standing team or 0, `SetBotCallsign` cut by
`BotSeats.ClipName` to 12, refusing a blank name or one another row holds) are refused on a guest
and while the host is Ready, the gate its options have; a bot change starts no new round, as a
team change does not. A disband sets that team's bots to 0. Only a Deathmatch takes bots
(`TakesBots`): Add is refused outside it, `BotsGrounded` holds LAUNCH! with the remake's line
`BotsDeathmatchOnly`, and `LaunchBots` is empty there. Wire: the roster row's flags gain bit 3
(bot) and bits 4 and 5 (tier), the airframe byte carries 0xFF for Random; a reader refuses a tier
of 3, tier bits on a person and a bot marked host or captain; an older build reads a bot row as a
person's, so no version change. Guests read bot rows from `Players` (`IsBot`, `Skill`) and can
neither pick nor edit them. Launch: `TakeNetLaunch` passes `LaunchBots` and the callsign pool to
`VersusLaunchField`, which resolves Random planes and empty callsigns through `BotSeats.Resolve`
on the host (`Rng.BotField`) and appends the bots after the guests with `NetSeats.AddBots`, each on
the stock fit; the roster then carries real stock planes to every guest. **Where the controls sit
(the user's call from the capture):** Mission Options' empty left column under the type's
description holds a "Bots" heading, Add Bot and Fill to (small plaques) and the count box with
arrows (2 to 16, opening on `DogfightLobby.DefaultFillTo` 8), plus a hint line on the host; a
guest's draw greyed. A host's press on a bot's row in the player list opens Select Plane on that
bot in place of its own picker: Callsign box, Plane (Random, then the eleven stock), Skill
(langui 3695 to 3697), Team (No team, then each standing team), Remove and Accept, with the stock
plane's icon and ratings on the right ("Drawn at launch" for Random). Any tab press lets it go.
The `--menu=lobby:host:bots` and `lobby:host:bot` aids pose it.

For later items: C22's "newest bot" is the last entry of `DogfightLobby.Bots` (ids rise with each
add). A guest joining a full field makes the lobby list more than 16 rows; `DogfightRosterMessage`
then cuts the last bot rows from the guests' view, and `AddBots` leaves those bots out at launch
with a log line, until C22 yields a bot. `LaunchNames` lists bot names after the guests, so
`ScoresOf` names bot seats correctly while the host flies one seat; D31's seat-roster naming still
applies. The "Players (n of cap)" line counts bot rows against the human cap.

**Model recommendation.** Opus: the team-book interaction (disband, lowest-free numbering), the
roster bits and the screen integration had to be read together; the code itself is plain.

**Verify.** `DogfightLobbyTests` (8 new): Add joins the smallest team with the first created of a
tie, is not moved by later team changes, and a disbanded team's bots go teamless; Fill-to-N stops at
N and at 16, with a two-seat host leaving one bot fewer of room; edits and Remove on the host alone,
refused while Ready; rows and `LaunchNames` survive `Launched`/`Land`; a host and its bot on one
team is `TooFewTeams` (10519) and bots balance two people's teams; Deathmatch only; a guest reads
the rows and cannot touch them; `LaunchBots` resolved through `BotSeats.Resolve` and
`NetSeats.AddBots` seats the bots after the guests on stock nodes. `NetMessagesTests` round-trips
bot rows with tier and Random plane and refuses the three malformed flag forms. New engine suite
`menu-original-lobby-bots` (weight 0.4): Add Bot and Fill to through the screen on a loopback host
and guest, the editor's boxes, Remove, LAUNCH! refused with 10519 for host, guest and both bots on
one team, then launched with no team; `VersusLaunchField` seats the bots after the guest with the
Random plane drawn, and the guest's `NetSession` joins reading both bots on stock planes (its last
check was flipped once to confirm it can fail). Captures for the look:
`.scratch/c21-lobby.png` (Select Plane on a bot), `c21-lobby-mission.png` (Mission Options with
three bot rows) and `c21-lobby-guest.png` (a guest's greyed view). Runs: units 6254 passed / 0
failed / 3 skipped; `-Filter menu- -Shards 4` 45/45; `-Filter net- -Shards 4` 58/58. No golden
shows the lobby.

**Verified.** Full `RunTests.ps1` on the merged tree (4a179350, B14, C20, C21 and C22 with main merged in): build, units 6265 passed / 0 failed / 3 skipped, engine 521 passed / 0 failed / 2 skipped (6 shards, engine errors clean), goldens 24 hash-identical; exit 0.

**⚠ Traps.** The screen layout is a look judgement; bring a capture to the user before settling it.
Bot rows count toward the team-launch rule (`NetTeamBook.Check`, fed by `DogfightLobby.LaunchRefusal`
with each row's team): a host plus bots all on one team is "one standing team", which LAUNCH!
must refuse with langui 10519. GitHub issue #140 reports a Deathmatch that launched with every
player on one team through some route round that check; read its state before wiring bot rows
into the refusal, so the bot path does not add a second route round it.

## C22 ☑ A joining human takes the newest bot's seat in the lobby, a late joiner once the match is back there

**Goal.** A guest who joins a full lobby takes the most recently added bot's seat (Decision 6). A
guest who joins while a match runs lands in the lobby, not the match, and takes the newest bot's
seat when the match is back in the lobby (Decision 7); a host Restart keeps the match running and
changes no seat. A guest who leaves is not replaced.

**Evidence (confidence: traced-to-code).** The holding already existed; the yield and the host's
in-flight advert did not. **How a late joiner is held.** The socket accepts it: ENet host peers are
`MaxPlayers - 1` on every host (`Launcher.OpenCliNet`, `LaunchMenu`, `OriginalLobbyScreen.OpenHost`,
`OriginalPresentation`), and a bot holds no peer, so fifteen bots leave all fifteen peer slots free.
The host's `NetLobby` is bound to the running session, and a bound lobby's `Peers` is the list
fixed at bind: `OnPeerConnected` sends the newcomer only the build version and the last advert,
`Keep` drops anything an unbound peer sends, and `NetSession` sends state, fire, damage and roster
only to `Peers`. The guest's door reaches Joined and its lobby opens at once (`OriginalShell.FollowDogfight`);
with no options heard it shows the guest alone (`GuestAlone`), the default options, Ready greyed
(`HasOptions` false), a silent chat, and `DogfightLaunchDue` stays false because nothing is held.
`RefuseOverCap` counts people only and does not run in flight, so a late joiner past the human cap
is refused in arrival order when the lobby comes back. **A finding beyond the plan:** nothing stepped
a Dogfight host's door in flight (the menu host is hidden, and the launcher stepped only a guest's
door), so the host answered no LAN search, let its master listing lapse, and a late joiner by typed
address read the lobby's last advert (Waiting). **Where "back in the lobby" is signalled.** Only a
completed match lands there: `ExitSession` asks `LobbyLanding` while the session lives, `CloseNetLaunch`
hands `EndNetWire` the kept lobby, `NetPlayFeature.Reclaim` unbinds the wire, the menu's `LobbyReturn`
reaches `DogfightLobby.Land`, and the door's next Hosting step runs `RefuseOverCap` and then the
lobby's `Step`. A match left early closes the door and tells every guest, so there is no lobby to
wait for. A Restart (`VersusDirector.Restart`) stays in the session and touches neither the door nor
the bound list. **The trap does not bite.** Seats change hands only in the lobby, and every launch
builds a new `GameSession`, whose new `SessionNet` holds a fresh `AircraftStateCadence` (`_sequence`
zeroed) and `_fireSequence`, whose new `NetSession` holds fresh `Instruments`, and whose every remote
rig takes a new `RemotePoseBuffer` (`HumanFlightAdapter`); `NetLobby.Unbind` drops the old flight's
held payloads. **Split-screen.** A Dogfight guest flies exactly one seat (`VersusLaunchField` seats
each peer once; `SeatsWanted` is co-op's), and only the host flies more (`LocalSeats`, unset in
production), so a guest with more seats than one bot frees does not arise; the rule below frees one
bot per seat past sixteen whoever brings it. **A seat changing hands is a row change.** People are
listed and seated before bots, so the guest takes the place after the people already seated (seat 1
here) and every bot moves up one; what changes hands is the sixteenth place, not the bot's seat
index. C21's backstop already cut the newest bot at launch (`NetSeats.AddBots` stops at sixteen), so
the yield's effect is the lobby: the host's list, `FieldSeats` and `BotRoom`, and a guest's view
that no longer silently drops the last row.

**Approach (landed).** `DogfightLobby.Step` on the host runs `YieldToPeople` between the team actions
and the send: while `FieldSeats` exceeds `NetSeats.MaxPlayers` and a bot row stands, the last row
(the newest bot) goes. One bot per seat past the cap, newest first, so people who waited take the
newest bots' places in join order; no row is added when a person leaves. It runs only from `Step`,
and a host's door steps its lobby only while the lobby stands, so nothing yields in flight or on a
Restart, and a join into a standing full lobby yields on the next step. The host's door in flight
(`NetPlayFeature`'s released branch) now also hands its advert to every connected peer, and the
launcher steps a host's door in a lobby flight as it already stepped a guest's
(`Launcher.TickVersusFlight`, was `TickVersusGuestFlight`): the door steps neither its wire nor its
lobby, but its advert reads In mission to its peers, the LAN search and the master listing. A late
joiner therefore finds the running game and reads `Advert.Status == InMission` while it waits. **Not
built, for the screen's owner:** a cue on `OriginalLobbyScreen` for a waiting guest (it could read
the door's `Advert` status In mission with `HasOptions` false, for example "Match in progress"), and
a lobby notice when a bot yields (`DogfightLobby.Announce` would post one; no langui line exists for
it, so the wording is a look call). For C23: `YieldToPeople` reads `FieldSeats`, whose people count
is `LocalSeats` plus seated peers; a local board with no wire has no peers and yields nothing.

**Model recommendation.** Sonnet would do the code, which is a loop and a stepped door. Opus was
used because the holding had to be traced through the lobby's binding, the session's peer list, the
door's released branch and the launcher's landing path before deciding where the yield may run.

**Verify.** New suite `net-bot-yield` (`CSVM/src/Testing/NetBotYieldSuites.cs`, clean loopback, real
doors, the host's flight a whole `GameSession` on MP1, weight 7.0): Fill to sixteen seats fifteen
bots; a guest joining the full lobby takes the newest bot's place on the host and in its own lobby
(sixteen rows, its row 1); its leave refills nothing (control); launched with the host and fifteen
bots, a guest joining mid-match stands in its lobby reading In mission with no options and no launch
due, holds a peer on the host's wire outside the bound list, has nothing held, and the host's lobby
keeps all fifteen bots (control) while the session flies the same sixteen seats; after the match
ends and the host's Restart, all of that still holds; when the completed match lands, the newest
bot's row leaves the host's list and the late guest holds row 1 on both ends, its advert back to
Waiting; the next launch seats it at seat 1 with the fourteen remaining bots after it on the host's
roster, and its own `NetSession` joins flying seat 1. Able-to-fail, each run and reverted: with the
yield removed, four checks fail (the full-lobby join, the leave control, the re-add, the landing)
while the launch checks pass on `AddBots`' cut, and both new units fail; with the in-flight advert
removed, the two In mission checks fail; with the lobby stepped in flight, the two keep-fifteen
controls and the two waiting checks fail. Units: `DogfightLobbyTests.AGuestSeatedOnAFullFieldTakesTheNewestBotsPlaceInJoinOrder`
(an unstepped lobby lets no bot go, as a control; two guests take the two newest in order; a leaver
is not replaced) and `ABotYieldsOnlyWhenTheFieldIsFullCountingTheHostsSplitscreenSeats` (a guest
seated with room takes no bot's place, as a control; a host flying two seats still frees one bot per
guest). Runs in `bots-c22`: `RunTests.ps1 -UnitFilter FullyQualifiedName~DogfightLobbyTests -SkipEngine
-SkipGoldens` 41 passed; `-Filter 'menu-,net-' -Shards 4 -SkipGoldens -SkipHitch` units 6265 passed /
0 failed / 3 skipped, engine 104 passed / 0 failed, engine errors clean.

**Verified.** Full `RunTests.ps1` on the merged tree (4a179350, B14, C20, C21 and C22 with main merged in): build, units 6265 passed / 0 failed / 3 skipped, engine 521 passed / 0 failed / 2 skipped (6 shards, engine errors clean), goldens 24 hash-identical; exit 0.

**⚠ Traps.** No seat changes hands mid-match, so a swap must never be triggered by Restart. The
seat's channels and sequence counters (`AircraftStateCadence._sequence`, `SessionNet._fireSequence`,
`NetInstruments`) must reset when the seat changes hands, or the guest's first samples in the next
match read as stale.

## C23 ☑ Local join board: bot rows, and the two-pilot minimum counts bots

Landed. The user approved the join board's Bots block and Edit Bot panel from the captures
(`c23-board.png`, `c23-board-editor.png`, below).

**Goal.** The local Dogfight setup offers the same bot rows as the lobby, and one human plus one
bot can start a match (Decision 13).

**Evidence (confidence: traced-to-code).** The leads pointed at the right files but the wrong
seams. The join board (`OriginalJoinBoard.cs`) hands nothing to the launch: it signs pads onto the
shared `PlayerSetupFeature`'s seats, and the Original Dogfight screen (`OriginalSeats.cs`, a shell
partial) launches through `PlayerSetupFeature.BuildExit`, a `LaunchExit` with one `MenuSeatChoice`
per seat and no wire. The two-pilot minimum is `PlayerSetupFeature.MinimumSeats(Versus)` = 2, read
by `Refusal` (which `BuildExit` throws on), by the Dogfight screen's `FlyEnabled` and its hint, and
by Built-in's `LaunchMenu.CanLaunch`. `DogfightLobby.LocalSeats` is the network lobby's and plays no
part in a local match. C20's wiring note held: `StartSessionFromMenu` -> `TakeNetLaunch` leaves
`_netRoster` null with no wire, `SessionSpec.FromMenu` kept the command line's `VsBots` (`cli with`)
but no menu path read them, and `CloseNetLaunch` clears the roster only when a wire existed. The
local Dogfight has no teams: no menu screen offers one and `FromMenu` sets none, so the one-team
refusal (10519) has no local counterpart. The Built-in presentation has a join board too
(`LaunchMenu`) but, as C21 found for the lobby, no bot controls; it is left without them.

**Approach (landed).** The bot rows and their rules moved out of `DogfightLobby` into a small shared
type, `UI/Menu/DogfightBots.cs`: the list, the id counter, `CallsignPool` and `Draws`, and `Add`
(Random, veteran, a drawn callsign no row or person holds, else `Bot n`), `Rename` (cut at 12,
refusing a blank or held name), `SetAirframe`, `SetSkill`, `SetTeam`, `Remove`, `DropNewest`,
`ClearTeam`, `ById`, `Room`, `FillTo` and `LaunchEntries`. **What moved out of `DogfightLobby.cs`:**
the fields `_bots`/`_draws`/`_nextBot` became one `DogfightBots _bots`; the bodies of `AddBot`,
`FillTo`, `RemoveBot`, `SetBotCallsign`, `SetBotAirframe`, `SetBotSkill`, `SetBotTeam`, `BotById`,
`BotAt`, `LaunchBots`, `YieldToPeople`, the `HostRows` bot loop and the disband loop in `Post` now
call it; the private `DrawCallsign`, `EditBot` and the string `Contains` overload were deleted. The
lobby keeps its gates (`EditsBots`, `TakesBots`, `BotRoom`, `SmallestTeam`, standing teams) and its
public API is unchanged; `DogfightBot` and `DogfightScore` did not move. The local rows are
`PlayerSetupFeature.Bots`, team 0 always: `AddBot` (refused past 16), `FillBots(n)` (n counts seats
and bots), `RenameBot` (refusing `P1` to `P4`), `Pilots(mode)` (seats plus bots for a Dogfight, seats
otherwise), `FieldPilots` and `BotRoom`. `Refusal` and the Dogfight screen's `FlyEnabled` count
`Pilots`, so one seat and one bot fly; a seat that `Join`s a full field takes the newest bot's place
(C22's rule, `DropNewest`); `Discard` clears the rows and a return from flight keeps them
(Decision 18). `BuildExit` puts them on the new `LaunchExit.Bots` for a Dogfight alone.
`SessionSpec.FromMenu` takes `bots` and sets `VsBots` to them for a Deathmatch and to none
otherwise, so a menu launch never seats the command line's. `StartSessionFromMenu`, after
`StepSortieSeed`, sets `_netRoster = LocalVersusField(_spec, _messagesPath)` when no wire opened: C20's
own builder, panes from `NetSeats.LocalPanes`, then `NetSeats.AddBots` over `BotSeats.Resolve`
(Random planes drawn on `Rng.BotField`, named planes and the board's callsigns kept), then
`Validate`. **Where the controls sit:** on the join board's right page, the closest equivalent of
Mission Options' left column. Under the articles stand a "Bots" heading with a "Dogfight only" hint,
ADD BOT and FILL TO (the board's own paper plaques), the count box with arrows (2 to 16, opening on
`DogfightLobby.DefaultFillTo` 8), then the rows in two columns of eight, each callsign with its tier.
A press on a row puts the Edit Bot panel in the articles' place: Callsign (focused, typed into),
Plane (Random, then the eleven stock planes as "Stock <name>"), Skill (langui 3695 to 3697, first
letter raised), REMOVE and ACCEPT, with the stock plane's caption, icon and four ratings below, or
"Drawn at launch" for Random. No Team box, since the local match has none. The boxes, lists and
rows take face 10558 and the labels 10096, the heading 10099 and the editor's title 10114, as
C21's. Module: `UI/Menu/Original/OriginalBotPanel.cs`, driven by `OriginalJoinBoard`; the shell
routes typed text and `CapturingText` to it. The Dogfight screen's seat strip adds a "+ N bots from
the JOIN BOARD" line and its lone-seat hint reads "Dogfight needs a second seat or a bot, from the
JOIN BOARD". Aids: `--menu=join-board:N:bots` and `join-board:N:bot`. For later items: the widget
drawing (dropdown, box, open list) is a copy of `OriginalLobbyScreen`'s private composers, which
could move into one shared helper once D31 has landed there.

**Model recommendation.** Opus: the shared-type move had to keep `DogfightLobby`'s API and C21/C22's
tests intact beside a sibling's edits, and the screen work spans the shell, a module and the
launcher.

**Verify.** `JoinBoardBotTests` (8 units): one seat is refused alone and launches with one bot, the
exit carrying a Random veteran row the menu spec seats; bots count for a Dogfight alone and ride no
other exit, a CTF, ZvZ or Free Flight spec carries none, a menu spec drops the command line's, and
`Discard` clears them; Fill-to-N stops at 16 (15 bots for one seat, 6 for two seats filling to 8)
with distinct callsigns and no player tag; a seat joining a full field takes the newest bot's place
(control: one joining with room takes none); a rename is cut and refuses a tag or a held name;
Random resolves to a stock node at launch and a named plane is kept, the local roster validating;
and on the layout fixture the board adds, fills, opens Edit Bot, renames by typing, picks a plane
from the list and steps the skill, removes, and the Dogfight screen names the bots and launches one
seat with them, while a lone seat's hint offers a bot. `DogfightLobbyTests` (41) pass unchanged on
the moved rules. New engine suite `menu-join-board-bots` (`CSVM/src/Testing/MenuJoinBoardBotSuites.cs`,
weight 0.3): the install's layout and pilot names, Add Bot, Edit Bot to a Fury at ace, Fill to 4,
the strip, FLY with one seat and the exit's three bots in order (able-to-fail: with `Pilots`
counting seats alone, four checks fail). `versus-local-bot` gains a menu launch cell (weight now
16.0): a one-seat `BuildExit` with a Fury ace and a Random bot through `FromMenu` and
`LocalVersusField` flies the pane then both bots by callsign, the Random one on a stock plane, both
on armed pilots (able-to-fail: with `FromMenu` dropping the bots, the roster check fails). Captures
(`RunProbe.ps1 -Resolution 800x600 --presentation=original --menu=join-board:2:bots` and `:bot`):
`c23-board.png` and `c23-board-editor.png` in the session scratchpad; no golden shows a menu. Runs in `bots-c23`:
`RunTests.ps1 -SkipEngine -SkipGoldens -SkipHitch` units 6273 passed / 0 failed / 3 skipped;
`-Filter menu- -Shards 4 -SkipUnits -SkipGoldens -SkipHitch` 46/46; `-Filter net- -Shards 4 -SkipUnits
-SkipGoldens -SkipHitch` 60/60; `-Suite versus-local-bot` pass; engine errors clean on every run.

**Verified.** Full `RunTests.ps1` on the merged tree (4f95503c, C23 and D31 with main through #148 merged in): build, units 6355 passed / 0 failed / 3 skipped, engine 536 passed / 0 failed / 2 skipped (6 shards, engine errors clean), goldens 24 hash-identical; exit 0.

**⚠ Traps.** Never let a menu launch read the command line's `VsBots`: `FromMenu` replaces them, or
a `--menu --vs-bots=` run would seat bots no board shows. The join board's bots fly a Dogfight
alone; a Free Flight exit must carry none.

# Wave D, presentation and measurement

## D31 ☑ Bot tag on the lobby roster and the board; a bot's callsign on the target marker

The user approved the captures: the BOT tag, the tag in place of the Ready tick, the header, and
both remake lines as worded. The user ruled that the results board names a bot by its callsign
("Crawford BOT") while people keep P1 to Pn, which landed with the capture re-taken.

**Goal.** The lobby roster and the results board show a bot tag beside a bot's callsign; in flight
a bot is marked as any aircraft is, and the target marker's name line shows its callsign
(Decision 11).

**Evidence (confidence: traced-to-code).** GitHub issue #141 (delete `VersusHud`) is still open, so
nothing here touches `VersusHud`; a bot is marked through `TargetHud` like any aircraft. **The
lead's marker fix was already in place:** `HumanFlightAdapter` sets `controller.PilotName` from the
seat's callsign for every seat that has one ("Unknown" for a nameless person), bots included, on
the host and on a guest, and `TargetPool.Describe` reads `MarkerName` else `PilotName`, so the
selected bot's name line already read its callsign. A3's "fallback name" is the other path:
`TargetHud.UpdateHostile`, the nearest-AI-hostile tracker drawn only while the pane has no
selection (after Target Nothing, or with an empty pool), tagged the bot `HostileTag(node name)`,
"PLAYER3". A remote person never reaches it (`NearestHostile` takes AI-piloted aircraft only), and
a world AI aircraft has no `PilotName`. **Game Scores naming:** `LaunchNames` holds one name per
lobby row (host, seated guests, bots), so it names seats right only while every machine flies one
seat in row order; a host flying split-screen seats shifts every later name (unit below, three
seats on two rows). `Launcher.ExitSession` calls `LobbyLanding` while `_session` lives, and
`GameSession.NetSeats` is the roster on both ends (a guest's is the host's copy). **The board:**
`VersusBoard` labels every row by `SplitScreen.PlayerTag`, network persons included, and
`SessionBoards` already holds the session's seat list (`_in.NetSeats`), a local bot match's roster
too. **langui:** 10048 "Players (%1!d! of %2!d!)" is the header; the cap it names is the door's
`SessionCap`, which `RefuseOverCap` counts people against, so a bot row has no place in it. 10090
"In Progress" is the games list's word for a running game (status 3,
`docs/org/multiplayer-messages.md`), the only string near the waiting cue; 10506 "[%1!s! left the
game.]" is the nearest lobby notice to a yield; no string names a bot. `HostRows` marks every bot
Ready, since it has no plane to consent to.

**Approach (landed).** Each proposal below was settled from the captures.
- **Game Scores naming.** `DogfightScore` gains `IsBot`. `DogfightLobby.ScoresOf(match, seats)`
  names each seat by its callsign on the session's roster and marks a bot's line (team lines and
  members laid out as before); the `LaunchNames` overloads stay as the fallback.
  `Launcher.LobbyLanding` takes the session's seats (`_session.NetSeats` from `ExitSession`) and
  uses the roster when it has one.
- **The tag** is the word `BOT` (`OriginalLobbyScreen.BotTag`, `VersusBoard.BotTag`), in the lobby
  in the dark red the pinned Network rows use, so the remake's words read apart from the original's.
  Lobby list: in the Ready column, in place of the tick. Game Scores: at the right end of the name
  column (a callsign holds 12 characters). Results board: a bot's row reads its callsign in the
  seat's colour with the tag after it in the headers' smaller ink, and a bot that wins heads the
  board by its callsign; `VersusBoard.Build` takes `Func<int, string?> botName` by `PlayerIndex`,
  which `SessionBoards` fills from the seat list, and a board with no bot is unchanged.
- **Mission Options spacing** (a C21 layout bug the capture showed): the type's description wrapped
  into the Bots heading at 1280x720. The Bots block now starts at `BotsY`, 205 below the page top,
  and the description is boxed to the gap with a fixed pitch, so the view's fit steps its face down
  rather than overlap; checked for all three types at 800x600 and 1280x720.
- **Ready tick:** a bot row draws no tick; the BOT tag stands in its column. `AllReady` still counts
  a bot Ready.
- **Header:** langui 10048 counts people only (`DogfightLobby.PeopleListed`) against the cap, and the
  bots follow it: "Players (3 of 16) + 3 bots" (`BotsListed`), "+ 1 bot" for one.
- **Waiting cue:** `DogfightLobby.WaitsOnMatch(advert status)` (a guest, no options heard, its
  host's advert In mission). While it holds, the guest's chat pins one row, as a host's Network rows
  are pinned: langui 10090 "In Progress" in the name column and the remake's
  `OriginalLobbyScreen.MatchInProgress`, "The host's match is under way. You take a seat when it
  ends." It goes when the landed lobby's options arrive.
- **Yield notice:** `YieldToPeople` announces `DogfightLobby.YieldLine`, langui 10506's form with
  the reason, "[Crawford left the game to make room for a player.]", to the host's chat and every
  seated guest's, one per bot that yields.
- **Target marker:** `TargetHud.TrackedTag` tags the tracked hostile by its `PilotName` where a seat
  gives one, else by `HostileTag` of the node name as before.
- Capture aids: `--menu=lobby:guest:bots` now draws the pilot-name callsigns, `lobby:host:bot-scores`
  (and `guest:bot-scores`) lands Game Scores over the six posed seats, and `lobby:late` is a guest
  joined to a stand-in host advertising In mission (`NetDoorAid.LateDogfightGuest`; the in-process
  mesh links every peer at once, so the real doors cannot hold a guest back as `net-bot-yield`'s
  arrival gate does).

For later items: a local bot match's results board tags its bots with no further change, since
`SessionBoards` reads the local roster too. `--debug-scoreboard` fires its kill between the first
two panes (`VersusDirector.ForceDebugScoreboard` reads `Panes`), so a board capture with bots needs
`--players=2`.

**Model recommendation.** Sonnet would do the code, which is presentation over existing state.
Opus was used because the marker lead had to be traced to the tracker fallback rather than the
name line it named, and the naming defect needed the launch field and both ends' rosters read
together.

**Verify.** Units: `DogfightLobbyTests.TheScoresNameEachSeatOffTheSessionsRosterAndMarkItsBots` (a
host flying two panes beside a bot, three seats on two lobby rows: the roster names Crawford, P2,
Zachary with only Crawford marked; control, the lobby's list names the bot's line P3 and puts
Crawford on the second pane's line), `ATeamMatchTagsABotsLineUnderItsTeam`,
`TheListCountsItsPeopleApartFromItsBotsOnBothEnds` (2 and 0, then 2 and 4 on both ends after Fill to
six), `ABotThatGivesItsPlaceToAPersonSaysSoInTheChatOnBothEnds` (control: a full field with nobody
waiting posts nothing), `AGuestWaitsOnTheMatchOnlyWhileItsHostFliesOneAndNoOptionsHaveCome` (In
mission yes; Waiting, none and the host no; control: false once the options land), and
`VersusBoardTagTests` ("Crawford BOT" and "CRAWFORD WINS"; control: a person, a seat past the list
and a board with no seat list read the plain tag, a person who wins keeps it). Engine: `net-bot-seat` and
`net-bot-seat-lossy` gain a reading on both ends: the bot's aeroplane carries its callsign as
`PilotName`, a target pool built as the pane builds it names it so, and `TrackedTag` tags it so;
control, its node name tags it otherwise. `net-bot-yield`: the landing, named off the live
session's seats, names every seat by callsign and tags fifteen bots; the late guest waits on the
match while it runs and after the Restart, not once landed; both chats carry the yield notice.
`menu-original-lobby-bots`: after Fill to five, host and guest each draw three BOT tags and the
header "Players (2 of N) + 3 bots"; control, none before Add Bot. Runs in `bots-d31`:
`RunTests.ps1 -Suite 'net-bot-seat,net-bot-seat-lossy' -SkipUnits -SkipGoldens` 2 passed and
`-Suite 'net-bot-yield,menu-original-lobby-bots'` 2 passed; `RunTests.ps1 -Filter 'menu-,net-,versus-local-bot' -Shards 4
-SkipGoldens`: units 6271 passed / 0 failed / 3 skipped, engine 105 passed / 0 failed, engine errors
clean; `RunTests.ps1 -SkipUnits -SkipEngine`: goldens 24 hash-identical (no golden shows the lobby
or the results board). Captures for the user, through `RunProbe.ps1` on the hidden desktop:
`d31-lobby-host.png` and `d31-lobby-guest.png` (`--menu=lobby:host:bots`, `lobby:guest:bots`),
`d31-before-lobby-host.png` and `d31-before-lobby-guest.png` (the same before D31),
`d31-lobby-late.png` (`--menu=lobby:late`), `d31-game-scores.png` and `d31-game-scores-guest.png`
(`lobby:host:bot-scores`, `lobby:guest:bot-scores`), `d31-versus-board.png` (`--vs --mission=MP1
--mute --players=2 --vs-bots=2 --debug-scoreboard --vs-kills=1 --frames=120`) and
`d31-target-marker.png` (`--vs --mission=MP1 --mute --vs-bots=1 --target=player2 --frames=90`, the
bot off screen in the spyglass, its name line "Black Swan").

**Verified.** Full `RunTests.ps1` on the merged tree (4f95503c, C23 and D31 with main through #148 merged in): build, units 6355 passed / 0 failed / 3 skipped, engine 536 passed / 0 failed / 2 skipped (6 shards, engine errors clean), goldens 24 hash-identical; exit 0.

**⚠ Traps.** `SplitScreen.PlayerColor` is a 4-colour palette taken modulo 4
(`CSVM/src/UI/Boards/SplitScreen.cs:76-82,142`), so colours repeat at 16 pilots. That predates bots
(16 humans repeat too) and stays a separate change; file it if D32 shows it confuses play.
`VersusHud`'s per-seat `Pn` markers still stand until #141 lands; do not tag a bot there. The
results board names a person's seat by `Pn`, a network person's too; naming people by callsign
there is #141's open question, not this item's.

## D32 ☑ Crowded free-for-all playtest at the controls

**Goal.** The user flies a full-field Deathmatch against bots and judges whether the bots fight
convincingly in a crowded arena, rearm sensibly and do not pile into terrain.

**Evidence (confidence: lead-only).** The AI's targeting and manoeuvres have been exercised only in
player-centred fights; no suite flies a dozen hostile AI against each other.

**Approach.** A `playtest.md` entry (minted with `New-ItemId.ps1`) once Waves A to C land.

The watch-list the landed items left for this sortie:
- **Rearm thresholds (TUNE, B14):** `AiRearmOrder.LowAmmoShare = 0.2` of the forward guns' load and
  `DamagedHullShare = 0.35` of whole-vehicle health. Judge whether bots leave the fight too early,
  too late, or too often.
- **Rearm detour (B14):** a bot on a base's closed side flies to a gate 2.5 km out and back in, up to
  about 95 s away from the fight. Only C1's MP1 base was flown; watch the other chapters' bases.
- **Lay-off assist (B11):** `AiModeMachine.UpdateLayOff` eases a bot off a human it targets who chases
  it and falls behind; a bot never lays off for a bot. Decision 10 does not cover it; judge whether it
  reads as fair or as bots going soft on people.
- **`--hold=` (B14, INSTR-101):** a hold script outranks the AI pilot, so a scripted run with bots
  and `--hold=` flies the bots on the script. Not a play issue; a probe trap.

**Model recommendation.** None; the user's sortie and an orchestrator log read.

**Verify.** The user's verdict at the controls.

**Verdict.** Two sorties, local MP1 with fifteen bots. The first lost a bot to the AI ram rule two
seconds in, which became B15 and B16. On the second (the B15/B16 build) the user's verdict is that
the Dogfight is good; it was too chaotic to judge the watch-list at the controls, so the log was read
for it (`vs-20261006-193354.log`, about 3.7 minutes). Every one of the ten deaths was gunfire: no
bot flew into terrain or a structure, no `AI ram` death, four harmless grazes. The rearm order fired
eighteen times, every one on hull (health 0.02 to 0.34) and never on ammunition (the guns read 0.91
to 1.00 each time); all eighteen chose the same of MP1's two bases; three ended in a restore after
46, 59 and 71 s away, and the rest were shot down en route or still flying at the log's end. The
ammunition threshold that never tripped is replaced by B17: the user ruled that a bot rearms when
every rocket pylon is empty or its hull is badly damaged, never for its guns. That every trip goes
to one base stays TUNE for a later playtest; the lay-off assist logs nothing, so it was not read. The sortie also
showed a held Respawn control placing the user's plane six times after a crash, an older defect
fixed on this branch (84bad29a) and not a bot one.

**⚠ Traps.** Check which build is running before reading a symptom as a bot defect.

## D33 ☑ Host cost of fifteen bots, measured

Measured on the user's rig and on the Steam Deck.

**Goal.** A number for the host's step cost with fifteen bots, and a judgement whether it holds on
the host machines that matter (the user's rig, the Steam Deck).

**Evidence (confidence: traced-to-code for where each cost lands; the Deck measured).** A bot's whole flight step runs in the
`HumanAircraft` phase (`GameSession`'s `StepHumanAircraft` walks every seat rig, then
`BroadcastAircraftState`), its rearm order in `Versus` (`RearmPlay.Step`), its rounds in
`Projectiles`, and its rig's animation runtime in `AnimAdvance`. A bot is never in
`AiAircraft`, so `--perf`'s `ai_ms` reads 0 for it. The lead's cost list held except for its
weights: the `AiPilot` decision is about 4 µs a bot a tick, the state send to one guest about 3 µs,
the `PersonSeatPositions` scan about 1 µs, and the rearm order with the match clock about 1 µs. The largest single
term is one the lead did not name: the pose write at the end of `FlightController.SimStep`
(`GlobalTransform = _simCurr`), 31 to 49 µs a bot a tick, because each bot's rig adds about 1,195
nodes (17,578 nodes with no bot, 35,501 with fifteen) and Godot walks the whole subtree on a
transform write. It is the same write every aircraft rig makes, a pane's and a world AI's, so it is
not a bot defect and not quadratic in the field.

**Approach (landed: a measurement, no code).** The instrument is the repo's own `--perf` on the
wall clock (`--no-det --no-vsync --seed=1`), read per tick as PERF-1 and PERF-37 say:
`phys_tick_ms` (the whole tick, the session step inside it) and `phys_hz` (sim seconds per wall
second, the cadence the trap below asks for), with `sim_ms`'s `HumanAircraft`, `Projectiles` and
`Versus` slots as the bots' share of the tick and `proc_sites_ms`'s `Flight` as their per-frame
presentation. `--det` was not used: it moves the step into the process pass and steps the sim per
rendered frame, which a network pair cannot share. Windows from sim frame 1200 on (20 s in, the
bots fighting: six respawns per 15-bot run, rounds in `Projectiles`), 40 windows a run, 3600 frames
each, MP1, seed 1. A temporary bracket split the bots' step by part on both the wall clock and the
main thread's own cycles (PERF-36); it was removed and is not in the tree.

Readings, ms, median (min to max) over six or seven local runs (`--vs`, one idle pane plus N bots)
and two or three network runs (`--net-host=127.0.0.1` plus one `--net-join` guest that stays to the
end; 15 bots requested, 14 seated, since the guest takes the sixteenth seat). `phys_hz` read 59.98 to 60.04 in
every run: the sim kept wall time throughout.

| Bots | `phys_tick_ms` | bot phases of `sim_ms` | sim step, thread CPU | bots' own step, CPU | of it the pose write | `Flight` per frame |
|---|---|---|---|---|---|---|
| 0 local | 1.61 (1.31-1.71) | 0.20 (0.18-0.23) | 0.23 (0.20-0.23) | - | - | 0.15 (0.13-0.20) |
| 1 local | 1.55 (1.41-2.24) | 0.32 (0.29-0.53) | 0.35 (0.33-0.37) | 0.10 (0.10-0.11) | 0.04 (0.03-0.04) | 0.20 (0.18-0.40) |
| 4 local | 2.16 (1.78-2.82) | 0.73 (0.56-0.99) | 0.69 (0.59-0.79) | 0.38 (0.31-0.44) | 0.15 (0.11-0.18) | 0.37 (0.29-0.54) |
| 8 local | 2.25 (2.00-3.35) | 0.99 (0.86-1.53) | 1.01 (0.94-1.08) | 0.69 (0.64-0.75) | 0.30 (0.27-0.33) | 0.52 (0.47-0.80) |
| 15 local | 3.63 (2.73-4.25) | 2.03 (1.49-2.36) | 1.53 (1.48-2.03) | 1.18 (1.14-1.58) | 0.49 (0.47-0.73) | 1.04 (0.75-1.29) |
| 0 network | 1.79 (1.65-1.86) | 0.32 (0.28-0.35) | 0.40 (0.32-0.43) | - | - | 0.36 (0.31-0.39) |
| 8 network | 3.41 (2.74-4.09) | 1.65 (1.28-2.01) | 1.67 (1.52-1.81) | 1.04 (0.90-1.18) | 0.51 (0.42-0.61) | 0.98 (0.79-1.18) |
| 14 network | 4.13 (3.73-4.19) | 2.36 (2.10-2.42) | 2.49 (2.07-2.86) | 1.83 (1.43-2.07) | 0.91 (0.65-1.09) | 1.23 (1.15-1.34) |

The thread-CPU columns come from three of the local runs (two at 1, 4 and 8 bots). The bots' step
at fifteen, by part (CPU, those runs): pose write 0.47 to 0.73, collision sweep 0.12 to 0.14,
gunner 0.08 to 0.10, fire step 0.07 to 0.09, `AiPilot` 0.05 to 0.06, flight model 0.04 to 0.05,
far-field scan 0.01 to 0.02, and 0.3 to 0.4 elsewhere in the step. The network host's state send
was 0.01 to 0.02 ms with no bot and 0.05 to 0.07 ms with fourteen; the `Versus` phase (the rearm
order with the match clock) 0.004 with none and 0.013 to 0.022 with fifteen. Another session's
engine runs shared the machine for most readings (a six-shard battery through two of the three
network runs at each count), which inflates the wall terms and, through shared cores, the thread
clock too. The quietest pair, no other engine process alive, read 1.55 and 2.93 ms `phys_tick_ms`
(0.23 and 1.48 ms of thread CPU) locally at 0 and 15 bots, and 1.65 and 3.73 ms (0.32 and 2.07)
on the network host with one other process. `AnimAdvance`, the world's
animation, moves between 1.0 and 1.7 ms from run to run whatever the field, and is most of the
spread in `phys_tick_ms`.

**Judgement.** On the user's rig a host with fifteen bots spends about 2 ms more per tick than one
with none (2.0 ms at the local `phys_tick_ms` medians; 1.3 ms locally and 2.1 ms on the network
host at the thread-clock medians), 0.09 to 0.14 ms a bot, against the 16.7 ms tick, so the tick
stays near a quarter of its budget and the sim never fell behind wall time. It holds with room to
spare, local or network. No threshold is warranted on the rig.

**On the Steam Deck, measured** (the user's two runs of the Verify command on the `bots` build in
`~/CSVM`, local MP1, `--perf --no-det --no-vsync --seed=1`, windows from sim frame 1200 on; the
lighter run seated one bot rather than none):

| Deck | `phys_tick_ms` mean (min to max) | `phys_hz` mean (lowest window) | fps mean | `frame_ms` | `gpu_ms` |
|---|---|---|---|---|---|
| 1 bot | 2.04 (1.73-2.66) | 59.99 (58.7) | 93.1 | 10.97 | 10.47 |
| 15 bots | 4.69 (4.23-6.03) | 60.01 (58.5) | 92.2 | 11.10 | 9.31 |

Fourteen more bots cost the Deck 2.65 ms a tick, about 0.19 ms a bot (1.4 times the rig's), inside
the CM24 projection of 2 to 3 ms and far under the provisional 8.3 ms threshold; `phys_hz` keeps wall
time and the frame rate does not move, because the Deck's frame is bound by its GPU (9 to 10 ms),
not by the sim. The projection that a full field would cost a Deck host its 60 fps did not hold. A
full field is fit to offer on a Deck host, and Fill-to-N keeps its default.

**Hot spot, not fixed here.** The pose write is 40 to 50 per cent of a bot's step and scales with
the rig's node count (about 1,195 nodes a bot), not with the field squared. Every aircraft rig pays
it; a lever would be fewer nodes under the transform root of an aircraft rig (the pre-warmed crash
rig emitters, about 200 a rig, are one candidate) or a write that does not walk them. Measure it on
the Deck before choosing.

**Model recommendation.** Sonnet would do the runs and the arithmetic. Opus was used because the
instrument had to be chosen against PERF-1, -36, -37 and -42 and the readings separated from a
shared, loaded machine.

**Verify.** Every run through `RunProbe.ps1` on the hidden desktop, from the worktree, with
`$env:CSVM_DATA_ROOT="Z:\CSVM"`: local, `RunProbe.ps1 -TimeoutSec 300 --vs --mission=MP1 --mute
--perf --no-det --no-vsync --seed=1 --vs-bots=<N> --screenshot=<png> --frames=3600` (no
`--vs-bots` for the baseline); network, the host as the same plus `--net-host=127.0.0.1:47781`, and a guest started 6 s later with `--vs --mission=MP1 --mute --no-det
--no-vsync --net-join=127.0.0.1:47781 --screenshot=<png> --frames=6600`, so it stays until the host
quits. Read the `[perf] window` lines from sim frame 1200. The Deck reading, with a build that
carries the bots in `~/CSVM`, in Desktop Mode or over ssh with `DISPLAY=:0` and
`CSVM_DATA_ROOT=/home/deck/CSVM`:
`cd ~/CSVM && ./CSVM.x86_64 -- --vs --mission=MP1 --mute --perf --no-det --no-vsync --seed=1
--vs-bots=15 --screenshot=/tmp/d33-15.png --frames=3600`, then the same without `--vs-bots=15`
(the shot is blank without `--det`, which does not matter here). From the newest file in
`~/CSVM/logs/`, read each run's `phys_tick_ms`, `phys_hz` and `fps` from sim frame 1200 on.

**Verified.** A measurement with no code change: the readings above are its verification, the rig's taken on the plan tree at da019b0d with the build at 0 warnings and 0 errors, the Deck's on the `bots` build a6303e57 installed in `~/CSVM`.

**⚠ Traps.** Read the sim-clock cadence (`phys_hz`), not `physics_ms`, on the user's rig. A guest
given the host's frame count finishes first under `--no-vsync`, and with no bot the host's match
then ends `AllAlone` and its tick drops to the results board's: give the guest more frames. Every
scripted network pair here but one process exited `0xE0434352` at quit on the ENet service thread
(GitHub issue #144), after its readings; read the screenshot line, not the exit code.
