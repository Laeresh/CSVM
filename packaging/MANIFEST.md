# Release zip manifest (for the B13 assembler, not the recipient)

The friend-release zip holds these pieces. Everything sits at the zip root except
`tools\unzbd.exe`; the friend unzips, runs `Extract.ps1`, then `CSVM.exe`.

`ExportRelease.ps1` assembles it: it exports into `.scratch\export\`, copies the rows below in
beside the export output, and zips that folder to `.scratch\CSVM.zip`. This file is the
statement of what belongs in the zip; the script is what performs it, so a row added here
needs a matching entry in the script's `$ReleaseFiles`.

| Zip path | Source | Notes |
|---|---|---|
| `CSVM.exe` + export payload (`.pck`/`.dll`s etc.) | B11's Godot release export output | Whatever the export produces at its root, copied verbatim |
| `Extract.ps1` | `packaging/Extract.ps1` | The thin dispatcher; contains no extraction logic |
| `ExtractAssets.ps1` | repo root `ExtractAssets.ps1` | UNMODIFIED repo script — do not fork a package variant |
| `ExtractRof.ps1` | repo root `ExtractRof.ps1` | UNMODIFIED repo script — do not fork a package variant |
| `ExtractRof.MenuLayout.cs` | repo root `ExtractRof.MenuLayout.cs` | The menu-layout decoder `ExtractRof.ps1` `Add-Type`s from beside itself; without it the extraction fails on the friend's machine |
| `tools\unzbd.exe` | `Z:\CSVM\tools\mech3ax\target\release\unzbd.exe` | The fork build (branch `cs-anim`), NOT the pinned v0.6.1 binary (Decision 5) |
| `README.md` | `packaging/README.md` | User-reviewed before hand-off (standing rule: the user owns outward communication) |
| `LICENSE` | `packaging/LICENSE` | GPL-3, byte-identical to repo root `LICENSE` |
| `LICENSE-unzbd` | `packaging/LICENSE-unzbd` | EUPL-1.2, byte-identical to `tools/mech3ax/LICENSE` |

Not in the zip, created on the friend's machine by `Extract.ps1`: `extracted\` (game data —
must never ship; the zip contains zero game assets by construction).

Assembly checks before hand-off. The copies are taken from the sources above on every export,
so byte-identity is by construction and only the unzbd build needs confirming:

- `tools\unzbd.exe` is the fork build: `unzbd.exe --version` shows the fork's build
  timestamp, and its SHA-256 should match the `unzbdSha256` a dev-tree extraction stamps
  into `extracted/VERSION.json`. The script checks the path exists, not which build it is.
- Everything in the staging folder goes into the zip, so the script deletes `.scratch\export\`
  at the start of every run: what is in the archive is what that run produced, never a leftover
  from an earlier export or a hand assembly.
