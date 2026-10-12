# How the pre-tool gates read a shell command line: where a statement begins, what git's global
# options look like, and which tree the command acts on. Dot-sourced by FormatBeforeTests.ps1 and
# CheckCommitContent.ps1, never run on its own.
#
# ONE COPY ON PURPOSE. The two gates each carried their own parser, and the format gate's had
# drifted: it split only on ";", "&&", "||" and "|", so a newline, a braced block and a
# "powershell -File" never fired it, and it read no --work-tree. Each gate still decides for itself
# WHAT it guards (its own trigger pattern); how a command is read is decided here, once.
#
# Pure ASCII on purpose (PROJECT_CONTEXT.md).

$GateQuote = [char]39

# One quoted-or-bare token. Parenthesised because "," binds tighter than "+" in PowerShell.
$GateToken = '(?:"[^"]*"|' + $GateQuote + '[^' + $GateQuote + ']*' + $GateQuote + '|\S+)'

# Where a statement can begin: the start of the command, a newline, ";", a pipe or "&&"/"||", or
# the opening of a braced block or subexpression, then an optional call operator. A trigger anchored
# here fires on an invocation and not on a mention: the same words inside an argument or a quoted
# string follow none of these. A separator inside a quoted string still counts, which over-fires on
# a quoted mention rather than missing an invocation; a gate errs that way round.
$GateStatementStart = '(?:^|[\r\n;|&{(])\s*(?:&\s*)?'

# git's own global options, which are what stands between "git" and its subcommand in the form
# CLAUDE.md prescribes for naming a tree, "git -C <tree> commit". A trigger testing for the two
# words adjacent misses every one of those, and one accepting any tokens in the gap turns
# "git log --grep='a git commit'" into a commit.
$GateGitGlobal = '(?:-[Cc]\s+' + $GateToken +
    '|--(?:git-dir|work-tree|namespace|exec-path|super-prefix|config-env)(?:=|\s+)' + $GateToken +
    '|--[a-z][a-z-]*|-[a-zA-Z])'

# A git commit at the head of a statement (combine with $GateStatementStart). "commit-tree" and
# "git log commit" are not commits.
$GateGitCommit = 'git(?:\s+' + $GateGitGlobal + ')*\s+commit(?:\s|$)'

# A path as written on the command line, minus the quoting.
function Get-UnquotedPath {
    param([string]$Raw)
    $t = $Raw.Trim()
    if ($t.Length -ge 2) {
        $c = $t[0]
        if (($c -eq '"' -or $c -eq [char]39) -and $t[$t.Length - 1] -eq $c) {
            return $t.Substring(1, $t.Length - 2)
        }
    }
    return $t
}

# git speaks forward slashes and Windows PowerShell speaks backslashes; a trailing separator makes
# two spellings of the same tree compare unequal. The separator is the OS's own, since a backslash
# path on macOS or Linux names nothing.
function ConvertTo-NormalPath {
    param([string]$Path)
    if (-not $Path) { return '' }
    $sep = [IO.Path]::DirectorySeparatorChar
    return ($Path -replace '[\\/]', $sep).TrimEnd($sep)
}

# $Path taken from $Base when it is relative. Windows PowerShell's path API throws on a character
# such as a stray quote, and a malformed token is a path that names no tree, not a crashed gate.
function Join-CommandPath {
    param([string]$Base, [string]$Path)
    try {
        if ([IO.Path]::IsPathRooted($Path)) { return $Path }
        return (Join-Path $Base $Path)
    } catch { return $Path }
}

# The work tree holding $Path, or empty. A --git-dir names the .git folder, which has no work tree
# of its own, so its parent is asked instead.
function Resolve-Toplevel {
    param([string]$Path)
    if (-not $Path) { return '' }
    try { if (-not (Test-Path -LiteralPath $Path)) { return '' } } catch { return '' }
    $top = git -C $Path rev-parse --show-toplevel 2>$null
    if (-not $top -and (Split-Path -Leaf $Path) -eq '.git') {
        $top = git -C (Split-Path -Parent $Path) rev-parse --show-toplevel 2>$null
    }
    if (-not $top) { return '' }
    return (ConvertTo-NormalPath -Path ([string]$top))
}

# The tree the command names through git: -C, else --work-tree, else --git-dir, anywhere in the
# command, so "git -C <tree> rev-parse; .\RunTests.ps1" names a tree for the runner too. Empty when
# it names none. The -C is case-sensitive: "git -c k=v" is a config key, not a path.
function Get-NamedTree {
    param([string]$CommandLine)
    $patterns = @(
        ('(?:^|\s)-C\s+(' + $GateToken + ')'),
        ('--work-tree(?:=|\s+)(' + $GateToken + ')'),
        ('--git-dir(?:=|\s+)(' + $GateToken + ')')
    )
    foreach ($p in $patterns) {
        $m = [regex]::Match($CommandLine, $p)
        if ($m.Success) { return Get-UnquotedPath -Raw $m.Groups[1].Value }
    }
    return ''
}

