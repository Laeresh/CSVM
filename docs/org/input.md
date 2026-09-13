# The command map: keybindings, devices and dispatch, decoded from `crimson.exe`

Read out of the retail executable with Ghidra (static analysis of the shipped x86 build,
`crimson.exe`, `language x86:LE:32:default`), 2026-09-02. Every claim below names the function or
address it came from.

Everything here is a description of *behaviour*. No decompiler output is reproduced; the addresses
are given so any claim can be re-checked at source.

⚠ **This page is a decode, not a proposal.** The original's binding model is narrower than anything
CSVM can ship (one joystick, ten buttons, no axes). Where this page describes a limit, it is
recording what the original does, not setting a bar for the port. See "What this means for CSVM".

**Where the neighbours live.** The eleven targeting actions this page lists by id are decoded as
*behaviour* in [`targeting.md`](targeting.md). The camera actions (`0x30`-`0x39`) are
[`cameraViews.md`](cameraViews.md). The bindings CSVM ships today are `../controls.md`, whose
flight table is the shipped defaults below, key for key, with this port's own actions on the keys
the original leaves free.

## The headline

There is **no default keymap table in the data**. The 64 shipped defaults are emitted by code, one
call each, and the live map is a flat array of **one 32-bit word per command id**, indexed by that
id. Each word packs four binding slots: two keyboard codes, one joystick button, one mouse button.
Any of the four fires the action.

Three findings will not be guessed from the keybind screens:

- **The UI's two columns are not two fields.** The record has four slots, and the row renderer packs
  the first two non-empty of them into the two visible columns. A row showing "E" and "Joystick 3"
  is slot A and the joystick field, with slot B empty.
- **A joystick binding is a bare button index with no device.** There is one joystick pointer in the
  whole program, and axes and hats cannot be bound at all.
- **The keymap persists to the registry, byte for byte.** The saved blob is the in-memory array,
  2400 bytes, not a serialized or textual form.

## Function map

| Address | Role |
|---|---|
| `FUN_004936c0` | **The defaults.** 64 calls, one per shipped binding, `0x004936c0`-`0x00493cf5` |
| `FUN_00493630` | The per-default shim: resolves the message id, applies the Japanese remap, forwards six arguments |
| `FUN_00493600` | The Japanese-keyboard code remap |
| `FUN_005384a0` | The keyboard-type probe. Returns 99 unless `GetKeyboardType(0) == 7` |
| `FUN_00537360` | **Define command.** The sole entry point that writes a command's word |
| `FUN_00537090` | The word encoder (the bit layout below) |
| `FUN_005370f0` / `FUN_00537110` | Read keyboard slot A / slot B |
| `FUN_00537130` / `FUN_00537150` | Read the joystick button field / the mouse button field |
| `FUN_005371d0` / `FUN_00537230` | Reassign a code, clearing it out of its previous owner's word |
| `FUN_00536f50` | Allocates the map. `FUN_005bde90("CmdMap", 7, count * 4, 1)` |
| `FUN_00537890` | Creates the manager. Called once from `0x004a7340` with `count = 0x258` |
| `FUN_00536e80` | Rebuilds the four reverse-lookup arrays whenever the map changes |
| `FUN_00537530` | **The per-frame test.** ORs the state of all four slots |
| `FUN_005357b0` | The buffered keyboard read. Composes the control code and dispatches |
| `FUN_00535a00` | The control-code range gate, `0..0x7dd` |
| `FUN_00535fb0` | Handler registration. Refuses an already-claimed code |
| `FUN_00536b50` | `GetDeviceState` into a `DIJOYSTATE2` |
| `FUN_00536c40` | Reads one joystick button, gated `1 <= n < 0xb` |
| `FUN_00536cb0` | Clears joystick buttons `1..0xa` |
| `FUN_00537910` | Fills the joystick button name table for indices 1-10 |
| `FUN_00449fc0` | The keybind screen's row renderer |
| `FUN_005375f0` | The keyboard control-code name formatter |
| `FUN_00537830` / `FUN_005379b0` | The joystick / mouse button name formatters |
| `FUN_005bdad1` / `FUN_005bdc89` | Registry load / save |
| `FUN_005bd900` | Builds the registry key path |
| `FUN_0043fb50` | Startup order: register, defaults, registry load, rebuild |

