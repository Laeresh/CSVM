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
| 16 | `PR-EnemyDwn-A` | *(no dispatch site located)* |
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
call is made with the **shooter** as the speaker. The one exception is a kill by the local player,
which takes 24 instead and broadcasts. This closes the polarity question the page left
open.

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

**Runtime (`CSVM/src/Mech3/CombatVoice.cs`).** The chain above is a queryable service:
`accentID` → pool → `PilotFor` (random pick, clipless ids skipped) → `PlayableFor(voId, family)`,
which returns the `_random` group when authored, else the bare def; both feed
`WorldSounds.PlayOneShot`, whose `Node3D` overload follows a moving speaker and whose bus argument
puts the line on Voice rather than with the Effects one-shots sharing that call. Because a clip that
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
broadcast helper:

1. collects every AI pilot that is **alive**, **not the player**, on the caller's team or teamless,
   and **owns a non-null slot for that trigger**;
2. picks one **at random**;
3. runs the gate above on it, and **if the talker roll fails, moves to the next pilot in the list**,
   wrapping, until one succeeds or every candidate has been tried.

So the flight speaks with one voice per event, and a quiet pilot passes the line along rather than
swallowing it. Triggers 0, 13, 15, 24 and the bearing call-outs dispatch this way; the distress,
death and taunt triggers address a specific aircraft.

## The remake's dispatch sites

The rules above are represented in `CSVM/src/Flight/AiVoiceDispatcher.cs` (the gate, cooldowns,
halving, election, DI tiers, bearing index, engine-free, seeded) and wired by
`CSVM/src/Session/AiVoiceRuntime.cs`. Where the original's dispatch site is
decoded, the remake uses it; where only the trigger's meaning is decoded, the chosen stand-in
site is recorded here:

| ids | status | site / reason |
|---|---|---|
| 1–12, 14 | wired | our chosen site: the mode machine's patrol→pursue transition against a human target ("committing to an attack"), the attacker speaks `WA-Attack`, and the flight broadcasts the bearing call-out computed in the warned player's frame. The original's exact "enemy spotted" event is undecoded; this is the closest transition the machine has |
| 13 | wired | a human rig's summary health crossing 30 % on the projectile hit path (decoded threshold), broadcast |
| 17–19 | wired | the speaker's own summary health on the projectile hit path, 70/50/30 % most-severe-first (decoded) |
| 20–21 | wired | `FlightController.Downed`, with force: id 20 (`DA`) when the dying aircraft's `Team` is `AimAssist.PlayerTeam`, id 21 (`DE`) otherwise (`AiVoiceRuntime.RegisterAi`). Free flight and `--vs` still give every AI its own default team, so `DA` stays dormant there in practice, it fires once a mission places an AI on the player's team |
| 25 | wired | a pursuer's failed sixth-sense (tail) check stunning it, its evading AI target speaks; a human evader stays silent (the player speaks no AI lines) |
| 27 | wired | the speaker's own evade/evasive-maneuver reaction completing ("fires as the reaction flag clears", decoded) |
| 0 | unwired | turret acquisition is `TurretController`'s event; owned by C9's thread, not wired from here |
| 15 | unwired | the danger-zone modes are never entered (their gate data is undecoded, F17) |
| 16 | unwired | no dispatch site located in the binary (above) |
| 22–24 | unwired | the polarity is decoded (above) and the 22/23 split is answerable now that a team model exists, no dispatch site chosen yet, left for a future item |
| 26 | unwired | the original's shake-attempt check is undecoded; no machine transition maps to it without force-fitting |
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
  is voiceless. A campaign roster spawn carries its own slot 65 (`CampaignDirector` hands
  `AiSpawn.AccentId` to `RegisterVoice`), and its talker and constitution slots reach the runtime
  the same way: `RegisterVoice` takes one override per stat, and `GameSession.RegisterAiVoice`
  looks up `talker_chance` at the block's own talker rating and `constitution_chance` at its own
  constitution rating, each on its own curve. A rating a block does not author falls back to the
  session's skill rating, same as before.
- The gate's "must not already be talking" is a hook (`AiVoiceDispatcher.IsTalking`), unwired:
  the remake's one-shots carry no per-speaker playing state yet.
- **Force bypasses only the aliveness check**, as decoded, a forced death cry still respects
  the slot cooldown and still rolls talker (`AiVoiceDispatcherTests` pins this).

## What is not pinned down

- **What the three globals `DAT_0071c4e4`/`e8`/`ec` are.** Trigger 28's second arm counts their
  survivors, and they are on the hostile side of the team predicate, so the "the player's wingmen"
  reading of them does not hold (see the gloat/28 section above). *(Trigger 22–24's polarity, which
  this list carried as open, was settled there.)*
- **Trigger 16 (`PR-EnemyDwn`) has no located dispatch site.** It may be reached through a path not
  covered, or be unused.
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
