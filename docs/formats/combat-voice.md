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
| 14 | `WA-Attack-A` | a pilot puts a round at the player ("The pursue path") |
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
| 25 | `TA-FailTail-A` | taunt by an aircraft holding the player as its target, with the player inside its own astern cone; it has failed to keep its tail clear |
| 26 | `TA-FailShk-A` | taunt by an aircraft holding the player as its target, with the player inside its own nose cone; the player's shake attempt has failed |
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

### Which pilot of a pool an aircraft takes

**The pool is dealt in order, not drawn.** The vehicle constructor (`FUN_0047c210`) copies the
def's accent into `vehicle + 0x980` and calls the voice setup `FUN_00477790` (`0x0047d74f`). In
single player that asks `FUN_004b4af0` for the accent's pilot, and `FUN_004b47a0` answers with the
id under the accent row's cursor (row `+0x14`), then advances the cursor, wrapping from the pool's
end to its start. No random stream is read. The cursor starts on the pool's first id: the
`voice.zrd` loader `FUN_004b4cd0` rewinds it after every id it adds (`FUN_004b4a70`), and the
mission vehicle loader `FUN_004735b0` runs that loader (`0x004735d7`) after `FUN_004b4e40` has
cleared the table, so each mission starts every accent afresh. The Nth aircraft a mission builds on
an accent therefore takes the pool's `N mod size`-th id, in authored order. A dealt id with no clips
is not skipped: its slots stay null and that aircraft is silent (accent 0 deals 18, 35, 37, and the
aircraft dealt 35 says nothing).

**In multiplayer the accent is not read at all.** `FUN_00477790` tests the multiplayer flag
(`FUN_00440ad0`) and takes the VO id its caller hands in. A remote player's vehicle
(`FUN_00497990`, the call at `0x00497c84`) is handed that player's own net record `+0x78`. A
mission vehicle (`FUN_004735b0`, `0x0047531e`) is handed `FUN_00499c60`, which is the *local*
player's record `+0x78`, defaulting to 1. So in the original's network games every peer voices every
mission AI in its own player's voice, and two peers do not agree unless their players picked the
same voice. The remake keeps the single-player deal on every end instead, below. What `+0x78` holds
is the player's chosen voice, next.

### A player's own voice

**The Voice list's value is a pilot VO id.** Player Information's seven rows carry `WGA.LG[R].SF`
48, 2, 24, 29, 44, 26 and 31 (Nathan Zachary, Jack, Black Swan, Paladin Blake, Loyle Crawford,
Gruff Male, Texan Male), ids of the same space the `voice.zrd` pools and the `snd_id<N>_*` sets use.
All seven own a whole set, the player's own praise families (`PR-EngineDst`, `PR-ObjDst`,
`PR-ZepDst`) included. The picked value is the `nVoice` setting (`0x00642f14`, registered at
`0x00401470`, its name at `0x00619314`).

**It reaches each peer as DirectPlay player data, not in a game message.** Opening a session,
`FUN_00412b60` reads the local player's 80-byte player data, writes `nVoice` at `+0x4c`
(`0x00412bf2`) and sets it back through `FUN_005b31f0` (SetPlayerData, which DirectPlay delivers to
every peer guaranteed). `FUN_00414470`, the plane pick, rewrites the data around it and leaves
`+0x4c` as it stands. Each peer's `FUN_00414640` builds the lobby record of an arriving player and
copies data `+0x4c` into record `+0x78` (`0x004147aa`).

**Every aircraft of a player speaks as that pilot.** In multiplayer `FUN_00477790` fills the 29
slots from the VO id its caller hands in (`FUN_004b4b20`: the `snd_%s_id%d_random` group, else the
bare `snd_id%d_%s` def). A remote player's aircraft is handed that player's record `+0x78`
(`FUN_00497990`, read at `0x00497c55`, the call at `0x00497c84`). The local player's own aircraft is
the first vehicle the mission loader builds (`FUN_004735b0`, which makes it the player at
`0x00475397`), so it takes `FUN_00499c60`, its own record's `+0x78`. The talker chance is the
vehicle constructor's, off the def's talker rating or, with none, the session's skill rating
(`0x0047d170`..`0x0047d1d3`).

