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
**Resolved by C21**, which authors every d-pad default as a `Button` and makes `BindingStore` reject
a hat token, so the two encodings never coexist. `ControlKind.Hat` stays in the model.

⚠ **`ActionMap.Assign` steals from only the first owner it finds.** Its scan `break`s on the first
match, so a control held by two actions loses it from one of them and stays on the other. That state
is now reachable rather than theoretical: C21's defaults deliberately put each numpad snap-look
diagonal on two actions, through `ActionMap.Add`, which binds without stealing. Nothing today calls
`Assign` on those, so nothing is broken yet. **D31 owns the call**, because a rebinding screen is
the first thing that will: either `Assign` steals from every owner and reports a list rather than
one action, or the screen refuses the edit and says which actions share the control. Do not leave it
to be discovered at the controls.
**Resolved by D31**, which took the first branch: `Assign` steals from every owner and returns them
in enum order, `TryFindOwner` is replaced by `OwnersOf`, and there is no single-owner form left for a
caller to reach for.

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
4. ☑ A mouse control kind, so free-look is bindable at all

### Wave B — the migration

11. ☑ Census the polling sites, then migrate `FlightController`
12. ☑ Migrate `MenuInput`
13. ☑ Migrate `SpectatorCamera` and whatever the census turns up
14. ☑ Determinism gate: `--det` / `--hold` reproduce bit for bit
15. ☑ Reconcile what the migration proved: one seat device state, the axis rescale, the stunt marker

### Wave C — defaults and persistence

21. ☑ Persist the map: a versioned format, per player
22. ☑ `docs/controls.md` becomes the shipped-defaults record

### Wave D — the screen

31. ☑ The rebinding screen: capture, assign, steal-from-previous-owner
32. ☑ Binding an axis or a hat to a digital action
33. ☐ Close `BL-296` and `BL-398`, and hand `BL-357` its keys
34. ☑ The saved keymap is read at launch, so a flight rebind is felt
35. ☑ Staged edits and a whole-map reset: Accept, Cancel, Reset to default

⚠ **34 lands before 33, and 33 lands last of the wave.** A flight rebind is written and never read,
so closing `BL-296` and `BL-398` ahead of 34 would retire two items against a feature that works in
one context of three. 35 is landed and does not change that order.

## Dependency and parallelism notes

A1 blocks everything: every later item reads the model it defines. A2 depends on A1 and blocks all
of Wave B. A3 is independent of A2 and can run beside it once A1 lands, but both write to the same
new namespace, so give them separate files and a stated boundary (A2 owns the map and resolution,
A3 owns device enumeration and identity).

⚠ **C21 runs before Wave B, not after it.** As written this plan had Wave B migrating polling sites
onto a map that nothing populates, which would have made each migrating item hand-author its seat's
bindings at the call site and C21 then lift the same defaults out again. A2's report names the
dependency directly: a polling site needs an `IDeviceState` (A3, landed) *and* a populated map. The
default set is C21's, so C21 is promoted ahead of the migration and Wave B consumes it. C22 still
follows C21, and the wave letters stay as they are, since the IDs are cross-references rather than an
order.

