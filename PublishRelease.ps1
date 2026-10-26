<#
.SYNOPSIS
    Publishes a CSVM release: the annotated tag, the versioned Windows zip and Linux tarball,
    their SHA-256s and the GitHub release that carries them, all from one run.

.DESCRIPTION
    The publish entry point (see docs/tooling.md "Publishing a release"). It reads the
    version from its one home, CSVM/project.godot's application/config/version, runs
    ExportRelease.ps1 -Linux, checks both archives it produced, runs sandbox\LinuxRelease.ps1
    on the tarball, computes both SHA-256s, tags the commit that was built, pushes the tag,
    and creates the GitHub release with both archives attached.
    No pre-release flag: the first public build has to be the one a visitor lands on.

    Everything the release states comes from that single run, so the tag, the exe's stamped
    version, the zip's name, the published checksum and the notes cannot disagree with each
    other.

    What it refuses, and why each refusal exists:

      - A dirty CSVM worktree. The release says the zip was built from a commit; an
        uncommitted edit makes that false in a way nobody downstream can detect. (The commit
        needs no pushed-check: pushing the tag publishes it.)
      - A tag that already exists, locally or on the remote. A moved tag makes the source
        correspondence false for a binary somebody already downloaded, and nothing on their
        machine says so. There is no -Force here on purpose: the way to fix a bad release is
        another version, not another meaning for this one.
      - An existing release for the tag, a BUILD-INFO.txt whose provenance disagrees with
        this run, or a notes file that states a checksum of its own.

    -TagSuffix exists so this script can be exercised end to end without spending the real
    tag: the run is a real tag, upload and release under a name of its own, deleted
    afterwards, and the release version's tag is still minted exactly once.

    Every release carries both platforms: one tag, two archives. The tarball gets the zip's
    checks, and sandbox\LinuxRelease.ps1 must pass on it before anything is tagged, so a
    machine without the WSL Debian toolchain (docs/tooling.md, "-Linux") cannot publish.

    Each archive is also uploaded a second time under a name without the version
    (CSVM-win64.zip, CSVM-linux-x64.tar.gz), byte for byte the same file, so
    releases/latest/download/<name> always resolves to the newest build. Installers that
    follow the latest release (the Lutris script) link those names.

.PARAMETER NotesFile
    Markdown prepended to the release notes. The verification and provenance sections are
    generated below it from the run, so the file itself never states a checksum or a commit.

.PARAMETER TagSuffix
    Appended to the tag name for a rehearsal run (-TagSuffix rehearsal publishes
    v0.1.0-rehearsal). The zip keeps its version name; only the tag and title change.

.PARAMETER DryRun
    Run every check and the export, write the notes that would be published, and stop
    before the tag. Creates nothing that has to be retracted.

.PARAMETER Yes
    Skip the confirmation prompt. For an unattended run only; the prompt is the last point
    at which the tag and the upload can still be called off.

.EXAMPLE
    .\PublishRelease.ps1 -NotesFile docs\release-notes-v0.2.0.md

.EXAMPLE
    .\PublishRelease.ps1 -TagSuffix rehearsal -Yes
    A full publish under v<version>-rehearsal, to be deleted afterwards with the command
    the run prints.
#>
[CmdletBinding()]
param(
    [string]$NotesFile,
    [ValidatePattern('^[A-Za-z0-9][A-Za-z0-9.-]*$')]
    [string]$TagSuffix,
    [switch]$DryRun,
    [switch]$Yes
)

$ErrorActionPreference = 'Stop'

$RepoRoot      = $PSScriptRoot
$ExportScript  = Join-Path $RepoRoot 'ExportRelease.ps1'
$ProjectGodot  = Join-Path $RepoRoot 'CSVM\project.godot'
$ExportDir     = Join-Path $RepoRoot '.scratch\export'
$BuildInfoPath = Join-Path $ExportDir 'BUILD-INFO.txt'
$LinuxCheck    = Join-Path $RepoRoot 'sandbox\LinuxRelease.ps1'
# Windows' own bsdtar reads .tar.gz. Named by path so a Git or MSYS tar earlier on PATH, which
# takes a Windows path for a remote host, is never the one that runs.
$WindowsTar    = Join-Path $env:SystemRoot 'System32\tar.exe'

