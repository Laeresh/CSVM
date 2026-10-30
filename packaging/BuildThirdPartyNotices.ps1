<#
.SYNOPSIS
    Regenerates packaging/LICENSE-thirdparty.txt from the payloads and the ported source
    it speaks for.

.DESCRIPTION
    The release zip ships CSVM.exe (the Godot engine statically linked) and a
    self-contained .NET runtime beside it. Each carries notice obligations that CSVM's own
    LICENSE does not cover, so the assembled file is what discharges them. A third
    obligation is not a payload at all: CSVM's managed MPEG-1 decoder is a port of pl_mpeg,
    and MIT requires the notice to travel with the derived source. A fourth is the
    PromptFont file packed into the export, whose SIL Open Font License wants its copyright
    notice and licence carried with it. The header also carries the one line crediting
    mech3ax, whose format research CSVM's game-file readers build on.

    The payload sources are read from the artefacts themselves, or from the toolchain that
    built them, rather than from a copy of upstream's website:

    - Godot. The Windows distribution ships NO LICENSE.txt or COPYRIGHT.txt on disk, and
      neither does the export-template archive: the engine keeps both texts inside the
      binary and hands them back through Engine.get_license_text(),
      get_copyright_info() and get_license_info(). So this script runs the pinned editor
      headless over a throwaway project and reads them out of the same binary the export
      templates were cut from. The dump project is built in TEMP, never in CSVM/, so no
      Godot run here can touch project.godot.
    - .NET. A self-contained publish copies the Microsoft.NETCore.App runtime pack into
      the payload, and that pack ships LICENSE.TXT (MIT) plus THIRD-PARTY-NOTICES.TXT.
      Those two files are the notice the publish requires, taken from the exact version
      in the NuGet cache rather than from the machine-wide dotnet install, which is a
      different (usually newer) build.

    PromptFont follows the same rule. Its copyright statement is read out of the shipped
    font's own name table, and its licence is the LICENSE.txt from the PromptFont release,
    kept byte-identical as packaging/LICENSE-promptfont because that text carries no
    copyright line to read.

    pl_mpeg is the exception to that rule, and cannot be anything else: upstream declares
    MIT through an SPDX-License-Identifier line in its header and publishes no licence
    file, so there is no upstream text to read. The terms are therefore kept in this
    repository as packaging/LICENSE-plmpeg, and the section says in the shipped file where
    that text came from so a reader is not misled into taking it for a verbatim copy.

    ExportRelease.ps1 checks the version stamps this script writes into the file's header
    against what it is actually packaging, and refuses an export whose notice was
    assembled for a different engine, runtime or runtime pack. That check is what makes a
    stale notice a build failure rather than a silent shipping mistake. pl_mpeg and
    PromptFont carry no such stamp because both live in this repository rather than in a
    separately built binary: they move with CSVM's own history, which BUILD-INFO.txt
    already records.

    -Linux also writes packaging/LICENSE-thirdparty-linux.txt, which the Linux tarball
    ships as LICENSE-thirdparty.txt. Its .NET sections are the Linux payload's own, the
    linux-x64 runtime pack the Linux export bundles. Godot's texts are the same for both:
    the engine compiles them from one COPYRIGHT.txt on every platform, and the run checks
    through WSL that the Linux export template reports the same build string as the
    editor they are read from.
    The tarball carries no SDL2, so neither notice has an SDL section; the zip's
    LICENSE-SDL2.txt is SDL's own file beside it.

.EXAMPLE
    .\packaging\BuildThirdPartyNotices.ps1
    Rewrite packaging/LICENSE-thirdparty.txt from the current tools/ and NuGet cache.

.EXAMPLE
    .\packaging\BuildThirdPartyNotices.ps1 -Linux
    The same, then packaging/LICENSE-thirdparty-linux.txt beside it.
#>

[CmdletBinding()]
param(
    # Empty means "the newest win-x64 runtime pack in the NuGet cache", which is what a
    # self-contained publish on this machine picks up. -Linux reads the linux-x64 pack of
    # the same version, since both exports publish from the one SDK.
    [string] $RuntimeVersion = "",

    # The checkout whose git-ignored tools\ holds Godot, as for ExportRelease.ps1's
    # -ToolsRoot; a worktree has none of its own.
    [string] $ToolsRoot = "",

    # Also write the Linux tarball's notice. Needs WSL (Debian) for the template check.
    [switch] $Linux
)