Wave B is then a fan-out: B11, B12 and B13 touch disjoint files and can run in parallel worktrees.
B14 is a gate, not a change, and runs after all three. **File contention:** B11 is the large one
(`FlightController`, 34 sites) and must not run in parallel with any other item that touches it.
B12's `MenuInput` (33 sites) is very nearly as large, so do not schedule it as a quick follow-up.

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
this item exists to prevent. Godot has no raw hat or POV API of its own: a controller's d-pad arrives
as four `JoyButton` values (`DpadUp`/`DpadRight`/`DpadDown`/`DpadLeft`), so `GodotDeviceState`
treats hat index 0 as that d-pad and every other hat index as unbindable. The plan's Approach names
a "live index-to-identity table maintained on the joy_connection_changed signal" without saying who
owns the wiring; `GodotDeviceState.RefreshDevices()` does the read and is meant to be called once at
launch and again from that signal, but the call site is left for whoever builds the per-tick
resolver (A2's `ActionMap`, or `GameSession`), since A3 has no polling loop of its own to hook it
into.

## A4 ☑ A mouse control kind, so free-look is bindable at all

**Landed.** `ControlKind.Mouse` end to end: `DeviceId.Mouse` (a singleton with an empty id, following
`DeviceId.Keyboard`, since there is one mouse and no registry involvement), the
`BindingControl.Mouse` factory, a resolve arm in `Binding.Resolve`, `IsMouseButtonDown` on
`IDeviceState` implemented over `Input.IsMouseButtonPressed`, a `mouse:` token in `BindingStore`, and
`FreeLook` bound to the right button. `Unbound` holds `MenuJoin` alone, and its test asserts that
exact list rather than a count, so the tightening cannot be masked by a loosened assertion.

**A pad-only seat's mouse is silenced along with its keyboard**, which this item did not anticipate
and which adding a member to `IDeviceState` forced. It is not a new rule: `FlightController:3681`
already reads free-look as `UseKeyboard && Input.IsMouseButtonPressed(MouseButton.Right)`, so the
mouse is gated on the keyboard seat today, and `PlayerActions.MutedKeyboard` reproduces that. Left
ungated, every splitscreen pad-only seat would have gained a live free-look off the one physical
mouse, which is a behaviour change and this plan forbids those.

**Verified.** A resolve fact in `BindingModelTests`, a `FreeLook_IsTheRightMouseButton` fact and the
tightened `Unbound` assertion in `DefaultBindingsTests`, and a `mouse/mouse:Right` round-trip in
`BindingStoreTests`. Run with
`.\RunTests.ps1 -UnitFilter "FullyQualifiedName~BindingModelTests|FullyQualifiedName~DefaultBindingsTests|FullyQualifiedName~BindingStoreTests" -SkipEngine -SkipGoldens`.
Complete `.\RunTests.ps1` in the item's worktree: PASS, exit 0, 3138 units, 230 engine suites, 18
goldens hash-identical, 0 build warnings.

**Original approach (kept for reference).**

**Goal.** `InputAction.FreeLook` has a default binding like every other action, and B11 and B13 can
migrate their free-look sites instead of leaving a raw poll behind.

**Evidence (confidence: traced).** C21 bound 56 of 58 actions and left `FreeLook` out because the
model has no mouse control kind, while `docs/controls.md` ships RMB-held free-look twice: the
first-person head pan, and the freecam look posture that `SpectatorCamera` reads. The gap is not
hypothetical, and two Wave B items run into it.

⚠ **This is the one respect in which our model came out narrower than the original's, and Decision 3
says it should be wider.** `crimson.exe` carries a mouse button in bits 26-27 of every command word,
values 1-3 for left, right and middle (`FUN_00537150`, named by `FUN_005379b0`, decoded in
`docs/org/input.md`). A1 modelled keyboard, pad button, axis and hat, and dropped the one input the
original actually had. Nothing decided that: it fell out of this plan's own Approach line naming
four kinds, which was written from the pad-and-keyboard end of the problem.

**Approach.** A fifth `ControlKind.Mouse` carrying a button index, its factory beside the other four,
a resolve arm in `Binding.Resolve`, an `IsMouseButtonDown` member on `IDeviceState` implemented over
`Input.IsMouseButtonPressed`, a `mouse:` token in `BindingStore`, and `FreeLook` bound to the right
button in `DefaultBindings` with its entry dropped from `Unbound`. Pointer motion is out of scope:
this binds the button that gates free-look, not the delta, which stays where it is.

**Model recommendation.** medium. Contained and well-specified, but it touches five landed files and
their tests.

**Verify.** `DefaultBindingsTests`'s coverage gate tightens by one, leaving `MenuJoin` as the only
member of `Unbound`. Add a `mouse:` round-trip through `BindingStore` and a resolve test over the
fake device state in the shape A1 used. Complete `.\RunTests.ps1`.

**⚠ Traps.** Do not model pointer motion here. Free-look direction is a relative law with no absolute
position (`docs/controls.md` says so, and it is why the pad takes a different path), and folding
motion in would put a delta into a type whose whole contract is a held-and-how-far pair. Leave
`ControlValue` alone.

# Wave B — the migration

## B11 ☑ Census the polling sites, then migrate `FlightController`

**Landed.** `FlightController` resolves every attitude, weapon, targeting and view control through
`InputContext.Flight`. The census counted wrapper calls rather than raw polls: the file reached the
hardware at four reads behind `KeyDown` / `KeyAxis` / `PadPressed` / `PadAxis`, and the work was the
two rules those wrappers carried that the seam did not, the `UseKeyboard` gate and
`Pads.For(PadDevices)`.

One `ActionMap` carries three `PlayerActions` readers over it, full, keyboard-muted and pad-muted,
because A2's one-profile-polled-once shape does not survive `ReadKeyboard`: the keyboard and pad
halves of one attitude action take different processing, a `StickRamp` ramp against a `StickCurve`
curve, and are then summed and clamped, so a single OR-ed read would delete both silently. `PollInput`
is idempotent per rendered frame, since input is read from `_Process`, `SimStep`, `EndPhotoMode` and
`LandingApproachRuntime` and no one caller owns the site.

Two behaviour changes are recorded rather than hidden: a three or four key tie on one attitude axis
reads zero where the old count difference gave a sign, which follows from the OR-ed default set; and
pad `X` stopped cycling the stunt target, which B15 answered by putting `CycleStuntTarget` on `D-pad
Up`.

**Verified.** `FlightBindingMappingTests`, 77 facts, each asserting an action resolves from the
control the old code polled. A `--det` baseline taken in the item's worktree before the first edit
over the two scripted runs below, seven PNGs md5-identical after the change and again after the
pad-list caching. Complete `.\RunTests.ps1` in the item's worktree, exit 0, 3214 units, 230 engine
suites, 18 goldens hash-identical.

**Original approach (kept for reference).**

**Goal.** No raw input poll remains in `FlightController`, and the aeroplane flies identically.

**Evidence (confidence: traced).** `FlightController` reaches the hardware through four wrappers
(`KeyDown`, `KeyAxis`, `PadPressed`, `PadAxis`) rather than at 34 raw calls, and those wrappers carry
two rules the seam does not: the keyboard gate `UseKeyboard`, and `Pads.For(PadDevices)`, which for a
single player is every connected pad rather than one identity. `docs/controls.md` is the binding
record and C21's defaults reproduce it.

**Approach.** Census first (`grep` for `IsKeyPressed`, `IsJoyButtonPressed`, `GetAxis`, `IsActionPressed`
under `CSVM/src`), record the real count in "What the data actually ships", then replace each site
with an `InputAction` lookup. One action per existing binding, no renaming and no regrouping, so the
diff is mechanical and reviewable.

**Model recommendation.** medium. Mechanical once A2 exists, but the file is large and the blast
radius is the whole flight model.

**Verify.** `CSVM.Tests/FlightBindingMappingTests.cs`, 76 facts over a fake `IDeviceState` in
`BindingModelTests`'s shape, is what actually proves the migration: every migrated key and pad
button against the action its call site now names, the four signed key pairs and the pad's four
axes in the sign the flight model reads, the snap-look cluster's eight composed directions, the
keyboard/pad splits the tap-hold splitter and the four view-mode edge slots depend on, both ends of
every key pair reading zero rather than one end taking priority, and the two dropped pad routes
asserted still unbound. Run with
`.\RunTests.ps1 -UnitFilter "FullyQualifiedName~FlightBindingMappingTests" -SkipEngine -SkipGoldens`.

`--det` baseline, taken in the item's worktree before the first edit and repeated after it:
`--chapter=C1 --plane=player_bhawk --hold=0.2,0.1,0,1 --det --mute --shots=4 --frames=120` and
`--chapter=C1 --plane=player_bhawk --ai=player_fury --ai-damage=0.02 --fire --det --mute --shots=3`,
both through `RunProbe.ps1`, seven PNGs md5-identical across the pair. The second one holds the gun
trigger, so the fire path is compiled and called rather than merely present.

Complete `.\RunTests.ps1` in the item's worktree: PASS, exit 0, 3214 units, 230 engine suites, 18
goldens hash-identical, 0 build warnings (159.2s total, every stage inside its budget).

**⚠ Traps.** `docs/controls.md:29-33` and `:46` record bindings that are load-bearing beyond the
keymap: `L` is *reserved and deliberately unbound* for Track Target (`BL-399`), and the debug keys
`F13`-`F17` follow the physical rows of the author's keypad rather than a contiguous block. Do not
tidy either while migrating.

## B12 ☑ Migrate `MenuInput`

**Landed.** Twenty-five of `MenuInput`'s controls resolve through `InputContext.Menu`. Three of the
file's rules had no expression in the landed seam and needed design rather than the mechanical
fan-out this item was written as. A menu seat reads a set of pads while a binding names one device,
so a seat-local placeholder identity answers for the set over `Pads.For`. The pad-only twins come
from a second `PlayerActions` with `ReadsKeyboard` false over the same map. Text entry clones the map
and drops every keyboard binding whose key is typeable, which reproduces the alias rule as a rule
rather than a hard-coded list, so it keeps holding after a rebind.

Nine sites stay raw and are meant to. `ScanActivePad` and `JoinPressed` both ask which device
produced an input, and a seat's bindings OR across every pad it holds, so the resolved boolean cannot
say which one fired. That is the same reason C21 leaves `MenuJoin` unbound. The typing sweep has no
named action by design.

**Verified.** `MenuInputBindingTests`, 46 facts. Each migrated control is asserted to resolve its
action **and no other menu action**, which is the wrong-mapping failure B14's gate cannot see.
Complete `.\RunTests.ps1` in the item's worktree, exit 0, 3184 units, 230 engine suites, 18 goldens
hash-identical. B14 later added the `--det` pair this item landed without, over the four probes in
its own section: the menu and freecam shots are md5-identical across this item's before and after
trees.

**Original approach (kept for reference).**

**Goal.** Menu navigation resolves through the same seam.

**Evidence (confidence: traced).** `MenuInput` polls 34 named controls, not 33. Twenty-five are the
nine menu actions and migrate: the keys `Up` `W` `Down` `S` `Left` `A` `Right` `D` `Enter` `KpEnter`
`Space` `Escape` `L` `P`, the pad buttons `DpadUp` `DpadDown` `DpadLeft` `DpadRight` `A` `B` `Start`
`Y` `X`, and the two halves each of `LeftY` and `LeftX` past a 0.5 deadzone. Nine do not: the five
in `ScanActivePad` (`A`, `B`, `DpadUp`, `DpadDown`, `LeftY`) and `JoinPressed`'s `Start` answer
*which pad acted*, and `Shift`, `Backspace` and the 37-key typing sweep have no named action at all.

**Approach.** As B11, scoped to the menu actions. Three readings of one seat cover the file's own
distinctions: the pad-only twins (`PadMove`, `PadMoveX`, `PadBack`) are a seat with
`ReadsKeyboard` false, and `TextEntry`'s dead letter aliases are a map with every binding on a
typeable key dropped (`MenuInput.TypingMap`), which reproduces `AliasDown` and keeps holding after a
rebind. Pad rows sit on a seat-local placeholder identity, `MenuInput.SeatPads`, because a menu seat
reads a set of pads (player 1 holds every unclaimed one) and no binding may store a connection index;
`MenuInput.SeatDevices` answers for it through `CSVM.Pads.For`, keeping the focus and `--no-pads`
gates.

**Model recommendation.** medium, low effort. Mechanical fan-out.

**Verify.** `CSVM.Tests/MenuInputBindingTests.cs`, 46 facts over a fake device state in
`BindingModelTests`'s shape: each of the 25 migrated controls resolves its action *and no other menu
action*, the stick fires strictly past 0.5 and not at it, a pad-only seat reads its pad and none of
the keyboard rows, every typeable key is dead in the text-entry map while the dedicated keys and the
whole pad stay live, `Dir` takes the negative end when both are held, and `MenuJoin` is the only
menu action the seat cannot resolve. Run with
`.\RunTests.ps1 -UnitFilter "FullyQualifiedName~MenuInputBindingTests" -SkipEngine -SkipGoldens`.
Then the complete `.\RunTests.ps1`, since this lands under `CSVM/`.

**⚠ Traps.** Menu input runs outside the fixed tick. Confirm the once-per-tick snapshot in A2 does
not starve it before reusing the same resolver here.

⚠ **The join path stays a raw poll and is not a hole in the migration.** `JoinPressed` and
`ScanActivePad` ask which *device* produced an input, and a seat's bindings OR across every pad the
seat holds, so the answer is unrecoverable from an action. C21 already leaves `MenuJoin` unbound for
this reason; `LastActivePad` is the same question and gets the same treatment.

## B13 ☑ Migrate `SpectatorCamera` and whatever the census turns up

**Landed.** Every polled key and pad read in the freecam resolves through `InputContext.Camera`. The
seven raw `Input` reads are gone from the camera's logic and live only inside the seat's device
state, which is where a seat's hardware read belongs.

The mouse does not move. Free-look there is an `InputEventMouseButton` toggling a field with
`InputEventMouseMotion` doing the pan, the wheel is a discrete event with no held state, and the `F`
and pad `X` target key are events on purpose per `BL-279`, because a polled edge fires behind a host
that already consumed the key. Converting any of them to a poll would change when free-look starts
and stops relative to the frame, which is the inverse of `BL-296`'s trap.

Two `PlayerActions` sit over one shared `ActionMap`, one fed a keyboard-only device view and one a
pad-only view, because this camera gives a key and a stick bound to the same action different rates
and sums them. One merged read would have to pick a rate and would drop the sum, sending keyboard
look fifty per cent faster. The seat placeholder answers for itself and nothing else, so a real
hardware identity from a loaded profile cannot be silently served by the seat's whole pad set.

Three deviations were recorded rather than hidden, and B15 answered two of them: the deadzone rescale
in `Binding.Resolve` is removed, and the orbit dolly has its own actions at deadzone 0 rather than
inheriting the digital boost threshold. Opposed-key edge cases are the third and stand.

**Verified.** `SpectatorBindingsTests`, 29 facts, including that every axis pair here is the
symmetric subtract-both form, so `ActionSnapshot.Axis` is correct for this file where `MenuInput`'s
negative-priority form made it wrong there. Complete `.\RunTests.ps1` in the item's worktree, exit 0,
3166 units, 230 engine suites, 18 goldens hash-identical. B14 later added this item's missing `--det`
pair, md5-identical across its before and after trees on all eleven shots.

**Original approach (kept for reference).**

**Goal.** The last raw polls are gone.

**Evidence (confidence: lead-only).** Named in `BL-296`, plus whatever B11's census finds beyond the
three named files.

**Approach.** As B11.

**Model recommendation.** medium, low effort.

**Verify.** `CSVM.Tests/SpectatorBindingsTests.cs`, 28 facts over a fake `IDeviceState` in
`BindingModelTests`'s shape, is the per-site mapping evidence B14 cannot produce: every key the old
code polled resolves the action that replaced it, each key pair and each stick keeps the sign the
old `Axis`/`PadAxis` helper returned, the shoulders reproduce `PadButtonAxis`, the triggers cross the
same half-travel boost gate, a stick inside 0.18 still reads nothing, every pad default sits on the
seat placeholder rather than on a hardware identity, both ends of a pair read zero (this camera's
pairs are all the symmetric form `ActionSnapshot.Axis` implements, unlike `MenuInput`'s), and no
camera binding is a mouse control. Run with
`.\RunTests.ps1 -UnitFilter "FullyQualifiedName~SpectatorBindingsTests" -SkipEngine -SkipGoldens`.
A repo-wide grep for `Input.IsKeyPressed`, `Input.IsJoyButtonPressed`, `Input.GetJoyAxis` and
`Input.IsMouseButtonPressed` leaves `SpectatorCamera`'s own `SeatDevices` (the seat's
`IDeviceState`, which is where the reads belong) and `Launcher.cs`'s pointer click, which is not an
action. Complete `.\RunTests.ps1` in the item's worktree.

One difference is deliberate and recorded rather than hidden: past the deadzone the seam rescales an
axis onto [0, 1] (`Binding.Resolve`) where `PadAxis` passed the raw travel through, so the freecam's
stick response between 0.18 and full deflection is a ramp from zero rather than a step to 0.18. Rest
and full deflection are unchanged, so the top look and fly rates are the same. The orbit dolly reads
the boost and slow triggers, whose shipped 0.5 threshold now dead-zones the first half of a trigger
that used to dolly from zero; giving the dolly its own action would fix it and is not this item's.

**⚠ Traps.** ⚠ **`SpectatorCamera` does not poll the mouse; it handles `InputEvent`s.** Free-look
there is `InputEventMouseButton { ButtonIndex: MouseButton.Right }` toggling a `_looking` field, with
the pan itself driven by `InputEventMouseMotion`. The census counted its polled key and pad reads,
which are migratable, but the mouse path is not one of them. **Do not convert those events into
polls, and do not convert any polled site into an event.** `BL-296`'s trap is that an event-based
seam breaks scripted `--det` / `--hold` runs, and the inverse swap here would change when free-look
starts and stops relative to the frame. Migrate the polled reads, leave the event handlers alone,
and say in the landing commit which sites were left and why. The wheel-sets-speed and mouse-pick
paths are events too and are equally out of scope.

## B14 ☑ Determinism gate: `--det` / `--hold` reproduce bit for bit

**Landed.** No production code. The item is a gate, and what it delivers is evidence plus this
section's correction of its own Approach.

⚠ **The single before/after across the whole of Wave B, which this item's Approach called for,
cannot be reconstructed and was not taken.** Main advanced with unrelated work while Wave B ran, so
a pre-A1 tree does not isolate the migration. `git diff --name-only be7ed774^ eccea121 -- CSVM/src`
reaches 39 files; the narrower span against B11, B12 and B13's shared parent,
`git diff --name-only af95cd13 eccea121 -- CSVM/src`, still reaches 29, of which 9 are Wave B's.
The rest are m5-polish-9 and m5-polish-10 work:
`a88b617f` rewrites `Weather.cs`, `WeatherRig.cs`, `GameSession.cs` and `Launcher.cs` to light an
aircraft by the brightness its mission authors, which is exactly the imagery these probes shoot, and
is an ancestor of neither `be7ed774^` nor `af95cd13`; `e593be51` re-pins four aircraft goldens on
the same reading; and `AiEngineAudio`, `CameraController`, `MotionRuntime`, `NameResolver`,
`AnimRuntime` and `ZeppelinRuntime` all changed alongside. Any pixel difference over that span is
unattributable, and quoting one as an input-refactor result would be a fabrication.

The confound is visible in the measurements rather than only in the log. Across
`af95cd13` to `eccea121` the two flight probes below differ while the freecam and menu probes are
md5-identical, which is what a rendering change to aircraft imagery looks like and not what an input
refactor looks like. That asymmetry is the reason the span is not quoted as a gate.

**Verified.** Two things were run in place of the missing span, and each answers a question a
per-item pair cannot.

*The merged combination of all five Wave B items is reproducible.* On `eccea121`, twice, with a
`dotnet clean` and a full rebuild between the two passes, four invocations through `RunProbe.ps1`:

- `--chapter=C1 --plane=player_bhawk --hold=0.2,0.1,0,1 --det --mute --shots=4 --frames=120`
- `--chapter=C1 --plane=player_bhawk --ai=player_fury --ai-damage=0.02 --fire --det --mute --shots=3`
- `--freecam --chapter=C1 --det --mute --shots=3 --frames=120`
- `--menu=mode --det --mute --frames=60`

Eleven PNGs, all eleven md5-identical across the two passes. The second invocation holds the gun
trigger, so the fire path is compiled and called rather than merely present. The third and fourth
were added here because B11's pair reaches neither `SpectatorCamera` nor `MenuInput`, so a flight
probe alone would have left B12's and B13's files unrendered. Each argument is quoted at the shell:
unquoted, PowerShell splits `--hold=0.2,0.1,0,1` on the commas and the run never quits.

*B12 and B13 gain the per-item pair they never had.* B11 recorded one (lines above) and B15 re-ran
it, but `4eaf4535` and `b1e2ae4b` landed on a test suite alone. Both are single commits off the same
parent `af95cd13`, each touching exactly one production file, so the pair isolates cleanly. The same
four invocations were run in three throwaway worktrees at `af95cd13`, `4eaf4535` and `b1e2ae4b`: all
eleven PNGs carry one md5 each across all three trees, 33 hashes and 11 distinct values.

Complete `.\RunTests.ps1` in the item's worktree: PASS, exit 0, 3312 units, 233 engine suites, 18
goldens hash-identical, 0 build warnings (174.3s total; the engine stage 105.2s against a 100.0s
budget, which is awareness only).

**⚠ What this gate could not have caught, which is more than what it caught.** Five rules in
`docs/verification.md` bite here, and four of them bite against the result:

- **DET-2** (disable live input during scripted runs) and **DET-6** (scripted probes imply `--det`)
  together are the whole limitation. `--det` bundles `--no-pads` and an unattended run has nobody at
  the keyboard, so every polled read is false in every tree measured above. There is no way to
  strengthen the gate by admitting live input, because dropping `--det` for `--no-det` drops the
  reproducibility the comparison is made of.
- **INSTR-14** (every automated session check runs on a parent-driven clock) narrows it again. All
  four invocations drive the session themselves, so the gate says nothing about the realtime
  `_PhysicsProcess` adapter, which is the only path a human's input ever arrives on.
- **INSTR-33** is the same shape one level up, and is quoted here as the precedent rather than as a
  finding about this item: the mode that makes a capture reproducible is the mode that removes the
  thing under test.
- **DET-8** (`--det` ignores `config.json`) is the one rule that works in the gate's favour. It is
  what makes an md5 comparison across four different worktrees legitimate at all, since each run is
  a function of its committed tree rather than of anyone's local tuning file.

So a migrated site that resolves the *wrong* action, or no action at all, passes every number above
unchanged. `FirePressed()` is still on the scripted path (`FlightController:1679` combines
`AutoFire` with the polled read), so the seam is compiled and called, but the polled half is
constantly false. By the Ground rules' own standard an unchanged number is not evidence unless you
have seen it able to fail, and none of these could have failed on a mapping error.

**What the gate does prove** is narrow and worth having: the migration did not disturb the scripted
path, the fixed tick, the sim, the menu's construction or the freecam's, and the merged combination
of five items is as reproducible as each item was alone.

**What actually proves the migration** is the per-site mapping evidence in the Wave B items' own
Verify lines: `FlightBindingMappingTests` (77 facts), `MenuInputBindingTests` (46) and
`SpectatorBindingsTests` (29), each asserting that an action resolves from the control the old code
polled and, in B12's case, from no other action. Read those before reading this section's numbers,
not after.

**Original approach (kept for reference).**

**Goal.** Proof that the refactor changed no behaviour.

**Evidence (confidence: lead-only).** `BL-296`'s trap states that an event-based seam would break
scripted `--det` / `--hold` runs, which makes those runs the instrument that proves the polling seam
was kept.

**Approach.** A gate, not a change. Take baselines before Wave B starts.

⚠ **That Approach line is wrong and is corrected above.** A baseline taken before Wave B starts is
only a gate if nothing else lands in between, and this repo runs several plans in parallel worktrees
onto one main, so something else always does. The affordable form is the one Wave B items already
used: a pair inside each item's own worktree, where the only delta is that item's change, plus one
reproducibility check on the merged result. Write the per-item pair into the item, not into the
gate.

**Model recommendation.** medium. Reading a diff, not writing code.

**Verify.** The four `RunProbe.ps1` invocations listed above, compared by md5 with `Get-FileHash`,
run twice at merged main across a clean rebuild and once each in the before and after worktrees of
every Wave B item that lacked a pair. Complete `.\RunTests.ps1`.

**⚠ Traps.** ⚠ `docs/verification.md` exists because the instruments here mislead. Read it and cite
the rule that bites before quoting any number as a pass.

⚠ **This gate is necessary and nowhere near sufficient, and it must not be reported as if it were.**
`--det` bundles `--no-pads`, and an unattended run has nobody at the keyboard, so every polled read
returns false in both the before tree and the after tree. A migrated site that resolves the *wrong*
action, or no action at all, passes this gate unchanged. `FirePressed()` is still on the scripted
path (`FlightController:1679` combines `AutoFire` with the polled read), so the seam is compiled and
called, but the polled half is constantly false. By the Ground rules' own standard, an unchanged
number is not evidence unless you have seen it able to fail, and this one cannot fail on a wrong
mapping.

What actually proves a migration is per-site mapping evidence: for each migrated read, a test that
the action resolves from the same control the old code polled, driven by a fake device state. Wave B
items carry that requirement in their own Verify lines. This gate's job is narrower and still worth
running: it proves the refactor did not disturb the scripted path, the fixed tick, or the sim.

## B15 ☑ Reconcile what the migration proved: one seat device state, the axis rescale, the stunt marker

**Landed.** `CSVM/src/Bindings/SeatDeviceState.cs` replaces the three private nested copies in
`FlightController`, `MenuInput` and `SpectatorCamera` (164 lines deleted between them). It takes the
seat's placeholder identity and a `Func` supplying the seat's pad list, because two of the three
seats change that list after construction, ORs buttons and takes the largest magnitude for axes
across `Pads.For`, and snapshots once per tick. The pad half is gated and the keyboard and mouse
halves read `Input` directly, per the asymmetry above.

The axis rescale is gone: `Binding.Resolve` returns raw travel past the deadzone. Verified against
the pre-migration code, whose `PadAxis` was `Mathf.Abs(best) < PadDeadzone ? 0f : best`, so a stick
at 0.508 on the camera's 0.18 deadzone reads 0.508 again rather than the rescale's 0.400. A1's
invariant is untouched, since a below-deadzone read is still `None`.

`CameraDollyOut`/`CameraDollyIn` are their own actions on the triggers at deadzone 0, so the dolly
covers the same travel it used to, while boost and slow keep their 0.5 gate on the same two axes.
`CycleStuntTarget` sits on `D-pad Up` beside `TargetNextEnemy` through `ActionMap.Add`, and
`docs/controls.md`'s stunt-cycle row gains its pad column.

**Verified.** All three migration suites still pass, `--det` re-run image-identical across both of
B11's invocations (seven PNGs, md5-identical), complete `.\RunTests.ps1` PASS exit 0 at 3312 units,
233 engine suites, 18 goldens hash-identical.

⚠ **Four tests changed, and the plan predicted one.** Each pins an intended change rather than
accommodating an accident, and the suite named all four before any was touched: B13's rescale test
inverted as expected; `BindingModelTests.AnAxisAtPointFour…` also pinned the rescale, which the plan
missed, so A1's own suite was asserting the rule being removed; `FlightBindingMappingTests`'s
dropped-pad-route fact asserted `CycleStuntTarget` held no joypad binding at all and narrowed to
"not pad `X`", which is what it was really protecting; and `DefaultBindingsTests`'s one-control-one-
action rule took a named list of deliberate sharers rather than only the numpad diagonals.

⚠ **A third alias class exists now**, created by the dolly change and recorded for D31 alongside the
other two: `Axis(TriggerRight, +1, 0.5)` and `Axis(TriggerRight, +1, 0f)` are two `Binding` values,
since deadzone is part of equality, but one control under `SameControl`, which ignores deadzone. Two
camera actions therefore share a trigger, reachable through `Assign` and `OwnersOf`.
**D31 left this one alone deliberately.** The comparison is right as it stands, and the screen names
both owners before taking the trigger from either, so the pair breaks only when a player asks for it.

**Correction to this item's own Evidence.** Calling `GodotDeviceState`'s missing `Pads.For` gate a
live determinism hole overstated it. A repo-wide grep shows no polling site has ever used that type,
because all three migrations wrote private copies precisely to avoid it. It was a latent hazard for
the next site rather than a `--det` hole in anything shipped, and the consolidation closes it by
making the shared type the only thing a site would reach for.

**Original approach (kept for reference).**

**Goal.** The three things Wave B each had to work around privately become one answer in
`CSVM/src/Bindings/`, the two behaviour deviations B13 recorded are undone, and the stunt marker
sits where the user put it.

**Evidence (confidence: traced).** Every item below was found by a migration that had to route
around it rather than by inspection, and each is named in a landed commit.

**Approach.** Six changes, in this order:

1. **One seat-scoped `IDeviceState`.** B11, B12 and B13 each wrote a private one, because
   `GodotDeviceState` resolves one `DeviceId` to one connection index while a seat reads a *set* of
   pads, and because it reads `Input.GetJoyAxis` and `IsJoyButtonPressed` with **no `Pads.For`
   gate**, so a site on it loses `--no-pads` and the window-focus gate. Since `--det` implies
   `--no-pads`, that is a determinism hole rather than a tidiness point. Replace all three with one
   type taking the placeholder identity as a constructor argument, since `menu-seat`,
   `spectator-seat` and `AnyPad` are the same species. It ORs buttons and takes the largest
   magnitude for axes across `Pads.For(seat)`, and snapshots the seat's pad list once per tick,
   because `Pads.For` re-reads the roster on every call.
   ⚠ **Gate the pad half only.** Godot polls joypads globally regardless of window focus, so a
   drifting stick reaches a run that owns no window, while keyboard reads are focus-scoped and every
   scripted run sits on its own desktop. That asymmetry is why `--no-pads` exists with no
   `--no-keyboard` counterpart, and a uniform gate would either under-protect pads or add a keyboard
   gate nothing needs.
2. **Remove the axis rescale.** `Binding.Resolve` maps travel past the deadzone onto [0, 1], and no
   existing polling site rescales, so keeping the deadzone number did not keep the behaviour: with
   the camera's 0.18 deadzone a stick at 0.508 reads 0.40. Return the raw travel instead. A1's
   invariant survives untouched, because a below-deadzone read still returns zero, so `Pressed`
   still holds exactly when `Value` is above zero. Invert B13's test that pins the rescale.
3. **The orbit dolly gets its own action.** It inherited `CameraBoost`'s 0.5 digital threshold, so
   the first half of trigger travel no longer dollies and the second half doubles in slope.
4. **`CycleStuntTarget` moves to `D-pad Up`**, beside `TargetNextEnemy`, and keeps `Tab`. It sat on
   pad `X`, undocumented and colliding with `Nitro`, and B11 dropped it, leaving a pad-only stunt
   pilot unable to cycle the marker. The user's call, and the reasoning is that a stunt target *is*
   an objective marker, so cycling one is the objective cycle. Two actions deliberately share the
   control through `ActionMap.Add`, the shape C21 used for the snap-look diagonals. `BL-686` carries
   the real fix, and its constraint holds here too: stunt markers are per player and never shared.
5. **Sanction the multi-reader pattern.** B11 and B13 both ran several `PlayerActions` over one
   shared `ActionMap` (full, keyboard-muted, pad-muted), because the keyboard and pad halves of one
   action take different processing and are then *summed*, not ORed: a `StickRamp` ramp against a
   `StickCurve` curve in flight, and `KeyLookRate` 1.6 against `PadLookRate` 2.4 in the camera. One
   merged read would have to pick a rate and would drop the sum. Two of three migrations
   independently needed it, so document it as the shape rather than leaving it to be rediscovered.
6. **Correct `ActionSnapshot.Axis`'s doc comment.** It claims both ends held reads zero "as a key
   pair does today", which is true of `FlightController` and `SpectatorCamera`, both confirmed pair
   by pair, and false of `MenuInput`, whose `up ? -1 : down ? 1 : 0` gives the negative end priority.
   That is why B12 wrote its own `Dir`. Say which is which.

**Model recommendation.** high. It changes a resolve rule every migrated site now depends on.

**Verify.** `CSVM/src/Bindings/SeatDeviceState.cs` replaces the three private copies, and the three
migrations' suites still pass: `FlightBindingMappingTests` 77, `MenuInputBindingTests` 46,
`SpectatorBindingsTests` 29. Four facts moved, each pinning a change this item makes rather than
accommodating one it did not intend, and every other fact in the three suites is untouched:

- `SpectatorBindingsTests`'s rescale fact inverts, and now reads the raw 0.508 the old `PadAxis`
  returned instead of the 0.40 the rescale produced.
- `BindingModelTests`'s A1 fact pinned the same rule one level down (0.6 travel past a 0.5 deadzone
  read 0.2) and now reads 0.6.
