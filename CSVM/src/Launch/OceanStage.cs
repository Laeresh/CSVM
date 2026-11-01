using System;
using System.Collections.Generic;
using System.Linq;
using CSVM.Effects;
using CSVM.Mech3;
using CSVM.Session.World;
using CSVM.Spec;
using CSVM.Utils;
using Godot;

namespace CSVM.Launch;

/// <summary>The Enhanced wave ocean over a flown or free-camera chapter the ocean covers, one step of
/// the session build kept for the session. It holds the chapter's sea and the ocean drawn from it.
/// The graphics switch and the live Water Quality word both take <see cref="Follow"/>.
/// Module entry: docs/architecture/Launch.md on src/Launch/OceanStage.cs.</summary>
internal sealed class OceanStage
{
    private readonly SessionSpec _spec;
    private readonly Node3D _plane;
    private readonly SceneBuilder? _worldScene;
    private readonly TextureArchive? _textures;
    // The world runtime whose motions say which hulls the ocean follows.
    private readonly AnimRuntime? _runtime;
    private readonly Func<SurfaceVehicleRuntime?> _vessels;

    /// <summary>The stage over one session's chapter. It resolves the sea and builds the ocean if the
    /// standing mode and word ask for waves. The roster hulls come and go, so the stage reads
    /// <paramref name="vessels"/> at each build and each frame.</summary>
    public OceanStage(SessionSpec spec, Node3D plane, SceneBuilder? worldScene, TextureArchive? textures,
        AnimRuntime? runtime, Func<SurfaceVehicleRuntime?> vessels)
    {
        _spec = spec;
        _plane = plane;
        _worldScene = worldScene;
        _textures = textures;
        _runtime = runtime;
        _vessels = vessels;
        // The chapter's sea from the shipped file, under any --debug-ocean overrides. A chapter with no entry draws the defaults.
        Saved = OceanSeas.Load().For(spec.Chapter);
        Sea = Saved.WithOverrides(spec.DebugOcean);
        string debug = spec.DebugOcean is { Length: > 0 } flag ? $" (--debug-ocean={flag})" : "";
        Log.Info("world", $"ocean: sea {spec.Chapter} saved={Saved.Describe()} drawn={Sea.Describe()}{debug}");
        Follow();
    }

    /// <summary>The chapter's saved sea.</summary>
    public SeaState Saved { get; }

    /// <summary>The sea the ocean draws: the saved one under any --debug-ocean overrides and the ocean
    /// lab's edits. An ocean rebuilt by a live switch takes it.</summary>
    public SeaState Sea { get; private set; }

    /// <summary>The wave ocean standing, null while none does.</summary>
    public Ocean? Ocean { get; private set; }

    /// <summary>Whether the wave ocean stands in this session's world.</summary>
    public bool Built => Ocean != null;

    /// <summary>Whether a session can stand the ocean at all: a whole flown or free-camera world of a
    /// chapter the ocean covers.</summary>
    public static bool Eligible(SessionSpec spec, bool partialStage) =>
        !partialStage && (spec.Fly || spec.Freecam) && Ocean.Covers(spec.Chapter);

    /// <summary>Drops a standing ocean and follows again, so its shore mask is baked from the world as
    /// it now draws. The shadow decals' gate calls it (<see cref="SceneBuilder.FollowShadowDecals"/>).</summary>
    public void Rebuild()
    {
        if (Ocean == null)
            return;
        Drop();
        Follow();
    }

    /// <summary>Builds the ocean on the first Enhanced frame that asks for waves. A switch back to
    /// Original or to flat water drops it, and the sea sheet draws again because the shared switch resets.</summary>
    public void Follow()
    {
        if (!GraphicsMode.Enhanced || !WaterQualitySetting.DrawsWaves)
        {
            Drop();
            return;
        }
        if (Ocean != null || _worldScene == null || _textures == null)
            return;
        if ((_spec.SkippedPasses & EnhancedPasses.Ocean) != 0)
        {
            Log.Info("world", $"ocean: skipped (--no-ocean)");
            return;
        }
        var hulls = new HashSet<Node>();
        if (_vessels() is { } vessels)
        {
            foreach (var v in vessels.Vessels)
                hulls.Add(v.Body);
        }
        Ocean = Ocean.Create(_plane, _worldScene, _textures, Hulls, hulls, AnimatedMovers(hulls), Sea, _spec.OceanMaskPath);
        if (Ocean != null)
        {
            Ocean.DebugDrive = _spec.DebugWake;
            _plane.AddChild(Ocean);
        }
    }

    /// <summary>The ocean lab's edit: the standing ocean takes it at once, and a rebuilt one takes it too.</summary>
    public void Apply(SeaState sea)
    {
        Sea = sea.Clamped();
        Ocean?.Apply(Sea);
    }

    private void Drop()
    {
        if (Ocean != null)
        {
            Ocean.GetParent()?.RemoveChild(Ocean);
            Ocean.QueueFree();
        }
        Ocean = null;
    }

    // The world nodes the bound program's played motions carry, as the runtime resolves them. The
    // roster hulls are left out; they have their own source.
    private List<Node3D> AnimatedMovers(HashSet<Node> roster)
    {
        var movers = new List<Node3D>();
        if (_runtime is not { } runtime)
            return movers;
        var seen = new HashSet<Node3D>();
        foreach (var def in runtime.ProgramDefs)
        {
            var moving = OceanMovers.MovingEvents(def);
            if (moving.Count == 0)
                continue;
            foreach (var node in runtime.TargetsOf(def, moving))
            {
                if (node != _plane && GodotObject.IsInstanceValid(node) && !InRoster(node) && seen.Add(node))
                    movers.Add(node);
            }
        }
        var names = movers.Select(m => m.Name.ToString()).OrderBy(n => n, StringComparer.Ordinal);
        Log.Debug("world", $"ocean: movers resolved [{string.Join(", ", names)}]");
        return movers;

        bool InRoster(Node node)
        {
            for (Node? n = node; n != null; n = n.GetParent())
            {
                if (roster.Contains(n))
                    return true;
            }
            return false;
        }
    }

    // The roster hulls the ocean calms around, each over its own waterline.
    private IEnumerable<Node3D> Hulls()
    {
        if (_vessels() is not { } vessels)
            yield break;
        foreach (var v in vessels.Vessels)
        {
            if (GodotObject.IsInstanceValid(v.Body) && v.Body.IsVisibleInTree())
                yield return v.Body;
        }
    }
}
