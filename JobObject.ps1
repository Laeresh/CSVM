# Holds every child a runner starts in one Windows job object that kills its members when its last
# handle closes. Dot-sourced by RunTests.ps1 and RunProbe.ps1 (through HiddenDesktop.ps1); see
# docs/tooling.md. `.\JobObject.ps1 -SelfTest` checks the kill on close end to end.
#
# Why this exists: the runner's PowerShell is the only thing that waits on its children, so without
# the job a killed runner leaves its Godot shards and testhosts running on the hidden desktop, beside
# the next run. The only handle to the job is this process's, so the kernel closes it whatever ends
# the process, and every member dies with it. Stop-StrayGodots stays as the backstop for a session
# that refuses the job.
#
# The runner's own process is never a member: a member cannot leave, so the job's close at the
# summary would kill the shell the runner was started from.

param([switch]$SelfTest)

if (-not ([System.Management.Automation.PSTypeName]'CSVMRunJob').Type) {
    Add-Type -Language CSharp -TypeDefinition @'
using System;
using System.Runtime.InteropServices;

public class CSVMRunJob
{
    [StructLayout(LayoutKind.Sequential)]
    struct BASIC_LIMIT {
        public Int64 PerProcessUserTimeLimit, PerJobUserTimeLimit;
        public UInt32 LimitFlags;
        public UIntPtr MinimumWorkingSetSize, MaximumWorkingSetSize;
        public UInt32 ActiveProcessLimit;
        public UIntPtr Affinity;
        public UInt32 PriorityClass, SchedulingClass;
    }
    [StructLayout(LayoutKind.Sequential)]
    struct IO_COUNTERS {
        public UInt64 ReadOperationCount, WriteOperationCount, OtherOperationCount;
        public UInt64 ReadTransferCount, WriteTransferCount, OtherTransferCount;
    }
    [StructLayout(LayoutKind.Sequential)]
    struct EXTENDED_LIMIT {
        public BASIC_LIMIT Basic;
        public IO_COUNTERS Io;
        public UIntPtr ProcessMemoryLimit, JobMemoryLimit, PeakProcessMemoryUsed, PeakJobMemoryUsed;
    }

    [DllImport("kernel32.dll", SetLastError=true, CharSet=CharSet.Unicode)]
    static extern IntPtr CreateJobObject(IntPtr sa, string name);
    [DllImport("kernel32.dll", SetLastError=true)]
    static extern bool SetInformationJobObject(IntPtr job, int infoClass, ref EXTENDED_LIMIT info, int size);
    [DllImport("kernel32.dll", SetLastError=true)]
    static extern bool QueryInformationJobObject(IntPtr job, int infoClass, out EXTENDED_LIMIT info, int size, IntPtr returned);
    [DllImport("kernel32.dll", SetLastError=true)] static extern bool AssignProcessToJobObject(IntPtr job, IntPtr process);
    [DllImport("kernel32.dll", SetLastError=true)] static extern bool CloseHandle(IntPtr h);

    const int ExtendedLimitInformation = 9;
    const uint SILENT_BREAKAWAY_OK = 0x1000, KILL_ON_JOB_CLOSE = 0x2000;

    static IntPtr _job = IntPtr.Zero;
    static bool _refused = false;

    /// <summary>The run's job, or zero. The seam for limits other than the kill on close; a limit
    /// set through it is read, modified and written back, as SetBreakaway does.</summary>
    public static IntPtr Handle { get { return _job; } }

    /// <summary>Creates the run's job, first closing one an interrupted run in this session left
    /// open (which kills its orphans). Returns null, or why there is no job.</summary>
    public static string Open()
    {
        Close();
        _refused = false;
        _job = CreateJobObject(IntPtr.Zero, null);
        if (_job == IntPtr.Zero) { return "CreateJobObject failed, win32 error " + Marshal.GetLastWin32Error(); }
        EXTENDED_LIMIT info = new EXTENDED_LIMIT();
        info.Basic.LimitFlags = KILL_ON_JOB_CLOSE;
        int err = Write(ref info);
        if (err != 0) { Close(); return "SetInformationJobObject failed, win32 error " + err; }
        return null;
    }

    /// <summary>With breakaway on, a member's new children start outside the job (they still
    /// land in any job above it). Toggles only that flag, keeping every other limit on the job.
    /// Returns 0 or the win32 error.</summary>
    public static int SetBreakaway(bool on)
    {
        EXTENDED_LIMIT info;
        if (!QueryInformationJobObject(_job, ExtendedLimitInformation, out info, Size, IntPtr.Zero))
        {
            return Marshal.GetLastWin32Error();
        }
        info.Basic.LimitFlags = on ? (info.Basic.LimitFlags | SILENT_BREAKAWAY_OK)
                                   : (info.Basic.LimitFlags & ~SILENT_BREAKAWAY_OK);
        return Write(ref info);
    }

    const uint LIMIT_AFFINITY = 0x10, LIMIT_PRIORITY_CLASS = 0x20;

    /// <summary>Sets the priority class and affinity every member runs at, or clears either
    /// limit with 0. Keeps every other limit on the job. Returns 0 or the win32 error.</summary>
    public static int SetThrottle(uint priorityClass, ulong affinity)
    {
        EXTENDED_LIMIT info;
        if (!QueryInformationJobObject(_job, ExtendedLimitInformation, out info, Size, IntPtr.Zero))
        {
            return Marshal.GetLastWin32Error();
        }
        info.Basic.LimitFlags &= ~(LIMIT_AFFINITY | LIMIT_PRIORITY_CLASS);
        if (priorityClass != 0) { info.Basic.LimitFlags |= LIMIT_PRIORITY_CLASS; info.Basic.PriorityClass = priorityClass; }
        if (affinity != 0) { info.Basic.LimitFlags |= LIMIT_AFFINITY; info.Basic.Affinity = new UIntPtr(affinity); }
        return Write(ref info);
    }

    static int Size { get { return Marshal.SizeOf(typeof(EXTENDED_LIMIT)); } }

    static int Write(ref EXTENDED_LIMIT info)
    {
        return SetInformationJobObject(_job, ExtendedLimitInformation, ref info, Size) ? 0 : Marshal.GetLastWin32Error();
    }

    /// <summary>Adds a process. Returns 0, or the win32 error of the FIRST refusal; after one the
    /// run goes on without the job and later calls return 0 silently.</summary>
    public static int Assign(IntPtr process)
    {
        if (_job == IntPtr.Zero || _refused) { return 0; }
        if (AssignProcessToJobObject(_job, process)) { return 0; }
        _refused = true;
        return Marshal.GetLastWin32Error();
    }

    /// <summary>Closes the job, which kills every member still running.</summary>
    public static void Close()
    {
        if (_job != IntPtr.Zero) { CloseHandle(_job); _job = IntPtr.Zero; }
    }
}
'@
}

