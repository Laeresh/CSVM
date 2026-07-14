# Crimson Skies remake — Godot project

Godot 4 (.NET/C#) engine remake of Crimson Skies (2000). This project ships **no game
assets**: it reads the player's own legally-owned install at runtime, via
[mech3ax](https://github.com/TerranMechworks/mech3ax) extraction output.

## Prerequisites

- Godot 4.7 (.NET edition)
- .NET SDK 8+
- mech3ax `unzbd` extraction output from your own Crimson Skies install:
  - `unzbd cs gamez <install>/ZBD/planes.zbd extracted/planes-gamez.zip`
  - `unzbd cs textures <install>/ZBD/C1/texture.zbd extracted/c1-texture.zip`

By default the viewer looks for the `extracted/` directory next to (one level above)
this project directory; override with `--gamez=` / `--textures=`.

## Run the plane viewer

```
dotnet build CrimsonSkies.sln
godot --path . res://scenes/Main.tscn -- --plane=player_bhawk
```

Left-drag orbits, mouse wheel zooms, Escape quits. Useful args (all after `--`):
`--plane=<node>` (e.g. `player_kestrel`, `player_autogyro`), `--yaw=` / `--pitch=`
(radians), `--screenshot=<path>` (renders a few frames, saves a PNG, exits).
