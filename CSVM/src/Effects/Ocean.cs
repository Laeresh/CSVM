using System;
using System.Collections.Generic;
using System.Globalization;
using CSVM.Mech3;
using CSVM.Utils;
using Godot;

namespace CSVM.Effects;

/// <summary>
/// The Enhanced-only wave ocean that replaces the flat sea-level sheet. A camera-centred polar grid
/// carries a Gerstner wave sum in its vertex stage, and finer waves live in its normals. A mask
/// baked from the world's water polygons (<see cref="OceanMask"/>) calms the waves under the surf
/// ring, along every shore and around the ships. The base sheet's colliders stay flat at y = 0; only
/// its triangles step aside (<c>csky_ocean.gdshaderinc</c>). Driven by csky_time, so a network peer
/// and a fixed-step capture see the same waves. The chapter's <see cref="SeaState"/> writes the
/// shader text (<see cref="OceanShader"/>) and its uniforms; <see cref="Apply"/> takes a new one live.
/// </summary>
public sealed partial class Ocean : Node3D
{
    /// <summary>The global switch's name, declared in <c>res://shaders/csky_ocean.gdshaderinc</c>.</summary>
    public const string Param = "csky_ocean_on";

    // The grid's angular resolution near the eye; it halves at each of BandEnds.
    private const int InnerSegments = 384;

    // Remake-only rule, set by the look. The waves stay flat this far past a hull's waterline, about the swell's sideways sway.
    // They reach full height over the fade beyond, so no crest crosses the deck.
    private const float CalmMargin = 2f;
    private const float CalmFade = 16f;
    private const int MaxShips = OceanShader.MaxShips;
    private const int WakeSamples = OceanShader.WakeSamples;

    // The hull meshes that reach this close to the hull's own y = 0 make its waterline.
    private const float WaterlineBand = OceanMovers.WaterlineBand;

    private static readonly StringName ParamName = Param;
    private static readonly float[] BandEnds = { 700f, 2800f, 11000f, 45000f };

    private readonly Func<IEnumerable<Node3D>> _ships;
    private readonly List<Node3D> _wakes;
    private readonly Node3D _worldRoot;
    private readonly ShaderMaterial _material;
    private readonly Vector4[] _calmAxes = new Vector4[MaxShips];
    private readonly Vector4[] _calmSizes = new Vector4[MaxShips];
    private readonly OceanCalmZone[] _zones = new OceanCalmZone[MaxShips];
    private readonly Node3D?[] _zoneHulls = new Node3D?[MaxShips];
    private readonly Dictionary<Node3D, Aabb?> _waterlines = new();
    private readonly OceanMask _mask;
    private readonly List<Node3D> _candidates = new();
    private readonly List<Vector3> _candidateAt = new();
    private readonly List<bool> _candidateWake = new();
    private readonly List<int> _order = new();
    private readonly Dictionary<Node3D, Transform3D> _nestedRest = new();
    private readonly HashSet<Node3D> _zoned = new();
    private readonly Vector4[] _wakeTrack = new Vector4[MaxShips * WakeSamples];
    private readonly Vector4[] _wakeBounds = new Vector4[MaxShips];
    private readonly Dictionary<Node3D, OceanWakeTrack> _tracks = new();
    private readonly List<Node3D> _untracked = new();
    private readonly HashSet<Node3D> _tracked = new();
    private readonly Dictionary<MeshInstance3D, uint> _sheetLayers = new();
    private readonly int _level;
    private readonly bool _zonedSheet;

    // The hull --debug-wake sails, where it stood and the sim time it set off.
    private (Node3D Hull, Transform3D Start, double From)? _driven;

    private Ocean(ShaderMaterial material, Func<IEnumerable<Node3D>> ships, OceanMask mask, Node3D worldRoot,
        int level, SeaState sea)
    {
        _mask = mask;
        _wakes = mask.Wakes;
        Name = "Ocean";
        _material = material;
        _ships = ships;
        _worldRoot = worldRoot;
        _level = level;
        _zonedSheet = mask.ZoneTexture != null;
        Sea = sea;
    }

