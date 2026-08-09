using System;
using System.Collections.Generic;
using CSVM.Effects;
using CSVM.Flight;
using CSVM.Mech3;
using CSVM.UI;
using Godot;

namespace CSVM.Session;

/// <summary>Loads and applies the flown mission's weather (<see cref="Build"/>), and drives its
/// per-frame rig state — the per-rig skydome/whiteout/deck update — in <see cref="Tick"/>.
/// Constructed once per session
/// (<c>_weatherRig</c> in <c>GameSession.StartSession</c>, same lifetime as
/// <see cref="LiveryResolver"/>/<see cref="SpawnPicker"/>/<see cref="WorldEffectsFactory"/>).
/// The horizon (skydome) build loop itself lives on <c>GameSession</c> — it is a
/// <c>SceneBuilder</c> concern, not weather state — so <see cref="Build"/> takes it as a callback
/// invoked between the zone resolving and the fog/whiteout/precip setup.
///
/// <para>⚠ The ambient cloud field is <b>not</b> weather state and is not built here — do not
/// re-add a hand-tuned per-rig cloud field keyed off <c>CLOUD_COVER</c>. It is the
/// chapter's authored <c>fogvol.zrd</c> clutter scattered through its <c>fvol*</c> volumes —
/// gamez + chapter-zrdr data, world-anchored and shared by every pane — built beside the world in
/// <c>GameSession</c> (<see cref="CSVM.Effects.FogVolumeClutter"/>).</para></summary>
public sealed class WeatherRig
{
    // ⚠ TUNE (C21/C25, 2026-08-08) — how far above the camera the deck hangs while the camera
    // is BELOW the cloud band. The original's deck is engine trickery, not a placed sheet: at
    // the controls its texture looks exactly the same at every altitude on the way up, which a
    // world-fixed sheet cannot do (it would grow and parallax), so below the band it is a
    // ceiling carried with the camera and this is the only free parameter left in it.
    //
    // Supersedes A7's K = 400 m, which fit apparent mottling scale against the wrong texture
    // period: A7 read the deck's "authored tile" as 1024 m, but the deck's authored UVs span
    // 0.5x0.5 per tile in a 2x2 checker (`models.json`, 144/144 in both C1 and C4), so
    // `cloudlayer.tif` actually repeats every 2048 m — the estimator locked onto the second
    // harmonic and its "1062 m vs 1024 m" self-check was a coincidence, not a validation. A7's
    // render is also heavily mip-blurred at grazing angles where the original is crisp
    // (`.scratch/c21/mottling-AB.png`), which under-counts zero-crossings and biases that
    // estimator's K upward on top of the period error.
    //
    // Re-derived (`C21`) from the same `OriginalScreenshots/C1 IA1 Fog river.png`, this time
    // against the ZONE'S OWN AUTHORED fog ramp rather than the texture: the deck tiles author
    // `fog: true` (not exempt), so a ceiling at height h should fade toward `FOG_COLOR` on
    // exactly the authored linear ramp, elevation e = f·h/d px above the horizon at ground
    // distance d. Fitting `L(e) = mix(L0, FOG_COLOR, clamp((f·h/e − 1000)/3000))` to the still's
    // near-horizon row-means with L0 left free (`.scratch/c21/fitfog.py`) returns
    // f·h = 80,000–91,500 px·m ⇒ K = 135–154 m, bias-corrected against the same fit run on our
    // own render at a KNOWN K = 400 (which recovers +14…+21 % high, and reproduces that render's
    // saturation elevation, f·400/4000 = 60 px, to sd 0) to a corrected 110–155 m bracket —
    // residual 1.0–1.3 luminance units rms over ~200 rows either way.
    //
    // This K also predicts the "sky stripe below the deck" B16 exposed: the sheet's rim sits at
    // f·K/6144 (half the 144-tile, 12,288 m span). At K = 400 that lands at row 321 = 39 px,
    // matching our own render's rim to the pixel (599.1×400/6144 = 39.0) — but the original's
    // frame plainly shows deck at that elevation (row 318, luminance 170.5), which only that
    // frame's own K resolves: at K = 135 the rim moves to 13 px, INSIDE the fog-saturated band
    // (completion at ≈17.5 px), so it can never be seen. No ceiling extension and no fog
    // exemption is needed — the stripe was K putting the rim past where this zone's own fog
    // already hides it; correct K and it closes itself (`C25`).
    //
    // K = 135 is the user's pick from that bracket (110–155), not a further measurement: 135 and
    // 400 were rendered side by side against the original still and the user chose 135
    // (`.scratch/c25/k-decision-montage.png`, 2026-08-08). Changing an approved look (A7's
    // 400 m carried "it looks a lot better. approved.") is a call the fit cannot make on its
    // own — the fit bounds the bracket, the user's eye picked the point inside it. Still one
    // constant for every deck chapter (A7): C1's river still is the only original frame that
    // can measure one; a per-chapter K needs a per-chapter still.
    private const float DeckCeilingHeight = 135f;