function Invoke-Probe {
    # For the calls whose failure is a normal answer: a tag that does not exist yet, a release
    # that has not been created. PowerShell 5.1 turns a native command's stderr into a
    # terminating NativeCommandError as soon as it is redirected under
    # $ErrorActionPreference = 'Stop', so "gh release view" reporting "release not found" would
    # end the run instead of answering the question. Lift the preference around the call and
    # read the exit code, which is what the answer is actually in.
    param(
        [Parameter(Mandatory = $true)][string] $Exe,
        [Parameter(ValueFromRemainingArguments = $true)][string[]] $Arguments
    )
    $previous = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try {
        $output = & $Exe @Arguments 2>&1
        return [pscustomobject]@{
            ExitCode = $LASTEXITCODE
            Lines    = @($output | ForEach-Object { "$_".Trim() } | Where-Object { $_ })
        }
    } finally {
        $ErrorActionPreference = $previous
    }
}

function Get-GitHubSlug([string] $RemoteUrl) {
    # git@github.com:Owner/Name.git and https://github.com/Owner/Name.git both reduce to
    # Owner/Name, which is what gh --repo takes and what a commit URL is built from.
    $match = [regex]::Match($RemoteUrl, '[:/]([^/:]+)/([^/]+?)(\.git)?\s*$')
    if (-not $match.Success) { return $null }
    return "$($match.Groups[1].Value)/$($match.Groups[2].Value)"
}

# gh is installed per-user by winget and its PATH entry only reaches shells started after the
# install, which is a confusing failure in a long-lived agent shell -- so look where winget
# puts it before giving up, and name the fix when it is genuinely absent.
$gh = (Get-Command gh -ErrorAction SilentlyContinue).Source
if (-not $gh) {
    $candidates = @(
        (Join-Path $env:ProgramFiles 'GitHub CLI\gh.exe'),
        (Join-Path $env:LOCALAPPDATA 'Microsoft\WinGet\Links\gh.exe')
    )
    $gh = $candidates | Where-Object { Test-Path $_ } | Select-Object -First 1
}
if (-not $gh) {
    throw "gh not found. Install it (winget install GitHub.cli) and open a FRESH shell -- a " +
        "shell started before the install does not have it on PATH."
}

if ((Invoke-Probe $gh auth status).ExitCode -ne 0) {
    throw "gh is not authenticated -- run 'gh auth login' (scope: repo) in a fresh shell first."
}

$origin = Invoke-Probe git -C $RepoRoot remote get-url origin
if ($origin.ExitCode -ne 0 -or $origin.Lines.Count -eq 0) { throw "No 'origin' remote in $RepoRoot." }
$originUrl = $origin.Lines[0]
$RepoSlug = Get-GitHubSlug $originUrl
if (-not $RepoSlug) { throw "Could not read an owner/name out of origin ($originUrl)." }
$RepoUrl = "https://github.com/$RepoSlug"

# --repo is passed to every gh call rather than letting gh infer the repository from the
# working directory: the inference walks remotes and can land on a fork, and this is the one
# script whose target must be the repository the tag was just pushed to.
$repoView = Invoke-Probe $gh repo view $RepoSlug --json 'nameWithOwner,visibility'
if ($repoView.ExitCode -ne 0) {
    throw "gh cannot see $RepoSlug -- $($repoView.Lines -join ' ')"
}
$Visibility = ($repoView.Lines -join '' | ConvertFrom-Json).visibility

Write-Host "Publishing to $RepoSlug ($($Visibility.ToLower()))" -ForegroundColor Cyan

