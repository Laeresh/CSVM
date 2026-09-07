# Audio preferences — the AUDIO page and the mix behind it

**ACTIVE PLAN** (written 2026-09-07). It sits in `docs/`, which by this repo's convention makes it
a live plan; PROJECT_CONTEXT.md's "Current status" names it. When every item lands, the closing
commit deletes this file, records the completion in its message, and clears the "Current status"
pointer; any live prose linking this file by path is unlinked in the same commit.

This plan delivers `BL-455`: Preferences' AUDIO door opens a real page composed over its own
authored artwork, carrying four volume sliders (Master, Music, Effects, Voice), and the port grows
the audio bus layout those sliders need. Every sound the engine makes is placed in one of the three
categories, Master multiplies all three, and `MusicPlayer.ChannelLevel` (the hard-coded 0.2 stand-in
the backlog entry exists to remove) is deleted rather than re-tuned.

`BL-455` was re-verified still-open in this session against both the record and the code. The entry's
own mechanism is untouched: `MusicPlayer.ChannelLevel` is still `0.2f` with its "remove it, do not
re-tune it" comment (`CSVM/src/Mech3/MusicPlayer.cs:59-64`), `PF_B_AUDIO` is still the second entry
of the disabled-doors array (`CSVM/src/UI/Menu/Original/OriginalShell.cs:192-195`), and
`git log -S'AddBus'` returns nothing, so no bus work has ever landed. ⚠ A `git log --grep=BL-455`
returns `13cf0f18 A3 BL-455: delete AiControlLaw.Throttle's dead far-from-player branch`, which is a
different item under a reused number from the same day; the live entry was minted in `3bbc4a7d` and
has no closing commit. No other backlog item is drawn into this plan.

**Another session is building `docs/PLAN-video-preferences.md` at the same time.** That plan's `B2`
widens the same three types this plan's `B4` widens (`OptionsDef`, `OptionsStore`,
`OptionsApplyExit`) and its `A1` adds a page partial beside the one this plan's `B6` adds. The
contention is real and named in "Dependency and parallelism notes"; neither plan's carrier item may
run while the other's is open.

## Milestone goal

- Preferences' AUDIO door opens a page instead of drawing greyed, the third of the three unbuilt
  doors to be answered after `BL-696`'s CONTROLS and `BL-768`'s VIDEO.
- Every sound the engine plays sits on a named bus: Music, Effects or Voice. None is left on Master
  by accident, and a suite fails if one is.
- A player sets four levels at the controls (Master, Music, Effects, Voice), with Master a multiplier
  over the other three, and no `config.json` and no command-line flag.
- Each of those levels survives a restart, saved by the options file's one writer.
- `MusicPlayer.ChannelLevel` is gone, and the music level a player hears is the one they set.

**Sound Quality does not come back.** The authored row tiered a 2000-era mixer's sample rate and
voice count, which is why the original had to quit and restart to change it
(`IDS_AP_QUALITYCHANGED`). Godot's mixer has no equivalent tier, and a row that changes nothing is
worse than an absent one.

**The developer volume path is not replaced.** `--volume=` and `audio.volume` stay exactly what they
are, a gain on the Master bus that keeps repo runs silent, and the four sliders are a separate mix
below them. This plan adds a player-facing control; it does not touch how a scripted run sounds.

## Decisions (2026-09-07)

