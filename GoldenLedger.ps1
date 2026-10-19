# The goldens ledger's command line. The code is GoldenLedgerCore.ps1, which RunTests.ps1
# dot-sources; see docs/tooling.md.
#
#   .\GoldenLedger.ps1             the ledger grouped by shot: flakes, moves that stayed, re-renders
#                                  with no hash, Faithful reds, re-renders a deferral stopped, and
#                                  each shot's recent moves with their run's load
#   .\GoldenLedger.ps1 -Recent N   N recent moves per shot (default 5)
#   .\GoldenLedger.ps1 -SelfTest   the path, the append from concurrent writers, the mutex timeout,
#                                  the shot lines, the diff fields and the summary, against a
#                                  private file, without Godot
#
# Pure ASCII on purpose (PROJECT_CONTEXT.md).

param([int]$Recent = 5, [switch]$SelfTest)

. (Join-Path $PSScriptRoot "GoldenLedgerCore.ps1")

if (-not $SelfTest) {
    Format-GoldenLedger -Recent $Recent | ForEach-Object { Write-Host $_ }
    exit 0
}

$failed = 0
function Assert-Ledger {
    param([bool]$Ok, [string]$What)
    if ($Ok) { Write-Host "  PASS  $What" } else { Write-Host "  FAIL  $What" -ForegroundColor Red; $script:failed++ }
}
function Start-LedgerChild {
    param([string]$Command)
    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName = "powershell.exe"
    $psi.Arguments = "-NoProfile -ExecutionPolicy Bypass -Command `". '$(Join-Path $PSScriptRoot "GoldenLedgerCore.ps1")'; $Command`""
    $psi.UseShellExecute = $false
    $psi.CreateNoWindow = $true
    $psi.EnvironmentVariables["CSVM_GOLDEN_LEDGER"] = $env:CSVM_GOLDEN_LEDGER
    return [System.Diagnostics.Process]::Start($psi)
}
$dir = Join-Path ([System.IO.Path]::GetTempPath()) ("csvm-golden-ledger-selftest-" + $PID)
$null = New-Item -ItemType Directory -Path $dir -Force
$savedPath = $env:CSVM_GOLDEN_LEDGER
$ledger = Join-Path $dir "ledger.jsonl"
$env:CSVM_GOLDEN_LEDGER = $ledger
try {
    Assert-Ledger ((Get-GoldenLedgerPath) -eq $ledger) "the test-only override moves the ledger"
    # The main checkout as git worktree list names it (its first entry), not through the common dir.
    $env:CSVM_GOLDEN_LEDGER = ""
    $mainTree = (@(git -C $PSScriptRoot worktree list --porcelain) | Where-Object { $_ -like "worktree *" } | Select-Object -First 1).Substring(9)
    $expected = [System.IO.Path]::GetFullPath((Join-Path $mainTree.Replace('/', '\') "goldens-ledger.jsonl"))
    $got = [System.IO.Path]::GetFullPath((Get-GoldenLedgerPath))
    Assert-Ledger ($got -eq $expected) "a worktree resolves the main checkout's file, $got"
    $fallback = Get-GoldenLedgerPath -Root $dir
    Assert-Ledger ($fallback -eq (Join-Path $dir "goldens-ledger.jsonl")) "a tree outside git falls back to its own root (and warns above)"
    $env:CSVM_GOLDEN_LEDGER = $ledger
    Assert-Ledger (@(Format-GoldenLedger)[0] -like "*: 0 move(s) in 0 run(s), 0 clean run(s)") "a missing ledger reads as empty"

    # Four writers at once, 200 lines each, every line long enough that a torn or interleaved write
    # would break its JSON. Without the mutex this loses about a tenth of the lines.
    $writers = @(foreach ($w in 1..4) {
        Start-LedgerChild "foreach (`$i in 1..200) { Add-GoldenLedgerEntry ([ordered]@{ kind = 'shot'; run = 'r$w'; time = '2026-01-0${w}T' + `$i.ToString('000000'); shot = 'shot-$w'; verdict = 'flake'; diff = 'd'; pad = 'x' * 3200 }) }"
    })
    $done = @($writers | Where-Object { $_.WaitForExit(60000) -and $_.ExitCode -eq 0 }).Count
    Assert-Ledger ($done -eq 4) "four concurrent writers finish cleanly"
    $raw = @([System.IO.File]::ReadAllLines($ledger))
    $entries = @(Read-GoldenLedger)
    Assert-Ledger ($raw.Count -eq 800 -and $entries.Count -eq 800) "800 appends make 800 lines, each one parseable JSON ($($raw.Count) lines, $($entries.Count) parsed)"
    Assert-Ledger (@($entries | Where-Object { $_.pad.Length -ne 3200 }).Count -eq 0) "no line was torn or interleaved"
    $bytes = [System.IO.File]::ReadAllBytes($ledger)
    Assert-Ledger ($bytes[0] -eq [byte][char]'{') "the file is written without a BOM"

    # A held mutex makes the append throw at its timeout instead of blocking.
    $marker = Join-Path $dir "held"
    $holder = Start-LedgerChild "`$m = New-Object System.Threading.Mutex(`$false, 'Global\csvm-golden-ledger'); `$null = `$m.WaitOne(); [IO.File]::WriteAllText('$marker', 'x'); Start-Sleep 30"
    for ($i = 0; $i -lt 200 -and -not (Test-Path $marker); $i++) { Start-Sleep -Milliseconds 100 }
    $threw = $null
    try { Add-GoldenLedgerEntry ([ordered]@{ kind = "shot" }) -TimeoutSec 1 } catch { $threw = $_.Exception.Message }
    $holder.Kill(); $null = $holder.WaitForExit(5000)
    Assert-Ledger ((Test-Path $marker) -and $threw -like "*not free within 1s*") "a mutex held elsewhere times out with an error: $threw"
    Add-GoldenLedgerEntry ([ordered]@{ kind = "probe" })
    Assert-Ledger (@([System.IO.File]::ReadAllLines($ledger)).Count -eq 801) "a holder that died frees the mutex for the next append"

    # The diff sentence as fields, in Get-GoldenFrameDiff's own words.
    $d = ConvertTo-GoldenDiffFields "5 pixel(s) differ, at most 1 level(s), inside (10,20)-(12,21)"
    Assert-Ledger ($d.pixels -eq 5 -and $d.maxLevel -eq 1 -and ($d.box -join ",") -eq "10,20,12,21") "a diff sentence becomes pixels, level and box"
    $d = ConvertTo-GoldenDiffFields "the re-render wrote no PNG"
    Assert-Ledger ($null -eq $d.pixels -and $null -eq $d.box) "a diff that measured nothing has null fields"

    # A run line carries the census once; its shots' moves print with its load. An unparseable line
    # is skipped. A run that judged shots and recorded no move is clean; one that judged none is not.
    $busy = [ordered]@{ kind = "run"; run = "busy"; time = "2026-02-01T00:00:00"; tree = "t"; commit = "c"; judged = 24; gaming = "gaming mode: off"
        memory = @("memory ledger x: 9.0 GB available, floor 8 GB, 3 live reservation(s)"); godots = @("a", "b")
        gpu = @("|    0   N/A  N/A            2332    C+G   ...\SearchHost.exe      N/A      |", "| header |") }
    Add-GoldenLedgerEntry $busy
    Add-GoldenLedgerEntry ([ordered]@{ kind = "run"; run = "clean"; time = "2026-02-02T00:00:00"; judged = 24 })
    Add-GoldenLedgerEntry ([ordered]@{ kind = "run"; run = "unjudged"; time = "2026-02-03T00:00:00"; judged = 0 })
    $shots = @(
        (New-GoldenLedgerShot -Run $busy -Shot "shot-a" -Verdict "moved" -Pin "p" -FirstHash "f" -Rerender "r" -Diff "5 pixel(s) differ, at most 1 level(s), inside (10,20)-(12,21)" -Failures "x"),
        (New-GoldenLedgerShot -Run $busy -Shot "shot-b" -Verdict "unrendered" -Pin "p" -FirstHash "f" -Rerender "" -Diff "the re-render wrote no PNG" -Failures "x"),
        (New-GoldenLedgerShot -Run $busy -Shot "shot-c" -Verdict "red" -Pin "p" -FirstHash "f" -Diff "not re-rendered: a Faithful shot" -Failures "x"),
        (New-GoldenLedgerShot -Run $busy -Shot "shot-d" -Verdict "deferred" -Pin "p" -FirstHash "f" -Diff "not re-rendered: the run was deferred" -Failures "x"))
    for ($i = 0; $i -lt $shots.Count; $i++) { $shots[$i].time = "2026-02-01T00:00:0$($i + 1)"; Add-GoldenLedgerEntry $shots[$i] }
    [System.IO.File]::AppendAllText($ledger, "not json`n")
    $c = @(Read-GoldenLedger | Where-Object { $_.shot -eq "shot-c" })[0]
    Assert-Ledger ($c.run -eq "busy" -and $c.tree -eq "t" -and $c.commit -eq "c" -and $c.pin -eq "p" -and $c.firstHash -eq "f" -and $null -eq $c.rerender -and $null -eq $c.pixels -and $null -eq $c.box -and $c.failures -eq "x") "a Faithful red names its run, pin and moved hash, with no re-render and no diff fields"
    $a = @(Read-GoldenLedger | Where-Object { $_.shot -eq "shot-a" })[0]
    Assert-Ledger ($a.rerender -eq "r" -and $a.pixels -eq 5 -and ($a.box -join ",") -eq "10,20,12,21") "a re-rendered shot's line carries its re-render hash and diff fields"
    $summary = @(Format-GoldenLedger -Recent 2)
    Assert-Ledger ($summary[0] -like "*: 804 move(s) in 3 run(s), 1 clean run(s)") "moves, runs and clean runs are counted apart, $($summary[0])"
    Assert-Ledger ($summary[2] -match '^\s+shot-d\s+0\s+0\s+0\s+0\s+1\s+2026-02-01T00:00:04 deferred$') "a re-render a deferral stopped has its own column: '$($summary[2])'"
    Assert-Ledger ($summary[3] -eq "      2026-02-01T00:00:04 deferred: not re-rendered: the run was deferred; gaming mode: off; 3 reservation(s), 2 CSVM Godot(s), 1 GPU process(es)") "each move prints its run's load: '$($summary[3])'"
    Assert-Ledger ($summary[4] -match '^\s+shot-c\s+0\s+0\s+0\s+1\s+0\s+2026-02-01T00:00:03 red$' -and $summary[5] -like "      2026-02-01T00:00:03 red: not re-rendered: a Faithful shot; *") "a Faithful red has its own column: '$($summary[4])'"
    Assert-Ledger ($summary[6] -match '^\s+shot-b\s+0\s+0\s+1\s+0\s+0\s+2026-02-01T00:00:02 unrendered$') "a re-render with no hash has its own column: '$($summary[6])'"
    Assert-Ledger ($summary[8] -match '^\s+shot-a\s+0\s+1\s+0\s+0\s+0\s+2026-02-01T00:00:01 moved$') "the summary groups by shot, most recent first: '$($summary[8])'"
    Assert-Ledger ($summary[9] -like "      2026-02-01T00:00:01 moved: 5 px, 1 level(s), (10,20,12,21); gaming mode: off;*") "a move prints its region from the diff fields: '$($summary[9])'"
    Assert-Ledger ($summary[10] -match '^\s+shot-4\s+200\s+0\s+0\s+0\s+0\s+2026-01-04T000200 flake$' -and $summary[11] -eq "      2026-01-04T000200 flake: d; no census recorded" -and $summary[13] -eq "      ... 198 older") "a run with no census line, and the older moves folded: '$($summary[11])', '$($summary[13])'"

    # The stage's append warns on a failed write instead of throwing.
    $env:CSVM_GOLDEN_LEDGER = Join-Path $dir "missing\ledger.jsonl"
    $threw = $false
    try { Save-GoldenLedgerEntry ([ordered]@{ kind = "run" }) "a probe" } catch { $threw = $true }
    Assert-Ledger (-not $threw -and -not (Test-Path $env:CSVM_GOLDEN_LEDGER)) "a failed append warns (above) and never throws"
    $env:CSVM_GOLDEN_LEDGER = $ledger
} finally {
    $env:CSVM_GOLDEN_LEDGER = $savedPath
    Remove-Item -LiteralPath $dir -Recurse -Force -ErrorAction SilentlyContinue
}
Write-Host ""
if ($failed -gt 0) { Write-Host "SELFTEST FAIL: $failed check(s)" -ForegroundColor Red; exit 1 }
Write-Host "SELFTEST PASS" -ForegroundColor Green
exit 0
