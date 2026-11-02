using System;
using System.Collections.Generic;
using System.Linq;
using CSVM.Mech3;
using CSVM.Utils;
using Godot;

namespace CSVM.Effects;

/// <summary>Remake-only: the spray each breaker throws up the cliffs behind the painted coasts. It
/// stands at the mask's spray sites (<see cref="OceanMaskRaster.SpraySite"/>) and keeps the surf's own
/// phase (<see cref="OceanShader.SurfArrive"/>). A child of the <see cref="Ocean"/>, so only Enhanced at the
/// waves water quality builds it. A sea whose <see cref="SeaState.Sprays"/> is off (C3's) builds none,
/// and it draws only while the sea breaks surf and sprays. Bare sites draw only with <c>,quays</c>. Two mechanisms:
/// the original's water-splash sprites (<c>hg_splasher</c>'s <c>splasher</c> puffer) burst from a
/// pool, or one MultiMesh of upright sheets (<see cref="OceanShader.SprayCode"/>).</summary>
public sealed partial class CoastSpray : Node3D
{
    /// <summary>The mechanism a launch draws unless <c>--coast-spray=</c> names another.</summary>
    public const Mechanism Default = Mechanism.Sprites;

    // TUNE. The sprite pool, which bounds the cost at C1's many short harbour faces. A burst finding no
    // free emitter is dropped.
    private const int Pool = 24;

    // The splasher's own far fade end: past it the puffer culls every particle anyway.
    private const float CullDistance = 1200f;

    // TUNE. Each burst's length, and the splasher's numbers retuned from a plane's impact to a breaker's.
    // It throws fewer sprites a batch, lower (16 m/s climbs about 13 m), for a shorter life.
    private const float BurstSeconds = 0.3f;
    private const int BurstNumber = 8;
    private const float RiseSpeed = 16f;
    private const float LifeMin = 1.0f, LifeMax = 2.4f;

    private readonly OceanMaskRaster.SpraySite[] _sites;
    private readonly long[] _cycle;
    private readonly Puffer[] _pool;
    private readonly double[] _freeAt;
    private readonly Ocean _ocean;
    private readonly EffectAmbience _ambience;
    private SeaState? _sea;
    private float _rate;
    private bool _surf;
    private double _clock;

    private CoastSpray(Ocean ocean, OceanMaskRaster.SpraySite[] sites, Puffer[] pool, EffectAmbience ambience, Mechanism mechanism)
    {
        Name = "CoastSpray";
        _ocean = ocean;
        _sites = sites;
        _cycle = new long[sites.Length];
        _pool = pool;
        _freeAt = new double[pool.Length];
        _ambience = ambience;
        Drawn = mechanism;
        foreach (var p in pool)
            AddChild(p);
    }

    /// <summary>How the spray is drawn, <c>--coast-spray=</c>'s word.</summary>
    public enum Mechanism
    {
        Off,
        Sprites,
        Sheet,
    }

    /// <summary>The mechanism this spray draws by.</summary>
    public Mechanism Drawn { get; }

    /// <summary>The sites it throws spray at.</summary>
    public int Sites => _sites.Length;

    /// <summary>Whether the spray draws now: its ocean in the tree and its sea breaking surf, spray on.</summary>
    public bool Active => IsInsideTree() && Surf().Breaks;

    /// <summary>Bursts fired since the build.</summary>
    public int Bursts { get; private set; }

    /// <summary>Bursts dropped for want of a free emitter since the build.</summary>
    public int Dropped { get; private set; }

    /// <summary>Reads <c>--coast-spray=</c>'s word: <c>sprites</c>, <c>sheet</c> or <c>off</c>, an
    /// empty word the <see cref="Default"/>, with <c>,quays</c> spraying the bare solids too.</summary>
    public static (Mechanism Mechanism, bool Quays, bool Known) Parse(string word)
    {
        var parts = word.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        bool quays = parts.Contains("quays", StringComparer.OrdinalIgnoreCase);
        var head = parts.FirstOrDefault(p => !p.Equals("quays", StringComparison.OrdinalIgnoreCase)) ?? "";
        return head.ToLowerInvariant() switch
        {
            "" => (Default, quays, true),
            "sprites" => (Mechanism.Sprites, quays, true),
            "sheet" => (Mechanism.Sheet, quays, true),
            "off" => (Mechanism.Off, quays, true),
            _ => (Default, quays, false),
        };
    }