## The record

The live map is an array at `inputMgr + 8`, allocated by `FUN_00536f50` as 600 dwords (`0x258`
commands, 2400 bytes). The manager object is `DAT_0075caf4` (read at `0x00537bfa`), size `0x3f5c`,
created by `FUN_00537890` from `0x004a7340`. Parallel arrays in the same object hold the handler
pointers (`+0x10`), the action name strings (`+0x14`, `char[0x50]` each) and the count (`+0x04`).

One command is one 32-bit word. From the encoder `FUN_00537090` and its four accessors:

| Bits | Field | Accessor |
|---|---|---|
| 0-10 | Keyboard binding A, an 11-bit control code | `FUN_005370f0` |
| 11-21 | Keyboard binding B, same encoding | `FUN_00537110` |
| 22-25 | Joystick button, 1-10, 0 meaning unbound | `FUN_00537130` |
| 26-27 | Mouse button, 1-3, 0 meaning unbound | `FUN_00537150` |
| 28-31 | Unused | |

### The keyboard control code

A control code is a **raw DirectInput DIK scancode** in bits 0-7, OR'd with modifier flags
`0x100` Alt, `0x200` Ctrl, `0x400` Shift (`FUN_005357b0`, whose source path is
`D:\zipper\gamez\zinput\zin_kbd.c`; the name formatter is `FUN_005375f0`). It is not a virtual-key
code, so a binding is a physical key position and prints differently under a different layout.

Left and right modifier variants collapse onto the same flag: `0x1d`/`0x9d` Ctrl, `0x2a`/`0x36`
Shift, `0x38`/`0xb8` Alt.

Worked examples from the shipped defaults: `E` is `0x012`, `Shift+E` is `0x412`, `Ctrl+E` is
`0x212`, `Ctrl+X` is `0x22d`, `Shift+L` is `0x426`.

The code space is 2014 values (`0..0x7dd`), gated at `FUN_00535a00` and matching the 2014-entry
tables cleared by `FUN_00536e80` and `FUN_00535fe0`.

### The joystick and mouse fields

A joystick binding is **a button index alone**. `FUN_00536c40` reads the current and previous
`rgbButtons[n-1]` of a single `DIJOYSTATE2` at `DAT_0075c1e4` (0x110 bytes, previous frame's copy at
`DAT_0075c2f4`, filled by `GetDeviceState` in `FUN_00536b50`). The index is gated `1 <= n < 0xb`, so
ten buttons are bindable; `FUN_00536cb0` clears exactly `1..0xa` and `FUN_00537910` fills the name
table over the same range as localized `MSG_JBTN_%d` with a `"Button %d"` fallback.

A mouse binding is a third encoding: the 2-bit field indexes `DAT_0075ccc0 + 0x30/+0x34/+0x38`
(`FUN_00537530`), named Left, Right and Middle by `FUN_005379b0`.

## What the keybind screen shows

`FUN_00449fc0` emits up to four strings for a row, in slot order: keyboard A (`FUN_00537c30` into
`FUN_005375f0`), keyboard B (the same formatter), joystick (`FUN_00537c50` into `FUN_00537830`),
mouse (`FUN_00537c70` into `FUN_005379b0`). Empty slots are skipped and the survivors pack into the
visible columns. So the screen's "Control A" and "Control B" headings name positions in the row, not
fields in the record, and a row reading "E / Joystick 3" has slot B empty.

## The shipped defaults

64 rows, from `FUN_004936c0`. `FUN_00493630` also stores each command's message id into the global
array at `0x0071c5b8` (write at `0x00493697`). Names are resolved by message id through
`language.dll`; the English text is not in `crimson.exe` and is quoted here from the extracted
message table. Control codes are the raw stored values.

