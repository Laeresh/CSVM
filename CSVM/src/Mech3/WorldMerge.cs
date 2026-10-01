using System;
using System.Collections.Generic;
using CSVM.Utils;
using Godot;

namespace CSVM.Mech3;

/// <summary>
/// Enhanced Graphics only: the placed world's static opaque surfaces drawn as one mesh per group
/// instead of one draw per node. A group shares a node frame, material, draw-order bias, zone layer and
/// shadow setting. Its mesh stands at that exact frame with the members' committed vertices unchanged,
/// so every vertex reaches the GPU as it did. A member keeps its mesh instance, which draws a residual
/// of the surfaces left out (blends, billboards) boxed as the whole mesh. A member is a visible node no
/// name query has handed out (<see cref="AnimRuntime.ClaimedNodes"/>). A later claim, a visibility
/// change, <see cref="Release"/> or a sweep-found move puts it back on its own mesh.
/// </summary>
public sealed partial class WorldMerge : Node3D
{
    // Members the transform sweep checks per frame. The claim and visibility hooks act at once; this
    // is the backstop for a writer that reaches a node without a name query.
    private const int SweepPerFrame = 64;

    private static readonly StringName[] DefaultedParams =
    {
        "csky_fog_on", "csky_light_fade", SceneBuilder.OpacityParam,
        SceneBuilder.TintParam, SceneBuilder.PhotoEyeParam,
    };

    private static readonly StringName NodeBiasParam = "node_bias";

    private readonly Node3D _world;
    private readonly SceneBuilder _scene;
    private readonly AnimRuntime? _runtime;
    private readonly Func<IEnumerable<Node3D>> _extraClaims;
    private readonly Dictionary<ulong, Member> _members = new();
    private readonly List<Group> _groups = new();
    private readonly HashSet<ulong> _covering = new();
    private readonly Dictionary<(Mesh Mesh, ulong Mask), ArrayMesh> _residuals = new();
    private readonly Dictionary<(Mesh Mesh, int Surface), SurfaceData> _arrays = new();
    private readonly List<Member> _sweepOrder = new();
    private int _sweep;

    // True while the faithful path draws every node itself but the merge is kept for a switch back.
    private bool _parked;

    /// <summary>Holds the world's merge, empty until <see cref="Follow"/> asks for one.
    /// <paramref name="extraClaims"/> names subtrees that never merge beyond what the runtime has
    /// claimed. ⚠ Keep <paramref name="world"/> at the identity, or a merged frame rounds off its
    /// members' own.</summary>
    public WorldMerge(Node3D world, SceneBuilder scene, AnimRuntime? runtime, Func<IEnumerable<Node3D>> extraClaims)
    {
        Name = "world_merge";
        _world = world;
        _scene = scene;
        _runtime = runtime;
        _extraClaims = extraClaims;
        if (_runtime != null)
            _runtime.NodeClaimed += OnClaimed;
    }

    /// <summary>Gets a value indicating whether the merged meshes draw.</summary>
    public bool Merged { get; private set; }

    /// <summary>Gets how many merged meshes there are.</summary>
    public int GroupCount => _groups.Count;

    /// <summary>Gets how many nodes draw a residual of their mesh.</summary>
    public int MemberCount => _members.Count;

    /// <summary>Gets how many surfaces the merged meshes carry.</summary>
    public int SurfaceCount
    {
        get
        {
            int n = 0;
            foreach (var group in _groups)
                n += group.Parts.Count;
            return n;
        }
    }

    /// <summary>Gets how many members have been released since the merge was built.</summary>
    public int Released { get; private set; }

    /// <summary>Gets each member's own mesh instance. Read by the suites.</summary>
    internal IEnumerable<MeshInstance3D> MemberMeshes
    {
        get
        {
            foreach (var member in _members.Values)
                yield return member.Mesh;
        }
    }

    /// <summary>Gets the runtime whose claims release members. Read by the suites.</summary>
    internal AnimRuntime? Runtime => _runtime;

    /// <summary>Puts every member under <paramref name="node"/>, itself included, back on its own
    /// mesh. A writer that reaches a node outside a name query calls this before it writes.</summary>
    public static void Release(Node3D node)
    {
        for (Node? n = node; n != null; n = n.GetParent())
        {
            foreach (var child in n.GetChildren())
            {
                if (child is WorldMerge merge)
                {
                    merge.ReleaseSubtree(node, "released");
                    return;
                }
            }
        }
    }

