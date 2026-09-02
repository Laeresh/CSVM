# Input rebinding — a named-action seam and a device model that outlives the original's

**ACTIVE PLAN** (written 2026-09-02). It sits in `docs/`, which by this repo's convention makes it
a live plan; PROJECT_CONTEXT.md's "Current status" names it. Move it to `docs/plans/` with a
`COMPLETE` banner, and add its row to [`plans.md`](plans.md), when every item lands.

This plan delivers `BL-296` (a per-player `ActionMap`: named actions over the raw key and pad
polling) and `BL-398` (a rebindable keymap) as one piece of work, because they are one design. The
`ActionMap`'s data model *is* the rebinding feature's data model: if the seam lands as a minimal
enum-to-keycode indirection, the rebind layer has to break it open again to introduce device
identity and axis-as-button. The plan therefore settles the binding model first (Wave A), migrates
every polling site onto it (Wave B), makes it persist with its shipped defaults (Wave C), and only
then builds the screen that edits it (Wave D).

`BL-398` was re-verified still-open in this session against both the record (`git log --grep=BL-398`
is empty) and the code (nothing named `ActionMap` exists anywhere under `CSVM/src`).
`BL-296` was re-verified against the code by the same search but <TODO: re-verify BL-296 still-open
against `git log --grep=BL-296` and `git log --grep=BL-295`, which its entry names as the decision
that set its shape>.

