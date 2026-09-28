using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The family order, enforced over compiled metadata. Every <c>CSVM.*</c> namespace belongs to one
/// ranked family, and a type may name only types in its own family or a lower one. The rank table
/// below is the only copy of the order; <c>docs/architecture.md</c> points here. Each reference
/// that breaks the order today is an exact type pair in <see cref="Allowed"/>. That list only
/// shrinks: a new pair fails, and so does an entry the code no longer produces. The scan reads
/// IL, so a <c>const</c> the compiler inlined is invisible to it.
/// </summary>
[Trait("Tier", "Quick")]
public sealed class FamilyOrderTests
{
    // Lowest first; the longest matching prefix wins, which is how UI.Boards ranks below Flight
    // while the rest of UI ranks above Session.
    private static readonly (string Prefix, int Rank)[] Families =
    {
        ("CSVM.Utils", 1),
        ("CSVM.Extraction", 2),
        ("CSVM.Mech3", 3),
        ("CSVM.Video", 4),
        ("CSVM.Bindings", 5),
        ("CSVM.Sticks", 6),
        ("CSVM.Effects", 7),
        ("CSVM.Net", 8),
        ("CSVM.UI.Boards", 9),
        ("CSVM.Flight", 10),
        ("CSVM.Spec", 11),
        ("CSVM.Session", 12),
        ("CSVM.Tooling", 13),
        ("CSVM.UI", 14),
        ("CSVM.Session.Launch", 15),
        ("CSVM.Testing", 16),
    };

    // A documented dispatch, not a debt: the probe runner hands a scripted run to the harness.
    private static readonly (string From, string To)[] Exceptions =
    {
        ("CSVM.Tooling", "CSVM.Testing"),
    };

