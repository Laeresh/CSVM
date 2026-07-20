# UI text — the `langui.dll` string table

Every piece of text the game's shell UI displays — aircraft names and descriptions, engine and
gun names, menu labels, purchase prompts, multiplayer chat notices — is a **Win32 STRINGTABLE
resource** in `GOSDATA\ASSETS\BINARIES\langui.dll`, addressed by numeric ID. The `.rof` archive
holds the *layout* that references those IDs, not the text itself (see [rof.md](rof.md)).

This is a different mechanism from the in-mission string table: `messages.json` resolves
symbolic `MSG_*` keys for briefings and objective text and is documented in
[missions.md](missions.md). The two do not overlap — shell UI is numeric here, mission text is
keyed there.

Decoded 2026-07-20, alongside `.rof`. It is where the plane customisation screens get their
aircraft description panels, which exist nowhere in the ZBD data.

## The two DLLs

| File | Strings | What it is |
|---|---|---|
| `langui.dll` | 1,247 | The localisable UI text. This is the one that matters. |
| `language.dll` | 36 | GameOS engine runtime strings — joystick/sound/renderer error messages, plus the locale name (`English`), its default font (`Arial.ttf`) and LCID (`1033`). Not UI text. |

Both are resource-only DLLs, so a localised build swaps them. `ExtractRof.ps1` emits both,
tagged with their source in a `dll` field.

## Resource format

Standard Win32, no game-specific quirks — the work is just walking the PE resource directory:

- Resource type `RT_STRING` (6).
- Strings are packed **16 per resource block**; a block's resource id is `(string_id >> 4) + 1`
  and the string's slot within it is `string_id & 15`.
- Within a block, each of the 16 slots is a `u16` length followed by that many **UTF-16LE**
  code units. A length of 0 marks an unused slot — blocks are not required to be full.

## The `[FONTID]` prefix

Most strings begin with a bracketed font tag naming the font to render them in —
`[COUR9]Ford Hoplite`, `[CSB9I]The Hughes Bloodhawk is …`. It is markup, not content: strip it
before display. 1,020 of the 1,247 strings carry one; 227 have none and fall back to the
default.

The font table is **self-documenting inside the same string table**. ID 9 is a comment
explaining the convention ("*The FIRST font in the list is the default font. Any string that
doesn't have a specific [FONTID] in it will use this font*"), and IDs 10 onward each declare
one font as `[TAG]/font=<TAG.ttf>`. The most-used tags are `COUR9` (153 strings), `CSB9I` (90),
`AB14I` (88) and the `TREB*` Trebuchet family.

A handful of strings also carry inline `<B>…<b>` bold markup.

## Placeholders

Substitutions use the positional `FormatMessage` form — `%1!s!`, `%2!d!` — not bare printf:

> `Your %1!s! is worth <B>$%2!d!<b>. Are you sure you want to sell it?`

Positional indices mean a translation can reorder the arguments, which is the point of the
form.

## Symbol names — `RESOURCE.H`

`ASSETS/SCRIPTS/RESOURCE.H`, inside the `.rof`, is the Visual Studio-generated header for
`LangUI.rc` and maps symbolic names to IDs:

```
#define IDS_PX_B_AIRFRAME               1004
#define IDS_AIRFRAMEDESCRIPTION         3040
```

It covers **327 of the 1,283 extracted rows** — the ones the GUI scripts and `LAYOUT.CSV`
reference by name. The rest, including most of the aircraft description block, are addressed
numerically and have no symbol. `ExtractRof.ps1` joins the two, leaving `symbol` null where
none exists.

## ID map

Blocks are contiguous and stable, which is what makes the unnamed ranges usable:

| IDs | Contents |
|---|---|
| 9–~60 | The font table (above) |
| 100–199 | Common UI labels (`OK`, `Cancel`) |
| 200–299 | Validation and confirmation messages |
| 500–599 | Pilot names and skill ratings |
| 700–799 | Purchase / sell prompts |
| 1000–1099 | Hangar and plane-customisation labels |
| 1100–1199 | Options screens (graphics, audio, controls) |
| 1200–1299 | Mission / campaign UI |
| **3000–3010** | **Aircraft full names** — "Hughes Bloodhawk", "Curtiss-Wright J2 Fury" |
| **3020–3030** | **Aircraft short names** — "Bloodhawk", "Fury" |
| **3040–3050** | **Aircraft descriptions** — the customisation screen's flavour text |
| 3060–3079 | Gun mount position names (`Nose Turret`, `Outer Wing Guns`) |
| 3100–3199 | Engine names (`Ford v-8`, `Junkers Jumo 230B`, `Bristol Mercury VI`) |
| 3200–3299 | Gun and ammunition names |
| 3300–3399 | Engine / component descriptions |
| 3400–3499 | Hardpoint and effect names |
| 3500–3599 | Campaign act titles |
| 3600–3699 | Mission names |
| 3700–3799 | Squadron names (`Hoplites`, `Hellhounds`) |
| 10000–10599 | Multiplayer: lobby, game types, chat notices |
| 20000+ | Key names for the controls screen |

The three aircraft blocks are **parallel and in the same order**, so
`full_name = 3000 + i`, `short_name = 3020 + i`, `description = 3040 + i` for the same
aircraft `i` — eleven entries, matching the eleven `player_*` aircraft in `planes.zbd`
(see [gamez.md](gamez.md)). Order is Hoplite, Hellhound, Balmoral, Bloodhawk, Brigand,
Devastator, Firebrand, Fury, Kestrel, Peacemaker, Warhawk — the Hoplite and Hellhound first,
then the remaining nine alphabetically.

## Open

- **The leading `]` marker.** A few strings start with `]` (`]Nathan Zachary`, `]0`) — some
  in-band flag the shell strips, not yet identified. It appears on pilot names and on the
  4000-block counters.
- **Aircraft index → `player_*` name.** The 3000-block order matches neither the `planes.zbd`
  node order nor the launchscreen roster, and its first two entries break the otherwise
  alphabetical run; a consumer should map by name rather than by index.

## Extraction

`ExtractRof.ps1` (repo root) writes `extracted\rof\ui_strings.json` — one row per string with
`id`, `symbol` (from `RESOURCE.H`, or null), `font` (the parsed `[FONTID]`), `text` (tag
stripped) and `dll`. It needs the `.rof` extracted first for `RESOURCE.H`, which the same run
does. `-Raw` skips the string table entirely.
