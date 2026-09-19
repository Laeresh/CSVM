# Combat voice, the 29 triggers and the accent chain

Part of the [format documentation](README.md). The AI's radio chatter: which events make a pilot
speak, which pilot's voice they speak in, and the rules that decide whether the line plays at all.
This page covers the
dispatch, read from `crimson.exe`. Claims name the evidence at the point of use; no code is
reproduced.

## The trigger table

The engine carries **29 triggers, ids 0–28**, as a contiguous ordered table of `TYPE` tokens in
`.rdata` (`0x0062af5c`–`0x0062b0d4`). The count is not inferred from the strings: the loader that
populates a pilot's voice slots runs `i` from `0` to `0x1d` exclusive.

| id | Token | Fires when |
|---|---|---|
| 0 | `WA-Turret-A` | a turret acquires the player and has line of sight, broadcast |
| 1–12 | `WA-Enemy-{12,3,6,9}{L,·,H}` | bearing call-out; see the index formula below, broadcast |
| 13 | `WA-HighDmg-A` | the player's health crosses a 30 % threshold, broadcast |
| 14 | `WA-Attack-A` | a pilot commits to an attack on the player |
| 15 | `PR-DngrZn-A` | a Danger Zone run, broadcast |
| 16 | `PR-EnemyDwn-A` | the local player downs an aircraft hostile to the player, broadcast |
| 17 | `DI-LowDmg-A` | the speaker's health drops below **70 %** |
| 18 | `DI-MedDmg-A` | …below **50 %** |
| 19 | `DI-HighDmg-A` | …below **30 %** |
| 20 | `DA-Bail-A` | the speaker is destroyed and is **on the player's team** |
| 21 | `DE-Bail-A` | the speaker is destroyed and is **not** |
| 22 | `GL-AllyDwn-A` | gloat by the killer; the plane it downed was on the player's team |
| 23 | `GL-EnemyDwn-A` | gloat by the killer; the plane it downed was not |
| 24 | `GL-PlyrDwn-A` | gloat for the local player's own kill; raised on the player and broadcast |
| 25 | `TA-FailTail-A` | taunt, a pursuer failed its tail check |
| 26 | `TA-FailShk-A` | taunt, a shake attempt failed |
| 27 | `TA-SucShk-A` | taunt, the speaker shook its pursuer (fires as the reaction flag clears) |
| 28 | `DS-Ally-A` | ally distress; the local player's round damaged a friendly, or the last of three tracked planes is alive |

⚠ **The token in the table is a family root, not a clip name.** The `-A`/`-B`/`-C` variants and the
`Bail`/`NoBail` split are resolved below this layer, trigger 20 selects `DA-*`, not
`DA-Bail-A` specifically. A reader that treats the table entry as a filename will find most of the
shipped clips unreachable.

Two entries in the table settle questions the clip survey could only guess at:

- **`DA` is the ally counterpart of `DE`, and the split is by team, not by outcome.** Both are the
  dying pilot's own death cry; id 20 fires if the dying aircraft is on the player's team and id 21
  if it is not. Both the split and its polarity are read from the dispatch.
- **`TA-FailTail` is a real engine trigger with a real dispatch site**, not an orphan clip family.
  The survey noted the design's taunt table omits it; the engine does not.

### The gloat triggers and trigger 28

Both were traced out of the vehicle damage path
([`org/vehicleDamage.md`](../org/vehicleDamage.md), "Teams and friendly fire"), which is where the
team predicate used below (*the two teams are equal, or either one is 0*) is written down.

**Triggers 22 to 24 are spoken by the killer, and the 22/23 split is decided by the victim, not by
the speaker** (`0x004ba125`–`0x004ba194`, in the take-hit body `FUN_004b9bc0`, on the branch where
the victim's health has just crossed zero). The dispatch runs the predicate twice. First over
shooter and victim: friendly, and **no gloat is chosen at all**, so a friendly kill is silent.
Otherwise over victim and the local player: friendly picks 22, hostile picks 23, and either way the
call is made with the **shooter** as the speaker. This closes the polarity question the page left
open.

**A kill by the local player takes a different arm, and that arm carries trigger 16.** The killer is
compared against the local player's vehicle once, early in the same function (`0x004b9d54`), and the
result is held in a flag the death branch reads at `0x004ba15c`. On the player's side of that flag,
two calls run in order:

- **Trigger 24 is addressed to the player's own aircraft**, not broadcast (`0x004ba173`, guarded by
  the slot read at `vehicle + 0x850`, which is slot 24). The player rig is the speaker, so the line
  exists only when the player's own vehicle resolved a voice set.
