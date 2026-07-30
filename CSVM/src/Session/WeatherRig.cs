using System;
using System.Collections.Generic;
using CSVM.Effects;
using CSVM.Flight;
using CSVM.Mech3;
using CSVM.UI;
using Godot;

namespace CSVM.Session;

/// <summary>Loads and applies the flown mission's weather, and drives its per-frame rig state
/// (PLAN-planeviewer-split A5, moved verbatim off <c>GameSession</c>): <c>LoadWeather</c> +
/// <c>SetupWeather</c> become <see cref="Build"/>, and the per-rig skydome/whiteout/deck/puff
/// update block from <c>_Process</c> becomes <see cref="Tick"/>. Constructed once per session
/// (<c>_weatherRig</c> in <c>GameSession.StartSession</c>, same lifetime as
/// <see cref="LiveryResolver"/>/<see cref="SpawnPicker"/>/<see cref="WorldEffectsFactory"/>).
/// The horizon (skydome) build loop itself stays on <c>GameSession</c> — it is a
/// <c>SceneBuilder</c> concern, not weather state — so <see cref="Build"/> takes it as a callback
/// invoked between the zone resolving and the fog/whiteout/puffs/precip setup, at exactly the
/// point the original code ran it.</summary>
public sealed class WeatherRig
{
    private static readonly Color WhiteoutColor = new(0.95f, 0.95f, 0.96f);

    private readonly SessionSpec _spec;
    private readonly Node3D _worldRoot;

    private WeatherState? _weather;
    private string _activeZone;
    private Precipitation? _precip;
    private Vector3 _deckCenter;

    public WeatherRig(SessionSpec spec, Node3D worldRoot)
    {
        _spec = spec;
        _worldRoot = worldRoot;
        _activeZone = spec.SkyZone;
    }

    /// <summary>Loads the mission's weather.json and resolves the rendered zone, builds the
    /// per-rig domes via <paramref name="buildDomes"/> (needs the resolved zone), then applies
    /// fog + whiteout + puffs + precipitation — the same order <c>StartSession</c> ran before the
    /// move.</summary>
    public void Build(string missionZrdrPath, IReadOnlyList<PlayerRig> rigs, TextureArchive textures,
        Action<string> buildDomes)
    {
        LoadWeather(missionZrdrPath);
        buildDomes(_activeZone);
        SetupWeather(rigs, textures);
    }

    /// <summary>The deck geometry's original AABB centre, so <see cref="Tick"/> can re-anchor it
    /// under each player every frame. Set separately from <see cref="Build"/> because the cloud
    /// deck is world geometry (<c>GameSession</c>'s <c>cloudDeck</c>), not weather state, and is
    /// built whenever a chapter world loads — not only when this rig itself gets built.</summary>
    public void SetDeckCenter(Vector3 center) => _deckCenter = center;

