# A file that exists exactly as long as the handle that holds it: the claim behind the memory
# ledger's reservations and waiters and gaming mode's lock and waiters. Opened delete-on-close and
# shared for reading only, so a dead holder frees it with its process (a named semaphore would leak
# its count on a crash) and nobody else can remove it while it is held. Dot-sourced by
# MemoryLedger.ps1 and GamingModeCore.ps1; it takes no parameters, so dot-sourcing binds nothing.
#
# Pure ASCII on purpose (PROJECT_CONTEXT.md).

<#
.SYNOPSIS
Opens a held file and writes its fields as JSON. Returns { Path; Stream; Fields }; disposing the
stream deletes the file. Throws when another handle holds it (OpenOrCreate) or it exists (CreateNew).
#>
function Open-HeldFile {
    param([string]$Path, [System.IO.FileMode]$Mode, $Fields)
    $stream = New-Object System.IO.FileStream($Path, $Mode, [System.IO.FileAccess]::ReadWrite,
        [System.IO.FileShare]::Read, 4096, [System.IO.FileOptions]::DeleteOnClose)
    $held = [pscustomobject]@{ Path = $Path; Stream = $stream; Fields = $Fields }
    Write-HeldFields $held
    return $held
}

# Rewrites a held file's fields from its .Fields.
function Write-HeldFields {
    param($Held)
    $bytes = [System.Text.Encoding]::UTF8.GetBytes((ConvertTo-Json -InputObject $Held.Fields -Compress))
    $Held.Stream.SetLength(0)
    $Held.Stream.Write($bytes, 0, $bytes.Length)
    $Held.Stream.Flush()
}

# A held file's fields, read without disturbing its holder, or $null when it is gone or unreadable.
function Read-HeldFile {
    param([string]$Path)
    try {
        $s = New-Object System.IO.FileStream($Path, [System.IO.FileMode]::Open, [System.IO.FileAccess]::Read,
            ([System.IO.FileShare]::ReadWrite -bor [System.IO.FileShare]::Delete))
        try { return ((New-Object System.IO.StreamReader($s)).ReadToEnd() | ConvertFrom-Json) } finally { $s.Dispose() }
    } catch { return $null }
}