    // ⚠ TUNE, and the FALLBACK only — a mission that authors CLOUD_COVER colours overrides it
    // (WeatherState.WhiteoutColor). It survives because the three reachable-band chapters that
    // author nothing are measured right at this value: CAP-12 puts C1's in-cloud interior at 248
    // in the original against our 243. C1C and C2B share C1's colourless CLOUD_COVER
    // block. Do not "unify" it with the zone FOG_COLOR: C1's fog is 0.69 = 176, which would
    // darken a passing A/B by 70 units.
    private static readonly Color WhiteoutFallbackColor = new(0.95f, 0.95f, 0.96f);

    private readonly SessionSpec _spec;
    private readonly Node3D _worldRoot;
    // One rig's deck tiles and which variant they currently carry, keyed by the deck node's
    // instance id — per rig, because each rig flies its own copy of the deck (AssignCloudDecks)
    // through its own altitude regime.
    private readonly Dictionary<ulong, DeckLighting> _deckLighting = new();

    private WeatherState? _weather;
    private string _activeZone;
    private Precipitation? _precip;
    private Vector3 _deckCenter;
    // The deck tiles' undimmed meshes by dimmed-mesh RID (WorldBuilder.CloudDeckUndimmedMeshes).
    private IReadOnlyDictionary<Rid, ArrayMesh> _deckUndimmedMeshes = new Dictionary<Rid, ArrayMesh>();
    private bool _loggedDeckLighting;

    public WeatherRig(SessionSpec spec, Node3D worldRoot)
    {
        _spec = spec;
        _worldRoot = worldRoot;
        _activeZone = spec.SkyZone;
    }

    /// <summary>The deck's altitude regime for ONE camera: where its cloud-deck copy sits,
    /// whether that camera renders the two ambient cloud populations, and whether the deck
    /// carries the mission's SUNLIGHT dimming. Below the band's centre the deck is a ceiling
    /// carried with the camera, the clouds are hidden and the sheet is the overcast's dimmed
    /// UNDERSIDE; at or above it the deck is a world-fixed floor at the centre, the clouds
    /// render and the sheet is the undimmed top (A7 + C23).
    ///
    /// <para>Pure and public because it is the whole rule, and the rule is what has to be
    /// asserted: <see cref="Tick"/> only applies it once per rig. Two cameras on opposite sides
    /// of <paramref name="bandCentre"/> must get opposite answers from it — that is the
    /// splitscreen requirement, and it is a property of this function, not of the loop.</para>
    ///
    /// <para>⚠ <c>DeckDimmed</c> is C23's fork resolved (M-a, user 2026-08-09), and it is a
    /// REGIME rule rather than face-dependent lighting: the two regimes are two different
    /// objects, and C22's <c>csky_world_light</c> dimming is verified from below (the original's
    /// underside 167.7 against our 168.9) and contradicted from above (no pixel in any original
    /// above-band frame falls below <c>FOG_COLOR</c> 175, and a surface whose own colour is
    /// 168.9 can never render above it, fog being a pull TOWARD the fog colour).</para>
    ///
    /// <para>⚠ Both the deck altitude and the deck's lit-ness JUMP at the crossing. That is
    /// unobservable only because the crossing is the band centre, which is the middle of the
    /// fully-opaque whiteout core (<see cref="WeatherState.WhiteoutAmount"/>).</para></summary>
    public static (float DeckY, bool CloudsVisible, bool DeckDimmed) DeckRegime(
        float cameraY, float bandCentre)
    {
        bool below = cameraY < bandCentre;
        return (below ? cameraY + DeckCeilingHeight : bandCentre, !below, below);
    }