- `FlightBindingMappingTests`'s dropped-pad-routes fact narrows from "no pad binding at all" to "not
  pad `X`, which stays Nitro's", since the stunt cycle gains d-pad up.
- `DefaultBindingsTests`'s one-control-one-action fact takes a list of the shipped shared controls
  rather than the four snap-look diagonal keys alone.

Two facts are new: the dolly reading the whole of a trigger's travel on its own action while the
boost gate stays at half, and d-pad up firing both the stunt cycle and the target cycle.

B11's `--det` image comparison, both invocations, baselined in this item's worktree before the first
edit and repeated after the six changes: seven PNGs md5-identical across the pair, the trigger-held
run included. Complete `.\RunTests.ps1` in the item's worktree: PASS, exit 0, 3312 units, 233 engine
suites, 18 goldens hash-identical, 0 build warnings (169.5s total; the engine stage 3.6s over its
budget, which is awareness only).

⚠ **The `--det` pass is weak evidence here for the reason B14 states**, and the arithmetic is what
actually stands behind the rescale removal: a stick at 0.508 through the camera's 0.18 deadzone read
`(0.508 - 0.18) / (1 - 0.18) = 0.40` and now reads 0.508, which is what `PadAxis` passed through.
`--det` implies `--no-pads`, so no axis moves in either tree and the resolve rule cannot fail there.