| # | Question | Decision |
|---|---|---|
| 1 | Which of the five authored rows can this port honour? | **Four rows, not five** — Master, Music Volume, Effects Volume, Voice Volume. The three authored volume sliders are honoured as authored; Master is the added fourth and Sound Quality is left out. |
| 2 | Where does the added Master row go? | **It takes the In-Game Music checkbox's row.** The page then reads Master, Music, Effects, Voice from the top, in the order a multiplier and its operands belong in, and every row keeps its authored line. The row's title and description are rewritten, the same licence the Enhanced Graphics row already takes on an authored page. |
| 3 | Does In-Game Music survive as its own control? | **No.** Its whole function was a mute the authored slider could not reach, because the authored `MinValue` is 1. Decision 4 gives every slider a real zero, which is the same control in one fewer widget. |
| 4 | What range do the sliders take? | **0 to 100, where 0 is silence.** The authored fields say `MinValue 1, MaxValue 100`, so the original's far-left is about -40 dB and audible in a quiet room. One step below the authored floor buys a player the ability to turn a category off, which is the obvious thing to want from a Voice or Music slider. |
| 5 | What does a fresh install open on? | **Master 100, Music 50, Effects 50, Voice 50** — the authored `CurrentValue` on the three category rows, and full on the added one. ⚠ This re-bases the whole mix 6 dB down for a player who never opens the page; `B7` is where that gets judged rather than assumed. |
| 6 | Where do the sliders apply, given `--volume=` already writes the Master bus? | **On the three child buses, never on Master.** Each child bus takes `category × master`, and the Master bus stays the developer gain alone. `--volume=0` therefore still silences a repo run whatever the saved sliders say, and a full-volume launch still leaves Master at its resting gain and stays byte-identical in output and console log. |
| 7 | Combat voice plays through `WorldSounds.PlayOneShot`, the effects one-shot path. Which bus? | **Voice, through a bus argument on the call.** The category boundary cuts across one call site rather than one module, so the seam goes on the call. A separate voice-only pool was rejected: it would duplicate the pooling, the prewarm and the 3D falloff for one caller. |

## ⚠ Read this before implementing anything

| Confidence | Items | What that means for you |
|---|---|---|
| **Traced to an exact mechanism in code, with the data that proves it** | A1, A2, A3, B4, B6 | Confirm the trace, then implement. |
| **Direction sound, magnitude a judgement call** | B7 | The re-base is a fact; whether the resulting mix is right is the author's ear, and the numbers that come out of it are TUNE. |
| **Leads only — no mechanism yet** | B5 | The shell has no continuous control of any kind. The thumb-drag machinery it does have is a lead, not a fitting. |

**⚠ A player left on Master is silent about it.** Godot places an `AudioStreamPlayer` on Master by
default and logs nothing, so a construction site this plan misses keeps working, keeps sounding, and
simply escapes the player's mix forever. "The sliders work" is therefore not evidence that every
sound obeys them. `A1` ends with a guard that enumerates the live players and fails on any whose bus
is Master, and that guard is the item, not a nicety attached to it.

**⚠ Do not reach for `AudioServer.SetBusVolumeDb(0, …)` for the Master slider.** Bus 0 already
carries `--volume=`/`audio.volume`, and `ApplyMasterVolume` deliberately leaves it untouched at full
gain so a normal launch is byte-identical in output and console log to one with no volume path at
all (`CSVM/src/Session/Launcher.cs:1613-1618`). A Master slider written there would fight that
contract and make a saved slider able to un-silence a scripted run. Decision 6 is what avoids it.

**⚠ Worktree hazard.** `git stash` is repo-global and shared across worktrees, so never use it in a
worktree session here; use a local commit or a file copy.

## What the data actually ships

**The authored `Audio` section** of `extracted/rof/menu_layout.json` carries 19 widgets: the
background `AP_BACKGROUND`, the plaques `AP_B_ACCEPTCHANGES` and `AP_B_CANCELCHANGES`, the title
`AP_T_TITLE`, the checkbox `AP_B_MUSIC`, the dropdown `AP_D_SQuality`, the three sliders, and a
title plus a description text per row. `OriginalScreenshots/Preferences Audio.png` is the page live,
if the user's tree holds one under that name.

The five authored rows, their title line and their control:

| Row | Title widget (Y) | Control (Y) | String |
|---|---|---|---|
| 1 | `AP_T_MusicTitle` (266) | `AP_B_MUSIC` checkbox at X=259 (273) | "In-Game Music" / "Select to hear in-game music." |
| 2 | `AP_T_MVolTitle` (324) | `AP_S_MVOLUME` slider at X=137 (350) | "Music Volume" / "Set the volume of the in-game music." |
| 3 | `AP_T_EVolTitle` (381) | `AP_S_EVOLUME` slider at X=137 (407) | "Effects Volume" / "Set the volume of the sound effects." |
| 4 | `AP_T_VVolTitle` (434) | `AP_S_VVOLUME` slider at X=137 (461) | "Voice Volume" / "Set the volume of the voices." |
| 5 | `AP_T_QualityTitle` (487) | `AP_D_SQuality` dropdown at X=137 (505) | "Sound Quality" / "Select the sound quality. Low may improve performance." |

