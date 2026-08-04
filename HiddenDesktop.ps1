# Runs child processes on a SEPARATE, never-switched-to Windows desktop, so their windows cannot
# appear on the user's screen at all. Dot-sourced by RunTests.ps1; see docs/tooling.md.
#
# Why this exists: a scripted run's window is created without focus and the engine hides it as soon
# as _Ready knows the flags, but the window still EXISTS for the ~1 s Godot takes to boot, and a full
# test run launches it about twenty times. A window belongs to the desktop its creating process was
# started on, and only one desktop is ever displayed, so a process started on a desktop nobody
# switches to cannot flash anything -- the placement is decided before the process runs, which is the
# only kind of placement that works (SHELL-9).
#
# Rendering is unaffected: a golden shot rendered on the hidden desktop matched its manifest md5
# exactly (0bb2532d29261339fb11e3eda79403d0, c1-waterfall), and the window was seen on the new
# desktop in 50 of 52 samples and on ours in 0 -- both halves checked, because "no window appeared"
# is also what a crash looks like (SHELL-12).

if (-not ([System.Management.Automation.PSTypeName]'CSVMHiddenDesktop').Type) {
    Add-Type -Language CSharp -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
using System.Text;

public class CSVMHiddenDesktop
{
    [StructLayout(LayoutKind.Sequential)]
    public struct STARTUPINFO {
        public Int32 cb;
        [MarshalAs(UnmanagedType.LPWStr)] public string lpReserved;
        [MarshalAs(UnmanagedType.LPWStr)] public string lpDesktop;
        [MarshalAs(UnmanagedType.LPWStr)] public string lpTitle;
        public Int32 dwX, dwY, dwXSize, dwYSize, dwXCountChars, dwYCountChars, dwFillAttribute, dwFlags;
        public Int16 wShowWindow, cbReserved2;
        public IntPtr lpReserved2, hStdInput, hStdOutput, hStdError;
    }
    [StructLayout(LayoutKind.Sequential)]
    public struct PROCESS_INFORMATION { public IntPtr hProcess, hThread; public Int32 dwProcessId, dwThreadId; }
    [StructLayout(LayoutKind.Sequential)]
    public struct SECURITY_ATTRIBUTES { public Int32 nLength; public IntPtr lpSecurityDescriptor; public bool bInheritHandle; }

    [DllImport("user32.dll", SetLastError=true, CharSet=CharSet.Unicode)]
    static extern IntPtr CreateDesktop(string name, IntPtr dev, IntPtr dm, int flags, uint access, IntPtr sa);
    [DllImport("user32.dll", SetLastError=true)] static extern bool CloseDesktop(IntPtr h);
    [DllImport("kernel32.dll", SetLastError=true, CharSet=CharSet.Unicode)]
    static extern IntPtr CreateFile(string path, uint access, uint share, ref SECURITY_ATTRIBUTES sa,
                                    uint disp, uint flags, IntPtr tmpl);
    [DllImport("kernel32.dll", SetLastError=true, CharSet=CharSet.Unicode)]
    static extern bool CreateProcess(string app, StringBuilder cmd, IntPtr pa, IntPtr ta, bool inherit,
                                     uint flags, IntPtr env, string cwd, ref STARTUPINFO si,
                                     out PROCESS_INFORMATION pi);
    [DllImport("kernel32.dll", SetLastError=true)] static extern uint WaitForSingleObject(IntPtr h, uint ms);
    [DllImport("kernel32.dll", SetLastError=true)] static extern bool GetExitCodeProcess(IntPtr h, out uint code);
    [DllImport("kernel32.dll", SetLastError=true)] static extern bool TerminateProcess(IntPtr h, uint code);
    [DllImport("kernel32.dll", SetLastError=true)] static extern bool CloseHandle(IntPtr h);

    const uint GENERIC_ALL = 0x10000000, GENERIC_READ = 0x80000000, GENERIC_WRITE = 0x40000000;
    const uint SHARE_RW = 3, CREATE_ALWAYS = 2, OPEN_EXISTING = 3, NORMAL = 0x80, USESTDHANDLES = 0x100;

    static IntPtr _desk = IntPtr.Zero;
    static string _name = null;

    public static bool IsOpen { get { return _desk != IntPtr.Zero; } }
    public static string Name { get { return _name; } }

    /// <summary>Creates the desktop. Returns false rather than throwing: a machine or session that
    /// refuses it must still be able to run the tests, just visibly.</summary>
    public static bool Open(string name)
    {
        if (_desk != IntPtr.Zero) { return true; }
        _desk = CreateDesktop(name, IntPtr.Zero, IntPtr.Zero, 0, GENERIC_ALL, IntPtr.Zero);
        if (_desk == IntPtr.Zero) { return false; }
        _name = name;
        return true;
    }

    public static void Close()
    {
        if (_desk != IntPtr.Zero) { CloseDesktop(_desk); _desk = IntPtr.Zero; _name = null; }
    }

    /// <summary>Runs exe to completion on the hidden desktop and returns its exit code. stdout and
    /// stderr go to real inheritable file handles -- without them the GUI binary reattaches to the
    /// launching console and prints past every redirection (SHELL-10).</summary>
    public static int Run(string exe, string cmdLine, string cwd, string outPath, string errPath)
    {
        return Run(exe, cmdLine, cwd, outPath, errPath, 0xFFFFFFFF);
    }

    /// <summary>Same, but the wait is bounded: after timeoutMs the process is terminated and 124
    /// is returned (the GNU timeout convention), so a probe that never quits cannot hang its
    /// caller forever. 0xFFFFFFFF (INFINITE) preserves the unbounded wait.</summary>
    public static int Run(string exe, string cmdLine, string cwd, string outPath, string errPath, uint timeoutMs)
    {
        if (_desk == IntPtr.Zero) { throw new InvalidOperationException("hidden desktop not open"); }

        SECURITY_ATTRIBUTES sa = new SECURITY_ATTRIBUTES();
        sa.nLength = Marshal.SizeOf(typeof(SECURITY_ATTRIBUTES));
        sa.bInheritHandle = true;

        IntPtr hOut = CreateFile(outPath, GENERIC_WRITE, SHARE_RW, ref sa, CREATE_ALWAYS, NORMAL, IntPtr.Zero);
        IntPtr hErr = CreateFile(errPath, GENERIC_WRITE, SHARE_RW, ref sa, CREATE_ALWAYS, NORMAL, IntPtr.Zero);
        IntPtr hIn  = CreateFile("NUL",   GENERIC_READ,  SHARE_RW, ref sa, OPEN_EXISTING, NORMAL, IntPtr.Zero);

        STARTUPINFO si = new STARTUPINFO();
        si.cb = Marshal.SizeOf(typeof(STARTUPINFO));
        si.lpDesktop  = "WinSta0\\" + _name;
        si.dwFlags    = (int)USESTDHANDLES;
        si.hStdOutput = hOut; si.hStdError = hErr; si.hStdInput = hIn;

        PROCESS_INFORMATION pi;
        StringBuilder buf = new StringBuilder(cmdLine);   // CreateProcess may write into it
        bool ok = CreateProcess(exe, buf, IntPtr.Zero, IntPtr.Zero, true, 0, IntPtr.Zero, cwd, ref si, out pi);
        int err = Marshal.GetLastWin32Error();
        CloseHandle(hOut); CloseHandle(hErr); CloseHandle(hIn);
        if (!ok) { throw new Exception("CreateProcess failed, win32 error " + err); }

        uint code;
        if (WaitForSingleObject(pi.hProcess, timeoutMs) == 0x102 /* WAIT_TIMEOUT */)
        {
            TerminateProcess(pi.hProcess, 124);
            WaitForSingleObject(pi.hProcess, 5000);   // let the kill land before the caller reads streams
            code = 124;
        }
        else
        {
            GetExitCodeProcess(pi.hProcess, out code);
        }
        CloseHandle(pi.hThread);
        CloseHandle(pi.hProcess);
        return (int)code;
    }
}
'@
}

