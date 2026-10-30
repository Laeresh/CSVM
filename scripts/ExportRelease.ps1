<#
.SYNOPSIS
    Builds CSVM, exports the "Windows Desktop" release preset, and stages the whole
    friend-facing release payload in .scratch\export\. With -Linux it also exports the
    "Linux/X11" preset and packages the Linux tarball.

.DESCRIPTION
    The packaging entry point (see PROJECT_CONTEXT.md "Exporting a release build" and
    docs/tooling.md). Runs `dotnet build`, imports the project headless (needed once per
    fresh tree before an export can see every asset), exports the release preset defined
    in CSVM/export_presets.cfg, then copies the non-export pieces of the release listed
    in packaging/MANIFEST.md next to it, so .scratch\export\ is the zip's contents.

    The zip is CSVM-v<version>-win64.zip, named from CSVM/project.godot's
    application/config/version -- the same key the exported exe's file properties and the
    first line of every log state, so all three agree by construction.

    Copying is what keeps MANIFEST.md's "byte-identical to the repo source" rule true by
    construction: the READMEs and licences are taken from their one home in the
    repo on every export, never forked into a package variant that can drift. The zip takes
    the checkout's bytes (CRLF under autocrlf); the tarball's text files are staged with LF,
    the bytes git stores.

    Two of the zip's files are about the build rather than part of it.
    LICENSE-thirdparty.txt is copied like any other payload row, but it names the Godot
    build and the .NET runtime version and pack it was assembled for, and this script
    re-checks all of them against what it is packaging. The tarball ships
    packaging\LICENSE-thirdparty-linux.txt under that name. BUILD-INFO.txt is the one
    generated file: it records the CSVM commit the shipped executable was built from, which
    is what lets a release page state the source it came from.

    Godot's export templates are user-global, not part of this repo, and there is no
    reliable way to install them unattended -- so this script checks for them first and
    throws a clear error naming the one-time setup step instead of letting Godot's own
    export fail cryptically partway through.

    -Linux is opt-in and leaves the Windows half untouched: the same run then exports the
    "Linux/X11" preset into .scratch\export-linux\, stages the Linux payload beside it and
    packs CSVM-v<version>-linux-x64.tar.gz inside WSL (Debian), because only a tar written
    on Linux carries the executable bit that CSVM.x86_64 needs. A zip made here cannot,
    which is why the Linux download is a tarball.

.PARAMETER Linux
    Also export the Linux build and package the .tar.gz. Needs the Linux export templates
    and a WSL Debian distro with tar (docs/tooling.md, "-Linux").

.EXAMPLE
    .\scripts\ExportRelease.ps1
    Build, import, export to .scratch\export\CSVM.exe, and stage the release files.

.EXAMPLE
    .\scripts\ExportRelease.ps1 -Linux
    The same, then the Linux export in .scratch\export-linux\ and its tarball.

.EXAMPLE
    .\scripts\ExportRelease.ps1 -ToolsRoot Z:\CSVM
    The same from a worktree, taking Godot and SDL2 from the main checkout's tools\, since
    tools\ is git-ignored and a worktree has none.

.PARAMETER ToolsRoot
    The checkout whose tools\ folder holds Godot and SDL2. Defaults to this
    script's own folder, or to CSVM_DATA_ROOT when this folder has no tools\godot (a worktree),
    the same fallback RunTests.ps1 uses. Only the tools move: the build, the payload and the
    recorded CSVM commit stay this tree's own.
#>
param(
    [string] $ToolsRoot = "",
    [switch] $Linux
)

$ErrorActionPreference = "Stop"