    /// <summary>Loads the mission's weather.json and resolves the rendered zone, builds the
    /// per-rig domes via <paramref name="buildDomes"/> (needs the resolved zone), then applies
    /// fog + whiteout + precipitation — the same order <c>StartSession</c> ran before the
    /// move.</summary>
    public void Build(string missionZrdrPath, IReadOnlyList<PlayerRig> rigs,
        IReadOnlyList<HorizonZone> horizonZones, Action<string> buildDomes)
    {
        LoadWeather(missionZrdrPath, horizonZones);
        buildDomes(_activeZone);
        SetupWeather(rigs);
    }

    /// <summary>The deck geometry's original AABB centre, so <see cref="Tick"/> can re-anchor it
    /// under each player every frame. Set separately from <see cref="Build"/> because the cloud
    /// deck is world geometry (<c>GameSession</c>'s <c>cloudDeck</c>), not weather state, and is
    /// built whenever a chapter world loads — not only when this rig itself gets built.</summary>
    public void SetDeckCenter(Vector3 center) => _deckCenter = center;

    /// <summary>The deck tiles' undimmed twin meshes
    /// (<c>WorldBuilder.CloudDeckUndimmedMeshes</c>), which is what lets <see cref="Tick"/> take
    /// the deck's SUNLIGHT dimming off above the cloud band (C23's <c>DeckDimmed</c>). Set from
    /// the same place as <see cref="SetDeckCenter"/>, for the same reason — the deck is world
    /// geometry built with the chapter, not weather state. An empty map (a chapter with no deck,
    /// or a world built before this existed) simply leaves the deck as built.</summary>
    public void SetDeckUndimmedMeshes(IReadOnlyDictionary<Rid, ArrayMesh> undimmed)
    {
        _deckUndimmedMeshes = undimmed;
        _deckLighting.Clear();
        _loggedDeckLighting = false;
    }