The title column is X=137 and the description column X=348 at width 310, both also given as the
section's own `TITLEX` and `DESCX` macros. The row pitch is not uniform (58, 57, 53, 53), so the
page's row shape is read per row off its own title widget rather than off a first row and a pitch,
which is where it differs from Game Options.

**The three sliders are identical** in everything but their line:
`MinValue 1, MaxValue 100, CurrentValue 50, RegionArt PF_B_SliderSlot.png, SliderArt PF_B_Slider.png`,
with the hit region widened by `Left 0, Top -10, Right 1, Bottom -10`. Two art files, one for the
slot and one for the thumb, and no frame count, so the thumb is a single image and not a state strip.

**Every site that constructs a player**, which is the whole surface `A1` has to place. Fourteen
sites, in three categories, plus combat voice, which has no player of its own; the count is the
number of `new AudioStreamPlayer`/`AudioStreamPlayer3D` expressions under `CSVM/src`, and every one
of them is named by line number below. `CSVM/scenes/Main.tscn` authors no audio node, so there is no
site outside the code:

| Category | Sites |
|---|---|
| Music | `MusicPlayer._player` (`CSVM/src/Mech3/MusicPlayer.cs:89`) |
| Voice | `MissionRadio._player` (`CSVM/src/Mech3/MissionRadio.cs:35`), `MenuAudioService._narration` (`CSVM/src/Session/MenuAudioService.cs:46`), and combat voice, which has no player of its own and rides `WorldSounds.PlayOneShot` (`A2`) |
| Effects | `FlightAudio`'s crash, warning shot, gun loop, nitro loop and its two factories (`CSVM/src/Flight/FlightAudio.cs:114,132,157,180,418,437`), `AiEngineAudio`'s engine slots (`CSVM/src/Flight/AiEngineAudio.cs:234`), `WorldSounds`' ambient emitters and one-shots (`CSVM/src/Mech3/WorldSounds.cs:184,390`), `Projectile`'s eight-player pool (`CSVM/src/Flight/Projectile.cs:881`), `MenuAudioService._cuePlayer` (`CSVM/src/Session/MenuAudioService.cs:48`) |

**The bus layout** is `CSVM/default_bus_layout.tres`, which `A1` added: Master, plus Music, Effects
and Voice sending into it. Godot 4.7's `audio/buses/default_bus_layout` still defaults to
`res://default_bus_layout.tres`, confirmed by the `audio-buses` suite reading four named buses out of
`AudioServer` with no `audio/` key in `project.godot`, so no key was added. Before `A1` the project
shipped no layout and Master was the only bus.

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
- **Read `docs/verification.md` before measuring anything** — the instruments here mislead; cite the
  rule that bites per item.
- **Verify against a full 8-chapter `--freecam --chapter=<X>` regression** (zero errors, same
  mesh/node counts unless the change is meant to add coverage) plus a targeted capture at the
  location the report came from.
- **Read the module's entry in `docs/architecture/<Namespace>.md` (found through the index in
  `docs/architecture.md`) before modifying it,** then the comments on the members you touch; dead
  ends are in the landing commits (`git log --grep=<ID>`), so search those before re-chasing one.

## Checklist

Statuses: ☐ open · ◐ in progress · ☑ done · ❌ closed/disproven. **Keep this in sync as items land.**

### Wave A — the mix

1. ☑ The bus layout, and every player on a named bus
2. ☑ Combat voice leaves the effects path
3. ☐ The mix seam, and the music placeholder's deletion

### Wave B — the page

4. ☐ The options carrier takes a level that is not a vocabulary word
5. ☐ The slider the shell has never had
6. ☐ The AUDIO page opens from Preferences
7. ☐ The precedence ladder, and the re-based mix judged at the controls

## Dependency and parallelism notes

A1 blocks everything: there is nothing to route to and nothing for a slider to move until the buses
exist. A2 needs A1's bus names and edits `WorldSounds.cs`, which A1 also edits, so run them as a
chain and not in parallel. A3 needs both, because the level it applies has to reach every sound.

