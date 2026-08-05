<#
.SYNOPSIS
    One-command asset extraction for the CSVM release package: point it at your
    Crimson Skies install and it fills extracted\ next to CSVM.exe.

.DESCRIPTION
    Run this once, from the unzipped CSVM folder, before the first flight:

        powershell -ExecutionPolicy Bypass -File Extract.ps1 "C:\...\Crimson Skies"

    The one argument is your Crimson Skies install folder -- the folder that
    contains the ZBD and GOSDATA subfolders (look for where the game is
    installed, e.g. under Program Files, and open it until you see those two).

    The script validates that layout, then hands off to the two real extractors
    shipped next to it (ExtractAssets.ps1 for the ZBD archives, ExtractRof.ps1
    for the UI resources and string table). All extraction logic lives in those
    two scripts; this wrapper only maps paths. Output lands in extracted\ next
    to this script, which is where CSVM.exe looks for it.

    Nothing in your game install is modified -- it is only read.

.PARAMETER InstallRoot
    Your Crimson Skies install folder (the one containing ZBD and GOSDATA).

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

if (-not $InstallRoot) {
    Write-Host "Usage:  powershell -ExecutionPolicy Bypass -File Extract.ps1 `"<your Crimson Skies install folder>`""
    Write-Host ""
    Write-Host "Example:"
    Write-Host "  powershell -ExecutionPolicy Bypass -File Extract.ps1 `"C:\Program Files (x86)\Microsoft Games\Crimson Skies`""
    Fail "Missing argument: the path to your Crimson Skies install (the folder containing ZBD and GOSDATA)."
}

# Defuse the classic quoting accident: a trailing backslash before the closing
# quote ("C:\...\Crimson Skies\") makes Windows swallow the quote into the
# argument. Strip stray quotes and trailing separators instead of failing on them.
$InstallRoot = $InstallRoot.Trim().Trim('"').TrimEnd('\', '/')

if (-not (Test-Path -LiteralPath $InstallRoot)) {
    Fail ("The folder '$InstallRoot' does not exist. Pass your Crimson Skies install folder -- " +
          "the one that contains the ZBD and GOSDATA subfolders.")
}

$ZbdDir = Join-Path $InstallRoot "ZBD"
$GosAssetsDir = Join-Path $InstallRoot "GOSDATA\ASSETS"

if (-not (Test-Path -LiteralPath $ZbdDir)) {
    Fail ("Expected a ZBD folder at '$ZbdDir', but it is not there. " +
          "'$InstallRoot' does not look like a Crimson Skies install root -- open your install " +
          "until you see the ZBD and GOSDATA folders side by side, and pass that folder.")
}
if (-not (Test-Path -LiteralPath $GosAssetsDir)) {
    Fail ("Expected the game's UI assets at '$GosAssetsDir', but that folder is not there. " +
          "'$InstallRoot' does not look like a complete Crimson Skies install root -- open your " +
          "install until you see the ZBD and GOSDATA folders side by side, and pass that folder.")
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
