# The memory ledger's command line. The code is MemoryLedgerCore.ps1, which the runners dot-source;
# see docs/tooling.md.
#
#   .\scripts\MemoryLedger.ps1 status      live reservations, waiters, the learned estimate per kind
#   .\scripts\MemoryLedger.ps1 -SelfTest   the admission rule, handle-held liveness, the empty-ledger
#                                  progress rule, the estimate update and the quiet census
#
# Pure ASCII on purpose (PROJECT_CONTEXT.md).

param([string]$Action = "", [switch]$SelfTest)

. (Join-Path $PSScriptRoot "MemoryLedgerCore.ps1")

if ($Action -eq "status") {
    Format-MemLedger | ForEach-Object { Write-Host $_ }
    exit 0
}

if ($SelfTest) {
    $failed = 0
    function Assert-Mem {
        param([bool]$Ok, [string]$What)
        if ($Ok) { Write-Host "  PASS  $What" } else { Write-Host "  FAIL  $What" -ForegroundColor Red; $script:failed++ }
    }
    $script:MemLedgerDir = Join-Path ([System.IO.Path]::GetTempPath()) ("csvm-mem-selftest-" + $PID)
    $script:MemHistoryFile = Join-Path $MemLedgerDir "history.json"
    $script:MemEstimatesFile = Join-Path $MemLedgerDir "estimates.json"
    $null = New-Item -ItemType Directory -Path $MemLedgerDir -Force
    $savedAvail = $env:CSVM_MEM_AVAILABLE_GB
    try {
        # The rule: 20 available, 3 owed, floor 8 leaves 9 for a 4 GB launch, not for a 10 GB one.
        Assert-Mem (Test-MemAdmissible -AvailableGB 20 -OutstandingGB @(1, 2) -EstimateGB 4 -FloorGB 8 -LiveCount 2) "admits when available - owed - floor covers the estimate"
        Assert-Mem (-not (Test-MemAdmissible -AvailableGB 20 -OutstandingGB @(1, 2) -EstimateGB 10 -FloorGB 8 -LiveCount 2)) "refuses when it does not"
        Assert-Mem (Test-MemAdmissible -AvailableGB 9 -OutstandingGB @() -EstimateGB 50 -FloorGB 8 -LiveCount 0) "progress: an empty ledger above the floor admits any estimate"
        Assert-Mem (-not (Test-MemAdmissible -AvailableGB 7 -OutstandingGB @() -EstimateGB 1 -FloorGB 8 -LiveCount 0)) "an empty ledger below the floor still waits"

        # Progress end to end: 9 GB simulated, nothing live, a 4 GB shard is admitted though 4 + 8
        # exceeds 9; a second is then refused, because the first is live and owes its estimate.
        $env:CSVM_MEM_AVAILABLE_GB = "9"
        $first = Request-MemAdmission -Kind "engine-shard" -Worktree "selftest"
        Assert-Mem $first.Admitted "progress: the empty ledger admits a 4 GB launch at 9 GB available"
        Assert-Mem (Test-Path $MemEstimatesFile) "the first admission seeds estimates.json for the engine"
        $second = Request-MemAdmission -Kind "engine-shard" -Worktree "selftest"
        Assert-Mem (-not $second.Admitted) "a second launch waits while the first reservation is live"
        Assert-Mem (@(Get-MemReservations).Count -eq 1) "the held reservation is listed live"
        Close-MemReservation $first.Reservation
        Assert-Mem (@(Get-MemReservations).Count -eq 0 -and -not (Test-Path $first.Reservation.Path)) "a released reservation is gone"

        # A file nobody holds (a holder that died before delete-on-close) is swept, never counted.
        Write-MemFile -Path (Join-Path $MemLedgerDir "res-stale.json") -Text '{"pid":0,"kind":"probe","estimateGB":4}'
        Assert-Mem (@(Get-MemReservations).Count -eq 0) "an unheld reservation file is swept"

        # Liveness across processes: a killed holder frees its reservation with its process.
        $ledger = Join-Path $PSScriptRoot "MemoryLedgerCore.ps1"
        $psi = New-Object System.Diagnostics.ProcessStartInfo
        $psi.FileName = "powershell.exe"
        $psi.Arguments = "-NoProfile -ExecutionPolicy Bypass -Command `". '$ledger'; `$r = Request-MemAdmission -Kind probe -Worktree child; Start-Sleep 60`""
        $psi.UseShellExecute = $false
        $psi.CreateNoWindow = $true
        $psi.EnvironmentVariables["CSVM_MEM_DIR"] = $MemLedgerDir
        $child = [System.Diagnostics.Process]::Start($psi)
        $seen = $false
        for ($i = 0; $i -lt 300 -and -not $seen; $i++) {
            Start-Sleep -Milliseconds 100
            $seen = @(Get-MemReservations | Where-Object { $_.Worktree -eq "child" }).Count -eq 1
        }
        Assert-Mem $seen "another process's held reservation is seen live"
        $child.Kill()
        $null = $child.WaitForExit(5000)
        $freed = $false
        for ($i = 0; $i -lt 30 -and -not $freed; $i++) {
            Start-Sleep -Milliseconds 100
            $freed = @(Get-MemReservations).Count -eq 0
        }
        Assert-Mem $freed "killing the holder frees its reservation"

        # Estimates: the seed with no history, then the highest of the last 20 plus 25 percent.
        Assert-Mem ((Get-MemEstimateGB -Kind "capture-xr") -eq $MemSeedGB["capture-xr"]) "a kind with no history is estimated at its seed"
        Add-MemHistory -Kind "perf" -PeakGB 10 -Worktree "selftest"
        for ($i = 1; $i -le 20; $i++) { Add-MemHistory -Kind "perf" -PeakGB 2 -Worktree "selftest" }
        Assert-Mem ((Get-MemEstimateGB -Kind "perf") -eq $MemSeedGB["perf"]) "the older 10 GB peak dropped out, and 20 small peaks never take the estimate below the seed"
        Add-MemHistory -Kind "perf" -PeakGB 6 -Worktree "selftest"
        Assert-Mem ((Get-MemEstimateGB -Kind "perf") -eq 7.5) "a higher peak raises the estimate"
        $written = [System.IO.File]::ReadAllText($MemEstimatesFile) | ConvertFrom-Json
        Assert-Mem ([double]$written.perf -eq 7.5 -and [double]$written."engine-shard" -eq $MemSeedGB["engine-shard"]) "estimates.json carries every kind for the engine"

        # An engine-shard peak is learned only from a launch marked a full shard (RunTests.ps1's
        # Get-EngineShardPlan decides which, and its -SelfTest checks that).
        foreach ($complete in @($false, $true)) {
            $res = (Request-MemAdmission -Kind "engine-shard" -Worktree "selftest").Reservation
            $short = [System.Diagnostics.Process]::Start((New-Object System.Diagnostics.ProcessStartInfo -Property @{
                FileName = "cmd.exe"; Arguments = "/c ping -n 2 127.0.0.1 >nul"; UseShellExecute = $false; CreateNoWindow = $true }))
            Set-MemReservationPid -Reservation $res -ProcessId $short.Id
            $short.WaitForExit()
            Close-MemReservation -Reservation $res -LearnShardPeak:$complete
        }
        Assert-Mem (@((Read-MemHistory)["engine-shard"]).Count -eq 1) "an unmarked engine-shard peak is not recorded, a full shard's is"

        Assert-Mem ((Get-MemKind @("--run-tests=quick")) -eq "engine-shard" -and (Get-MemKind @("--det", "--graphics=enhanced", "--frames=9")) -eq "capture-enhanced") "kinds classify from the user arguments"
        Assert-Mem ((Test-MemNonInteractive @("--frames=9")) -and -not (Test-MemNonInteractive @("--plane=player_bhawk"))) "interactive play is not admitted"

        # The quiet census: scripted launches by worktree (a quoted tree with a space, a bare one,
        # one known only by its live reservation, this process standing in), never play, an editor
        # or another project.
        $held = (Request-MemAdmission -Kind "probe" -Worktree "selftest").Reservation
        Set-MemReservationPid -Reservation $held -ProcessId $PID
        $fake = @(
            [pscustomobject]@{ ProcessId = 11; CommandLine = '"G.exe" --path "Z:\Crimson Skies\CSVM" --log-file x res://scenes/Main.tscn -- --run-tests' }
            [pscustomobject]@{ ProcessId = 12; CommandLine = 'G.exe --path Z:\CSVM\.claude\worktrees\a\CSVM\ res://scenes/Main.tscn -- --fly --frames=9 "--screenshot=Z:\a b\x.png"' }
            [pscustomobject]@{ ProcessId = $PID; CommandLine = 'G.exe --path Z:\CSVM\.claude\worktrees\b\CSVM res://scenes/Main.tscn -- --fly' }
            [pscustomobject]@{ ProcessId = 13; CommandLine = 'G.exe --path Z:\CSVM\CSVM res://scenes/Main.tscn -- --plane=player_bhawk' }
            [pscustomobject]@{ ProcessId = 14; CommandLine = 'G.exe --path Z:\CSVM\CSVM --remote-debug tcp://127.0.0.1:6007' }
            [pscustomobject]@{ ProcessId = 15; CommandLine = 'G.exe --editor --path Z:\CSVM\CSVM' }
            [pscustomobject]@{ ProcessId = 16; CommandLine = 'G.exe --path D:\Other\game -- --det' }
        )
        $quiet = @(Get-CsvmGodots -Processes $fake)
        Close-MemReservation $held
        Assert-Mem ($quiet.Count -eq 3 -and $quiet[0].Worktree -eq "Z:\Crimson Skies" -and $quiet[0].Kind -eq "unledgered" -and
            $quiet[1].Worktree -eq "Z:\CSVM\.claude\worktrees\a" -and $quiet[2].Kind -eq "probe" -and $quiet[2].Worktree -eq "Z:\CSVM\.claude\worktrees\b") "the quiet census names scripted CSVM launches by worktree and skips play, editors and other projects"
    } finally {
        $env:CSVM_MEM_AVAILABLE_GB = $savedAvail
        Remove-Item -LiteralPath $MemLedgerDir -Recurse -Force -ErrorAction SilentlyContinue
    }
    Write-Host ""
    if ($failed -gt 0) { Write-Host "SELFTEST FAIL: $failed check(s)" -ForegroundColor Red; exit 1 }
    Write-Host "SELFTEST PASS" -ForegroundColor Green
    exit 0
}
