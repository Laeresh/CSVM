<#
.SYNOPSIS
    Builds CSVM and launches it straight into the in-game launchscreen.

.DESCRIPTION
    The "play the game" entry point (as opposed to RunDev.ps1, the dev helper with
    console prompts). It runs `dotnet build`, then launches the game with NO user args,
    so PlaneViewer shows the in-game launchscreen: pick Free Flight or Stunt Flying,
    then the chapter (map), then the aircraft, all with the keyboard or a controller
    (see the Milestone 2.5 launchscreen). Nothing is chosen on the command line.

    Any arguments you pass are forwarded verbatim to the game, so an explicit content
    arg (--plane=, --chapter=, --stunt, --viewer, --screenshot=, ...) bypasses the
    launchscreen and builds directly -- handy for jumping straight into a specific
    setup. Flight is the default for those: --plane=player_fury flies the Fury, and
    --viewer is what asks for the static inspection view instead. See
    CSVM/src/PlaneViewer.cs for the full arg list.

.EXAMPLE
    .\RunGame.ps1
    Build, then the launchscreen (Mode -> Chapter -> Plane), keyboard or controller.

.EXAMPLE
    .\RunGame.ps1 --stunt --chapter=C4 --plane=player_fury
    Build, then jump straight into a Rocky Mountains stunt run in the Fury (no menu).

.EXAMPLE
    .\RunGame.ps1 --viewer --plane=player_kestrel
    Build, then orbit-view the Kestrel; press L for the livery lab.
#>

$ErrorActionPreference = "Stop"

$RepoRoot   = $PSScriptRoot
$ProjectDir = Join-Path $RepoRoot "CSVM"
$Sln        = Join-Path $ProjectDir "CSVM.sln"
$GodotExe   = Join-Path $RepoRoot "tools\godot\Godot_v4.7-stable_mono_win64\Godot_v4.7-stable_mono_win64_console.exe"

if (-not (Test-Path $Sln)) {
    throw "Solution not found at $Sln"
}
if (-not (Test-Path $GodotExe)) {
    throw "Godot not found at $GodotExe -- see CLAUDE.md for the tools/ setup."
}

Write-Host "Building CSVM..." -ForegroundColor Cyan
dotnet build $Sln
if ($LASTEXITCODE -ne 0) {
    throw "dotnet build failed (exit $LASTEXITCODE)."
}

# Freeze workaround: Godot 4.5+ bundles an SDL whose DirectInput backend counts HID buttons
# uncapped, and on disconnect SDL spins forever in a Uint8 loop when a device claims >255
# buttons (the 8BitDo Ultimate 2 dongle does) -- a hard engine hang the moment the pad sleeps
# (godot#115667 / SDL#14961; fixed upstream in SDL 3.4.4, not yet bundled). Disabling the
# DirectInput backend removes those phantom device views; real pads still work via XInput.
# Drop this (both Run scripts) once tools/godot ships an SDL >= 3.4.4.
if (-not $env:SDL_JOYSTICK_DIRECTINPUT) { $env:SDL_JOYSTICK_DIRECTINPUT = "0" }

# Forward any args as-is (none = the launchscreen; an explicit content arg bypasses it).
$UserArgs = @()
if ($args.Count -gt 0) { $UserArgs += $args }

if ($UserArgs.Count -gt 0) {
    Write-Host "Launching Godot: $($UserArgs -join ' ')" -ForegroundColor Cyan
} else {
    Write-Host "Launching Godot: launchscreen (no args)" -ForegroundColor Cyan
}
& $GodotExe --path $ProjectDir res://scenes/Main.tscn -- @UserArgs
exit $LASTEXITCODE