$RepoRoot   = Split-Path $PSScriptRoot -Parent
if (-not $ToolsRoot) {
    $ToolsRoot = $RepoRoot
    if ((-not (Test-Path (Join-Path $RepoRoot "tools\godot"))) -and $env:CSVM_DATA_ROOT) {
        $ToolsRoot = $env:CSVM_DATA_ROOT
    }
}
$ToolsRoot  = (Resolve-Path $ToolsRoot).Path
$ProjectDir = Join-Path $RepoRoot "CSVM"
$Sln        = Join-Path $ProjectDir "CSVM.sln"
$GodotExe   = Join-Path $ToolsRoot "tools\godot\Godot_v4.7-stable_mono_win64\Godot_v4.7-stable_mono_win64_console.exe"

$TemplateDir = Join-Path $env:APPDATA "Godot\export_templates\4.7.stable.mono"
$ExportDir   = Join-Path $RepoRoot ".scratch\export"
$ExportExe   = Join-Path $ExportDir "CSVM.exe"
$ProjectGodot = Join-Path $ProjectDir "project.godot"
$ThirdPartyNotices = Join-Path $RepoRoot "packaging\LICENSE-thirdparty.txt"
# The tarball's notice, shipped under the same name: its runtime pack is the Linux payload's own
# (packaging\BuildThirdPartyNotices.ps1 -Linux).
$LinuxThirdPartyNotices = Join-Path $RepoRoot "packaging\LICENSE-thirdparty-linux.txt"
$BuildInfo    = Join-Path $ExportDir "BUILD-INFO.txt"
$Sdl2Dir      = Join-Path $ToolsRoot "tools\sdl2"
# webrtc-native sits inside the project, not in tools\, because Godot only exports an extension the
# project it opens contains. Its licences ship from the release's own files, in one folder.
$WebRtcDir      = Join-Path $ProjectDir "addons\webrtc_native"
$WebRtcLicences = @("libdatachannel", "libjuice", "libsrtp", "mbedtls", "plog", "usrsctp", "webrtc-native") |
    ForEach-Object { @{ Source = Join-Path $WebRtcDir "LICENSE.$_"; Dest = "LICENSE-webrtc\LICENSE.$_" } }

$LinuxExportDir = Join-Path $RepoRoot ".scratch\export-linux"
$LinuxExportExe = Join-Path $LinuxExportDir "CSVM.x86_64"
$LinuxDistro    = "Debian"
# The Linux README, shipped as README.md at the tarball root like the zip's own. A separate
# file because the Windows one describes SmartScreen and Direct3D 12.
$LinuxReadme = Join-Path $RepoRoot "packaging\README-linux.md"

# The zip payload beside the export output, from packaging/MANIFEST.md. Sources are the
# files' one home in the repo, so a copy is byte-identical to what the manifest names.
$ReleaseFiles = @(
    @{ Source = Join-Path $RepoRoot "packaging\README.md";      Dest = "README.md" },
    @{ Source = Join-Path $RepoRoot "packaging\CSVM-VR.cmd";    Dest = "CSVM-VR.cmd" },
    @{ Source = Join-Path $RepoRoot "packaging\LICENSE";        Dest = "LICENSE" },
    @{ Source = $ThirdPartyNotices;                             Dest = "LICENSE-thirdparty.txt" },
    @{ Source = Join-Path $Sdl2Dir "SDL2.dll";                  Dest = "SDL2.dll" },
    @{ Source = Join-Path $Sdl2Dir "README-SDL.txt";            Dest = "README-SDL.txt" },
    @{ Source = Join-Path $Sdl2Dir "LICENSE.txt";               Dest = "LICENSE-SDL2.txt" }
) + $WebRtcLicences

# The tarball payload, packaging/MANIFEST.md's Linux table: the zip's list with the Linux README
# and the Linux notices; the zip's VR launcher stays out, since VR is the Windows build's alone.
# Lf rows are text: git stores them LF and this checkout's autocrlf hands them over CRLF, so they
# are staged with the carriage returns taken out, which is the committed bytes. The zip keeps the
# checkout's CRLF, the Windows convention, and no attribute changes what any tree checks out.
$LinuxReleaseFiles = @(
    @{ Source = $LinuxReadme;                                   Dest = "README.md"; Lf = $true },
    @{ Source = Join-Path $RepoRoot "packaging\LICENSE";        Dest = "LICENSE"; Lf = $true },
    @{ Source = $LinuxThirdPartyNotices;                        Dest = "LICENSE-thirdparty.txt"; Lf = $true }
) + $WebRtcLicences

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