    /// <summary>Everything decided per *camera*, once per rig — one in single player, one per
    /// pane in splitscreen: re-centers the skydome, fades the cloud-band whiteout, places the
    /// cloud deck in its altitude regime, and gates the two ambient cloud populations by that
    /// camera's own altitude. The first three are per-rig NODES (each on that player's own
    /// visual layer); the last is a per-camera CULL MASK over one shared layer, because the
    /// cloud populations are world geometry no pane owns.
    ///
    /// <para>Every camera in the session comes through here, including the freecam/probe
    /// camera — <c>GameSession.BuildRigs</c> gives a single-player or spectator session one rig
    /// holding the main-viewport camera — so a scripted shot obeys the same altitude rules the
    /// player does.</para></summary>
    public void Tick(IReadOnlyList<PlayerRig> rigs)
    {
        foreach (var rig in rigs)
        {
            var camPos = rig.Camera.Position;

            // Keep the skydome centered on the camera in ALL axes (a pure zero-parallax
            // backdrop, like the original): the moon then stays at its designed 28° elevation
            // against the dark dome cap — whose color its painted background matches — instead
            // of sliding down into the bright horizon band as the plane climbs.
            // (One-frame lag vs the flight camera is invisible at 22 km.)
            if (rig.Horizon != null)
                rig.Horizon.Position = camPos;

            // Cloud-band whiteout: fade the overlay in as the camera altitude enters the band.
            if (rig.Whiteout != null && _weather != null)
            {
                // The colour is re-read per frame, not set once at build: where the authored pair
                // differs it lerps across the band with the camera (CLOUD_COVER, weather.md).
                var c = _weather.WhiteoutColor(camPos.Y) ?? WhiteoutFallbackColor;
                // --no-fog covers the whiteout too: flying into the cloud band would otherwise
                // still white the pane out, which reads as "fog is not actually off".
                c.A = _spec.NoFog ? 0f : _weather.WhiteoutAmount(camPos.Y);
                rig.Whiteout.Color = c;
            }

            // The deck is ENGINE TRICKERY, in two regimes split at the cloud band's centre
            // (A7, 2026-08-08 — the user's decode at the controls of the original):
            //
            //   camera BELOW the centre — a CEILING carried with the camera in all three axes,
            //     DeckCeilingHeight above it. Climbing, the sheet's texture then looks exactly
            //     the same at every altitude, which is what the original does and what a
            //     world-fixed sheet cannot do: that would grow and parallax as you close on it.
            //   camera AT/ABOVE the centre — a world-fixed FLOOR at the band centre, still
            //     following in X/Z so the sheet has no reachable edge. After a climb through the
            //     whiteout the original's sheet lies below at a fixed height ~ the band centre.
            //
            // ⚠ The flip is a JUMP of DeckCeilingHeight, and it is unobservable only because it
            // happens exactly at the band centre — the middle of the fully-opaque whiteout core
            // (C1: total in 1032–1062, WeatherState.WhiteoutAmount). Moving this altitude, or
            // thinning that core, makes a hard pop visible; if one ever shows, that is a finding
            // about the whiteout band, not a licence to move the flip.
            //
            // The two cloud POPULATIONS are gated on the same crossing, per camera, below —
            // from underneath, the original shows the bare sheet and no cloud groups at all.
            //
            // (History: until A6 this pinned Y to the band centre in BOTH regimes — the
            // above-band half of this trick applied everywhere — which buried the deck inside
            // the `fvol` slab and hung every sprite below it. A6 removed the pin; A7 restores it
            // for the regime it actually belongs to and gives the other regime its own rule.
            // The authored altitudes it is NOT using: the four deck chapters ship the deck ~10 m
            // below their `fvol1`–`fvol9` slab floor — C1 960/970.00, C1C 960/970.73,
            // C2B 960/970.00, C4 1050/1060.00.)
            if (rig.Deck != null && _weather is { HasCloudBand: true } weather)
            {
                (float deckY, bool cloudsVisible, bool deckDimmed) =
                    DeckRegime(camPos.Y, weather.CloudBandCentre);
                rig.Deck.Position = new Vector3(
                    camPos.X - _deckCenter.X,
                    deckY - _deckCenter.Y,
                    camPos.Z - _deckCenter.Z);
                // The same crossing takes the mission's SUNLIGHT dimming off the sheet: what the
                // ceiling regime shows is the overcast's underside, what the floor regime shows
                // is its top, and the original renders those at 167.7 and ~210 respectively
                // (C23). Beside the Y write because it IS the same flip — one rule, applied once
                // per rig, hidden by the same opaque whiteout core.
                SetDeckDimmed(rig.Deck, deckDimmed);
                // ⚠ Per-camera CULL MASK, never node visibility: in splitscreen two players can
                // sit on opposite sides of the band, and hiding the shared field as a node would
                // take it out of BOTH panes. Only a chapter with a deck gets gated at all — see
                // the guard in the `if` above: C5 has the clutter and a band at 9950–10150 m but
                // no deck, so an ungated rule would hide its street haze at street level for
                // ever.
                SetCloudFieldVisible(rig.Camera, cloudsVisible);
            }
        }
    }

