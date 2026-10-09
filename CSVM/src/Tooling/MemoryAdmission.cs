using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using CSVM.Utils;

namespace CSVM.Tooling;

/// <summary>
/// The engine's side of the machine-wide memory ledger that <c>MemoryLedger.ps1</c> keeps in
/// <c>%TEMP%\csvm-mem</c> (docs/tooling.md). A launch that drives and ends itself refuses to start
/// below the memory floor. One that no runner admitted registers itself, so every other admission
/// counts it. Interactive play stays out of the ledger.
/// ⚠ Keep <see cref="Kind"/>, <see cref="IsNonInteractive"/> and the floor in step with
/// <c>MemoryLedger.ps1</c>; the two sides read one ledger.
/// </summary>
public static class MemoryAdmission
{
    /// <summary>The exit code of a refused launch; <c>MemoryLedger.ps1</c>'s
    /// <c>$MemTripwireExitCode</c> must equal it.</summary>
    public const int TripwireExitCode = 75;

    private const double Gb = 1024.0 * 1024.0 * 1024.0;

    // The largest seed in MemoryLedger.ps1's table, for a ledger the scripts have not seeded yet.
    private const double FallbackEstimateGb = 8.5;

    // Read by no one: held open for the process lifetime, so the reservation file lives exactly
    // as long as this launch.
    private static FileStream? _reservation;

    /// <summary>Whether a flag drives and ends this launch: <c>--det</c>, <c>--run-tests</c>,
    /// <c>--frames=</c>, <c>--shots=</c> or <c>--screenshot=</c>.</summary>
    public static bool IsNonInteractive(IReadOnlyList<string> args) => args.Any(a =>
        a == "--det" || a.StartsWith("--run-tests", StringComparison.Ordinal) || a.StartsWith("--frames=", StringComparison.Ordinal)
        || a.StartsWith("--shots=", StringComparison.Ordinal) || a.StartsWith("--screenshot=", StringComparison.Ordinal));

    /// <summary>The ledger kind a launch's memory is learned and estimated under.</summary>
    public static string Kind(IReadOnlyList<string> args) =>
        args.Any(a => a.StartsWith("--run-tests", StringComparison.Ordinal)) ? "engine-shard"
        : args.Any(a => a.StartsWith("--xr", StringComparison.Ordinal)) ? "capture-xr"
        : args.Contains("--graphics=enhanced") ? "capture-enhanced"
        : args.Contains("--perf") ? "perf"
        : args.Any(a => a.StartsWith("--hitch-inject", StringComparison.Ordinal)) ? "hitch"
        : "probe";

    /// <summary>The floor in GB: 16 while the gaming-mode marker exists, else 8.</summary>
    public static double FloorGb(bool gaming) => gaming ? 16.0 : 8.0;

    /// <summary>The refusal line when <paramref name="availableGb"/> is below the floor, else null.
    /// An unreadable figure (null) never refuses.</summary>
    public static string? Refusal(double? availableGb, double floorGb) => availableGb is not { } gb || gb >= floorGb ? null
        : string.Format(CultureInfo.InvariantCulture,
            "memory: {0:0.0} GB available is below the {1:0} GB floor, refusing a non-interactive launch (exit {2})",
            gb, floorGb, TripwireExitCode);

    /// <summary>Runs once at startup. Returns true when the launch must quit with
    /// <see cref="TripwireExitCode"/>; it has then logged why.</summary>
    public static bool Refuses(IReadOnlyList<string> args, string worktree)
    {
        // The ledger and its runners are Windows-only; CI and the Linux checks have neither.
        if (!OperatingSystem.IsWindows() || !IsNonInteractive(args))
        {
            return false;
        }

        string temp = Path.GetTempPath();
        double floor = FloorGb(File.Exists(Path.Combine(temp, "csvm-gaming")));
        double? available = AvailableGb();
        if (Refusal(available, floor) is { } refusal)
        {
            Log.Error("core", $"{refusal}");
            return true;
        }

        Log.Info("core", $"memory: {(available is { } gb ? gb.ToString("0.0", CultureInfo.InvariantCulture) : "unknown")} GB physical available, floor {floor:0} GB");
        if (string.IsNullOrEmpty(System.Environment.GetEnvironmentVariable("CSVM_MEM_RESERVATION")))
        {
            // A mutex an elevated process created refuses this one; the launch still runs.
            try
            {
                Register(Path.Combine(temp, "csvm-mem"), Kind(args), worktree);
            }
            catch (UnauthorizedAccessException e)
            {
                Log.Warn("core", $"memory: could not open the ledger's admission mutex: {e.Message}");
            }
        }

        return false;
    }

    // Physical memory available, as MemoryLedger.ps1 reads it; null when unreadable. Godot's own
    // "available" is commit headroom including the page file. CSVM_MEM_AVAILABLE_GB is the
    // ledger's test-only stand-in.
    private static double? AvailableGb()
    {
        string? forced = System.Environment.GetEnvironmentVariable("CSVM_MEM_AVAILABLE_GB");
        if (double.TryParse(forced, NumberStyles.Float, CultureInfo.InvariantCulture, out double gb))
        {
            return gb;
        }

        var status = new MemoryStatusEx { Length = (uint)Marshal.SizeOf<MemoryStatusEx>() };
        return GlobalMemoryStatusEx(ref status) ? status.AvailPhys / Gb : null;
    }

    // Written under the ledger's admission mutex, as the scripts write theirs. The estimate is the
    // kind's in estimates.json, or the largest seed before the scripts have written one.
    private static void Register(string dir, string kind, string worktree)
    {
        using var mutex = new Mutex(false, @"Global\csvm-mem-admission");
        bool held = false;
        try
        {
            try
            {
                held = mutex.WaitOne(TimeSpan.FromSeconds(10));
            }
            catch (AbandonedMutexException)
            {
                held = true;
            }

            Directory.CreateDirectory(dir);
            double estimate = FallbackEstimateGb;
            string estimates = Path.Combine(dir, "estimates.json");
            if (File.Exists(estimates))
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(estimates));
                if (doc.RootElement.TryGetProperty(kind, out var value))
                {
                    estimate = value.GetDouble();
                }
            }

            string path = Path.Combine(dir, $"res-{Guid.NewGuid():N}.json");
            _reservation = new FileStream(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.Read, 4096, FileOptions.DeleteOnClose);
            var fields = new Dictionary<string, object>
            {
                ["pid"] = System.Environment.ProcessId,
                ["kind"] = kind,
                ["estimateGB"] = estimate,
                ["worktree"] = worktree,
            };
            byte[] bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(fields));
            _reservation.Write(bytes, 0, bytes.Length);
            _reservation.Flush();
            Log.Info("core", $"memory: no runner admitted this launch, registered it in the ledger as {kind} ({estimate:0.0} GB)");
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        {
            Log.Warn("core", $"memory: could not register in the ledger at {dir}: {e.Message}");
        }
        finally
        {
            if (held)
            {
                mutex.ReleaseMutex();
            }
        }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx status);

    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryStatusEx
    {
        public uint Length;
        public uint MemoryLoad;
        public ulong TotalPhys;
        public ulong AvailPhys;
        public ulong TotalPageFile;
        public ulong AvailPageFile;
        public ulong TotalVirtual;
        public ulong AvailVirtual;
        public ulong AvailExtendedVirtual;
    }
}
