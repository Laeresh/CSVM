using System.Runtime.CompilerServices;
using CSVM.Utils;

namespace CSVM.Tests;

/// <summary>
/// Installs a process-wide no-op <see cref="Log.ConsoleSink"/> before any test runs.
/// With no sink installed, <see cref="Log"/> falls through to the real <c>GD.Print</c>, whose
/// native string marshalling kills the whole test host with an <c>AccessViolationException</c>
/// when no engine is loaded. A per-class save/restore ritual alone cannot prevent that: xunit runs
/// test classes in parallel, so the first class to finish restores the sink to the null it saw
/// while another class is still logging. With this default installed once, "restore to previous"
/// always lands on some managed sink, never on the engine fallthrough. A test that asserts on
/// console lines still swaps in its own capturing sink for its duration.
///
/// <para>⚠ Do not "simplify" this away because every test looks safe without it: what makes them
/// safe IS this default. It is also the reason <see cref="Log"/>'s scoped sink could not simply
/// replace the static — a <c>[ModuleInitializer]</c> runs on a flow xunit's test threads do not
/// inherit, so an <c>AsyncLocal</c> written here would be invisible where it is needed.</para>
/// </summary>
internal static class TestHostLogSink
{
    /// <summary>Runs once when the test assembly loads, before any test class's code.</summary>
    [ModuleInitializer]
    internal static void Install() => Log.ConsoleSink = _ => { };
}