    /// <summary>Adds or drops <see cref="SplitScreen.CloudFieldLayer"/> in one camera's cull
    /// mask — the ambient cloud field and the placed <c>cloudparent</c> clusters both live
    /// there, and this is the only thing that decides whether a given view renders them.</summary>
    private static void SetCloudFieldVisible(Camera3D camera, bool visible)
    {
        uint mask = camera.CullMask;
        uint want = visible
            ? mask | SplitScreen.CloudFieldLayer
            : mask & ~SplitScreen.CloudFieldLayer;
        // Written only on a change: CullMask is a property setter into the RenderingServer, and
        // this runs per camera per frame.
        if (want != mask)
        {
            camera.CullMask = want;
        }
    }

    // "zone2 0 meshes, zone1 3 meshes" — the evidence the pick was made on, not just its result.
    private static List<string> HorizonZoneCounts(IReadOnlyList<HorizonZone> horizonZones)
    {
        var counts = new List<string>();
        foreach (var z in horizonZones)
            counts.Add($"{z.Name} {z.MeshedNodes} meshes");
        return counts;
    }

    /// <summary>Puts ONE rig's deck copy into its regime's lit variant: the dimmed mesh each
    /// tile was built with (below the band), or its undimmed twin (above it). A per-INSTANCE
    /// mesh assignment, never a change to a shared material — the two variants are separate
    /// cached resources, so two splitscreen panes on opposite sides of the band can hold
    /// different ones at the same instant. That is the same requirement the cloud gate meets
    /// with a per-camera cull mask, met the same way: nothing here is global state.
    ///
    /// <para>The variants differ ONLY in which shader the surface picked (see
    /// <c>SceneBuilder.BuildMesh</c>'s <c>lit</c>): same vertices, same AABB, same instance
    /// uniforms, so the swap cannot move a pixel except through the brightness it exists to
    /// change. Written only on a change — a flight spends whole minutes in one regime.</para></summary>
    private void SetDeckDimmed(Node3D deck, bool dimmed)
    {
        ulong id = deck.GetInstanceId();
        if (!_deckLighting.TryGetValue(id, out var lighting))
        {
            _deckLighting[id] = lighting = CollectDeckTiles(deck);
        }
        if (lighting.Dimmed == dimmed)
        {
            return;
        }
        lighting.Dimmed = dimmed;
        foreach (var tile in lighting.Tiles)
        {
            tile.Instance.Mesh = dimmed ? tile.Dimmed : tile.Undimmed;
        }
    }

    /// <summary>Resolves one deck copy's tiles to swap, once: every <see cref="MeshInstance3D"/>
    /// under it whose mesh has a recorded undimmed twin. Deferred to the first
    /// <see cref="Tick"/> rather than done at build because the splitscreen copies are made
    /// after the world is built, and a copy's instances are its own nodes (they share the
    /// resources, which is exactly what makes the RID lookup find them).</summary>
    private DeckLighting CollectDeckTiles(Node3D deck)
    {
        // As built: WorldBuilder gives every deck tile `forceLit: true`, which is the ceiling
        // regime's variant, so a deck that never ticks keeps C22's look.
        var lighting = new DeckLighting { Dimmed = true };
        int instances = 0;
        Collect(deck);
        if (!_loggedDeckLighting)
        {
            _loggedDeckLighting = true;
            // Said out loud once per session: "0 of 144" is what a broken lookup looks like, and
            // it would otherwise be indistinguishable from a deck that is simply never above the
            // band (DIAG-15).
            GD.Print($"deck lighting: {lighting.Tiles.Count} of {instances} deck tile(s) carry an "
                     + "undimmed twin — the sheet keeps SUNLIGHT below the cloud band, drops it above");
        }
        if (lighting.Tiles.Count != instances)
        {
            GD.PushWarning($"deck lighting: {instances - lighting.Tiles.Count} deck tile(s) have no "
                           + "undimmed twin and will stay dimmed above the cloud band");
        }
        return lighting;

        void Collect(Node node)
        {
            // C26's rim extension is a MeshInstance3D under this same node (so it follows the
            // deck's regime Y and X/Z exactly as the tiles do — see WorldBuilder.AddDeckAnnulus)
            // but it is not itself a deck TILE and carries no undimmed twin to look up: it is
            // fully fog-saturated everywhere it renders, so the dimmed/undimmed swap this method
            // exists to drive would be an identity on it either way (verified, not assumed, in
            // PLAN-overcast-match C26). Skipped by the meta tag so it inflates neither `instances`
            // nor the "N of M" census below — "144 of 144" stays the deck TILE count, not
            // "144 of 145" with a spurious "1 tile has no undimmed twin" warning.
            if (node is MeshInstance3D mi && !mi.HasMeta(WorldBuilder.DeckExtensionMeta))
            {
                instances++;
                if (mi.Mesh is { } mesh && _deckUndimmedMeshes.TryGetValue(mesh.GetRid(), out var undimmed))
                {
                    lighting.Tiles.Add((mi, mesh, undimmed));
                }
            }
            foreach (var child in node.GetChildren())
            {
                Collect(child);
            }
        }
    }