**Out of scope.** `BL-357` (the hardpoint selector's missing second direction) and `BL-351`
(target-class cycling keys) are *unblocked* by this plan and are not part of it. Both are stuck only
because the flight keymap has no spare paired keys, and both become ordinary items the moment
rebinding exists. Landing them here would turn an input plan into a weapons plan. Likewise the
targeting keys that `BL-398` complains about (`T` `Y` `U` `I` `O`, chosen because they were empty)
stay exactly as they are: this plan makes them changeable, it does not change them.

## Milestone goal

- A control is a named action, resolved through a per-player `ActionMap`, at every input site in the
  game. No gameplay code polls a key or a pad button directly.
- A binding is a stable device identity plus a tagged control (button, signed axis with deadzone, or
  hat direction), held in a list. An axis can drive a digital action and a button can drive an
  analogue one.
- Bindings survive a restart, and survive a pad being unplugged and plugged back in.
- The player can rebind any action from a menu screen, per player, with conflicts resolved the way
  the original resolves them: reassigning a control takes it from its previous owner.
- `docs/controls.md` becomes a record of the shipped defaults rather than the definition of the
  keymap.

**No behaviour change reaches the sim.** Every action resolves to the same boolean or float it
resolves to today, on the same 60 Hz fixed tick, and `--det` / `--hold` runs reproduce bit for bit.
An input refactor that changes what the aeroplane does has failed, however good the new model looks.

## Decisions (2026-09-02)

| # | Question | Decision |
|---|---|---|
| 1 | `BL-296` then `BL-398` as two items, or one plan? | **One plan.** The seam's data model is the rebind feature's data model; sequencing them means designing it twice and breaking the first one open. |
| 2 | Copy the original's binding encoding? | **No.** Take its *semantics* (several bindings per action, all ORed; reassignment steals the control from its previous owner), reject its *encoding* (a scancode or a bare button index in a fixed typed slot). |
| 3 | How wide does the device model have to be? | **Wider than the original, deliberately.** Multiple pads, axes and hats bindable as digital controls, devices identified by something stable across a replug. The original's one-joystick ten-button ceiling is a fact about a 2000 build, not a target. |
| 4 | Does the plan change any current binding? | **No.** The shipped defaults reproduce today's `docs/controls.md` exactly. Rebinding is the deliverable; a better default keymap is a separate judgement. |

## ⚠ Read this before implementing anything

The original's keybind screens were transcribed off seven German-language screenshots into
`docs/plans/PLAN-targeting.md:115-160`, and that transcription was the project's reference for the
original's keymap. It has now been decoded out of `crimson.exe` (`docs/org/input.md`). Six readings
did not survive. None of them changes what this plan builds, and all six are recorded so nobody
re-derives them from the pictures.

| # | The wrong claim | How it died |
|---|---|---|
| 1 | External Camera is `F10`-`F12` | The defaults emit `F9` Down, `F10` Back, `F11` Left, `F12` Right, plus `F7` Access Chase View which the transcription omits entirely (`FUN_004936c0`) |
| 2 | Fire is one action bound to `Space` and `X` | Two separate actions: `0x13` Fire Guns = Space + joystick button 1, `0x14` Fire Rockets = X + joystick button 2 |
| 3 | The keybind screen's two columns are two fields | Four slots per record (keyboard A, keyboard B, joystick button, mouse button); the row renderer `FUN_00449fc0` packs the first two non-empty into the visible columns |
| 4 | Only two joystick defaults exist (buttons 3 and 6) | Six do: also guns-counterclockwise = 7, rockets-counterclockwise = 8, Toggle Spyglass = 5, Cycle Cockpit Views = 4 |
| 5 | Turn Left/Right are bound to `,` and `.` alone | Both carry a second keyboard binding, Numpad0 and NumpadDecimal, in slot B |
| 6 | Throttle up/down are `´` and `ß` | They are `DIK_EQUALS` and `DIK_MINUS`. The screenshot reading was correct for a German layout, but the stored value is a layout-independent scancode |

⚠ **The clockwise/counterclockwise names are inverted against their message keys** in the original:
"Cycle guns clockwise" is `MSG_CMD_CANNON_PREV`. Trust the displayed name when reading `BL-357`.

⚠ **A d-pad direction is expressible two ways, and the steal rule does not know it.** Godot reports
no raw hat, so `GodotDeviceState` implements hat index 0 as an OR of the four `JoyButton` d-pad
values (A3). That makes `Hat(0, HatDirection.Up)` and `Button(JoyButton.DpadUp)` the same physical
control under two encodings, and `ActionMap.SameControl` (A2) returns false as soon as the two kinds
differ. Two actions can therefore hold one d-pad button at once, which is exactly the state the
original forbids. Neither item is wrong on its own and this is not a defect in either; it appears
only where they meet. **Capture cannot produce a `Hat` binding on this engine** (D31 reads what
Godot reports, which is a button), so the hazard can only arrive from a hand-authored default. C21
owns the defaults and therefore owns the call: either never author a `Hat` binding, or drop
`ControlKind.Hat` from the model as unreachable on this backend. Do not resolve it by widening
`SameControl` to alias the two, which would bake the d-pad's button numbers into the comparison.

| Confidence | Items | What that means for you |
|---|---|---|
| **Traced to an exact mechanism in code, with the data that proves it** | A1, A3, C21 | The original's model is decoded in `docs/org/input.md` with an address per claim. Confirm the trace, then design against it deliberately rather than copying it. |
| **Direction sound, magnitude a judgement call** | A2, D32 | The *shape* is settled; deadzone defaults and the axis-to-digital threshold are TUNE. Add them to `backlog.md`'s TUNE list rather than inventing them as fact. |
| **Leads only — no mechanism yet** | B11, B12, B13, B14, C22, D31, D33 | The polling sites are named in `BL-296`'s entry but have not been read in the session that wrote this plan. Budget for a census pass before the first migration. |

**⚠ Worktree hazard.** `git stash` is repo-global and shared across worktrees — never use it in a
worktree session here; use a local commit or a file copy.

## What the data actually ships

The original's command map is decoded in full at [`docs/org/input.md`](org/input.md), read out of
`crimson.exe` with Ghidra. The facts this plan leans on, each traceable there:

- **The map is one 32-bit word per command id** (`FUN_00537090`), 600 slots allocated by
  `FUN_00536f50`, packing four binding slots: keyboard A (bits 0-10), keyboard B (bits 11-21),
  joystick button 1-10 (bits 22-25), mouse button 1-3 (bits 26-27).
- **`FUN_00537530` ORs all four slots.** An action has up to four bindings at once and any of them
  fires it. This is the semantics worth reusing.
- **Reassignment steals.** `FUN_005371d0` and `FUN_00537230` clear a control out of its previous
  owner's word, and `FUN_00535fb0` refuses a second handler for a claimed code. Two actions cannot
  share a control. This is the conflict rule worth reusing.
- **A keyboard binding is a raw DIK scancode plus `0x100`/`0x200`/`0x400` Alt/Ctrl/Shift bits**
  (`FUN_005357b0`), so it names a key *position*, not a character.
- **The device ceiling is hard.** One joystick pointer in the whole program (`DAT_0075c1e0`),
  buttons gated `1 <= n < 0xb` (`FUN_00536c40`), axes and hats read outside the map and unbindable.
- **The 64 shipped defaults are emitted by code** (`FUN_004936c0`), not held in a table, and persist
  as 2400 raw bytes under `HKEY_CURRENT_USER\SOFTWARE\Microsoft\Microsoft Games\Crimson Skies\1.0`
  with no version field (`FUN_005bdc89`, `FUN_005bd900`).

On our side, the polling census, taken during A2 over `Input.IsKeyPressed`, `IsJoyButtonPressed`,
`GetJoyAxis` and `IsMouseButtonPressed` under `CSVM/src`: **84 occurrences in 6 files**. The real
sites are `FlightController.cs` (34), `MenuInput.cs` (33) and `SpectatorCamera.cs` (14);
`Launcher.cs` holds 1, and the two hits under `Bindings/` are the interface and its own
documentation.

⚠ **`BL-296`'s "about a dozen bindings in `FlightController`" undercounts by roughly three times**,
and the same entry's three-file list is complete only because `Launcher.cs`'s single site is
incidental. Wave B is therefore larger than its Evidence lines assumed: B12 (`MenuInput`, 33) is
comparable in size to B11's `FlightController` (34), not the small follow-up the wave ordering
implies.

## Ground rules

- **Original-game data drives everything.** Read the reader/compiled JSON before writing a handler;
  never guess a value. Inventing content is the trap this project falls into most often.
- **Evidence is a lead to verify, not a finding to implement.** Confirm every claim against the
  data/code before building on it; **a correct disproof that lands no code is a success here**, not a
  failure. Mark each item's Evidence with its confidence (traced-to-code / direction-sound-magnitude-
  TUNE / lead-only).