<#
.SYNOPSIS
Opens the hidden desktop. Returns $true if child processes can be run on it.
#>
function Open-HiddenDesktop {
    param([string]$Name = "csvm-tests")
    return [CSVMHiddenDesktop]::Open($Name)
}

<#
.SYNOPSIS
Runs an executable to completion on the hidden desktop and returns its exit code.
#>
function Invoke-OnHiddenDesktop {
    param(
        [Parameter(Mandatory=$true)][string]$Exe,
        [Parameter(Mandatory=$true)][string]$CommandLine,
        [Parameter(Mandatory=$true)][string]$WorkingDirectory,
        [Parameter(Mandatory=$true)][string]$StdOut,
        [Parameter(Mandatory=$true)][string]$StdErr,
        # 0 = wait forever (the pre-timeout behaviour); > 0 kills the process after that many
        # seconds and returns 124.
        [int]$TimeoutSec = 0
    )
    $timeoutMs = if ($TimeoutSec -gt 0) { [uint32]($TimeoutSec * 1000) } else { [uint32]::MaxValue }
    return [CSVMHiddenDesktop]::Run($Exe, $CommandLine, $WorkingDirectory, $StdOut, $StdErr, $timeoutMs)
}

<#
.SYNOPSIS
Closes the hidden desktop. Safe to call when it was never opened.
#>
function Close-HiddenDesktop {
    [CSVMHiddenDesktop]::Close()
}
