# Bindings

What a binding is, how one resolves against hardware, and the named actions a polling site asks for. The registry that turns a device identity into a live pad and the map that holds the actions both sit on top of these types.

One `## src/...` entry per module, body at most 8 lines, 12 for the highest-traffic modules.

Traps do not live here; the rule is in `docs/architecture.md`.

## src/Bindings/DeviceId.cs

Which device a binding sits on, as a value: `DeviceKind` plus, for a joypad, the hardware string
its platform reports. `Keyboard` is a singleton because the platform reports one key state whatever
produced it; `Joypad` takes the stable string and refuses a blank one. The default value is
`DeviceKind.None` and names nothing, which is what a binding read from a file with a missing device
becomes. Turning an identity into a live joypad index belongs to the device registry, not here.

## src/Bindings/BindingControl.cs

The tagged control half of a binding: `ControlKind` picks which of `Key`, `Button`, `Axis` and `Hat`
the numeric members mean, and the four static factories are the only way to build one, because they
are where the per-kind invariants live. `HatDirection` is a flags enum so a device can report a
diagonal, while a binding names exactly one direction. `Deadzone` is both the noise gate and the
digital threshold; the model carries no second number for the two jobs. The original's four fixed
typed slots and its reasons are decoded in `docs/org/input.md`.

## src/Bindings/Binding.cs

`Binding` is a device identity plus a control, and `Resolve` is the whole read: it reaches hardware
only through `IDeviceState`, so the model is exercised without an engine. `ControlValue` is the
held/how-far pair, with `Pressed` true exactly when `Value` is above zero, which is what lets an
axis drive an action written as digital and a button drive one written as analogue. Structural
equality is what a rebinding screen compares when it takes a control off its previous owner. Past
its deadzone an axis reports its raw travel, deliberately not the remainder rescaled onto `[0, 1]`:
no polling site this seam replaced rescaled, so a rescale moved every stick's response curve while
keeping the deadzone number intact.

## src/Bindings/IDeviceState.cs

The tick's raw hardware state, addressed by device identity rather than connection index: keys,
buttons, axis travel with no deadzone applied, and a hat's full direction flags. Every member
answers for an absent device instead of throwing, which is how an unplugged pad silences its
actions without the map losing the rows. Two implementations ship: `GodotDeviceState` for a single
device identity through the registry, and `SeatDeviceState` for a seat that reads a whole pad set.

## src/Bindings/SeatDeviceState.cs

One seat's hardware behind the binding seam, which is what every polling site actually holds. A seat
reads a SET of pads while a binding names one device, so pad defaults sit on a placeholder identity
and this answers for it: buttons ORed, axes taken at the largest magnitude across the set. Pad reads
go through `Pads.For`, never a registry index, because that gate is what `--no-pads` and an
unfocused window act on and it carries the phantom-device policy; `GodotDeviceState` has neither,
which is why a site on it would lose `--det`'s implied `--no-pads`. Only the pad half is gated:
Godot polls joypads regardless of window focus while key state is focus-scoped, so a uniform gate
would either under-protect pads or add a keyboard gate nothing needs. `Refresh` takes the pad list
once a tick, since `Pads.For` re-reads the roster on every call.

## src/Bindings/BindingSet.cs

The bindings one action holds. `Resolve` ORs them, which is the original's four-slots rule
(`FUN_00537530`, `docs/org/input.md`) without its four fixed slots, and takes the deepest deflection
any of them reports so a half-pressed trigger cannot beat a held button on the same action. `Add`
drops a duplicate rather than rejecting it, `Remove` is the losing half of the steal rule, and
`Clone` gives an editing screen something it can throw away. It owns no action name and no device
lookup. Coverage: `CSVM.Tests/BindingModelTests.cs`.

## src/Bindings/InputAction.cs

The named actions, one member per binding a polling site holds today, so migrating a site is a
lookup swap rather than a rename. The members are contiguous from zero because `ActionSnapshot`
indexes arrays by them; adding one at the end is safe and renumbering is not. Debug and lab keys
are deliberately outside the enum: they are development instruments, and a rebinding screen that
offered them would let a player break their own diagnostics.

