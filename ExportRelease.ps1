<#
.SYNOPSIS
    Builds CSVM and exports the "Windows Desktop" release preset to .scratch\export\CSVM.exe.

.DESCRIPTION
    The packaging entry point (see PROJECT_CONTEXT.md "Exporting a release build" and
    docs/tooling.md). Runs `dotnet build`, imports the project headless (needed once per
    fresh tree before an export can see every asset), then exports the release preset
    defined in CSVM/export_presets.cfg.

    Godot's export templates are user-global, not part of this repo, and there is no
    reliable way to install them unattended -- so this script checks for them first and
    throws a clear error naming the one-time setup step instead of letting Godot's own
    export fail cryptically partway through.

.EXAMPLE
    .\ExportRelease.ps1
    Build, import, export to .scratch\export\CSVM.exe.
#>

$ErrorActionPreference = "Stop"

$RepoRoot   = $PSScriptRoot
$ProjectDir = Join-Path $RepoRoot "CSVM"
$Sln        = Join-Path $ProjectDir "CSVM.sln"
$GodotExe   = Join-Path $RepoRoot "tools\godot\Godot_v4.7-stable_mono_win64\Godot_v4.7-stable_mono_win64_console.exe"

$TemplateDir = Join-Path $env:APPDATA "Godot\export_templates\4.7.stable.mono"
$ExportDir   = Join-Path $RepoRoot ".scratch\export"
$ExportExe   = Join-Path $ExportDir "CSVM.exe"

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

if (-not (Test-Path $ExportDir)) {
    Write-Host "Creating $ExportDir..." -ForegroundColor Cyan
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

Write-Host "Exported to $ExportExe" -ForegroundColor Green