B4 blocks B6 and B7. B5 and B4 are independent (B5 is the shell's control kind, B4 is the store's
field) and are the one pair in this plan that can run in parallel; give B5 `OriginalShell.cs` and
give B4 `OptionsStore.cs`/`MenuExit.cs`, and neither touches the other's file. B6 needs both. B7
verifies the whole ladder and runs last.

**Wave B waits on the video plan.** The video plan's `B2` and five of its siblings are landed on
branch `video-preferences` and not yet on `main`, and that branch's `OriginalShell.cs` already
occupies the regions `B5` and `B6` need: `PreferencesPageKeys`, the shell's saved-setting fields and
its choice properties. Wave A is clean against `main` and runs first; Wave B runs after
`video-preferences` merges to `main` and that merge is pulled into this branch, so `B4` widens the
record the video plan actually shipped rather than a second copy of it.

**Cross-plan contention with `docs/PLAN-video-preferences.md`.** B4 and that plan's B2 both widen
`OptionsDef`, `OptionsStore`'s validation and the `OptionsApplyExit` record, which is deliberately a
closed hierarchy; two independent widenings of it will conflict textually and, worse, will each
review only their own half. Land one and rebase the other. B6 and that plan's A1 both add a page
partial beside `OriginalGameOptions.cs` and both edit `PreferencesPageKeys`; the conflict there is a
one-line array and is cheap, but B6 should read the video page's partial before mirroring Game
Options, in case that plan already generalised the row shape.

---

# Wave A — the mix

## A1 ☑ The bus layout, and every player on a named bus

**Goal.** The process runs four buses (Master, Music, Effects, Voice, the last three routed to
Master), every one of the fourteen player-construction sites names the bus its sound belongs to, and
a suite fails if any live player is on Master. Nothing is audibly different yet: the three child
buses sit at their resting gain.

**Evidence (confidence: traced).** The survey above is the whole surface, read off the tree in this
session: fourteen `new AudioStreamPlayer`/`AudioStreamPlayer3D` sites, no bus layout anywhere, and
`Launcher`'s constant naming Master as the only bus (`CSVM/src/Session/Launcher.cs:49-51`).
`MenuAudioService`'s own comment states the current arrangement as a deliberate one ("On the Master
bus by default, which is where `--volume=`/`audio.volume` already applies",
`CSVM/src/Session/MenuAudioService.cs:45`), so that comment is part of the edit, not collateral.

**Approach.** Ship `CSVM/default_bus_layout.tres` with the four buses rather than calling
`AudioServer.AddBus` at startup: a bus that exists before the first node enters the tree cannot be
missed by a player built early, and Godot resolves an unknown bus name to Master with no error.
Confirm that Godot 4.7's `audio/buses/default_bus_layout` project setting still defaults to
`res://default_bus_layout.tres`; add the explicit key to `project.godot` if it does not. Put the bus
names in one place (a small `AudioBuses` holder in `src/Utils/`) so no site spells a string, and set
`Bus` at construction on all eleven. Do not change any `VolumeDb` in this item: every per-sound gain
stays exactly what it is, so the item is provably inaudible.

**Model recommendation.** medium. The edit is mechanical and wide; the judgement is only in the
guard's shape.

**Verify.** The guard first, and prove it can fail: walk the live scene tree in an engine suite,
collect every `AudioStreamPlayer`/`AudioStreamPlayer3D`, and fail on any whose `Bus` is `Master`;
then deliberately revert one site and confirm the suite reports it before restoring it. An unchanged
sound is the other half: a `--volume=1.0` flight over one chapter should be indistinguishable, and
the existing audio suites (`MusicSuites`, and the `sound` log's per-slot resolution lines that
`AiEngineAudio` prints) should be unchanged. `.\RunTests.ps1` green.

**⚠ Traps.** `SetFocusMuted` mutes bus 0 and must keep doing so: muting Master mutes its children, so
alt-tab silence still works and needs no per-bus change. `--mute` is a load-time switch that skips
flight audio entirely and is not a bus concern; do not reimplement it as a bus mute.
`Projectile`'s pool is built once and reused, so its bus is set at construction and never per shot.
A suite that walks the tree sees only what a given session built, so run the guard in a session that
has a world, a flown plane and the menu service up, or it passes by seeing nothing.