# SDL2.dll is the flight-stick reader (docs/tooling.md, "SDL2 for flight sticks"). A dev launch
# without it only loses sticks, but a release without it ships a build that cannot see them, so
# the export requires the pinned files. InstallSdl2.ps1 holds the pins and throws naming itself.
$Sdl2 = & (Join-Path $PSScriptRoot "InstallSdl2.ps1") -Root $ToolsRoot -Verify

# webrtc-native carries internet play (docs/tooling.md, "The WebRTC library and the master
# server"). A build exported without it lists the master server's games but can neither host nor
# join one, and nothing at export time would say so, so the export installs the pinned release into
# this tree itself rather than only checking for it. InstallWebRtc.ps1 holds the pin and the hash.
& (Join-Path $PSScriptRoot "InstallWebRtc.ps1") -Root $RepoRoot
$WebRtc = & (Join-Path $PSScriptRoot "InstallWebRtc.ps1") -Root $RepoRoot -Verify

foreach ($file in $ReleaseFiles) {
    if (-not (Test-Path $file.Source)) {
        throw "Release payload file not found at $($file.Source) -- see packaging\MANIFEST.md."
    }
}

# The Linux half's two prerequisites live outside the repo, like the templates above, and each
# would otherwise surface only after the Windows export has spent its minutes.
if ($Linux) {
    $linuxTemplate = Join-Path $TemplateDir "linux_release.x86_64"
    if (-not (Test-Path $linuxTemplate)) {
        throw "Linux export template not found at $linuxTemplate -- the one-time template " +
            "install above must include the linux_release.x86_64 file of the .tpz."
    }
    try {
        & wsl.exe -d $LinuxDistro --exec tar --version | Out-Null
        $wslExit = $LASTEXITCODE
    } catch {
        $wslExit = -1
    }
    if ($wslExit -ne 0) {
        throw "WSL distro '$LinuxDistro' with tar is not reachable (wsl.exe -d $LinuxDistro) -- " +
            "one-time setup: 'wsl --install -d Debian'. The tarball is packed inside it so the " +
            "executable bits survive."
    }
}

# PowerShell 5.1 mangles embedded double quotes in a native command's arguments, so every WSL step
# is a script file with LF endings, run with its paths passed as arguments.
function ConvertTo-WslPath([string] $Path) {
    $wslPath = (& wsl.exe -d $script:LinuxDistro --exec wslpath -u $Path)
    if ($LASTEXITCODE -ne 0 -or -not $wslPath) {
        throw "wslpath could not translate $Path for WSL."
    }
    return $wslPath.Trim()
}
function Write-WslScript([string] $Name, [string] $Text) {
    $path = Join-Path $script:RepoRoot ".scratch\$Name-$PID.sh"
    New-Item -ItemType Directory -Force (Split-Path $path -Parent) | Out-Null
    [System.IO.File]::WriteAllText($path, ($Text -replace "`r`n", "`n"),
        (New-Object System.Text.UTF8Encoding($false)))
    return $path
}

if ($Linux) {
    foreach ($file in $LinuxReleaseFiles) {
        if (-not (Test-Path $file.Source)) {
            throw "Linux payload file not found at $($file.Source) -- see packaging\MANIFEST.md."
        }
    }
}