**Each peer derives a remote player's lines itself** and queues them on the one flat voice channel.
Nothing is sent. Read on the machine that hears the line, with "local side" the local player's team:

| ids | when | site |
|---|---|---|
| 25, 26 | a remote player hostile to the local player and within 1695 m of them, off the remote's own nose; 25 only from its second frame in range | `FUN_00470750`, `0x00470822`, `0x00470849` |
| 17–19 | a remote on the local side whose health falls through 70, 50 or 30 % | `FUN_00498170`, `0x004985cf` |
| 22, 23 | a remote killer's gloat: 22 when it is hostile and the victim on the local side, 23 when it is on the local side and the victim hostile | `FUN_00498bf0`, `0x00498f36`, `0x00498f63` |
| 20, 21 | a remote victim's cry, forced: 20 on the local side, 21 otherwise | `FUN_00498bf0`, `0x00498f67`, `0x00498f9a` |
| 22 | the local player shot down by a remote hostile: the take-hit death branch runs on the victim's own machine with no multiplayer gate, so the killer gloats there when the two aircraft's team fields differ | `FUN_004b9bc0`, `0x004ba14c` |
| 24 | the local player's own aircraft, on a kill the local take-hit decides | `0x004ba173` |

⚠ **The death report's voice arm returns before the gloat and the cry when the victim or the killer
is the local player** (`0x00498ef5`, `0x00498f07`, against `DAT_0071c7ac`). A player therefore
never hears their own death cry, and their machine says nothing when they down a remote player: that
death arrives by `0x12` from the victim's machine, where the take-hit decided it. A remote aircraft's
team is built as 1 or 2 against the local player's (`FUN_00497990`, the `+0x3c` comparison passed to
`FUN_00426d00`), so in a match without teams every remote is hostile, and the 22/23 arm of the
report never fires. What the local aircraft's own team field holds in a network game was not read.

The remake speaks a network player's lines the same way (`Session/Roster/AiVoiceRuntime.cs`). The
choice travels in the co-op pick to the host and in the seat roster to every machine
([`../org/multiplayer-messages.md`](../org/multiplayer-messages.md)), each machine registers every
seat's aircraft as that pilot at the session's skill rating, never elected for a broadcast, and
derives the table above for the seats flown elsewhere. The bearing broadcast that follows a remote's
taunt in `FUN_00470750` is not raised: it elects from the local flight's AI, and no network match of
the remake puts AI beside a hostile player. A campaign co-op host's first seat speaks as Nathan
Zachary whatever it chose, the scripted player whose mission dialogue is his. Every human in co-op is
on one side, so there the lines are a teammate's DI tiers and death cry, a guest's `GL-EnemyDwn` on
an AI it downs, and a player's own 24. A splitscreen seat has no Player Information answer and so no
voice, as a local splitscreen session has none.

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
`accentID` → pool → `PilotFor(accent, turn)` (the deal above, a dealt id with no clip def
answering null) → `PlayableFor(voId, family)`,
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

`AiVoiceRuntime.RegisterAi` counts the turn per accent in the order the session hands its AI over,
from 0 in each session, which is the original's per-mission deal. In a network session this is also
what keeps the voices together: a host and its guests register the same AI in the same order (the
order their admission ordinals already depend on), so every end deals every AI the same pilot with
nothing on the wire. A pick drawn from each end's own voice stream would part as soon as one end
rolls a line the other does not. This departs from the original's network rule above,
which voices every mission AI in the local player's own voice. That rule is a remake-only departure
kept on purpose: every network map's roster (`MP<n>`'s `aiv.zrd`, 21 of them) authors the one
`player` block and no AI, so in the original the rule voices only the local player's own aircraft,
while the remake's campaign co-op flies whole
authored rosters whose accents name distinct pilots, the named aces among them. Voicing those in each
player's own voice would make every wingman and ace sound like the player on that machine, and no
two machines would agree.

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

## The pursue path raises the attack pair

Triggers 14 and 1–12 are raised by the **pursuer**, from two sites, neither of them an edge, and
the taunt pair 25 and 26 rides the second of them.