    private static readonly string[] Allowed =
    {
        "CSVM.Effects.EffectAmbience -> CSVM.Flight.Camera.ViewerSet",
        "CSVM.Effects.Precipitation -> CSVM.Flight.Airframe.WeatherState",
        "CSVM.Effects.Puffer -> CSVM.Flight.Camera.ViewerSet",
        "CSVM.Flight.Airframe.PlaneRoster -> CSVM.Spec.SessionSpec",
        "CSVM.Flight.Hud.TargetHud -> CSVM.UI.Overlays.OrbitCamera",
        "CSVM.Session.Campaign.CampaignDirector -> CSVM.UI.Hangar.PlanePickerRoster",
        "CSVM.Session.Campaign.ChapterCinema -> CSVM.UI.Screens.CinemaHandoff",
        "CSVM.Session.Campaign.ChapterCinema -> CSVM.UI.Screens.CinemaPlay",
        "CSVM.Session.Campaign.ClosingCinema -> CSVM.UI.Screens.CinemaHandoff",
        "CSVM.Session.Campaign.ClosingCinema -> CSVM.UI.Screens.CinemaPlay",
        "CSVM.Session.InstantAction.InstantActionDirector -> CSVM.UI.Menu.IaWrapupSnapshot",
        "CSVM.Session.InstantAction.InstantActionDirector -> CSVM.UI.Screens.IaWrapupBoard",
        "CSVM.Session.InstantAction.InstantActionDirector -> CSVM.UI.Screens.MenuInput",
        "CSVM.Session.InstantAction.InstantActionDirector -> CSVM.UI.Screens.ResultsBoard",
        "CSVM.Session.InstantAction.InstantActionDirector -> CSVM.UI.Screens.StuntSplits",
        "CSVM.Session.InstantAction.InstantActionDirector -> CSVM.UI.Screens.StuntSummary",
        "CSVM.Session.Roster.HumanFlightAdapter -> CSVM.Tooling.ProbeRunner",
        "CSVM.Session.Roster.HumanFlightAdapter -> CSVM.UI.Hangar.HangarPaintPage",
        "CSVM.Session.Roster.HumanFlightAdapter -> CSVM.UI.Screens.ResultsBoard",
        "CSVM.Session.Roster.HumanFlightAdapter -> CSVM.UI.Screens.StuntScoreboard",
        "CSVM.Session.Roster.HumanRosterBindings -> CSVM.UI.Screens.MenuInput",
        "CSVM.Spec.SessionSpec -> CSVM.UI.Labs.NodeLab",
        "CSVM.Spec.SessionSpec -> CSVM.UI.Labs.WorldDamageLab",
        "CSVM.Spec.SessionSpec -> CSVM.UI.Menu.NetPlayFeature",
        "CSVM.Tooling.CaptureDirector -> CSVM.UI.Overlays.OrbitCamera",
        "CSVM.Tooling.ProbeRunner -> CSVM.UI.Overlays.OrbitCamera",
        "CSVM.UI.Boards.BoardMenuHost -> CSVM.UI.Screens.MenuInput",
        "CSVM.UI.Boards.BoardMenuView -> CSVM.UI.Screens.MenuInput",
        "CSVM.UI.Boards.BoardPalette -> CSVM.UI.Campaign.CampaignScreen",
        "CSVM.UI.Boards.ComposedBoardView -> CSVM.UI.Screens.MovieSurface",
        "CSVM.UI.Boards.PauseReadout -> CSVM.UI.Menu.BriefingObjective",
        "CSVM.UI.Boards.PauseReadout -> CSVM.UI.Overlays.MissionMap",
        "CSVM.UI.Boards.PauseScreens -> CSVM.UI.Menu.BriefingElement",
        "CSVM.UI.Boards.PauseScreens -> CSVM.UI.Menu.BriefingPoint",
        "CSVM.UI.Boards.PauseScreens -> CSVM.UI.Menu.BriefingReveal",
        "CSVM.UI.Boards.PauseScreens -> CSVM.UI.Menu.EscapeButton",
        "CSVM.UI.Boards.PauseScreens -> CSVM.UI.Menu.EscapeCursor",
        "CSVM.UI.Boards.PauseScreens -> CSVM.UI.Menu.EscapeMap",
        "CSVM.UI.Boards.PauseScreens -> CSVM.UI.Menu.EscapeObjectivesList",
        "CSVM.UI.Boards.PauseScreens -> CSVM.UI.Menu.EscapeShared",
        "CSVM.UI.Boards.PauseScreens -> CSVM.UI.Menu.EscapeState",
        "CSVM.UI.Boards.PauseScreens -> CSVM.UI.Overlays.MissionMap",
        "CSVM.UI.Boards.PauseSheet -> CSVM.UI.Menu.BriefingReveal",
        "CSVM.UI.Boards.PauseSheet -> CSVM.UI.Menu.EscapeButton",
        "CSVM.UI.Boards.PauseSheet -> CSVM.UI.Menu.EscapeDialog",
        "CSVM.UI.Boards.PauseSheet -> CSVM.UI.Menu.EscapeObjectivesList",
        "CSVM.UI.Boards.PauseSheet -> CSVM.UI.Menu.EscapeShared",
        "CSVM.UI.Boards.PauseSheet -> CSVM.UI.Menu.EscapeState",
        "CSVM.UI.Boards.PauseSheet -> CSVM.UI.Screens.LoadScreens",
        "CSVM.UI.Boards.ScreenFlash -> CSVM.Flight.Camera.ViewerSet",
        "CSVM.UI.Menu.Original.OriginalAvailability -> CSVM.Session.Launch.ExtractionStamp",
        "CSVM.UI.Screens.ExtractionFlow -> CSVM.Session.Launch.ExtractionStamp",
        "CSVM.UI.Screens.ExtractionFlow -> CSVM.Session.Launch.StampStanding",
        "CSVM.UI.Screens.NoGameDataScreen -> CSVM.Session.Launch.ExtractionStamp",
    };

    [Fact]
    public void NoFamilyNamesAFamilyAboveIt()
    {
        var upward = Pairs()
            .Where(p => Rank(Namespace(p.To)) > Rank(Namespace(p.From)))
            .Where(p => !Exceptions.Any(e => Under(Namespace(p.From), e.From) && Under(Namespace(p.To), e.To)))
            .Select(p => $"{p.From} -> {p.To}")
            .ToHashSet(StringComparer.Ordinal);

        var added = upward.Where(p => Array.IndexOf(Allowed, p) < 0).OrderBy(p => p, StringComparer.Ordinal).ToList();
        var stale = Allowed.Where(p => !upward.Contains(p)).ToList();

        Assert.True(added.Count == 0, "New upward references:" + Lines(added));
        Assert.True(stale.Count == 0, "Allowlist entries the code no longer produces:" + Lines(stale));
        Assert.Equal(Allowed.OrderBy(p => p, StringComparer.Ordinal), Allowed);
    }

