# Release zip manifest (for the assembler, not the recipient)

The release zip holds these pieces. The recipient unzips and double-clicks `CSVM.exe`, which finds the player's game files (an install, a
drive, an `.iso` or a `.cue`/`.img`) and reads them in place. The zip's one script is `CSVM-VR.cmd`,
which starts `CSVM.exe` in VR; nothing in the setup needs it.

`ExportRelease.ps1` assembles it: it exports into `.scratch\export\`, copies the rows below in
beside the export output, and zips that folder to `.scratch\CSVM-v<version>-win64.zip`, the version
being `application/config/version` from `CSVM/project.godot` — the same number the exe's file
properties and the first line of every log state. This file is the
statement of what belongs in the zip; the script is what performs it, so a copied row added here
needs a matching entry in the script's `$ReleaseFiles`. Two rows are not copies and say so in
their Source column: the export's own output, and `BUILD-INFO.txt`, whose content differs on
every run and therefore has no repo home to be copied from. Two rows are in the table without
being in the zip at all, `LICENSE-plmpeg` and `LICENSE-promptfont`, because they are inputs the
notices file is assembled from; their Zip path column names them as no zip row and they take no
`$ReleaseFiles` entry.

| Zip path | Source | Notes |
|---|---|---|
| `CSVM.exe` + export payload (`.pck`/`.dll`s etc.) | the Godot release export output | Whatever the export produces at its root, copied verbatim; the exe's file properties carry the version |
| `SDL2.dll` | `tools/sdl2/SDL2.dll` | The official libsdl-org SDL 2.32.10 Windows x64 runtime, which the game reads flight sticks through. Installed by `InstallSdl2.ps1` against a pinned SHA-256; the game loads it from beside the exe (`docs/tooling.md`, "SDL2 for flight sticks") |
| `README-SDL.txt` | `tools/sdl2/README-SDL.txt` | From the same release zip, which asks for it to be distributed with the runtime |
| `LICENSE-SDL2.txt` | `tools/sdl2/LICENSE.txt` | SDL's zlib licence, read by `InstallSdl2.ps1` from the release's own commit because the runtime zip carries none; renamed so it does not read as CSVM's licence |
| `libwebrtc_native.windows.template_release.x86_64.dll` | the Godot release export output, from `CSVM/addons/webrtc_native/` | webrtc-native 1.2.2, the transport internet play runs over (`docs/tooling.md`, "The WebRTC library and the master server"). The export runs `InstallWebRtc.ps1` into the exported tree first, so the pinned, hash-checked release is always the one shipped, and throws if the export left the library out |
| `LICENSE-webrtc\LICENSE.*` | `CSVM/addons/webrtc_native/LICENSE.*` | The seven licence files the webrtc-native release carries: its own (MIT), libdatachannel and libjuice (MPL-2.0), libsrtp, usrsctp (BSD), mbedtls (Apache-2.0) and plog (MIT). `BUILD-INFO.txt` names the release tag, which is where the MPL-2.0 source is |
| `README.md` | `packaging/README.md` | The one document a downloader reads: the releases page and the zip's SHA-256, the requirements including the renderer floor, choosing the game files and the first flight, the log and save locations, what the other files here are, and where to report a problem. Author-reviewed before any hand-off or release (standing rule: the author owns outward communication) |
| `CSVM-VR.cmd` | `packaging/CSVM-VR.cmd` | The VR launcher: `start` runs `CSVM.exe` beside it, in its own folder, with the engine argument `--xr-mode on` ahead of whatever it is given, so `CSVM-VR.cmd -- <flags>` reaches the game's own arguments. `.gitattributes` checks it out CRLF on every OS. Windows only: Linux VR is not supported, so the tarball has no launcher |
| `LICENSE` | `packaging/LICENSE` | GPL-3, byte-identical to repo root `LICENSE` |
| `LICENSE-thirdparty.txt` | `packaging/LICENSE-thirdparty.txt` | The notices the executable's contents oblige the zip to carry: the Godot engine, its own third-party components and the self-contained .NET runtime, plus one notice for source rather than payload, `pl_mpeg`, which CSVM's managed MPEG-1 decoder is ported from, and the SIL Open Font License for PromptFont, the font packed into the export that the pad glyphs are drawn from. Its header carries the one line crediting [TerranMechworks/mech3ax](https://github.com/TerranMechworks/mech3ax). Assembled by `packaging/BuildThirdPartyNotices.ps1` from the shipped artefacts themselves; its header names the Godot build, the .NET runtime version and the `win-x64` runtime pack it speaks for, and the export refuses to ship it against any other |
| none, not a zip row | `packaging/LICENSE-plmpeg` | An input to the row above, not a payload: `BuildThirdPartyNotices.ps1` reads it into section 6 of the notices file, so it ships inside that file and never beside it. It holds the MIT terms because `pl_mpeg` declares MIT by SPDX identifier alone and publishes no licence file for the script to read out of an artefact |
| none, not a zip row | `packaging/LICENSE-promptfont` | An input to the notices row, not a payload: `BuildThirdPartyNotices.ps1` reads it into section 7, under the copyright statement it reads out of `CSVM/data/promptfont.ttf.bin`'s own name table. Byte-identical to the `LICENSE.txt` in PromptFont's release, which is the bare OFL text with no copyright line of its own |
| `BUILD-INFO.txt` | GENERATED by `ExportRelease.ps1` | Which commit each shipped binary was built from: CSVM's `HEAD`, with whether its worktree was clean, the SDL release, commit and DLL hash `SDL2.dll` came from, and the webrtc-native release and library hash. `PublishRelease.ps1` reads it and refuses a release whose worktree was not clean |

## Linux tarball

`ExportRelease.ps1 -Linux` also exports the `Linux/X11` preset into `.scratch\export-linux\` and
packs it inside WSL (Debian) as `.scratch\CSVM-v<version>-linux-x64.tar.gz`, entries at the
archive root like the zip's. It is the zip's payload with the Linux README and the Linux notices
in place of the Windows ones, its text files staged LF (the bytes git stores; the
zip keeps the checkout's CRLF), without the VR launcher, and without the three SDL2 rows: the Linux build reads flight sticks
through the system's `libSDL2-2.0.so.0` (on SteamOS, sdl2-compat over SDL3), found through the
system loader, and runs without sticks when it is absent. The tarball ships no SDL2, so its
`BUILD-INFO.txt` has no SDL block (`docs/tooling.md`, "SDL2 for flight sticks"). Modes are set in the archive, root-owned: `0755` for the executable and every
directory, `0644` for everything else, and the script reads the executable's mode back out of
the archive before it reports success.

| Tarball path | Source | Notes |
|---|---|---|
| `CSVM.x86_64` + `data_CSVM_linuxbsd_x86_64/` | the Godot `Linux/X11` release export output | `0755`; the pck is embedded as in the Windows exe. The data folder holds the self-contained linux-x64 .NET runtime |
| `README.md` | `packaging/README-linux.md` | The Linux README: the community-tested label, requirements (x86_64, Vulkan), unpacking with the executable bit, where the original game's files come from, choosing them in the game, the "On Steam Deck" section, logs, the settings folder and the licences. Author-reviewed before release like the zip's README; `$LinuxReadme` in the script names it |
| `libwebrtc_native.linux.template_release.x86_64.so` | the Godot `Linux/X11` release export output | `0644`; the Linux library of the same webrtc-native release as the zip's DLL |
| `LICENSE`, `LICENSE-webrtc/LICENSE.*` | as in the zip | |
| `LICENSE-thirdparty.txt` | `packaging/LICENSE-thirdparty-linux.txt` | The zip's notice assembled from the Linux payload instead, by `BuildThirdPartyNotices.ps1 -Linux`: the same Godot sections (the engine compiles them from one `COPYRIGHT.txt` on every platform, and the script checks that the Linux template reports the editor's build) and the `linux-x64` runtime pack's own `LICENSE.TXT` and `THIRD-PARTY-NOTICES.TXT`. Its header stamps the `linux-x64` pack; the export refuses it unless that and the two version stamps match, and refuses it if it names any Windows payload file or pack. No SDL section: the tarball ships no SDL2 |
| `BUILD-INFO.txt` | GENERATED, as in the zip | The same commit, with the Linux file names and LF line endings. `PublishRelease.ps1` reads it out of the tarball and refuses the same qualifier as the zip's |

The archives hold no game data: CSVM reads the player's own game files in place, and its
optional copy of them lands in the data folder on the player's machine. The zip contains zero
game assets by construction.

Assembly checks before hand-off. The copies are taken from the sources above on every export,
so byte-identity is by construction:

- `LICENSE-thirdparty.txt` needs no manual check: the export reads the Godot build and .NET runtime
  version out of each notice's header and throws if either disagrees with what it is packaging, or
  if a notice's runtime pack stamp is not its platform's. Regenerate with
  `packaging\BuildThirdPartyNotices.ps1` (add `-Linux` for the tarball's) when one does; a `-Linux`
  run rewrites both files. `sandbox\LinuxRelease.ps1`'s payload stage repeats the Linux half on the
  built tarball's own copy. The `pl_mpeg` section carries no such stamp and needs none: it speaks for
  source CSVM's own code was ported from, which moves with CSVM's commit rather than with a payload
  version, and `BUILD-INFO.txt` already records that commit.
- The three SDL2 rows need no manual check: the export runs `InstallSdl2.ps1 -Verify`, which
  throws unless all three match their pinned SHA-256, and `BUILD-INFO.txt` records the SDL version,
  commit and DLL hash.
- The WebRTC rows need no manual check either: the export installs the pinned release, which
  `InstallWebRtc.ps1` refuses unless its SHA-256 matches, and throws if the exported folder holds
  no library.
- Everything in the staging folder goes into the zip, so the script deletes `.scratch\export\`
  at the start of every run: what is in the archive is what that run produced, never a leftover
  from an earlier export or a hand assembly.
