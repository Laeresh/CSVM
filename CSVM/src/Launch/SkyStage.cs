using System;
using System.Collections.Generic;
using CSVM.Effects;
using CSVM.Extraction;
using CSVM.Flight.Camera;
using CSVM.Mech3;
using CSVM.Session.World;
using CSVM.Spec;
using CSVM.UI.Boards;
using CSVM.Utils;
using Godot;

namespace CSVM.Launch;

/// <summary>The sky over a flown or weathered world, one step of the session build kept for the
/// session. It holds the mission's weather rig and each rig's skydomes and cloud deck. It also
/// holds the ambient cloud field and the sun's lens flare.
/// <see cref="Build"/> reads the <see cref="BuildState"/> and the world builder once. The session
/// ticks <see cref="Weather"/> and <see cref="Flare"/> in its own frame order.
/// Module entry: docs/architecture/Launch.md on src/Launch/SkyStage.cs.</summary>
internal sealed class SkyStage
{
    private const float HorizonScale = 2.5f;

    // The fraction of the camera's far plane the scaled skydome may reach. Past it the dome clips
    // and the clear colour shows through. 0.9 leaves room for the one-frame anchor lag.
    // See HorizonScaleFor.
    private const float HorizonFarFraction = 0.9f;

    private static readonly string[] InstanceShaderParams =
        { "node_bias", "csky_fog_on", "csky_light_fade", SceneBuilder.OpacityParam };

    private readonly SessionSpec _spec;
    private readonly Node3D _worldRoot;
    private readonly Camera3D _camera;
    private readonly DirectionalLight3D _sun;
    private readonly Godot.Environment? _env;
    private readonly EffectAmbience _ambience;
    private readonly ViewerSet _viewers;
    private readonly IReadOnlyList<PlayerRig> _rigs;
    private readonly Func<IReadOnlyList<Node3D>> _gatedObjects;

    // A FOG_STATE raised during the world bootstrap, before the weather rig exists. It is applied
    // once the rig has written its zone, the original's order: the zone first, the event over it.
    private AnimRuntime.FogStateChange? _fogStateBeforeWeather;

    /// <summary>The stage over one session's world. The rigs are the session's pane list, read at
    /// each build call. The gated objects are what the cloud band's per-object gate moves between
    /// layers, read fresh each frame.</summary>
    public SkyStage(SessionSpec spec, Node3D worldRoot, Camera3D camera, DirectionalLight3D sun,
        Godot.Environment? env, EffectAmbience ambience, ViewerSet viewers,
        IReadOnlyList<PlayerRig> rigs, Func<IReadOnlyList<Node3D>> gatedObjects)
    {
        _spec = spec;
        _worldRoot = worldRoot;
        _camera = camera;
        _sun = sun;
        _env = env;
        _ambience = ambience;
        _viewers = viewers;
        _rigs = rigs;
        _gatedObjects = gatedObjects;
    }

    /// <summary>The mission's weather and its per-rig sky state, null before the first weathered
    /// build and in a mode that builds none.</summary>
    public WeatherRig? Weather { get; private set; }

    /// <summary>The sun's lens flare, null before the world stage built one.</summary>
    public LensFlareRig? Flare { get; private set; }

    /// <summary>The fogvol.zrd clutter field, null where the world has none.</summary>
    public FogVolumeClutter? CloudField { get; private set; }

    /// <summary>The world bootstrap's FOG_STATE sink: applied at once over a built rig, held until
    /// the rig has applied its zone otherwise.</summary>
    public void TakeFogState(AnimRuntime.FogStateChange fog)
    {
        if (Weather != null)
            Weather.ApplyFogState(fog);
        else
            _fogStateBeforeWeather = fog;
    }

    /// <summary>The weather step of a chapter world's build, in the modes that draw a sky. It
    /// builds the cloud field, the weather rig with one dome set per rig, then the held
    /// fog. The lens flare follows in every mode, after the domes it anchors to.</summary>
    public void Build(BuildState state, WorldBuilder builder)
    {
        if (_spec.Fly || _spec.Freecam || _spec.SkyZoneExplicit)
        {
            BuildWeather(state, builder);
        }

        // ⚠ Build the flare after the weather. Its sun node is a child of each rig's horizon, and
        // finding it is one of the two gates. The chapter data decides whether anything is built.
        Flare = new LensFlareRig(_spec);
        Flare.Build(_rigs, state.Textures, state.InterpPath, _spec.Chapter);
    }