    /// <summary>The sea the ocean draws, clamped.</summary>
    public SeaState Sea { get; private set; }

    /// <summary>The grids' shader text, as the sea last wrote it.</summary>
    public string ShaderText => _material.Shader.Code;

    /// <summary><c>--debug-wake</c>: the speed in m/s and the turn in degrees a second. The first wake
    /// ship on the water sails at them from where it stands, along its own -Z. Null leaves hulls alone.</summary>
    public Vector2? DebugDrive { get; set; }

    /// <summary>The wake sheets the bake found, which step aside while the ocean draws their hull's
    /// wake. Read by the suites.</summary>
    internal IReadOnlyList<Node3D> WakeSheets => _wakes;

    /// <summary>Declares the switch and the mask globals the base sheet's hide reads. Must run before
    /// the first shader that reads them is built.</summary>
    public static void RegisterGlobal()
    {
        RenderingServer.GlobalShaderParameterAdd(Param, RenderingServer.GlobalShaderParameterType.Float, 0.0f);
        OceanMask.RegisterGlobals();
    }

    /// <summary>Whether a chapter's sea gets the ocean: every chapter with a sea at y = 0
    /// (<see cref="OceanSeas.Chapters"/>).</summary>
    public static bool Covers(string chapter) => OceanSeas.IsSeaChapter(chapter);