**Trigger 14 is one call inside the AI's weapon pass** (`FUN_0041f420`, the push of `0xe` at
`0x0041f7a1` and the gate call at `0x0041f7a5`). It sits on the branch that has just put a round
away, past that weapon record's own next-fire stamp, under a single guard: the standing target's
vehicle against the local player's (`DAT_0071c298`). So "a pilot commits to an attack on the
player" is literally *shoots at the player*, with no acquisition edge, no range and no promotion
test, and the slot's 15 s cooldown is the entire re-arm.

**The bearing call-outs are raised from the combat driver** (`FUN_0041d9f0`, state 0, the steering
case the remake splits into patrol, pursue and the evade flag). Every frame, when the pursuer's
target answers the `Target` vtable's `+0x38` player predicate and the pursuer's own evade flag at
`+0xba` is clear, the driver computes the index and broadcasts it (`0x0041dcfa`). Again no range
and no edge: the rate is the 15 s slot cooldown alone, which is why the original's aces call the
player's bearing every few seconds through a whole engagement.

**The quantisation is decoded whole**, so nothing in it is invented any more. The band is the
vertical component of the UNIT vector from the pursuer to the player, split at ±0.3
(`0x006034ac`/`0x006035ac`, read at `0x0041dc72`), about 17.5°: an angle, so the band does not
widen with separation. Above +0.3 the player is the higher of the two and the call-out takes the
`L` variant. The quadrant is the player's own heading minus the bearing to the pursuer, wrapped
into one turn, scaled by 2/π (`0x006035a8`), 0.5 added (`0x006032e0`) and truncated, with 4
folding back to 0: round-to-nearest, so the split falls at ±45°.