    /// <summary>Once the deck is in the tree at its original position, the weather rig learns its
    /// centre to re-anchor it under each player every frame. Every rig past the first takes its own
    /// copy.</summary>
    public void AttachDeck(BuildState state)
    {
        if (state.CloudDeck == null)
            return;
        Weather?.SetDeckCenter(OrbitCamera.MergedAabb(state.CloudDeck).GetCenter());
        if (state.DeckUndimmedMeshes != null)
            Weather?.SetDeckUndimmedMeshes(state.DeckUndimmedMeshes);
        AssignCloudDecks(state.CloudDeck);
    }

    private static void CopyInstanceShaderParams(Node source, Node copy)
    {
        if (source is GeometryInstance3D from && copy is GeometryInstance3D to)
            foreach (var name in InstanceShaderParams)
            {
                var value = from.GetInstanceShaderParameter(name);
                if (value.VariantType != Variant.Type.Nil)
                    to.SetInstanceShaderParameter(name, value);
            }
        int n = Math.Min(source.GetChildCount(), copy.GetChildCount());
        for (int i = 0; i < n; i++)
            CopyInstanceShaderParams(source.GetChild(i), copy.GetChild(i));
    }

    private void BuildWeather(BuildState state, WorldBuilder builder)
    {
        long weatherMark = StartupProfile.Mark();
        // The ambient cloud field: fogvol.zrd clutter scattered through the fvol* boxes this
        // gamez authors. World-anchored, so it is built once beside the world rather than per
        // rig, and needs no per-frame driving unlike the dome/deck/whiteout below.
        var fogVolumes = FogVolumeSpec.VolumesOf(state.Gamez);
        var fogVolumeSpec = FogVolumeSpec.Load(SessionPaths.ChapterZrdr(state.DataRoot, _spec.Chapter));
        var cloudField = FogVolumeClutter.Create(state.Gamez, state.Textures,
            fogVolumeSpec, fogVolumes, _spec.CloudJitter);
        CloudField = cloudField;
        if (cloudField != null)
        {
            _worldRoot.AddChild(cloudField);
            // The jitter is named on every launch, not only when a flag moved it. The shipped
            // offset is a departure from the decoded lattice, so a run's record has to say
            // which field it drew.
            string jitterVia = _spec.CloudJitter > 0f
                ? $", remake jitter {_spec.CloudJitter:0.#} m" : ", decoded lattice, no jitter";
            Log.Info("world", $"fogvol clouds: {cloudField.InstanceCount} sprites ({cloudField.BaseCount} base + {cloudField.ExtensionCount} map-edge extension) over {fogVolumes.Count} volume(s), {cloudField.Summary}{jitterVia}");
        }
        // ⚠ Read the fvol zone from the data, never assume it. A chapter authoring -1 keeps the
        // default layer and renders below its deck, as authored. One MultiMesh spans every volume.
        int fvolZone = WorldBuilder.FogVolumeZoneIdOf(state.Gamez);
        if (cloudField != null && ZoneGate.LayerFor(fvolZone) is var fvolLayer and not 0)
        {
            SplitScreen.SetVisualLayer(cloudField, fvolLayer);
        }

        // The sun goes in with the weather: its bearing is the zone's own SUNLIGHT_ORIENTATION,
        // applied by the same zone-apply that writes the fog. The ambience is the wind seam and
        // the viewer set carries each pane's camera pose for the puffer distance fade.
        var weather = new WeatherRig(_spec, _worldRoot, _sun, _ambience, _viewers, _env);
        Weather = weather;
        // The deck's own zone_id, the one gated population that cannot ride a visual layer (it
        // is a per-rig camera-anchored copy, see WeatherRig.SetDeckZoneId).
        weather.SetDeckZoneId(builder.CloudDeckZoneId);
        // ⚠ Read the deck's altitude off the built data; do not hardcode it or pin the deck to
        // the CLOUD_COVER band centre.
        weather.SetDeckAltitude(builder.CloudDeckAltitude);
        // The same census and parsed fogvol.zrd the cloud field was built from, handed to a
        // second consumer rather than re-loaded. Tick resolves each camera's weather state from
        // it, and its in-volume whiteout where fog_zone is armed.
        weather.SetFogVolumes(fogVolumes, fogVolumeSpec);
        // The flown objects the band's per-object gate moves between layers (ObjectZoneGate).
        // A plane or a zeppelin on the far side of the overcast stops drawing.
        weather.SetGatedObjects(_gatedObjects);
        // The horizon's zone children go in with the mission's weather. The zone the fog and the
        // dome share is picked from both, since three chapters ship an empty zone2.
        weather.Build(state.MissionZrdrPath, _rigs, builder.HorizonZones(),
            activeZone => BuildDomes(builder, activeZone));
        if (_fogStateBeforeWeather is { } heldFog)
        {
            _fogStateBeforeWeather = null;
            weather.ApplyFogState(heldFog);
        }
        StartupProfile.Record("weather", weatherMark);
    }

