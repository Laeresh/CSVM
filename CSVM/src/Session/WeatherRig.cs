using System;
using System.Collections.Generic;
using CSVM.Effects;
using CSVM.Flight;
using CSVM.Mech3;
using CSVM.Utils;
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
/// <c>GameSession</c> (<see cref="CSVM.Effects.FogVolumeClutter"/>).</para>
///
/// <para><see cref="Tick"/> also publishes each rig's own
/// <see cref="WeatherState.CameraWeatherState"/> onto <c>PlayerRig.CameraWeatherState</c>
/// (<c>PLAN-weather-decompile-match</c> A2) — the binary's per-frame camera zone 1/2/3, fed by
/// this mission's <see cref="WeatherState"/> plus <see cref="SetFogVolumes"/>'s chapter data — and
/// re-applies the FOG globals for that state's zone whenever it changes (B11, see
/// <see cref="FogStateTrigger"/>): below the deck a deck chapter wears <c>ZONE1</c>'s ranges,
/// altitude, colour and <c>SUNLIGHT</c>, above it <c>ZONE2</c>'s. The matching DOME follows the
/// same state (B14): <c>GameSession</c>'s build callback puts one dome per gateable horizon zone
/// under each rig (<c>WorldBuilder.DomeZonesToBuild</c>) and <see cref="Tick"/> shows the one whose
/// <c>zone_id</c> matches — so below the deck a deck chapter draws <c>horizon/zone1</c>, whose own
/// camera-anchored, UV-scrolled geometry IS the overcast ceiling the player flies under.</para>
///
/// <para><b>This class owns render visibility for the whole world</b> as of B12
/// (<c>PLAN-weather-decompile-match</c>): <see cref="Tick"/> narrows each camera's cull mask to
/// that camera's own weather state (<see cref="CSVM.Mech3.ZoneGate"/>), which is the original's
/// <c>FUN_0056c430</c> gate, and switches the rig's private deck and dome copies with
/// <c>Node3D.Visible</c> under the same rule. It SUBSUMED the hand-rolled altitude gate over the
/// two ambient cloud populations that <see cref="DeckRegime"/> used to drive (A7) — that rule was
/// this gate's <c>zone_id 2</c> special case, and there is now exactly one owner of "does this
/// draw". <see cref="DeckRegime"/> keeps only the deck's PLACEMENT — the tiles' own authored
/// altitude at every camera altitude (B13/B14, <see cref="SetDeckAltitude"/>) — and its lit
/// variant.</para></summary>
public sealed class WeatherRig
{
    // ⚠ RETIRED (B14, 2026-08-09) — `DeckCeilingHeight`, the height at which the deck sheet used
    // to hang above a below-band camera as the overcast CEILING. There is no such mechanism: the
    // deck tiles are ordinary world meshes on `zone_id 2`, the original culls them outright below
    // the deck (B12), and what the player sees overhead there is `horizon/zone1`'s own
    // camera-anchored, UV-scrolled dome. The deck is now world-fixed at its authored altitude in
    // BOTH regimes and nothing carries it anywhere.
    //
    // The constant's own history is kept because its MEASUREMENT is what died, not just its use,
    // and the next reader must not re-fit it: A7 read K = 400 m from apparent mottling scale
    // against a texture period that was wrong by a factor of two; C21/C25 re-fit it to 110–155 m
    // (user's pick: 135) from the fog ramp on `OriginalScreenshots/C1 IA1 Fog river.png`, i.e. by
    // assuming the surface overhead FOGS. It does not. Every horizon model in every chapter is
    // authored `fog: false` (WorldBuilder.BuildHorizon's own note), so the original's below-deck
    // ceiling is unfogged geometry and a fog-ramp fit against it measures nothing — which is also
    // the standing explanation for `PLAN-overcast-match` B15's "ceiling texture survives to
    // ~12.6 km" anomaly. Both K estimates are therefore instruments pointed at the wrong surface.
    //
    // ⚠ The one live consumer of the number was C26's rim arithmetic (`WorldBuilder.AddDeckAnnulus`
    // — half-span 20,480 m so the below-band rim lands inside the fog-saturated band). That
    // geometry is UNCHANGED: it is still what keeps the ABOVE-band floor's edge out of frame, and
    // re-deriving it is not this item's to do.

    // ⚠ TUNE, and the FALLBACK only — a mission that authors CLOUD_COVER colours overrides it
    // (WeatherState.WhiteoutColor). It survives because the three reachable-band chapters that
    // author nothing are measured right at this value: CAP-12 puts C1's in-cloud interior at 248
    // in the original against our 243. C1C and C2B share C1's colourless CLOUD_COVER
    // block. Do not "unify" it with the zone FOG_COLOR: C1's fog is 0.69 = 176, which would
    // darken a passing A/B by 70 units.
    private static readonly Color WhiteoutFallbackColor = new(0.95f, 0.95f, 0.96f);

    // D32: one flicker per rig, keyed like `_lastLoggedVolumeWhiteout` — the original's
    // `_DAT_0064efcc`/`_DAT_0062154c` pair is a single global, but that assumes one camera, and
    // splitscreen panes on opposite sides of the band must not share a drift phase.
    private readonly Dictionary<int, BandFlicker> _bandFlicker = new();