**Verified.** <pending orchestrator run>

## A2 ☑ Combat voice leaves the effects path

**Goal.** A pilot's combat callout plays on the Voice bus while the destruction and impact one-shots
that share its code path stay on Effects.

**Evidence (confidence: traced).** Combat voice has no player of its own. `CombatVoice.PlayableFor`
returns "the one name to hand `WorldSounds.PlayOneShot`" (`docs/architecture/Mech3.md`'s
`src/Mech3/CombatVoice.cs` entry), and `AiVoiceRuntime` states that "clips play through
`WorldSounds.PlayOneShot` alone" (`docs/architecture/Session.md`'s entry). That call is also the
fire-and-forget destruction and impact path (`CSVM/src/Mech3/WorldSounds.cs:390`), so one call site
serves two categories.

**Approach.** Add the bus as a parameter of `PlayOneShot` (and its `Node3D` overload), defaulted to
Effects so every existing caller keeps its category without an edit, and pass Voice from
`AiVoiceRuntime`'s call. Do not move the dispatch or the resolver: `AiVoiceDispatcher` is engine-free
by contract and must not learn what a bus is.

**Model recommendation.** medium. One parameter and one caller, but on a call with two overloads and
a pooling path behind it.

**Verify.** Drive the `ai-voice` suite and assert the clip landed on the Voice bus, alongside the
existing assertion that it played at all; `WorldSounds.OneShotsStarted` is the counter that already
exists for "a cue fired". Then at the controls, with Voice at 0 and Effects at 100 after `A3`, a
dogfight is silent of callouts and loud with gunfire. `.\RunTests.ps1` green.

**⚠ Traps.** The default must be Effects, not "whatever the caller last passed", so the bus is set
on every play and never once at a fill point. `WorldSounds.Spawn` builds a fresh
`AudioStreamPlayer3D` per one-shot and frees it when the clip ends, so no player crosses two calls
and the leak is unreachable today; the pooling in this class is the `SOUND_NODE` emitter half, one
player per live host. The rule is kept on the construction anyway, and the `ai-voice` suite replays
one clip with the default argument after the Voice call, so a later pooling of these players cannot
reintroduce it silently.

**Verified.** <pending orchestrator run>

## A3 ☐ The mix seam, and the music placeholder's deletion

**Goal.** One module turns four 0..100 levels into three bus gains and applies them, live and at
startup. `MusicPlayer.ChannelLevel` is deleted, and the music a player hears is the level the mix
seam applied.

**Evidence (confidence: traced).** `ChannelLevel = 0.2f` multiplies into the channel's gain at
`CSVM/src/Mech3/MusicPlayer.cs:383` (`gain * ChannelLevel * _duck`), and its own comment says to
remove rather than re-tune it once an options menu can carry a music slider (`:59-64`). No suite
reads it: a search of `CSVM/src/Testing` and `CSVM.Tests` for `ChannelLevel` returns nothing, while
`DuckLevel` is asserted twice in `MusicSuites` (`CSVM/src/Testing/MusicSuites.cs:162,168`).
`DuckLevel`'s own comment already anticipates this item ("This one survives an options menu: a slider
sets the level a duck is a share of", `:66-71`).

**Approach.** A small `AudioMix` in `src/Utils/`, engine-free in its arithmetic and unit-testable:
four integer levels in, three linear gains out (`category/100 × master/100`), with the same
`LinearToDb` floor `Launcher` already uses so a level of 0 is `-80 dB` and not negative infinity. A
thin apply beside it writes the three child buses. Delete `ChannelLevel` and its multiplication in
the same item, leaving `gain * _duck`; keep `MusicPlayer.Gain` as the fade's own 0..1 value, which
the decoded ramp assertions read. Update `DuckLevel`'s comment to say what it is now a share of.

**Model recommendation.** high. Deleting the placeholder changes the shipped mix by construction, and
this is the item where the narration regression `BL-455` was filed for can come back.