## src/Bindings/ActionMap.cs

One player's keymap, an action to a `BindingSet`. `Assign` is the winning half of the steal rule and
returns every action that lost the control, in enum order, so a screen can name each loss instead of
performing it silently; `OwnersOf` asks the same question without committing, and there is no
single-owner form, because several shipped controls sit on two actions and a caller taking the first
owner would report one loss and perform two. `SameControl` is what "the same control" means there,
ignoring an axis deadzone so re-binding an axis adjusts it rather than stacking a copy, and reading a
hat direction and the d-pad button Godot actually reports as different controls (which is why nothing
authors a hat and no capture produces one). `ResolveInto` reads every bound action once per tick into
a reused snapshot. `Add` is the other half: it binds without stealing, for the shipped defaults and a
loaded file, where a control is deliberately on two actions (a numpad snap-look diagonal, flight's
d-pad up). It holds no defaults and no device lookup. Coverage: `CSVM.Tests/ActionMapTests.cs`.

## src/Bindings/ControlCapture.cs

What a rebinding screen may capture, and the scan that turns a press into a `Binding`: the bindable
key list, Godot's whole pad button range, the mouse buttons past the pointer's own, and Godot's SDL
axis range. `Arm` masks everything already held so the press that opened the capture is not read as
the answer to it, and a masked control has to be released first. Every pad control is stamped with
the seat's own identity rather than a hardware GUID, because a seat reads a set of pads through a
placeholder and a real GUID beside a placeholder row would be two controls to
`ActionMap.SameControl` and one to the player. An axis carries the release-first rule as a
rest-then-move rule, since a resting stick drifts and has no release: it is masked until it is seen
inside `RestBand`, and only then does a travel past `MoveThreshold` capture, with the sign the
direction moved and a fixed `CapturedDeadzone` rather than the travel the crossing happened to
report. Hats are not scanned, because a d-pad arrives as four buttons on this backend and a hat
binding would be a second encoding of a control the defaults already author as a button. Escape and
pad B cancel and are therefore never captured. Coverage: `CSVM.Tests/ControlCaptureTests.cs`.

## src/Bindings/BindingLabels.cs

What a rebinding screen prints: an action's name, a control's name in keycap terms rather than enum
terms, and one row of an action's whole binding list. A row states how many bindings it is not
showing, because the original ships four slots per action and draws the first two non-empty
(`FUN_00449fc0`, `docs/org/input.md`), so its screen hides bindings with no way for a player to tell.
Separate from `BindingStore`'s tokens on purpose: a file is parsed back and a label is only read.

## src/Bindings/ActionSnapshot.cs

The tick's resolved held/how-far pair per action, so two consumers asking the same question in one
tick cannot disagree. It carries no went-down and no went-up: edge detection stays in the consumer
that already owns its previous-frame slot, because the sim is a level read on the fixed tick and
scripted `--det` / `--hold` runs depend on that. The instance is reused every tick. `Axis` reads
zero with both ends held, which is what the flight and camera pairs do; a menu cursor axis gives the
negative end priority instead, so `MenuInput.Dir` reads those rather than this.

## src/Bindings/PlayerActions.cs

What a polling site holds: a player's `ActionMap`, the tick's snapshot, and `Poll` as the one place
hardware is read. `ReadsKeyboard` is the existing player-1-only keyboard rule, kept on the seat
rather than in the map, so a pad-only splitscreen player keeps the shipped keyboard defaults in
their map and simply reads none of them. Turning a device identity into live hardware belongs to the
device registry; this type only reads the state it is handed. Several of these over one `ActionMap`
is the supported shape, not a workaround: where the keyboard and pad halves of an action take
different processing and are then summed rather than ORed (a ramp against a curve in flight, two
look rates in the camera), one merged read would have to pick a rate and would drop the sum.

## src/Bindings/InputContext.cs