| Page | Cmd | Action | Keyboard A | Keyboard B | Joy | Mouse |
|---|---|---|---|---|---|---|
| Movement | `0x01` | Point Nose Down | `0x0c8` Up | | | |
| | `0x02` | Point Nose Up | `0x0d0` Down | | | |
| | `0x03` | Roll Left | `0x0cb` Left | | | |
| | `0x04` | Roll Right | `0x0cd` Right | | | |
| | `0x05` | Turn Left | `0x033` `,` | `0x052` Numpad0 | | |
| | `0x06` | Turn Right | `0x034` `.` | `0x053` NumpadDecimal | | |
| | `0x2f` | Level Off | `0x426` Shift+L | | | |
| Throttle | `0x07` | Throttle Up | `0x00d` DIK_EQUALS | | | |
| | `0x08` | Throttle Down | `0x00c` DIK_MINUS | | | |
| | `0x09`-`0x11` | Throttle 0/8 through 8/8 | `0x002`-`0x00a` (`1`-`9`) | | | |
| Weapons | `0x13` | Fire Guns | `0x039` Space | | 1 | |
| | `0x14` | Fire Rockets | `0x02d` X | | 2 | |
| | `0x19` | Cycle guns clockwise | `0x03d` F3 | | | |
| | `0x1a` | Cycle guns counterclockwise | `0x03e` F4 | | 7 | |
| | `0x20` | Cycle rockets clockwise | `0x03f` F5 | | | |
| | `0x21` | Cycle rockets counterclockwise | `0x040` F6 | | 8 | |
| Targeting | `0x24` | Next Enemy/Objective | `0x012` E | | 3 | |
| | `0x25` | Previous Enemy/Objective | `0x412` Shift+E | | | |
| | `0x26` | Nearest Enemy/Objective | `0x212` Ctrl+E | | | |
| | `0x27` | Next Ally | `0x011` W | | | |
| | `0x28` | Previous Ally | `0x411` Shift+W | | | |
| | `0x29` | Nearest Ally | `0x211` Ctrl+W | | | |
| | `0x2a` | Next Non-Aircraft | `0x013` R | | 6 | |
| | `0x2b` | Previous Non-Aircraft | `0x413` Shift+R | | | |
| | `0x2c` | Nearest Non-Aircraft | `0x213` Ctrl+R | | | |
| | `0x2d` | Select Target Nearest Crosshairs | `0x010` Q | | | |
| | `0x2e` | Target Nothing | `0x014` T | | | |
| Views 1 | `0x30` | Toggle Spyglass | `0x41f` Shift+S | | 5 | |
| | `0x31` | Cycle Cockpit Views | `0x042` F8 | | 4 | |
| | `0x32` | External Camera Down | `0x043` F9 | | | |
| | `0x33` | External Camera Back | `0x044` F10 | | | |
| | `0x34` | External Camera Left | `0x057` F11 | | | |
| | `0x35` | External Camera Right | `0x058` F12 | | | |
| | `0x36` | Access Chase View | `0x041` F7 | | | |
| | `0x37` | Access Snap Look Mode | `0x025` K | | | |
| | `0x38` | Track Target | `0x026` L | | | |
| | `0x39` | Access Smooth Look Mode | `0x024` J | | | |
| Views 2 | `0x3a`-`0x42` | Look Up/Left, Left, Rear, through Look Up/Right | Numpad 1-9 (`0x04f`, `0x050`, `0x051`, `0x04b`, `0x04c`, `0x04d`, `0x047`, `0x048`, `0x049`) | | | |
| | `0x43` | External Camera Zoom In | `0x04e` NumpadPlus | | | |
| | `0x44` | External Camera Zoom Out | `0x04a` NumpadMinus | | | |
| Other | `0x12` | Use Nitro-Booster | `0x031` N | | | |
| | `0x22` | Bail Out | `0x22d` Ctrl+X | | | |
| | `0x23` | Display Scores (Multiplayer Only) | `0x00f` Tab | | | |
| | `0x45` | Pause/Quit/Objectives | `0x001` Esc | | | |
| | `0x6a` | Auto-Dock | `0x01e` A | | | |
| | `0x6d` | Chat to Everyone | `0x029` DIK_GRAVE | | | |
| | `0x6e` | Chat to Team | `0x429` Shift+GRAVE | | | |
| | `0x6f` | View Help | `0x03b` F1 | | | |

