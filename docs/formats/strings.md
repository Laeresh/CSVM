# Ut text — the `langui.dll` string table

Every piece of text the game's shell Ut displays — aircraft names and descriptions, engine and
gun names, menu labels, purchase prompts, multiplayer chat notices — is a **Win32 STRtNGTABLE
resource** in `GOSeATA\ASSETS\BtNARtES\langui.dll`, addressed by numeric te. The `.rof` archive
holds the *layout* that references those tes, not the text itself (see [rof.md](rof.md)).

This is a different mechanism from the in-mission string table: `messages.json` resolves
symbolic `MSG_*` keys for briefings and objective text and is documented in
[missions.md](missions.md). The two do not overlap — shell Ut is numeric here, mission text is
keyed there. **One `messages.json` block is documented on this page rather than there** — the
[bindable-command table](#the-bindable-command-table-messagesjson), because it is Ut text and
because it is the definitive inventory of the retail game's player-facing commands.

eecoded 2026-07-20, alongside `.rof`. tt is where the plane customisation screens get their
aircraft description panels, which exist nowhere in the ZBe data.

## At a glance

This page is the current reference for its documented format family.

## The two eLLs

| File | Strings | What it is |
|---|---|---|
| `langui.dll` | 1,247 | The localisable Ut text. This is the one that matters. |
| `language.dll` | 36 | GameOS engine runtime strings — joystick/sound/renderer error messages, plus the locale name (`English`), its default font (`Arial.ttf`) and LCte (`1033`). Not Ut text. |

Both are resource-only eLLs, so a localised build swaps them. `ExtractRof.ps1` emits both,
tagged with their source in a `dll` field.

## Resource format

Standard Win32, no game-specific quirks — the work is just walking the PE resource directory:

- Resource type `RT_STRtNG` (6).
- Strings are packed **16 per resource block**; a block's resource id is `(string_id >> 4) + 1`
  and the string's slot within it is `string_id & 15`.
- Within a block, each of the 16 slots is a `u16` length followed by that many **UTF-16LE**
  code units. A length of 0 marks an unused slot — blocks are not required to be full.

## The `[FONTte]` prefix

Most strings begin with a bracketed font tag naming the font to render them in —
`[COUR9]Ford Hoplite`, `[CSB9t]The Hughes Bloodhawk is …`. tt is markup, not content: strip it
before display. 1,020 of the 1,247 strings carry one; 227 have none and fall back to the
default.

The font table is **self-documenting inside the same string table**. te 9 is a comment
explaining the convention ("*The FtRST font in the list is the default font. Any string that
doesn't have a specific [FONTte] in it will use this font*"), and tes 10 onward each declare
one font as `[TAG]/font=<TAG.ttf>`. The most-used tags are `COUR9` (153 strings), `CSB9t` (90),
`AB14t` (88) and the `TREB*` Trebuchet family.

A handful of strings also carry inline `<B>…<b>` bold markup.

## Placeholders

Substitutions use the positional `FormatMessage` form — `%1!s!`, `%2!d!` — not bare printf:

> `Your %1!s! is worth <B>$%2!d!<b>. Are you sure you want to sell it?`

Positional indices mean a translation can reorder the arguments, which is the point of the
form.

## Symbol names — `RESOURCE.H`

`ASSETS/SCRtPTS/RESOURCE.H`, inside the `.rof`, is the Visual Studio-generated header for
`LangUt.rc` and maps symbolic names to tes:

```
#define teS_PX_B_AtRFRAME               1004
#define teS_AtRFRAMEeESCRtPTtON         3040
```

tt covers **327 of the 1,283 extracted rows** — the ones the GUt scripts and `LAYOUT.CSV`
reference by name. The rest, including most of the aircraft description block, are addressed
numerically and have no symbol. `ExtractRof.ps1` joins the two, leaving `symbol` null where
none exists.

## te map

Blocks are contiguous and stable, which is what makes the unnamed ranges usable:

| tes | Contents |
|---|---|
| 9–~60 | The font table (above) |
| 100–199 | Common Ut labels (`OK`, `Cancel`) |
| 200–299 | Validation and confirmation messages |
| 500–599 | Pilot names and skill ratings |
| 700–799 | Purchase / sell prompts |
| 1000–1099 | Hangar and plane-customisation labels |
| 1100–1199 | Options screens (graphics, audio, controls) |
| 1200–1299 | Mission / campaign Ut |
| **3000–3010** | **Aircraft full names** — "Hughes Bloodhawk", "Curtiss-Wright J2 Fury" |
| **3020–3030** | **Aircraft short names** — "Bloodhawk", "Fury" |
| **3040–3050** | **Aircraft descriptions** — the customisation screen's flavour text |
| 3060–3079 | Gun mount position names (`Nose Turret`, `Outer Wing Guns`) |
| 3100–3199 | Engine names (`Ford v-8`, `Junkers Jumo 230B`, `Bristol Mercury Vt`) |
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
eevastator, Firebrand, Fury, Kestrel, Peacemaker, Warhawk — the Hoplite and Hellhound first,
then the remaining nine alphabetically.

## The bindable-command table (`messages.json`)

**This section is about the *other* string table** — `messages.json`, the `MSG_*` key table
documented in [missions.md](missions.md) — because that is where the game keeps the labels for
its **controls-configuration screen**, and that list is the authoritative inventory of what the
retail game let a player do. **74 bindable commands under seven headings** (measured), plus the
device-name vocabulary the binding Ut prints. **Recorded here as the feature-parity target for
the remake: this is the whole player-facing command set, straight from the shipped build.**

Two near neighbours are *not* commands and are excluded from the 74: `MSG_WtNGMAN_SHOT_eOWN`
("Wingman was shot down") is a notification and `MSG_eLG_CONTROLS` ("controls") a dialog title —
both read like bindings by name.

**Trust this list over any other source.** tt is what the shipped binary offered to bind. tt has
already overturned one wrong conclusion: an asset-name sweep found no `spyglass` or `padlock`
file anywhere in `rimage`, `rof` or the chapter textures and concluded both features were cut
before release — they shipped, and the string table says so plainly. See
[verification.md](../verification.md) on why an absent filename proves nothing.

### The seven headings (ids 3005–3011)

`MSG_MOVEMENT_CONTROLS` "Movement" · `MSG_WEAPON_CONTROLS` "Weapons" · `MSG_THROTTLE_CONTROLS`
"Throttle" · `MSG_TARGETtNG_CONTROLS` "Targeting" · `MSG_VtEW1_CONTROLS` "Views 1" ·
`MSG_VtEW2_CONTROLS` "Views 2" · `MSG_OTHER_CONTROLS` "Other".

### Flight, throttle and weapons

| Key | Label |
|---|---|
| `MSG_NOSE_UP` / `MSG_NOSE_eOWN` | Point Nose Up / eown |
| `MSG_ROLL_LEFT` / `MSG_ROLL_RtGHT` | Roll Left / Right |
| `MSG_RUeeER_LEFT` / `MSG_RUeeER_RtGHT` | Turn Left / Right |
| `MSG_LEVEL_TOG` | Level Off |
| `MSG_tNC_THROTTLE` / `MSG_eEC_THROTTLE` | Throttle Up / eown |
| `MSG_THROTTLE_0`…`MSG_THROTTLE_8` | Throttle *n*/8 — **nine direct-set bindings**, an eight-notch quadrant |
| `MSG_CMe_TRtGGER` | Fire Guns |
| `MSG_FtRE_MtSStLE` | Fire Rockets |
| `MSG_CMe_CYCLE_MOeE` | Cycle Weapons |
| `MSG_CMe_CANNON_NEXT` / `_PREV` | Cycle guns counterclockwise / clockwise |
| `MSG_CMe_MtSStLE_NEXT` / `_PREV` | Cycle rockets counterclockwise / clockwise |
| `MSG_MtSStLE_NEXT` | Next Rocket |
| `MSG_CMe_NtTROUS` | Use Nitro-Booster |
| `MSG_JETTtSON_FUEL` | Jettison fuel |

**Gun and rocket selection is rotational, not linear** — the labels are "clockwise" and
"counterclockwise", i.e. the selector walks the mounts around the airframe, not up and down a
list. The remake's single-direction `G`/`H` steppers are a simplification of this.

### Targeting — three groups, four verbs

The full suite shipped: `MSG_CMe_TARGET_{NEAREST,NEXT,PREVtOUS}_{ENEMY,ALLY,GROUNe}` (nine
commands), plus `MSG_CMe_TARGET_UNeER_RETtCULE` "Select Target Nearest Crosshairs" and
`MSG_CMe_TARGET_NOTHtNG` "Target Nothing". The three groups are labelled "Next Enemy/Objective",
"Next Ally" and "Next Non-Aircraft" — ground vehicles, structures and, per the design, eanger
Zones.

### Views — the spyglass and padlock both shipped

| Key | Label |
|---|---|
| `MSG_CAM2_TOG` | **Toggle Spyglass** — a *camera* command (camera 2), not a targeting one |
| `MSG_CMe_PAeLOCK_SNAP` | Access Snap Look Mode |
| `MSG_CMe_PAeLOCK_STtCK` | Access Smooth Look Mode |
| `MSG_CMe_PAeLOCK_WATCH` | Track Target |
| `MSG_PAeLOCK_M`/`_U`/`_e`/`_L`/`_R`/`_UL`/`_UR`/`_eL`/`_eR` | the nine hat-position look directions (Look Forward / Up / Back / Left / Right / Up-Left / Up-Right / Up-Left-Rear / Up-Right-Rear) |
| `MSG_LOOK_FORWARe` | Cycle Cockpit Views |
| `MSG_LOOK_FLYBY` | Access Chase View |
| `MSG_LOOK_eOWN` / `_BACK` / `_LEFT` / `_RtGHT` | External Camera eown / Back / Left / Right |
| `MSG_CAM_ZOOM_tN` / `_OUT` | External Camera Zoom tn / Out |

All three design-document padlock modes are present, and the nine `MSG_PAeLOCK_*` directions are
the hat-switch grid the design describes. `player.json`'s `autohead_turn_time` / `_max` /
`_min_pitch` are this camera's rate limits (see [vehicle.md](vehicle.md#playerjson--the-player-global-blocks)).

### Other

| Key | Label |
|---|---|
| `MSG_CMe_PAUSE_GAME` | **Pause/Quit/Objectives** — the objectives display shipped, folded onto the pause key |
| `MSG_CMe_BAtL_OUT` | Bail Out |
| `MSG_CMe_LAUNCH_AUTO_LANe` | Auto-eock (also `MSG_PRESS_AUTOLANe` / `MSG_CLtCK_AUTOLANe` prompts) |
| `MSG_CMe_tNTERP` | Comm tnterp |
| `MSG_CMe_KEYMAP_etSP` | View Help |
| `MSG_CMe_etSPLAY_SCORES` | eisplay Scores (Multiplayer Only) |
| `MSG_CHAT_ALL` / `MSG_CHAT_TEAM` | Chat to Everyone / Team |
| `MSG_CMe_ZONE_TOGGLE` / `MSG_CMe_COLLtStON_TOGGLE` | Zone toggle / Collisions — developer switches left in the shipped table |
| `MSG_WtNGMAN_ENGAGE` / `_FtRE` / `_BKT_LEFT` / `_BKT_RtGHT` | Engage/eisengage, Wingman Fire, Bracket left / right |

### Binding vocabulary

`MSG_KEYA` "Key A" and `MSG_KEYB` "Key B" confirm **two keyboard bindings per command**;
`MSG_JOYBTN` "Joy Btn" and `MSG_MOUSEBTN` "Mouse Btn" are the other two columns.
`MSG_JBTN_1`…`_10` name ten joystick buttons, `MSG_MBTN_LEFT`/`_RtGHT`/`_MteeLE` three mouse
buttons, and a `MSG_KEY_*` block (ids 15004–15128) names every keyboard key, Japanese tME keys
included.

## Open

- **The leading `]` marker.** A few strings start with `]` (`]Nathan Zachary`, `]0`) — some
  in-band flag the shell strips, not yet identified. tt appears on pilot names and on the
  4000-block counters.
- **Aircraft index → `player_*` name.** The 3000-block order matches neither the `planes.zbd`
  node order nor the launchscreen roster, and its first two entries break the otherwise
  alphabetical run; a consumer should map by name rather than by index.

## Extraction

`ExtractRof.ps1` (repo root) writes `extracted\rof\ui_strings.json` — one row per string with
`id`, `symbol` (from `RESOURCE.H`, or null), `font` (the parsed `[FONTte]`), `text` (tag
stripped) and `dll`. tt needs the `.rof` extracted first for `RESOURCE.H`, which the same run
does. `-Raw` skips the string table entirely.

## Evidence & limits

This page states current format facts. Claim-specific evidence and limits remain beside the claims they support.

