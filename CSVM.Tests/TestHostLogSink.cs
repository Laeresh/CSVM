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
/// <para>⚠ Known residual flake, accepted: a capturing sink is still the one global static, so a
/// parallel class's log line can land in another test's capture list and flip an exact-count
/// assertion (e.g. <c>StuntRaceTests</c>' <c>lines.Count</c>). Never observed in practice; the
/// engineered fix would be an <c>AsyncLocal</c> sink, deliberately not built until it fires.</para>
/// </summary>
internal static class TestHostLogSink
{
    /// <summary>Runs once when the test assembly loads, before any test class's code.</summary>
    [ModuleInitializer]
    internal static void Install() => Log.ConsoleSink = _ => { };
}