**Verify.** Unit-test the arithmetic (0 is silent, 100/100 is the resting gain, master halves every
category, the floor holds). Then the regression the placeholder existed to prevent, at the controls:
open a campaign briefing at the shipped defaults and confirm the narration is still followable over
the music. ⚠ The default music level is 0.5 against today's 0.2, and the narration now sits on a
Voice bus at 0.5 rather than at full gain, so the gap that made the words hard to follow narrows by
about 14 dB and this is the most likely place in the plan for something to sound wrong.
`.\RunTests.ps1` green.

**⚠ Traps.** Do not re-tune `ChannelLevel` on the way past, and do not preserve it as a hidden factor
under the slider; the backlog entry exists because it is a placeholder, and leaving it in would make
a player's 100 mean 0.2. If the narration does turn out to be buried, the fix is `DuckLevel` (which
is TUNE and survives this plan by design) or the shipped defaults, and either is a decision for `B7`
with the author at the controls, never a new constant multiplied into the music channel.

# Wave B — the page

## B4 ☐ The options carrier takes a level that is not a vocabulary word

**Goal.** `OptionsDef`, `OptionsStore` and `OptionsApplyExit` carry four volume levels with the same
guarantees the three existing options have: one writer, validation that drops an out-of-range value
rather than failing the file, and a missing field that reads as never set.

**Evidence (confidence: traced).** `OptionsDef` carries three nullable strings and nothing else
(`CSVM/src/Utils/OptionsStore.cs:12-21`). Every field is validated against a fixed `HashSet<string>`
and an unknown value is dropped like a missing one, with only the version gate rejecting a whole file
(`:120-147`, `:196-201`). `OptionsApplyExit` is documented as one of exactly four members of a closed
hierarchy and carries three values, with the note that all three ride the exit rather than being
saved by the screen so the options file keeps one writer (`CSVM/src/UI/Menu/MenuExit.cs:36-40`).
`Launcher.ApplyOptions` is that writer. The store's `Version` comment states that adding a field does
not bump the schema version, because a missing field already reads as never set (`:33-39`).

**Approach.** Four nullable integers, validated by range (0..100 inclusive) rather than by set
membership, so `Read`'s drop-on-unknown contract still holds for a value that is out of range, the
wrong JSON kind, or not a number at all. Add a numeric sibling to the existing string `Read` helper
rather than widening the string one. Widen `OptionsApplyExit` with the four levels in one edit, since
the record is deliberately closed and a second widening is a second review. Do not bump
`OptionsStore.Version`. Do not add a save call anywhere else: the single-writer rule is the reason
the exit carries values at all.

**Model recommendation.** high. The record is closed on purpose and the store's drop-on-unknown
contract is what keeps a bad file from bricking the options; widening both is the highest
blast-radius edit in the plan, and the video plan is widening the same two types.

**Verify.** Extend `CSVM.Tests/OptionsStoreTests.cs`, which already holds this class's round-trip and
malformed-file properties (`:12,78-168`). The properties to assert are the ones the class already
promises: a serialized def deserializes equal, a file missing the new fields loads with the old ones
intact, a level of `-1`, `101`, `"loud"` or `null` reads as never set rather than invalidating the
file, and a file at a wrong version is rejected whole. `.\RunTests.ps1` green.

**⚠ Traps.** A level of 0 is a legitimate saved value and must not be conflated with "never set";
`int?` makes that distinction and a plain `int` destroys it, which would make a player's mute
un-saveable. `OptionsLaunchSuites` (`CSVM/src/Testing/OptionsLaunchSuites.cs`) drives the saved
difficulty through a real launch and is the pattern for the launch half of `B7`; do not duplicate it
here.

## B5 ☐ The slider the shell has never had

**Goal.** `OriginalShell` has a slider control: it draws from the authored slot and thumb art at a
row's authored line, a pointer drag moves it, a sideways keyboard or pad step moves it, and its value
reads back as a 0..100 integer.