**⚠ Traps.** ⚠ **Do not widen `ActionMap.SameControl` to fix aliasing.** Two aliases are recorded
now: `Hat(0, Up)` against `Button(DpadUp)`, and a saved real-GUID binding against a `pad:*` default,
which a seat device state resolves to the same physical button while `SameControl` calls them
different controls. Both are D31's to resolve at capture and assign time, and widening the comparison
would bake device and button numbers into it.
⚠ `PlayerActions.ReadsKeyboard` now silences the mouse too, so it names three devices. Renaming it is
tempting and is a separate change; do not fold it in here.

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

## C22 ☑ `docs/controls.md` becomes the shipped-defaults record

**Landed.** `docs/controls.md`'s opening states that the listed keys and buttons are the shipped
defaults, that the set is data in `DefaultBindings.cs` as one `ActionMap` per `InputContext`, that
per-player rebinding is being built and these are the starting point it resets to, and that the
debug overlays and lab panels sit outside the action set on purpose. It also states the per-context
rule with the three controls that prove it (`W`, `P`, `Space` each mean different things in flight,
on a board and in the free camera), because a reader who takes the page as one flat keymap reads
those three rows as contradictions.

Six places where the page and the shipped table disagreed, resolved by reading both:

1. **The `I` row claimed a pad default the table does not hold.** `TargetNearest` is keyboard `I`
   alone; the pad reaches it by holding d-pad up past 250 ms, which is `TargetNextEnemy`'s binding
   dispatched by hold length inside the consumer. The code is right, and the page said "D-pad ↑
   (hold 250 ms)" in a column that otherwise means "this action's pad binding". Both target rows now
   say that d-pad up is the pad's one targeting binding and where the split happens.