    /// <summary>Resolves <see cref="_activeZone"/>: the zone the fog AND the skydome are both
    /// built from. Called before the domes, because the zone names are per chapter — C5 ships
    /// zone1+zone3, so the `zone2` default has to fall back or C5 renders with no fog and no dome
    /// at all. Two corrections, in this order, and the second one only applies to the DEFAULT
    /// request: the mission's own zone names (<c>ResolveZone</c>), then the chapter's horizon
    /// geometry — a request whose dome is a bare marker yields to the zone that has one
    /// (C1B/C2/C3; see <see cref="WeatherState.PreferPopulatedHorizonZone"/>). An explicit
    /// <c>--sky-zone=</c> is honoured as asked, empty dome and all: it is the flag for looking at
    /// a named zone, and the repro poses recorded in <c>analysis/</c> depend on it.
    /// Which zone a mission flies is in no reader file (docs/formats/weather.md), so where the
    /// geometry does not decide it, the choice is still the user's A/B against the original.</summary>
    private void LoadWeather(string missionZrdrPath, IReadOnlyList<HorizonZone> horizonZones)
    {
        _weather = WeatherState.Load(missionZrdrPath);
        string byFile = _weather?.ResolveZone(_spec.SkyZone) ?? _spec.SkyZone;
        _activeZone = _spec.SkyZoneExplicit
            ? byFile
            : _weather?.ResolveZone(_spec.SkyZone, horizonZones)
              ?? WeatherState.PreferPopulatedHorizonZone(byFile, horizonZones);
        if (!_activeZone.Equals(byFile, StringComparison.OrdinalIgnoreCase))
            // The horizon correction. Printed with the counts it was decided on, because this is
            // the one line that says which sky and which fog the flight actually got.
            GD.Print($"weather: {_spec.Chapter} builds no horizon geometry under '{byFile}' "
                     + $"({string.Join(", ", HorizonZoneCounts(horizonZones))}) — "
                     + $"rendering '{_activeZone}' sky and fog");
        if (_weather == null)
        {
            GD.PushWarning($"no weather.json for {_spec.Chapter}/{_spec.Mission} — flying without fog / whiteout");
            return;
        }
        if (!byFile.Equals(_spec.SkyZone, StringComparison.OrdinalIgnoreCase))
            // Not a fault: a chapter that numbers its zones differently resolves here every
            // flight. C5 (zone1/zone3) does so on all 8 missions, and zone1 is the confirmed
            // correct choice there — so this must not read as a missing-data warning.
            GD.Print($"weather: {_spec.Chapter}/{_spec.Mission} has no '{_spec.SkyZone}' "
                     + $"(zones: {string.Join("/", _weather.ZoneNames)}) — rendering '{_activeZone}'");
    }

