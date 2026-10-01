<#
.SYNOPSIS
    Downloads the pinned webrtc-native GDExtension into CSVM\addons\webrtc_native\, checked by
    SHA-256, and registers it with the Godot project.

.DESCRIPTION
    Internet play through a master server (server\README.md) runs over Godot's WebRTC peer, which
    on desktop needs the webrtc-native GDExtension (godotengine/webrtc-native, MIT, with
    libdatachannel and its dependencies under their own permissive licences). The binaries are not
    committed: CSVM\addons\webrtc_native\ is git-ignored, so the extension is fetched here, as
    InstallSdl2.ps1 fetches SDL2.

    Installs into <Root>\CSVM\addons\webrtc_native\:
      webrtc_native.gdextension          the extension's own manifest, as released
      LICENSE.*                          every licence the release carries
      lib\*.windows.template_*.x86_64.dll  Windows x64, debug and release
      lib\*.linux.template_*.x86_64.so     Linux x64, debug and release

    and adds res://addons/webrtc_native/webrtc_native.gdextension to
    <Root>\CSVM\.godot\extension_list.cfg, which is where a run that never opened the editor looks
    for extensions. An editor import writes the same line, so the two never disagree.

    Without the extension the game runs as before: LAN and direct play are unchanged, and a master
    server set in the options lists games but cannot host or join over the internet.

    -Verify installs nothing. It checks the manifest and the four libraries are present and throws
    naming this script as the fix.

.PARAMETER Root
    The checkout to install into. Defaults to this script's own tree. A worktree needs its own
    install, since the extension must sit inside the project folder Godot opens.

.PARAMETER Force
    Re-download even when the install is already complete.

.EXAMPLE
    .\InstallWebRtc.ps1
    Fetch webrtc-native 1.2.2 into CSVM\addons\webrtc_native\ of this tree.
#>

[CmdletBinding()]
param(
    [string]$Root = $PSScriptRoot,
    [switch]$Force,
    [switch]$Verify
)

$ErrorActionPreference = "Stop"

# The pin. ZipSha256 matches the digest GitHub publishes for the release asset.
$Version   = "1.2.2-stable"
$ZipUrl    = "https://github.com/godotengine/webrtc-native/releases/download/$Version/godot-extension-webrtc_native.zip"
$ZipSha256 = "98E9446921740D995BD9CA1BE48798DC3C2CEED51E044A25CE18B3CFF11F56E5"

$Manifest   = "res://addons/webrtc_native/webrtc_native.gdextension"
$ProjectDir = Join-Path $Root "CSVM"
$AddonDir   = Join-Path $ProjectDir "addons\webrtc_native"
$ListFile   = Join-Path $ProjectDir ".godot\extension_list.cfg"
$Libraries  = @(
    "lib\libwebrtc_native.windows.template_debug.x86_64.dll",
    "lib\libwebrtc_native.windows.template_release.x86_64.dll",
    "lib\libwebrtc_native.linux.template_debug.x86_64.so",
    "lib\libwebrtc_native.linux.template_release.x86_64.so"
)

function Get-Missing {
    $missing = @()
    foreach ($name in @("webrtc_native.gdextension") + $Libraries) {
        if (-not (Test-Path -LiteralPath (Join-Path $AddonDir $name))) { $missing += $name }
    }
    return , $missing
}

# extension_list.cfg is one res:// path per line. Written as UTF-8 without a BOM, as Godot writes it.
function Register-Extension {
    $lines = @()
    if (Test-Path -LiteralPath $ListFile) {
        $lines = @([IO.File]::ReadAllLines($ListFile) | Where-Object { $_.Trim().Length -gt 0 })
    }
    if ($lines -notcontains $Manifest) {
        $lines += $Manifest
        New-Item -ItemType Directory -Force (Split-Path $ListFile) | Out-Null
        [IO.File]::WriteAllText($ListFile, (($lines -join "`n") + "`n"), (New-Object Text.UTF8Encoding($false)))
        Write-Host "Registered $Manifest in $ListFile" -ForegroundColor Green
    }
}

