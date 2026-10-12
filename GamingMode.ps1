# Gaming mode's command line: while the user plays, test runs from every worktree queue behind one
# machine-wide lock and run throttled on a few CPU threads. The code is GamingModeCore.ps1, which
# the runners dot-source; see docs/tooling.md.
#
#   .\GamingMode.ps1 on [-Hours N | -Forever]   switch it on (4 hours by default)
#   .\GamingMode.ps1 off                        switch it off
#   .\GamingMode.ps1 status                     time left, tunables, lock holder, waiters
#   .\GamingMode.ps1 -SelfTest                  expiry, tunable parsing and the lock
#
# Pure ASCII on purpose (PROJECT_CONTEXT.md).

param([string]$Action = "", [double]$Hours = 4, [switch]$Forever, [switch]$SelfTest)

. (Join-Path $PSScriptRoot "GamingModeCore.ps1")
if ($Action -or $SelfTest) {
    if (-not $GamingOnWindows) { Write-Host "gaming mode is a no-op off Windows; nothing to do"; exit 0 }
}

if ($Action -eq "on") {
    $doc = [ordered]@{ until = $(if ($Forever) { "forever" } else { (Get-Date).AddHours($Hours).ToString("s") }) }
    foreach ($k in $GamingDefaults.Keys) { $doc[$k] = $GamingDefaults[$k] }
    # Switching on again keeps the tunables the user edited into the marker.
    if (Test-Path -LiteralPath $GamingMarker) {
        try {
            $old = [System.IO.File]::ReadAllText($GamingMarker) | ConvertFrom-Json
            foreach ($k in $GamingDefaults.Keys) { if ($null -ne $old.$k) { $doc[$k] = $old.$k } }
        } catch { }
    }
    [System.IO.File]::WriteAllText($GamingMarker, (ConvertTo-Json -InputObject $doc), (New-Object System.Text.UTF8Encoding($false)))
    $Action = "status"
}
if ($Action -eq "off") {
    Remove-Item -LiteralPath $GamingMarker -Force -ErrorAction SilentlyContinue
    $Action = "status"
}
if ($Action -eq "status") {
    $s = Get-GamingMode
    if (-not $s) {
        Write-Host "gaming mode: off"
    } else {
        $left = if ($s.Until) { $span = $s.Until - (Get-Date); "until $($s.Until.ToString('HH:mm')), {0}:{1:00} left" -f [int][math]::Floor($span.TotalHours), $span.Minutes } else { "until switched off" }
        Write-Host "gaming mode: ON ($left), marker $GamingMarker"
        Write-Host ("  shards {0}, golden workers {1}, dotnet -m:{2}, priority {3}, threads {4}, watchdog x{5}, maxWaitSec {6}" -f $s.shards, $s.goldenWorkers, $s.dotnetCpus, $s.priority, $s.ThreadsText, $s.watchdogFactor, $s.maxWaitSec)
    }
    $holder = Read-HeldFile $GamingLock
    Write-Host ("  lock: {0}" -f $(if ($holder) { "held by $(Format-GamingHolder $holder)" } else { "free" }))
    foreach ($w in (Get-GamingWaiters)) { Write-Host "  waiting: $(Format-GamingHolder $w)" }
    exit 0
}
if ($Action) { Write-Host "usage: .\GamingMode.ps1 on [-Hours N | -Forever] | off | status | -SelfTest"; exit 2 }