- **Trigger 16 (`PR-EnemyDwn`) is then broadcast to the flight** (`0x004ba178`, the push of `0x10`
  into the broadcast helper at `0x004b86a0`). It runs whether or not slot 24 held a line, because
  the null-slot test at `0x004ba16b` jumps *into* the broadcast rather than around it. So the
  flight's "enemy down" call is the reliable half of a player kill and the player's own gloat is the
  conditional half. The clips back this: **17 pilot ids ship a `PR-EnemyDwn` set** (1, 2, 4, 6, 7,
  12, 14, 20, 24, 26, 27, 28, 29, 31, 42, 44, 48), 51 WAVs of three takes each with their
  `snd_PR-EnemyDwn-A_id<N>_random` groups, and none of them is one of the def-only ids, so every
  authored set is playable.

⚠ **Slot 16 is never read at an immediate offset, so searching for `vehicle + 0x7f0` finds nothing.**
Every dispatch reaches a slot through the trigger id: the gate scales it at `0x004afdd9` and the
broadcast helper at `0x004b872e`, both as `base + 3 * id * 4 + 0x730`. The immediate slot offsets
that do appear (`0x838`, `0x844`, `0x850`) are the gloat triggers' own null tests, inlined because
those three are dispatched to a named aircraft rather than elected.

**Trigger 28 has two dispatch arms**, both in `FUN_004b9770` and both on a hit rather than a death.
The first (`0x004b98e0`) fires when the local player's round damages an aircraft the predicate calls
friendly: the **struck** aircraft speaks, if it owns a line for the trigger. The second
(`0x004b98fc`) is the recorded one, and it fires when exactly one of three globally
tracked planes (`DAT_0071c4e4`/`e8`/`ec`) is still alive; it also installs a default sound set
(`DAT_0071c4f4`) into the speaker's slot 28 first. ⚠ **The second arm sits on the *hostile* side of
the same predicate**, so the three tracked planes are not on the player's team and the
"the player's wingmen" reading of them is not supported by this dispatch; what they are was not
chased here.

### The bearing call-outs are computed, not enumerated

Triggers 1–12 are `WA-Enemy` bearing warnings, ordered **low, level, high** within each clock
bearing, and the engine indexes them arithmetically:

```
id = 1 + 3 * bearingIndex + altitudeBand
     bearingIndex ∈ {0,1,2,3}  →  12, 3, 6, 9 o'clock, wrapping at 4
     altitudeBand ∈ {0,1,2}    →  L (below), level, H (above)
```

This is the family only **7 of 31 pilot ids** carry any clips for (§ 6). A pilot without them simply
has a null slot for those 12 ids and stays silent on bearings, which is the design's claim that
positional detail is a per-pilot capability, expressed as missing data rather than as a check.

## Where a pilot's voice comes from

Each aircraft holds **29 voice slots**, one per trigger, at `vehicle + 0x730 + 12 * triggerId`.
Each slot is three words: the sound set, the handle of the line currently playing, and the time the
slot is next allowed to speak. A slot whose sound set is null means *this pilot has no line for
this trigger*, every dispatch site tests for that before rolling anything.

The slots are filled at spawn from the pilot's voice set, which resolves through the chain the
scoping study described:

```
aiv accentID (slot 65)  →  a row in zrdr/voice.zrd  →  a pool of pilot VO ids
                        →  one pilot's clip set     →  29 trigger slots
```

⚠ **`voice.zrd` is the accent table, not the trigger table.** Its 35 rows are keyed by `accentID`
and their values are **pilot VO ids** (`soundsh/VO_id<N>_*`), the numbers run over the same sparse
id set the clip survey found. The row count coincidentally sits near the trigger count; they are
unrelated tables and conflating them will mis-key every lookup. Row shape: `[accentId, voId, …]`;
rows 0–10 hold 2–3-id pools, rows 11–34 a single id.

In multiplayer the accent lookup is skipped and the voice set is handed in directly, so a remake's
single-player path is the one that needs `accentID`.

### The clips are sounds.json entries, and the data picks the variants itself

The extraction shows:

- **Every combat clip has an ordinary `SETS` entry**: sounds.json carries one set per pilot id
  (`id1` … `id48`, 35 sets), whose entries are `snd_id<N>_<TYPE>` → `VO_id<N>_<TYPE>.wav` with
  `PURGEABLE`, `QUEUE [0.5]` and (on most `-B`/`-C` takes) `OPTIONAL`. 1,414 clip defs total. So
  the ordinary sound pipeline plays a voice line; no separate loader exists.