    /// <summary>Applies the loaded weather: sets the distance-fog global shader parameters for
    /// the rendered sky zone (all world + aircraft surfaces pick them up), and builds the
    /// full-screen cloud-band whiteout overlay (its opacity is driven each frame from the camera
    /// altitude in <see cref="Tick"/>). No-op if the mission has no weather.json — the fog
    /// globals keep their registered no-op range. Call <see cref="LoadWeather"/> first.</summary>
    private void SetupWeather(IReadOnlyList<PlayerRig> rigs)
    {
        if (_weather == null)
            return;
        var fog = _weather.Fog(_activeZone);
        // FOG_COLOR is a DX7-era framebuffer (sRGB) value: the original's fully-fogged pixels
        // are exactly 0.69·255 = 176 gray (measured in OriginalScreenshots/"C1 IA1 Cloudcoverage
        // 1.png", flat regions std 0). The shader mixes ALBEDO in linear space, so convert —
        // feeding 0.69 in raw made saturated fog render as 216, a washed-out near-white.
        var fogLinear = fog.FogColor.SrgbToLinear();
        RenderingServer.GlobalShaderParameterSet("csky_fog_color",
            new Vector3(fogLinear.R, fogLinear.G, fogLinear.B));
        // The AUTHORED ranges ARE the ranges. A `fogRangeFactor = 2.0` used to halve them here
        // ("this does not seem to be radius but diameter"); it was a TUNE predating the fog-colour
        // sRGB fix and it is gone (PLAN-overcast-match B15, 2026-08-08). The data never supported
        // it: `VIEWING_RANGE` ships `FOG_SCALE 1.0` at HIGH detail in all eight chapters, and
        // every other multiplier in that block is <= 1 (MED 0.85, LOW 0.7), so nothing in the file
        // shortens a range at all. Measured: at the C1 river pose the halved range saturated the
        // overcast ceiling into flat fog far too close in, and the authored range pushes the
        // saturation out; C3's over-fogged canyon slope moves 178 -> 106 against the original's
        // 36.5 (still open, BL-321).
        // ⚠ The residual at the river pose was never this factor: the original's overcast ceiling
        // reads 166-175 in its own still while ours rendered 200-220 BEFORE any fog. That was the
        // deck's own underside brightness seen from below, and it is fixed — the deck now carries
        // the mission's SUNLIGHT below the band (WorldBuilder's forceLit) and measures 168-170
        // unfogged against the original's 167.7. Do not re-diagnose that pose as fog.
        // --no-fog pushes the range out of reach instead of touching `csky_fog_on`. That uniform
        // would work — every shader still honours it — but it is an INSTANCE uniform declared at
        // index 1 in SceneBuilder's shader and index 0 in Clutter's, and Godot merges that mapping
        // per GeometryInstance3D. Writing it would make a latent index mismatch live (the
        // unfogged-hilltops bug; see Clutter.ShaderCode's comment). `csky_fog_range` is
        // a GLOBAL uniform every fogged shader reads, so one write covers the world, the clutter
        // sprites, the solid city blocks and the dome with no ordering hazard at all.
        var fogRange = _spec.NoFog
            ? new Vector2(1e8f, 1e9f)   // same no-op range Weather.NoFog uses
            : new Vector2(fog.FogNear, fog.FogFar);
        RenderingServer.GlobalShaderParameterSet("csky_fog_range", fogRange);
        // FOG_ALTITUDE: the fog cylinder's vertical extent — full fog below FogLow, fading to
        // none at FogHigh (FRAGMENT altitude, settled in C2 at the controls of the original —
        // see csky_atmosphere.gdshaderinc and weather.md).
        RenderingServer.GlobalShaderParameterSet("csky_fog_alt", new Vector2(fog.FogLow, fog.FogHigh));
        // World brightness from the zone's SUNLIGHT (see WeatherState.WorldLight): the original
        // dims the baked-vertex world by the mission's ambient+diffuse; we apply it as a scalar
        // on the fullbright world/deck/dome (the fog color, set above, is unaffected — it mixes
        // in after). 1.0 for bright/day missions, < 1 for overcast/night.
        // Apply the dimming in GAMMA space (the DX7 chain texel×vtx×light is all sRGB-space),
        // consistent with the gamma-space vertex modulate: the shader multiplies LINEAR ALBEDO,
        // so feed the linearised factor — linear_ALBEDO · srgbToLinear(f) == gamma-space · f.
        // (Applied in linear space, 0.80 only reaches 210→190; gamma-space lands the deck 210→169.)
        float worldLightLinear = new Color(fog.WorldLight, fog.WorldLight, fog.WorldLight).SrgbToLinear().R;
        RenderingServer.GlobalShaderParameterSet("csky_world_light", worldLightLinear);
        GD.Print($"weather [{_activeZone}]{(_spec.NoFog ? " --no-fog: fog + whiteout OFF, world light unchanged;" : ":")} " +
                 $"fog {fog.FogColor.R:0.00} gray {fogRange.X:0}–{fogRange.Y:0} m " +
                 $"(authored {fog.FogNear:0}–{fog.FogFar:0}), " +
                 $"altitude {fog.FogLow:0}–{fog.FogHigh:0} m; world light {fog.WorldLight:0.00}; " +
                 $"cloud band {_weather.CloudBottom:0}–{_weather.CloudTop:0} m (±{_weather.CloudThickness:0})");

        if (_weather.HasCloudBand)
        {
            // The whiteout overlay follows *a* camera, so each rig gets its own: in splitscreen
            // it must dim only the pane whose player is inside the cloud. (The ambient cloud
            // field is NOT here — it is world-anchored authored geometry every pane shares, built
            // with the world; see Effects/FogVolumeClutter.)
            foreach (var rig in rigs)
            {
                // A pane-filling overlay so the whiteout swallows everything (terrain, plane,
                // clouds) uniformly, like the original. Layer 0 keeps it behind the HUD (layer 1).
                var canvas = new CanvasLayer { Layer = UI.HudLayers.WorldOverlay, Name = "whiteout" };
                rig.Whiteout = new ColorRect
                {
                    Color = new Color(
                        _weather.WhiteoutColor(_weather.CloudBottom) ?? WhiteoutFallbackColor, 0f),
                    MouseFilter = Control.MouseFilterEnum.Ignore,
                };
                rig.Whiteout.SetAnchorsPreset(Control.LayoutPreset.FullRect);
                canvas.AddChild(rig.Whiteout);
                rig.HudParent.AddChild(canvas);
            }
        }

        // Precipitation (rain/snow) — only the missions whose weather.json carries a TYPE block
        // get a field (C4 snow, C1C/C2B rain). It shows only below the CLOUD_COVER band (the
        // rain falls from the cloud base — none above the overcast). Self-animating from the
        // csky_time global + the camera built-ins, so it needs no _Process driving — that uniform
        // is the only handle on it, which is why a halted clock still stops the fall.
        _precip = Precipitation.Create(_weather.Precip, _weather.CloudBottom, _weather.CloudTop);
        if (_precip != null)
            _worldRoot.AddChild(_precip);
    }

    /// <summary>ONE rig's deck copy, resolved for the lit-variant swap: its tiles with both
    /// meshes each, and which variant they are carrying now (null until the first
    /// <see cref="Tick"/> decides). Per deck copy, not per session — see
    /// <see cref="SetDeckDimmed"/>.</summary>
    private sealed class DeckLighting
    {
        public List<(MeshInstance3D Instance, Mesh Dimmed, Mesh Undimmed)> Tiles { get; } = new();

        public bool? Dimmed { get; set; }
    }
}