    public override void _Process(double delta)
    {
        var (breaks, rate) = Surf();
        // A live sea that stops breaking surf or turns the spray off hides the sheet and every burst.
        Visible = breaks;
        if (_pool.Length == 0)
            return;
        _clock += GameClock.Current?.FrameDt ?? delta;
        float t = ShaderTime.Seconds;
        var viewers = _ambience.Viewers;
        for (int i = 0; i < _sites.Length; i++)
        {
            var site = _sites[i];
            long cycle = (long)Math.Floor(OceanShader.SurfArrive(site.At.X, site.At.Y, t, rate) - 0.25f - OceanShader.SprayLag);
            // The first half second only learns each site's cycle, so a build fires no burst everywhere at once.
            bool due = cycle != _cycle[i] && _clock > 0.5;
            _cycle[i] = cycle;
            if (!due || !breaks || !Near(viewers, site.At))
                continue;
            int k = 0;
            while (k < _pool.Length && _freeAt[k] > _clock)
                k++;
            if (k == _pool.Length)
            {
                Dropped++;
                continue;
            }
            var at = site.At + (site.Seaward * OceanShader.SprayLead);
            _pool[k].Burst(new Vector3(at.X, 0f, at.Y));
            _freeAt[k] = _clock + BurstSeconds + LifeMax;
            Bursts++;
        }
    }

    /// <summary>The spray over <paramref name="ocean"/>'s mask sites, or null with none to draw. The
    /// sprites read the splasher from <paramref name="zrdr"/>.</summary>
    internal static CoastSpray? Create(Ocean ocean, OceanMask mask, string word, ZrdrSet zrdr, TextureArchive textures, EffectAmbience ambience)
    {
        var (mechanism, quays, known) = Parse(word);
        if (!known)
            Log.Warn("world", $"--coast-spray={word}: unknown word, drawing {Default}");
        var sites = mask.Spray.Where(s => quays || !s.Bare).ToArray();
        int bare = mask.Spray.Count(s => s.Bare);
        string off = ocean.Sea.Sprays ? "" : " off by the sea";
        Log.Info("world", $"ocean: spray sites cliff={mask.Spray.Length - bare} bare={bare} drawn={sites.Length} mechanism={mechanism}{(quays ? " quays" : "")}{off}");
        if (mechanism == Mechanism.Off || sites.Length == 0 || !ocean.Sea.Sprays)
            return null;
        if (mechanism == Mechanism.Sheet)
        {
            var spray = new CoastSpray(ocean, sites, Array.Empty<Puffer>(), ambience, mechanism);
            spray.AddChild(Sheet(sites));
            return spray;
        }
        var state = PufferState.Load(zrdr, "huge_splash.json", "splasher");
        if (state == null)
            return null;
        state.Number = BurstNumber;
        state.LocalVelocity = new Vector3(0f, RiseSpeed, 0f);
        state.LifetimeMin = LifeMin;
        state.LifetimeMax = LifeMax;
        var pool = new Puffer[Pool];
        for (int i = 0; i < Pool; i++)
        {
            if (Puffer.Create(state, textures, BurstSeconds, ambience: ambience, stream: Rng.CoastSpray) is not { } p)
                return null;
            pool[i] = p;
        }
        return new CoastSpray(ocean, sites, pool, ambience, mechanism);
    }

    private static bool Near(IReadOnlyList<ViewerSet.ViewerPose> viewers, Vector2 at)
    {
        foreach (var v in viewers)
        {
            if (new Vector3(at.X, 0f, at.Y).DistanceSquaredTo(v.Position) < CullDistance * CullDistance)
                return true;
        }
        return false;
    }

    // One upright quad a site, its custom data the site's seaward direction; the shader poses and times it.
    private static MultiMeshInstance3D Sheet(OceanMaskRaster.SpraySite[] sites)
    {
        var mm = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            UseCustomData = true,
            Mesh = new QuadMesh { Size = Vector2.One },
            InstanceCount = sites.Length,
        };
        var box = new Aabb(new Vector3(sites[0].At.X, 0f, sites[0].At.Y), Vector3.Zero);
        for (int i = 0; i < sites.Length; i++)
        {
            var p = new Vector3(sites[i].At.X, 0f, sites[i].At.Y);
            mm.SetInstanceTransform(i, new Transform3D(Basis.Identity, p));
            mm.SetInstanceCustomData(i, new Color(sites[i].Seaward.X, sites[i].Seaward.Y, 0f, 0f));
            box = box.Expand(p);
        }
        string code = OceanShader.SprayCode();
        ShaderCensus.Note(code);
        float pad = OceanShader.SprayWidth + OceanShader.SprayLead;
        return new MultiMeshInstance3D
        {
            Name = "SpraySheet",
            Multimesh = mm,
            MaterialOverride = new ShaderMaterial { Shader = new Shader { Code = code } },
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            // The shader stands each quad up and turns it, so the box is the sites' grown by a sheet.
            CustomAabb = new Aabb(box.Position - new Vector3(pad, 0f, pad), box.Size + new Vector3(2f * pad, OceanShader.SprayHeight, 2f * pad)),
        };
    }

    // The sea's surf, read again only when the ocean takes a new sea.
    private (bool Breaks, float Rate) Surf()
    {
        if (!ReferenceEquals(_sea, _ocean.Sea))
        {
            _sea = _ocean.Sea;
            var g = OceanShader.SurfGlobal(_sea);
            _surf = g.Y > 0.5f && _sea.Sprays;
            _rate = g.X;
        }
        return (_surf, _rate);
    }
}