**Evidence (confidence: lead-only for the fitting, traced for the absence).** `OriginalRowKind` has
six members (Button, TextButton, ListRow, Dropdown, Radio, TextField) and none is continuous
(`CSVM/src/UI/Menu/Original/OriginalShell.cs:99-120`). The shell does already drag: `_drag` holds
"which list, where the pointer took hold and where the window stood" and `DragThumb` runs a held
pointer against a list's scrollbar thumb (`:250-251,375-376,790-844`), which is the nearest existing
mechanism but is a different one (it moves a window over rows, not a value over a range). The
authored art is `PF_B_SliderSlot.png` and `PF_B_Slider.png` with no frame count, so the thumb is one
image and has no focus or pressed state to draw.

**Approach.** A seventh `OriginalRowKind`, and the drag as a second use of the existing hold-and-move
shape rather than a second drag system: the row's hit box is the widened authored region
(`Left 0, Top -10, Right 1, Bottom -10`), the value is the pointer's X mapped across the slot, and a
sideways step moves it by a fixed increment, mirroring `StepGameOptionValue`'s wrap-free
left/right handling on a dropdown. The keyboard and pad step is 5, twenty presses end to end: fine
enough to land on a considered level, coarse enough to cross the range without holding the key. The
original is a mouse-only page and settles nothing here, so this is the author's call. The thumb has
one frame, so focus is drawn the way the page draws focus elsewhere rather
than by a strip frame.

**Model recommendation.** high. This is the shell's first continuous control and every later
presentation inherits its shape; the drag interacting with the existing thumb drag is the risk.

**Verify.** `<TODO: name the existing shell suite that drives pointer input, so the drag is asserted
rather than only clicked. OriginalShellTests covers the engine-free half; the pointer path may only
be reachable from an in-engine suite.>` The assertions to make: a press inside the slot sets the
value to the pressed point, a held move tracks it, a release ends the drag, a drag that began on a
slider is not also an activation of the row under the release point (the shell already makes exactly
that distinction for a list thumb, `:486-489`), and a sideways step clamps at both ends instead of
wrapping. A `--screenshot` of the page at four different values. `.\RunTests.ps1` green.

**⚠ Traps.** A slider must clamp, not wrap. Every other stepped control on this shell wraps
(`StepGameOptionValue`), and inheriting that would step a player from silence to full volume with one
press. The existing `_drag` field is keyed by list; a slider drag needs its own state or a widened
one, and sharing it carelessly would let a drag begun on a scrollbar finish on a slider.

## B6 ☐ The AUDIO page opens from Preferences

**Goal.** Pressing AUDIO on Preferences opens a page composed over the `Audio` section's own artwork,
showing four slider rows. ACCEPT CHANGES saves and returns to Preferences; CANCEL CHANGES returns
with the edits dropped.

**Evidence (confidence: traced).** `PF_B_AUDIO` is the second entry of `PreferencesPageKeys` and
draws disabled because no shared option stands behind it
(`CSVM/src/UI/Menu/Original/OriginalShell.cs:192-195`). The section's 19 widgets, its five rows,
its non-uniform pitch and its two columns are tabulated in "What the data actually ships" above, read
off `extracted/rof/menu_layout.json` in this session. `OriginalGameOptions.cs` is the page pattern:
a section constant, a door key, an accept and a cancel key, a row table of records (title,
description selector, control kind, words, read, write), a `BuildGameOptionsRows` with a no-layout
fallback to text buttons, and an `OpenGameOptions` that clears focus so the page opens on its first
row (`:18-28,70-99,129-159`).

**Approach.** Mirror `OriginalGameOptions.cs` rather than inventing a second page shape, but read the
row shape per row: this section's pitch is not uniform, so an `AudioPage` record holds each row's own
title line read off its own widget, with the authored numbers as the per-row fallback. The row table
takes `B5`'s slider kind and a level range instead of a word list. Enable `PF_B_AUDIO` in
`PreferencesPageKeys`. Rewrite row 1's title and description for Master (decision 2); rows 2 to 4
keep `IDS_AP_MVOLUME_*`, `IDS_AP_EVOLUME_*` and `IDS_AP_VVOLUME_*` as authored. ACCEPT CHANGES builds
`B4`'s widened `OptionsApplyExit`; CANCEL CHANGES re-reads the saved options and returns.

**Model recommendation.** medium. The page pattern exists in full; the judgement is composition
against the authored geometry, not design.