    private readonly SessionSpec _spec;
    private readonly Node3D _worldRoot;
    // B6: the seam every puffer in the session reads its wind through. Owned by GameSession (which
    // has to hand it to the emitter factories long before this rig exists), written here because
    // this is the class that holds the mission's weather and already ticks once per frame.
    private readonly EffectAmbience _ambience;
    // One rig's deck tiles and which variant they currently carry, keyed by the deck node's
    // instance id — per rig, because each rig flies its own copy of the deck (AssignCloudDecks)
    // through its own altitude regime.
    private readonly Dictionary<ulong, DeckLighting> _deckLighting = new();
    // B13 verification: the deck Y last applied to each rig's OWN deck copy (keyed by instance
    // id, same key SetDeckDimmed uses), so Tick can log a change rather than every frame — the
    // probe evidence for "authored altitude, not the band centre" this item's Verify asks for.
    private readonly Dictionary<ulong, float> _lastLoggedDeckY = new();
    // C21 verification: per rig, the last in-volume whiteout density logged — so a probe's log says
    // the curtain engaged and how hard (METHOD-15), without a line every frame. Only the crossings
    // of the 0/non-0 boundary and moves of 0.05 or more are said out loud.
    private readonly Dictionary<int, float> _lastLoggedVolumeWhiteout = new();

    private WeatherState? _weather;
    private string _activeZone;
    private Precipitation? _precip;
    private Vector3 _deckCenter;
    // The deck tiles' own authored altitude (WorldBuilder.CloudDeckAltitude, B13) — where the
    // above-band regime now places the deck floor, world-fixed, instead of the band centre.
    private float _deckAltitude;
    // The deck tiles' undimmed meshes by dimmed-mesh RID (WorldBuilder.CloudDeckUndimmedMeshes).
    private IReadOnlyDictionary<Rid, ArrayMesh> _deckUndimmedMeshes = new Dictionary<Rid, ArrayMesh>();
    private bool _loggedDeckLighting;
    // The chapter's fog-volume census + whether its fogvol.zrd arms fog_zone (A2) — the two
    // inputs CameraWeatherState's state-3 test needs. Set separately from Build for the same
    // reason as _deckCenter: this is chapter/world data (GameSession's fogVolumes census), not
    // mission weather, built once beside the world rather than per rig. Empty/false by default,
    // which simply never resolves to state 3 (most chapters, and every chapter before this is
    // wired up).
    private IReadOnlyList<FogVolumeBox> _fogVolumes = Array.Empty<FogVolumeBox>();
    // C21: the same chapter data read as the in-volume WHITEOUT — the two decompiled ramps, the
    // union and the authored fog_color (FogVolumeWhiteout). Disarmed everywhere but C5, where it
    // costs nothing: Density short-circuits on the flag before touching a volume.
    private FogVolumeWhiteout _fogWhiteout = FogVolumeWhiteout.Disarmed;
    // B11's edge trigger: which zone's fog globals are live, and the state they were applied for.
    // Rebuilt by LoadWeather (a new mission is a new zone table); disarmed outright by an explicit
    // --sky-zone, per Decision 5.
    private FogStateTrigger _fogState = new(stateDriven: false, buildZone: string.Empty);
    // B12's gate, for the one subtree SceneBuilder does NOT stamp with a zone layer because it is
    // a per-rig camera-anchored copy: the deck tiles' own gamez zone_id
    // (WorldBuilder.CloudDeckZoneId). -1 = ungated, which is what a chapter with no deck and a
    // legacy extraction both give, and it simply keeps the node visible at every state. The domes
    // are the other such subtree and carry their zone ids per dome, on the rig
    // (PlayerRig.HorizonDomes, B14) — there is one per built horizon zone, not one per rig.
    private int _deckZoneId = -1;

    // B6: the mission's global wind — the WIND block's static vector plus its random-walk gust
    // (Effects.WorldWind, FUN_0054ee10). Still air until LoadWeather reads a weather.json, and
    // still air for a mission that has none.
    private WorldWind _wind = WorldWind.Still();

    public WeatherRig(SessionSpec spec, Node3D worldRoot, EffectAmbience? ambience = null)
    {
        _spec = spec;
        _worldRoot = worldRoot;
        _activeZone = spec.SkyZone;
        _ambience = ambience ?? new EffectAmbience();
    }

    /// <summary>The deck's regime for ONE camera: where its cloud-deck copy sits, and whether the
    /// deck carries the mission's SUNLIGHT dimming. The deck is a world-fixed sheet at
    /// <paramref name="authoredY"/> — the tiles' OWN authored altitude
    /// (<c>WorldBuilder.CloudDeckAltitude</c>: C1/C1C/C2B 960, C4 1050) — at every camera
    /// altitude; below <paramref name="bandCentre"/> what faces the camera is the overcast's
    /// dimmed UNDERSIDE, at or above it the undimmed top (C23).
    ///
    /// <para>⚠ <b>The Y is no longer a regime at all (B14, 2026-08-09).</b> The below-band branch
    /// used to hang the sheet at <c>camera.y + DeckCeilingHeight</c> as the overcast ceiling —
    /// A7's decode of the behaviour, and the right behaviour, but the wrong object. The tiles are
    /// ordinary world meshes carrying <c>zone_id 2</c>; nothing in the decompile moves them, the
    /// original culls them outright below the deck, and the ceiling the player sees there is
    /// <c>horizon/zone1</c>'s own camera-anchored dome (<c>WorldBuilder.DomeZonesToBuild</c>,
    /// <see cref="Tick"/>'s dome gate). So the deck stops moving and the <c>DeckCeilingHeight</c>
    /// TUNE is retired outright — see the note at the top of this file for why BOTH of its fits
    /// (A7's 400 m, C21/C25's 135 m) measured the wrong surface.</para>
    ///
    /// <para>The dimming flip stays at <paramref name="bandCentre"/> rather than at
    /// <paramref name="authoredY"/>, which is the physically obvious divider: the two are 87 m
    /// apart in C1, the whole interval sits inside the opaque whiteout core, and the sheet is
    /// culled for the entire below-band half anyway — so moving it would re-decide an approved
    /// look (C23's M-a) on no evidence at all.</para>
    ///
    /// <para>⚠ It no longer answers whether the two ambient cloud populations RENDER. That third
    /// member was A7's hand-rolled altitude gate, and B12 replaced it with the original's own
    /// <c>zone_id</c> gate (<see cref="CSVM.Mech3.ZoneGate"/>), of which it was the
    /// <c>zone_id 2</c> special case — read per chapter from the data rather than from an
    /// altitude, which is what lets C2B's <c>zone_id −1</c> fog volumes keep rendering below its
    /// deck where the altitude rule hid them. Do not re-add it here: two owners of one visibility
    /// question is the failure this item removed.</para>
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
    /// <para>⚠ The deck's lit-ness still JUMPS at the crossing. That is unobservable only because
    /// the crossing is the band centre, which is the middle of the fully-opaque whiteout core
    /// (<see cref="WeatherState.WhiteoutAmount"/>).</para></summary>
    public static (float DeckY, bool DeckDimmed) DeckRegime(float cameraY, float bandCentre, float authoredY)
        => (authoredY, cameraY < bandCentre);

