# Tree keys and stamps. A key is the git hash of chosen paths as a tree holds them now, tracked or
# not, so two trees with the same content under those paths share a key whatever their commits. A
# stamp records that a check passed on a key. Stamps live in the common git directory: every worktree
# of the checkout reads the same ones and none is ever committed. Dot-sourced by RunTests.ps1,
# FormatBeforeTests.ps1 and CheckPushGreen.ps1; it takes no parameters, so dot-sourcing binds nothing.
#
# Pure ASCII on purpose (PROJECT_CONTEXT.md).

# What the complete battery judges: the game, its unit tests and the golden pins.
$GreenKeyPaths = @('CSVM', 'CSVM.Tests', 'analysis/goldens')
# What dotnet format and the StyleCop build of CSVM.csproj read.
$FormatKeyPaths = @('CSVM', '.editorconfig', 'Directory.Build.props')
$TreeStampKeepDays = 60

<#
.SYNOPSIS
The key of $Paths in $Root's working tree, or in $Commit when one is given. Empty when git cannot
say (no repository, an index with unmerged entries); a caller treats that as "no stamp".
The working tree is read through a throwaway copy of the index, so the real index is untouched.
#>
function Get-TreeKey {
    param([string]$Root, [string[]]$Paths, [string]$Commit = '')
    # git's line-ending notes arrive on stderr, which Stop would turn into a throw (SHELL-20).
    $ErrorActionPreference = 'Continue'
    $tree = $Commit
    if (-not $tree) {
        $gitDir = [string](git -C $Root rev-parse --absolute-git-dir 2>$null)
        if (-not $gitDir) { return '' }
        $tmp = Join-Path $gitDir ('csvm-key-index-' + [guid]::NewGuid().ToString('N'))
        $saved = $env:GIT_INDEX_FILE
        try {
            $index = Join-Path $gitDir 'index'
            if (Test-Path -LiteralPath $index) { Copy-Item -LiteralPath $index -Destination $tmp }
            $env:GIT_INDEX_FILE = $tmp
            # One path per call: a pathspec matching nothing aborts the whole add.
            foreach ($p in $Paths) { git -C $Root add -A -- $p 2>$null | Out-Null }
            $tree = [string](git -C $Root write-tree 2>$null)
        } finally {
            $env:GIT_INDEX_FILE = $saved
            Remove-Item -LiteralPath $tmp -Force -ErrorAction SilentlyContinue
        }
        if (-not $tree) { return '' }
    }
    $rows = @(git -C $Root ls-tree $tree -- $Paths 2>$null)
    if ($LASTEXITCODE -ne 0 -or $rows.Count -eq 0) { return '' }
    $sha = [System.Security.Cryptography.SHA1]::Create()
    try {
        $bytes = $sha.ComputeHash([System.Text.Encoding]::UTF8.GetBytes(($rows -join "`n")))
    } finally { $sha.Dispose() }
    return (-join ($bytes | ForEach-Object { $_.ToString('x2') }))
}

function Get-TreeStampFile {
    param([string]$Root, [string]$Kind, [string]$Key)
    if (-not $Key) { return '' }
    $ErrorActionPreference = 'Continue'
    $common = [string](git -C $Root rev-parse --git-common-dir 2>$null)
    if (-not $common) { return '' }
    if (-not [System.IO.Path]::IsPathRooted($common)) { $common = Join-Path $Root $common }
    return (Join-Path (Join-Path $common 'csvm-stamps') "$Kind-$Key.json")
}

# The fields a stamp was written with, or $null when $Key carries no $Kind stamp.
function Get-TreeStamp {
    param([string]$Root, [string]$Kind, [string]$Key)
    $file = Get-TreeStampFile -Root $Root -Kind $Kind -Key $Key
    if (-not $file -or -not (Test-Path -LiteralPath $file)) { return $null }
    try { return ([System.IO.File]::ReadAllText($file) | ConvertFrom-Json) } catch { return $null }
}

# Writes a stamp and sweeps the ones past $TreeStampKeepDays. A stamp that cannot be written is
# only a check that runs again, so nothing here throws.
function Set-TreeStamp {
    param([string]$Root, [string]$Kind, [string]$Key, $Fields)
    $file = Get-TreeStampFile -Root $Root -Kind $Kind -Key $Key
    if (-not $file) { return }
    try {
        $dir = Split-Path -Parent $file
        if (-not (Test-Path -LiteralPath $dir)) { $null = New-Item -ItemType Directory -Path $dir -Force }
        [System.IO.File]::WriteAllText($file, (ConvertTo-Json -InputObject $Fields -Compress), (New-Object System.Text.UTF8Encoding($false)))
        $cutoff = (Get-Date).AddDays(-$TreeStampKeepDays)
        Get-ChildItem -LiteralPath $dir -Filter '*.json' | Where-Object { $_.LastWriteTime -lt $cutoff } |
            Remove-Item -Force -ErrorAction SilentlyContinue
    } catch { }
}