    // One dome per horizon zone the gate can tell apart, not just the flown one. Below the cloud
    // deck the camera is in state 1 and zone1 is the sky. Tick anchors the per-rig container, so
    // each dome keeps its own scale and gate.
    private void BuildDomes(WorldBuilder builder, string activeZone)
    {
        var zones = builder.HorizonZones();
        var domeZones = WorldBuilder.DomeZonesToBuild(zones, activeZone);
        foreach (var rig in _rigs)
        {
            var anchor = new Node3D { Name = "horizon" };
            foreach (string zoneName in domeZones)
            {
                var dome = builder.BuildHorizon(zoneName);
                if (dome == null)
                    continue;
                dome.Name = $"dome_{zoneName}";
                // ⚠ Scale per dome, never once for the container. A chapter's two zone domes
                // differ in size, and a second dome must not move the flown one's scale.
                dome.Scale = Vector3.One * HorizonScaleFor(dome);
                anchor.AddChild(dome);
                int zoneId = -1;
                foreach (var z in zones)
                    if (z.Name.Equals(zoneName, StringComparison.OrdinalIgnoreCase))
                        zoneId = z.ZoneId;
                rig.HorizonDomes.Add(new HorizonDome(dome, zoneId));
            }
            if (anchor.GetChildCount() == 0)
            {
                anchor.QueueFree();
                break;
            }
            if (rig.VisualLayer != 0)
                SplitScreen.SetVisualLayer(anchor, rig.VisualLayer);
            _worldRoot.AddChild(anchor);
            rig.Horizon = anchor;
        }

        // The evidence that the swap exists at all, since a broken gate and a one-dome
        // chapter render identically at the state they share (docs/verification.md).
        if (_rigs.Count > 0)
        {
            var built = _rigs[0].HorizonDomes;
            var parts = new List<string>();
            foreach (var d in built)
                parts.Add($"{d.Node.Name} (zone_id {d.ZoneId})");
            Log.Info("world", $"horizon: {built.Count} dome(s) per rig, {string.Join(", ", parts)}{(built.Count > 1 ? "; shown by camera weather state" : "")}");
        }
    }

    // The anchor scale for one built skydome. It is HorizonScale, reduced where the dome's far
    // wall would pass the far plane and let the clear colour through.
    // ⚠ HorizonScale is a MAXIMUM, not a constant, and the fit is measured from the built dome's
    // own AABB, never a per-chapter table. Scaling only Y is refuted; the domes keep one uniform
    // fitted scale.
    private float HorizonScaleFor(Node3D dome)
    {
        if (WorldBuilder.DetachedWorldAabb(dome) is not { } aabb)
            return HorizonScale;
        var min = aabb.Position;
        var max = aabb.End;
        float radius = Mathf.Max(
            Mathf.Max(Mathf.Abs(min.X), Mathf.Abs(max.X)),
            Mathf.Max(
                Mathf.Max(Mathf.Abs(min.Y), Mathf.Abs(max.Y)),
                Mathf.Max(Mathf.Abs(min.Z), Mathf.Abs(max.Z))));
        if (radius <= 0f)
            return HorizonScale;
        float fitted = Mathf.Min(HorizonScale, _camera.Far * HorizonFarFraction / radius);
        if (fitted < HorizonScale)
            Log.Info("world", $"horizon: dome radius {radius:0} m x {HorizonScale:0.##} would reach past the {_camera.Far:0} m far plane, scaled {fitted:0.##}x instead");
        return fitted;
    }

    // Gives every rig a cloudlayer deck to anchor under its own camera. Rig 0 takes the world's
    // deck, the rest get copies on their player's visual layer. ⚠ Re-apply the instance uniforms
    // from the source; they are RenderingServer state and no copy carries them.
    private void AssignCloudDecks(Node3D deck)
    {
        _rigs[0].Deck = deck;
        if (_rigs[0].VisualLayer != 0)
            SplitScreen.SetVisualLayer(deck, _rigs[0].VisualLayer);
        var parent = deck.GetParent();
        for (int i = 1; i < _rigs.Count; i++)
        {
            var copy = SceneCopy.Of(deck);
            copy.Name = $"cloud_deck{i + 1}";
            CopyInstanceShaderParams(deck, copy);
            SplitScreen.SetVisualLayer(copy, _rigs[i].VisualLayer);
            parent.AddChild(copy);
            _rigs[i].Deck = copy;
        }
    }
}
