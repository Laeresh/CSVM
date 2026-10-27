# Modding the game's archives

CSVM reads the original's files in place. To change what an archive holds, extract it with unzbd,
edit the output, and name the folder holding it as the **modding folder**. Each archive the folder
holds is then read from there; every other archive still comes from the game files.

## The unzbd build

CSVM reads the output of the unzbd build CSVM v0.4.0 bundled (`tools\unzbd.exe` in that release),
mech3ax's extractor with its Crimson Skies (`cs`) modes. CSVM no longer ships it. One archive is
extracted with

```
unzbd cs <mode> <input> <output>
```

| Archive in the install | Mode | Output in the modding folder |
|---|---|---|
| `strings.dll` | `messages` | `messages.json` |
| `ZBD/interp.zbd` | `interp` | `interp.json` |
| `ZBD/planes.zbd` | `gamez` | `planes.zip` |
| `ZBD/<chapter>/gamez.zbd` | `gamez` | `<chapter>/gamez.zip` |
| `ZBD/rimage.zbd` | `textures` | `rimage.zip` |
| `ZBD/<chapter>/rtexture<N>.zbd` | `textures` | `<chapter>/rtexture<N>.zip` |
| `ZBD/soundsh.zbd` | `sounds` | `soundsh.zip` |
| `ZBD/zrdr.zbd` | `reader` | `zrdr.zip` |
| `ZBD/<chapter>/zrdr.zbd` | `reader` | `<chapter>/zrdr.zip` |
| `ZBD/<chapter>/<mission>/zrdr.zbd` | `reader` | `<chapter>/<mission>/zrdr.zip` |
| `ZBD/<chapter>/cam_anim.zbd` | `anim` | `<chapter>/cam_anim.zip` |
| `ZBD/<chapter>/<mission>/mis_anim.zbd` | `anim` | `<chapter>/<mission>/mis_anim.zip` |

The rule behind the table: the install path without `ZBD/`, with `.zip` for `.zbd`. Folder names
are upper case (`C1B`, `M02`) and file names lower case, which matters on Linux.

A zip may be unpacked into a folder of the same name beside it (`C1/gamez/` for `C1/gamez.zip`),
which is easier to edit; the folder is read in preference to the zip. Only the files a mod changes
need to be there.

## Which archives CSVM reads

- **Textures.** A chapter renders one texture archive, its highest `rtexture<N>` tier (C1's is
  `rtexture15`), else `texture.zbd`. A modded `texture.zip` beside a tier the install has is not
  read; edit the tier.
- **Sounds.** The engine plays `soundsh.zbd`; `soundsl.zip` is not read.
- **Not covered.** The UI half (`crimson.rof`, `crimptch.rof`, the string DLLs, the cinemas) is
  always read from the game files; HD texture packs replace its art. An old `extracted/rof/`
  folder is ignored.

## Naming the folder

- **On the command line**, `--mod-folder=<folder>` names it for one launch, ahead of any saved
  folder; `--mod-folder=` names none ([cli.md](cli.md)).
- **In Options**, the GAME OPTIONS page's Modding Folder row picks Off or one of the folders it
  offers: the saved one, the one this launch's `--mod-folder=` named, and an `extracted` folder an
  older CSVM left beside the data. Its last item, **Choose folder...**, opens a folder dialog (the
  same one the game-files screen uses, driven by a controller as well); the folder it returns is
  picked and offered in the list for the rest of the launch. A folder holding none of the outputs
  in the table above gets a warning but is still taken, so it may be filled later. The pick is
  saved as `modFolder` in `options.json` on ACCEPT CHANGES, which may also be set by hand, and
  takes effect at the next start.
- With no folder named, nothing reads unzbd output, an old `extracted/` included. A deterministic
  run (`--det`, so every test and golden shot) and a `--synthetic-data` run ignore the saved folder.

The log names the folder at start (`modding folder <path>: ...`) and each archive read from it
(`modding folder: ZBD/C1/gamez.zbd read from ...`). Output that does not read is logged as an error
and the game file is read instead.

## Match the copy you play

Archives refer to each other by name: a world's materials name textures, a mission's readers name
nodes, anim definitions name sounds and nodes. CSVM does not check that a modding folder's
archives agree with the game files around them. Output extracted from another copy of the game
(another pressing, a patched or unpatched install) can name things the other archives lack, which
shows as missing textures, objects or sounds. Extract from the copy CSVM reads, and keep the
modding folder to the archives a mod changes.
