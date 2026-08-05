# Release zip manifest (for the B13 assembler, not the recipient)

The friend-release zip is assembled by hand from these pieces. Everything sits at the zip
root except `tools\unzbd.exe`; the friend unzips, runs `Extract.ps1`, then `CSVM.exe`.

| Zip path | Source | Notes |
|---|---|---|
| `CSVM.exe` + export payload (`.pck`/`.dll`s etc.) | B11's Godot release export output | Whatever the export produces at its root, copied verbatim |
| `Extract.ps1` | `packaging/Extract.ps1` | The thin dispatcher; contains no extraction logic |
| `ExtractAssets.ps1` | repo root `ExtractAssets.ps1` | UNMODIFIED repo script — do not fork a package variant |
| `ExtractRof.ps1` | repo root `ExtractRof.ps1` | UNMODIFIED repo script — do not fork a package variant |
| `tools\unzbd.exe` | `Z:\CSVM\tools\mech3ax\target\release\unzbd.exe` | The fork build (branch `cs-anim`), NOT the pinned v0.6.1 binary (Decision 5) |
| `README.md` | `packaging/README.md` | User-reviewed before hand-off (standing rule: the user owns outward communication) |
| `LICENSE` | `packaging/LICENSE` | GPL-3, byte-identical to repo root `LICENSE` |
| `LICENSE-unzbd` | `packaging/LICENSE-unzbd` | EUPL-1.2, byte-identical to `tools/mech3ax/LICENSE` |

Not in the zip, created on the friend's machine by `Extract.ps1`: `extracted\` (game data —
must never ship; the zip contains zero game assets by construction).

Assembly checks before zipping:

- `tools\unzbd.exe` is the fork build: `unzbd.exe --version` shows the fork's build
  timestamp, and its SHA-256 should match the `unzbdSha256` a dev-tree extraction stamps
  into `extracted/VERSION.json`.
- The two extractor scripts are byte-identical to the repo-root versions at the release
  commit (`git diff --no-index` them).
- `LICENSE` / `LICENSE-unzbd` hashes match their sources (`Get-FileHash`).