    /// <summary>Builds the ocean over the world under <paramref name="worldRoot"/> with the chapter's
    /// <paramref name="sea"/>, or null when that world has no sea-level base sheet to replace. The
    /// <paramref name="movers"/> an animation carries (<see cref="OceanMovers"/>) are calmed around as
    /// hulls wherever they float. A non-empty <paramref name="maskPng"/> receives the baked mask.</summary>
    public static Ocean? Create(Node3D worldRoot, SceneBuilder scene, TextureArchive textures,
        Func<IEnumerable<Node3D>> ships, ISet<Node> skip, IReadOnlyCollection<Node3D> movers, SeaState sea,
        string maskPng)
    {
        long start = System.Diagnostics.Stopwatch.GetTimestamp();
        var mask = OceanMask.Bake(worldRoot, scene, skip, movers);
        if (mask == null)
        {
            Log.Info("world", $"ocean: no sea-level base sheet, not built");
            return null;
        }
        double bakeMs = System.Diagnostics.Stopwatch.GetElapsedTime(start).TotalMilliseconds;
        var baseTex = textures.Find(mask.BaseTexture);

        bool zoned = mask.ZoneTexture != null;
        sea = sea.Clamped();
        int level = mask.BaseLevel - 1;
        string code = OceanShader.Code(sea, level, zoned);
        ShaderCensus.Note(code);
        var shader = new Shader { Code = code };
        var material = new ShaderMaterial { Shader = shader };
        material.SetShaderParameter("mask_tex", mask.Texture);
        material.SetShaderParameter("mask_rect", new Vector4(mask.Origin.X, mask.Origin.Y, 1f / mask.Size.X, 1f / mask.Size.Y));
        if (baseTex != null)
            material.SetShaderParameter("albedo_tex", baseTex);
        material.SetShaderParameter("tile_m", mask.TileMetres);
        material.SetShaderParameter("tint_tex", mask.TintTexture);
        material.SetShaderParameter("lift_tex", mask.LiftTexture);
        if (mask.ZoneTexture is { } zoneTex)
            material.SetShaderParameter("zone_tex", zoneTex);

        var ocean = new Ocean(material, ships, mask, worldRoot, level, sea);
        ocean.SetSeaUniforms();
        ocean.PublishShips();
        var mesh = BuildGrid();
        mesh.SurfaceSetMaterial(0, material);
        // One grid per zone group, on that group's layers. The zone gate then culls the waves with
        // the sheet they replace; C5 splits its sea over zone_id 1 and 3.
        for (int z = 0; z < Math.Max(1, mask.ZoneLayers.Count); z++)
        {
            var instance = new MeshInstance3D
            {
                Name = z == 0 ? "OceanGrid" : $"OceanGrid{z}",
                Mesh = mesh,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                // The grid follows every camera in its vertex stage, so it must never be culled.
                CustomAabb = new Aabb(new Vector3(-80000f, -50f, -80000f), new Vector3(160000f, 100f, 160000f)),
            };
            if (z < mask.ZoneLayers.Count)
                instance.Layers = mask.ZoneLayers[z];
            if (zoned)
                instance.SetInstanceShaderParameter("zone_index", (float)z);
            ocean.AddChild(instance);
        }
        double ms = System.Diagnostics.Stopwatch.GetElapsedTime(start).TotalMilliseconds;
        Log.Info("world", $"ocean: built grid verts={mesh.SurfaceGetArrayLen(0)} mask={mask.Width}x{mask.Height} cell={OceanMask.Cell}m origin=({mask.Origin.X:0},{mask.Origin.Y:0}) base={mask.BaseTexture} tile={mask.TileMetres:0.0}m base_tris={mask.BaseTriangles} base_level={mask.BaseLevel} zone_groups={mask.ZoneLayers.Count} edge_tris={mask.EdgeTriangles} ramp_tris={mask.RampTriangles} solid_tris={mask.SolidTriangles} wakes={mask.Wakes.Count} movers={movers.Count} moving_hulls={mask.MovingHulls.Count} unjudged={mask.UnjudgedMovers.Count} hull_tris={mask.MovingHullTriangles} ms={ms:0} bake={bakeMs:0} ({mask.Timing}) sea={sea.Describe()}");
        foreach (var hull in mask.MovingHulls)
        {
            var p = hull.GlobalPosition;
            var line = ocean.Waterline(hull);
            string size = line is { } l ? string.Create(CultureInfo.InvariantCulture, $"{l.Size.X:0}x{l.Size.Z:0}m") : "none";
            Log.Info("world", $"ocean: moving hull '{hull.Name}' at ({p.X:0},{p.Y:0.0},{p.Z:0}) waterline {size}");
        }
        foreach (var mover in mask.UnjudgedMovers)
            Log.Debug("world", $"ocean: mover '{mover.Name}' hidden at the bake, judged when shown");
        foreach (var wake in mask.Wakes)
        {
            if (wake.IsInsideTree() && wake is MeshInstance3D { Mesh: { } wakeMesh })
            {
                var p = wake.GlobalPosition;
                var hull = ocean.HullOf(wake);
                var zone = new OceanCalmZone(new Vector2(p.X, p.Z), Heading(hull));
                zone.Add(wakeMesh.GetAabb(), wake.GlobalTransform);
                var half = zone.HalfExtents;
                Log.Info("world", $"ocean: wake '{wake.GetParent()?.Name}/{wake.Name}' of '{hull.Name}' at ({p.X:0},{p.Y:0.0},{p.Z:0}) sheet {2f * half.X:0}x{2f * half.Y:0}m visible={wake.IsVisibleInTree()}");
            }
        }
        if (maskPng.Length > 0)
        {
            var err = mask.Image.SavePng(maskPng);
            if (err == Error.Ok)
                err = mask.TintImage.SavePng(System.IO.Path.ChangeExtension(maskPng, ".tint.png"));
            if (err == Error.Ok)
                Log.Info("world", $"ocean: mask written to {maskPng}");
            else
                Log.Error("world", $"--dump-ocean-mask: could not write {maskPng} ({err})");
        }
        return ocean;
    }

    // The sheet hides only where this ocean's mask says it draws, so the mask goes live with it.
    public override void _EnterTree()
    {
        _mask.Publish();
        RenderingServer.GlobalShaderParameterSet(ParamName, 1.0f);
    }