$ErrorActionPreference = "Stop"

$RepoRoot     = Split-Path $PSScriptRoot -Parent
# tools/ is git-ignored, so a worktree with no -ToolsRoot takes it from CSVM_DATA_ROOT, as
# ExportRelease.ps1 does.
if (-not $ToolsRoot) {
    $ToolsRoot = $RepoRoot
    if ((-not (Test-Path (Join-Path $RepoRoot "tools\godot"))) -and $env:CSVM_DATA_ROOT) {
        $ToolsRoot = $env:CSVM_DATA_ROOT
    }
}
$ToolsRoot    = (Resolve-Path $ToolsRoot).Path
$GodotExe     = Join-Path $ToolsRoot "tools\godot\Godot_v4.7-stable_mono_win64\Godot_v4.7-stable_mono_win64_console.exe"
$PlMpegFile   = Join-Path $PSScriptRoot "LICENSE-plmpeg"
$PromptFontLicense = Join-Path $PSScriptRoot "LICENSE-promptfont"
$PromptFontFile    = Join-Path $RepoRoot "CSVM\data\promptfont.ttf.bin"
$PackCache    = Join-Path $env:USERPROFILE ".nuget\packages"
$PackRoot     = Join-Path $PackCache "microsoft.netcore.app.runtime.win-x64"
$LinuxTemplate = Join-Path $env:APPDATA "Godot\export_templates\4.7.stable.mono\linux_release.x86_64"
$LinuxDistro  = "Debian"

# What differs between the two notices. Pack is also written into the header as a stamp, which
# ExportRelease.ps1 and sandbox\LinuxRelease.ps1 read back, so a notice built for one platform
# cannot ship in the other's archive. Sep is the path separator the recipient's system uses, so
# the file names read as they appear in that archive.
$Platforms = @(
    [pscustomobject]@{
        Name = "Windows"; OutFile = Join-Path $PSScriptRoot "LICENSE-thirdparty.txt"
        Pack = "win-x64"; Archive = "zip"; Sep = "\"
        Exe = "CSVM.exe"; DataDir = "data_CSVM_windows_x86_64"
    }
)
if ($Linux) {
    $Platforms += [pscustomobject]@{
        Name = "Linux"; OutFile = Join-Path $PSScriptRoot "LICENSE-thirdparty-linux.txt"
        Pack = "linux-x64"; Archive = "tarball"; Sep = "/"
        Exe = "CSVM.x86_64"; DataDir = "data_CSVM_linuxbsd_x86_64"
    }
}

$Rule = "=" * 78
$Thin = "-" * 78

# BOM-less UTF-8 everywhere: PowerShell 5.1's Get-Content/Set-Content default to ANSI and
# would double-encode every copyright sign in 800 KB of upstream text (verification.md
# SHELL-7, CLAUDE.md). Read and write through the .NET APIs, which detect and emit UTF-8.
$Utf8NoBom = New-Object System.Text.UTF8Encoding($false)
function Read-Utf8([string] $Path) { return [System.IO.File]::ReadAllText($Path) }

$requiredPaths = @($GodotExe, $PackRoot, $PromptFontFile, $PromptFontLicense)
if ($Linux) { $requiredPaths += $LinuxTemplate }
foreach ($required in $requiredPaths) {
    if (-not (Test-Path $required)) {
        throw "Not found: $required -- see PROJECT_CONTEXT.md for the tools/ setup."
    }
}

if (-not (Test-Path $PlMpegFile)) {
    throw "Not found: $PlMpegFile -- the MIT terms the pl_mpeg port ships under live in " +
        "the repository, because upstream publishes no licence file to read them from."
}

# ---------------------------------------------------------------- Godot

$godotBuild = (& $GodotExe --version | Select-Object -Last 1).Trim()
if (-not $godotBuild) {
    throw "Could not read a version string from $GodotExe."
}

