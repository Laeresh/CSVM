<#
.SYNOPSIS
    Builds CSVM and launches it straight into the in-game launchscreen.

.DESCRIPTION
    The "play the game" entry point (as opposed to RunDev.ps1, the dev helper with
    console prompts). It runs `dotnet build`, then launches the game with NO user args,
    so Launcher shows the in-game launchscreen: pick Free Flight or Stunt Flying,
    then the chapter (map), then the aircraft, all with the keyboard or a controller
    (see the Milestone 2.5 launchscreen). Nothing is chosen on the command line.

    Any arguments you pass are forwarded verbatim to the game, so an explicit content
    arg (--plane=, --chapter=, --stunt, --viewer, --screenshot=, ...) bypasses the
    launchscreen and builds directly -- handy for jumping straight into a specific
    setup. Flight is the default for those: --plane=player_fury flies the Fury, and
    --viewer is what asks for the static inspection view instead. See
    docs/cli.md for the full arg list.

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

if (-not (Test-Path $Sln)) {
    throw "Solution not found at $Sln"
}
if (-not (Test-Path $GodotExe)) {
    throw "Godot not found at $GodotExe -- see PROJECT_CONTEXT.md for the tools/ setup. In a git worktree, set `$env:CSVM_DATA_ROOT to the primary tree."
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
[DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
public delegate bool EnumProc(IntPtr hWnd, IntPtr lParam);
[DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr lParam);
public static IntPtr FindForPid(uint target) {
    IntPtr found = IntPtr.Zero;
    EnumWindows((h, l) => { uint pid; GetWindowThreadProcessId(h, out pid);
        if (pid == target) { found = h; return false; } return true; }, IntPtr.Zero);
    return found;
}
'@ -ErrorAction SilentlyContinue

# -WindowStyle Normal: a launcher whose own window is hidden (an agent shell, a scheduled
# task) otherwise passes SW_HIDE down via STARTUPINFO and the game boots invisible.
# Streams go to files, not the console: given no stdout handle the plain exe calls
# AttachConsole and prints its whole engine chatter over whatever terminal (or chat
# transcript) launched it. The engine's own categorized log always lands in
# .scratch/logs/ regardless; these files catch what only raw stdout/stderr sees.
$Stamp   = Get-Date -Format "yyyyMMdd-HHmmss"
$LogDir  = Join-Path $RepoRoot ".scratch\logs"
New-Item -ItemType Directory -Force $LogDir | Out-Null
$OutFile = Join-Path $LogDir "game-$Stamp.out"
$ErrFile = Join-Path $LogDir "game-$Stamp.err"
$proc = Start-Process -FilePath $GodotExe -ArgumentList $LaunchArgs -WindowStyle Normal -PassThru `
    -RedirectStandardOutput $OutFile -RedirectStandardError $ErrFile
# The main window does not exist at spawn; poll briefly rather than guessing a sleep.
# Found via EnumWindows by pid, NOT Process.MainWindowHandle -- that property is zero for a
# HIDDEN window, which is exactly the case this rescue exists for.
for ($i = 0; $i -lt 200; $i++) {
    Start-Sleep -Milliseconds 50
    if ($proc.HasExited) { break }
    $hwnd = [CsvmLaunch.FgWin]::FindForPid([uint32]$proc.Id)
    if ($hwnd -ne [IntPtr]::Zero) {
        [void][CsvmLaunch.FgWin]::ShowWindow($hwnd, 5)   # SW_SHOW
        [void][CsvmLaunch.FgWin]::SetForegroundWindow($hwnd)
        break
    }
}
$proc.WaitForExit()
exit $proc.ExitCode