<#
.SYNOPSIS
Creates this run's job. Prints one line and returns $false when the session refuses it.
#>
function Open-RunJob {
    $err = [CSVMRunJob]::Open()
    if ($err) {
        Write-Host "  job object: $err; children run without it, so a killed runner can leave them running" -ForegroundColor Yellow
        return $false
    }
    return $true
}

<#
.SYNOPSIS
Adds a started process (its handle) to this run's job. Call it before the process can start
children. A refusal (access denied under a job that forbids nesting) prints one line, once.
#>
function Add-ToRunJob {
    param([Parameter(Mandatory=$true)][IntPtr]$Process)
    $err = [CSVMRunJob]::Assign($Process)
    if ($err -ne 0) {
        Write-Host "  job object: AssignProcessToJobObject failed, win32 error $err; later children run without it" -ForegroundColor Yellow
    }
}

<#
.SYNOPSIS
Turns silent breakaway on or off for children created from now on.
#>
function Set-RunJobBreakaway {
    param([Parameter(Mandatory=$true)][bool]$On)
    if ([CSVMRunJob]::Handle -ne [IntPtr]::Zero) { $null = [CSVMRunJob]::SetBreakaway($On) }
}

<#
.SYNOPSIS
Runs every member, present and future, at a Win32 priority class and on an affinity mask, or with
0 and 0 lifts both limits (a process keeps the priority it had). Prints one line when refused.
#>
function Set-RunJobThrottle {
    param([uint32]$PriorityClass, [uint64]$Affinity)
    if ([CSVMRunJob]::Handle -eq [IntPtr]::Zero) { return }
    # A session that loaded an older CSVMRunJob cannot load this one; Add-Type types never unload.
    if (-not [CSVMRunJob].GetMethod("SetThrottle")) {
        Write-Host "  job object: this shell loaded an older JobObject.ps1, so the run is not throttled; start a new shell" -ForegroundColor Yellow
        return
    }
    $err = [CSVMRunJob]::SetThrottle($PriorityClass, $Affinity)
    if ($err -ne 0) {
        Write-Host "  job object: the priority and affinity limits were refused, win32 error $err; the run is not throttled" -ForegroundColor Yellow
    }
}

<#
.SYNOPSIS
Closes this run's job, killing any member still running. Safe to call when none was opened.
#>
function Close-RunJob {
    [CSVMRunJob]::Close()
}

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