Which controls a seat is reading: flight, menu, or the free camera. A seat holds one `ActionMap` per
context rather than one map overall, because the shipped keymap gives one control several meanings
(`W` pitches down in flight, moves a menu cursor up, and flies the spectator camera forward) while a
map holds a control once. The steal rule therefore runs inside a context, which is also the scope a
rebinding screen edits: a conflict is two flight actions wanting one button, not flight and the menu
sharing it.

## src/Bindings/DefaultBindings.cs

The shipped keymap as data, one `ActionMap` per context, transcribed from `docs/controls.md`. Every
action is either bound here or on the `Unbound` list, which stops a migrating site meeting a hole one
call at a time: `FreeLook` is unbound because the model has no mouse-button kind, `MenuJoin` because
joining is a gesture over controls the menu binds elsewhere. A pad default names the placeholder
identity `AnyPad`, which no hardware reports, and `MapFor` puts the seat's own pad in its place. Two
rules bind edits here: nothing authors a hat binding, since Godot reports a d-pad as four buttons and
the two encodings are one control `ActionMap.SameControl` reads as two; and some controls are
deliberately on two actions, which is why the set is built through `ActionMap.Add` rather than
`Assign`. Those are the numpad snap-look diagonals, and flight's d-pad up, which cycles the stunt
marker beside the target because a stunt target is an objective marker (`BL-686`). The camera's
trigger pair carries a related case: boost and slow gate at half travel while the orbit dolly reads
the same triggers from zero on its own actions, so neither reading has to carry the other's number.
Coverage: `CSVM.Tests/DefaultBindingsTests.cs`.

## src/Bindings/BindingProfile.cs

One seat's whole input: an `ActionMap` and a `PlayerActions` per context, and the keyboard gate that
applies to all of them at once. This is what a polling site is handed and what a rebinding screen
edits. `Poll` resolves every context on the tick rather than only the mode in front of the player,
because a pause board and the aeroplane behind it are both live on one tick.

## src/Bindings/BindingStore.cs

The keymap file: versioned JSON, one per player, under `user://`. Named and versioned against the
original, which writes 2400 unversioned raw bytes to the registry and points its live array at the
loaded buffer (`docs/org/input.md`), so a record-layout change there reinterprets an old save. A
binding is stored as `device/control` in words (`keyboard/key:Space`, `pad:<id>/axis:LeftY+@0.25`), so
a player can correct one row by hand. Anything this build cannot read costs that action its saved
bindings and nothing else: an unknown action or context name, an unreadable token, and a hat row (see
`DefaultBindings`) all leave that action at its default while the rest of the file loads. Every
action is written, the unbound ones included, so a deliberate unbind survives a reload. Whether a
seat reads the keyboard is not persisted, or a saved file could hand a pad-only splitscreen player
the keyboard back. Coverage: `CSVM.Tests/BindingStoreTests.cs`.

## src/Bindings/LaunchBindings.cs

Where a seat's keymap comes from when the seat is built. `FlightController`, `SpectatorCamera` and
`MenuInput` each ask this instead of building `BindingProfile.Defaults` for themselves, so there is
one place the read is gated and one place to look when a rebind is not felt. `Launcher` calls
`Configure` before the first seat exists, and the gate is shut until it does, so a host that never
configures gets the shipped set rather than somebody's file.

⚠ **A scripted run reads no keymap** (`docs/verification.md`, DET-8). `--det` ignores `config.json`
so that a run is a function of its committed tree, and a keymap loaded from the user's profile
directory would make every golden and every probe depend on whoever ran it; `--run-tests` is shut for
the same reason. `LaunchBindings.ReadSavedKeymaps` is the ship switch, and setting it false leaves
every seat on the defaults while the rebinding screen still edits and saves.

A seat is put on the loaded map through `ActionMap.Fill`, which replaces a map's contents in place.
A polling site hands one map to two or three `PlayerActions`, so swapping the reference would leave
those readers on the map the seat was constructed with. Coverage:
`CSVM.Tests/LaunchBindingsTests.cs` for the gate and the fallbacks, the `bindings-launch-load` engine
suite for a real `FlightController` picking the file up through its own `Bind`.
