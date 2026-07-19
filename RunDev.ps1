<#
.SYNOPSIS
    Builds CrimsonSkies and launches it in Godot.

.DESCRIPTION
    Runs `dotnet build` on the Godot .NET project, then launches the game via the
    Godot console executable. The default action is free flight (--fly) over a
    chapter world -- the milestone 2 vertical slice.

    Interactive selection: whenever you do NOT pass --plane, the script prompts you
    to pick a plane; whenever you do NOT pass --chapter, it prompts you to pick a
    chapter (map). Both prompts run in the "fly flow" -- i.e. any invocation that
    isn't an explicit static view (a bare --plane orbit view, or a bare --chapter
    world view). So you can run it with no args (prompts both, then flies) or with
    extra flight flags (e.g. --debug-collision) and still get the prompts.

    Explicit static views:
      * --plane=<node>          orbit-view one aircraft (verbatim, no prompts)
      * --chapter[=<code>]      static world view (verbatim, no prompts)
      * --damage[=part:frac,..] the damage lab -- a static plane view with per-part
                                HP sliders. Prompts for the plane unless --plane= is
                                also given; never adds --fly or a chapter. (Passing
                                --fly/--chapter alongside --damage goes through
                                verbatim instead, and the viewer ignores --damage
                                with its own note.)

    Any other CrimsonSkies user args are forwarded as-is (see
    CrimsonSkies/src/PlaneViewer.cs for the full list: --mission=, --scenario=,
    --spawn=, --sky-zone=, --mute, --debug-collision, --hold=, etc).

.EXAMPLE
    .\RunDev.ps1
    Build, pick a plane + chapter from the menus, then free flight.

.EXAMPLE
    .\RunDev.ps1 --debug-collision
    Build, pick a plane + chapter, then fly with the collision probe drawn.

.EXAMPLE
    .\RunDev.ps1 --fly --plane=player_kestrel
    Build, fly the Kestrel; plane prompt skipped, chapter prompt still shown.

.EXAMPLE
    .\RunDev.ps1 --plane=player_kestrel
    Build, then orbit-view the Kestrel (static view -- no prompts, no flight).

.EXAMPLE
    .\RunDev.ps1 --damage
    Build, pick a plane from the menu, then the damage lab (per-part HP sliders).

.EXAMPLE
    .\RunDev.ps1 --damage=leftwing:0.25 --plane=player_kestrel
    Build, then the Kestrel damage lab preset to a 25% left wing; no prompts.
#>

$ErrorActionPreference = "Stop"

$RepoRoot   = $PSScriptRoot
$ProjectDir = Join-Path $RepoRoot "CrimsonSkies"
$Sln        = Join-Path $ProjectDir "CrimsonSkies.sln"
$GodotExe   = Join-Path $RepoRoot "tools\godot\Godot_v4.7-stable_mono_win64\Godot_v4.7-stable_mono_win64_console.exe"

# Every player-flyable aircraft: the vehicle.json defs with kind_of=player_airplane,
# in the game's roster order. Name = the def's MSG_VEH_ title; Node = its `nodename`
# (the root node in planes.zbd passed to --plane). Note the two non-obvious ones:
# the Devastator's node is player_pfighter, and player_avenger's title is Hellhound.
$Planes = @(
    [pscustomobject]@{ Name = "Devastator"; Node = "player_pfighter" }
    [pscustomobject]@{ Name = "Bloodhawk";  Node = "player_bhawk" }
    [pscustomobject]@{ Name = "Firebrand";  Node = "player_fbrand" }
    [pscustomobject]@{ Name = "Brigand";    Node = "player_brigand" }
    [pscustomobject]@{ Name = "Fury";       Node = "player_fury" }
    [pscustomobject]@{ Name = "Autogyro";   Node = "player_autogyro" }
    [pscustomobject]@{ Name = "Hellhound";  Node = "player_avenger" }
    [pscustomobject]@{ Name = "Kestrel";    Node = "player_kestrel" }
    [pscustomobject]@{ Name = "Peacemaker"; Node = "player_peacemaker" }
    [pscustomobject]@{ Name = "Balmoral";   Node = "player_balmoral" }
    [pscustomobject]@{ Name = "Warhawk";    Node = "player_warhawk" }
)
$DefaultPlane = "player_bhawk"   # Bloodhawk: the primary test aircraft