    public override void _ExitTree()
    {
        RenderingServer.GlobalShaderParameterSet(ParamName, 0.0f);
        OceanMask.Withdraw();
        foreach (var (sheet, layers) in _sheetLayers)
        {
            if (IsInstanceValid(sheet))
                sheet.Layers = layers;
        }
        _sheetLayers.Clear();
        _tracks.Clear();
        _driven = null;
    }

    public override void _Process(double delta)
    {
        DriveDebugHull();
        PublishShips();
    }

    /// <summary>Takes <paramref name="sea"/> live, clamped. The shader recompiles only when its text
    /// changes; height, foam strength and roughness are uniforms and change nothing else. The grids,
    /// the mask and the flat colliders stay as they are.</summary>
    public void Apply(SeaState sea)
    {
        Sea = sea.Clamped();
        string code = OceanShader.Code(Sea, _level, _zonedSheet);
        if (!string.Equals(code, _material.Shader.Code, StringComparison.Ordinal))
            ShaderTwins.Rewrite(_material.Shader, code);
        SetSeaUniforms();
    }

    // Concentric rings around the origin whose spacing grows with radius, so a triangle covers
    // about the same screen area at every distance. UV.x carries the local spacing for the
    // vertex stage's per-wave fade.
    private static ArrayMesh BuildGrid()
    {
        var verts = new List<Vector3>();
        var uvs = new List<Vector2>();
        var idx = new List<int>();
        verts.Add(Vector3.Zero);
        uvs.Add(new Vector2(0.5f, 0f));
        int n = InnerSegments;
        float r = 4f;
        int prevStart = AddRing(verts, uvs, r, n);
        int prevN = n;
        for (int i = 0; i < n; i++)
            idx.AddRange(new[] { 0, prevStart + i, prevStart + ((i + 1) % n) });
        for (int band = 0; band < BandEnds.Length; band++)
        {
            while (r < BandEnds[band])
            {
                r *= 1f + (Mathf.Tau / n);
                int start = AddRing(verts, uvs, r, n);
                Stitch(idx, prevStart, prevN, start, n);
                prevStart = start;
                prevN = n;
            }
            n = Math.Max(n / 2, 24);
        }
        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = verts.ToArray();
        arrays[(int)Mesh.ArrayType.TexUV] = uvs.ToArray();
        arrays[(int)Mesh.ArrayType.Index] = idx.ToArray();
        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        return mesh;
    }

    private static int AddRing(List<Vector3> verts, List<Vector2> uvs, float r, int n)
    {
        int start = verts.Count;
        float spacing = r * Mathf.Tau / n;
        for (int i = 0; i < n; i++)
        {
            float a = Mathf.Tau * i / n;
            verts.Add(new Vector3(r * Mathf.Cos(a), 0f, r * Mathf.Sin(a)));
            uvs.Add(new Vector2(spacing, 0f));
        }
        return start;
    }

    // Joins an inner ring to the next one out, which has the same count or half of it.
    private static void Stitch(List<int> idx, int a, int na, int b, int nb)
    {
        if (na == nb)
        {
            for (int i = 0; i < na; i++)
            {
                int i1 = (i + 1) % na;
                idx.AddRange(new[] { a + i, b + i, b + i1 });
                idx.AddRange(new[] { a + i, b + i1, a + i1 });
            }
            return;
        }
        for (int k = 0; k < nb; k++)
        {
            int k1 = (k + 1) % nb;
            int i0 = 2 * k, i1 = 2 * k + 1, i2 = (2 * k + 2) % na;
            idx.AddRange(new[] { a + i0, b + k, a + i1 });
            idx.AddRange(new[] { a + i1, b + k, b + k1 });
            idx.AddRange(new[] { a + i1, b + k1, a + i2 });
        }
    }

    // A ship's horizontal heading, its local +Z on the water.
    private static Vector2 Heading(Node3D hull)
    {
        var z = hull.GlobalBasis.Z;
        return new Vector2(z.X, z.Z);
    }