$dumpDir = Join-Path $env:TEMP "csvm-notices-$PID"
if (Test-Path $dumpDir) { Remove-Item $dumpDir -Recurse -Force }
New-Item -ItemType Directory -Force $dumpDir | Out-Null
try {
    [System.IO.File]::WriteAllText(
        (Join-Path $dumpDir "project.godot"),
        "config_version=5`n`n[application]`n`nconfig/name=`"csvm-notices`"`n",
        $Utf8NoBom)

    # Space-indented on purpose: a tab that a copy-paste turns into spaces is a GDScript
    # parse error, and this script is edited far more often than it is read by Godot.
    $dumpScript = @'
extends SceneTree


func _init():
    var f := FileAccess.open("res://license.txt", FileAccess.WRITE)
    f.store_string(Engine.get_license_text())
    f.close()

    # COPYRIGHT.txt's machine-readable (DEP5) stanzas, rebuilt from the engine's tables.
    f = FileAccess.open("res://copyright.txt", FileAccess.WRITE)
    for component in Engine.get_copyright_info():
        f.store_string("Comment: " + str(component["name"]) + "\n")
        for part in component["parts"]:
            var files: Array = Array(part["files"])
            f.store_string("Files: " + "\n       ".join(files) + "\n")
            var holders: Array = Array(part["copyright"])
            f.store_string("Copyright: " + "\n           ".join(holders) + "\n")
            f.store_string("License: " + str(part["license"]) + "\n")
        f.store_string("\n")
    f.close()

    f = FileAccess.open("res://license_info.txt", FileAccess.WRITE)
    var info := Engine.get_license_info()
    for k in info:
        f.store_string("License: " + str(k) + "\n")
        f.store_string(str(info[k]))
        f.store_string("\n\n")
    f.close()

    quit()
'@
    [System.IO.File]::WriteAllText((Join-Path $dumpDir "dump.gd"), $dumpScript, $Utf8NoBom)

    Write-Host "Reading Godot $godotBuild licence tables out of the engine..." -ForegroundColor Cyan
    & $GodotExe --headless --path $dumpDir --script dump.gd | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "Godot licence dump failed (exit $LASTEXITCODE)."
    }

    $godotLicense   = Read-Utf8 (Join-Path $dumpDir "license.txt")
    $godotCopyright = Read-Utf8 (Join-Path $dumpDir "copyright.txt")
    $godotLicenses  = Read-Utf8 (Join-Path $dumpDir "license_info.txt")
} finally {
    Remove-Item $dumpDir -Recurse -Force -ErrorAction SilentlyContinue
}

# The texts above come out of the Windows editor. The Linux export template is a different
# binary of the same build, compiled from the same COPYRIGHT.txt, so its tables are the same
# ones; what can drift is which build the installed template is. Its own --version answers
# that, and only a Linux process can run it.
if ($Linux) {
    $templateWsl = (& wsl.exe -d $LinuxDistro --exec wslpath -u $LinuxTemplate)
    if ($LASTEXITCODE -ne 0 -or -not $templateWsl) {
        throw "WSL distro '$LinuxDistro' is not reachable to run $LinuxTemplate -- one-time " +
            "setup: 'wsl --install -d Debian' (ExportRelease.ps1 -Linux needs it too)."
    }
    $templateBuild = (& wsl.exe -d $LinuxDistro --exec $templateWsl.Trim() --version |
        Select-Object -Last 1)
    if ($LASTEXITCODE -ne 0 -or "$templateBuild".Trim() -ne $godotBuild) {
        throw "The Linux export template $LinuxTemplate reports build '$templateBuild', not the " +
            "editor's $godotBuild -- reinstall the templates from the pinned .tpz."
    }
}

# ---------------------------------------------------------------- .NET runtime

if (-not $RuntimeVersion) {
    # Sort as versions, not as strings: 8.0.9 sorts after 8.0.30 alphabetically.
    $RuntimeVersion = (Get-ChildItem $PackRoot -Directory |
        Sort-Object { [version] $_.Name } | Select-Object -Last 1).Name
}
foreach ($platform in $Platforms) {
    $packRootHere = Join-Path $PackCache "microsoft.netcore.app.runtime.$($platform.Pack)"
    $packDir = Join-Path $packRootHere $RuntimeVersion
    if (-not (Test-Path $packDir)) {
        throw "Runtime pack $($platform.Pack) $RuntimeVersion not in the NuGet cache " +
            "($packRootHere) -- export the $($platform.Name) build once so the pack is " +
            "restored, or pass -RuntimeVersion for one that is there."
    }
    $platform | Add-Member PackName "Microsoft.NETCore.App.Runtime.$($platform.Pack)"
    $platform | Add-Member DotnetLicense (Read-Utf8 (Join-Path $packDir "LICENSE.TXT"))
    $platform | Add-Member DotnetNotices (Read-Utf8 (Join-Path $packDir "THIRD-PARTY-NOTICES.TXT"))
}