# Each notice speaks for payloads whose versions it names in its own header
# (packaging\BuildThirdPartyNotices.ps1 assembles both). A notice assembled for a different
# engine or runtime is worse than none: it states, in the archive, obligations that belong to
# software the archive does not contain. So the header is read back and re-checked against what
# this run is actually packaging. The runtime pack stamp, and the other platform's names being
# absent, are what keep the zip's notice out of the tarball and the tarball's out of the zip;
# sandbox\LinuxRelease.ps1 repeats the Linux half on the built tarball.
# Read through ReadAllText because the file is BOM-less UTF-8 and 5.1's own readers would decode
# it as ANSI (verification.md SHELL-7).
function Read-Notice([string] $Path, [string] $Pack, [string[]] $Foreign, [string] $Regenerate) {
    $name = "packaging\" + (Split-Path $Path -Leaf)
    $text = [System.IO.File]::ReadAllText($Path)
    $stamp = {
        param([string] $Label, [string] $Pattern)
        $match = [regex]::Match($text, $Pattern)
        if (-not $match.Success) {
            throw "$name states no $Label -- regenerate it with $Regenerate."
        }
        return $match.Groups[1].Value
    }
    $notice = [pscustomobject]@{
        Name       = $name
        Regenerate = $Regenerate
        Godot      = & $stamp "Godot Engine build" 'Godot Engine build: (\S+)'
        Runtime    = & $stamp ".NET runtime version" '\.NET runtime version: (\S+)'
        Pack       = & $stamp ".NET runtime pack" '\.NET runtime pack: (\S+)'
    }
    if ($notice.Pack -ne "Microsoft.NETCore.App.Runtime.$Pack") {
        throw "$name was assembled from $($notice.Pack), not the $Pack pack -- regenerate it " +
            "with $Regenerate."
    }
    foreach ($marker in $Foreign) {
        if ($text.IndexOf($marker, [StringComparison]::OrdinalIgnoreCase) -ge 0) {
            throw "$name names '$marker', which belongs to the other platform's archive -- " +
                "regenerate it with $Regenerate."
        }
    }
    return $notice
}
$notices = @(Read-Notice $ThirdPartyNotices "win-x64" `
    @("linux-x64", "data_CSVM_linuxbsd_x86_64", "CSVM.x86_64") `
    "packaging\BuildThirdPartyNotices.ps1")
$winNotice = $notices[0]
if ($Linux) {
    $linuxNotice = Read-Notice $LinuxThirdPartyNotices "linux-x64" `
        @("win-x64", "data_CSVM_windows_x86_64", "CSVM.exe") `
        "packaging\BuildThirdPartyNotices.ps1 -Linux"
    $notices += $linuxNotice
}

$godotBuild = (& $GodotExe --version | Select-Object -Last 1).Trim()
foreach ($notice in $notices) {
    if ($godotBuild -ne $notice.Godot) {
        throw "$($notice.Name) was assembled for Godot $($notice.Godot) but this export runs " +
            "$godotBuild -- regenerate it with $($notice.Regenerate)."
    }
}

$csvmCommit = (& git -C $RepoRoot rev-parse HEAD 2>$null)
if ($LASTEXITCODE -ne 0 -or $csvmCommit -notmatch '^[0-9a-f]{40}$') {
    throw "Could not resolve HEAD in $RepoRoot -- the exe's source commit is part of the release."
}
$csvmDirty = [bool] (& git -C $RepoRoot status --porcelain)

# The version has one home: project.godot's application/config/version, which the engine reads at
# startup for the log's first line and the launchscreen's corner, and which the export stamps into
# the exe. Read back here so the zip's name cannot disagree with what is inside it. -Encoding utf8
# because 5.1 decodes a BOM-less file as ANSI (CLAUDE.md); the key itself is ASCII, the file is not
# necessarily. Exactly one match, so a second definition is an error rather than a coin toss.
$versionMatch = @(Get-Content $ProjectGodot -Encoding utf8 | Select-String -Pattern '^config/version="([^"]+)"')
if ($versionMatch.Count -ne 1) {
    throw "Expected exactly one config/version in $ProjectGodot, found $($versionMatch.Count) -- " +
        "the release's version number lives there and nowhere else (docs/tooling.md)."
}
$Version = $versionMatch[0].Matches[0].Groups[1].Value
$ZipPath = Join-Path $RepoRoot ".scratch\CSVM-v$Version-win64.zip"
$TarPath = Join-Path $RepoRoot ".scratch\CSVM-v$Version-linux-x64.tar.gz"
Write-Host "Version $Version (CSVM\project.godot)" -ForegroundColor Cyan