# The version has one home (docs/tooling.md): project.godot's application/config/version, read
# the same way ExportRelease.ps1 reads it, so the tag and the zip name cannot come apart.
# -Encoding utf8 because 5.1 decodes a BOM-less file as ANSI (CLAUDE.md).
$versionMatch = @(Get-Content $ProjectGodot -Encoding utf8 | Select-String -Pattern '^config/version="([^"]+)"')
if ($versionMatch.Count -ne 1) {
    throw "Expected exactly one config/version in $ProjectGodot, found $($versionMatch.Count) -- " +
        "the release's version number lives there and nowhere else (docs/tooling.md)."
}
$Version = $versionMatch[0].Matches[0].Groups[1].Value
$Tag     = if ($TagSuffix) { "v$Version-$TagSuffix" } else { "v$Version" }
$Title   = "CSVM $Tag"
$ZipName = "CSVM-v$Version-win64.zip"
$ZipPath = Join-Path $RepoRoot ".scratch\$ZipName"
$TarName = "CSVM-v$Version-linux-x64.tar.gz"
$TarPath = Join-Path $RepoRoot ".scratch\$TarName"
# The version-free copies get a folder of their own because gh names an asset after its file.
$LatestDir     = Join-Path $RepoRoot '.scratch\latest'
$LatestZipName = 'CSVM-win64.zip'
$LatestTarName = 'CSVM-linux-x64.tar.gz'
$LatestZipPath = Join-Path $LatestDir $LatestZipName
$LatestTarPath = Join-Path $LatestDir $LatestTarName
foreach ($needed in @($LinuxCheck, $WindowsTar)) {
    if (-not (Test-Path $needed)) { throw "The Linux tarball's checks need $needed, which is missing." }
}

# A tag is a promise about a commit that other people's downloads depend on, so an existing
# one ends the run instead of being moved. Both sides are checked: a local tag that was never
# pushed still blocks the push, and a remote tag can exist without a local one.
if ((Invoke-Probe git -C $RepoRoot rev-parse -q --verify "refs/tags/$Tag").ExitCode -eq 0) {
    throw "Tag $Tag already exists locally. A tag is never re-pointed: it is the source " +
        "correspondence for binaries that may already be downloaded. Publish a new version, " +
        "or delete this tag by hand if it was never pushed."
}
$remoteTag = Invoke-Probe git -C $RepoRoot ls-remote --tags origin "refs/tags/$Tag"
if ($remoteTag.ExitCode -ne 0) {
    throw "Could not reach origin to check for tag $Tag -- $($remoteTag.Lines -join ' ')"
}
if ($remoteTag.Lines.Count -gt 0) {
    throw "Tag $Tag already exists on origin. It is never re-pointed; publish a new version."
}
if ((Invoke-Probe $gh release view $Tag --repo $RepoSlug).ExitCode -eq 0) {
    throw "A release for $Tag already exists on $RepoSlug. Delete it first if it was a test, " +
        "or publish a new version."
}

$csvmDirty = @(& git -C $RepoRoot status --porcelain)
if ($csvmDirty.Count -gt 0) {
    throw "The CSVM worktree is dirty ($($csvmDirty.Count) path(s)). A published binary states " +
        "the commit it was built from; commit or stash first."
}
$CsvmCommit = (& git -C $RepoRoot rev-parse HEAD)
if ($CsvmCommit -notmatch '^[0-9a-f]{40}$') {
    throw "Could not resolve HEAD in $RepoRoot."
}

# Not a refusal: the tag push publishes the commit either way, so the release's links all
# resolve. It is worth saying out loud, because a reader who opens the branch expects to find
# the release's commit in its history.
$onOrigin = Invoke-Probe git -C $RepoRoot branch -r --contains $CsvmCommit
if ($onOrigin.Lines.Count -eq 0) {
    Write-Host "  ! HEAD is on no remote branch yet. The tag push publishes the commit, but " -ForegroundColor Yellow -NoNewline
    Write-Host "push main too so the branch contains it." -ForegroundColor Yellow
}