    /// <summary>Merges under Enhanced Graphics and restores every node's own mesh on the faithful
    /// path. The live graphics switch calls it.</summary>
    public void Follow(bool enhanced)
    {
        if (enhanced)
            Merge();
        else
            Unmerge();
    }

    public override void _Process(double delta) => Sweep();

    public override void _ExitTree()
    {
        if (_runtime != null)
            _runtime.NodeClaimed -= OnClaimed;
    }

    public override void _Notification(int what)
    {
        // A parked merge's meshes are out of the tree, so nothing else frees them.
        if (what != NotificationPredelete)
            return;
        foreach (var group in _groups)
        {
            if (group.Node is { } node && IsInstanceValid(node) && node.GetParent() == null)
                node.Free();
        }
    }

    /// <summary>Builds the merge over the world as it stands, or draws the kept one again after a
    /// switch away. A no-op while one draws.</summary>
    public void Merge()
    {
        if (Merged)
            return;
        if (_parked)
        {
            Resume();
            return;
        }
        long start = System.Diagnostics.Stopwatch.GetTimestamp();
        Merged = true;
        Released = 0;
        var blocked = new HashSet<ulong>();
        foreach (var node in _runtime?.ClaimedNodes() ?? new List<Node3D>())
            blocked.Add(node.GetInstanceId());
        foreach (var node in _extraClaims())
        {
            if (IsInstanceValid(node))
                blocked.Add(node.GetInstanceId());
        }
        var toWorld = _world.GlobalTransform.AffineInverse();
        var candidates = new List<(Member Member, int Surface, GroupKey Key)>();
        var census = new Census();
        Collect(_world, blocked, false, toWorld, candidates, census);

        var byKey = new Dictionary<GroupKey, List<(Member Member, int Surface)>>();
        foreach (var (member, surface, key) in candidates)
        {
            if (!byKey.TryGetValue(key, out var parts))
                byKey[key] = parts = new List<(Member, int)>();
            parts.Add((member, surface));
        }
        foreach (var (key, parts) in byKey)
        {
            // A surface alone in its group would draw as often merged as not.
            if (parts.Count < 2)
            {
                census.Singletons++;
                continue;
            }
            var group = new Group(key, parts);
            foreach (var (member, surface) in parts)
            {
                member.Merged |= 1UL << surface;
                member.Groups.Add(group);
            }
            Build(group);
            _groups.Add(group);
        }
        foreach (var candidate in candidates)
        {
            var member = candidate.Member;
            if (member.Merged == 0 || _members.ContainsKey(member.Id))
                continue;
            _members[member.Id] = member;
            _sweepOrder.Add(member);
            member.Residual = Residual(member.Full, member.Merged);
            member.Mesh.Mesh = member.Residual;
            var onVisibility = Callable.From(() => ReleaseMember(member, "visibility"));
            member.OnVisibility = onVisibility;
            member.Mesh.Connect(Node3D.SignalName.VisibilityChanged, onVisibility);
            for (Node? n = member.Mesh; n != null && n != _world; n = n.GetParent())
                _covering.Add(n.GetInstanceId());
        }
        _arrays.Clear();
        double ms = System.Diagnostics.Stopwatch.GetElapsedTime(start).TotalMilliseconds;
        Log.Info("world", $"world merge: {SurfaceCount} surface(s) of {_members.Count} node(s) into {_groups.Count} mesh(es), {ms:0.0} ms; left alone: {census.Claimed} claimed, {census.Hidden} hidden subtree(s), {census.Other} other, {census.NotOpaque} blended or billboard surface(s), {census.Singletons} alone in their group");
    }

    /// <summary>Puts every member back on its own mesh and takes the merged meshes out of the tree.
    /// They are kept, releases and all, so a switch back to Enhanced draws them without a rebuild.</summary>
    public void Unmerge()
    {
        if (!Merged)
            return;
        foreach (var member in _members.Values)
        {
            if (IsInstanceValid(member.Mesh) && member.Mesh.Mesh == member.Residual)
                member.Mesh.Mesh = member.Full;
        }
        // Out of the tree rather than hidden, so the faithful world holds what a fresh build holds.
        foreach (var group in _groups)
        {
            if (group.Node is { } node && node.GetParent() == this)
                RemoveChild(node);
        }
        Merged = false;
        _parked = true;
    }

