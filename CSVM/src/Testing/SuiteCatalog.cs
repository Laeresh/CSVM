using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace CSVM.Testing;

/// <summary>The registry of the in-engine assertion suites, discovered from the
/// <see cref="SuiteAttribute"/> each body carries in the <c>*Suites.cs</c> modules. It holds no
/// per-suite table of its own: a suite's name and description live on the suite, so adding one
/// touches one file.</summary>
public static class SuiteCatalog
{
    // The suites `--run-tests=tier:quick` runs: one representative per failure surface, checked in
    // here rather than inferred from a diff. Membership is a judgement about the SET, which is why
    // it stays a list rather than moving onto the bodies. The selection rule is in docs/tooling.md;
    // the tier is partial and the full catalog stays the landing gate.
    public static readonly IReadOnlyList<string> QuickTier = new[]
    {
        "puffer-modes",
        "loadout-bind",
        "weapons-fire",
        "air-to-air",
        "ai-actor",
        "instant-action",
        "damage-stages",
        "damage-hd",
        "effect-template-mesh",
        "chapter-census",
        "target-selection",
        "campaign-objectives",
        "music-states",
    };

    // The suites `--run-tests=tier:ci` runs and CI requires to pass. A suite earns membership by
    // passing with no extraction but the --synthetic-data tree, headless, with no IPv6 loopback
    // and no OS shell. A listed suite that loses its input must go red, so this tier counts a skip
    // as a failure. A suite joins when it stops needing the install. One name per line, sorted;
    // the rule is in docs/tooling.md.
    public static readonly IReadOnlyList<string> CiTier = new[]
    {
        "ai-actor",
        "ai-airframe-pool",
        "ai-pursues-structure",
        "anim-call-start-order",
        "anim-clock-realtime",
        "audio-levels-launch",
        "bindings-launch-load",
        "bindings-prompt-device",
        "build-stamp-icons",
        "campaign-coop-cutscene-fullscreen",
        "chase-trail",
        "cinema-skip-pad",
        "cockpit-overlay-pass",
        "cockpit-panel-staging",
        "damage-staging-pool",
        "death-camera",
        "display-det-guard",
        "display-render-scale",
        "display-shadow-quality",
        "empty-stage-net",
        "enet-load-stall",
        "enet-transport",
        "extraction-picker",
        "extraction-screen",
        "fade-walk-bound",
        "flight-input-handback",
        "flight-live-respawn-gate",
        "flight-roster-transaction",
        "flight-telemetry-gate",
        "flyby-camera",
        "forward-rotation",
        "gltf-export",
        "graphics-switch-cover",
        "ground-contact-core",
        "hostile-marker-hud",
        "hud-auto-dock-line",
        "hud-crash-prompt",
        "inert-aircraft",
        "lan-discovery",
        "launch-direction-cache",
        "menu-backdrop",
        "menu-controls-seats",
        "menu-coop-door",
        "menu-hangar-journey-core",
        "menu-host-address",
        "menu-host-pointer",
        "menu-host-tracer",
        "menu-instant-action-journey-core",
        "menu-join-board",
        "menu-launch-return",
        "menu-net-door",
        "menu-original-boot",
        "menu-original-builtin-host",
        "menu-original-cheats",
        "menu-original-connection",
        "menu-original-controls",
        "menu-original-coop-boot",
        "menu-original-coop-film",
        "menu-original-coop-flow",
        "menu-original-lobby",
        "menu-original-lobby-teams",
        "menu-original-outlaw-list",
        "menu-original-version",
        "menu-player-setup-journey",
        "menu-player-setup-seats",
        "menu-zone-layout",
        "net-cutscene-skip-episode",
        "options-difficulty-launch",
        "options-targeting-launch",
        "options-view-launch",
        "pause-board",
        "perf-hud-layout",
        "plane-shader-reuse",
        "puffer-distance-fade",
        "puffer-draw-order",
        "puffer-priority-size",
        "puffer-wind",
        "remote-airframe",
        "render-poses",
        "results-board-shell",
        "scene-build-throw-frees",
        "session-start-cover",
        "splitscreen-listeners",
        "stick-discrete-buttons",
        "target-flag",
        "target-input",
        "target-ref",
        "target-selection",
        "targeting-candidates",
        "team-model",
        "weapon-selector-input",
        "wind-streaks",
        "world-lights-enhanced-budget",
        "world-lights-nearest-viewer",
        "world-query-reuse",
    };

    /// <summary>The tier carrying that name, or null when none does (which a selector must treat
    /// as a miss, not as an empty selection).</summary>
    public static SuiteTier? Tier(string name)
    {
        if (name.Equals("quick", StringComparison.OrdinalIgnoreCase))
        {
            return new SuiteTier(QuickTier, SkipFails: false);
        }
        if (name.Equals("ci", StringComparison.OrdinalIgnoreCase))
        {
            return new SuiteTier(CiTier, SkipFails: true);
        }
        return null;
    }

    /// <summary>Every marked body, ordered by name. ⚠ Keep this order deterministic and
    /// machine-independent: <see cref="SuiteShards"/> breaks balancer ties on registry position and
    /// sorts each shard by it, so a rerun divides the same way only if the order does not move.
    /// Reflection order does not qualify; alphabetical does. No suite depends on running after
    /// another.</summary>
    internal static IReadOnlyList<TestHarness.Suite> Discover()
    {
        var found = new List<TestHarness.Suite>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var type in typeof(SuiteCatalog).Assembly.GetTypes())
        {
            const BindingFlags Where = BindingFlags.Static | BindingFlags.Public
                | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            foreach (var method in type.GetMethods(Where))
            {
                if (method.GetCustomAttribute<SuiteAttribute>() is not { } mark)
                {
                    continue;
                }
                found.Add(new TestHarness.Suite(mark.Name, mark.What, Body(method, mark, seen)));
            }
        }
        found.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));
        return found;
    }

    // A malformed declaration throws rather than being skipped: a suite that quietly fails to
    // register runs nowhere and is reported by nothing, which is the failure the registry exists to
    // prevent.
    private static Action<TestContext> Body(MethodInfo method, SuiteAttribute mark, HashSet<string> seen)
    {
        string where = $"{method.DeclaringType?.Name}.{method.Name}";
        if (mark.Name.Length == 0 || mark.What.Length == 0)
        {
            throw new InvalidOperationException($"[Suite] on {where} carries an empty name or description");
        }
        if (!seen.Add(mark.Name))
        {
            throw new InvalidOperationException($"[Suite] name '{mark.Name}' is registered twice, at {where}");
        }
        if (method.ReturnType != typeof(void)
            || method.GetParameters() is not [{ ParameterType: var only }]
            || only != typeof(TestContext))
        {
            throw new InvalidOperationException(
                $"[Suite] on {where} must sit on a static void {method.Name}(TestContext) body");
        }
        return (Action<TestContext>)method.CreateDelegate(typeof(Action<TestContext>));
    }
}

/// <summary>A checked-in tier: the suite names it holds, and whether a member that SKIPs fails the
/// run. The rule rides on the tier, not the harness, so only a selector naming such a tier changes
/// what a skip means. Every other selector keeps SKIP as "the data was not there".</summary>
public sealed record SuiteTier(IReadOnlyList<string> Suites, bool SkipFails);
