# Combat voice — the 29 triggers and the accent chain

Part of the [format documentation](README.md). The AI's radio chatter: which events make a pilot
speak, which pilot's voice they speak in, and the rules that decide whether the line plays at all.
The clip inventory and its naming are surveyed in `docs/PLAN-M4-ai.md` § 6; this page covers the
dispatch, read from `crimson.exe`. Claims name the evidence at the point of use; no code is
reproduced.

## The trigger table

The engine carries **29 triggers, ids 0–28**, as a contiguous ordered table of `TYPE` tokens in
`.rdata` (`0x0062af5c`–`0x0062b0d4`). The count is not inferred from the strings: the loader that
populates a pilot's voice slots runs `i` from `0` to `0x1d` exclusive.

| id | Token | Fires when |
|---|---|---|
| 0 | `WA-Turret-A` | a turret acquires the player and has line of sight — broadcast |
| 1–12 | `WA-Enemy-{12,3,6,9}{L,·,H}` | bearing call-out; see the index formula below — broadcast |
| 13 | `WA-HighDmg-A` | the player's health crosses a 30 % threshold — broadcast |
| 14 | `WA-Attack-A` | a pilot commits to an attack on the player |
| 15 | `PR-DngrZn-A` | a Danger Zone run — broadcast |
| 16 | `PR-EnemyDwn-A` | *(no dispatch site located)* |
| 17 | `DI-LowDmg-A` | the speaker's health drops below **70 %** |
| 18 | `DI-MedDmg-A` | …below **50 %** |
| 19 | `DI-HighDmg-A` | …below **30 %** |
| 20 | `DA-Bail-A` | the speaker is destroyed and is **on the player's team** |
| 21 | `DE-Bail-A` | the speaker is destroyed and is **not** |
| 22 | `GL-AllyDwn-A` | gloat — a plane on the player's team went down |
| 23 | `GL-EnemyDwn-A` | gloat — an enemy went down |
| 24 | `GL-PlyrDwn-A` | gloat — the player's kill/loss case; also broadcasts |
| 25 | `TA-FailTail-A` | taunt — a pursuer failed its tail check |
| 26 | `TA-FailShk-A` | taunt — a shake attempt failed |
| 27 | `TA-SucShk-A` | taunt — the speaker shook its pursuer (fires as the reaction flag clears) |
| 28 | `DS-Ally-A` | ally distress — fires when only one of the player's wingmen is left alive |

⚠ **The token in the table is a family root, not a clip name.** The `-A`/`-B`/`-C` variants and the
`Bail`/`NoBail` split are resolved below this layer — trigger 20 selects `DA-*`, not
`DA-Bail-A` specifically. A reader that treats the table entry as a filename will find most of the
shipped clips unreachable.

Two entries in the table settle questions the clip survey could only guess at:

- **`DA` is the ally counterpart of `DE`, and the split is by team, not by outcome.** Both are the
  dying pilot's own death cry; id 20 fires if the dying aircraft is on the player's team and id 21
  if it is not. `docs/PLAN-M4-ai.md`'s open question 6 recorded this as inference — it is now
  read from the dispatch, along with its polarity.
- **`TA-FailTail` is a real engine trigger with a real dispatch site**, not an orphan clip family.
  The survey noted the design's taunt table omits it; the engine does not.

### The bearing call-outs are computed, not enumerated

Triggers 1–12 are `WA-Enemy` bearing warnings, ordered **low, level, high** within each clock
bearing, and the engine indexes them arithmetically:

```
id = 1 + 3 * bearingIndex + altitudeBand
     bearingIndex ∈ {0,1,2,3}  →  12, 3, 6, 9 o'clock, wrapping at 4
     altitudeBand ∈ {0,1,2}    →  L (below), level, H (above)
```

This is the family only **7 of 31 pilot ids** carry any clips for (§ 6). A pilot without them simply
has a null slot for those 12 ids and stays silent on bearings — which is the design's claim that
positional detail is a per-pilot capability, expressed as missing data rather than as a check.

## Where a pilot's voice comes from

Each aircraft holds **29 voice slots**, one per trigger, at `vehicle + 0x730 + 12 * triggerId`.
Each slot is three words: the sound set, the handle of the line currently playing, and the time the
slot is next allowed to speak. A slot whose sound set is null means *this pilot has no line for
this trigger* — every dispatch site tests for that before rolling anything.

