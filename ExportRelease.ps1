<#
.SYNOPSIS
    Builds CSVM, exports the "Windows Desktop" release preset, and stages the whole
    friend-facing release payload in .scratch\export\.

.DESCRIPTION
    The packaging entry point (see PROJECT_CONTEXT.md "Exporting a release build" and
    docs/tooling.md). Runs `dotnet build`, imports the project headless (needed once per
    fresh tree before an export can see every asset), exports the release preset defined
    in CSVM/export_presets.cfg, then copies the non-export pieces of the release listed
    in packaging/MANIFEST.md next to it, so .scratch\export\ is the zip's contents.

    Copying is what keeps MANIFEST.md's "byte-identical to the repo source" rule true by
    construction: the extractor scripts and licences are taken from their one home in the
    repo on every export, never forked into a package variant that can drift.

    Godot's export templates are user-global, not part of this repo, and there is no
    reliable way to install them unattended -- so this script checks for them first and
    throws a clear error naming the one-time setup step instead of letting Godot's own
    export fail cryptically partway through. unzbd.exe is checked the same way: it is a
    local fork build, not a repo artefact.

.EXAMPLE
    .\ExportRelease.ps1
    Build, import, export to .scratch\export\CSVM.exe, and stage the release files.
#>

$ErrorActionPreference = "Stop"

$RepoRoot   = $PSScriptRoot
$ProjectDir = Join-Path $RepoRoot "CSVM"
$Sln        = Join-Path $ProjectDir "CSVM.sln"
$GodotExe   = Join-Path $RepoRoot "tools\godot\Godot_v4.7-stable_mono_win64\Godot_v4.7-stable_mono_win64_console.exe"

$TemplateDir = Join-Path $env:APPDATA "Godot\export_templates\4.7.stable.mono"
$ExportDir   = Join-Path $RepoRoot ".scratch\export"
$ExportExe   = Join-Path $ExportDir "CSVM.exe"
$ZipPath     = Join-Path $RepoRoot ".scratch\CSVM.zip"
$UnzbdExe    = Join-Path $RepoRoot "tools\mech3ax\target\release\unzbd.exe"

# The zip payload beside the export output, from packaging/MANIFEST.md. Sources are the
# files' one home in the repo, so a copy is byte-identical to what the manifest names.
$ReleaseFiles = @(
    @{ Source = Join-Path $RepoRoot "packaging\Extract.ps1";    Dest = "Extract.ps1" },
    @{ Source = Join-Path $RepoRoot "ExtractAssets.ps1";        Dest = "ExtractAssets.ps1" },
    @{ Source = Join-Path $RepoRoot "ExtractRof.ps1";           Dest = "ExtractRof.ps1" },
    @{ Source = Join-Path $RepoRoot "packaging\README.md";      Dest = "README.md" },
    @{ Source = Join-Path $RepoRoot "packaging\LICENSE";        Dest = "LICENSE" },
    @{ Source = Join-Path $RepoRoot "packaging\LICENSE-unzbd";  Dest = "LICENSE-unzbd" },
    @{ Source = $UnzbdExe;                                      Dest = "tools\unzbd.exe" }
)

if (-not (Test-Path $Sln)) {
    throw "Solution not found at $Sln"
}
if (-not (Test-Path $GodotExe)) {
    throw "Godot not found at $GodotExe -- see PROJECT_CONTEXT.md for the tools/ setup."
}

# Export templates are a one-time, user-global install (see README.md "Package a release
# build"): the inner templates/ FILES of tools/godot-4.7-mono-export-templates.tpz extracted
# directly into $TemplateDir. Godot's own export otherwise fails partway through with an
# error that doesn't say what's missing, so check up front instead.
if (-not (Test-Path $TemplateDir)) {
    throw "Godot export templates not found at $TemplateDir -- one-time setup: extract the " +
        "inner templates\ files of tools\godot-4.7-mono-export-templates.tpz directly into " +
        "that folder (create the version dir; do not keep the templates\ folder level)."
}

# unzbd is built locally from the mech3ax fork (branch cs-anim, Decision 5) and is not a repo
# artefact, so a fresh tree can reach the export step without having it. Check before the long
# build rather than after, and never fall back to the pinned v0.6.1 binary: the fork's output is
# what the engine reads.
if (-not (Test-Path $UnzbdExe)) {
    throw "unzbd.exe not found at $UnzbdExe -- build the mech3ax fork (branch cs-anim) first; " +
        "see packaging\MANIFEST.md. The pinned v0.6.1 binary is not a substitute."
}