if ($SelfTest) {
    $failed = 0
    function Assert-Gaming {
        param([bool]$Ok, [string]$What)
        if ($Ok) { Write-Host "  PASS  $What" } else { Write-Host "  FAIL  $What" -ForegroundColor Red; $script:failed++ }
    }
    $dir = Join-Path ([System.IO.Path]::GetTempPath()) "csvm-gaming-selftest-$PID"
    $null = New-Item -ItemType Directory -Path $dir -Force
    $savedMarker = $env:CSVM_GAMING_MARKER
    $env:CSVM_GAMING_MARKER = Join-Path $dir "csvm-gaming"
    $script:GamingMarker = $env:CSVM_GAMING_MARKER
    $script:GamingLock = "$GamingMarker.lock"
    $children = @()
    try {
        # Expiry.
        $now = Get-Date "2026-01-01T12:00:00"
        Assert-Gaming (-not (ConvertFrom-GamingMarker -Text '{"until":"2026-01-01T11:59:00"}' -WrittenAt $now -Now $now)) "a past until reads as off"
        Assert-Gaming ((ConvertFrom-GamingMarker -Text '{"until":"2026-01-01T13:00:00"}' -WrittenAt $now -Now $now).Until -eq (Get-Date "2026-01-01T13:00:00")) "a future until reads as on until then"
        Assert-Gaming ($null -eq (ConvertFrom-GamingMarker -Text '{"until":"forever"}' -WrittenAt $now.AddDays(-30) -Now $now).Until) "forever never expires"
        Assert-Gaming ((ConvertFrom-GamingMarker -Text '' -WrittenAt $now.AddHours(-3) -Now $now) -and -not (ConvertFrom-GamingMarker -Text '' -WrittenAt $now.AddHours(-5) -Now $now)) "an unreadable marker expires 4 hours after it was written"
        [System.IO.File]::WriteAllText($GamingMarker, '{"until":"2000-01-01T00:00:00"}')
        Assert-Gaming ((-not (Get-GamingMode)) -and -not (Test-Path -LiteralPath $GamingMarker)) "an expired marker reads as off and is deleted, so the engine's file check agrees"

        # Tunable parsing.
        $d = ConvertFrom-GamingMarker -Text '{}' -WrittenAt $now -Now $now -ProcessorCount 16
        Assert-Gaming ($d.shards -eq 2 -and $d.goldenWorkers -eq 1 -and $d.dotnetCpus -eq 2 -and $d.PriorityClass -eq 0x4000 -and $d.watchdogFactor -eq 3 -and $d.maxWaitSec -eq 1800) "an empty marker takes every default"
        Assert-Gaming ($d.AffinityMask -eq [uint64]0xF000 -and $d.ThreadsText -eq "12-15") "the default affinity is the last 4 of 16 logical processors"
        $c = ConvertFrom-GamingMarker -Text '{"shards":3,"goldenWorkers":"x","threads":40,"priority":"Idle","maxWaitSec":-5}' -WrittenAt $now -Now $now -ProcessorCount 16
        Assert-Gaming ($c.shards -eq 3 -and $c.goldenWorkers -eq 1 -and $c.maxWaitSec -eq 1800 -and $c.PriorityClass -eq 0x40) "set fields are read, bad ones fall back to their default"
        Assert-Gaming ($c.AffinityMask -eq [uint64]0xFFFF -and $c.ThreadsText -eq "0-15") "threads are clamped to the machine"
        Assert-Gaming (((Get-GamingDotnetArgs $d -Build) -join " ") -eq "-m:2 -nodeReuse:false -p:UseSharedCompilation=false" -and @(Get-GamingDotnetArgs $null).Count -eq 0) "dotnet takes its throttle arguments only while on"
        Assert-Gaming ((Get-GamingTimeoutSec $d 300) -eq 900 -and (Get-GamingTimeoutSec $null 300) -eq 300 -and (Get-GamingTimeoutSec $d 0) -eq 0) "the watchdog scales by its factor only while on, and none stays none"
        $Hours = 99; $Action = "kept"
        . (Join-Path $PSScriptRoot "GamingModeCore.ps1")
        Assert-Gaming ($Hours -eq 99 -and $Action -eq "kept") "dot-sourcing the core binds nothing in the caller's scope"
        $script:GamingMarker = $env:CSVM_GAMING_MARKER
        $script:GamingLock = "$GamingMarker.lock"

        # The lock, across processes. This process holds it; a child with a 3 s cap waits, names the
        # holder, and ends DEFERRED.
        [System.IO.File]::WriteAllText($GamingMarker, '{"until":"forever","maxWaitSec":3}')
        $me = New-GamingRun -Worktree "selftest-holder"
        Sync-GamingStage $me
        Assert-Gaming ([bool]$me.Lock -and $me.Throttled) "gaming on: the first run takes the lock"
        $self = Join-Path $PSScriptRoot "GamingModeCore.ps1"
        function Start-GamingChild {
            param([string]$Script)
            $psi = New-Object System.Diagnostics.ProcessStartInfo
            $psi.FileName = "powershell.exe"
            $psi.Arguments = "-NoProfile -ExecutionPolicy Bypass -Command `". '$self'; $Script`""
            $psi.UseShellExecute = $false
            $psi.CreateNoWindow = $true
            $psi.RedirectStandardOutput = $true
            $psi.EnvironmentVariables["CSVM_GAMING_MARKER"] = $GamingMarker
            $p = [System.Diagnostics.Process]::Start($psi)
            $script:children += $p
            return [pscustomobject]@{ Process = $p; Out = $p.StandardOutput.ReadToEndAsync() }
        }
        $runChild = '$r = New-GamingRun -Worktree child; Sync-GamingStage $r; Write-Host (''lock='' + [bool]$r.Lock + '' settings='' + [bool]$r.Settings)'
        $waiter = Start-GamingChild $runChild
        $null = $waiter.Process.WaitForExit(30000)
        $out = $waiter.Out.Result
        Assert-Gaming ($out -match "waiting for gaming-mode lock, held by selftest-holder \(pid $PID\) since \d\d:\d\d" -and $out -match "DEFERRED: gaming mode, lock held by selftest-holder" -and $out -match "lock=False") "a second run waits, names the holder, and past maxWaitSec ends DEFERRED"

        # Switched off while a run waits: it stops waiting at its next poll and goes on unthrottled.
        [System.IO.File]::WriteAllText($GamingMarker, '{"until":"forever","maxWaitSec":60}')
        $waiter = Start-GamingChild $runChild
        $listed = $false
        for ($i = 0; $i -lt 100 -and -not $listed; $i++) { Start-Sleep -Milliseconds 100; $listed = @(Get-GamingWaiters | Where-Object { $_.worktree -eq "child" }).Count -eq 1 }
        Assert-Gaming $listed "a waiting run is listed as a waiter"
        Remove-Item -LiteralPath $GamingMarker
        $null = $waiter.Process.WaitForExit(30000)
        $out = $waiter.Out.Result
        Assert-Gaming ($out -match "switched off while waiting" -and $out -match "lock=False settings=False") "switching off releases a waiter unthrottled"
        Sync-GamingStage $me
        Assert-Gaming (-not $me.Lock -and -not $me.Throttled) "at the holder's next boundary, off releases the lock and lifts the throttle"

        # A killed holder frees the lock with its process.
        [System.IO.File]::WriteAllText($GamingMarker, '{"until":"forever","maxWaitSec":60}')
        $holder = Start-GamingChild '$r = New-GamingRun -Worktree child-holder; Sync-GamingStage $r; Start-Sleep 60'
        $held = $false
        for ($i = 0; $i -lt 100 -and -not $held; $i++) { Start-Sleep -Milliseconds 100; $held = (Read-HeldFile $GamingLock).worktree -eq "child-holder" }
        $holder.Process.Kill()
        $null = $holder.Process.WaitForExit(5000)
        $me = New-GamingRun -Worktree "selftest-holder"
        Sync-GamingStage $me
        Assert-Gaming ($held -and [bool]$me.Lock -and $me.WaitedSec -lt 10) "killing the holder frees the lock"
        Exit-GamingRun $me
        Assert-Gaming ((Format-GamingSummary $me) -match '^gaming mode: ON \(until switched off\), waited \d+:\d\d, shards 2, workers 1, threads \d+-\d+$' -and (Format-GamingSummary (New-GamingRun -Worktree x)) -eq "gaming mode: off") "the summary line"
    } finally {
        foreach ($p in $children) { if (-not $p.HasExited) { $p.Kill() } }
        $env:CSVM_GAMING_MARKER = $savedMarker
        Remove-Item -LiteralPath $dir -Recurse -Force -ErrorAction SilentlyContinue
    }
    Write-Host ""
    if ($failed -gt 0) { Write-Host "SELFTEST FAIL: $failed check(s)" -ForegroundColor Red; exit 1 }
    Write-Host "SELFTEST PASS" -ForegroundColor Green
    exit 0
}