    // Whether every instance uniform but node_bias reads its declared default, which is all a merged
    // instance can carry. The defaults are csky_instance_uniforms.gdshaderinc's.
    private static bool Defaulted(GeometryInstance3D instance)
    {
        foreach (var name in DefaultedParams)
        {
            var value = instance.GetInstanceShaderParameter(name);
            bool isDefault = value.VariantType switch
            {
                Variant.Type.Nil => true,
                Variant.Type.Float => Mathf.IsEqualApprox(value.AsSingle(), 1f),
                Variant.Type.Color => value.AsColor() == new Color(0f, 0f, 0f, 0f),
                Variant.Type.Vector4 => value.AsVector4() == Vector4.Zero,
                _ => false,
            };
            if (!isDefault)
                return false;
        }
        return true;
    }

    private static T[] Unindex<T>(T[] values, int[] index)
    {
        if (values.Length == 0)
            return values;
        var flat = new T[index.Length];
        for (int i = 0; i < index.Length; i++)
            flat[i] = values[index[i]];
        return flat;
    }

    // Hidden at once, since a queued free still draws this frame.
    private static void Drop(Group group)
    {
        if (group.Node is not { } node)
            return;
        group.Node = null;
        if (node.GetParent() == null)
        {
            node.Free();
            return;
        }
        node.Visible = false;
        node.QueueFree();
    }

    private static string NameOf(Member member) =>
        IsInstanceValid(member.Mesh) && member.Mesh.GetParent() is Node3D owner && owner.HasMeta(AnimRuntime.NameMeta)
            ? owner.GetMeta(AnimRuntime.NameMeta).AsString() : "?";

    // A switch back: a member changed while the faithful path drew it is released, the rest draw
    // their residuals again.
    private void Resume()
    {
        _parked = false;
        Merged = true;
        var toWorld = _world.GlobalTransform.AffineInverse();
        var changed = new List<Member>();
        foreach (var member in _members.Values)
        {
            if (!IsInstanceValid(member.Mesh) || member.Mesh.Mesh != member.Full || !member.Mesh.IsVisibleInTree()
                || member.Mesh.MaterialOverride != null
                || toWorld * member.Mesh.GlobalTransform != member.World)
                changed.Add(member);
        }
        if (changed.Count > 0)
            ReleaseMembers(changed, "changed while drawn on its own");
        foreach (var member in _members.Values)
            member.Mesh.Mesh = member.Residual;
        foreach (var group in _groups)
        {
            if (group.Node is { } node && node.GetParent() == null)
                AddChild(node);
        }
    }

    private void Collect(Node node, HashSet<ulong> blocked, bool claimed, Transform3D toWorld,
        List<(Member, int, GroupKey)> into, Census census)
    {
        if (node == this || node is MultiMeshInstance3D)
            return;
        if (node is Node3D n3d)
        {
            if (!n3d.Visible)
            {
                census.Hidden++;
                return;
            }
            // The merged meshes hang under the world root, so a claim on the root alone binds nothing.
            claimed |= n3d != _world && blocked.Contains(n3d.GetInstanceId());
        }
        if (node is MeshInstance3D mi && mi.Name == "mesh" && mi.GetParent() is Node3D owner
            && owner.HasMeta(AnimRuntime.IndexMeta))
        {
            if (claimed)
                census.Claimed++;
            else
                Consider(mi, toWorld, into, census);
        }
        foreach (var child in node.GetChildren())
            Collect(child, blocked, claimed, toWorld, into, census);
    }

    private void Consider(MeshInstance3D mi, Transform3D toWorld, List<(Member, int, GroupKey)> into, Census census)
    {
        if (mi.Mesh is not ArrayMesh full || mi.MaterialOverride != null || mi.MaterialOverlay != null
            || full.GetSurfaceCount() > 64 || !Defaulted(mi))
        {
            census.Other++;
            return;
        }
        for (int s = 0; s < mi.GetSurfaceOverrideMaterialCount(); s++)
        {
            if (mi.GetSurfaceOverrideMaterial(s) != null)
            {
                census.Other++;
                return;
            }
        }
        var frame = toWorld * mi.GlobalTransform;
        var bias = mi.GetInstanceShaderParameter(NodeBiasParam);
        float nodeBias = bias.VariantType == Variant.Type.Float ? bias.AsSingle() : 0f;
        Member? member = null;
        for (int s = 0; s < full.GetSurfaceCount(); s++)
        {
            var material = full.SurfaceGetMaterial(s);
            if (full.SurfaceGetPrimitiveType(s) != Mesh.PrimitiveType.Triangles || !_scene.IsStaticOpaque(material))
            {
                census.NotOpaque++;
                continue;
            }
            member ??= new Member(mi, full, frame);
            into.Add((member, s, new GroupKey(frame, material!, nodeBias, mi.Layers, mi.CastShadow,
                full.SurfaceGetFormat(s))));
        }
    }