# ---------------------------------------------------------------- pl_mpeg

$plmpegLicense = Read-Utf8 $PlMpegFile

$plmpegIntro = @"
CSVM's MPEG-1 video and MP2 audio decoding is a port of pl_mpeg, a single-header
C library by Dominic Szablewski (https://phoboslab.org), published at
https://github.com/phoboslab/pl_mpeg. The C source is not in this archive and no
pl_mpeg binary is shipped: the code that runs is C# derived from it, so this
notice is carried for the source CSVM's own decoder is written from rather than
for a component inside a binary here.

pl_mpeg declares its terms as the line SPDX-License-Identifier: MIT in its
header. It publishes no LICENSE file and reproduces no licence block in the
header itself, so there is no upstream text to copy. What follows is therefore
the standard MIT licence with pl_mpeg's author named as the copyright holder,
kept in this project as packaging\LICENSE-plmpeg, and not a verbatim copy of a
file upstream distributes. No copyright year is given because upstream states
none.

$($plmpegLicense.TrimEnd())
"@

# ---------------------------------------------------------------- PromptFont

# WPF's GlyphTypeface throws on the shipped .bin name and reads the same bytes under .ttf,
# so the name table is read from a TEMP copy.
Add-Type -AssemblyName PresentationCore
$fontCopy = Join-Path $env:TEMP "csvm-notices-promptfont-$PID.ttf"
Copy-Item $PromptFontFile $fontCopy -Force
try {
    $typeface = New-Object System.Windows.Media.GlyphTypeface([Uri] $fontCopy)
    $fontCopyright = $typeface.Copyrights[[System.Globalization.CultureInfo] "en-US"]
} finally {
    Remove-Item $fontCopy -Force -ErrorAction SilentlyContinue
}
if (-not $fontCopyright) {
    throw "$PromptFontFile carries no en-US copyright string in its name table."
}
$fontCopyrightLines = (($fontCopyright -split "`r?`n") | Where-Object { $_.Trim() } |
    ForEach-Object { "  " + $_.Trim() }) -join "`n"
$promptFontLicenseText = Read-Utf8 $PromptFontLicense

function Get-PromptFontIntro([string] $Sep) {
    return @"
The controller button pictures CSVM draws in its prompts are characters of
PromptFont by Yukari "Shinmera" Hafner, published at
https://github.com/Shinmera/promptfont. The font file is packed unmodified inside
the exported .pck as data$($Sep)promptfont.ttf.bin; only the file name differs from
upstream's promptfont.ttf.

The font's copyright statement, as its own name table carries it:

$script:fontCopyrightLines

The licence text follows, byte-identical to the LICENSE.txt in the PromptFont
release and kept in this project as packaging\LICENSE-promptfont.

$($script:promptFontLicenseText.TrimEnd())
"@
}

# ---------------------------------------------------------------- assembly

function Section([string] $Number, [string] $Title, [string] $Body) {
    return @(
        $script:Rule
        "$Number. $Title"
        $script:Rule
        ""
        $Body.TrimEnd()
        ""
        ""
    ) -join "`n"
}

function Build-Notice($P) {
    $dataDir = $P.DataDir + $P.Sep
    $flag = if ($P.Name -eq "Linux") { " -Linux" } else { "" }
    $header = @"
CSVM third-party notices
========================

This file carries the notices that the third-party software inside this build is
licensed on condition of carrying, and one notice for third-party source CSVM's own
code is ported from. It is not CSVM's own licence: CSVM is under the GNU General
Public License v3, whose text is in LICENSE beside this file.

This archive contains no Crimson Skies code, data or artwork: CSVM reads the
player's own game files in place. Its readers of those files build on the
format research of mech3ax, https://github.com/TerranMechworks/mech3ax.

Assembled from these exact payload versions, which ExportRelease.ps1 re-checks
against what it packages before it will build a $($P.Archive):

  Godot Engine build: $script:godotBuild
  .NET runtime version: $script:RuntimeVersion
  .NET runtime pack: $($P.PackName)

Regenerate with packaging\BuildThirdPartyNotices.ps1$flag, which reads every
text in sections 1 to 5 and 7 out of the shipped artefacts themselves.
Section 6 is the one exception and says so in its own text: pl_mpeg publishes
no licence file to read.

Sections
--------

  1. Godot Engine, the engine linked into $($P.Exe)
  2. Godot Engine third-party components
  3. Godot Engine third-party licence texts
  4. .NET runtime, published self-contained into $dataDir
  5. .NET runtime third-party notices
  6. pl_mpeg, the MPEG-1 decoder CSVM's video code is ported from
  7. PromptFont, the font the controller button pictures are drawn from

"@

    $sections = @(
        $header
        (Section "1" "Godot Engine, the engine linked into $($P.Exe)" $script:godotLicense)
        (Section "2" "Godot Engine third-party components" $script:godotCopyright)
        (Section "3" "Godot Engine third-party licence texts" $script:godotLicenses)
        (Section "4" "The .NET runtime published into $dataDir" $P.DotnetLicense)
        (Section "5" ".NET runtime third-party notices" $P.DotnetNotices)
        (Section "6" "pl_mpeg, the MPEG-1 decoder CSVM's video code is ported from" $script:plmpegIntro)
        (Section "7" "PromptFont, the font the controller button pictures are drawn from" (Get-PromptFontIntro $P.Sep))
    )
    return $sections -join "`n"
}

# The markers ExportRelease.ps1 and sandbox\LinuxRelease.ps1 refuse in the other platform's
# archive. Checked here too, so a notice that would fail there is never written.
$ForeignMarkers = @{
    Windows = @("linux-x64", "data_CSVM_linuxbsd_x86_64", "CSVM.x86_64")
    Linux   = @("win-x64", "data_CSVM_windows_x86_64", "CSVM.exe")
}

foreach ($platform in $Platforms) {
    $document = Build-Notice $platform

    # Godot 4.7 embeds the FreeType licence with its copyright sign ALREADY double-encoded --
    # the engine hands back U+00C2 U+00A9 where one copyright sign was meant -- and the repo's
    # own CheckEncoding.ps1 is right to refuse to commit that. Undoing the sequence restores
    # the character upstream meant and changes no term of any licence, but it is still a
    # deliberate edit to a verbatim text, so it is confined to that one pattern and counted out
    # loud: a second occurrence turning up here is something to go and look at, not to wave
    # through. Written as regex escapes, not as the characters themselves: a BOM-less .ps1
    # carrying non-ASCII is mangled by PowerShell 5.1's own interpreter before it runs.
    $repairPattern = '\u00C2([\u00A0-\u00BF])'
    $repaired = [regex]::Matches($document, $repairPattern).Count
    $document = [regex]::Replace($document, $repairPattern, '$1')

    foreach ($marker in $ForeignMarkers[$platform.Name]) {
        if ($document.IndexOf($marker, [StringComparison]::OrdinalIgnoreCase) -ge 0) {
            throw "The $($platform.Name) notice names '$marker', which belongs to the other " +
                "platform's archive -- an upstream text or this script's wording has changed."
        }
    }

    # CRLF throughout, as git's autocrlf checkout of every text file here gives it, so a
    # regenerated file and a checked-out one are the same bytes. Normalise from LF rather than
    # replacing blindly, or the upstream files that already use CRLF gain a second carriage
    # return per line. The Linux notice too: git stores it LF, and ExportRelease.ps1 stages the
    # tarball's text files with LF, the committed bytes.
    $document = ($document -replace "`r`n", "`n") -replace "`n", "`r`n"
    [System.IO.File]::WriteAllText($platform.OutFile, $document, $Utf8NoBom)

    Write-Host "Wrote $($platform.OutFile)" -ForegroundColor Green
    Write-Host "  Godot $godotBuild, .NET runtime $RuntimeVersion ($($platform.PackName))"
    Write-Host "  Repaired $repaired double-encoded character(s) in upstream text (expected 1, Godot's FreeType notice)"
}
Write-Host "  pl_mpeg's MIT terms taken from packaging\LICENSE-plmpeg (upstream ships no licence file)"
Write-Host "  PromptFont's copyright read from its name table, its OFL text from packaging\LICENSE-promptfont"