The slots are filled at spawn from the pilot's voice set, which resolves through the chain the
scoping study described:

```
aiv accentID (slot 65)  →  a row in zrdr/voice.zrd  →  a pool of pilot VO ids
                        →  one pilot's clip set     →  29 trigger slots
```

⚠ **`voice.zrd` is the accent table, not the trigger table.** Its 35 rows are keyed by `accentID`
and their values are **pilot VO ids** (`soundsh/VO_id<N>_*`) — the numbers run over the same sparse
id set the clip survey found. The row count coincidentally sits near the trigger count; they are
unrelated tables and conflating them will mis-key every lookup.

In multiplayer the accent lookup is skipped and the voice set is handed in directly, so a remake's
single-player path is the one that needs `accentID`.

## Whether a line actually plays

Every trigger goes through one gate function. In order:

1. **Voice must not be globally muted**, and the mission clock must be **past 2.0 s** — nothing
   speaks in the first two seconds.
2. **The speaker must be alive**, unless the caller passes a *force* flag. Only the death cries
   (ids 20/21) do, because the speaker has just been marked dead.
3. **The speaker must not already be talking**, and must not be in the suppressed state.
4. **The talker roll.** `rand01 < talkerChance`, where `talkerChance` is the per-pilot value the
   `talker` skill slot indexes out of `ai_skill_parameters` (0.25 at rating 1 → 0.95 at rating 9 —
   [ai-rosters.md](ai-rosters.md)).

⚠ **Triggers 1–12 have their talker chance halved**, hardcoded. The bearing call-outs are
deliberately half as likely as everything else, on top of only 7 pilots owning the clips at all.

The binary carries the debug strings for both outcomes — *"Talker test passed. Play AI sound #%d."*
and *"Talker test failed. Don't play AI sound #%d."* — which is how the numeric trigger id was
first spotted.

### The cooldown is armed by failure too

⚠ **Each slot has a 15-second cooldown, and losing the talker roll starts it exactly as winning
does.** The failure path is the success path minus the play call: both stamp `now + 15` into the
slot. So a low-`talker` pilot is not merely quieter — a failed roll silences that trigger for the
next 15 seconds rather than letting it retry on the next event.

This is the single behaviour most likely to be got wrong by re-deriving from the design prose,
which describes `talker` only as a volume-of-chatter stat.

### Broadcasts elect one speaker

Several triggers are not addressed to a pilot at all — they are broadcast to the flight. The
broadcast helper:

1. collects every AI pilot that is **alive**, **not the player**, on the caller's team or teamless,
   and **owns a non-null slot for that trigger**;
2. picks one **at random**;
3. runs the gate above on it, and **if the talker roll fails, moves to the next pilot in the list**,
   wrapping, until one succeeds or every candidate has been tried.

So the flight speaks with one voice per event, and a quiet pilot passes the line along rather than
swallowing it. Triggers 0, 13, 15, 24 and the bearing call-outs dispatch this way; the distress,
death and taunt triggers address a specific aircraft.

## What is not pinned down

- **The exact attacker/victim polarity of triggers 22–24.** The three gloat triggers are selected
  by the team relationship between the attacker, the victim and the player, and each is gated on
  the speaker owning that slot — but which party is the speaker in each branch is not settled here.
  The token names indicate the intent; the dispatch was not traced far enough to assert it.
- **Trigger 16 (`PR-EnemyDwn`) has no located dispatch site.** It may be reached through a path not
  covered, or be unused.
- **How `-A`/`-B`/`-C` and `Bail`/`NoBail` are chosen** below the family root.

## What this is not

- The **clip inventory** — 2,520 files, 31 pilot ids, the 11 `TYPE` families and their counts — is
  `docs/PLAN-M4-ai.md` § 6. This page covers only what the engine does with them.
- **Mission-scripted dialogue** (`VO_<chapter>-<faction>-<mission>_<Character>_<n>.wav`, 990 clips)
  is a different system, driven by objectives scripting, and does not go through the trigger table.
- The **sound format and the SETS table** are [sounds.md](sounds.md).