    // The group's surfaces concatenated as committed, at the frame they share.
    private void Build(Group group)
    {
        int count = 0;
        foreach (var (member, surface) in group.Parts)
            count += Arrays(member.Full, surface).Vertices.Length;
        var vertices = new Vector3[count];
        var normals = new Vector3[count];
        Color[]? colors = null;
        Vector2[]? uvs = null;
        int at = 0;
        foreach (var (member, surface) in group.Parts)
        {
            var (v, n, c, uv) = Arrays(member.Full, surface);
            Array.Copy(v, 0, vertices, at, v.Length);
            Array.Copy(n, 0, normals, at, n.Length);
            if (c.Length > 0)
            {
                colors ??= new Color[count];
                Array.Copy(c, 0, colors, at, c.Length);
            }
            if (uv.Length > 0)
            {
                uvs ??= new Vector2[count];
                Array.Copy(uv, 0, uvs, at, uv.Length);
            }
            at += v.Length;
        }
        var merged = new Godot.Collections.Array();
        merged.Resize((int)Mesh.ArrayType.Max);
        merged[(int)Mesh.ArrayType.Vertex] = vertices;
        merged[(int)Mesh.ArrayType.Normal] = normals;
        if (colors != null)
            merged[(int)Mesh.ArrayType.Color] = colors;
        if (uvs != null)
            merged[(int)Mesh.ArrayType.TexUV] = uvs;
        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, merged);
        mesh.SurfaceSetMaterial(0, group.Key.Material);
        if (group.Node == null)
        {
            group.Node = new MeshInstance3D
            {
                Name = "merged",
                Layers = group.Key.Layers,
                CastShadow = group.Key.Shadow,
                Transform = group.Key.Frame,
            };
            group.Node.SetInstanceShaderParameter(NodeBiasParam, group.Key.NodeBias);
            if (Merged)
                AddChild(group.Node);
        }
        group.Node.Mesh = mesh;
    }

    // One surface's attributes, unindexed, kept while a merge builds, since one model's mesh is
    // shared by every node placing it.
    private SurfaceData Arrays(ArrayMesh mesh, int surface)
    {
        if (_arrays.TryGetValue((mesh, surface), out var data))
            return data;
        var arrays = _scene.SurfaceArrays(mesh, surface);
        var v = arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
        var n = arrays[(int)Mesh.ArrayType.Normal].AsVector3Array();
        var c = arrays[(int)Mesh.ArrayType.Color].AsColorArray();
        var uv = arrays[(int)Mesh.ArrayType.TexUV].AsVector2Array();
        var index = arrays[(int)Mesh.ArrayType.Index].AsInt32Array();
        if (index.Length > 0)
        {
            v = Unindex(v, index);
            n = Unindex(n, index);
            c = Unindex(c, index);
            uv = Unindex(uv, index);
        }
        data = new SurfaceData(v, n, c, uv);
        _arrays[(mesh, surface)] = data;
        return data;
    }

    // The member's mesh less its merged surfaces, boxed as the whole mesh. Godot sorts a blended
    // surface by its instance's box centre and culls by the box, so both stay where they were.
    private ArrayMesh Residual(ArrayMesh full, ulong merged)
    {
        if (_residuals.TryGetValue((full, merged), out var cached))
            return cached;
        var residual = new ArrayMesh { CustomAabb = full.GetAabb() };
        for (int s = 0; s < full.GetSurfaceCount(); s++)
        {
            if ((merged & (1UL << s)) != 0)
                continue;
            residual.AddSurfaceFromArrays(full.SurfaceGetPrimitiveType(s), _scene.SurfaceArrays(full, s));
            residual.SurfaceSetMaterial(residual.GetSurfaceCount() - 1, full.SurfaceGetMaterial(s));
        }
        _residuals[(full, merged)] = residual;
        return residual;
    }

    private void OnClaimed(Node3D node)
    {
        // The merged meshes hang under the world root, so whatever is written to it reaches them too.
        if (node == _world)
            return;
        if ((Merged || _parked) && _covering.Contains(node.GetInstanceId()))
            ReleaseSubtree(node, "claimed");
    }

    private void ReleaseSubtree(Node3D node, string why)
    {
        if (!Merged && !_parked)
            return;
        var found = new List<Member>();
        void Walk(Node n)
        {
            if (n is MeshInstance3D mi && _members.TryGetValue(mi.GetInstanceId(), out var member))
                found.Add(member);
            if (n is Node3D && n != node && !_covering.Contains(n.GetInstanceId()))
                return;
            foreach (var child in n.GetChildren())
                Walk(child);
        }
        Walk(node);
        if (found.Count > 0)
            ReleaseMembers(found, why);
    }

    private void ReleaseMember(Member member, string why) => ReleaseMembers(new List<Member> { member }, why);

    private void ReleaseMembers(List<Member> members, string why)
    {
        var dirty = new HashSet<Group>();
        foreach (var member in members)
        {
            if (!_members.Remove(member.Id))
                continue;
            Released++;
            Restore(member);
            foreach (var group in member.Groups)
            {
                group.Parts.RemoveAll(p => p.Member == member);
                dirty.Add(group);
            }
            if (Released <= 12)
                Log.Info("world", $"world merge: '{NameOf(member)}' {why}, drawn on its own again");
        }
        foreach (var group in dirty)
        {
            if (group.Parts.Count == 0)
            {
                Drop(group);
                _groups.Remove(group);
            }
            else
            {
                Build(group);
            }
        }
        _sweepOrder.RemoveAll(m => !_members.ContainsKey(m.Id));
        _arrays.Clear();
    }

    private void Restore(Member member)
    {
        if (!IsInstanceValid(member.Mesh))
            return;
        if (member.OnVisibility is { } call && member.Mesh.IsConnected(Node3D.SignalName.VisibilityChanged, call))
            member.Mesh.Disconnect(Node3D.SignalName.VisibilityChanged, call);
        // A writer that swapped the mesh itself keeps its own.
        if (member.Mesh.Mesh == member.Residual)
            member.Mesh.Mesh = member.Full;
    }

    private void Sweep()
    {
        if (!Merged || _sweepOrder.Count == 0)
            return;
        var toWorld = _world.GlobalTransform.AffineInverse();
        List<(Member, string)>? changed = null;
        for (int i = 0; i < SweepPerFrame && i < _sweepOrder.Count; i++)
        {
            _sweep = (_sweep + 1) % _sweepOrder.Count;
            var member = _sweepOrder[_sweep];
            string? why = !IsInstanceValid(member.Mesh) ? "freed"
                : member.Mesh.Mesh != member.Residual ? "given a mesh"
                : member.Mesh.MaterialOverride != null ? "given a material"
                : toWorld * member.Mesh.GlobalTransform != member.World ? "moved"
                : null;
            if (why != null)
                (changed ??= new List<(Member, string)>()).Add((member, why));
        }
        if (changed == null)
            return;
        foreach (var (member, why) in changed)
        {
            Log.Warn("world", $"world merge: '{NameOf(member)}' was {why} after the merge without a name query; released");
            ReleaseMember(member, why);
        }
    }

    private readonly record struct SurfaceData(Vector3[] Vertices, Vector3[] Normals, Color[] Colors, Vector2[] Uvs);

    // Frame is compared exactly, so a merged mesh stands where every member's own instance stood.
    private readonly record struct GroupKey(Transform3D Frame, Material Material, float NodeBias, uint Layers,
        GeometryInstance3D.ShadowCastingSetting Shadow, Mesh.ArrayFormat Format);

    private sealed class Member(MeshInstance3D mesh, ArrayMesh full, Transform3D world)
    {
        public MeshInstance3D Mesh { get; } = mesh;

        public ulong Id { get; } = mesh.GetInstanceId();

        public ArrayMesh Full { get; } = full;

        public Transform3D World { get; } = world;

        public ArrayMesh? Residual { get; set; }

        public ulong Merged { get; set; }

        public List<Group> Groups { get; } = new();

        public Callable? OnVisibility { get; set; }
    }

    private sealed class Group(GroupKey key, List<(Member Member, int Surface)> parts)
    {
        public GroupKey Key { get; } = key;

        public List<(Member Member, int Surface)> Parts { get; } = parts;

        public MeshInstance3D? Node { get; set; }
    }

    private sealed class Census
    {
        public int Claimed { get; set; }

        public int Hidden { get; set; }

        public int Other { get; set; }

        public int NotOpaque { get; set; }

        public int Singletons { get; set; }
    }
}