    /// <summary>Everything anchored to *a* camera, once per rig — one in single player, one per
    /// pane in splitscreen (each on that player's own visual layer): re-centers the skydome,
    /// fades the cloud-band whiteout, re-anchors the cloud deck, and advances the ambient puffs.
    /// Moved verbatim off <c>GameSession._Process</c>.</summary>
    public void Tick(IReadOnlyList<PlayerRig> rigs, float dt)
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
                var c = rig.Whiteout.Color;
                // --no-fog covers the whiteout too: flying into the cloud band would otherwise
                // still white the pane out, which reads as "fog is not actually off".
                c.A = _spec.NoFog ? 0f : _weather.WhiteoutAmount(camPos.Y);
                rig.Whiteout.Color = c;
            }

            // Cloud deck follows the player: centered on the camera x/z and pinned to a fixed
            // altitude at the whiteout-band centre. You climb toward it as a fixed ceiling (floor
            // once above) and pass through it exactly where the whiteout is fully opaque, so the
            // ceiling→floor transition is hidden.
            if (rig.Deck != null && _weather is { HasCloudBand: true })
            {
                float mid = (_weather.CloudTop + _weather.CloudBottom) * 0.5f;
                rig.Deck.Position = new Vector3(
                    camPos.X - _deckCenter.X,
                    mid - _deckCenter.Y,
                    camPos.Z - _deckCenter.Z);
            }

            // Ambient cloud puffs: keep the drifting field around the plane (world-anchored,
            // recycled at the shell edge — see CloudPuffs). Forward is the camera's -Z look dir,
            // so fresh puffs spawn ahead and the plane flies into them.
            rig.Puffs?.Update(dt, camPos, -rig.Camera.GlobalTransform.Basis.Z);
        }
    }

    /// <summary>Resolves <see cref="_activeZone"/>: the zone the fog AND the skydome are both
    /// built from. Called before the domes, because the zone names are per chapter — C5 ships
    /// zone1+zone3, so the `zone2` default has to fall back or C5 renders with no fog and no dome
    /// at all. The default stays `zone2` deliberately; which zone a mission actually flies is in
    /// no reader, so it is the user's A/B against the original (docs/formats/weather.md).</summary>
    private void LoadWeather(string missionZrdrPath)
    {
        _weather = WeatherState.Load(missionZrdrPath);
        _activeZone = _weather?.ResolveZone(_spec.SkyZone) ?? _spec.SkyZone;
        if (_weather == null)
        {
            GD.PushWarning($"no weather.json for {_spec.Chapter}/{_spec.Mission} — flying without fog / whiteout");
            return;
        }
        if (!_activeZone.Equals(_spec.SkyZone, StringComparison.OrdinalIgnoreCase))
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
    private void SetupWeather(IReadOnlyList<PlayerRig> rigs, TextureArchive textures)
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
        //Range is halved because this does not seem to be radius but diameter. See Screenshot C1 IA1 Fog Range.png vs Screenshots\Fog Range.png
        // NOTE: that calibration predates the fog-color sRGB fix below (the old washed-out
        // near-white read weaker than true 176 gray) — worth a fresh in-game A/B; factor 1
        // makes the overcast deck's texture persist further down toward the horizon.
        float fogRangeFactor = 2.0f;
        // --no-fog pushes the range out of reach instead of touching `csky_fog_on`. That uniform
        // would work — every shader still honours it — but it is an INSTANCE uniform declared at
        // index 1 in SceneBuilder's shader and index 0 in Clutter's, and Godot merges that mapping
        // per GeometryInstance3D. Writing it would make a latent index mismatch live (the
        // unfogged-hilltops bug; see Clutter.ShaderCode's comment). `csky_fog_range` is
        // a GLOBAL uniform every fogged shader reads, so one write covers the world, the clutter
        // sprites, the solid city blocks and the dome with no ordering hazard at all.
        var fogRange = _spec.NoFog
            ? new Vector2(1e8f, 1e9f)   // same no-op range Weather.NoFog uses
            : new Vector2(fog.FogNear, fog.FogFar) / fogRangeFactor;
        RenderingServer.GlobalShaderParameterSet("csky_fog_range", fogRange);
        // FOG_ALTITUDE: the fog cylinder's vertical extent — full fog below FogLow, fading to
        // none at FogHigh (fragment altitude; see SceneBuilder's fog shader block). Absolute
        // altitudes, so the range factor doesn't apply.
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
                 $"fog {fog.FogColor.R:0.00} gray {fog.FogNear:0}–{fog.FogFar:0} m, " +
                 $"altitude {fog.FogLow:0}–{fog.FogHigh:0} m; world light {fog.WorldLight:0.00}; " +
                 $"cloud band {_weather.CloudBottom:0}–{_weather.CloudTop:0} m (±{_weather.CloudThickness:0})");

        if (_weather.HasCloudBand)
        {
            // Both of these follow *a* camera, so each rig gets its own: in splitscreen
            // the overlay must dim only the pane whose player is inside the cloud, and the puff
            // field must sit around that player.
            foreach (var rig in rigs)
            {
                // A pane-filling overlay so the whiteout swallows everything (terrain, plane,
                // clouds) uniformly, like the original. Layer 0 keeps it behind the HUD (layer 1).
                var canvas = new CanvasLayer { Layer = 0, Name = "whiteout" };
                rig.Whiteout = new ColorRect
                {
                    Color = new Color(WhiteoutColor, 0f),
                    MouseFilter = Control.MouseFilterEnum.Ignore,
                };
                rig.Whiteout.SetAnchorsPreset(Control.LayoutPreset.FullRect);
                canvas.AddChild(rig.Whiteout);
                rig.HudParent.AddChild(canvas);

                // Ambient cloud puffs: the soft wisps that drift past the plane at altitude
                // (OriginalScreenshots/"C1 IA1 Cloud Puffs and Moon.png"). A hand-tuned field
                // gated to the cloud band and drifting with the weather WIND; Tick advances it
                // each frame from the camera. Added at world identity (its instance positions are
                // absolute world coords).
                rig.Puffs = CloudPuffs.Create(textures, _weather.WindStatic,
                    _weather.CloudBottom, _weather.CloudTop);
                if (rig.Puffs == null)
                    continue;
                if (rig.VisualLayer != 0)
                    SplitScreen.SetVisualLayer(rig.Puffs, rig.VisualLayer);
                _worldRoot.AddChild(rig.Puffs);
                if (rig.Index == 0)
                    GD.Print("cloud puffs: ambient field active over the cloud band");
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
}