- **The `-A`/`-B`/`-C` selection is authored data, not code.** sounds.json ships **466**
  `SOUND_GROUPS` entries named `snd_<FAMILY>-A_id<N>_random`, each a `DYNAMIC_WEIGHTS 0.5` group
  over that pilot's takes of one sub-family (`snd_DA-Bail-A_id2_random` picks `DA-Bail-A/B`).
  This closes the open question below for the variant letter; the `Bail`/`NoBail` split above it
  is still the dispatcher's. The 12 bearing tokens have no variants and no groups.
- ⚠ **A def is not a clip.** Five pilot ids (13, 15, 17, 35, 36) ship full 25-def sets with **no
  WAVs at all**, id 44 ships the 12 bearing defs without their WAVs (so the *playable* bearing set
  is 7 ids, the def-side set 8), and the accent table maps ids 5 and 40 which have neither defs
  nor WAVs. Id 47 (the multiplayer announcer) is the inverse: 51 WAVs with no `id47` set.
  Availability is only answerable after decode, per pilot, per clip.
- ⚠ **Nine of the 35 accent rows reach an unplayable pilot, and seven of those are single-id pools
  with nothing to fall back to**: accents 14 → id 5, 19 → id 15, 20 → id 17, 21 → id 17, 27 → id 35,
  28 → id 36, 30 → id 40. Accents 0 and 9 name a dead id inside a pool that also holds live ones, so
  they still speak. This is not confined to the far end of the table: **eight of the campaign's 26
  named aces (slot 67, [ai-rosters.md](ai-rosters.md)) sit on a dead single-id accent** and are
  silent on this channel, A. Dixon (accent 20), C. Steele (28), Sir Charles Emmett Winthrop (19)
  and Utah Blacke (30), across seven missions. Two further ace blocks author no accent at all. A
  named ace's combat chatter therefore cannot be assumed to exist; where an ace speaks in the
  original it is usually the mission's own dialogue chain, which is a different system (below).
  Instant Action pays the same price on its **second wingman**, whose slot accent 14 is one of the
  seven dead single-id rows ([instant-action.md](instant-action.md), "The player and the
  wingmen"); its other four slots and all thirteen militia wave accents reach live ids.

**Runtime (`CSVM/src/Mech3/CombatVoice.cs`).** The chain above is a queryable service:
`accentID` → pool → `PilotFor` (random pick, clipless ids skipped) → `PlayableFor(voId, family)`,
which returns the `_random` group when authored, else the bare def; both feed
`MissionRadio.Speak`, the flat Voice-bus queue the objective callouts share, because the original
queues a combat line on that one channel with no position ([sounds.md](sounds.md), "There is one
queue"). A line therefore plays at its authored level wherever the speaker is, and a bark that
arrives while the channel is busy waits its 0.8 s tolerance and is then dropped. Because a clip that
was not prewarmed while the sound archive was open never plays, a flight session prewarms the
mission roster's own accents (`CombatVoice.SessionPrewarmNames`, wired through
`WorldSession.Options.VoiceClipNames`): median 24 clip defs per mission, worst case 457 (C2/M03),
21 of 53 missions author none. Prewarming everything was measured at 1,258 streams / 60.7 MB PCM
/ ~0.7 s and rejected. E16's dispatch (the gate, cooldowns and elections below) sits on top of
this seam.

## Whether a line actually plays

Every trigger goes through one gate function. In order:

1. **Voice must not be globally muted**, and the mission clock must be **past 2.0 s**, nothing
   speaks in the first two seconds.
2. **The speaker must be alive**, unless the caller passes a *force* flag. Only the death cries
   (ids 20/21) do, because the speaker has just been marked dead.
3. **The speaker must not already be talking**, and must not be in the suppressed state.
4. **The talker roll.** `rand01 < talkerChance`, where `talkerChance` is the per-pilot value the
   `talker` skill slot indexes out of `ai_skill_parameters` (0.25 at rating 1 → 0.95 at rating 9,
   [ai-rosters.md](ai-rosters.md)).

⚠ **Triggers 1–12 have their talker chance halved**, hardcoded. The bearing call-outs are
deliberately half as likely as everything else, on top of only 7 pilots owning the clips at all.

The binary carries the debug strings for both outcomes, *"Talker test passed. Play AI sound #%d."*
and *"Talker test failed. Don't play AI sound #%d."*, which is how the numeric trigger id was
first spotted.

### The cooldown is armed by failure too

⚠ **Each slot has a 15-second cooldown, and losing the talker roll starts it exactly as winning
does.** The failure path is the success path minus the play call: both stamp `now + 15` into the
slot. So a low-`talker` pilot is not merely quieter, a failed roll silences that trigger for the
next 15 seconds rather than letting it retry on the next event.

This is the single behaviour most likely to be got wrong by re-deriving from the design prose,
which describes `talker` only as a volume-of-chatter stat.

### Broadcasts elect one speaker

Several triggers are not addressed to a pilot at all, they are broadcast to the flight. The
broadcast helper (`0x004b86a0`):

1. collects every AI pilot that is **alive**, **not the player**, on the **local player's** team or
   teamless, and **owns a non-null slot for that trigger**;
2. picks one **at random**;
3. runs the gate above on it, and **if the talker roll fails, moves to the next pilot in the list**,
   wrapping, until one succeeds or every candidate has been tried.

So the flight speaks with one voice per event, and a quiet pilot passes the line along rather than
swallowing it. ⚠ **The team the helper compares against is always the local player's**, not the
team of whatever raised the event: the helper takes only a trigger id and reads the player vehicle
itself, so a broadcast raised by a hostile still elects a speaker from the player's own flight.
Triggers 0, 13, 15, 16 and the bearing call-outs dispatch this way, at the call sites `0x004ab098`,
`0x004ba316`, `0x004469ff`, `0x004ba17a` and (with the index computed rather than pushed)
`0x004708e0` and `0x0041dcfa`; the gloat, distress, death and taunt triggers address a specific
aircraft.

## The remake's dispatch sites

The rules above are represented in `CSVM/src/Flight/AiVoiceDispatcher.cs` (the gate, cooldowns,
halving, election, DI tiers, bearing index, engine-free, seeded) and wired by
`CSVM/src/Session/AiVoiceRuntime.cs`. Where the original's dispatch site is
decoded, the remake uses it; where only the trigger's meaning is decoded, the chosen stand-in
site is recorded here. The runtime watches the mode machine of every AI the session hands it,
whether or not that aircraft resolved a voice of its own: rows 1-12 and 25 below are spoken by an
aircraft other than the one whose mode moved, and the shipped rosters leave nearly every enemy on
`accentID` -1, so watching only the voiced aircraft leaves those rows silent for a whole mission.
Every aircraft's death report is watched for the same reason, human rigs included: rows 22 to 24
are spoken by the killer, not by the aircraft that died.

| ids | status | site / reason |
|---|---|---|
| 1–12, 14 | wired | our chosen site: the mode machine's patrol→pursue transition against a human target ("committing to an attack"), the attacker speaks `WA-Attack`, and the flight broadcasts the bearing call-out computed in the warned player's frame. The original's exact "enemy spotted" event is undecoded; this is the closest transition the machine has |
| 13 | wired | a human rig's summary health crossing 30 % on the projectile hit path (decoded threshold), broadcast |
| 17–19 | wired | the speaker's own summary health on the projectile hit path, 70/50/30 % most-severe-first (decoded) |
| 20–21 | wired | `FlightController.Downed`, with force: id 20 (`DA`) when the dying aircraft's `Team` is `AimAssist.PlayerTeam`, id 21 (`DE`) otherwise (`AiVoiceRuntime.RegisterAi`). Free flight and `--vs` still give every AI its own default team, so `DA` stays dormant there in practice, it fires once a mission places an AI on the player's team |
| 22–24 | wired | the same `FlightController.Downed` report read for its killer (`AiVoiceRuntime.OnDowned`), the decoded order of the two predicates: friendly over shooter and victim and no gloat is chosen, else friendly over the victim and `AimAssist.PlayerTeam` picks 22, hostile picks 23, and the killer speaks it. A kill by a rig registered through `RegisterPlayer` takes 24 instead and broadcasts on the player's team, which is a stand-in: the original addresses 24 to the player's own aircraft and broadcasts 16 (row 16). A killer that resolved no voice of its own is silent |
| 25 | wired | a pursuer's failed sixth-sense (tail) check stunning it, its evading AI target speaks; a human evader stays silent (the player speaks no AI lines) |
| 26 | wired | our chosen stand-in for the undecoded shake-attempt check: the speaker's evade episode ending with the flag still up, which is the decoded tail-cone hold test (`AiModeMachine.EvadeClearAlignment`) answering that the pursuer's nose is still on it. The dwell reverting the task, a stun and an avoid-crash climb-out are the ends that reach it |
| 27 | wired | the same episode end with the flag already cleared, the pursuer shaken ("fires as the reaction flag clears", decoded). An episode the speaker leaves with the flag still up is 26, not this. A target lost mid-reaction reads as a shake, the flag's own clear rule with no pursuer left to test |
| 0 | unwired | turret acquisition is `TurretController`'s event; owned by C9's thread, not wired from here |
| 15 | unwired | the danger-zone modes are never entered (their gate data is undecoded, F17) |
| 16 | unwired | the original broadcasts it on the local player's kill of a hostile (above). The remake broadcasts 24 on that event instead of addressing 24 to the player's own rig, so the two rows move together, left for a future item |
| 28 | unwired | both arms (above) are answerable now that a team model exists, no dispatch site chosen yet, left for a future item |

Stand-ins and inventions, named:

- **Speakers register on their real `FlightController.Team`**, the teamless stand-in is retired.
  Free flight and `--vs` still give every pilot its own default team (`AimAssist.TeamOfPilot`, pilot
  N = team N+1), so a broadcast only ever elects a "teamless" match there in practice; it goes live
  the moment a mission puts two AI, or an AI and the player, on the same explicit team.
- **`Bail`/`NoBail` is a constitution roll** (`constitution_chance`, 0.35→0.95), the open
  item's natural-candidate reading, implemented and marked unconfirmed.
- **Bearing quantisation**: the four clock quadrants split at ±45° (the natural reading of a
  nearest-quadrant index), and "level" is ±100 m (`AiVoiceDispatcher.LevelBandM`, invented).
  Only the index formula itself is decoded.
- **Pilot identity**: an `--ai=` spawn takes an optional `accent=<id>` segment
  ([cli.md](../cli.md)); its accents join the mission roster's prewarm set. A spawn without one
  is voiceless. An Instant Action mission's actors join it the same way
  (`InstantActionRuntime.VoiceAccentIds`): the ace's `ace_accentID` on `dogfight_ace`, each
  configured wingman slot's accent, and a wave's `enemy_accentID`, whose value of 12 joins its
  whole 12 to 16 re-roll range because the roll happens at spawn, after the archive has closed
  ([instant-action.md](instant-action.md)). A dispatch refused for want of a clip is logged once
  per speaker and family: a warning when the pilot owns the family's defs and none was prewarmed,
  an info line when it owns none. A campaign roster spawn carries its own slot 65 (`CampaignDirector` hands
  `AiSpawn.AccentId` to `RegisterVoice`), and its talker and constitution slots reach the runtime
  the same way: `RegisterVoice` takes one override per stat, and `GameSession.RegisterAiVoice`
  looks up `talker_chance` at the block's own talker rating and `constitution_chance` at its own
  constitution rating, each on its own curve. A rating a block does not author falls back to the
  session's skill rating, same as before.
- **The taunt pair 26/27 is decided by the evade flag**, one dispatch point at the episode's end
  (`AiVoiceRuntime.OnModeChanged`): the original's shake-attempt check is undecoded, so the stand-in
  is the flag's own decoded hold test, the pursuer's nose inside the tail cone. A move between the
  two evade modes is the same episode and speaks nothing.
- The gate's "must not already be talking" is a hook (`AiVoiceDispatcher.IsTalking`) the session
  answers from the radio channel: every combat line is queued with the speaker it belongs to, and
  that speaker is talking while its own line waits or is on air, for the line's own length
  (`MissionRadio.IsSpeaking`). A refused line arms no cooldown, and a broadcast passes a talking
  candidate over to the next one. The suppressed state has no counterpart here.
- **Force bypasses only the aliveness check**, as decoded, a forced death cry still respects
  the slot cooldown and still rolls talker (`AiVoiceDispatcherTests` pins this).

## What is not pinned down

- **What the three globals `DAT_0071c4e4`/`e8`/`ec` are.** Trigger 28's second arm counts their
  survivors, and they are on the hostile side of the team predicate, so the "the player's wingmen"
  reading of them does not hold (see the gloat/28 section above). *(Trigger 22–24's polarity, which
  this list carried as open, was settled there.)*
- **How `Bail`/`NoBail` is chosen** below the family root (the natural candidate is the
  constitution roll, unconfirmed). The `-A`/`-B`/`-C` half closed : the shipped
  `snd_<FAMILY>-A_id<N>_random` groups pick the take, weighted-random with recency 0.5 (see
  "The clips are sounds.json entries" above).

## What this is not

- The **clip inventory**, 2,520 files, 31 pilot ids, the 11 `TYPE` families and their counts, is
  not repeated here. This page covers only what the engine does with them.
- **Mission-scripted dialogue** (`VO_<chapter>-<faction>-<mission>_<Character>_<n>.wav`, 990 clips)
  is a different system, driven by objectives scripting, and does not go through the trigger table.
- The **sound format and the SETS table** are [sounds.md](sounds.md).

## Evidence & limits

This page states current format facts. Claim-specific evidence and limits remain beside the claims they support.