# The chapter worlds (--chapter=). Code is the extracted folder; Name is the map
# (region labels per CLAUDE.md; C1/C1B/C1C are day/night/weather variants of Sea Haven).
$Chapters = @(
    [pscustomobject]@{ Name = "Sea Haven (Northwest) - night"; Code = "C1" }
    [pscustomobject]@{ Name = "Sea Haven - variant B";         Code = "C1B" }
    [pscustomobject]@{ Name = "Sea Haven - variant C";         Code = "C1C" }
    [pscustomobject]@{ Name = "Hollywood";                     Code = "C2" }
    [pscustomobject]@{ Name = "Hollywood - variant B";         Code = "C2B" }
    [pscustomobject]@{ Name = "Hawaii (islands)";              Code = "C3" }
    [pscustomobject]@{ Name = "Rocky Mountains";               Code = "C4" }
    [pscustomobject]@{ Name = "New York";                      Code = "C5" }
)
$DefaultChapter = "C1"

function Select-Item {
    param(
        [string]$Title,
        [array] $Items,          # objects with a .Name property
        [int]   $DefaultIndex = 0
    )
    Write-Host ""
    Write-Host $Title -ForegroundColor Cyan
    for ($i = 0; $i -lt $Items.Count; $i++) {
        $marker = ""
        if ($i -eq $DefaultIndex) { $marker = "  (default)" }
        Write-Host ("  {0,2}. {1}{2}" -f ($i + 1), $Items[$i].Name, $marker)
    }
    $choice = Read-Host ("Enter number (default {0})" -f ($DefaultIndex + 1))
    if ($choice -match '^\d+$' -and [int]$choice -ge 1 -and [int]$choice -le $Items.Count) {
        return $Items[[int]$choice - 1]
    }
    return $Items[$DefaultIndex]
}

# Prompt for a plane from the roster and return its --plane= argument.
function Select-Plane {
    $defIdx = [Math]::Max(0, [array]::IndexOf(($Planes | ForEach-Object { $_.Node }), $DefaultPlane))
    $plane  = Select-Item -Title "Select a plane:" -Items $Planes -DefaultIndex $defIdx
    Write-Host "Plane:   $($plane.Name) ($($plane.Node))" -ForegroundColor Green
    return "--plane=$($plane.Node)"
}

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

# Freeze workaround (same as RunGame.ps1, see the comment there): the bundled SDL's
# DirectInput backend hangs the engine when a >255-button phantom device disconnects
# (8BitDo Ultimate 2 dongle). XInput/HIDAPI pads are unaffected by disabling it.
if (-not $env:SDL_JOYSTICK_DIRECTINPUT) { $env:SDL_JOYSTICK_DIRECTINPUT = "0" }

$UserArgs = @()
if ($args.Count -gt 0) { $UserArgs += $args }

$hasPlane   = @($UserArgs | Where-Object { $_ -like '--plane=*' }).Count -gt 0
$hasChapter = @($UserArgs | Where-Object { $_ -like '--chapter=*' -or $_ -eq '--chapter' }).Count -gt 0
$hasFly     = $UserArgs -contains '--fly'
$hasDamage  = @($UserArgs | Where-Object { $_ -eq '--damage' -or $_ -like '--damage=*' }).Count -gt 0

# Damage-lab flow: --damage is the static plane viewer's lab (PlaneViewer ignores it
# in world/fly mode), so it needs a plane and must NOT get --fly or a chapter. Prompt
# for the plane when missing and pass everything else through. An explicit --fly or
# --chapter alongside --damage skips this flow (verbatim pass-through; the viewer
# prints its own ignore note).
$damageFlow = $hasDamage -and -not $hasFly -and -not $hasChapter

# Fly flow = anything that isn't an explicit static view. A bare --plane (orbit view),
# a bare --chapter (static world view), or a --damage lab run stays static; everything
# else (no args, --fly, or extra flight flags) prompts for whatever wasn't specified.
$flyFlow = -not $damageFlow -and ($hasFly -or (-not $hasPlane -and -not $hasChapter))

if ($damageFlow) {
    if (-not $hasPlane) { $UserArgs += Select-Plane }
}
elseif ($flyFlow) {
    if (-not $hasPlane) { $UserArgs += Select-Plane }
    if (-not $hasChapter) {
        $defIdx  = [Math]::Max(0, [array]::IndexOf(($Chapters | ForEach-Object { $_.Code }), $DefaultChapter))
        $chapter = Select-Item -Title "Select a chapter (map):" -Items $Chapters -DefaultIndex $defIdx
        Write-Host "Chapter: $($chapter.Name) ($($chapter.Code))" -ForegroundColor Green
        $UserArgs += "--chapter=$($chapter.Code)"
    }
    if (-not $hasFly) { $UserArgs += "--fly" }
}

Write-Host "Launching Godot: $($UserArgs -join ' ')" -ForegroundColor Cyan
& $GodotExe --path $ProjectDir res://scenes/Main.tscn -- @UserArgs
exit $LASTEXITCODE