    // Whether a hull or wake sheet shows with its origin on sea level; a hoisted lifeboat's is not.
    private static bool Afloat(Node3D node) =>
        IsInstanceValid(node) && node.IsInsideTree() && node.IsVisibleInTree()
        && Mathf.Abs(node.GlobalPosition.Y) <= OceanMovers.OriginBand;

    // A visible wake sheet's mesh while the sheet lies on the water, else null.
    private static Mesh? WakeOnWater(Node3D wake) =>
        Afloat(wake) && wake is MeshInstance3D { Mesh: { } mesh } ? mesh : null;

    // The sea's uniforms. At the defaults they equal the values the shader text declares.
    private void SetSeaUniforms()
    {
        _material.SetShaderParameter("wave_scale", Sea.Height);
        _material.SetShaderParameter("foam_strength", Sea.FoamStrength);
        _material.SetShaderParameter("rough_near", Sea.RoughNear);
        _material.SetShaderParameter("rough_far", Sea.RoughFar);
    }

    // The ocean draws a wake for each hull holding one of the first held slots. Those hulls' sheets
    // move onto the flat-sea layer, which only a spyglass disc draws. Every other sheet keeps
    // its own layers. A hidden sheet stays visible, so the zones and animations read it as before.
    private void FollowWakeSheets(int held)
    {
        foreach (var wake in _wakes)
        {
            if (wake is not MeshInstance3D sheet || !IsInstanceValid(sheet))
                continue;
            bool hide = ZoneHullOf(wake, _zoneHulls, held) >= 0;
            if (hide && sheet.Layers != UI.Boards.SplitScreen.FlatSeaLayer)
            {
                // A sheet written since it was hidden (an object's zone gate) keeps that mask for later.
                _sheetLayers[sheet] = sheet.Layers;
                sheet.Layers = UI.Boards.SplitScreen.FlatSeaLayer;
            }
            else if (!hide && _sheetLayers.Remove(sheet, out uint own))
            {
                sheet.Layers = own;
            }
        }
    }

    // --debug-wake: the first wake ship on the water sails an arc on the sim clock. It is set after the
    // frame's animation, so the capture and the wake both see the arc.
    private void DriveDebugHull()
    {
        if (DebugDrive is not { } drive || GameClock.Current is not { } clock)
            return;
        if (_driven is not { } driven || !IsInstanceValid(driven.Hull))
        {
            Node3D? hull = null;
            foreach (var wake in _wakes)
            {
                if (WakeOnWater(wake) != null)
                {
                    hull = HullOf(wake);
                    break;
                }
            }
            if (hull == null)
                return;
            _driven = driven = (hull, hull.GlobalTransform, clock.Time);
            Log.Info("world", $"--debug-wake: '{hull.Name}' sails at {drive.X:0.##} m/s turning {drive.Y:0.##} deg/s");
        }
        float t = (float)(clock.Time - driven.From);
        float w = Mathf.DegToRad(drive.Y);
        var forward = -driven.Start.Basis.Z with { Y = 0f };
        forward = forward.Normalized();
        var left = forward.Rotated(Vector3.Up, Mathf.Pi / 2f);
        var path = Mathf.Abs(w) < 1e-6f ? forward * t
            : (forward * (Mathf.Sin(w * t) / w)) + (left * ((1f - Mathf.Cos(w * t)) / w));
        driven.Hull.GlobalTransform = new Transform3D(driven.Start.Basis.Rotated(Vector3.Up, w * t), driven.Start.Origin + (path * drive.X));
    }

    // The hull a wake sheet trails: the sheet's mesh sits under its own node, which hangs off the
    // ship. A wake node straight under the world root is its own hull.
    private Node3D HullOf(Node3D wake)
    {
        var node = wake.GetParent() as Node3D ?? wake;
        return node.GetParent() is Node3D ship && ship != _worldRoot ? ship : node;
    }