2. **The menu table hid its own defaults**, because it was written with two columns and several rows
   carried three cells, so a renderer dropped the text after the pad letter. It now has an `Input` /
   `Pad` / `Does` shape like the flight table.
3. **Four menu defaults were undocumented**: the `W`/`S`/`A`/`D` aliases beside the arrows, numpad
   `Enter` beside `Enter`, the loadout key (`L`, pad `Y`), and the contents list (`P`, pad `X`). The
   code is right: they are real shipped bindings a player can press today.
4. **`MenuLeft`/`MenuRight` had no row at all**, so the horizontal steppers on a board were
   undocumented. The code is right.
5. **The freecam table omitted boost and slow** (`Shift`/`Ctrl` and the two triggers past half
   travel), which is the whole reason `CameraDollyIn`/`CameraDollyOut` are separate actions at
   deadzone 0. The code is right, and both readings of the trigger pair are now stated together.
6. **The chase-camera zoom (`numpad +`/`−`) is not a bindable action**, and the page listed it beside
   rows that are. `CameraController.UpdateZoom` and `FlightController.OrbitInput` poll the two keys
   directly. That is deliberate (the weapon-lab orbit sums two key pairs, which an action read cannot
   express) and the row now says the pair is outside the default table.

**Verified.** Every row of the page read against `DefaultBindings.cs` binding for binding, in all
three contexts. Flight's 35 actions, Menu's 10 and Camera's 15 each have a row or a stated reason not
to. Three things that look like discrepancies and are not, checked and left alone: the numpad snap
cluster's `Kp1`/`Kp2`/`Kp3` drive `LookDown` while the page calls `Kp2` "Look Back", which agrees
because `HeadLook.SnapTargets` reads the pair as a compass direction rather than a pitch, so a held
`Kp2` resolves to dead astern at level; `CycleStuntTarget` on d-pad up beside `TargetNextEnemy`, both
documented and both shipped; and the `F13` onward debug block, whose ordering follows the physical
rows of the author's keypad and is not a mistake to tidy.

This item touches only `docs/`, so the complete `.\RunTests.ps1` landing gate does not apply and was
not run. `.\CheckCommitContent.ps1` passes against the item's worktree.

**Original approach (kept for reference).**

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

## D31 ☑ The rebinding screen: capture, assign, steal-from-previous-owner

⚠ **D35 replaced this item's edit model, and the paragraphs below describe the model it replaced.**
This item edited the live `ActionMap` in place, so a rebind was felt on the next frame in the menu
context and `Discard()` dropped only the capture and the status line while the binding change stood;
`Save()` wrote on Back and `ResetContext()` reset one context. D35 stages every edit in a working
copy, commits on Accept and abandons on Cancel, which is the original's model, and adds the
whole-map reset. Read **D35** for what the screen does now. Everything else here still holds: the
capture, the steal rule, the four inherited defects and their decisions, the row limit, and the
per-seat routing.

**Landed.** Three new files and one screen. `CSVM/src/Bindings/ControlCapture.cs` is what a screen
may capture and the release-first scan that turns a press into a `Binding`;
`CSVM/src/Bindings/BindingLabels.cs` is what the screen prints, including the row that counts what
it is not showing; `CSVM/src/UI/Menu/ControlsFeature.cs` is the shared engine-free
`IMenuFeature` holding the seats, the row and slot cursors, the capture in progress and the steal it
is about to perform. The built-in launchscreen draws it as `Screen.Controls`, reached from a new
Options row and from `--menu=controls`. Three landed files changed: `ActionMap` (`Assign` returns a
list and `TryFindOwner` becomes `OwnersOf`), `MenuInput` (its seat device state, its live map and a
`RebindsApplied` that rebuilds the text-entry reading after a rebind), and the two host wirings that
register the feature. Nothing else under `CSVM/src/Bindings` moved.

**The menu framework (the first `<TODO>`).** The rebinding logic is a shared feature in
`CSVM.UI.Menu` and the screen that offers it is the presentation's, which is the split
`docs/menu-presentations.md` defines and `PlayerSetupFeature` and `HangarFeature` already follow. The
alternatives were both worse: a screen built only inside `LaunchMenu` would have to be written a
second time for Original, and a screen built only inside `OriginalShell` would need the decoded
layout to exist before a player could rebind anything. Built-in draws it here; Original's
`PF_B_CONTROLS` is a decoded, currently disabled door on its Preferences page and is where the same
feature goes there, which this item does not build.

⚠ **The capture deliberately does not come through `MenuCommands`, and that is the one place this
screen steps outside the seam.** `IMenuInputSource` is device-neutral by contract, and a rebinding
screen's whole subject is the raw control. Navigation still goes through the seat's semantic
commands like every other screen; the capture reads the seat's own `IDeviceState`, newly exposed as
`MenuInput.Devices`. A capture in progress swallows the whole frame, so the press being bound cannot
also walk the cursor and confirm the row under it.

**The four inherited defects, one decision each.**

1. **`Assign` steals from the first owner only: fixed in `ActionMap`.** It now returns every action
   that lost the control, in enum order, and `TryFindOwner` is replaced by `OwnersOf`, which returns
   the same list. There is no single-owner form left, on purpose: the state is reachable rather than
   theoretical (C21 puts each numpad snap-look diagonal on two actions and B15 puts d-pad up on
   `TargetNextEnemy` and `CycleStuntTarget`), and any caller taking the first owner would report one
   loss and perform two. The screen is the only thing that calls `Assign`, so the fix is invisible
   to the sim.
2. **The Hat/Button d-pad alias: correct as it stands, no change.** C21 closed it by authoring every
   d-pad default as a `Button` and making `BindingStore` reject a hat token, so the two encodings
   never coexist. `ControlCapture` completes that from the other end: it scans buttons and never
   hats, so a capture cannot introduce the alias either. Widening `SameControl` would bake the
   d-pad's button numbers into the comparison, which B15's traps forbid. A fact in `ActionMapTests`
   pins the two as different controls so the decision cannot be quietly reversed.
3. **The `pad:*` placeholder against a real GUID: handled in the screen, not in `SameControl`.**
   Every seat in the game reads a set of pads through a placeholder identity today
   (`DefaultBindings.AnyPad` in flight, `menu-seat`, `spectator-seat`), so a capture that read raw
   hardware would produce a real GUID that the seat's own device state cannot resolve and that the
   steal rule reads as a different control. `ControlCapture` therefore never invents a device
   identity: it is constructed with the identity that context's bindings are already authored on and
   stamps captured pad controls with it. C21's and B13's rule that a placeholder answers only for
   itself is untouched, and `SameControl` is untouched.
4. **The trigger-deadzone alias: correct as it stands, no change.** `SameControl` ignoring the
   deadzone is A2's deliberate call, and it is what makes re-binding an already-bound axis adjust it
   in place rather than stack a copy. B15's double-binding of `TriggerRight` survives because the
   screen never calls `Assign` on it unasked. What the screen adds is that both owners are named:
   binding that trigger to a third action reports "Camera Boost and Camera Dolly Out lost it", so
   the player sees the pair before agreeing to break it. Fixing this in `SameControl` would have to
   choose between breaking B15's pair and letting a player silently hold one trigger on three
   actions, and neither is the screen's call to make behind their back.