- **`PROJECT_CONTEXT.md` + `docs/architecture.md` / `docs/formats/` are updated in the same turn** as each
  landed item; a landed item gets its record in the landing commit's message (`docs/HISTORY.md` is
  frozen — never append) and is **deleted** from
  `backlog.md` (not marked FIXED there). New decodes land with their `docs/formats/` page.
- **Read `docs/verification.md` before measuring anything** — the instruments here mislead; cite the
  rule that bites per item.
- **Verify against a full 8-chapter `--freecam --chapter=<X>` regression** (zero errors, same
  mesh/node counts unless the change is meant to add coverage) plus a targeted capture at the
  location the report came from.
- **Read the module's entry in `docs/architecture.md` before modifying it.** Dead ends are recorded
  there precisely so they are not re-chased.

## Checklist

Statuses: ☐ open · ◐ in progress · ☑ done · ❌ closed/disproven. **Keep this in sync as items land.**

### Wave A — the model

1. ☑ The binding model: device identity, tagged control, binding list
2. ☑ `InputAction` and `ActionMap`: named actions resolved per player
3. ☑ The device registry: enumeration, stable identity, hot-plug

### Wave B — the migration

11. ☐ Census the polling sites, then migrate `FlightController`
12. ☐ Migrate `MenuInput`
13. ☐ Migrate `SpectatorCamera` and whatever the census turns up
14. ☐ Determinism gate: `--det` / `--hold` reproduce bit for bit

### Wave C — defaults and persistence

21. ☑ Persist the map: a versioned format, per player
22. ☐ `docs/controls.md` becomes the shipped-defaults record

### Wave D — the screen

31. ☐ The rebinding screen: capture, assign, steal-from-previous-owner
32. ☐ Binding an axis or a hat to a digital action
33. ☐ Close `BL-296` and `BL-398`, and hand `BL-357` its keys

## Dependency and parallelism notes

A1 blocks everything: every later item reads the model it defines. A2 depends on A1 and blocks all
of Wave B. A3 is independent of A2 and can run beside it once A1 lands, but both write to the same
new namespace, so give them separate files and a stated boundary (A2 owns the map and resolution,
A3 owns device enumeration and identity).

Wave B is a fan-out: B11, B12 and B13 touch disjoint files and can run in parallel worktrees once A2
lands. B14 is a gate, not a change, and runs after all three. **File contention:** B11 is the large
one (`FlightController`) and must not run in parallel with any other item that touches it.

C21 depends on A1 and A3 (it serializes device identity) but not on Wave B. C22 is documentation and
depends on C21 landing the real default set. D31 depends on A2, A3 and C21. D32 depends on D31 and
A1. D33 is the closing item and depends on everything.

---

# Wave A — the model

## A1 ☑ The binding model: device identity, tagged control, binding list

**Landed.** `CSVM/src/Bindings/`, namespace `CSVM.Bindings`, five files: `DeviceId` (kind plus a
stable hardware string, no connection index in the type), `BindingControl` (the four-way tag, built
only through `Key`/`Button`/`Axis`/`Hat` factories that hold the invariants), `Binding` (a device
and a control, with `Resolve`), `IDeviceState` (this tick's raw hardware, addressed by identity,
contracted to answer for an absent device rather than throw), and `BindingSet` (the list an action
owns, ORing its members).

Three calls the plan left open, settled here: the namespace is `CSVM.Bindings` and the type is
`BindingControl`, because `CSVM.Input` makes `Input.IsKeyPressed` resolve to the namespace at every
future call site and a bare `Control` collides with `Godot.Control`; the deadzone doubles as the
digital threshold, so `Pressed` holds exactly when `Value` is above zero and a second number is
D32's to add if it needs one; and a set's analogue read takes the deepest deflection rather than the
first, so a half-pressed trigger cannot beat a fully held button on the same action. The plan's
"an action owns a `List<Binding>`" became `BindingSet` rather than a bare list, because the OR rule
and the duplicate policy need a home both the map and a screen can reach.

