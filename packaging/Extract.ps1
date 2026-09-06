<#
.SYNOPSIS
    One-command asset extraction for the CSVM release package: point it at your
    Crimson Skies install and it fills extracted\ next to CSVM.exe.

.DESCRIPTION
    Run this once, from the unzipped CSVM folder, before the first flight.
    Double-clicking Extract.cmd beside it is the same run without a terminal.

        powershell -ExecutionPolicy Bypass -File Extract.ps1 "C:\...\Crimson Skies"

    The one argument is your Crimson Skies install folder -- the folder that
    contains the ZBD and GOSDATA subfolders (look for where the game is
    installed, e.g. under Program Files, and open it until you see those two).
    Passed nothing, the script looks for that folder in the places the game is
    normally installed and offers a folder picker either way.

    The script validates that layout, then hands off to the two real extractors
    shipped next to it (ExtractAssets.ps1 for the ZBD archives, ExtractRof.ps1
    for the UI resources and string table). All extraction logic lives in those
    two scripts; this wrapper only maps paths. Output lands in extracted\ next
    to this script, which is where CSVM.exe looks for it.

    Nothing in your game install is modified -- it is only read.

.PARAMETER InstallRoot
    Your Crimson Skies install folder (the one containing ZBD and GOSDATA).
    Optional: without it the script probes and then offers the picker.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File Extract.ps1 "C:\Program Files (x86)\Microsoft Games\Crimson Skies"
#>

[CmdletBinding()]
param(
    [Parameter(Position = 0)]
    [string] $InstallRoot
)

$ErrorActionPreference = "Stop"

function Fail([string] $Message) {
    Write-Host ""
    Write-Host $Message -ForegroundColor Red
    exit 1
}

# Everything package-side is anchored to this script's own folder, never the
# caller's current directory -- the friend may run it from anywhere.
$PackageRoot = $PSScriptRoot

# The layout that makes a folder an install root. The probe judges a candidate silently with it;
# a folder the caller passed or picked gets the per-part errors below instead, which say which
# half is missing.
function Test-InstallRoot([string] $Path) {
    if (-not $Path) { return $false }
    return (Test-Path -LiteralPath (Join-Path $Path "ZBD")) -and
           (Test-Path -LiteralPath (Join-Path $Path "GOSDATA\ASSETS"))
}

# Where the game is normally installed: the retail installer's own folder under either Program
# Files, and the places a manual copy lands, on every fixed drive. Order is the answer: the
# installer's path is checked before anything a copy may have left elsewhere.
function Get-InstallCandidates {
    $roots = New-Object System.Collections.Generic.List[string]
    foreach ($programFiles in @($env:ProgramFiles, ${env:ProgramFiles(x86)}, $env:ProgramW6432)) {
        if ($programFiles) {
            $roots.Add((Join-Path $programFiles "Microsoft Games\Crimson Skies"))
        }
    }
    foreach ($drive in [System.IO.DriveInfo]::GetDrives()) {
        if ($drive.DriveType -ne [System.IO.DriveType]::Fixed -or -not $drive.IsReady) { continue }
        $letter = $drive.RootDirectory.FullName
        $roots.Add((Join-Path $letter "Microsoft Games\Crimson Skies"))
        $roots.Add((Join-Path $letter "Games\Crimson Skies"))
        $roots.Add((Join-Path $letter "Crimson Skies"))
    }
    return $roots | Select-Object -Unique
}

# The graphical folder picker, so the path is never typed. A host that cannot show it (no
# desktop, or a runspace that is not single-threaded-apartment) falls back to typing after all,
# which is still better than the run ending.
function Select-InstallFolder {
    try {
        Add-Type -AssemblyName System.Windows.Forms -ErrorAction Stop
        $dialog = New-Object System.Windows.Forms.FolderBrowserDialog
        $dialog.Description = "Select your Crimson Skies install folder (the one holding ZBD and GOSDATA)"
        $dialog.ShowNewFolderButton = $false
        if ($dialog.ShowDialog() -eq [System.Windows.Forms.DialogResult]::OK) {
            return $dialog.SelectedPath
        }
        return $null
    }
    catch {
        Write-Host ""
        Write-Host "(The folder picker could not open: $($_.Exception.Message))"
        $typed = Read-Host "Type or paste your Crimson Skies install folder, then press Enter"
        if ("$typed".Trim()) { return "$typed".Trim() }
        return $null
    }
}

# What a double-click resolves: report what the probe found, and let the picker overrule it.
function Resolve-InstallRoot {
    Write-Host "No install folder was given, so this script will look for one."
    Write-Host ""
    $found = @(Get-InstallCandidates | Where-Object { Test-InstallRoot $_ })
    if ($found.Count -gt 0) {
        Write-Host "Found a Crimson Skies install at:" -ForegroundColor Green
        foreach ($candidate in $found) {
            Write-Host "  $candidate"
        }
        Write-Host ""
        Write-Host "Press Enter to use the first one, or type P and press Enter to pick a folder yourself."
        $answer = Read-Host "Your choice"
        if ("$answer".Trim().ToUpperInvariant() -ne "P") { return $found[0] }
    }
    else {
        Write-Host "No Crimson Skies install was found where the game is normally installed"
        Write-Host "(Microsoft Games under Program Files, or a Games folder on one of your drives)."
        Write-Host ""
        Write-Host "Pick it yourself: the folder that holds the ZBD and GOSDATA folders."
    }
    Write-Host ""
    $picked = Select-InstallFolder
    if (-not $picked) {
        Fail ("No folder was picked, so nothing was extracted. Run this again and choose your " +
              "Crimson Skies install folder -- the one that contains the ZBD and GOSDATA folders.")
    }
    return $picked
}

