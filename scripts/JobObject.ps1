# The job object's self-test. The code is JobObjectCore.ps1, which the runners dot-source (through
# HiddenDesktop.ps1); see docs/tooling.md.
#
#   .\scripts\JobObject.ps1 -SelfTest   closing the job kills a member and the member's own child

param([switch]$SelfTest)

. (Join-Path $PSScriptRoot "JobObjectCore.ps1")

if ($SelfTest) {
    # A member's grandchild is the case that matters: Godot and dotnet test reach the work through
    # their own children. cmd starts ping after it was assigned, so ping inherits the job.
    if (-not (Open-RunJob)) { Write-Host "SELFTEST FAIL: no job" -ForegroundColor Red; exit 1 }
    # A round trip through the build stage's toggle must leave the kill on close and no breakaway.
    Set-RunJobBreakaway -On $true
    Set-RunJobBreakaway -On $false
    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName = "cmd.exe"
    $psi.Arguments = "/c ping -n 60 127.0.0.1 >nul"
    $psi.UseShellExecute = $false
    $psi.CreateNoWindow = $true
    $cmd = [System.Diagnostics.Process]::Start($psi)
    Add-ToRunJob -Process $cmd.Handle
    $ping = $null
    for ($i = 0; $i -lt 50 -and -not $ping; $i++) {
        Start-Sleep -Milliseconds 100
        $ping = Get-CimInstance Win32_Process -Filter "ParentProcessId = $($cmd.Id) AND Name = 'PING.EXE'"
    }
    Close-RunJob
    $cmdGone = $cmd.WaitForExit(3000)
    $pingAlive = $ping -and (Get-Process -Id $ping.ProcessId -ErrorAction SilentlyContinue)
    if (-not $ping -or -not $cmdGone -or $pingAlive) {
        Write-Host "SELFTEST FAIL: grandchild seen $([bool]$ping), child gone $cmdGone, grandchild alive $([bool]$pingAlive)" -ForegroundColor Red
        if (-not $cmdGone) { $cmd.Kill() }
        exit 1
    }
    Write-Host "SELFTEST PASS: closing the job killed the child and its grandchild" -ForegroundColor Green
    exit 0
}