**Verified.** `CSVM.Tests/BindingModelTests.cs`, 12 facts over a fake `IDeviceState`, covering the
three cases the plan named plus device identity silencing the same button on another pad, sign
selecting the half of travel, cross-driving in both directions, deepest-deflection combining, an
absent device leaving the binding in place, and factory rejection. Complete `.\RunTests.ps1` in the
item's worktree: PASS, exit 0, 3085 units, 230 engine suites, 18 goldens hash-identical, 0 build
warnings (188.5s total, over the 180s budget, which is awareness only).

**Original approach (kept for reference).**

**Goal.** One type describes any binding the game can hold: a keyboard key, a pad button, a signed
axis past a deadzone, or a hat direction, on a named device. An action holds a list of them and
fires when any one resolves true, which is the original's semantics without the original's ceiling.

**Evidence (confidence: traced).** The original's model is decoded at `docs/org/input.md`: four
fixed typed slots in a 32-bit word (`FUN_00537090`), ORed at `FUN_00537530`, with reassignment
stealing a control from its previous owner (`FUN_005371d0`, `FUN_00537230`, `FUN_00535fb0`). Its
limits are equally traced and equally deliberate to reject: one device pointer (`DAT_0075c1e0`), ten
buttons (`FUN_00536c40`), no axis or hat binding at all. Decision 3 above is the call this item
implements.

**Approach.** A discriminated shape, not four typed fields: `Binding = (DeviceId, Control)` where
`Control` is one of `Key(code)`, `Button(index)`, `Axis(index, sign, deadzone)`, `Hat(index,
direction)`. An action owns `List<Binding>`. Resolution returns both a bool and a float so an axis
can drive a digital action (past threshold) and a button can drive an analogue one (0 or 1), which
is the property the original cannot express. Write it as a real module with its own file, not as
fields hung off `FlightController`.

**Model recommendation.** high. This is the decision the other eleven items inherit, and getting the
shape wrong is the expensive failure this plan exists to avoid.

**Verify.** Unit tests over resolution alone, no engine: an action with three bindings fires on any
one; an axis at 0.4 with deadzone 0.5 does not fire and at 0.6 does; a hat direction resolves
independently of the other three directions on the same hat. `CSVM.Tests/BindingModelTests.cs`, run
with `.\RunTests.ps1 -UnitFilter "FullyQualifiedName~BindingModelTests" -SkipEngine -SkipGoldens`.

