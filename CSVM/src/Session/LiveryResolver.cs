using System;
using System.Collections.Generic;
using CSVM.Mech3;
using CSVM.Utils;
using Godot;

namespace CSVM.Session;

/// <summary>Resolves which livery each player flies: the shipped paint catalog, the per-pattern
/// region-mask library, and the per-player scheme pick that reads them against a
/// <see cref="SessionSpec"/>'s <c>--paint=</c>/<c>--paint-color=</c>/<c>--paint-decal=</c>
/// overrides. Constructed once per session; caches the catalog and
/// library across the calls that build every player's plane.</summary>
public sealed class LiveryResolver
{
    private readonly SessionSpec _spec;
    private readonly string _rofPath;
    private List<PaintScheme>? _paintCatalog;
    private PatternLibrary? _patternLibrary;

    public LiveryResolver(SessionSpec spec, string rofPath)
    {
        _spec = spec;
        _rofPath = rofPath;
    }

    /// <summary>The original's per-pattern paint region masks, scanned once per session from
    /// the extracted UI archive. Empty (and a one-line note) when ExtractRof.ps1 has not been
    /// run — aircraft then build unpainted rather than failing.</summary>
    public PatternLibrary Patterns => _patternLibrary ??= PatternLibrary.Load(_rofPath);

    /// <summary>The 12 named schemes shipped in vehicle.json, loaded once per session.
    /// Empty on a read failure — paint is cosmetic and must never block a build.</summary>
    public List<PaintScheme> PaintCatalog(string zrdrPath)
    {
        if (_paintCatalog != null)
            return _paintCatalog;
        try
        {
            _paintCatalog = PaintScheme.LoadCatalog(zrdrPath);
            GD.Print($"[paint] {_paintCatalog.Count} shipped patterns: "
                + string.Join(", ", _paintCatalog.ConvertAll(s => s.Pattern)));
        }
        catch (Exception e)
        {
            GD.Print($"[paint] vehicle.json paint catalog unavailable ({e.Message}) — flying unpainted");
            _paintCatalog = new List<PaintScheme>();
        }
        return _paintCatalog;
    }

    /// <summary>The patterns this aircraft has masks for — the list the original's paint UI
    /// offers for that plane. Empty when the model carries no skin prefix to key on.</summary>
    public List<string> PatternsForPlane(GameZ planesGamez, string planeNode)
    {
        var root = planesGamez.FindByName(planeNode);
        var prefix = root != null ? PlanePainter.PrefixFor(planesGamez, root) : null;
        return prefix != null ? Patterns.PatternsFor(prefix) : new List<string>();
    }

    /// <summary>The livery player <paramref name="index"/> flies, or null to build the
    /// shipped unpainted skins. <paramref name="randomByDefault"/> is set for flight modes,
    /// where every player gets a fresh random livery on each map load unless --paint says
    /// otherwise; static views default to unpainted.</summary>
    public PaintScheme? SchemeFor(int index, string zrdrPath, bool randomByDefault, RandomNumberGenerator rng,
        IReadOnlyList<string>? available = null)
    {
        // --paint= takes one name per player like --plane=; the last covers any remainder.
        string? name = _spec.PaintNames is { Count: > 0 }
            ? _spec.PaintNames[Math.Min(index, _spec.PaintNames.Count - 1)]
            : null;

        // Resolve the no-paint cases before touching vehicle.json, so an unpainted static
        // view does no extra work and logs nothing (it is the pre-paint behaviour verbatim).
        if (string.Equals(name, "none", StringComparison.OrdinalIgnoreCase))
            return null;
        if (name == null && !randomByDefault)
            return null;

        var catalog = PaintCatalog(zrdrPath);
        PaintScheme? scheme;
        if (name == null || string.Equals(name, "random", StringComparison.OrdinalIgnoreCase))
        {
            // A pattern is per aircraft, so a random livery draws from the ones THIS plane
            // actually has masks for — picking one it does not carry would paint nothing.
            scheme = PaintScheme.Random(rng, catalog, available);
        }
        else
        {
            // Named: take the catalog's canonical colours when vehicle.json knows the pattern,
            // else a bare scheme (BROADWAY/ITSTAXI ship masks but no vehicle def names them).
            var known = catalog.Find(s => string.Equals(s.Pattern, name, StringComparison.OrdinalIgnoreCase)
                                       || string.Equals(s.FolderName, name, StringComparison.OrdinalIgnoreCase));
            bool haveMasks = available == null || available.Count == 0
                || ContainsPattern(available, name)
                || (known != null && ContainsPattern(available, known.FolderName));
            if (known == null && !haveMasks)
            {
                var offer = available is { Count: > 0 } ? string.Join(", ", available) : "(no pattern library)";
                GD.Print($"[paint] unknown pattern '{name}' — this aircraft has: {offer}, random, none");
                return null;
            }
            scheme = known ?? new PaintScheme { Pattern = name };
            if (!haveMasks)
                GD.Print($"[paint] pattern '{name}' ships no skins for this aircraft — "
                    + $"it has: {string.Join(", ", available!)}; painting decals only");
        }

        // An explicit colour/decal list overrides whatever the scheme brought, so a single
        // colour can be dialled in against a chosen pattern.
        if (scheme != null && (_spec.PaintColorOverride != null || _spec.PaintDecalOverride != null))
        {
            scheme = new PaintScheme
            {
                Pattern = scheme.Pattern,
                Color1 = _spec.PaintColorOverride?[0] ?? scheme.Color1,
                Color2 = _spec.PaintColorOverride?[1] ?? scheme.Color2,
                Color3 = _spec.PaintColorOverride?[2] ?? scheme.Color3,
                NoseDecal = _spec.PaintDecalOverride?[0] ?? scheme.NoseDecal,
                TailDecal = _spec.PaintDecalOverride?[1] ?? scheme.TailDecal,
                WingDecal = _spec.PaintDecalOverride?[2] ?? scheme.WingDecal,
            };
        }
        return scheme;
    }

    /// <summary>The RNG the session's random liveries draw from: the master seed's paint stream,
    /// so an unpinned launch repaints the field and a pinned one repeats it. --paint-seed=N
    /// overrides the derived seed, pinning liveries alone in an otherwise random run.</summary>
    public RandomNumberGenerator NewPaintRng() => new()
    {
        Seed = _spec.PaintSeedExplicit ? _spec.PaintSeed : Rng.SeedFor(Rng.Paint),
    };

    private static bool ContainsPattern(IReadOnlyList<string> list, string name)
    {
        foreach (var p in list)
            if (string.Equals(p, name, StringComparison.OrdinalIgnoreCase))
                return true;
        return false;
    }
}
