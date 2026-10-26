# Crimson Skies remake — Godot project

Godot 4 (.NET/C#) engine remake of Crimson Skies (2000). This project ships **no game
assets**: it reads the player's own legally-owned copy of the game in place at runtime,
through its own C# readers.

## Prerequisites

- Godot 4.7 (.NET edition)
- .NET SDK 8+
- Your own Crimson Skies install, as `CrimsonSkiesGame/` next to (one level above) this
  project directory, or wherever `CSVM_INSTALL_ROOT` points. A menu launch without one asks
  for an install folder, the disc's drive or a disc image, and remembers it.

## Run the plane viewer

```
dotnet build CSVM.sln
godot --path . res://scenes/Main.tscn -- --plane=player_bhawk
```

Left-drag orbits, mouse wheel zooms, Escape quits. Useful args (all after `--`):
`--plane=<node>` (e.g. `player_kestrel`, `player_autogyro`), `--yaw=` / `--pitch=`
(radians), `--screenshot=<path>` (renders a few frames, saves a PNG, exits).