**⚠ Traps.** Do not model a binding as "key or button" with a nullable device. The whole point is
that device identity is part of the binding, and a nullable field is how the ceiling creeps back in.
Do not use a Godot `InputEvent` as the stored type: `InputMap` is app-global and cannot express
per-player bindings (`BL-296`'s own trap), so the per-player layer stays ours whatever sits
underneath.

## A2 ☑ `InputAction` and `ActionMap`: named actions resolved per player

**Landed.** Four files in `CSVM/src/Bindings/`: `InputAction` (58 members, every one traceable to a
poll site that exists today), `ActionMap` (one seat's `Dictionary<InputAction, BindingSet>`, with
`Assign` returning the action that lost the control, `TryFindOwner`, `Clone`, `Resolve`),
`ActionSnapshot` (`Held`/`Value`/`Axis`, array-backed and reused each tick so resolution allocates
nothing at 60 Hz, fillable only through `internal` members so nothing outside the assembly can forge
a tick), and `PlayerActions` (the seam a polling site holds).

Calls the plan left open, settled here: "the same control" is not `Binding` equality but a
comparison of device, kind, index and an axis's sign or a hat's direction, deliberately ignoring the
deadzone, so re-binding an already-bound axis adjusts it in place rather than stacking a copy; the
keyboard gate lives on the seat (`PlayerActions.ReadsKeyboard`) rather than in the map, so a pad-only
seat keeps its keyboard defaults and simply does not read them; the snapshot is reused rather than
immutable, and `Current` is documented as live; and the debug and lab keys (`F13`-`F18`, the viewer
and weapon-lab panels) are deliberately outside `InputAction`, because putting them in the enum
would put them in D31's rebinding screen.

**Verified.** `CSVM.Tests/ActionMapTests.cs`, 17 facts over a fake device state, covering the steal
across two actions, the snapshot answering identically twice in one tick, per-player isolation, the
deadzone-adjust case, the two halves of one axis staying independent, and the pad-only seat. Run
with `.\RunTests.ps1 -UnitFilter "FullyQualifiedName~ActionMapTests" -SkipEngine -SkipGoldens`.
Complete `.\RunTests.ps1` in the item's worktree: PASS, exit 0, 3102 units, 230 engine suites, 18
goldens hash-identical, 0 build warnings.

⚠ **This item does not finish the per-player routing.** The plan's Evidence line conflated two
things: `UseKeyboard` is a gate and is implemented here, but `PadDevices` is a device *selection*
(which pad identities a seat may read) and belongs to the device registry. B11 cannot complete a
seat's routing on `PlayerActions` alone.

**Original approach (kept for reference).**

**Goal.** `actions.Held(InputAction.FireGuns)` replaces `Input.IsKeyPressed(...)` at the call site,
resolved through the calling player's own map.

**Evidence (confidence: direction-sound).** `BL-296` fixes the shape: a named-action indirection,
per-player (player 1 keyboard and pad, others pad-only), routed through the existing
`PadDevices`/`UseKeyboard` split. The magnitude question is how many actions there are, which the
B11 census settles.

**Approach.** An `InputAction` enum plus an `ActionMap` holding `Dictionary<InputAction,
List<Binding>>` and a resolver reading device state each tick. Resolution happens once per tick into
a snapshot the consumers read, so two sites asking the same question in one tick cannot disagree.

**Model recommendation.** high. It is the seam every other item plugs into.

**Verify.** Unit tests over a synthetic device state, `CSVM.Tests/ActionMapTests.cs`, run with
`.\RunTests.ps1 -UnitFilter "FullyQualifiedName~ActionMapTests" -SkipEngine -SkipGoldens`. The three
facts that must be there: assigning a control to a second action takes it off the first and reports
which one lost it; a snapshot polled once answers the same twice in a tick while the fake hardware
changes underneath; and the same control on two players' maps resolves to each player's own action.

**⚠ Traps.** **Not an event bus.** `BL-296` is explicit: fire is a held control on the 60 Hz fixed
tick, edge detection stays in the consumers (`FireControl` never changes, it consumes `FireInputs`
booleans), and events would break scripted `--det` / `--hold` runs. The polling *sites* are the
seam; nothing downstream of them moves.

## A3 ☑ The device registry: enumeration, stable identity, hot-plug

**Landed.** `CSVM/src/Bindings/DeviceRegistry.cs` (the live index-to-identity table, pure and
engine-free: `Refresh` takes an already-read roster of `(index, guid, name)` tuples, `IndexOf`
returns a live index or null, `IdentityOf` turns a live index back into the stable id a capture
screen should save) and `CSVM/src/Bindings/GodotDeviceState.cs` (the live `IDeviceState` over
Godot's `Input` singleton, reading every device through the registry so an unresolvable identity
answers false, zero or `HatDirection.None` rather than throwing).

Three calls the plan left open, settled here: two connected pads reporting the same stable string
is a real collision, so first-seen claims the identity and the second stays unresolved rather than
both driving one binding; hat index 0 is the d-pad and every other hat index is unbindable, because
Godot exposes no raw hat API; and `RefreshDevices()` is deliberately not called from a constructor,
leaving the launch and signal wiring to whoever owns a polling loop.

**Verified.** `CSVM.Tests/DeviceRegistryTests.cs`, 10 facts over synthetic rosters: resolve by GUID
regardless of index, unplug to null, replug at a different index, a fresh registry rebuilding the
same identity after a restart, the GUID-to-name fallback, a device reporting neither, the collision
case, an unknown index, the keyboard never resolving an index, and two pads staying independent.
Complete `.\RunTests.ps1` in the item's worktree: PASS, exit 0, 3095 units, 230 engine suites, 18
goldens hash-identical, 0 build warnings.

⚠ **Two things here are still unverified against hardware**, and no automated suite can reach them:
that Godot reports the GUID and name this code expects for real hardware, and that
`Input.Singleton.JoyConnectionChanged` fires on a genuine unplug and replug. The manual check is in
Verify below and is owed at the controls.

**Original approach (kept for reference).**

**Goal.** A binding still points at the right pad after that pad is unplugged, plugged into a
different port, and the game restarted.

**Evidence (confidence: traced, as a negative).** The original has nothing to learn from here: one
device pointer, no index in the record (`docs/org/input.md`, `DAT_0075c1e0`). The requirement comes
from Decision 3, not from the binary.

**Approach.** Identify a device by a stable string (Godot exposes a joypad GUID and name) rather
than by connection index, with a live index-to-identity table maintained on the joy_connection_changed
signal. Unknown or absent device: the binding stays in the map and resolves false rather than being
dropped, so unplugging a pad does not silently erase a player's keymap.

**Model recommendation.** medium. Mechanically contained once A1 fixes the model, but the
absent-device rule needs judgement.

**Verify.** The index-to-identity rule itself is engine-free and checkable without a pad:
`CSVM.Tests/DeviceRegistryTests.cs` drives `DeviceRegistry.Refresh` with synthetic (index, guid,
name) tuples standing in for the connected roster, and covers unplug (`IndexOf` goes to null,
`BindingModelTests` already covers a binding resolving false from that), replug into a different
index (the same guid resolves again at the new one), a restart (a fresh registry rebuilds the same
identity from the same tuple), the GUID-to-name fallback, a device reporting neither, and two pads
that collide on one GUID. Run with
`.\RunTests.ps1 -UnitFilter "FullyQualifiedName~DeviceRegistryTests" -SkipEngine -SkipGoldens`.
What still needs a physical pad: that Godot actually reports a given GUID and name for real
hardware, and that `Input.Singleton.JoyConnectionChanged` fires on a genuine unplug and replug.
`GodotDeviceState` reads both but no automated suite exercises them; confirm at the controls by
binding an action to a pad, unplugging it, watching the action go quiet, and replugging into a
different port to see it fire again.

**⚠ Traps.** Godot's joypad index is a connection slot and is reused. Storing it is exactly the bug
this item exists to prevent. Godot has no raw hat/POV API of its own: a controller's d-pad arrives
as four `JoyButton` values (`DpadUp`/`DpadRight`/`DpadDown`/`DpadLeft`), so `GodotDeviceState`
treats hat index 0 as that d-pad and every other hat index as unbindable. The plan's Approach names
a "live index-to-identity table maintained on the joy_connection_changed signal" without saying who
owns the wiring; `GodotDeviceState.RefreshDevices()` does the read and is meant to be called once at
launch and again from that signal, but the call site is left for whoever builds the per-tick
resolver (A2's `ActionMap`, or `GameSession`), since A3 has no polling loop of its own to hook it
into.

# Wave B — the migration

## B11 ☐ Census the polling sites, then migrate `FlightController`

**Goal.** No raw input poll remains in `FlightController`, and the aeroplane flies identically.

**Evidence (confidence: lead-only).** `BL-296` names `FlightController` as carrying about a dozen
bindings, with `MenuInput` and `SpectatorCamera` alongside it, and `docs/controls.md` as the binding
record. <TODO: none of these files were read in the session that wrote this plan. Start with the
census and correct this Evidence line from it.>

**Approach.** Census first (`grep` for `IsKeyPressed`, `IsJoyButtonPressed`, `GetAxis`, `IsActionPressed`
under `CSVM/src`), record the real count in "What the data actually ships", then replace each site
with an `InputAction` lookup. One action per existing binding, no renaming and no regrouping, so the
diff is mechanical and reviewable.

**Model recommendation.** medium. Mechanical once A2 exists, but the file is large and the blast
radius is the whole flight model.

**Verify.** `.\RunTests.ps1` in full (this lands under `CSVM/`), plus a `--det` run compared against
a baseline taken *before* the change. An unchanged number is not evidence unless you have seen it
able to fail, so take the baseline first.

**⚠ Traps.** `docs/controls.md:29-33` and `:46` record bindings that are load-bearing beyond the
keymap: `L` is *reserved and deliberately unbound* for Track Target (`BL-399`), and the debug keys
`F13`-`F17` follow the physical rows of the author's keypad rather than a contiguous block. Do not
tidy either while migrating.

## B12 ☐ Migrate `MenuInput`

**Goal.** Menu navigation resolves through the same seam.

**Evidence (confidence: lead-only).** Named in `BL-296`. <TODO: read `MenuInput` and state what it
polls.>

**Approach.** As B11, scoped to the menu actions.

**Model recommendation.** medium, low effort. Mechanical fan-out.

**Verify.** <TODO.>

**⚠ Traps.** Menu input runs outside the fixed tick. Confirm the once-per-tick snapshot in A2 does
not starve it before reusing the same resolver here.

## B13 ☐ Migrate `SpectatorCamera` and whatever the census turns up

**Goal.** The last raw polls are gone.

**Evidence (confidence: lead-only).** Named in `BL-296`, plus whatever B11's census finds beyond the
three named files.

**Approach.** As B11.

**Model recommendation.** medium, low effort.

**Verify.** A repo-wide grep for the poll calls returns only the resolver itself.

**⚠ Traps.** <TODO: fill from the census.>

## B14 ☐ Determinism gate: `--det` / `--hold` reproduce bit for bit

**Goal.** Proof that the refactor changed no behaviour.

**Evidence (confidence: lead-only).** `BL-296`'s trap states that an event-based seam would break
scripted `--det` / `--hold` runs, which makes those runs the instrument that proves the polling seam
was kept.

**Approach.** A gate, not a change. Take baselines before Wave B starts.

**Model recommendation.** medium. Reading a diff, not writing code.

**Verify.** <TODO: name the exact `--det` / `--hold` invocations and the comparison, from
`docs/verification.md` and `docs/cli.md`.>

**⚠ Traps.** ⚠ `docs/verification.md` exists because the instruments here mislead. Read it and cite
the rule that bites before quoting any number as a pass.

# Wave C — defaults and persistence

## C21 ☑ Persist the map: a versioned format, per player

**Landed.** Four files in `CSVM/src/Bindings/`: `InputContext`, `DefaultBindings` (the shipped keymap
as data, with `MapFor`, `ActionsIn`, `Retarget` and the `Unbound` list), `BindingProfile` (one seat:
a map and a `PlayerActions` per context) and `BindingStore` (versioned JSON per player, written
atomically through a temp file the way `OptionsStore` does).

⚠ **One map per seat cannot hold this keymap, so a seat holds one map per `InputContext`.** Today's
bindings give one control different meanings by mode: `W` is pitch-down, menu-up and camera-forward;
`Space` is fire guns and menu accept; `Escape` is pause and menu back. `ActionMap` holds a control
once and `Assign` steals, so a single map would make those collide. The steal rule now runs inside a
context, and a context is the scope D31 edits. The plan's "a default `ActionMap` per seat" was
wrong.

Other calls settled here: a shipped pad default cannot name a hardware string, so defaults are
authored on the placeholder identity `DeviceId.Joypad("*")` and `MapFor` substitutes the seat's pad,
which keeps a saved file portable between machines; `ReadsKeyboard` is deliberately not persisted,
since a saved file would otherwise hand a pad-only splitscreen seat its keyboard back; and one
unreadable token costs an action its saved bindings and it keeps its default, while an empty array
is a deliberate unbind and survives.

**The d-pad decision (the trap above): never author a `Hat` binding, and keep `ControlKind.Hat`.**
Every d-pad default is `Button(JoyButton.Dpad*)`, which is what Godot reports and what D31 capture
produces, so the two encodings never coexist and `SameControl` never has to alias anything. The kind
stays because dropping it would churn landed files for no gain and it is the right shape the moment
a backend with real hats exists. The prohibition is stated on the `BindingControl.Hat` factory, in
`DefaultBindings`, and in `docs/architecture.md`, and `BindingStore` treats a hat token as
unreadable so a hand-edited file cannot reintroduce the alias.

**Verified.** `CSVM.Tests/DefaultBindingsTests.cs` (8 facts, including the full-coverage gate that
stops Wave B finding holes one call site at a time) and `CSVM.Tests/BindingStoreTests.cs` (15 facts:
round-trips, a bumped-version file, unknown action, unknown context, a hat token, a malformed file,
`pad:*` retargeting, per-player isolation, and a leftover temp file). Run with
`.\RunTests.ps1 -UnitFilter "FullyQualifiedName~DefaultBindingsTests|FullyQualifiedName~BindingStoreTests" -SkipEngine -SkipGoldens`.
Complete `.\RunTests.ps1` in the item's worktree: PASS, exit 0, 3135 units, 230 engine suites, 18
goldens hash-identical, 0 build warnings.

**Coverage: 56 of `InputAction`'s 58 members** (Flight 35, Menu 10, Camera 13). `MenuJoin` is
unbound because joining is "any control on a pad no seat owns" and every control it watches already
belongs to another menu action. `FreeLook` is unbound for a reason that outgrew this item and became
A4: it is the held right mouse button, and the model has no mouse control kind.

Three places where the code and `docs/controls.md` disagreed were resolved toward the doc, per
Decision 4: `CycleStuntTarget` is `Tab` only (`FlightController` also reads pad `X`, undocumented and
colliding with `Nitro`), `TargetNearest` is keyboard `I` only (its pad route is the tap/hold splitter
on d-pad up, which is one control dispatching to two actions inside the consumer), and the freecam
`IJKL` directions take `SpectatorCamera`'s own sign.

**Original approach (kept for reference).**

**Goal.** Bindings survive a restart, and a future change to the model does not silently corrupt an
existing user's map.

**Evidence (confidence: traced).** The original writes 2400 raw bytes to the registry with no
version field, no key names and no textual form, and points its live array straight at the loaded
buffer (`docs/org/input.md`, `FUN_005bdc89`, `FUN_00536f50`). That is the anti-pattern: a struct
change would reinterpret an old blob as the new layout.

**Approach.** A named, versioned, human-readable form (device identity by string, action by name),
per player, in the user data directory. On a version mismatch or an unknown action name, fall back
to that action's default rather than failing the load.

**Model recommendation.** high. Format decisions are expensive to reverse once a user has a file.

**Verify.** `CSVM.Tests/BindingStoreTests.cs` (the round-trip whole, a rebind and an unbind through
it, a hand-written file from a bumped version keeping the rows this build reads, and the per-action
fallbacks) and `CSVM.Tests/DefaultBindingsTests.cs` (every `InputAction` bound or on the
deliberately-unbound list, no hat default, one control per action inside a context). Run with
`.\RunTests.ps1 -UnitFilter "FullyQualifiedName~DefaultBindingsTests|FullyQualifiedName~BindingStoreTests" -SkipEngine -SkipGoldens`.

**⚠ Traps.** ⚠ PowerShell 5.1 corrupts UTF-8 in BOM-less files. If any script touches this format,
pass `-Encoding utf8` on both ends and keep the script itself ASCII (`CLAUDE.md`).

## C22 ☐ `docs/controls.md` becomes the shipped-defaults record

**Goal.** The doc says "these are the defaults", not "these are the bindings".

**Evidence (confidence: lead-only).** `BL-296`'s entry ends with "Update `docs/controls.md` when this
lands."

**Approach.** Reword the framing, add the rebinding pointer, and state the default set as data the
code actually ships (C21's default table) rather than as a hand-maintained list that can drift.

**Model recommendation.** medium. Prose, but it is the user-facing record.

**Verify.** Every default in the doc matches the shipped table, checked by reading both.

**⚠ Traps.** Writing-style rules apply (`CLAUDE.md`): no em dashes, no dates in live prose, no
event narration. The keypad debug-key layout is intentional and is not a mistake to tidy.

# Wave D — the screen

## D31 ☐ The rebinding screen: capture, assign, steal-from-previous-owner

**Goal.** The player opens a screen, picks an action, presses a control, and that control is now
bound to it.

**Evidence (confidence: lead-only, with a traced conflict rule).** The conflict semantics are
decoded: reassigning a control clears it from its previous owner and two actions cannot share one
(`docs/org/input.md`, `FUN_005371d0`, `FUN_00535fb0`). The screen itself has no precedent in our
codebase. <TODO: name the menu framework the screen is built in, from `docs/architecture.md`.>

**Approach.** A capture mode that reads the next control from any device, resolves it to a
`Binding`, and assigns it. Show the steal explicitly (name the action losing the control) rather
than performing it silently.

**Model recommendation.** high. UI plus conflict semantics plus per-player routing.

**Verify.** <TODO: the manual check, in splitscreen: rebind for player 2 only and confirm player 1
is unaffected.>

**⚠ Traps.** The original ships four slots per action and shows the first two non-empty
(`FUN_00449fc0`), which means its screen *hides* bindings. Ours holds a list, so the screen must
show all of them or say how many it is not showing.

## D32 ☐ Binding an axis or a hat to a digital action

**Goal.** A player can bind a trigger or a stick direction to an action that was designed as a
button, and it works.

**Evidence (confidence: direction-sound).** A1 makes it expressible; the threshold and deadzone
defaults are TUNE, not decoded. The original cannot express this at all, so there is nothing to
match.

**Approach.** Capture mode recognises an axis crossing its deadzone as a binding candidate, with the
sign taken from the direction moved. Defaults for deadzone and the digital threshold go to
`backlog.md`'s TUNE list rather than being asserted here.

**Model recommendation.** medium.

**Verify.** <TODO: manual, at the controls.>

**⚠ Traps.** ⚠ A resting stick drifts. Capture must not latch the first axis it sees at rest, which
is the standard failure of this feature.

## D33 ☐ Close `BL-296` and `BL-398`, and hand `BL-357` its keys

**Goal.** Both backlog items are retired, and the item that was blocked on key space knows it is
free.

**Evidence (confidence: lead-only).** `BL-357` is blocked purely on there being no spare paired
keys; `BL-351` defers explicitly to "whatever input seam exists when this lands".

**Approach.** Run `/close-backlog-item` for `BL-296` and `BL-398`. Amend `BL-357` and `BL-351` to
name the seam rather than the blocker. Do not implement either.

**Model recommendation.** medium.

**Verify.** `backlog.md` no longer defines `BL-296` or `BL-398`; the content gate passes.

**⚠ Traps.** A closed item is deleted from `backlog.md`, not marked FIXED (Ground rules). The
closing evidence and its date go in the commit message, found later with `git log --grep=BL-398`.