**The steal is shown, not performed.** A capture that lands on a free control binds it and says so.
A capture that lands on a held control raises `Pending`, naming every action that would lose it, and
moves nothing until the player confirms; Back leaves every action's controls exactly where they
were. The commit message then names what actually happened, from `Assign`'s own return rather than
from the preview.

**No binding is hidden.** A row prints up to four controls, which is the longest row the shipped set
holds, and appends "+N more" beyond that, so a player can always tell a two-binding action from a
five-binding one. `NoShippedActionHidesABindingAtTheRowsOwnLimit` asserts nothing ships hidden. The
slot cursor walks every binding an action holds, the empty slot past the last one adds a control, and
L/Y drops the one under the cursor.

⚠ **Nothing in the game reads a saved keymap yet, and this item does not change that.** `FlightController`,
`SpectatorCamera` and `MenuInput` each build `BindingProfile.Defaults(...)` in their own constructor,
and `BindingStore` is called from no polling site. So the Menu context is live (the screen writes
into `MenuInput.Map` itself, the object seat 0's readers hold, so a menu rebind is felt on the frame
after Accept; before D35 it was felt on the frame after the capture) while Flight and Camera are
edited and saved but not yet consumed. Closing that gap means a
launch-time load with a `--det` gate, since DET-8 makes a scripted run a function of the committed
tree and a user's saved keymap would break exactly that; it also means re-entering
`FlightController`'s constructor, which the plan flags. It is a successor item's, and it is stated
here rather than left to be found at the controls.
**Resolved by D34**, which puts all three sites on `LaunchBindings` behind a gate `--det` and
`--run-tests` shut, so every context is read at launch and a scripted run still reads nothing.

**D32 is unaffected either way.** `ControlCapture` scans keys, pad buttons and mouse buttons and
returns `Binding?`; adding an axis arm means adding a movement rule (a rest baseline plus a travel
threshold, per its own trap about a drifting stick) and a fourth loop, with no change to
`ControlsFeature`, which only ever sees a `Binding`.

**Verified.** Three suites, 47 facts, each asserting a specific resolution.

- `CSVM.Tests/ControlCaptureTests.cs`, 8 facts: a fresh key press captured as a keyboard binding, a
  control held when the capture armed not captured until released and pressed again, a pad button
  stamped with the seat's identity while another pad's button is ignored, a fully moved axis and a
  reported hat captured as nothing at all, Escape and pad B cancelling rather than binding, an
  Escape still held from opening the capture not cancelling it, a pad-only seat capturing no key and
  no mouse button but still capturing its pad, and a mouse button past the pointer's own landing on
  the one mouse.
