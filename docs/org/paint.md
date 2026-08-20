# The paint scheme, decoded from `crimson.exe`

Read out of the retail executable with Ghidra (static analysis of the shipped x86 build,
`crimson.exe`, `language x86:LE:32:default`), 2026-08-20, while opening `BL-394`'s livery half.
Every claim below names the function it came from.

Everything here is a description of *behaviour and layout*. No decompiler output is reproduced; the
addresses are given so any claim can be re-checked at source.

**What this page settles.** How a vehicle def's `paint_pattern`, `paint_decal1..3` and
`paint_color1..3` reach a spawned aircraft: the slots they parse into, the per-field rule that
decides between an authored def value and a per-instance override, and how three colour components
become one packed dword. The authored side (which key means what, the `.BM` masks, the pattern
census) is [`formats/paint.md`](../formats/paint.md); this page is the engine half.

⚠ **This page is a decode, not a proposal.** Where CSVM deliberately differs, that is listed at the
bottom rather than hidden.

## Function map

| Address | Role |
|---|---|
| `FUN_00479240` | The vehicle-def parser; reads the seven paint keys into the def struct |
| `FUN_0047c210` | Assembles a spawn's scheme record from the override record and the def |
| `FUN_0057a090` | The def's by-name property lookup, called once per key |
| `FUN_00414f40` | Copies the finished scheme into a dispatched message (`FUN_004084a0(8, …)`) |
| `0x0071db08` | The global default scheme, 17 dwords, copied wholesale on one branch |

## The def's paint slots

`FUN_00479240` looks each key up by name and writes a fixed slot. The parse is strictly positional:
no re-ordering, no indirection, no cross-slot logic.

| Key | String | Read at | Def slot |
|---|---|---|---|
| `paint_pattern` | `0x006281b0` | `0x0047b384` | `+0x220`, as a string object |
| `paint_decal1` | `0x006281c0` | `0x0047b3a9` | `+0x230` |
| `paint_decal2` | `0x006281d0` | `0x0047b3c7` | `+0x234` |
| `paint_decal3` | `0x006281e0` | `0x0047b3e5` | `+0x238` |
| `paint_color1` | `0x006281f0` | `0x0047b403` | `+0x23c`, `+0x240`, `+0x244` |
| `paint_color2` | `0x00628200` | `0x0047b439` | `+0x248`, `+0x24c`, `+0x250` |
| `paint_color3` | `0x00628210` | `0x0047b46f` | `+0x254`, `+0x258`, `+0x25c` |

A colour's three components come off the property's list elements at `+0xc`, `+0x14` and `+0x1c`
and go to three consecutive dwords in authored order. Every key's block is guarded by a
`TEST EAX, EAX; JZ` that skips the whole block, so a def authoring none of them leaves the slots as
the struct was initialised and inherits nothing by name.

## Assembling a spawn's scheme

`FUN_0047c210` (`0x0047de45`–`0x0047dfc5`) builds a 17-dword scheme record on the stack: the
pattern, one further field, the three decals, then the three packed colours. Two paths reach it.

**The default-scheme path.** When the byte flag at `[EBP+0xb]` is set, the whole record is
`REP MOVSD`-copied from the global at `0x0071db08` (`0x0047de4c`) and the vehicle def is never
consulted at all.

**The override-over-def path.** Otherwise every field is taken from a per-instance override record
(fields `+0x124` through `+0x150`), falling back to the def's slot when the override is unset. The
fallback is decided **per field, not per colour**: each of the nine colour components tests
independently, so a scheme can inherit a def's red and green while overriding its blue.

The two sentinels are not the same, and treating them as one gets decals wrong:

- **Decals** count only values below `-1` as unset (`CMP ECX, -1; JL` at `0x0047de6c`, `0x0047de91`,
  `0x0047deb3`). `-1` is a legal override meaning "no decal in this slot".
- **Colour components** count any negative as unset (`TEST ECX, ECX; JGE` at `0x0047decf`,
  `0x0047dee8` and the six that follow).

So an aircraft whose override record leaves a field unset wears its **def's** authored value there.
That is the path an AI aircraft takes: its militia def's authored scheme is what it flies in unless
something explicitly sets a field, or the default-scheme branch is taken.

## Packing a colour

Each colour's three components are packed into one dword (`0x0047df0c`–`0x0047df1b`, and the two
repetitions that follow):

    packed = (third << 16) | (second << 8) | first

The first authored component ends up in the low byte. Each component is masked to `0xff` on the way
in, so an out-of-range authored value wraps rather than clamping.

## What this page does not settle

The scheme record is handed to `FUN_00414f40`, which copies it into a message built by
`FUN_004084a0(8, …)` and dispatches it. Which painted region each colour slot reaches is decided in
that message's handler, past the end of this decode. The slot-to-region reading in
[`formats/paint.md`](../formats/paint.md) ("Slot order, confirmed") therefore still rests on the
render comparison and the paint UI's Colour/Shade reading, with this page adding only the
independent point that nothing between `vehicle.json` and the scheme record permutes the three.

## Where CSVM differs

- CSVM has no per-instance override record. A player scheme comes from the launch menu or `--paint=`
  and an AI scheme from the def or the livery picker, so the per-field fallback above has no
  equivalent yet; it becomes relevant only when a mission or a saved plane can override one
  component of an otherwise authored scheme.
- CSVM keeps colours as an RGB triple rather than one packed dword, so the packing formula is a
  decode of the original's storage, not a shape to reproduce.