**Multiplayer raises the same pair from its own vehicle update** (`FUN_00470750`, called per frame
by `FUN_00470210`, the remote-vehicle interpolation, which the local simulation path never
reaches). There the condition is explicit rather than implied: the actor is not on the local
player's side (`FUN_004952f0`, the record's `+0x3c` against the local record's) and sits within
**1695 m** of the player, and the same taunt pair and bearing broadcast follow (`0x00470822`,
`0x00470849`, `0x004708e0`). That range belongs to the multiplayer path alone; the single-player
driver applies none. The remake's guest does not derive the pair this way; the host relays its AI's
raises instead ([`../org/multiplayer-messages.md`](../org/multiplayer-messages.md), "AI voice
across the link").

### The taunt pair 25 and 26 is the pursuer's own geometry

**Both bearing sites first raise one of the taunt pair, addressed to the pursuer itself.** The block
takes the dot product of the unit line to the player with the pursuer's `+0x198` basis row
(`0x0041dc10`–`0x0041dc3b`, `0x004707de`–`0x00470809`), compares it against **-0.85**
(`0x006035b4` single-player, the double at `0x00607e70` in multiplayer) and **0.7** (`0x006035b0`
in both), and calls the gate with `ECX` holding the pursuer (`MOV ECX,ESI` at `0x0041dc68` and at
`0x00470820`/`0x00470847`, force flag 0). The raise is **addressed, never broadcast**: the aircraft
that speaks is the one whose own nose was measured, and no listener is tested, because the gate
queues the line on the one flat voice channel. Only the bearing id that follows goes through the
election helper.

⚠ **`+0x198` is the vehicle's BACKWARD basis row, not its nose**
([../org/aiControlLaw.md](../org/aiControlLaw.md), "Body frame"), so the two thresholds read the
opposite way round from a forward-axis reading:

- **under -0.85 raises 26** (`TA-FailShk`, `0x0041dc50` / `0x0047081e`): the player lies within
  about **31.8° of the speaker's own nose**. The player has tried to shake this pursuer and is
  still out in front of it, which is the trigger table's "a shake attempt failed" with the failure
  being the player's.
- **over 0.7 raises 25** (`TA-FailTail`, `0x0041dc66` / `0x00470845`): the player lies within about
  **45.6° of dead astern** of the speaker. The speaker has the player on its own six and has failed
  to keep its tail clear.
- between the two cones, neither.

**Nothing on the path reads a tail check, a shake attempt, a stun, or any state but 0.** The
single-player block is gated on exactly two reads, both taken just above it: the `Target` vtable's
`+0x38` predicate answering that the standing target is the local player (called at `0x0041dad8`),
and the speaker's own evade flag `+0xba` being **clear** (`0x0041dc02`). An aircraft in its own
evade reaction therefore taunts nothing, and neither does one whose target is not the player. The
multiplayer block reads the side predicate and the 1695 m range in their place, plus one latch:
25 also requires the net record's `+0x1082` byte (`0x00470829`) and clears it on the raise
(`0x0047084e`), but the same block sets that byte again at `0x004708e8` on every frame it runs, so
the latch suppresses only the first frame a remote hostile comes into range.

**What the block does with the bearing id afterwards** is the quantisation above: it computes
`1 + 3 * quadrant + band` and hands it to the broadcast helper (`0x0041dcfa`, `0x004708e0`), which
elects one speaker from the local player's own flight. The taunt and the bearing are therefore two
different speakers on the same frame, the pursuer and one of the player's wingmen.

## The remake's dispatch sites

The rules above are represented in `CSVM/src/Flight/Ai/AiVoiceDispatcher.cs` (the gate, cooldowns,
halving, election, DI tiers, bearing index, engine-free, seeded) and wired by
`CSVM/src/Session/Roster/AiVoiceRuntime.cs`. Where the original's dispatch site is
decoded, the remake uses it; where only the trigger's meaning is decoded, the chosen stand-in
site is recorded here. The runtime watches the mode machine of every AI the session hands it,
whether or not that aircraft resolved a voice of its own: rows 1-12 below are broadcast, so they
are spoken by an aircraft other than the one whose mode moved, and the shipped rosters leave nearly
every enemy on `accentID` -1, so watching only the voiced aircraft leaves those rows silent for a
whole mission.
Every aircraft's death report is watched for the same reason, human rigs included: rows 22 to 24
are spoken by the killer, not by the aircraft that died.

| ids | status | site / reason |
|---|---|---|
| 0 | wired | the gunner's own acquisition (`Flight/Weapons/TurretController.cs`), carried mount and world emplacement alike: the first tick it holds a human player as its acquired target, by the entry's own `DETECTION_RANGE` and the shared target picker, with a clear sight line by its own rule. The report leaves through `ProjectilePool.TurretAcquiredPlayer`, the seam both turret families are built against, and `AiVoiceRuntime.WatchTurrets` broadcasts on the warned player's team |
| 1–12, 14 | wired | the decoded sites above: the pursuer speaks `WA-Attack` and the flight broadcasts the bearing call-out computed in the warned player's frame, raised together while the pursuer is in play, hostile to the human its gunner holds, and holding one (`AiVoiceRuntime.RaiseAttackCallOuts`, off the sim clock, not off a mode edge). A downed pursuer's gunner keeps its target, and the original's combat driver never runs for a downed aircraft, so `InPlay` gates the raise; the hostility test mirrors the multiplayer path's explicit side check. The original raises them every frame from the weapon pass and the combat driver; the remake raises at the 15 s slot-cooldown interval, since no slot can speak twice inside it. Losing the human re-arms the raise, so a fresh engagement speaks at once, and the mode machine's patrol→pursue commit raises the pair as well when it falls after the mute window. Rows 25 and 26 ride the same raise, ahead of `WA-Attack`, as they do in the original's own block |
| 13 | wired | a human rig's summary health crossing 30 % on the projectile hit path (decoded threshold), broadcast |
| 17–19 | wired | the speaker's own summary health on the projectile hit path, 70/50/30 % most-severe-first (decoded) |
| 20–21 | wired | `FlightController.Downed`, with force: id 20 (`DA`) when the dying aircraft's `Team` is `AimAssist.PlayerTeam`, id 21 (`DE`) otherwise (`AiVoiceRuntime.RegisterAi`). Free flight and `--vs` still give every AI its own default team, so `DA` stays dormant there in practice, it fires once a mission places an AI on the player's team |
| 22–24 | wired | the same `FlightController.Downed` report read for its killer (`AiVoiceRuntime.OnDowned`), the decoded order of the two predicates: friendly over shooter and victim and no gloat is chosen, else friendly over the victim and `AimAssist.PlayerTeam` picks 22, hostile picks 23, and the killer speaks it. A kill by a rig registered through `RegisterPlayer` takes the decoded player arm instead: 24 is addressed to that rig, and row 16 broadcasts after it. Outside a network match no player rig resolves a voice set, so 24 is silent there, which is the original's own behaviour for a player vehicle with a null slot; a network seat speaks it in its chosen voice ("A player's own voice"). A killer that resolved no voice of its own is silent |
| 16 | wired | the second call of the same player arm (`AiVoiceRuntime.OnDowned`): the local player's kill of a hostile broadcasts it, elected on `AimAssist.PlayerTeam` rather than on the killer's or the victim's side. It runs unconditionally after row 24's addressed line, never as its else-branch, because the decoded null-slot test jumps into the broadcast. This is the reliable half of a player kill: 17 pilot ids ship a playable `PR-EnemyDwn` set |
| 25–26 | wired | the decoded site above, on the same raise as rows 1–12 and 14 and addressed to the pursuer (`AiVoiceRuntime.RaiseAttackCallOut` through `AiVoiceDispatcher.TauntTriggerFor`): the cosine between the pursuer's nose and the line to the human it holds picks 26 above `TauntNoseCos` and 25 below `-TauntTailCos`, and neither between them. A pursuer in its own evade reaction is refused, as the decoded block's `+0xba` read refuses it; a pilot with no mode machine counts as not evading |
| 27 | wired | the speaker's evade episode ending with the flag already cleared, the pursuer shaken ("fires as the reaction flag clears", decoded, and the original raises it where it clears the flag and nowhere else). An episode the speaker leaves with the flag still up says nothing here; that geometry is the pursuer's own row 26. A target lost mid-reaction reads as a shake, the flag's own clear rule with no pursuer left to test |
| 28 | wired, one arm | the decoded first arm only: `FlightController.DamageApplied` carries the round's shooter, and a shooter registered through `RegisterPlayer` whose team the predicate calls friendly over the struck aircraft's makes that aircraft speak (`AiVoiceRuntime.OnFriendlyFire`). The second arm, the survivor count over `DAT_0071c4e4`/`e8`/`ec` with its default sound set, is left unwired: what those three globals are is undecoded (below) | 
| 15 | wired | the decoded site, which is the player's run and not an AI's: the zone module's completion routine broadcasts the trigger once more than one gate has been crossed (`FUN_00446990`, the `FUN_004b86a0(0xf)` call at `0x004469ff`), and that routine is reached for the local player's vehicle alone (`FUN_0048e580`, the owner guard at `0x0048ea1f`). The remake raises it on the same report the objective and wrap-up counters ride: `CampaignDirector.NotifyDangerZoneCompleted` raises `WorldInputs.DangerZoneSpoken`, `GameSession` hands the flown aeroplane to `AiVoiceRuntime.DangerZoneCompleted`, and the flight broadcasts on that aeroplane's team |

Stand-ins and inventions, named:

- **Speakers register on their real `FlightController.Team`**, the teamless stand-in is retired.
  Free flight and `--vs` still give every pilot its own default team (`AimAssist.TeamOfPilot`, pilot
  N = team N+1), so a broadcast only ever elects a "teamless" match there in practice; it goes live
  the moment a mission puts two AI, or an AI and the player, on the same explicit team.
- **The turret warning's cadence is ours**: the trigger's meaning is decoded, how often the
  original raises it is not, so a gunner reports once per acquisition episode and re-arms only
  when it loses that target. Its sight-line cast is taken uncached, beside the fire path's cached
  verdict rather than through it: that verdict is stamped out of the gunner's own RNG stream, and
  refreshing it on the ticks the fire gates skip would move every round the gun fires.
- **`Bail`/`NoBail` is a constitution roll** (`constitution_chance`, 0.35→0.95), the open
  item's natural-candidate reading, implemented and marked unconfirmed.
- **The raise interval** is the remake's only departure on the attack pair: the original re-raises
  every frame and lets the gate refuse, and `AiVoiceRuntime` raises once per
  `AiVoiceDispatcher.SlotCooldownS` per pursuer instead, because the broadcast election walks and
  resolves every registered speaker on each raise. A raise the mute window refuses consumes no
  interval, so a pursuer that commits on the mission's first frame speaks as the window lifts.
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
- **Row 27's dispatch point is the evade episode's end** (`AiVoiceRuntime.OnModeChanged`) rather
  than the frame the flag drops, which is where the original raises it. The remake reads the flag
  when the aircraft leaves the pair of evade modes and speaks only if it has cleared, so the line
  can arrive a few frames late; a move between the two evade modes is the same episode and speaks
  nothing.
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