# The staging folder is rebuilt from nothing each run, because everything in it is copied into
# the archive: a file left by an earlier export or a hand assembly would otherwise ship forever.
# PowerShell 5.1's recursive delete FOLLOWS directory junctions into their target (see
# CleanScratch.ps1), so refuse to sweep a folder someone has linked something into rather than
# deleting whatever is on the far side of the link.
function Clear-StagingDir([string] $Dir) {
    if (Test-Path $Dir) {
        $links = Get-ChildItem $Dir -Recurse -Directory -Force |
            Where-Object { $_.Attributes -band [System.IO.FileAttributes]::ReparsePoint }
        if ($links) {
            throw "$Dir contains a junction or symlink ($($links[0].FullName)) -- remove it " +
                "by hand; a recursive delete here would delete the link's target."
        }
        Write-Host "Clearing $Dir..." -ForegroundColor Cyan
        # Emptied, not deleted: a shell or a running build sitting in the folder holds the
        # directory itself open, and removing its contents works where removing the folder fails.
        # A running exported build still holds its own exe, which Godot reports much later and far
        # less clearly, as a failure to rename its temporary file after the whole pack is done.
        try {
            Get-ChildItem $Dir -Force | Remove-Item -Recurse -Force -ErrorAction Stop
        } catch {
            throw "Could not clear $Dir -- close the exported build if it is still running. " +
                "($($_.Exception.Message))"
        }
    } else {
        New-Item -ItemType Directory -Force $Dir | Out-Null
    }
}
Clear-StagingDir $ExportDir
if ($Linux) {
    Clear-StagingDir $LinuxExportDir
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

# NOT --headless, unlike the import above: the preset's Shader Baker compiles the pipeline
# variants into the pack, and it needs a live rendering device on the renderer the target will
# use. Headless has a dummy one, so the bake is skipped in silence and the export still reports
# success -- the payload just ships without it and every player pays the compile at first draw.
# The driver and method are pinned rather than left to the editor's own setting for the same
# reason: baking on a different renderer than the target cannot include the core shaders.
# A real editor rewrites project.godot on startup: same values, but its own key order and NONE
# of the comments, so an export would silently strip every decode the file carries. Snapshot and
# restore it byte-for-byte around the run. Copy-Item both ways rather than a text round-trip,
# which is what keeps PowerShell 5.1's ANSI default away from the file (see CLAUDE.md). This is
# the file's only writer: the version above is READ from it, never stamped into it.
function Invoke-PresetExport([string] $Preset, [string] $OutFile) {
    $stagingDir = Split-Path $OutFile -Parent
    Write-Host "Exporting release build to $OutFile..." -ForegroundColor Cyan
    $backup = Join-Path $env:TEMP "csvm-project-godot-$PID.bak"
    Copy-Item $script:ProjectGodot $backup -Force

    $log = Join-Path $stagingDir "export.log"
    try {
        & $script:GodotExe --path $script:ProjectDir --rendering-driver vulkan `
            --rendering-method forward_plus --export-release $Preset $OutFile |
            Tee-Object -FilePath $log
        $exportExit = $LASTEXITCODE
    } finally {
        # In a finally so a failed or interrupted export cannot leave the stripped file behind.
        Copy-Item $backup $script:ProjectGodot -Force
        Remove-Item $backup -Force
    }
    if ($exportExit -ne 0) {
        throw "Godot export of '$Preset' failed (exit $exportExit)."
    }

    # A skipped bake is the failure this script cannot see any other way: it costs no exit code,
    # no warning and no missing file, only a slower first draw on someone else's machine. Assert
    # the stage ran rather than trusting the flag, since the preset key and the renderer have to
    # agree for it to do anything.
    if (-not (Select-String -Path $log -Pattern "baking_shaders" -Quiet)) {
        throw "Export of '$Preset' finished but baked no shaders -- check shader_baker/enabled " +
            "in CSVM\export_presets.cfg and that this export ran with a real rendering device."
    }
    Remove-Item $log -Force
}

# The self-contained publish's runtime version is only knowable after the export has produced
# it, which is why this is the one notice stamp checked after the export rather than up front.
# It moves whenever the SDK does, silently, and it is what sections 4 and 5 of the notice quote.
function Assert-ExportRuntime([string] $DataDir, $Notice) {
    $runtimeConfig = Join-Path $DataDir "CSVM.runtimeconfig.json"
    if (-not (Test-Path $runtimeConfig)) {
        throw "Export produced no $runtimeConfig -- the preset's .NET publish did not run."
    }
    $exportRuntime = ([System.IO.File]::ReadAllText($runtimeConfig) |
        ConvertFrom-Json).runtimeOptions.includedFrameworks[0].version
    if ($exportRuntime -ne $Notice.Runtime) {
        throw "$($Notice.Name) was assembled for .NET runtime $($Notice.Runtime) but the " +
            "export bundles $exportRuntime -- regenerate it with $($Notice.Regenerate)."
    }
}

# Godot exports an extension's library beside the executable only when the project's
# .godot\extension_list.cfg names it, and an export that left it out still exits 0. So the library
# is looked for rather than assumed, and its hash goes into BUILD-INFO.txt. $Sep is the archive's
# own path separator, so the block reads as the recipient's system names the files.
function Get-WebRtcInfo([string] $Dir, [string] $Library, [string] $Sep) {
    $path = Join-Path $Dir $Library
    if (-not (Test-Path $path)) {
        throw "Export produced no $Library beside the executable -- check that " +
            "CSVM\.godot\extension_list.cfg names res://addons/webrtc_native/webrtc_native.gdextension " +
            "(InstallWebRtc.ps1 writes that line)."
    }
    $sha = (Get-FileHash $path -Algorithm SHA256).Hash.ToLower()
    $info = "`n$Library`n" +
        "  version:  webrtc-native $($script:WebRtc.Version), the godotengine release as published`n" +
        "  source:   https://github.com/godotengine/webrtc-native  (tag $($script:WebRtc.Version), its`n" +
        "            libdatachannel, libjuice and other submodules at the commits that tag pins)`n" +
        "  sha256:   $sha`n"
    $licence = " The WebRTC library's licences`nare in LICENSE-webrtc$Sep; libdatachannel and libjuice are under the MPL-2.0," +
        "`nwhose source is the tag above."
    return @{ Info = $info; Licence = $licence }
}

function Copy-ReleaseFiles($Files, [string] $Dir) {
    Write-Host "Staging release files..." -ForegroundColor Cyan
    foreach ($file in $Files) {
        $dest = Join-Path $Dir $file.Dest
        $destDir = Split-Path $dest -Parent
        if (-not (Test-Path $destDir)) {
            New-Item -ItemType Directory -Force $destDir | Out-Null
        }
        if ($file.Lf) {
            # Through Latin-1, which maps each byte to one character and back, so the UTF-8 in
            # the text passes through untouched: every CR that starts a CRLF pair is dropped and
            # nothing else changes. A lone CR is left for sandbox\LinuxRelease.ps1 to refuse.
            $latin1 = [System.Text.Encoding]::GetEncoding(28591)
            $text = $latin1.GetString([System.IO.File]::ReadAllBytes($file.Source))
            [System.IO.File]::WriteAllBytes($dest, $latin1.GetBytes($text.Replace("`r`n", "`n")))
            Write-Host "  $($file.Dest) (LF)"
            continue
        }
        Copy-Item $file.Source $dest -Force
        Write-Host "  $($file.Dest)"
    }
}

# BUILD-INFO.txt is the one payload file that is GENERATED rather than copied from packaging\,
# because the fact it states -- which commit each shipped binary was built from -- is different
# on every run and has no repo home that could hold it. It states the worktree qualifier honestly
# instead of refusing: an export off a dirty tree is the normal development case, and
# PublishRelease.ps1 is where a qualifier becomes a refusal, since only a published binary
# makes a false source-correspondence claim to anybody. UTF8Encoding($false) rather than
# Set-Content, whose 5.1 default is ANSI (CLAUDE.md). The names and line endings are the
# target platform's, so the file reads as native in the archive it ships in. $Extra is the
# platform's further binaries (SDL2.dll on Windows) and the licence line naming theirs.
# PublishRelease.ps1 refuses any text matching 'MODIFIED' case-insensitively, so no other line
# may contain the word "modified".
function Write-BuildInfo([string] $Path, [string] $ExeLine, [string] $Newline,
        [string] $Extra = "", [string] $ExtraLicence = "") {
    $text = @"
CSVM build provenance
=====================

Every binary in this archive is built from public source. These are the commits.

$ExeLine
  version:  $script:Version
  source:   https://github.com/Laeresh/CSVM
  commit:   $script:csvmCommit
  worktree: $(if ($script:csvmDirty) { "MODIFIED -- this build does not match the commit above" } else { "clean" })
$Extra
CSVM's own licence is LICENSE (GPL-3). The notices for the third-party software
inside the executable, including the Godot engine and the .NET runtime, are in
LICENSE-thirdparty.txt.$ExtraLicence
"@
    $text = ($text -replace "`r`n", "`n") -replace "`n", $Newline
    [System.IO.File]::WriteAllText($Path, $text, (New-Object System.Text.UTF8Encoding($false)))
    Write-Host "  BUILD-INFO.txt (generated)"
    if ($script:csvmDirty)   { Write-Host "  ! CSVM worktree is dirty; BUILD-INFO.txt says so" -ForegroundColor Yellow }
}

# The .NET publish leaves scanner shadow copies (name~RFxxxxxxx.TMP) in the data folder, and
# Godot's own CSVM.tmp survives a failed embed. Both are junk a recipient must not receive, and
# a locked one aborts the whole archive midway, so drop them and skip them when archiving.
function Remove-ExportJunk([string] $Dir) {
    Get-ChildItem $Dir -Recurse -File -Include "*.TMP", "*.tmp" |
        Remove-Item -Force -ErrorAction SilentlyContinue
}

Invoke-PresetExport "Windows Desktop" $ExportExe

# Whether the version reached the exe is not something the exit code can say: with the preset's
# application/modify_resources off, the export succeeds and ships an exe whose properties still
# name Godot's own export template. Read the stamp back instead of trusting the flag.
$exeInfo = (Get-Item $ExportExe).VersionInfo
if ($exeInfo.FileVersion -notlike "$Version*" -or $exeInfo.ProductVersion -notlike "$Version*") {
    throw "Exported exe states file version '$($exeInfo.FileVersion)' and product version " +
        "'$($exeInfo.ProductVersion)', neither of them $Version -- check application/modify_resources " +
        "and the application/*_version keys in CSVM\export_presets.cfg."
}

Assert-ExportRuntime (Join-Path $ExportDir "data_CSVM_windows_x86_64") $winNotice
Copy-ReleaseFiles $ReleaseFiles $ExportDir
$sdl2Info = "`nSDL2.dll`n" +
    "  version:  SDL $($Sdl2.Version), the official libsdl-org Windows x64 runtime as released`n" +
    "  source:   https://github.com/libsdl-org/SDL  (tag release-$($Sdl2.Version))`n" +
    "  commit:   $($Sdl2.Commit)`n" +
    "  sha256:   $($Sdl2.DllSha256)`n"
$winWebRtc = Get-WebRtcInfo $ExportDir "libwebrtc_native.windows.template_release.x86_64.dll" "\"
Write-BuildInfo $BuildInfo "CSVM.exe, and data_CSVM_windows_x86_64\ beside it" "`r`n" `
    ($sdl2Info + $winWebRtc.Info) (" SDL2.dll is`nunder the zlib licence in LICENSE-SDL2.txt." + $winWebRtc.Licence)
Remove-ExportJunk $ExportDir

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

if (-not $Linux) {
    return
}

# The Linux half. Same export, checks and payload discipline as above; what differs is the
# preset, the payload list, and that the archive is written inside WSL.
Invoke-PresetExport "Linux/X11" $LinuxExportExe
Assert-ExportRuntime (Join-Path $LinuxExportDir "data_CSVM_linuxbsd_x86_64") $linuxNotice
Copy-ReleaseFiles $LinuxReleaseFiles $LinuxExportDir
$linuxWebRtc = Get-WebRtcInfo $LinuxExportDir "libwebrtc_native.linux.template_release.x86_64.so" "/"
Write-BuildInfo (Join-Path $LinuxExportDir "BUILD-INFO.txt") `
    "CSVM.x86_64, and data_CSVM_linuxbsd_x86_64/ beside it" "`n" `
    $linuxWebRtc.Info $linuxWebRtc.Licence
Remove-ExportJunk $LinuxExportDir

# Files on a Windows drive have no Unix mode of their own (WSL reports every one as 0777), so the
# modes are set on a copy in the distro's own filesystem and the tar is written from there: 0755
# for the executable and every directory, 0644 for everything else, root-owned so an unpack
# by any user does not try to restore this machine's uid. Entries sit at the archive root, as they
# do in the zip.
$packScript = Write-WslScript "pack-linux" @'
set -eu
src="$1"; out="$2"
tmp="$(mktemp -d)"
trap 'rm -rf -- "$tmp"' EXIT
cp -R -- "$src/." "$tmp/"
find "$tmp" -type d -exec chmod 0755 {} +
find "$tmp" -type f -exec chmod 0644 {} +
chmod 0755 "$tmp/CSVM.x86_64"
rm -f -- "$out"
cd "$tmp"
tar --create --gzip --file="$out" --owner=0 --group=0 --numeric-owner --sort=name -- *
tar --list --verbose --gzip --file="$out"
'@

Write-Host "Packaging $TarPath in WSL ($LinuxDistro)..." -ForegroundColor Cyan
try {
    $listing = @(& wsl.exe -d $LinuxDistro --exec sh (ConvertTo-WslPath $packScript) `
        (ConvertTo-WslPath $LinuxExportDir) (ConvertTo-WslPath $TarPath))
    $packExit = $LASTEXITCODE
} finally {
    Remove-Item $packScript -Force -ErrorAction SilentlyContinue
}
if ($packExit -ne 0 -or -not (Test-Path $TarPath)) {
    throw "Packing $TarPath in WSL failed (exit $packExit)."
}

# Read the modes back out of the archive rather than trusting the chmod: a tarball whose
# executable lost its bit unpacks without complaint and fails only when a player runs it.
foreach ($exe in @("CSVM.x86_64")) {
    $line = $listing | Where-Object { $_ -match "\s$([regex]::Escape($exe))$" }
    if (-not $line -or $line -notmatch '^-rwxr-xr-x ') {
        throw "$TarPath does not mark $exe executable (listing: '$line')."
    }
}
$tarFiles = @($listing | Where-Object { $_ -match '^-' }).Count
$TarSha256 = (Get-FileHash $TarPath -Algorithm SHA256).Hash.ToLower()

Write-Host "Exported to $LinuxExportExe" -ForegroundColor Green
Write-Host "Packaged  $TarPath ($tarFiles files)" -ForegroundColor Green
Write-Host "SHA-256   $TarSha256" -ForegroundColor Green