$notesBody = ''
if ($NotesFile) {
    if (-not (Test-Path $NotesFile)) { throw "Notes file not found: $NotesFile" }
    $notesBody = [System.IO.File]::ReadAllText((Resolve-Path $NotesFile).Path)
    # The checksum is generated below from the file that is actually uploaded. A second one
    # typed into the prose is the copy that goes wrong, and a wrong checksum on a release page
    # reads as a tampered download.
    if ([regex]::IsMatch($notesBody, '(?i)\b[0-9a-f]{64}\b')) {
        throw "$NotesFile states a SHA-256 of its own. This script appends the checksums of the " +
            "archives it uploads; remove the hand-written one."
    }
    $notesBody = $notesBody.TrimEnd() + "`n`n"
}

Write-Host "  version $Version" -ForegroundColor Cyan
Write-Host "  tag     $Tag on $($CsvmCommit.Substring(0,10))" -ForegroundColor Cyan
Write-Host "  asset   $ZipName" -ForegroundColor Cyan
Write-Host "  asset   $TarName" -ForegroundColor Cyan
Write-Host "  asset   $LatestZipName, $LatestTarName (the same files, version-free names)" -ForegroundColor Cyan
Write-Host ''

$exportStarted = Get-Date
# It throws on every failure of its own; the archive checks below are the backstop.
& $ExportScript -Linux

$archives = @($ZipPath, $TarPath)
foreach ($archive in $archives) {
    if (-not (Test-Path $archive)) {
        throw "ExportRelease.ps1 produced no $archive -- the archive's name comes from the same " +
            "config/version this script read, so a mismatch means one of them changed mid-run."
    }
    if ((Get-Item $archive).LastWriteTime -lt $exportStarted) {
        throw "$archive predates this run -- the export did not rewrite it."
    }
}

# The pre-release Linux check (docs/tooling.md, "The Linux release check in WSL"), whole: the
# payload against the manifest, the tarball's own --extract, and the engine suites headless.
# Run before the tree re-check below, so that re-check covers its minutes too.
Write-Host ''
Write-Host "Checking $TarName in WSL (sandbox\LinuxRelease.ps1)..." -ForegroundColor Cyan
$global:LASTEXITCODE = 0
& $LinuxCheck -Tarball $TarPath
if ($LASTEXITCODE -ne 0) {
    throw "The Linux release check failed on $TarPath (exit $LASTEXITCODE). Nothing was " +
        "tagged or uploaded; its logs are under .scratch\linux-check\."
}

# Re-read the tree after a build that takes minutes: an edit or a commit landing while it ran
# would otherwise tag a commit that is not the one the zip was built from.
$dirtyNow = @(& git -C $RepoRoot status --porcelain)
if ($dirtyNow.Count -gt 0) {
    # The paths are named because on a machine where more than one session works this tree, the
    # edit that lands during a five-minute export is usually somebody else's and in a file the
    # build never reads. It still ends the run: the release says the zip was built from a
    # commit, and this tree is no longer that commit.
    throw "The worktree became dirty during the export. Nothing was tagged or uploaded. " +
        "Changed: $($dirtyNow -join '; ')"
}
if ((& git -C $RepoRoot rev-parse HEAD) -ne $CsvmCommit) {
    throw "HEAD moved during the export. Nothing was tagged or uploaded; re-run."
}