    /// <summary>The trailing zone number of a <c>zone1</c>/<c>zone2</c>/<c>zone3</c> name, or null
    /// for anything else — the <c>--sky-zone</c> STATE OVERRIDE (Decision 5). An explicit
    /// <c>--sky-zone=zoneN</c> forces the <see cref="CSVM.Mech3.ZoneGate"/> to state N as well as
    /// pinning the fog: the flag exists so an inspection pose reproduces, and a pose whose fog
    /// said <c>zone2</c> while its CONTENT was culled for state 1 would not be that. Public and
    /// static because it is the rule, not a helper.</summary>
    public static int? ZoneNumberOf(string zoneName)
    {
        if (zoneName.Length == 0)
            return null;
        int n = zoneName[^1] - '0';
        return n >= 1 && n <= ZoneGate.MaxZoneId ? n : null;
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

    /// <summary>The chapter's fog-volume census (<c>FogVolumeSpec.VolumesOf</c>) plus its parsed
    /// <c>fogvol.zrd</c> — set separately from <see cref="Build"/> for the same reason as
    /// <see cref="SetDeckCenter"/>: this is chapter/world data (<c>GameSession</c>'s own fog-volume
    /// load), not mission weather. Two consumers, both keyed on the file's <c>fog_zone</c> arm bit
    /// (<see cref="FogVolumeSpec.FogZoneArmed"/>, C5 alone in the install):
    /// <see cref="Tick"/>'s per-camera <see cref="WeatherState.CameraWeatherState"/> call (A2) and
    /// its in-volume whiteout (<see cref="FogVolumeWhiteout"/>, C21). Never called — or called with
    /// a null/disarmed spec — keeps every camera at state 1/2 and every frame's volume whiteout at
    /// 0.</summary>
    public void SetFogVolumes(IReadOnlyList<FogVolumeBox> volumes, FogVolumeSpec? spec)
    {
        _fogVolumes = volumes;
        _fogWhiteout = FogVolumeWhiteout.From(spec, volumes);
    }

    /// <summary>The cloud deck's own gamez <c>zone_id</c> (<c>WorldBuilder.CloudDeckZoneId</c>) —
    /// the one piece of B12's gate that cannot ride a visual layer, because the deck is a per-rig
    /// camera-anchored copy. Set from the same place as <see cref="SetDeckCenter"/>, for the same
    /// reason: the deck is world geometry built with the chapter, not mission weather. Never
    /// called leaves the deck at −1, i.e. drawn at every state — the pre-B12 behaviour.</summary>
    public void SetDeckZoneId(int zoneId) => _deckZoneId = zoneId;

    /// <summary>The deck tiles' own AUTHORED altitude (<c>WorldBuilder.CloudDeckAltitude</c> —
    /// C1/C1C/C2B 960, C4 1050), read off the built data. Set from the same place as
    /// <see cref="SetDeckCenter"/>, for the same reason: the deck is world geometry built with
    /// the chapter, not mission weather.
    ///
    /// <para><c>PLAN-weather-decompile-match</c> B13: above the cloud band <see cref="Tick"/>
    /// places the deck HERE, world-fixed, instead of re-pinning it to
    /// <see cref="WeatherState.CloudBandCentre"/> (A7's pin, which was C4's own coincidence — its
    /// authored altitude equals its band centre, 1050, so C4 renders unchanged by this item; C1's
    /// 960 vs its former 1047 pin is an 87 m drop). Never called leaves the deck at 0, which only
    /// matters for a chapter with no deck — <c>rig.Deck</c> is null there and this value is
    /// unread.</para></summary>
    public void SetDeckAltitude(float altitude) => _deckAltitude = altitude;

    /// <summary>Everything decided per *camera*, once per rig — one in single player, one per
    /// pane in splitscreen: re-centers the skydome, fades the cloud-band whiteout, places the
    /// cloud deck in its altitude regime, and applies the <see cref="CSVM.Mech3.ZoneGate"/> for
    /// that camera's own weather state. The first three are per-rig NODES (each on that player's
    /// own visual layer); the gate is a per-camera CULL MASK over three shared zone layers,
    /// because the content it hides is world geometry no pane owns — plus <c>Node3D.Visible</c> on
    /// this rig's own deck and dome copies, which are the two things that ARE per pane.
    ///
    /// <para>Every camera in the session comes through here, including the freecam/probe
    /// camera — <c>GameSession.BuildRigs</c> gives a single-player or spectator session one rig
    /// holding the main-viewport camera — so a scripted shot obeys the same altitude rules the
    /// player does.</para></summary>
    public void Tick(IReadOnlyList<PlayerRig> rigs)
    {
        // D32's rate input: null only outside a running session (GameClock.Current unset), which
        // freezes the flicker rather than pacing it off a wall clock (DET-1) — nothing calls Tick
        // there anyway.
        float frameDt = GameClock.Current?.FrameDt ?? 0f;

        // B6 — the mission's global wind, stepped ONCE per frame and before anything reads it,
        // exactly where FUN_0054ee10 steps it: at the head of the tick, ahead of every emitter and
        // every particle. Outside the rig loop deliberately — the original has one wind for the
        // world, not one per camera, and stepping it per rig would make a splitscreen session's
        // gust walk twice as fast as a single-player one.
        _wind.Step(frameDt);
        _ambience.SetWind(_wind.Velocity);

        // C7 — the camera pose the puffer distance fade measures against, published on the same
        // seam and in the same place, for the same reason: it is world state an emitter READS.
        // Player 1's camera, not one per pane — see EffectAmbience.SetCamera for why, and note
        // that a single-player, spectator or freecam session has exactly one rig anyway, so this
        // is the only camera there is on every path a capture takes.
        if (rigs.Count > 0)
        {
            var fadeCam = rigs[0].Camera;
            var camXform = fadeCam.GlobalTransform;
            _ambience.SetCamera(camXform.Origin, -camXform.Basis.Z);
        }

        foreach (var rig in rigs)
        {
            var camPos = rig.Camera.Position;

            // A2's plumbing: the binary's per-frame camera weather state (1/2/3, FUN_0042ee40),
            // published on the rig. B11 consumes it for fog, below the loop. Logged only on a
            // change, at debug verbosity, since a flight spends whole minutes in one state.
            if (_weather != null)
            {
                int state = _weather.CameraWeatherState(camPos, _fogWhiteout.Armed, _fogVolumes);
                if (state != rig.CameraWeatherState)
                {
                    Log.Debug("world",
                        $"camera weather state: player {rig.Index} {rig.CameraWeatherState} -> {state}");
                    rig.CameraWeatherState = state;
                }
            }

            // Keep the skydome centered on the camera in ALL axes (a pure zero-parallax
            // backdrop, like the original): the moon then stays at its designed 28° elevation
            // against the dark dome cap — whose color its painted background matches — instead
            // of sliding down into the bright horizon band as the plane climbs.
            // (One-frame lag vs the flight camera is invisible at 22 km.)
            if (rig.Horizon != null)
                rig.Horizon.Position = camPos;

            // The whiteout overlay, and it carries TWO sources on one surface (C21, Decision 6):
            // the CLOUD_COVER band's altitude whiteout, and — where the chapter arms fog_zone —
            // the fvol volumes' own approach/interior curtain. The original computes ONE
            // camera-space density per frame and blends the frame with it; it never builds
            // per-volume fog meshes, which is why both live here rather than in the world.
            if (rig.Whiteout != null && _weather != null)
            {
                // The colour is re-read per frame, not set once at build: where the authored pair
                // differs it lerps across the band with the camera (CLOUD_COVER, weather.md).
                var c = _weather.WhiteoutColor(camPos.Y) ?? WhiteoutFallbackColor;
                // --no-fog covers the whiteout too: flying into the cloud band would otherwise
                // still white the pane out, which reads as "fog is not actually off". It covers
                // the volume curtain for the same reason.
                float band = _spec.NoFog ? 0f : _weather.WhiteoutAmount(camPos.Y);
                // D32: the decompiled in-cloud flicker — only while the band opacity sits strictly
                // inside (0,1), exactly the binary's `*pfVar5 != 0.0 && *pfVar5 != 1.0` guard. The
                // volume curtain (C21) is untouched: it never shares this surface with the band in
                // shipped data (Decision 6), and the binary's remap block only ever touches the
                // band's own opacity.
                if (band > 0f && band < 1f)
                    band = ApplyBandFlicker(rig.Index, band, frameDt);
                float volume = _spec.NoFog ? 0f : _fogWhiteout.Density(camPos);
                if (volume > 0f)
                {
                    // ⚠ The two NEVER coexist in shipped data — C5 is the only chapter arming
                    // fog_zone and its CLOUD_COVER band sits at 9950-10150 m, ~9.8 km above its
                    // highest street strip — so nothing here is measurable today. It is a union
                    // rather than a pick because that is the combiner the binary already uses
                    // between volumes (a + b - a·b), and picking one would silently delete the
                    // other if a future chapter ever authored both.
                    //
                    // Colour: the curtain composited OVER the band (it is the nearer air), which
                    // is the standard over-blend — union alpha, and each layer's own colour
                    // weighted by the share of that alpha it contributes. Degenerate at both ends:
                    // band-only gives the band's colour, volume-only the volume's.
                    var volumeColor = _fogWhiteout.Color ?? _weather.CloudTopColor ?? WhiteoutFallbackColor;
                    float bandShare = band * (1f - volume);
                    float union = volume + bandShare;
                    c = volumeColor.Lerp(c, bandShare / union);
                    c.A = union;
                }
                else
                {
                    // Byte-identical to the pre-C21 path by construction, which is what keeps the
                    // seven disarmed chapters (and C5 away from its strips) off this item's books.
                    c.A = band;
                }
                LogVolumeWhiteout(rig.Index, volume);
                rig.Whiteout.Color = c;
            }

            // The deck is a world-fixed FLOOR at `_deckAltitude` — the tiles' OWN authored
            // altitude (`WorldBuilder.CloudDeckAltitude`: C1/C1C/C2B 960, C4 1050) — at every
            // camera altitude, following the camera in X/Z only so the sheet has no reachable edge
            // (B13; the altitude supersedes A7's band-centre pin, which was C4's own coincidence:
            // its authored altitude equals its centre, 1050, so C4 was unmoved by that change,
            // while C1's 960 vs the former 1047 pin was an 87 m drop).
            //
            // ⚠ B14, 2026-08-09: it no longer JUMPS. The below-band branch used to carry the sheet
            // at `camera.y + DeckCeilingHeight` as the overcast ceiling — A7's behaviour decode,
            // the wrong object. The tiles carry `zone_id 2`, the gate below culls them entirely
            // below the deck, and the ceiling there is `horizon/zone1`'s own camera-anchored,
            // UV-scrolled dome (the dome gate at the end of this loop). Only the deck's LIT
            // VARIANT still flips at the band centre, and that flip stays inside the fully-opaque
            // whiteout core (C1: total in 1032–1062, WeatherState.WhiteoutAmount).
            //
            // ⚠ Whether the deck DRAWS is not decided here at all — that is the zone gate below.
            // This block only decides WHERE it sits and which lit variant it wears, and it keeps
            // running while the deck is hidden (a `--no-zone-cull` run still needs it placed).
            //
            // (History: until A6 this pinned Y to the band centre in BOTH regimes — the
            // above-band half of this trick applied everywhere — which buried the deck inside
            // the `fvol` slab and hung every sprite below it. A6 removed the pin; A7 restored it
            // for the regime it actually belongs to; B13 replaces THAT pin with the tiles' own
            // authored altitude, read off the built data (`_deckAltitude`, set by
            // `SetDeckAltitude`) rather than hardcoded — the four deck chapters ship the deck
            // ~10 m below their `fvol1`–`fvol9` slab floor: C1 960/970.00, C1C 960/970.73,
            // C2B 960/970.00, C4 1050/1060.00 — that mesh/slab relationship is unaffected by
            // this item, since only the WORLD placement changed, not the mesh's own authored Y.)
            if (rig.Deck != null && _weather is { HasCloudBand: true } weather)
            {
                (float deckY, bool deckDimmed) = DeckRegime(camPos.Y, weather.CloudBandCentre, _deckAltitude);
                rig.Deck.Position = new Vector3(
                    camPos.X - _deckCenter.X,
                    deckY - _deckCenter.Y,
                    camPos.Z - _deckCenter.Z);
                // B13 verification evidence: logged only on a change, so the probe's log names the
                // exact Y this rig's deck copy renders at — the authored 960/1050, now at every
                // altitude (B14 retired the below-band ceiling branch, so a SECOND line here means
                // the deck moved and something is wrong).
                ulong deckId = rig.Deck.GetInstanceId();
                if (!_lastLoggedDeckY.TryGetValue(deckId, out float lastY) || !Mathf.IsEqualApprox(lastY, deckY))
                {
                    _lastLoggedDeckY[deckId] = deckY;
                    string regime = deckDimmed ? "below band, dimmed underside" : "at/above band, undimmed top";
                    Log.Debug("world",
                        $"deck: player {rig.Index} camera y={camPos.Y:0.0} -> deck y={deckY:0.0} ({regime})");
                }
                // The band centre takes the mission's SUNLIGHT dimming off the sheet: below it the
                // camera sees the overcast's underside, above it its top, and the original renders
                // those at 167.7 and ~210 respectively (C23). Hidden by the same opaque whiteout
                // core the crossing sits in.
                SetDeckDimmed(rig.Deck, deckDimmed);
            }

            // B12 — the original's zone_id visibility gate (FUN_0056c430), applied per CAMERA
            // because splitscreen panes can sit in different states at the same instant. Two
            // surfaces, one rule:
            //   * the shared world — every mesh SceneBuilder stamped with a zone layer — through
            //     this camera's cull mask, so nothing touches Node3D.Visible on content whose
            //     visibility the animation runtime, the unplaced-entity watch and DamageVisuals
            //     all read and write themselves;
            //   * this rig's OWN deck and dome copies through Node3D.Visible, because a per-player
            //     copy already carries its player's visual layer and a cull mask cannot express
            //     "this pane AND this zone".
            // Runs for every rig in every chapter, unlike the A7 rule it replaced: a chapter with
            // no deck simply never leaves state 1, and its zone-1 content is exactly what state 1
            // draws.
            int gate = _spec.SkyZoneExplicit
                ? ZoneNumberOf(_spec.SkyZone) ?? rig.CameraWeatherState
                : rig.CameraWeatherState;
            rig.Camera.CullMask = _spec.NoZoneCull
                ? ZoneGate.OpenCullMask(rig.Camera.CullMask)
                : ZoneGate.CullMask(rig.Camera.CullMask, gate);
            if (rig.Deck != null)
                rig.Deck.Visible = _spec.NoZoneCull || ZoneGate.Draws(_deckZoneId, gate);
            // The domes (B14). One per horizon zone the world could tell apart
            // (WorldBuilder.DomeZonesToBuild), each showing only at its own state: below the cloud
            // deck a deck chapter draws horizon/zone1 — the camera-anchored, UV-scrolled ceiling
            // this item put there — and above it horizon/zone2. A chapter with a SINGLE built dome
            // keeps it at every state, because "no sky at all" is not a frame the original can
            // render (B12's rule, now per dome rather than per rig).
            bool gateDomes = !_spec.NoZoneCull && rig.HorizonDomes.Count > 1;
            foreach (var dome in rig.HorizonDomes)
                dome.Node.Visible = !gateDomes || ZoneGate.Draws(dome.ZoneId, gate);
        }

        // B11: the zone the camera's own state wears, re-applied only at the state EDGE
        // (FUN_00472ea0 is called on change, not per frame — and the C26 rim annulus reads these
        // same fog globals, so a per-frame recompute would make it shimmer at the boundary).
        //
        // ⚠ Driven by rig 0, because the fog parameters this writes are GLOBAL shader uniforms —
        // one set for the whole session, unlike the whiteout overlay and the deck regime above,
        // which are per rig. In splitscreen with one player under the deck and one over it, both
        // panes therefore wear player 1's fog. That is a pre-existing property of the fog chain
        // (SetupWeather has always written one global set), not something this item introduces;
        // making it per pane needs per-instance fog uniforms, which SetupWeather's comment on
        // `csky_fog_on` explains is the hazard it was written to avoid.
        if (_weather != null && rigs.Count > 0
            && _fogState.Next(rigs[0].CameraWeatherState, _weather) is { } change)
        {
            if (change.FellBack)
                // Once per state, not per crossing: a mission that authors no ZONE<n> keeps the
                // file's first zone rather than rendering WeatherState.NoFog (fullbright, no fog).
                // Said out loud because it is the one line explaining a state change that moves
                // nothing on screen.
                GD.Print($"weather: {_spec.Chapter}/{_spec.Mission} authors no 'zone{change.State}' "
                         + $"(zones: {string.Join("/", _weather.ZoneNames)}) — camera state "
                         + $"{change.State} keeps fog zone '{change.Zone}'");
            if (change.Applied)
            {
                var fog = _weather.Fog(change.Zone);
                ApplyFogGlobals(fog);
                GD.Print($"weather: camera state {change.State} -> fog zone '{change.Zone}' — "
                         + $"fog {fog.FogNear:0}-{fog.FogFar:0} m, altitude {fog.FogLow:0}-{fog.FogHigh:0} m, "
                         + $"world light {fog.WorldLight:0.00} (dome built for '{_activeZone}')");
            }
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

    /// <summary>Says out loud that one rig's in-volume whiteout engaged, and how hard — the
    /// evidence a C21 probe reads (METHOD-15), since a curtain that never fires and one that fires
    /// at 0.02 look the same in a night frame. Only the 0 ↔ non-0 crossings and moves of 0.05 or
    /// more are logged, so a pass through a street strip costs a handful of lines rather than one
    /// per frame. Silent for every disarmed chapter, which never reaches a non-zero density.</summary>
    private void LogVolumeWhiteout(int rigIndex, float density)
    {
        if (!_fogWhiteout.Armed)
        {
            return;
        }
        bool known = _lastLoggedVolumeWhiteout.TryGetValue(rigIndex, out float last);
        if (known && (density > 0f) == (last > 0f) && Mathf.Abs(density - last) < 0.05f)
        {
            return;
        }
        if (!known && density <= 0f)
        {
            return;   // the resting state; the census line at build already says the rig is armed
        }
        _lastLoggedVolumeWhiteout[rigIndex] = density;
        Log.Debug("world", $"fvol whiteout: player {rigIndex} density {density:0.000}");
    }

    /// <summary>D32: this rig's flicker off the band's raw <c>WhiteoutAmount</c>, seeded from the
    /// same <see cref="Rng.Clouds"/> stream every <c>--det</c> run re-derives identically —
    /// created lazily so the first call for a rig is that rig's own frame 0 (the identity
    /// guarantee lives in <see cref="BandFlicker.Apply"/>, not here).</summary>
    private float ApplyBandFlicker(int rigIndex, float op, float frameDt)
    {
        if (!_bandFlicker.TryGetValue(rigIndex, out var flicker))
        {
            // A per-instance System.Random off Rng.Clouds (the shape the effects code already
            // uses, e.g. Puffer's per-instance fields) rather than the shared Godot
            // RandomNumberGenerator stream directly: BandFlicker re-draws repeatedly over its own
            // lifetime as the drift re-randomizes at each bound, and keeping that off a native
            // Godot object is what lets BandFlickerTests exercise it outside the engine (RngTests'
            // own reason for the same choice).
            _bandFlicker[rigIndex] = flicker = new BandFlicker(Rng.NewSystemRandom(Rng.Clouds));
        }
        return flicker.Apply(op, frameDt);
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
        // B6: the mission's WIND block. Every weather.zrd in the install authors the same four
        // values — STATIC_VELOCITY (0,2,0), MAX_SPEED 10, ACCEL 5, ANG_VEL 5 — but they are read
        // per mission, not baked, because the reader is the authority and a mission without a
        // weather.json must get still air rather than someone else's breeze.
        _wind = _weather == null
            ? WorldWind.Still()
            : new WorldWind(_weather.WindStatic, _weather.WindRandomMaxSpeed,
                _weather.WindRandomAccel, _weather.WindRandomAngVel,
                Rng.NewSystemRandom(Rng.Wind));
        if (_weather != null)
            // Said out loud once per session: a puffer that drifts sideways for no visible reason
            // is otherwise indistinguishable from a broken spawn (DIAG-15), and this is the one
            // line that names the force doing it.
            GD.Print($"wind: static ({_weather.WindStatic.X:0.##}, {_weather.WindStatic.Y:0.##}, "
                     + $"{_weather.WindStatic.Z:0.##}) m/s, gust <= {_weather.WindRandomMaxSpeed:0.##} m/s "
                     + $"(step {_weather.WindRandomAccel:0.##} m/s per frame, turning "
                     + $"{_weather.WindRandomAngVel:0.##} deg/s) — carries every puffer with FRICTION "
                     + "by its WIND_FACTOR");
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
        // B11: the fog zone follows the camera's weather state from here on, starting from
        // whatever _activeZone the build just resolved — so a chapter whose state-1 zone IS the
        // built zone (every non-deck chapter: their CLOUD_COVER band is authored out of reach, so
        // the state never leaves 1) never rewrites a single global and is pixel-identical to the
        // static behaviour. An explicit --sky-zone disarms the machine entirely (Decision 5): the
        // flag exists so an inspection pose renders one named zone reproducibly, and a pose that
        // silently switched zone with altitude would not be that.
        _fogState = new FogStateTrigger(stateDriven: !_spec.SkyZoneExplicit, buildZone: _activeZone);
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
        var fogRange = ApplyFogGlobals(fog);
        GD.Print($"weather [{_activeZone}]{(_spec.NoFog ? " --no-fog: fog + whiteout OFF, world light unchanged;" : ":")} " +
                 $"fog {fog.FogColor.R:0.00} gray {fogRange.X:0}–{fogRange.Y:0} m " +
                 $"(authored {fog.FogNear:0}–{fog.FogFar:0}), " +
                 $"altitude {fog.FogLow:0}–{fog.FogHigh:0} m; world light {fog.WorldLight:0.00}; " +
                 $"cloud band {_weather.CloudBottom:0}–{_weather.CloudTop:0} m (±{_weather.CloudThickness:0})");
        if (_fogWhiteout.Armed)
        {
            // Said out loud once per session, because "the curtain never fired" and "the chapter
            // never armed it" are the same picture otherwise (DIAG-15/DIAG-17). Only C5 prints it.
            var wc = _fogWhiteout.Color ?? _weather.CloudTopColor ?? WhiteoutFallbackColor;
            GD.Print($"fvol whiteout: armed — {_fogVolumes.Count} volume(s), approach {_fogWhiteout.FadeDist:0.#} m, "
                     + $"interior decay {_fogWhiteout.InteriorFadeDist:0.#} m, colour {wc.ToHtml(false)}"
                     + $"{(_fogWhiteout.Color == null ? " (CLOUD_COVER TOP_COLOR default)" : " (authored)")}");
        }
        SetupWhiteoutAndPrecip(rigs);
    }

    /// <summary>Writes ONE zone's fog into the global shader parameters — colour, range, altitude
    /// band and the <c>SUNLIGHT</c>-derived world light — and returns the range actually written
    /// (<c>--no-fog</c>'s out-of-reach pair, or the authored one). Called twice: once by
    /// <see cref="SetupWeather"/> for the zone the flight builds with, and again from
    /// <see cref="Tick"/> each time the camera's weather state changes zone (B11). Every write
    /// here is idempotent and order-free, which is what lets the second caller be an edge trigger
    /// rather than a per-frame recompute.</summary>
    private Vector2 ApplyFogGlobals(WeatherState.ZoneFog fog)
    {
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
        // saturation out; C3's over-fogged canyon slope moved 178 -> 106 against the original's
        // 36.5 at that measurement. ⚠ Do not re-open that gap from these numbers: the C3 murk was
        // closed at the controls on 2026-08-09 (BL-321), and the instrument that produced the
        // 106-vs-36.5 residual disagrees with the user's own eyes on the flown scene — the boxes,
        // not the fog, are what is unreliable there. `git log --grep=BL-321`.
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
        return fogRange;
    }

    /// <summary>The per-rig whiteout overlays and the mission's precipitation field — the half of
    /// <see cref="SetupWeather"/> that builds NODES rather than writing global shader parameters,
    /// split out so the fog half (<see cref="ApplyFogGlobals"/>) can be re-run on a camera-state
    /// change without rebuilding either.</summary>
    private void SetupWhiteoutAndPrecip(IReadOnlyList<PlayerRig> rigs)
    {
        if (_weather == null)
            return;
        // ⚠ The overlay is built for a reachable CLOUD_COVER band OR an armed fog_zone (C21) — the
        // volume curtain paints on this same surface, and a chapter that armed it without
        // authoring a band would otherwise have nothing to paint on. No shipped chapter is in that
        // state (C5 arms fog_zone and authors a band at 9950-10150 m), so this changes no built
        // node in the install; it exists so the two conditions cannot drift apart.
        if (_weather.HasCloudBand || _fogWhiteout.Armed)
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

    /// <summary>What one camera-state change asks the fog chain to do: which state it is, the zone
    /// that state resolved to, whether the globals need re-writing at all
    /// (<see cref="Applied"/> — false when two states share a zone through the file fallback), and
    /// whether that resolution WAS a fallback, reported once per state
    /// (<see cref="FellBack"/>).</summary>
    public readonly record struct FogZoneChange(int State, string Zone, bool Applied, bool FellBack);

    /// <summary>The B11 edge trigger between <c>PlayerRig.CameraWeatherState</c> and the fog
    /// globals: it answers "has the camera's state changed, and if so which zone's fog does it
    /// now wear", and it answers <c>null</c> every other frame.
    ///
    /// <para>⚠ Edge-triggered, not per-frame-recomputed, and that is a requirement rather than an
    /// optimisation: the binary applies a zone from <c>FUN_00472ea0</c> on change only, and the
    /// C26 rim annulus reads these same fog globals — a per-frame rewrite makes it shimmer at the
    /// boundary. <see cref="Applications"/> exists so a test can assert the count, since "wrote
    /// the same value again" is invisible in every other instrument.</para>
    ///
    /// <para>Two ways it stays silent. (a) <paramref name="stateDriven"/> false — an explicit
    /// <c>--sky-zone</c>, which per Decision 5 pins the flight to one named zone so inspection
    /// poses reproduce. (b) The state changed but resolved to the zone already live: a mission
    /// that authors no <c>ZONE2</c> falls back to its first zone
    /// (<see cref="WeatherState.ZoneForState"/>), so the crossing costs nothing and moves no
    /// pixel.</para></summary>
    public sealed class FogStateTrigger
    {
        private readonly bool _stateDriven;
        private readonly HashSet<int> _fallbacksReported = new();
        private string _zone;
        private int _state;

        /// <param name="stateDriven">False pins the fog to <paramref name="buildZone"/> for the
        /// whole flight (an explicit <c>--sky-zone</c>).</param>
        /// <param name="buildZone">The zone <c>WeatherRig.Build</c> already applied — the trigger
        /// starts live on it, so a chapter whose state-1 zone is that same zone never writes a
        /// global and renders byte-identically to the static behaviour.</param>
        public FogStateTrigger(bool stateDriven, string buildZone)
        {
            _stateDriven = stateDriven;
            _zone = buildZone;
        }

        /// <summary>How many times this trigger has asked for the globals to be re-written.</summary>
        public int Applications { get; private set; }

        /// <summary>The zone whose fog is live.</summary>
        public string Zone => _zone;

        /// <summary>The camera's state this frame; null unless it CHANGED since the last call
        /// (and always null when the trigger is not state-driven).</summary>
        public FogZoneChange? Next(int state, WeatherState weather)
        {
            if (!_stateDriven || state == _state)
                return null;
            _state = state;
            var zone = weather.ZoneForState(state);
            bool fellBack = !zone.Equals(
                "zone" + state.ToString(System.Globalization.CultureInfo.InvariantCulture),
                StringComparison.OrdinalIgnoreCase);
            bool applied = !zone.Equals(_zone, StringComparison.OrdinalIgnoreCase);
            if (applied)
            {
                _zone = zone;
                Applications++;
            }
            return new FogZoneChange(state, zone, applied, fellBack && _fallbacksReported.Add(state));
        }
    }

    /// <summary>D32: the decompiled in-cloud flicker (<c>FUN_0042ee40</c>) — remaps the band's raw
    /// opacity through two curves (<see cref="LogCurve"/>/<see cref="AtanCurve"/>) blended by a
    /// parameter that drifts in [0,1] and re-randomizes its speed at each bound, sign-flipped at
    /// the 1 bound (matching the decompile: the low-bound reset keeps a freshly-drawn POSITIVE
    /// speed, the high-bound clamp negates it, which is what turns the drift into a ping-pong
    /// rather than a one-shot ramp).
    ///
    /// <para>⚠ <b>Frame-0 identity is not "start at t=0".</b> Neither curve equals the identity
    /// function at an interior opacity — <c>AtanCurve(0.5) = 0.267</c>, not 0.5 — so a fresh
    /// instance ramps the blended remap's AMPLITUDE in from 0 over <see cref="RampFrames"/> calls
    /// to <see cref="Apply"/> instead: the first call for any instance returns <c>op</c> completely
    /// unchanged (ramp exactly 0, so <c>op + 0f * anything == op</c> bit-for-bit), which is what
    /// keeps a static probe or golden shot at a rig's first tick reading the unremapped
    /// <see cref="WeatherState.WhiteoutAmount"/> the <c>FlatColorTests</c>/<c>DeckRegimeTests</c>
    /// pins were measured against (D32's trap).</para></summary>
    public sealed class BandFlicker
    {
        // ⚠ TUNE (D32) — the decompile's rate multiplier is read from a per-mission weather-struct
        // field (≈ +0x934) that no reader decodes and no capture pins a value for. Picked so the
        // MIDPOINT of the re-randomized drift speed (0.2..1.0, mean 0.6) traverses the full [0,1]
        // range in a few seconds: 5.5 * 0.6 * 0.1 = 0.33/s -> ~3 s at the mean, 1.8-9.2 s across the
        // randomized range. Record kept here (the field's one implementation) and in
        // backlog.md `BL-329`'s `[Tuning]` entry (the discoverable index).
        public const float DefaultRate = 5.5f;

        // ⚠ TUNE (D32) — how many `Apply` calls (sim frames) the amplitude ramps in over: 30 = 0.5 s
        // at the fixed 60 Hz `--det` step. Long enough that the ramp itself is not the shimmer;
        // short enough that a flight spends effectively none of its time in it.
        public const int RampFrames = 30;

        private readonly Random _rng;
        private float _t;
        private float _driftSpeed;
        private long _framesSeen;

        public BandFlicker(Random rng)
        {
            _rng = rng;
            _driftSpeed = RandomDriftSpeed(rng);
        }

        /// <summary>The blend parameter's current value — exposed for the determinism test, not
        /// consumed by <see cref="WeatherRig"/>.</summary>
        public float T => _t;

        /// <summary>The log-shaped curve: <c>ln(op*5+1)/ln(6)</c> — <c>FUN_0042ee40</c>'s
        /// <c>fVar1/fVar2</c>, both computed as <c>log2</c> in the binary but the base cancels in
        /// the ratio, so natural log reproduces it exactly.</summary>
        public static float LogCurve(float op) => MathF.Log((op * 5f) + 1f) / MathF.Log(6f);

        /// <summary>The atan-shaped curve: <c>(atan((op-0.5)*10)+0.5)/(atan(5)+0.5)</c> —
        /// <c>FUN_0042ee40</c>'s <c>param_1</c>.</summary>
        public static float AtanCurve(float op) =>
            (MathF.Atan((op - 0.5f) * 10f) + 0.5f) / (MathF.Atan(5f) + 0.5f);

        /// <summary>The two curves blended by <paramref name="t"/> and clamped to [0,1] — the
        /// binary's <c>fVar3 = (logCurve - atanCurve) * t + atanCurve</c> followed by its own
        /// clamp. Both curves agree at <c>op=1</c> (both equal 1) and the clamp forces <c>op=0</c>
        /// to 0 regardless of <paramref name="t"/> (<c>AtanCurve(0)</c> is negative), which is why
        /// the remap never moves the band's hard edges — only its interior.</summary>
        public static float Remap(float op, float t) =>
            Mathf.Clamp(AtanCurve(op) + (t * (LogCurve(op) - AtanCurve(op))), 0f, 1f);

        /// <summary>Advances the drift by one frame and returns the flickered opacity — identity on
        /// the very first call (see the class summary), and identity again whenever
        /// <paramref name="op"/> sits exactly at 0 or 1 (the binary's guard: the whole remap block,
        /// including the drift update, is skipped there, so a band pinned at a hard edge never
        /// drifts).</summary>
        public float Apply(float op, float frameDt, float rate = DefaultRate)
        {
            if (op <= 0f || op >= 1f)
                return op;

            float ramp = Mathf.Clamp(_framesSeen / (float)RampFrames, 0f, 1f);
            _framesSeen++;

            _t += rate * frameDt * _driftSpeed * 0.1f;
            if (_t > 1f || _t < 0f)
            {
                float mag = RandomDriftSpeed(_rng);
                if (_t <= 1f)
                {
                    _t = 0f;
                    _driftSpeed = mag;
                }
                else
                {
                    _t = 1f;
                    _driftSpeed = -mag;
                }
            }

            float remapped = Remap(op, _t);
            return op + (ramp * (remapped - op));
        }

        private static float RandomDriftSpeed(Random rng) => 0.2f + ((float)rng.NextDouble() * 0.8f);
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