⚠ **The clockwise and counterclockwise names are inverted against the message keys.** "Cycle guns
clockwise" is `MSG_CMD_CANNON_PREV` and "counterclockwise" is `MSG_CMD_CANNON_NEXT`. Take the
displayed name, not the key, when reading a direction off this table.

⚠ **The joystick column carries only the counterclockwise half of each weapon cycle**, button 7 for
the guns and button 8 for the rockets. A pad player of the original steps one way and reaches the
other by going round, which is why a second pad control was never a given here.

⚠ **Three codes differ on Japanese hardware.** `FUN_00493630` remaps A and B through
`FUN_00493600` when `FUN_005384a0` returns 1-98, which happens only when `GetKeyboardType(0) == 7`.
On that path `0x0d` becomes `0x90`, `0x29` becomes `0x7d` and `0x429` becomes `0x47d`. Every other
keyboard, German included, gets the literals above.

## Dispatch

Not a scan. The buffered DirectInput keyboard read in `FUN_005357b0` composes
`code = DIK | modifiers` and writes the press or release state straight into
`(&DAT_007582e4)[code * 2]`, with the registered handler for that code at `(&DAT_007582e8)[code * 2]`.
Lookup is a direct index by control code.

`FUN_00536e80` maintains the inverse direction as four arrays inside the manager, one per slot
(`+0x18` keyboard A, `+0x1f90` keyboard B, `+0x3f08` joystick, `+0x3f48` mouse), each mapping a code
to a command id and rebuilt whenever the map changes.

Two consequences:

- **An action holds up to four bindings and any of them fires it.** `FUN_00537530` ORs all four
  slots.
- **Two actions cannot share a code.** `FUN_005371d0` and `FUN_00537230` clear a code out of its
  previous owner's word when it is reassigned, and `FUN_00535fb0` refuses a second handler
  registration for a claimed code, returning `0xffffffff`. The reverse arrays are single-valued, so
  the last write wins.

## Persistence

The map is saved to the **registry**, not to a file. `FUN_005bde90` registers it as name `"CmdMap"`,
type 7, size 2400, scope flag 1. `FUN_005bdc89` saves and `FUN_005bdad1` loads; flag 1 selects
`HKEY_CURRENT_USER` and flag 0 `HKEY_LOCAL_MACHINE`. `FUN_005bd900` builds the path from
`"SOFTWARE\%s\%s\%s"` (literal at `0x0061f0d0`), giving
`SOFTWARE\Microsoft\Microsoft Games\Crimson Skies\1.0`.

Type 7 writes a `REG_BINARY` of the raw buffer, and `FUN_00536f50` points `inputMgr + 8` at that
same buffer, so **the persisted form is the in-memory array**: 2400 bytes, 600 little-endian dwords
in the layout above, indexed by command id. There is no versioning, no key names and no textual
form.

Startup order is `FUN_0043fb50`: register the variables, run `FUN_004936c0` to lay down the
defaults, load from the registry over the top, then rebuild the reverse arrays.

## What this means for CSVM

