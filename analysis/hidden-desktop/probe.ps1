# Probe: can Godot render a golden-identical frame while running on a SEPARATE Windows desktop?
#
# Three separate questions, because "no window appeared" is also what a crash looks like:
#   1. does it render correctly      -> the engine's own raw-pixel md5 vs the golden manifest
#   2. is it invisible to the user   -> EnumWindows on OUR desktop must never see it
#   3. did it actually go elsewhere  -> EnumDesktopWindows on the NEW desktop must see it (rule 122)

$ErrorActionPreference = "Stop"

$cs = @'
using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Collections.Generic;

public class DesktopRunner
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
    [DllImport("user32.dll")] public static extern bool EnumDesktopWindows(IntPtr hDesktop, EnumProc cb, IntPtr p);
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr p);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
    public delegate bool EnumProc(IntPtr h, IntPtr p);

    [DllImport("kernel32.dll", SetLastError=true, CharSet=CharSet.Unicode)]
    static extern IntPtr CreateFile(string path, uint access, uint share, ref SECURITY_ATTRIBUTES sa,
                                    uint disp, uint flags, IntPtr tmpl);
    [DllImport("kernel32.dll", SetLastError=true, CharSet=CharSet.Unicode)]
    static extern bool CreateProcess(string app, StringBuilder cmd, IntPtr pa, IntPtr ta, bool inherit,
                                     uint flags, IntPtr env, string cwd, ref STARTUPINFO si,
                                     out PROCESS_INFORMATION pi);
    [DllImport("kernel32.dll", SetLastError=true)] static extern uint WaitForSingleObject(IntPtr h, uint ms);
    [DllImport("kernel32.dll", SetLastError=true)] static extern bool GetExitCodeProcess(IntPtr h, out uint code);
    [DllImport("kernel32.dll", SetLastError=true)] static extern bool CloseHandle(IntPtr h);

    const uint GENERIC_ALL = 0x10000000, GENERIC_READ = 0x80000000, GENERIC_WRITE = 0x40000000;
    const uint SHARE_RW = 3, CREATE_ALWAYS = 2, OPEN_EXISTING = 3, NORMAL = 0x80, USESTDHANDLES = 0x100;

    static IntPtr _desk = IntPtr.Zero;
    static PROCESS_INFORMATION _pi;
    public static int ProcessId { get { return _pi.dwProcessId; } }

    public static void Start(string desktop, string exe, string cmd, string cwd, string outPath, string errPath)
    {
        _desk = CreateDesktop(desktop, IntPtr.Zero, IntPtr.Zero, 0, GENERIC_ALL, IntPtr.Zero);
        if (_desk == IntPtr.Zero) { throw new Exception("CreateDesktop failed, err " + Marshal.GetLastWin32Error()); }

        SECURITY_ATTRIBUTES sa = new SECURITY_ATTRIBUTES();
        sa.nLength = Marshal.SizeOf(typeof(SECURITY_ATTRIBUTES));
        sa.bInheritHandle = true;

        // Same reason as Invoke-Godot: without real std handles the GUI binary reattaches to the
        // launching console and prints past us (rule 119).
        IntPtr hOut = CreateFile(outPath, GENERIC_WRITE, SHARE_RW, ref sa, CREATE_ALWAYS, NORMAL, IntPtr.Zero);
        IntPtr hErr = CreateFile(errPath, GENERIC_WRITE, SHARE_RW, ref sa, CREATE_ALWAYS, NORMAL, IntPtr.Zero);
        IntPtr hIn  = CreateFile("NUL",   GENERIC_READ,  SHARE_RW, ref sa, OPEN_EXISTING, NORMAL, IntPtr.Zero);

        STARTUPINFO si = new STARTUPINFO();
        si.cb = Marshal.SizeOf(typeof(STARTUPINFO));
        si.lpDesktop  = "WinSta0\\" + desktop;
        si.dwFlags    = (int)USESTDHANDLES;
        si.hStdOutput = hOut; si.hStdError = hErr; si.hStdInput = hIn;

        StringBuilder cmdBuf = new StringBuilder(cmd);   // CreateProcess may write into it
        if (!CreateProcess(exe, cmdBuf, IntPtr.Zero, IntPtr.Zero, true, 0, IntPtr.Zero, cwd, ref si, out _pi))
        {
            throw new Exception("CreateProcess failed, err " + Marshal.GetLastWin32Error());
        }
        CloseHandle(hOut); CloseHandle(hErr); CloseHandle(hIn);
    }

    public static bool HasExited()
    {
        uint code;
        GetExitCodeProcess(_pi.hProcess, out code);
        return code != 259;   // STILL_ACTIVE
    }

    /// windows the process owns ON THE NEW DESKTOP
    public static int CountOnNewDesktop()
    {
        int n = 0;
        int target = _pi.dwProcessId;
        EnumDesktopWindows(_desk, delegate(IntPtr h, IntPtr p) {
            uint pid; GetWindowThreadProcessId(h, out pid);
            if ((int)pid == target) { n++; }
            return true;
        }, IntPtr.Zero);
        return n;
    }

    /// windows the process owns ON OUR OWN (visible) DESKTOP
    public static int CountOnOurDesktop()
    {
        int n = 0;
        int target = _pi.dwProcessId;
        EnumWindows(delegate(IntPtr h, IntPtr p) {
            uint pid; GetWindowThreadProcessId(h, out pid);
            if ((int)pid == target && IsWindowVisible(h)) { n++; }
            return true;
        }, IntPtr.Zero);
        return n;
    }

    public static int Wait()
    {
        WaitForSingleObject(_pi.hProcess, 0xFFFFFFFF);
        uint code; GetExitCodeProcess(_pi.hProcess, out code);
        CloseHandle(_pi.hThread); CloseHandle(_pi.hProcess);
        CloseDesktop(_desk);
        return (int)code;
    }
}
'@
Add-Type -TypeDefinition $cs -Language CSharp

