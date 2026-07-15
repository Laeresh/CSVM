<#
.SYNOPSIS
    Builds CrimsonSkies and launches it in Godot.

.DESCRIPTION
    Runs `dotnet build` on the Godot .NET project, then launches the game via the
    Godot console executable. Defaults to `--fly` (free flight over the C1 world --
    the milestone 2 vertical slice). If launched with no arguments, prompts you to
    pick a plane from a numbered list before flying it. Any arguments passed to
    this script are forwarded as user args instead, skipping the prompt (see
    CrimsonSkies/src/PlaneViewer.cs for the full list: --plane=, --chapter=,
    --sky-zone=, --debug-collision, etc).

.EXAMPLE
    .\RunDev.ps1
    Build, pick a plane from the menu, then free flight over Sea Haven (C1).

.EXAMPLE
    .\RunDev.ps1 --plane=player_kestrel
    Build, then orbit-view the Kestrel instead of flying (prompt skipped).
#>

$ErrorActionPreference = "Stop"

$RepoRoot   = $PSScriptRoot
$ProjectDir = Join-Path $RepoRoot "CrimsonSkies"
$Sln        = Join-Path $ProjectDir "CrimsonSkies.sln"
$GodotExe   = Join-Path $RepoRoot "tools\godot\Godot_v4.7-stable_mono_win64\Godot_v4.7-stable_mono_win64_console.exe"

# The confirmed-flyable player planes (CLAUDE.md: full hierarchies incl. control
# surfaces, props, gear, firepoints, cockpits).
$Planes = @(
    [pscustomobject]@{ Name = "Autogyro";   Node = "player_autogyro" }
    [pscustomobject]@{ Name = "Bloodhawk";  Node = "player_bhawk" }
    [pscustomobject]@{ Name = "Peacemaker"; Node = "player_peacemaker" }
    [pscustomobject]@{ Name = "Kestrel";    Node = "player_kestrel" }
    [pscustomobject]@{ Name = "Avenger";    Node = "player_avenger" }
    [pscustomobject]@{ Name = "Balmoral";   Node = "player_balmoral" }
    [pscustomobject]@{ Name = "Fury";       Node = "player_fury" }
)

if (-not (Test-Path $Sln)) {
    throw "Solution not found at $Sln"
}
if (-not (Test-Path $GodotExe)) {
    throw "Godot not found at $GodotExe -- see CLAUDE.md for the tools/ setup."
}

Write-Host "Building CrimsonSkies..." -ForegroundColor Cyan
dotnet build $Sln
if ($LASTEXITCODE -ne 0) {
    throw "dotnet build failed (exit $LASTEXITCODE)."
}

if ($args.Count -gt 0) {
    $UserArgs = $args
}
else {
    Write-Host ""
    Write-Host "Select a plane:" -ForegroundColor Cyan
    for ($i = 0; $i -lt $Planes.Count; $i++) {
        Write-Host ("  {0}. {1}" -f ($i + 1), $Planes[$i].Name)
    }
    $choice = Read-Host "Enter number (default 1)"
    $index = 0
    if ($choice -match '^\d+$' -and [int]$choice -ge 1 -and [int]$choice -le $Planes.Count) {
        $index = [int]$choice - 1
    }
    $plane = $Planes[$index]
    Write-Host "Flying $($plane.Name)..." -ForegroundColor Cyan
    $UserArgs = @("--fly", "--plane=$($plane.Node)")
}

Write-Host "Launching Godot: $($UserArgs -join ' ')" -ForegroundColor Cyan
& $GodotExe --path $ProjectDir res://scenes/Main.tscn -- @UserArgs
exit $LASTEXITCODE