**Verify.** Mint an `AudioAid` beside `GameOptionsAid` in `OriginalPresentation.cs` (`:36-45` holds
the aid constants), add its `--menu=` name to `docs/cli.md`'s `--menu=` bullet, and take a
`--screenshot` to compare against the original's page for row placement and the description column.
The reference shot is `OriginalScreenshots/Preferences Audio.png`, beside the video plan's
`Preferences Video.png`. Drive the door and both plaques through the Original menu suites the way
`MenuOriginalSuites` already drives Game Options, asserting one `OptionsApplyExit` on Accept and none
on Cancel. `.\RunTests.ps1` green.

**⚠ Traps.** The page has its own `AP_B_ACCEPTCHANGES`/`AP_B_CANCELCHANGES` pair; do not route it
through `GameOptionsAcceptKey`, or Cancel on one page will read as Accept on the other. The row pitch
is not uniform, so a `FirstY` plus a pitch (which is what Game Options reads) misplaces rows 3 to 5
by up to 5 pixels each; read each title's own Y. Row 1's control sits at the title's own line while
rows 2 to 4 sit about 26 pixels under theirs, because row 1 was authored for a checkbox at X=259 and
is now a slider at X=137; place it at the slider offset, not the checkbox's.

## B7 ☐ The precedence ladder, and the re-based mix judged at the controls

**Goal.** The four levels sit in the same precedence ladder the existing options do, `--volume=` and
`audio.volume` still beat them into silence in a repo run, `--det` reads none of them, and the mix
the shipped defaults produce has been heard and accepted.

**Evidence (confidence: traced for the ladder, direction-sound for the mix).** The ladder exists and
is documented per option: `--difficulty=` beats the saved word which beats the default, with the note
that `--det` reads no saved option at all (`docs/cli.md`, the `--difficulty=` bullet), and
`OptionsLaunchSuites` (`CSVM/src/Testing/OptionsLaunchSuites.cs:29-56`) is that assertion for
difficulty, saved-file and all. `ApplyMasterVolume` resolves `--volume=` over `audio.volume` over a
default that is 0 in a repo run and 1 in an exported one
(`CSVM/src/Session/Launcher.cs:1600-1621`). The mix half is direction-sound only: decision 5 puts
every category at 0.5, which is 6 dB under today's build by construction, and whether that is right
is the author's ear.

**Approach.** Assert the ladder rather than describe it, mirroring `OptionsLaunchSuites`: a saved
level reaches the bus, `--det` leaves every bus at its resting gain, and `--volume=0` silences the
output whatever the saved levels say. Then judge the mix at the controls at the shipped defaults and
record what comes out of it: any level that changes as a result is TUNE and lands as the new default,
and any residual complaint that is really about one sound's own gain becomes its own `backlog.md`
entry rather than a default. Update `docs/cli.md`'s `--volume=` bullet to name the sliders as the
separate mix below it, `docs/menu-presentations.md`'s Audio section to carry the bus model and the
four levels, and each touched module's entry in `docs/architecture/`.

**Model recommendation.** high. The regression surface is every sound in the game and the judgement
is the author's, so the item is as much about capturing a verdict as about landing code.

**Verify.** The ladder in suites, with the guard proved able to fail: let a saved level through under
`--det` once, see the suite report it, then restore. The mix at the controls, over a sortie that
carries all three categories at once (a campaign mission with a briefing, radio callouts, combat
voice and gunfire). ⚠ Today's mix judgements were all made before this re-base: `BL-252`'s overspeed
whine level, `BL-281`'s faint ricochets and the crash levels in `backlog.md`'s Damage section were
each heard against a full-gain effects path, so any of them re-read at the new default is a
re-measurement and not a new finding. Full 8-chapter `--freecam --chapter=<X>` regression with zero
errors. `.\RunTests.ps1` green.

**⚠ Traps.** "It sounds fine" is not a verified ladder; the flag, the config key, the saved file and
`--det` are four separate paths and only a suite that has been seen to fail proves any of them.
Do not let a saved level un-silence a repo run: that is the failure decision 6 exists to prevent, and
it is the one this item has to demonstrate rather than assume. `BL-455` is deleted from `backlog.md`
in this item's commit, not marked fixed there, and its record goes in the commit message.
