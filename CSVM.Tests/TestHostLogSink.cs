using System.Runtime.CompilerServices;
using CSVM.Utils;

namespace CSVM.Tests;

/// <summary>
/// Installs a process-wide no-op <see cref="Log.ConsoleSink"/> before any test runs.
/// ⚠ Do not remove this. With no sink, <see cref="Log"/> falls through to the real
/// <c>GD.Print</c>, which kills the test host with an <c>AccessViolationException</c> with no
/// engine loaded; a per-class save/restore cannot prevent it because xunit classes run in
/// parallel and can restore each other's sink to null mid-run. A <c>[ModuleInitializer]</c> is
/// used rather than an <c>AsyncLocal</c> because it runs on a flow test threads do not inherit.
/// </summary>
internal static class TestHostLogSink
{
    /// <summary>Runs once when the test assembly loads, before any test class's code.</summary>
    [ModuleInitializer]
    internal static void Install() => Log.ConsoleSink = _ => { };
}