if (-not $InstallRoot) {
    $InstallRoot = Resolve-InstallRoot
}

# Defuse the classic quoting accident: a trailing backslash before the closing
# quote ("C:\...\Crimson Skies\") makes Windows swallow the quote into the
# argument. Strip stray quotes and trailing separators instead of failing on them.
$InstallRoot = $InstallRoot.Trim().Trim('"').TrimEnd('\', '/')

if (-not (Test-Path -LiteralPath $InstallRoot)) {
    Fail ("The folder '$InstallRoot' does not exist. Point this at your Crimson Skies install " +
          "folder -- the one that contains the ZBD and GOSDATA subfolders.")
}

$ZbdDir = Join-Path $InstallRoot "ZBD"
$GosAssetsDir = Join-Path $InstallRoot "GOSDATA\ASSETS"

if (-not (Test-Path -LiteralPath $ZbdDir)) {
    Fail ("Expected a ZBD folder at '$ZbdDir', but it is not there. " +
          "'$InstallRoot' does not look like a Crimson Skies install root -- open your install " +
          "until you see the ZBD and GOSDATA folders side by side, and use that folder.")
}
# The folder layout can be right and the copy still empty (an interrupted install, a mounted
# image nothing was copied out of). Caught here, or the extractors report nothing but "Done"
# and the game contradicts them at boot with its no-game-data screen.
if (-not (Get-ChildItem -LiteralPath $ZbdDir -Recurse -File -Filter *.zbd -ErrorAction SilentlyContinue)) {
    Fail ("The folder '$ZbdDir' holds no .zbd archives, so there is nothing to extract. " +
          "'$InstallRoot' looks like an incomplete Crimson Skies install -- reinstall the game, " +
          "or use the folder of a complete install.")
}
if (-not (Test-Path -LiteralPath $GosAssetsDir)) {
    Fail ("Expected the game's UI assets at '$GosAssetsDir', but that folder is not there. " +
          "'$InstallRoot' does not look like a complete Crimson Skies install root -- open your " +
          "install until you see the ZBD and GOSDATA folders side by side, and use that folder.")
}

# The package must be complete: the two real extractors and the extraction tool
# ship next to this script.
$ExtractAssets = Join-Path $PackageRoot "ExtractAssets.ps1"
$ExtractRof = Join-Path $PackageRoot "ExtractRof.ps1"
$UnzbdExe = Join-Path $PackageRoot "tools\unzbd.exe"
foreach ($required in @($ExtractAssets, $ExtractRof, $UnzbdExe)) {
    if (-not (Test-Path -LiteralPath $required)) {
        Fail ("This package is incomplete: '$required' is missing. " +
              "Re-extract the CSVM zip (all files, folder structure intact) and try again.")
    }
}

$Dest = Join-Path $PackageRoot "extracted"

Write-Host "Extracting Crimson Skies data" -ForegroundColor Cyan
Write-Host "  from: $InstallRoot"
Write-Host "  to:   $Dest"
Write-Host "(your game install is only read, never modified)"
Write-Host ""

try {
    # Step 1 of 2: the ZBD archives (worlds, planes, sounds, textures, animations).
    $global:LASTEXITCODE = 0
    & $ExtractAssets -Source $ZbdDir -Dest $Dest -Unzbd $UnzbdExe
    if ($LASTEXITCODE -ne 0) {
        Fail ("The ZBD extraction reported errors (see above). Nothing more was attempted. " +
              "If your install is a plain retail one and this keeps failing, report it with the output above.")
    }

    Write-Host ""

    # Step 2 of 2: the UI resources and string table.
    $global:LASTEXITCODE = 0
    & $ExtractRof -Source $GosAssetsDir -Dest (Join-Path $Dest "rof")
    if ($LASTEXITCODE -ne 0) {
        Fail ("The UI-resource extraction reported errors (see above). " +
              "If your install is a plain retail one and this keeps failing, report it with the output above.")
    }

    # The HUD font and gun-reticle loaders read loose PNGs from extracted\rimage\,
    # not from rimage.zip -- unpack that one archive (a few tens of MB). Unpacking,
    # not extracting: the archive itself was produced by step 1.
    $RimageZip = Join-Path $Dest "rimage.zip"
    $RimageDir = Join-Path $Dest "rimage"
    if (Test-Path -LiteralPath $RimageZip) {
        $rimageCurrent = (Test-Path -LiteralPath $RimageDir) -and
            ((Get-Item -LiteralPath $RimageDir).LastWriteTime -ge (Get-Item -LiteralPath $RimageZip).LastWriteTime)
        if (-not $rimageCurrent) {
            Write-Host ""
            Write-Host "Unpacking rimage.zip (HUD font + reticle images)..."
            Expand-Archive -LiteralPath $RimageZip -DestinationPath $RimageDir -Force
            (Get-Item -LiteralPath $RimageDir).LastWriteTime = Get-Date
        }
    }
}
catch {
    Fail ("Extraction failed: $($_.Exception.Message)`n" +
          "If your install is a plain retail one and this keeps failing, report it with the message above.")
}

Write-Host ""
Write-Host "All done." -ForegroundColor Green
Write-Host "  Your extracted game data is in: $Dest"
Write-Host "  Now run CSVM.exe (in this folder) to fly."
exit 0