    // One zone per ship on the water, over its waterline, and the track its wake rides, each frame;
    // an unused slot sits far away. With more ships afloat than slots, the nearest to the eye win.
    private void PublishShips()
    {
        _candidates.Clear();
        _candidateAt.Clear();
        _candidateWake.Clear();
        foreach (var hull in _ships())
            Offer(hull, wake: false);
        foreach (var hull in _mask.MovingHulls)
            Offer(hull, wake: false);
        foreach (var mover in _mask.UnjudgedMovers)
        {
            if (Afloat(mover) && Waterline(mover) != null && _mask.IsSea(mover.GlobalPosition))
                Offer(mover, wake: false);
        }
        for (int i = _candidates.Count - 1; i >= 0; i--)
        {
            if (Rides(_candidates[i]))
            {
                _candidates.RemoveAt(i);
                _candidateAt.RemoveAt(i);
                _candidateWake.RemoveAt(i);
            }
        }
        foreach (var wake in _wakes)
        {
            if (WakeOnWater(wake) is { } && ZoneHullOf(wake, _candidates, _candidates.Count) < 0)
                Offer(HullOf(wake), wake: true);
        }
        int n = 0;
        var eye = IsInsideTree() ? GetViewport()?.GetCamera3D()?.GlobalPosition : null;
        // Without a camera the first listed keep the slots: roster, moving, then wake hulls.
        OceanMovers.Nearest(_candidateAt, eye ?? Vector3.Zero, eye != null ? MaxShips : int.MaxValue, _order);
        if (_order.Count > MaxShips)
            _order.RemoveRange(MaxShips, _order.Count - MaxShips);
        foreach (int i in _order)
        {
            var hull = _candidates[i];
            int added = AddHull(hull, n);
            if (added == n && _candidateWake[i])
            {
                // A wake's ship keeps its zone wherever its own origin stands.
                var o = hull.GlobalPosition;
                _zones[n] = new OceanCalmZone(new Vector2(o.X, o.Z), Heading(hull));
                _zoneHulls[n] = hull;
                added = n + 1;
            }
            if (added > n && _zoned.Add(hull))
                Log.Debug("world", $"ocean: zone over '{hull.Name}' at ({_candidateAt[i].X:0},{_candidateAt[i].Y:0.0},{_candidateAt[i].Z:0}) size {2f * _zones[n].HalfExtents.X:0}x{2f * _zones[n].HalfExtents.Y:0}m of {_candidates.Count} afloat");
            n = added;
        }
        // The wakes run on the sim clock; with none, as in a menu's backdrop, no hull draws one.
        double? time = GameClock.Current?.Time;
        FollowWakeSheets(time != null ? n : 0);
        for (int i = 0; i < MaxShips; i++)
        {
            _wakeBounds[i] = OceanWakeTrack.Unused;
            if (i < n)
            {
                var c = _zones[i].Center;
                var axis = _zones[i].Axis;
                var half = _zones[i].HalfExtents;
                _calmAxes[i] = new Vector4(c.X, c.Y, axis.X, axis.Y);
                _calmSizes[i] = new Vector4(half.X, half.Y, CalmMargin, CalmFade);
                if (time is { } t && _zoneHulls[i] is { } hull)
                {
                    if (!_tracks.TryGetValue(hull, out var track))
                        _tracks[hull] = track = new OceanWakeTrack();
                    track.Observe(t, c);
                    _tracked.Add(hull);
                    _wakeBounds[i] = track.Write(_wakeTrack, i * WakeSamples, half, axis);
                }
            }
            else
            {
                _calmAxes[i] = new Vector4(1e9f, 1e9f, 1f, 0f);
                _calmSizes[i] = new Vector4(0f, 0f, 0f, 1f);
            }
            _zoneHulls[i] = null;
        }
        ForgetUnseenTracks();
        _material.SetShaderParameter("calm_axes", _calmAxes);
        _material.SetShaderParameter("calm_sizes", _calmSizes);
        _material.SetShaderParameter("wake_track", _wakeTrack);
        _material.SetShaderParameter("wake_bounds", _wakeBounds);
    }