# The directory the command moves to in $Before (the text ahead of the invocation), or empty. A
# move after the invocation cannot affect where it runs, and the last move wins because that is
# the directory the invocation runs in. The hook runs before the command does, so this is read off
# the command string rather than observed.
function Get-ChangedDirectory {
    param([string]$Before)
    $pathGroup = '("[^"]*"|' + $GateQuote + '[^' + $GateQuote + ']*' + $GateQuote + '|[^\s;|&}]+)'
    # The optional -Path/-LiteralPath is how Set-Location and Push-Location are spelled out.
    $pattern = $GateStatementStart + '(?i:Set-Location|Push-Location|chdir|pushd|sl|cd)\s+(?i:-(?:Literal)?Path\s+)?' + $pathGroup
    $found = ''
    foreach ($m in [regex]::Matches($Before, $pattern)) {
        $candidate = Get-UnquotedPath -Raw $m.Groups[1].Value
        if ($candidate -and -not $candidate.StartsWith('-')) { $found = $candidate }
    }
    return $found
}

# A tree written as a variable ("git -C $wt commit", "& $wt\RunTests.ps1") names no path the hook
# can see, and falling back to the hook's own tree acts on a tree the command never named. So a
# variable is resolved only from a quoted literal assigned to it in $Before, the command ahead of
# the invocation, and anything else returns $null for the caller to refuse. A token that is not a
# variable or an expression comes back unchanged.
function Resolve-CommandVariable {
    param([string]$Before, [string]$Token)
    if ($Token.StartsWith('(')) { return $null }
    if (-not $Token.StartsWith('$')) { return $Token }
    $m = [regex]::Match($Token, '^\$(?:\{([^}]+)\}|((?:env:)?\w+))(.*)$')
    if (-not $m.Success) { return $null }
    $name = if ($m.Groups[1].Success) { $m.Groups[1].Value } else { $m.Groups[2].Value }
    $rest = $m.Groups[3].Value
    if ($rest -and $rest -notmatch '^[\\/][^$(]*$') { return $null }
    $n = [regex]::Escape($name)
    $assign = '(?:^|[;\r\n{(|&])\s*\$(?:\{' + $n + '\}|' + $n + ')\s*=\s*("[^"$`]*"|' +
        $GateQuote + '[^' + $GateQuote + ']*' + $GateQuote + ')\s*(?=$|[;\r\n|&}])'
    $found = $null
    foreach ($a in [regex]::Matches($Before, $assign, 'IgnoreCase')) {
        $found = Get-UnquotedPath -Raw $a.Groups[1].Value
    }
    if ($null -eq $found) { return $null }
    return $found + $rest
}

# The tree an invocation acts on, as Root, or as Unresolved (the token) when the command names it
# through a variable or expression that cannot be read; the caller blocks that case. In precedence
# order: $First (what the invocation itself names, such as an absolute runner path), the tree git
# is pointed at, the directory the command moves to, then $From. A candidate that names no tree is
# skipped rather than trusted, so $From is the floor. Relative candidates are taken from the
# directory moved to when there is one, since that is where the invocation runs.
function Resolve-CommandTree {
    param([string]$CommandLine, [string]$Before, [string]$From, [string[]]$First = @())
    $moved = Get-ChangedDirectory -Before $Before
    $base = $From
    $movedPath = $null
    if ($moved) {
        $movedPath = Resolve-CommandVariable -Before $Before -Token $moved
        if ($null -ne $movedPath) {
            $movedPath = Join-CommandPath -Base $From -Path $movedPath
            $base = $movedPath
        }
    }
    $candidates = @($First | Where-Object { $_ })
    $named = Get-NamedTree -CommandLine $CommandLine
    if ($named) { $candidates += $named }
    foreach ($candidate in $candidates) {
        $path = Resolve-CommandVariable -Before $Before -Token $candidate
        if ($null -eq $path) { return [pscustomobject]@{ Root = ''; Unresolved = $candidate } }
        $top = Resolve-Toplevel -Path (Join-CommandPath -Base $base -Path $path)
        if ($top) { return [pscustomobject]@{ Root = $top; Unresolved = '' } }
    }
    if ($moved) {
        if ($null -eq $movedPath) { return [pscustomobject]@{ Root = ''; Unresolved = $moved } }
        $top = Resolve-Toplevel -Path $movedPath
        if ($top) { return [pscustomobject]@{ Root = $top; Unresolved = '' } }
    }
    return [pscustomobject]@{ Root = (Resolve-Toplevel -Path $From); Unresolved = '' }
}