- `CSVM.Tests/ControlsFeatureTests.cs`, 17 facts: the free-control bind and its status line, the
  held-control preview naming both owners of d-pad up while moving nothing, confirm taking it from
  both, discard leaving both alone, a rebind on player 2 leaving player 1's map untouched, the slot
  cursor replacing rather than adding, the empty slot adding rather than replacing, an unbind
  touching no other action, a reset restoring the defaults in the very map the polling site holds,
  one context's edit leaving the other two alone, the save being the player's own and running once
  per change, two edited seats both written by one save (the dirty mark is per seat, or stepping the
  Player row would drop the first seat's work), a captured pad control carrying that context's own
  pad identity, a capture stopping to
  name the owner rather than binding, no shipped row hiding a binding, a long row counting what it
  hides, and `Discard` dropping the capture and the pending steal but not the keymap.
- `CSVM.Tests/ActionMapTests.cs` gains 5 facts and now holds 22: a control on two actions taken from
  both and both named, the loser list in enum order whatever order the map was filled, each loser
  keeping its other bindings, the two deadzones of one trigger naming both owners, and a hat
  direction and the d-pad button staying different controls. Three existing facts moved because
  `Assign`'s return became a list, and one because `TryFindOwner` became `OwnersOf`; none of them
  loosened.

Run the three with
`.\RunTests.ps1 -UnitFilter "FullyQualifiedName~ControlsFeatureTests|FullyQualifiedName~ControlCaptureTests|FullyQualifiedName~ActionMapTests" -SkipEngine -SkipGoldens`.

Complete `.\RunTests.ps1` in the item's worktree: PASS, exit 0, 3342 units, 233 engine suites, 18
goldens hash-identical, 0 build warnings (172.5s total; the engine stage 105.9s against a 100.0s
budget, which is awareness only). `menu-original-tracer` now walks into the Controls screen and back
out of it, so the screen is constructed, drawn and left in engine rather than only in unit tests;
that suite and `menu-launch-return` both gained the Options screen's fourth row.

⚠ **What the automated evidence cannot reach.** No suite presses a physical key or pad button:
`--det` implies `--no-pads` and every scripted run is unattended (B14's own limitation, `DET-2`,
`DET-6`, `INSTR-14`). The capture suites drive a fake `IDeviceState`, which proves the rule and not
the wiring from `MenuInput.Devices` to it. **Owed at the controls, and the author's to judge:**

1. Open Options, then Controls, on a keyboard. Walk the list, rebind one flight action to a free
   key, and read the status line. Does the screen say what it did in words you would use?
2. Rebind an action onto `D-pad Up` with a pad connected. The screen must name both Target Next
   Enemy and Cycle Stunt Target before taking it, and Back must leave both alone.
3. Rebind a menu action (say Menu Loadout) and confirm the launchscreen answers the new control
   immediately, without leaving the screen. This is the one context that is live today.
4. In splitscreen, join a second pad at aircraft select, come back to Controls, step the Player row
   to 2, and rebind something. Player 1's rows must not change when you step back, and player 2's
   seat must show a pad-only device with the keyboard rows unreachable.
5. Press P/X to restore defaults and confirm the rows come back to `docs/controls.md`'s table.
6. The look and the fit: two-column rows at 14 visible, the "+N more" tail, and the footer's press
   list. `ControlsLabelEms`, `ControlsValueEms`, `ControlsExtraWidth` and `ControlsWindow` are TUNE
   and were measured against the longest shipped row, not judged at the controls.

**Original approach (kept for reference).**

**Goal.** The player opens a screen, picks an action, presses a control, and that control is now
bound to it.

**Evidence (confidence: lead-only, with a traced conflict rule).** The conflict semantics are
decoded: reassigning a control clears it from its previous owner and two actions cannot share one
(`docs/org/input.md`, `FUN_005371d0`, `FUN_00535fb0`). The screen itself has no precedent in our
codebase. The menu framework is the shared-feature-plus-presentation split of
`docs/menu-presentations.md`: an engine-free `IMenuFeature` in `CSVM.UI.Menu` holding the model, and
one screen per presentation offering it, built-in's here and Original's behind its decoded
`PF_B_CONTROLS` door.

**Approach.** A capture mode that reads the next control from any device, resolves it to a
`Binding`, and assigns it. Show the steal explicitly (name the action losing the control) rather
than performing it silently.

**Model recommendation.** high. UI plus conflict semantics plus per-player routing.

**Verify.** In splitscreen, with a second pad joined at aircraft select: open Controls, step the
Player row to 2, rebind one action, then step back to player 1 and confirm that action's row is
unchanged. Player 2's seat is pad-only, so its keyboard rows must stay unreachable to a capture
while remaining listed.

**⚠ Traps.** The original ships four slots per action and shows the first two non-empty
(`FUN_00449fc0`), which means its screen *hides* bindings. Ours holds a list, so the screen must
show all of them or say how many it is not showing.

## D32 ☑ Binding an axis or a hat to a digital action

**Landed.** `ControlCapture` gains an axis arm and nothing else moved. Three constants, a fourth
loop over Godot's SDL axis range, and one mask set: `RestBand` 0.25, `MoveThreshold` 0.6,
`CapturedDeadzone` 0.5, ordered `RestBand` < `CapturedDeadzone` < `MoveThreshold` on purpose. The
axes are scanned after the keys, the pad buttons and the mouse, so a button press in the same frame
beats a stick a thumb is resting on.

**The drift rule, which is the release-first mask written for a control that has no release.** A
button is masked while held and unmasked when it goes up. An axis is masked while it is outside the
rest band and unmasked only by being seen inside it, which is the one place a mask is dropped. So an
axis already deflected when the capture armed cannot be captured until it centres, and a stick that
drifts a few per cent off centre never leaves the band and is therefore never a candidate at all. A
travel past `MoveThreshold` on an unmasked axis is the player's answer, with the sign taken from the
direction moved.

**The deadzone stamped on the binding is a constant, not the travel the capture saw.** A crossing is
a moment in a movement and the value it reports depends on how hard the player shoved, so binding
the observed travel would give two players two different keymaps for the same gesture. The stored
number is `CapturedDeadzone`, which sits below the movement threshold, so the control the player
just bound fires on a smaller push than the one that bound it.

**D31's "no change to `ControlsFeature`" claim held, and was checked rather than assumed.**
`ControlsFeature` contains no `ControlKind` at all: it takes a `Binding` from `ControlCapture.Poll`
and hands it to `Offer`, and every downstream step (`SameControl`, the steal, the staged commit,
`BindingLabels.Describe`, `BindingStore`'s `axis:` token) was already written for all five kinds by
A1, C21 and D31. The item is one file plus its tests.

**Hats stay out, and the alias stays closed from both ends.** Godot reports no raw hat, so a d-pad
direction arrives at `ControlCapture` as `JoyButton.DpadUp` and is captured as the `Button` C21's
defaults already author. Making hats capturable would have produced the second encoding C21 and D31
closed off, for no control a player cannot already reach.

**The double-bound trigger behaves, and here is what it does.** `SameControl` ignores the deadzone,
so a captured `TriggerRight+` at 0.5 is the same control as B15's boost binding at 0.5 and its dolly
binding at 0. The screen names both owners, moves nothing, and takes it from both on confirm. It
does not stack a third reading of one trigger, which is the property A2's deadzone-blind comparison
exists to give.

**Thresholds are TUNE and are recorded as such.** `BL-693` carries all three constants, the ordering
rule, what to judge at the controls, and the warning that the deadzone alias is not something these
numbers can be tuned around. The original cannot bind an axis to a command at all, so there is
nothing to decode and nothing to match.

**Verified.** `CSVM.Tests/ControlCaptureTests.cs`, 14 facts (7 new, replacing the one that asserted
an axis captures nothing): an axis idling anywhere inside its drift captures nothing, an axis past
the rest band but short of the movement threshold captures nothing either, a decisive move captures
the right axis with the sign it moved and the constant deadzone, an axis deflected when the capture
armed has to centre before it can be captured, a button beats a resting stick in one frame, a d-pad
direction captures as its button while a reported hat captures nothing, and a captured axis bound to
an action designed as a button resolves the boolean that action expects through `PlayerActions`.
`CSVM.Tests/ControlsFeatureTests.cs` gains one and now holds 25: a captured right trigger names both
Camera Boost and Camera Dolly Out whatever deadzone each holds, and the confirm empties the dolly
while leaving boost its key. `ActionMapTests` is unchanged at 22.

Run the three with
`.\RunTests.ps1 -UnitFilter "FullyQualifiedName~ControlsFeatureTests|FullyQualifiedName~ControlCaptureTests|FullyQualifiedName~ActionMapTests" -SkipEngine -SkipGoldens`.

Complete `.\RunTests.ps1` in the item's worktree: PASS, exit 0, 3356 units, 233 engine suites, 18
goldens hash-identical, 0 build warnings (204.0s total against a 180.0s budget; the engine stage
124.9s against 100.0s and the goldens 54.2s against 50.0s, which is awareness only).

⚠ **What the automated evidence cannot reach, and is owed at the controls.** No suite moves a
physical stick: `--det` implies `--no-pads` and every scripted run is unattended (`DET-2`, `DET-6`,
`INSTR-14`). The suites drive a fake `IDeviceState`, which proves the rule and not the feel. **The
author's judgement, not the implementer's:**

1. With a pad connected, open Controls and bind a flight action to a stick direction. Does 0.6 read
   as a decisive push rather than a nudge, and does 0.25 forgive the centre your pad actually rests
   at?
2. Bind another to a trigger, then fly it. At 0.5 the bound half has to feel like a button, on and
   off, with no dead patch that reads as a broken binding.
3. Push a stick, hold it, and open a capture while it is still over. It must refuse until you let go
   and centre, which is the drift rule doing its job and could read as an unresponsive screen.
4. Press a d-pad direction into a capture and confirm the row prints the pad button, not a hat.

**Original approach (kept for reference).**

**Goal.** A player can bind a trigger or a stick direction to an action that was designed as a
button, and it works.

**Evidence (confidence: direction-sound).** A1 makes it expressible; the threshold and deadzone
defaults are TUNE, not decoded. The original cannot express this at all, so there is nothing to
match.

**Approach.** Capture mode recognises an axis crossing its deadzone as a binding candidate, with the
sign taken from the direction moved. Defaults for deadzone and the digital threshold go to
`backlog.md`'s TUNE list rather than being asserted here.

**Model recommendation.** medium.

**Verify.** Facts in `ControlCaptureTests`'s shape over a fake `IDeviceState`, each asserting a
specific resolution: a resting axis captures nothing, a moved axis captures with the right sign, an
axis released and re-moved behaves, a d-pad direction captures as a button, and a captured axis on
an action resolves to the boolean the action expects. Then the complete `.\RunTests.ps1`. The feel
of the movement threshold at a real stick is the author's and is listed above.

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

⚠ **This item lands last, after D34.** `BL-398` asks for a rebindable keymap and `BL-296` for a
per-player one. Both are true in the menu context alone until the saved map is read at launch, so
closing them before D34 would retire the items against a third of the feature.

## D34 ☑ The saved keymap is read at launch, so a flight rebind is felt

**Landed.** One new file, `CSVM/src/Bindings/LaunchBindings.cs`, is where a seat's keymap comes from
when the seat is built: the player's saved file, or the shipped defaults. The three polling sites ask
it instead of calling `BindingProfile.Defaults` for themselves, so there is one place the read is
gated and one place to look when a rebind is not felt. `Launcher` calls `Configure` before the first
seat exists, and the gate is shut until it does, so a host that never configures gets the shipped set
rather than somebody's file.

**A seat is put on the loaded map through `ActionMap.Fill`, which replaces a map's contents in place
rather than swapping the reference.** That is not a refinement: every migrated site hands one
`ActionMap` to two or three `PlayerActions` (B11's three readers, B13's two, B12's typing clone), so
a load that returned a new map would leave every one of those readers on the map the seat was
constructed with. It is the same property D35 kept for `Accept`, for the same reason, and `Fill` uses
`Add` rather than `Assign` because the shipped set deliberately puts one control on two actions.

**Where each seat gets its player number**, which the constructors do not know:

- `FlightController` loads from `Bind`, after `PlayerIndex` is assigned and only when
  `IsHumanPiloted`. An AI rig reads no player's file, which matters because a mission builds dozens
  of them.
- `MenuInput` gains `LoadSavedKeymap(player)`. Seat 0 is player 1 and loads in `Launcher`; a
  splitscreen seat loads in `MenuSeatDevices` after the join, which is what decides its number.
- `SpectatorCamera` loads player one's camera context in its constructor. The free camera has no
  seat of its own.

**`LaunchMenu.ControlsProfile` now opens the screen on the same file.** It built the Flight and
Camera maps from the shipped defaults, which was correct while nothing read them and is a defect the
moment something does: the screen would show rows the player never chose, and Accept would write
those back over their saved ones.

**The corruption answers, each one a fact rather than a reading of the code.** (a) A file that is not
valid JSON, which is what a truncated or half-written save looks like, leaves every action of every
context at its shipped default and throws nothing; `Load` catches the IO failures on top of that.
(b) A valid file naming an action this build no longer has costs that row and nothing else: every
other row the file carries is kept, and every action it does not name stays at its default. (c) A
valid file binding nothing to a fire action loads that action unbound, because an empty row is C21's
deliberate unbind and the screen is the only thing that writes one. The seat stays flyable, which the
fact asserts beside it. The way back from a keymap a player regrets is Reset to default on the
screen, or deleting `bindings_p<N>.json` under `user://`.

⚠ **The one hazard this item creates and does not close.** Before it, an unbind of a menu control was
lost on the next launch; now it persists, so a player who unbinds Menu Accept in every context and
leaves keeps a launchscreen they cannot confirm anything on. Deleting the file is the recovery and
the screen offers no other. Whether the screen should refuse to leave an action unbound is D31's
judgement, not this item's, and is stated here rather than left to be found at the controls.

**Verified.** `CSVM.Tests/LaunchBindingsTests.cs`, 9 facts, and the `bindings-launch-load` engine
suite, 8 checks over a real `FlightController`.

*The gate is the item's whole risk, so its facts are built to fail.* Both `DeterministicRun_Ignores…`
and `TheGateIsTheOnlyDifference…` write a profile that genuinely differs from the shipped set,
through the real serializer, into a real file the real reader opens; the first asserts the
deterministic read does not carry the rebind and matches the defaults action for action across all
three contexts, and the second reads the same file twice, once with the gate open and once shut, and
asserts the two answers differ. Deleting the `!deterministic` term from `Configure` fails exactly
those two and nothing else, which was run rather than assumed. The engine suite repeats the pair
against a `FlightController` that has been through its own `Bind`.

*The rest.* A stored profile reaches a flight seat's live map; a human rig's own construction loads
the file while an AI rig built with the same index does not; player two's file leaves player one's
seat alone, at the seam and again over two live seats; the three corruption cases above; the menu
poller loading into the very map object its readers hold; and `Fill` replacing rather than merging
while keeping a control that drives two actions. Run the units with
`.\RunTests.ps1 -UnitFilter "FullyQualifiedName~LaunchBindingsTests" -SkipEngine -SkipGoldens` and
the suite with `.\RunTests.ps1 -Suite bindings-launch-load -SkipUnits -SkipGoldens`.

Complete `.\RunTests.ps1` in the item's worktree: PASS, exit 0, 3359 units, 234 engine suites, 18
goldens hash-identical, 0 build warnings (182.7s total against a 180.0s budget, and the engine stage
113.2s against 100.0s, which is awareness only).

**The `--det` probes B14 names are unchanged.** The four invocations in that section, run in a
throwaway worktree at this branch's parent and again in the item's own worktree: eleven PNGs, all
eleven md5-identical across the pair. Each argument is quoted at the shell, and each invocation
carries a `--screenshot=` path, without which `--shots` never quits.

⚠ **What no automated evidence here can reach.** A `--det` run with a real file present at
`user://` was not measured, because the gate's whole point is that such a run reads nothing, and
because writing a keymap into the author's own profile directory is not a suite's business. More
generally the B14 limitation applies unchanged: `--det` implies `--no-pads`, every scripted run is
unattended, and no suite presses a key (`DET-2`, `DET-6`, `INSTR-14`). **Owed at the controls, and
the author's to judge:**

1. Open Options, Controls, rebind a *flight* action (say Nitro) to a free key, Accept, quit, relaunch
   and fly. The new key must fly the aeroplane and the old one must not.
2. Rebind a menu action and confirm it survives a restart as well as the frame after Accept.
3. In splitscreen, rebind on seat 2, restart, and confirm seat 1's keymap came back unchanged.
4. Whether the load ships enabled at all. It is built enabled;
   `LaunchBindings.ReadSavedKeymaps = false` is the one-line switch that leaves every seat on the
   shipped defaults while the screen still edits and saves.

**Original approach (kept for reference).**

**Goal.** A control rebound on the screen is the control that flies the aeroplane, in the next
session and in this one.

**Evidence (confidence: traced).** `FlightController`, `SpectatorCamera` and `MenuInput` each build
`BindingProfile.Defaults(...)` in their own constructor, and `BindingStore` is called from no polling
site, so the Menu context is live while Flight and Camera are saved and never read (D31's section
records this in full).

**Approach.** A launch-time load that hands each seat its stored profile, replacing the defaults
those three constructors build for themselves. The seam already exists: C21's `BindingProfile` is
what each site holds, so the change is where the profile comes from rather than what it is.

**Model recommendation.** high. It reaches three constructors and the determinism gate at once.

**Verify.** A rebind survives a restart, at the controls, in flight rather than on a menu. Plus a
`--det` fact that a scripted run ignores a stored profile entirely.

**⚠ Traps.** ⚠ **DET-8 is the whole risk.** `--det` ignores `config.json` precisely so a scripted
run is a function of its committed tree, and a keymap loaded from the user's profile directory would
break exactly that: every golden and every probe would depend on whoever ran it. The load must be
gated off under `--det` the way the options store is, and the gate needs a test that can fail rather
than an assertion that it was written.

⚠ A stored profile names actions and controls that a later build may not have. A rename or a dropped
action must degrade to the default for that action rather than throwing at launch or, worse, leaving
a seat with no fire button.

## D35 ☑ Staged edits and a whole-map reset: Accept, Cancel, Reset to default

**Landed.** `ControlsFeature` edits a working copy of each registered seat's three `ActionMap`s
rather than the maps themselves. `Accept` writes every changed seat's working copy through and saves
it, `Cancel` throws every seat's staged edits away and re-stages from the live maps, and `ResetSeat`
puts the whole seat back to the shipped defaults across every `InputContext`. `ResetContext` stays
for the one-context form. `Save()` is gone: a save that is not a commit has no meaning in a staged
model, and leaving both would have given a caller two ways to half-commit. The built-in screen gains
three rows below the action list, in the original's own order, Reset to default, Cancel changes,
Accept changes; `P`/`X` still resets and now resets the whole seat; Back leaves the way Cancel does.

**The property D31 landed deliberately, and what happened to it.** D31 made the menu context live by
editing the very object `MenuInput`'s readers hold, so a menu rebind was felt on the next frame.
Staging necessarily moves that to Accept: an edit that is not committed is not in the map anything
reads. The property that survives is the one that matters, and it is kept on purpose rather than by
luck: `Accept` **fills the existing `ActionMap` in place** (clear, then `Add` each binding) instead
of swapping the reference, so the object `MenuInput` holds is still the object that changed, and a
menu rebind is felt on the frame after Accept without a reload. `TheCommitFillsTheSameMapObjectSoAMenuPollerFeelsIt`
asserts both halves, the reference identity and the new binding. `Add` rather than `Assign` in that
copy, because the shipped set deliberately puts one control on two actions (C21's numpad diagonals,
B15's d-pad up) and a steal on the way in would silently undo the second.

**What the binary answered, with addresses.**

- **RESET TO DEFAULT is staged, not immediate** (`FUN_00419de0`). Called with a non-zero flag it
  pushes a scratch copy of the command manager (`FUN_00537ca0`, which allocates a `0x3f5c` manager,
  copies the current one into it and links the old one onto a stack at `DAT_0075cb04`), clears every
  word of the copy (`FUN_00537ac0` into `FUN_00537040`), lays the 64 shipped defaults down into it
  (`FUN_004936c0`), refills the screen's own record table from that copy, and then pops it
  (`FUN_00537d90`), which destroys the copy and restores the manager the game was playing. The live
  command map is byte-identical across the whole call.
- **The screen edits a working copy and commits on ACCEPT** (`FUN_00419d50`, reached from the UI
  dispatcher `FUN_00407670` case `0x12`). The working table is `DAT_0064ab2c`, allocated at case
  `0x14` as `FUN_0041a000()` records of `0x168` bytes each, one per command across every category,
  each holding the command id at `+0x154` and the packed four-slot word at `+0x164`. `FUN_00419d50`
  clears the whole live map and re-defines every command from that table through `FUN_00537bc0` into
  `FUN_00537360`. Even the steal rule runs inside the working copy: `FUN_00405900` scans the table
  for another record holding the same code and clears the field out of *that record*, so no
  reassignment reaches the live map before ACCEPT either.
- **RESET reaches the whole command map, not the page on screen.** `FUN_00419de0`'s outer loop runs
  `0` to `FUN_00493530()` (the category count) and its inner loop `0` to `FUN_00493560(category)`,
  rebuilding a record for every command in every category. The author's reading is confirmed from
  the binary.
- **CANCEL restores nothing after a RESET, because a RESET moved nothing.** The reset rewrote only
  the working table and popped its scratch manager, so the live map still holds what it held when
  the screen was opened, and leaving without `FUN_00419d50` leaves it there.

The button-to-handler mapping is an inference and is stated as one: the labels are not in
`crimson.exe` (they live in the localised UI data, and a string search for "RESET TO DEFAULT" or
"ACCEPT CHANGES" returns nothing), so RESET was identified by `FUN_004936c0` having exactly two
callers, the startup path `FUN_0043fb50` and `FUN_00419de0`'s flagged branch, and ACCEPT by
`FUN_00419d50` being the only writer of the live map from the screen's table. **No cancel-specific
code path exists to find**, which is itself the answer: nothing needs undoing.

**What the screenshot alone settled.** That there are exactly three buttons and that they persist on
every category page (`Z:\CSVM\OriginalScreenshots\Keybinds Movement.png`, and the author states the
same strip appears on all seven). Their on-screen order, Reset / Cancel / Accept, is the screenshot's
too. Our three rows sit at the tail of one list rather than as a persistent strip, because this
presentation has one windowed list and no button band; that is a fit to our layout, not a decode
result, and it is the author's to judge.

**Verified.** `CSVM.Tests/ControlsFeatureTests.cs`, 24 facts, each asserting a specific resolution.
Six are new and speak to this item directly: an edit is visible in the staged view and absent from
the map the polling site holds until Accept; Accept writes it through and clears the dirty mark;
Cancel leaves it in neither the live map nor the save (a counting save hook, asserted empty); a
reset reaches every one of the three contexts, driven by breaking one binding in each so a
per-context reset would leave two of them broken; Cancel puts back everything a reset cleared, over
a rebind that had already been accepted; and Accept commits two contexts at once. Two more cover the
seams the change could have broken: the commit fills the same `ActionMap` object a menu poller holds,
and a reset on seat 2 leaves seat 1's staged edit alone. Every pre-existing fact was rewritten
against the staged view rather than loosened, and `DiscardDropsTheCaptureAndThePendingStealAndKeepsTheStagedEdits`
now asserts the staged edits survive a presentation switch while the capture and the pending steal
do not.

Run the three rebinding suites with
`.\RunTests.ps1 -UnitFilter "FullyQualifiedName~ControlsFeatureTests|FullyQualifiedName~ControlCaptureTests|FullyQualifiedName~ActionMapTests" -SkipEngine -SkipGoldens`:
54 facts, all passing.

Complete `.\RunTests.ps1` in the item's worktree: PASS, exit 0, 3349 units, 233 engine suites, 18
goldens hash-identical, 0 build warnings (183.1s total against a 180.0s budget, and the engine stage
112.5s against 100.0s, which is awareness only). `menu-original-tracer` still walks into the Controls
screen and back out, over the three rows this item adds.

⚠ **What the automated evidence cannot reach, and is owed at the controls.** The same limitation
D31 records applies unchanged: no suite presses a physical key, `--det` implies `--no-pads`, and
every scripted run is unattended (`DET-2`, `DET-6`, `INSTR-14`). The checks this item adds to D31's
list are in the report; the three-row tail, its labels and its position are the author's call.

**⚠ Traps.** ⚠ **Do not swap the `ActionMap` reference on commit.** `MenuInput` holds the object, not
the profile, and replacing it would leave the menu on the pre-Accept keymap with nothing to show for
it. ⚠ A staged model has one new way to lose work: leaving the screen. Back is Cancel here, which is
the original's shape, and it means a player who walks away without pressing Accept keeps the keymap
they were playing with. Whether that reads right at the controls is a judgement, not a decode.