    // A hull that held no slot this frame starts its track again when it next holds one, so a gap
    // never reads as a passage.
    private void ForgetUnseenTracks()
    {
        _untracked.Clear();
        foreach (var hull in _tracks.Keys)
        {
            if (!_tracked.Contains(hull))
                _untracked.Add(hull);
        }
        foreach (var hull in _untracked)
            _tracks.Remove(hull);
        _tracked.Clear();
    }

    // Opens a zone over a visible hull on the water and its waterline; returns the zones in use.
    private int AddHull(Node3D hull, int n)
    {
        if (n >= MaxShips || !Afloat(hull) || Array.IndexOf(_zoneHulls, hull, 0, n) >= 0)
            return n;
        var xf = hull.GlobalTransform;
        _zones[n] = new OceanCalmZone(new Vector2(xf.Origin.X, xf.Origin.Z), Heading(hull));
        if (Waterline(hull) is { } line)
            _zones[n].Add(line, xf);
        else
            _zones[n].Add(new Vector2(xf.Origin.X, xf.Origin.Z));
        _zoneHulls[n] = hull;
        return n + 1;
    }

    // The union of the hull's meshes that reach its waterline, in the hull's own frame, measured
    // once. The wake sheets are left out; the ocean draws the wake itself.
    private Aabb? Waterline(Node3D hull)
    {
        if (_waterlines.TryGetValue(hull, out var kept))
            return kept;
        Aabb? line = null;
        var toHull = hull.GlobalTransform.AffineInverse();
        var stack = new Stack<Node>();
        stack.Push(hull);
        while (stack.Count > 0)
        {
            var node = stack.Pop();
            foreach (var child in node.GetChildren())
                stack.Push(child);
            if (node is not MeshInstance3D { Mesh: { } mesh } mi || !mi.IsVisibleInTree() || _wakes.Contains(mi))
                continue;
            var box = toHull * mi.GlobalTransform * mesh.GetAabb();
            if (box.Position.Y > WaterlineBand || box.End.Y < -WaterlineBand)
                continue;
            line = line is { } l ? l.Merge(box) : box;
        }
        _waterlines[hull] = line;
        return line;
    }

    // Lists a ship for a zone this frame, once. A wake's ship needs no origin on the water.
    private void Offer(Node3D hull, bool wake)
    {
        if (!IsInstanceValid(hull) || !hull.IsInsideTree() || (!wake && !Afloat(hull)) || _candidates.Contains(hull))
            return;
        _candidates.Add(hull);
        _candidateAt.Add(hull.GlobalPosition);
        _candidateWake.Add(wake);
    }

    // The first of hulls[0..count) that is the wake's ship or holds the sheet, else -1.
    private int ZoneHullOf(Node3D wake, IReadOnlyList<Node3D?> hulls, int count)
    {
        var ship = HullOf(wake);
        for (int i = 0; i < count; i++)
        {
            if (hulls[i] is { } h && (h == ship || h.IsAncestorOf(wake)))
                return i;
        }
        return -1;
    }

    // Whether a hull rides inside another listed hull where it stood when first seen there, which
    // that hull's waterline already covers. A part that moves off on its own keeps a zone.
    private bool Rides(Node3D inner)
    {
        foreach (var outer in _candidates)
        {
            if (outer == inner || !outer.IsAncestorOf(inner))
                continue;
            var rel = outer.GlobalTransform.AffineInverse() * inner.GlobalTransform;
            if (!_nestedRest.TryGetValue(inner, out var rest))
            {
                _nestedRest[inner] = rel;
                return true;
            }
            return rel.Origin.DistanceTo(rest.Origin) < 0.5f && rel.Basis.Z.Normalized().Dot(rest.Basis.Z.Normalized()) > 0.999f;
        }
        return false;
    }
}