foreach ($file in $ReleaseFiles) {
    if (-not (Test-Path $file.Source)) {
        throw "Release payload file not found at $($file.Source) -- see packaging\MANIFEST.md."
    }
}

# The staging folder is rebuilt from nothing each run, because everything in it is copied into
# the zip: a file left by an earlier export or a hand assembly would otherwise ship forever.
# PowerShell 5.1's recursive delete FOLLOWS directory junctions into their target (see
# CleanScratch.ps1), so refuse to sweep a folder someone has linked something into rather than
# deleting whatever is on the far side of the link.
if (Test-Path $ExportDir) {
    $links = Get-ChildItem $ExportDir -Recurse -Directory -Force |
        Where-Object { $_.Attributes -band [System.IO.FileAttributes]::ReparsePoint }
    if ($links) {
        throw "$ExportDir contains a junction or symlink ($($links[0].FullName)) -- remove it " +
            "by hand; a recursive delete here would delete the link's target."
    }
    Write-Host "Clearing $ExportDir..." -ForegroundColor Cyan
    # Emptied, not deleted: a shell or a running build sitting in the folder holds the directory
    # itself open, and removing its contents works where removing the folder fails. A running
    # exported build still holds its own exe, which Godot reports much later and far less
    # clearly, as a failure to rename its temporary file after the whole pack is done.
    try {
        Get-ChildItem $ExportDir -Force | Remove-Item -Recurse -Force -ErrorAction Stop
    } catch {
        throw "Could not clear $ExportDir -- close the exported build if it is still running. " +
            "($($_.Exception.Message))"
    }
} else {
    New-Item -ItemType Directory -Force $ExportDir | Out-Null
}

Write-Host "Building CSVM..." -ForegroundColor Cyan
dotnet build $Sln
if ($LASTEXITCODE -ne 0) {
    throw "dotnet build failed (exit $LASTEXITCODE)."
}

Write-Host "Importing project..." -ForegroundColor Cyan
& $GodotExe --path $ProjectDir --headless --import
if ($LASTEXITCODE -ne 0) {
    throw "Godot import failed (exit $LASTEXITCODE)."
}

Write-Host "Exporting release build to $ExportExe..." -ForegroundColor Cyan
& $GodotExe --path $ProjectDir --headless --export-release "Windows Desktop" $ExportExe
if ($LASTEXITCODE -ne 0) {
    throw "Godot export failed (exit $LASTEXITCODE)."
}

Write-Host "Staging release files..." -ForegroundColor Cyan
foreach ($file in $ReleaseFiles) {
    $dest = Join-Path $ExportDir $file.Dest
    $destDir = Split-Path $dest -Parent
    if (-not (Test-Path $destDir)) {
        New-Item -ItemType Directory -Force $destDir | Out-Null
    }
    Copy-Item $file.Source $dest -Force
    Write-Host "  $($file.Dest)"
}

# The .NET publish leaves scanner shadow copies (name~RFxxxxxxx.TMP) in the data folder, and
# Godot's own CSVM.tmp survives a failed embed. Both are junk a recipient must not receive, and
# a locked one aborts the whole archive midway, so drop them and skip them when zipping.
Get-ChildItem $ExportDir -Recurse -File -Include "*.TMP", "*.tmp" |
    Remove-Item -Force -ErrorAction SilentlyContinue

# The zip lands beside the staging folder, not inside it: an archiver walking a directory it is
# writing into is how a release zip ends up containing a truncated copy of itself. Built through
# ZipFile rather than Compress-Archive, whose per-file errors are non-terminating: it reports
# success having written nothing, and the missing zip is only noticed on the next hand-off.
Write-Host "Packaging $ZipPath..." -ForegroundColor Cyan
if (Test-Path $ZipPath) {
    Remove-Item $ZipPath -Force
}
Add-Type -AssemblyName System.IO.Compression.FileSystem
$prefix = (Get-Item $ExportDir).FullName.TrimEnd("\") + "\"
$entries = Get-ChildItem $ExportDir -Recurse -File |
    Where-Object { $_.Extension -notin @(".tmp", ".TMP") }
$zip = [System.IO.Compression.ZipFile]::Open($ZipPath, "Create")
try {
    foreach ($entry in $entries) {
        $relative = $entry.FullName.Substring($prefix.Length)
        [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
            $zip, $entry.FullName, $relative, "Optimal") | Out-Null
    }
} finally {
    $zip.Dispose()
}

Write-Host "Exported to $ExportExe" -ForegroundColor Green
Write-Host "Packaged  $ZipPath ($($entries.Count) files)" -ForegroundColor Green