# BUILD-INFO.txt is what the zip itself tells its reader about where it came from, and the
# release notes restate its commit. Read it back rather than restating the same variables
# twice: if the export recorded a qualifier, the notes are about to claim something the zip
# already denies.
$buildInfos = [ordered]@{ 'zip' = [System.IO.File]::ReadAllText($BuildInfoPath) }
# Read out of the tarball itself rather than the staging folder, so the check is of the file a
# Linux downloader unpacks.
$tarInfo = Invoke-Probe $WindowsTar '-xOf' $TarPath 'BUILD-INFO.txt'
if ($tarInfo.ExitCode -ne 0 -or $tarInfo.Lines.Count -eq 0) {
    throw "Could not read BUILD-INFO.txt out of $TarPath -- $($tarInfo.Lines -join ' ')"
}
$buildInfos['tarball'] = $tarInfo.Lines -join "`n"
foreach ($archive in $buildInfos.Keys) {
    $buildInfo = $buildInfos[$archive]
    if ($buildInfo -notmatch $CsvmCommit) {
        throw "BUILD-INFO.txt in the $archive does not name $CsvmCommit -- the release notes and " +
            "the $archive would disagree about what was built."
    }
    if ($buildInfo -match 'MODIFIED') {
        throw "BUILD-INFO.txt in the $archive records a dirty source. Nothing was tagged."
    }
}

$Sha256 = (Get-FileHash $ZipPath -Algorithm SHA256).Hash.ToLower()
# Invariant culture, because this string is published: the release page would otherwise carry
# whatever decimal separator the publishing workstation happens to be set to.
$ZipSize = [string]::Format([cultureinfo]::InvariantCulture, '{0:N1} MB',
    ((Get-Item $ZipPath).Length / 1MB))
$TarSha256 = (Get-FileHash $TarPath -Algorithm SHA256).Hash.ToLower()
$TarSize = [string]::Format([cultureinfo]::InvariantCulture, '{0:N1} MB',
    ((Get-Item $TarPath).Length / 1MB))

# Copied after the checksums so the copies are of the files those hashes describe. Emptied first:
# a leftover from an earlier run would otherwise be uploaded under this release's name.
if (Test-Path $LatestDir) { Remove-Item $LatestDir -Recurse -Force }
New-Item -ItemType Directory -Path $LatestDir | Out-Null
Copy-Item $ZipPath $LatestZipPath
Copy-Item $TarPath $LatestTarPath

# Single-quoted here-string with -f placeholders: the notes are full of backticks, which a
# double-quoted here-string would read as escapes. The Linux README is linked at the tagged
# commit rather than restated: its Steam Deck section is the one home of those steps.
$notesTemplate = @'
### Verifying these downloads