if ($Verify) {
    $missing = Get-Missing
    if ($missing.Count -gt 0) {
        throw "webrtc-native $Version is not installed at $AddonDir ($($missing -join ', ') missing) -- run .\InstallWebRtc.ps1 -Root $Root."
    }
    return [pscustomobject]@{ Version = $Version; Directory = $AddonDir; ZipUrl = $ZipUrl; ZipSha256 = $ZipSha256 }
}

if ((-not $Force) -and (Get-Missing).Count -eq 0) {
    Register-Extension
    Write-Host "webrtc-native $Version already installed at $AddonDir" -ForegroundColor Green
    return
}

# A link here would put the files in another tree; CLAUDE.md forbids links from a worktree into
# the main checkout.
if ((Test-Path -LiteralPath $AddonDir) -and
    ((Get-Item -LiteralPath $AddonDir -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)) {
    throw "$AddonDir is a junction or symlink -- remove it by hand and re-run."
}

# Windows PowerShell 5.1 does not offer TLS 1.2 by default, and GitHub refuses anything older.
[Net.ServicePointManager]::SecurityProtocol =
    [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12

$Staging = Join-Path $env:TEMP "csvm-webrtc-$PID"
if (Test-Path -LiteralPath $Staging) { Remove-Item -LiteralPath $Staging -Recurse -Force }
New-Item -ItemType Directory -Force $Staging | Out-Null
try {
    $zip = Join-Path $Staging "webrtc_native.zip"
    Write-Host "Downloading $ZipUrl" -ForegroundColor Cyan
    $ProgressPreference = "SilentlyContinue"
    Invoke-WebRequest -UseBasicParsing -Uri $ZipUrl -OutFile $zip
    $hash = (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash
    if ($hash -ne $ZipSha256) {
        throw "webrtc-native zip SHA-256 is $hash, expected $ZipSha256 -- refusing to install it."
    }

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [IO.Compression.ZipFile]::OpenRead($zip)
    $unpacked = Join-Path $Staging "webrtc_native"
    try {
        $prefix = "addons/webrtc_native/"
        $wanted = @("webrtc_native.gdextension") + ($Libraries | ForEach-Object { $_.Replace("\", "/") })
        foreach ($entry in $archive.Entries) {
            if (-not $entry.FullName.StartsWith($prefix) -or $entry.Name.Length -eq 0) { continue }
            $relative = $entry.FullName.Substring($prefix.Length)
            if (($wanted -notcontains $relative) -and -not $entry.Name.StartsWith("LICENSE")) { continue }
            $target = Join-Path $unpacked ($relative.Replace("/", "\"))
            New-Item -ItemType Directory -Force (Split-Path $target) | Out-Null
            [IO.Compression.ZipFileExtensions]::ExtractToFile($entry, $target, $true)
        }
    } finally {
        $archive.Dispose()
    }

    # Every file is in staging before any of them replaces an installed one, so a failed run
    # leaves the addon folder as it was.
    foreach ($name in @("webrtc_native.gdextension") + $Libraries) {
        if (-not (Test-Path -LiteralPath (Join-Path $unpacked $name))) { throw "The release zip holds no $name." }
    }

    if (Test-Path -LiteralPath $AddonDir) { Remove-Item -LiteralPath $AddonDir -Recurse -Force }
    New-Item -ItemType Directory -Force (Split-Path $AddonDir) | Out-Null
    Copy-Item -LiteralPath $unpacked -Destination $AddonDir -Recurse
} finally {
    Remove-Item -LiteralPath $Staging -Recurse -Force -ErrorAction SilentlyContinue
}

Register-Extension
Write-Host "Installed webrtc-native $Version at $AddonDir" -ForegroundColor Green