    [Fact]
    public void EveryNamespaceHasARank()
    {
        var namespaces = AssemblyDependencyScan.Namespaces(AssemblyPath())
            .Where(n => n == "CSVM" || n.StartsWith("CSVM.", StringComparison.Ordinal))
            .ToList();

        var unranked = namespaces.Where(n => Rank(n) < 0).ToList();
        var unused = Families.Where(f => !namespaces.Any(n => Under(n, f.Prefix))).Select(f => f.Prefix).ToList();

        Assert.True(unranked.Count == 0, "Namespaces with no row in the family table:" + Lines(unranked));
        Assert.True(unused.Count == 0, "Family rows that match no namespace:" + Lines(unused));
    }

    [Fact]
    public void WithinFamilyRulesHold()
    {
        var rules = new (string Rule, Func<string, bool> From, Func<string, bool> To)[]
        {
            ("Flight.Camera names only Flight.Airframe in Flight",
                n => Under(n, "CSVM.Flight.Camera"),
                n => Under(n, "CSVM.Flight") && !Under(n, "CSVM.Flight.Camera") && !Under(n, "CSVM.Flight.Airframe")),
            ("nothing else in Flight names Flight.Hangar",
                n => Under(n, "CSVM.Flight") && !Under(n, "CSVM.Flight.Hangar"),
                n => Under(n, "CSVM.Flight.Hangar")),
            ("nothing else in UI names UI.Labs",
                n => Under(n, "CSVM.UI") && !Under(n, "CSVM.UI.Labs"),
                n => Under(n, "CSVM.UI.Labs")),
            ("UI.Hangar names only UI.Boards and the shared UI.Menu in UI",
                n => Under(n, "CSVM.UI.Hangar"),
                n => Under(n, "CSVM.UI") && !Under(n, "CSVM.UI.Hangar") && !Under(n, "CSVM.UI.Boards") && n != "CSVM.UI.Menu"),
        };

        var pairs = Pairs();
        var broken = rules
            .SelectMany(r => pairs
                .Where(p => r.From(Namespace(p.From)) && r.To(Namespace(p.To)))
                .Select(p => $"{r.Rule}: {p.From} -> {p.To}"))
            .ToList();

        Assert.True(broken.Count == 0, "Within-family rules broken:" + Lines(broken));
    }

    private static string AssemblyPath() => Path.Combine(AppContext.BaseDirectory, "CSVM.dll");

    // Every CSVM type pair across two namespaces. A nested type, compiler-generated or not,
    // carries its owner's name before a slash, so both ends fold to the owner.
    private static List<(string From, string To)> Pairs() =>
        AssemblyDependencyScan.Violations(
                AssemblyPath(),
                ns => ns == "CSVM" || ns.StartsWith("CSVM.", StringComparison.Ordinal),
                name => name.StartsWith("CSVM.", StringComparison.Ordinal))
            .Select(v => v.Split(" -> ", 2, StringSplitOptions.None))
            .Select(v => (From: Owner(v[0]), To: Owner(v[1])))
            .Where(p => Namespace(p.From) != Namespace(p.To))
            .Distinct()
            .ToList();

    private static string Owner(string type)
    {
        int nested = type.IndexOf('/');
        return nested < 0 ? type : type[..nested];
    }

    private static string Namespace(string type)
    {
        int dot = type.LastIndexOf('.');
        return dot < 0 ? string.Empty : type[..dot];
    }

    private static int Rank(string ns) =>
        Families.Where(f => Under(ns, f.Prefix)).OrderByDescending(f => f.Prefix.Length).Select(f => f.Rank).DefaultIfEmpty(-1).First();

    private static bool Under(string ns, string prefix) =>
        ns == prefix || ns.StartsWith(prefix + ".", StringComparison.Ordinal);

    private static string Lines(IEnumerable<string> items) =>
        string.Concat(items.Select(i => Environment.NewLine + "    \"" + i + "\","));
}