There are two downloads: `{0}` ({1}) for Windows, and `{2}` ({3})
for Linux, a community-tested build with the Steam Deck as its reference machine (its `README.md`
has an [On Steam Deck]({4}/blob/{5}/packaging/README-linux.md#on-steam-deck) section). Nothing in
either is code-signed, so these hashes are what say an archive is the one this project published
rather than something rebuilt or altered on the way:

```
Get-FileHash {0} -Algorithm SHA256
sha256sum {2}
```

- `{0}` SHA-256: `{6}`
- `{2}` SHA-256: `{7}`

`{10}` and `{11}` are the same two files under names without the version, for links that follow
the latest release (`{4}/releases/latest/download/{11}`). Their hashes are the ones above.

### Built from

Every binary in both archives is built from public source, and each archive repeats this commit
in its own `BUILD-INFO.txt`.

- `CSVM.exe` and `CSVM.x86_64` (version {8}): [`{9}`]({4}/commit/{5})
'@
$notes = $notesBody + ($notesTemplate -f $ZipName, $ZipSize, $TarName, $TarSize,
    $RepoUrl, $CsvmCommit, $Sha256, $TarSha256, $Version, $CsvmCommit.Substring(0, 10),
    $LatestZipName, $LatestTarName)

$notesPath = Join-Path $RepoRoot ".scratch\release-notes-$Tag.md"
[System.IO.File]::WriteAllText($notesPath, $notes, (New-Object System.Text.UTF8Encoding($false)))

Write-Host ''
Write-Host "About to publish:" -ForegroundColor Cyan
Write-Host "  repository  $RepoSlug ($($Visibility.ToLower()))"
Write-Host "  tag         $Tag -> $CsvmCommit  (annotated, pushed, never moved afterwards)"
Write-Host "  release     $Title, pre-release flag OFF"
Write-Host "  asset       $ZipPath ($ZipSize)"
Write-Host "  sha-256     $Sha256"
Write-Host "  asset       $TarPath ($TarSize)"
Write-Host "  sha-256     $TarSha256"
Write-Host "  asset       $LatestZipPath (copy)"
Write-Host "  asset       $LatestTarPath (copy)"
Write-Host "  linux check sandbox\LinuxRelease.ps1 passed on this tarball"
Write-Host "  notes       $notesPath$(if (-not $NotesFile) { ' (generated sections only)' })"

if ($DryRun) {
    Write-Host ''
    Write-Host "-DryRun: stopped before the tag. Nothing was created." -ForegroundColor Yellow
    return
}
if (-not $Yes) {
    Write-Host ''
    $answer = Read-Host "Type the tag ($Tag) to publish, anything else to stop"
    if ($answer -ne $Tag) {
        Write-Host 'Stopped. Nothing was created.' -ForegroundColor Yellow
        return
    }
}

# Tag, push, then upload, in that order: the release page must never be able to point at a tag
# that does not exist yet, and --verify-tag below makes gh refuse to invent one of its own.
# The message goes through a file because a multi-line -m is where shell quoting corrupts it
# (CLAUDE.md), and it carries the checksum so the tag object records the artifact it belongs
# to independently of the release page.
$tagMessagePath = Join-Path $RepoRoot ".scratch\tag-message-$Tag.txt"
$tagMessage = "CSVM $Tag`n`n$ZipName`nSHA-256: $Sha256`n`n$TarName`nSHA-256: $TarSha256`n"
[System.IO.File]::WriteAllText($tagMessagePath, $tagMessage, (New-Object System.Text.UTF8Encoding($false)))

Write-Host ''
Write-Host "Tagging $Tag..." -ForegroundColor Cyan
& git -C $RepoRoot tag -a $Tag $CsvmCommit -F $tagMessagePath
if ($LASTEXITCODE -ne 0) { throw "git tag failed (exit $LASTEXITCODE)." }

& git -C $RepoRoot push origin "refs/tags/$Tag"
if ($LASTEXITCODE -ne 0) {
    # A local tag left behind after a failed push blocks the retry that would otherwise just
    # work, and it is the one piece of this run that nobody else has seen yet.
    & git -C $RepoRoot tag -d $Tag | Out-Null
    throw "Pushing tag $Tag failed (exit $LASTEXITCODE). The local tag was removed; re-run."
}

$assets = @($ZipPath, $TarPath, $LatestZipPath, $LatestTarPath)
Write-Host "Creating release $Tag with $((($assets | ForEach-Object { Split-Path $_ -Leaf }) -join ' and '))..." -ForegroundColor Cyan
& $gh release create $Tag @assets --repo $RepoSlug --title $Title --notes-file $notesPath `
    --verify-tag --latest
if ($LASTEXITCODE -ne 0) {
    throw "gh release create failed (exit $LASTEXITCODE). The tag $Tag is pushed and stays where " +
        "it is: re-run the gh release create above by hand once the cause is fixed, or delete " +
        "the tag with 'git push origin :refs/tags/$Tag' if nothing was published from it."
}

$releaseUrl = "$RepoUrl/releases/tag/$Tag"
Write-Host ''
Write-Host "Published $releaseUrl" -ForegroundColor Green
Write-Host "SHA-256   $Sha256 ($ZipName)" -ForegroundColor Green
Write-Host "SHA-256   $TarSha256 ($TarName)" -ForegroundColor Green
if ($Visibility -ne 'PUBLIC') {
    Write-Host "The repository is $($Visibility.ToLower()), so this release is not visible to anyone else yet." -ForegroundColor Yellow
}
if ($TagSuffix) {
    Write-Host ''
    Write-Host "Rehearsal run. Delete it with:" -ForegroundColor Yellow
    Write-Host "  gh release delete $Tag --repo $RepoSlug --cleanup-tag --yes"
}
