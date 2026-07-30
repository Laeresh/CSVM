<#
.SYNOPSIS
    Builds CSVM and launches it in Godot.

.DESCRIPTION
    Runs `dotnet build` on the Godot .NET project, then launches the game via the
    Godot console executable. FLIGHT IS THE DEFAULT: any invocation without --viewer
    flies, so a bare --plane=<node> now flies that aircraft rather than orbit-viewing
    it (the 2026-07-20 CLI inversion; --fly is still accepted and still means this).

    Interactive selection: in the fly flow, whenever you do NOT pass --plane the
    script prompts you to pick a plane, and whenever you do NOT pass --chapter it
    prompts you to pick a chapter (map). So you can run it with no args (prompts
    both, then flies) or with extra flight flags (e.g. --debug-collision) and still
    get the prompts.

    Static inspection (--viewer):
      * --viewer                orbit-view an aircraft; prompts for the plane if
                                --plane= is missing. Press L in-session for the
                                LIVERY LAB (pattern, RGB colour sliders, decal
                                slots, repainting the plane live).
      * --viewer --chapter[=C1] static world view (no plane, no prompts)
      * --damage[=part:frac,..] the damage lab -- implies --viewer, so it needs no
                                extra flag. Per-part HP sliders (H toggles them);
                                prompts for the plane unless --plane= is given.

    Any other CSVM user args are forwarded as-is (see
    docs/cli.md for the full list: --mission=, --scenario=,
    --spawn=, --sky-zone=, --mute, --debug-collision, --hold=, etc).

.EXAMPLE
    .\RunDev.ps1
    Build, pick a plane + chapter from the menus, then free flight.

.EXAMPLE
    .\RunDev.ps1 --debug-collision
    Build, pick a plane + chapter, then fly with the collision probe drawn.

.EXAMPLE
    .\RunDev.ps1 --plane=player_kestrel
    Build, fly the Kestrel; plane prompt skipped, chapter prompt still shown.

.EXAMPLE
    .\RunDev.ps1 --viewer --plane=player_kestrel
    Build, then orbit-view the Kestrel (no prompts, no flight). L = livery lab.

.EXAMPLE
    .\RunDev.ps1 --viewer --paint=hughes
    Build, pick a plane, then orbit it in the Hughes livery; L to edit it live.

.EXAMPLE
    .\RunDev.ps1 --damage
    Build, pick a plane from the menu, then the damage lab (per-part HP sliders).

.EXAMPLE
    .\RunDev.ps1 --damage=leftwing:0.25 --plane=player_kestrel
    Build, then the Kestrel damage lab preset to a 25% left wing; no prompts.
#>

$ErrorActionPreference = "Stop"

$RepoRoot   = $PSScriptRoot
$ProjectDir = Join-Path $RepoRoot "CSVM"
$Sln        = Join-Path $ProjectDir "CSVM.sln"

# tools/ is git-ignored, so a git worktree checkout has no Godot. Fall back to the primary
# tree named by CSVM_DATA_ROOT -- the same env var GameSession reads for extracted/, so one
# `$env:CSVM_DATA_ROOT = 'Z:\Crimson Skies'` makes a worktree fully runnable. Godot inherits
# the environment, so nothing has to be forwarded on the command line.
# The _console.exe wrapper fails CreateProcess (error 193) on this repo's exact path shape
# (a space combined with enough nesting depth -- reproduced directly against the official,
# byte-identical release binary, so it's an upstream Godot console-wrapper bug, not a corrupt
# download). The plain .exe still writes to an inherited console when launched this way (as
# RunTests.ps1 already relies on), so it's the working substitute until upstream fixes it.
$GodotRel = "tools\godot\Godot_v4.7-stable_mono_win64\Godot_v4.7-stable_mono_win64.exe"
$GodotExe = Join-Path $RepoRoot $GodotRel
if ((-not (Test-Path $GodotExe)) -and $env:CSVM_DATA_ROOT) {
    $GodotExe = Join-Path $env:CSVM_DATA_ROOT $GodotRel
}

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

Write-Host "Building CSVM..." -ForegroundColor Cyan
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
$hasViewer  = $UserArgs -contains '--viewer'
$hasDamage  = @($UserArgs | Where-Object { $_ -eq '--damage' -or $_ -like '--damage=*' }).Count -gt 0

# Flight is the viewer's default (2026-07-20), so the flows key on --viewer, not --fly.
# Viewer flow  = the static inspection view: --viewer, or --damage (which implies it).
#                It needs a plane, unless --chapter asked for a static world instead.
# Fly flow     = everything else, including a bare --plane= (which now FLIES that
#                aircraft rather than orbit-viewing it). Prompts for whatever is missing.
$viewerFlow = $hasViewer -or $hasDamage

if ($viewerFlow) {
    if (-not $hasPlane -and -not $hasChapter) { $UserArgs += Select-Plane }
    if ($hasDamage -and -not $hasViewer) { $UserArgs += "--viewer" }
}
else {
    if (-not $hasPlane) { $UserArgs += Select-Plane }
    if (-not $hasChapter) {
        $defIdx  = [Math]::Max(0, [array]::IndexOf(($Chapters | ForEach-Object { $_.Code }), $DefaultChapter))
        $chapter = Select-Item -Title "Select a chapter (map):" -Items $Chapters -DefaultIndex $defIdx
        Write-Host "Chapter: $($chapter.Name) ($($chapter.Code))" -ForegroundColor Green
        $UserArgs += "--chapter=$($chapter.Code)"
    }
    # No --fly appended: it is the default now, and still accepted if you type it.
}

Write-Host "Launching Godot: $($UserArgs -join ' ')" -ForegroundColor Cyan
# SHELL-1: -ArgumentList quotes nothing itself and this repo path contains a space.
$LaunchArgs = @("--path", ('"' + $ProjectDir + '"'), "res://scenes/Main.tscn", "--") + $UserArgs
# The window is created WITHOUT focus (display/window/size/no_focus) so a scripted run never takes
# the desktop from whoever is using the machine. An interactive launch does want it, and the engine
# cannot take it for itself: Windows' foreground lock no-ops SetForegroundWindow from a process the
# user is not interacting with (verification SHELL-5). This console IS that process, so it is
# allowed to hand the foreground over -- which is why the grab lives here and not in the engine.
Add-Type -Name FgWin -Namespace CsvmLaunch -MemberDefinition @'
[DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
[DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
'@ -ErrorAction SilentlyContinue

$proc = Start-Process -FilePath $GodotExe -ArgumentList $LaunchArgs -PassThru
# The main window does not exist at spawn; poll briefly rather than guessing a sleep.
for ($i = 0; $i -lt 200; $i++) {
    Start-Sleep -Milliseconds 50
    if ($proc.HasExited) { break }
    $proc.Refresh()
    if ($proc.MainWindowHandle -ne [IntPtr]::Zero) {
        [void][CsvmLaunch.FgWin]::ShowWindow($proc.MainWindowHandle, 5)   # SW_SHOW
        [void][CsvmLaunch.FgWin]::SetForegroundWindow($proc.MainWindowHandle)
        break
    }
}
$proc.WaitForExit()
exit $proc.ExitCode