$repo   = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$exe    = Join-Path $repo "tools\godot\Godot_v4.7-stable_mono_win64\Godot_v4.7-stable_mono_win64.exe"
$log    = Join-Path $repo ".scratch\deskprobe.log"
$png    = Join-Path $repo ".scratch\deskprobe.png"
$expect = "0bb2532d29261339fb11e3eda79403d0"   # c1-waterfall, from analysis/goldens/manifest.json

# c1-waterfall's golden command line.
$cmd = '"' + $exe + '" --path "' + (Join-Path $repo "CSVM") + '" --log-file "' + $log + '"' +
       ' res://scenes/Main.tscn -- --freecam --chapter=C1 --pos=-7720,60,-3380' +
       ' --lookat=-7868,40,-3449 --det --mute --frames=120 --screenshot="' + $png + '"'

[DesktopRunner]::Start("csvm-tests", $exe, $cmd, $repo,
                       (Join-Path $repo ".scratch\deskprobe.out"),
                       (Join-Path $repo ".scratch\deskprobe.err"))
Write-Host ("started pid {0} on desktop 'csvm-tests'" -f [DesktopRunner]::ProcessId)

$onNew = 0; $onOurs = 0; $samples = 0
while (-not [DesktopRunner]::HasExited()) {
    $samples++
    if ([DesktopRunner]::CountOnNewDesktop() -gt 0) { $onNew++ }
    if ([DesktopRunner]::CountOnOurDesktop() -gt 0) { $onOurs++ }
    Start-Sleep -Milliseconds 100
}
$code = [DesktopRunner]::Wait()

Write-Host ("exit {0}, {1} samples" -f $code, $samples)
Write-Host ("  windows seen on the NEW desktop : {0} samples" -f $onNew)
Write-Host ("  windows seen on OUR  desktop    : {0} samples" -f $onOurs)
Write-Host ("  png written                     : {0}" -f (Test-Path $png))
if (Test-Path $log) {
    $line = Select-String -Path $log -Pattern "shot pixmd5=" | Select-Object -First 1
    if ($line) {
        $md5 = ([regex]"pixmd5=([0-9a-f]+)").Match($line.Line).Groups[1].Value
        Write-Host ("  raw-pixel md5                   : {0}" -f $md5)
        Write-Host ("  golden                          : {0}   {1}" -f $expect,
                    $(if ($md5 -eq $expect) { "MATCH" } else { "DIFFERENT" }))
    } else {
        Write-Host "  no pixmd5 line in the log -- it did not render"
    }
}
$err = Join-Path $repo ".scratch\deskprobe.err"
if ((Test-Path $err) -and (Get-Item $err).Length -gt 0) {
    Write-Host "  stderr:"; Get-Content $err | Select-Object -First 15 | ForEach-Object { "    $_" }
}