| | Original | CSVM today |
|---|---|---|
| Binding identity | a physical scancode plus three modifier bits, or a bare button index | a device identity plus a tagged control (`Binding`); a key control carries the same three modifier bits, so `E` and Shift+`E` are two controls |
| Slots per action | four, fixed by type: keyboard, keyboard, joystick button, mouse button | a list of any length, ORed together (`BindingSet`) |
| Joystick devices | exactly one, `DAT_0075c1e0`, with no index in the record | any number, named by stable hardware string and resolved to a live index per tick |
| Bindable joystick controls | buttons 1-10 only. Axes and hats are read outside the map and cannot be bound | every button the platform reports and either half of any axis; no hat, since a d-pad arrives as buttons |
| Rebinding | a keybind screen writing into the same word the defaults wrote | a screen editing an `ActionMap`, the steal rule naming every action that loses the control |
| Persistence | 2400 raw bytes under `HKEY_CURRENT_USER` | versioned JSON per player under `user://`, in the shape below |

Two things are worth taking. The **four-slots-ORed-together** semantics give an action several
bindings at once without a mode, and the **code-uniqueness rule** (reassigning a control steals it
from its previous owner rather than double-binding) is the behaviour a rebinding UI needs anyway.

The encoding is not worth taking. A binding here is a fixed slot of a fixed type on the one device
the program can see, which is why axes are unbindable and why ten buttons is a hard number rather
than a configured one. Wanting more than one pad, an axis driving a digital action, or a device that
survives a replug is what makes a binding a device identity plus a tagged control rather than a
scancode, held in a list rather than in typed slots.

## The CSVM keymap file

One JSON file per player under `user://`, named `bindings_p<n>.json`, written atomically through a
sibling temp file and a rename. It is versioned, and the version says how the tokens below are
encoded rather than which actions exist: an action a file does not name simply stays at its shipped
default, so adding one needs no bump.

```json
{
  "version": 2,
  "player": 1,
  "contexts": {
    "flight": {
      "FireGuns": ["keyboard/key:Space", "pad:*/button:B"],
      "PitchUp": ["pad:*/axis:LeftY+@0.25"],
      "TargetPreviousEnemy": ["keyboard/key:Shift+E"],
      "TargetNextAlly": []
    },
    "menu": { },
    "camera": { }
  }
}
```

A binding is one string, `device/control`, in words rather than numbers so a player can correct a
row by hand.

- The device is `keyboard`, `mouse`, or `pad:<hardware id>`. A shipped pad row is authored on the
  placeholder id `*`, which the loader replaces with the seat's own pad.
- The control is `key:<name>`, `button:<name>`, `mouse:<name>`, `axis:<name><sign>@<deadzone>` or
  `hat:<index>:<direction>`. A name is the engine's own enum name, or `#<number>` for a code the
  engine does not name; a bare number is accepted on the way back in either way. An axis carries its
  sign and its deadzone, which is both the noise gate and the digital threshold.
- A key name may carry the original's own modifiers in front of it, `key:Shift+E`, `key:Ctrl+E`,
  any of `Shift`, `Ctrl` and `Alt` in any order and any case. That prefix is version 2 of the file;
  a version 1 file names no modifier, and since a bare key token means the same thing in both, such
  a file still loads whole and the reader checks no version.

Every action of every context is written, the ones bound to nothing included, so a deliberate unbind
survives a reload rather than coming back at its default. Whether a seat reads the keyboard is not
written: a saved file could otherwise hand a pad-only splitscreen seat the keyboard back.

Nothing costs the file. A row the reader cannot read costs that action its saved bindings and
nothing more, leaving it on the shipped default while the rest of the file loads. That covers an
unknown context or action name, a token in a shape this build does not know, and a `hat:` row, which
is deliberately unreadable because Godot reports a d-pad as four buttons and a hat row would be a
second encoding of a control the defaults already author as a button.

## Open questions

- The `key_*` string table at `0x0060cdcc`-`0x0060d30f`, with a 120-entry pointer array at
  `0x00640708`, is the GOSSCRIPT console's key-name constants and has no visible xrefs. It is not
  the command map and its parallel value array was not chased.
- Commands are 600 slots wide but only 64 ids are defined, sparsely, up to `0x6f`. Whether the gaps
  are cut features, debug commands or slots for another build was not investigated.
