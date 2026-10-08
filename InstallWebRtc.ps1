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
      LICENSE.*                          the seven licences the release carries
      lib\*.windows.template_*.x86_64.dll  Windows x64, debug and release
      lib\*.linux.template_*.x86_64.so     Linux x64, debug and release

    and adds res://addons/webrtc_native/webrtc_native.gdextension to
    <Root>\CSVM\.godot\extension_list.cfg, which is where a run that never opened the editor looks
    for extensions. An editor import writes the same line, so the two never disagree.

    Without the extension the game runs as before: LAN and direct play are unchanged, and a master
    server set in the options lists games but cannot host or join over the internet.

    Every installed file is pinned by SHA-256 as well as the zip, so a run over an install that
    differs from the pins (an older pin, a hand-replaced or corrupt library) reinstalls it.

    -Verify installs nothing. It checks every installed file against its pin and throws naming
    this script as the fix.

.PARAMETER Root
    The checkout to install into. Defaults to this script's own tree. A worktree needs its own
    install, since the extension must sit inside the project folder Godot opens.

.PARAMETER Force
    Re-download even when the installed files already match the pins.

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
# Every file this script installs, with its SHA-256 as extracted from the pinned zip. A presence
# check alone passed a hand-replaced or corrupt library, and an install left over from an older
# pin, while ExportRelease.ps1 records this pin's version in BUILD-INFO.txt. Moving $Version means
# re-hashing every file here, not only the zip.
$Pinned = @(
    @{ Name = "webrtc_native.gdextension";                                Sha = "7956ED5526B81811E9EBCCB44C576B665CE1D9E44CB0766122F611D13A36F0A8" },
    @{ Name = "lib\libwebrtc_native.windows.template_debug.x86_64.dll";   Sha = "0FAFF9C71966DE25C09B6959D2B436C62452C661498DE8F0BF85F5ECE0CB32CD" },
    @{ Name = "lib\libwebrtc_native.windows.template_release.x86_64.dll"; Sha = "3A8053D325A874493F596630B0F165F3A83E265C61D9FD87D82DA6CB4C2A81FD" },
    @{ Name = "lib\libwebrtc_native.linux.template_debug.x86_64.so";      Sha = "D99224F5411C5083F1D0932F2ED30B0B68F5F697BE025671FF0083015F06A033" },
    @{ Name = "lib\libwebrtc_native.linux.template_release.x86_64.so";    Sha = "88C927C551592F526FDB13FAB28536629FAE026F8D738D7D83CE2CF2493DB9E3" },
    @{ Name = "LICENSE.libdatachannel"; Sha = "FAB3DD6BDAB226F1C08630B1DD917E11FCB4EC5E1E020E2C16F83A0A13863E85" },
    @{ Name = "LICENSE.libjuice";       Sha = "FAB3DD6BDAB226F1C08630B1DD917E11FCB4EC5E1E020E2C16F83A0A13863E85" },
    @{ Name = "LICENSE.libsrtp";        Sha = "8E19D42A1EEC9561F3F347253DDF2E385C55F392F025BB0FD41B88DBF38DB5AE" },
    @{ Name = "LICENSE.mbedtls";        Sha = "9B405EF4C89342F5EAE1DD828882F931747F71001CFBA7D114801039B52AD09B" },
    @{ Name = "LICENSE.plog";           Sha = "E4D01796524CBC13B1571A1F0914823C6A23B316BA039E7014F47A4EEF7FD4C3" },
    @{ Name = "LICENSE.usrsctp";        Sha = "FA53711B25AF4B9A9B8DADFEA3CB38166EC4B96760C8D62B284055554537D9EF" },
    @{ Name = "LICENSE.webrtc-native";  Sha = "0009CCA297DA2FB379EBF86B348A7E21B592F4253F2576C8F804800A0A9A813A" }
)

function Get-Sha256([string] $Path) {
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash
}

# Each pinned file under $Dir that is missing or differs, by name; empty when the install is complete.
function Get-Mismatches([string] $Dir = $AddonDir) {
    $bad = @()
    foreach ($file in $Pinned) {
        $path = Join-Path $Dir $file.Name
        if (-not (Test-Path -LiteralPath $path)) {
            $bad += "$($file.Name) (missing)"
        } elseif ((Get-Sha256 $path) -ne $file.Sha) {
            $bad += "$($file.Name) (SHA-256 differs from the pin)"
        }
    }
    return , $bad
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
    $bad = Get-Mismatches
    if ($bad.Count -gt 0) {
        throw "webrtc-native $Version is not installed at $AddonDir ($($bad -join ', ')) -- run .\InstallWebRtc.ps1 -Root $Root."
    }
    return [pscustomobject]@{ Version = $Version; Directory = $AddonDir; ZipUrl = $ZipUrl; ZipSha256 = $ZipSha256 }
}

if ((-not $Force) -and (Get-Mismatches).Count -eq 0) {
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
        $wanted = @($Pinned | ForEach-Object { $_.Name.Replace("\", "/") })
        foreach ($entry in $archive.Entries) {
            if (-not $entry.FullName.StartsWith($prefix) -or $entry.Name.Length -eq 0) { continue }
            $relative = $entry.FullName.Substring($prefix.Length)
            if ($wanted -notcontains $relative) { continue }
            $target = Join-Path $unpacked ($relative.Replace("/", "\"))
            New-Item -ItemType Directory -Force (Split-Path $target) | Out-Null
            [IO.Compression.ZipFileExtensions]::ExtractToFile($entry, $target, $true)
        }
    } finally {
        $archive.Dispose()
    }

    # Every file is checked in staging before any of them replaces an installed one, so a failed
    # run leaves the addon folder as it was.
    $bad = Get-Mismatches $unpacked
    if ($bad.Count -gt 0) {
        throw "The release zip does not match the pins ($($bad -join ', ')) -- refusing to install it."
    }

    if (Test-Path -LiteralPath $AddonDir) { Remove-Item -LiteralPath $AddonDir -Recurse -Force }
    New-Item -ItemType Directory -Force (Split-Path $AddonDir) | Out-Null
    Copy-Item -LiteralPath $unpacked -Destination $AddonDir -Recurse
} finally {
    Remove-Item -LiteralPath $Staging -Recurse -Force -ErrorAction SilentlyContinue
}

Register-Extension
Write-Host "Installed webrtc-native $Version at $AddonDir" -ForegroundColor Green
