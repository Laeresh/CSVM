using CSVM.Flight.Airframe;
using CSVM.Flight.Hud;
using CSVM.Mech3;
using CSVM.Utils;
using Godot;

namespace CSVM.Session.World;

/// <summary>
/// The carried Capture the Flag flag under <see cref="GraphicsMode.Enhanced"/>: a banner towed on a
/// line behind the carrier. The Original presentation keeps the billboard. A chain
/// of points trails the tow point, each held one segment behind the point ahead, so the banner lags
/// into a turn. Its vertex shader ripples the billboard's own art faster with airspeed. It reads the
/// carrier's drawn pose after the flight rigs, so a replicated carrier tows it as smoothly as it draws.
/// Rule: docs/org/multiplayer-ctf.md, "Towed banner".
/// </summary>
public sealed partial class FlagBanner : Node3D
{
    /// <summary>The tow line and the banner end to end, the chain's whole length in metres.</summary>
    internal const float TotalLength = TowLength + BannerLength;

    // Remake-only rule: the original tows nothing (docs/org/multiplayer-ctf.md, "Towed banner").
    private const float TowLength = 14f;
    private const int TowSegments = 4;
    private const float BannerLength = 9f;
    private const float BannerHeight = 3f;
    private const int BannerSegments = 16;
    private const float RopeHalfWidth = 0.04f;
    private const float RippleAmplitude = 0.4f;
    private const float RippleWaves = 1.5f;
    private const float RippleRadPerMetre = 0.25f;
    private const float MinRippleSpeed = 20f;
    private const float RightingDegPerS = 90f;

    // The leading edge rides its pole and the free edge flaps hardest. UV.x is -1 on the tow line,
    // which takes the rope colour and no ripple. The phase arrives accumulated, never as rate times
    // a clock, since the rate moves with airspeed every frame.
    private const string ShaderCode = """
        shader_type spatial;
        render_mode unshaded, cull_disabled, shadows_disabled, fog_disabled;

        uniform sampler2D albedo_tex : source_color, filter_linear_mipmap, repeat_disable;
        uniform vec4 rope_color : source_color = vec4(0.22, 0.2, 0.17, 1.0);
        uniform float phase = 0.0;
        uniform float amplitude;
        uniform float waves;

        #include "res://shaders/csky_atmosphere.gdshaderinc"

        void vertex() {
            float u = max(UV.x, 0.0);
            VERTEX += NORMAL * amplitude * u * sin(u * waves * TAU - phase);
        }

        void fragment() {
            vec4 col = UV.x < 0.0 ? rope_color : texture(albedo_tex, UV);
            vec3 fog_world = (INV_VIEW_MATRIX * vec4(VERTEX, 1.0)).xyz;
            ALBEDO = mix(col.rgb * csky_world_light_at(CAMERA_POSITION_WORLD),
                csky_fog_color_at(CAMERA_POSITION_WORLD), csky_fog_amount(fog_world, CAMERA_POSITION_WORLD));
            ALPHA = col.a;
            ALPHA_SCISSOR_THRESHOLD = 0.5;
        }
        """;

    private const int Points = TowSegments + BannerSegments + 1;

    private readonly Node3D _billboard;
    private readonly Texture2D? _art;
    private readonly Vector3[] _points = new Vector3[Points];
    private readonly Vector3[] _sides = new Vector3[Points];
    private Node3D? _mount;
    private FlightController? _pilot;
    private GeometryInstance3D? _hull;
    private MeshInstance3D? _drawn;
    private ImmediateMesh? _mesh;
    private ShaderMaterial? _material;
    private bool _hullSearched;
    private bool _primed;
    private bool _billboardShown = true;

    private FlagBanner(Node3D billboard, Texture2D? art)
    {
        _billboard = billboard;
        _art = art;
        Name = "flag_banner";
        TopLevel = true;
        ProcessPriority = TargetHud.AfterFlightRigs;
    }

    /// <summary>Whether the banner drew on its last frame.</summary>
    internal bool Drawn => _drawn is { Visible: true };

    /// <summary>The texture the banner shows, the billboard's own, or null where it has none.</summary>
    internal Texture2D? Art => _art;

    /// <summary>The ripple's accumulated phase in radians, wrapped to one turn.</summary>
    internal float Phase { get; private set; }

    /// <summary>The chain from the tow point to the banner's trailing edge, in world space.</summary>
    internal System.ReadOnlySpan<Vector3> Chain => _points;

    /// <summary>Each chain point's side, the unit axis across the banner the ripple pushes along.</summary>
    internal System.ReadOnlySpan<Vector3> Sides => _sides;

    /// <summary>The visual layers the banner was drawn on last, its carrier's cloud-band zone.</summary>
    internal uint DrawnLayers => _drawn?.Layers ?? 0u;

    /// <summary>Whether the billboard's geometry is drawn, which it is unless the banner stands in.</summary>
    internal bool BillboardShown => _billboardShown;

    /// <summary>The node the banner is towed from, null while the flag is not carried.</summary>
    internal Node3D? Mount => _mount;

    /// <summary>The banner's material, null until it first draws.</summary>
    internal ShaderMaterial? Material => _material;

    public override void _Process(double delta) => Draw(GameClock.Current?.FrameDt ?? (float)delta);

    /// <summary>The banner for <paramref name="billboard"/>, the carried flag node, taking the
    /// texture of its first textured surface. The caller adds it to the world.</summary>
    internal static FlagBanner For(Node3D billboard) => new(billboard, ArtOf(billboard));

    /// <summary>Hang the banner on <paramref name="mount"/>, the node the flag hangs under on the
    /// carrier, or take it down with null. It is laid out straight behind on its next frame.</summary>
    internal void Tow(Node3D? mount, FlightController? pilot)
    {
        _mount = mount;
        _pilot = pilot;
        _hull = null;
        _hullSearched = false;
        _primed = false;
    }

    /// <summary>One frame. Under Enhanced with the flag carried and drawn, the banner follows the
    /// mount's drawn pose in place of the billboard. Otherwise the billboard draws alone.
    /// <paramref name="dt"/> is the sim time the frame advanced, which the ripple runs on.</summary>
    internal void Draw(float dt)
    {
        bool towing = GraphicsMode.Enhanced && _mount is { } mount && IsInstanceValid(mount)
            && IsInstanceValid(_billboard) && _billboard.IsVisibleInTree();
        ShowBillboard(!towing);
        if (!towing)
        {
            if (_drawn != null)
            {
                _drawn.Visible = false;
            }

            _primed = false;
            return;
        }

        Build();
        _drawn!.Visible = true;
        _drawn.Layers = ZoneLayers();
        Follow(_mount!.GlobalTransform, _pilot?.WorldVelocity.Length() ?? 0f, dt);
    }

    /// <summary>The chain moved to a tow pose and the ripple advanced by a speed over a step. A tow
    /// point that jumped further than the whole banner is a teleport, laid out straight behind again.
    /// </summary>
    internal void Follow(Transform3D tow, float speed, float dt)
    {
        var head = tow.Origin;
        if (!_primed || head.DistanceSquaredTo(_points[0]) > TotalLength * TotalLength)
        {
            Prime(tow);
        }

        _points[0] = head;
        for (int i = 1; i < Points; i++)
        {
            var reach = _points[i] - _points[i - 1];
            float length = reach.Length();
            var along = length > 1e-4f ? reach / length : tow.Basis.Z.Normalized();
            _points[i] = _points[i - 1] + (along * SegmentLength(i));
        }

        Orient(dt);
        Phase = Mathf.PosMod(Phase + (Mathf.Max(speed, MinRippleSpeed) * RippleRadPerMetre * dt), Mathf.Tau);
        if (_mesh != null)
        {
            Rebuild(head);
        }
    }

    /// <summary>The way to the art's top edge at a chain point, square to the chain and its side.</summary>
    internal Vector3 TopOf(int point) => Tangent(point).Cross(_sides[point]);

    // The first surface's albedo texture under the billboard, the art every shipped sprite binds.
    private static Texture2D? ArtOf(Node3D billboard)
    {
        foreach (var node in billboard.FindChildren("*", nameof(MeshInstance3D), true, false))
        {
            var mesh = (MeshInstance3D)node;
            for (int s = 0; s < (mesh.Mesh?.GetSurfaceCount() ?? 0); s++)
            {
                if (mesh.GetActiveMaterial(s) is ShaderMaterial material
                    && material.GetShaderParameter("albedo_tex").AsGodotObject() is Texture2D art)
                {
                    return art;
                }
            }
        }

        return null;
    }

    private static float SegmentLength(int point) =>
        point <= TowSegments ? TowLength / TowSegments : BannerLength / BannerSegments;

    // Hides a surface the banner stands in for, or shows again one it hid, never one hidden by
    // anything else.
    private static void SetDrawn(GeometryInstance3D surface, bool drawn)
    {
        const string Hidden = "flag_banner_hidden";
        if (!drawn && surface.Visible)
        {
            surface.SetMeta(Hidden, true);
            surface.Visible = false;
        }
        else if (drawn && surface.HasMeta(Hidden))
        {
            surface.RemoveMeta(Hidden);
            surface.Visible = true;
        }
    }

    // The surfaces under the flag node, never the node itself, whose visibility means carried or
    // home. Visible rather than layers, which the cloud-band gate rewrites on a carrier's meshes.
    private void ShowBillboard(bool shown)
    {
        if (shown == _billboardShown || !IsInstanceValid(_billboard))
        {
            return;
        }

        _billboardShown = shown;
        foreach (var node in _billboard.FindChildren("*", nameof(GeometryInstance3D), true, false))
        {
            SetDrawn((GeometryInstance3D)node, shown);
        }
    }

    // The carrier's cloud-band zone, which ObjectZoneGate writes on its gated meshes, so a band that
    // hides the aircraft from a camera hides its banner too. Layer 1, the shared world's, otherwise.
    private uint ZoneLayers()
    {
        if (_hull != null && !IsInstanceValid(_hull))
        {
            _hull = null;
            _hullSearched = false;
        }

        // Once per tow: a local carrier's airframe sits on its seat's own layer and has no match.
        if (!_hullSearched)
        {
            _hullSearched = true;
            foreach (var node in _pilot?.PlaneModel?.FindChildren("*", nameof(GeometryInstance3D), true, false) ?? new())
            {
                if (node is GeometryInstance3D surface && !_billboard.IsAncestorOf(surface)
                    && (surface.Layers & (ZoneGate.LayerBand | 1u)) != 0)
                {
                    _hull = surface;
                    break;
                }
            }
        }

        uint zone = (_hull?.Layers ?? 0u) & ZoneGate.LayerBand;
        return zone != 0 ? zone : 1u;
    }

    // Point 0's side is carried from its own last frame, every other point's from the point ahead.
    // No side flips between frames or neighbours, and the carrier's roll never reaches them. Point 0
    // alone turns toward the face whose top edge is up, at a bounded rate fading near vertical.
    // ⚠ Do not pull the later points toward a face of their own: through a righting the trailing
    // edge then flips in one frame.
    private void Orient(float dt)
    {
        for (int i = 0; i < Points; i++)
        {
            var tangent = Tangent(i);
            var ahead = _sides[System.Math.Max(i - 1, 0)];
            var side = ahead - (tangent * ahead.Dot(tangent));
            side = side.LengthSquared() > 1e-8f ? side.Normalized() : ahead;
            var upright = Vector3.Up.Cross(tangent);
            float flat = upright.Length();
            if (i == 0 && flat > 1e-4f)
            {
                upright /= flat;
                float off = Mathf.Atan2(tangent.Dot(side.Cross(upright)), side.Dot(upright));
                float most = Mathf.DegToRad(RightingDegPerS) * dt * Mathf.SmoothStep(0.1f, 0.4f, flat);
                side = side.Rotated(tangent, Mathf.Clamp(off, -most, most)).Normalized();
            }

            _sides[i] = side;
        }
    }

    private Vector3 Tangent(int i) =>
        (_points[System.Math.Min(i + 1, Points - 1)] - _points[System.Math.Max(i - 1, 0)]).Normalized();

    // Laid out straight behind with its top edge up, or across the carrier's right where the chain
    // stands vertical. Orient carries that side on from here.
    private void Prime(Transform3D tow)
    {
        var back = tow.Basis.Z.Normalized();
        var upright = Vector3.Up.Cross(back);
        _sides[0] = upright.LengthSquared() > 1e-8f ? upright.Normalized() : tow.Basis.X.Normalized();
        _points[0] = tow.Origin;
        for (int i = 1; i < Points; i++)
        {
            _points[i] = _points[i - 1] + (back * SegmentLength(i));
        }

        _primed = true;
    }

    // Built on the first frame it draws, so the Original presentation compiles no banner shader.
    private void Build()
    {
        if (_drawn != null)
        {
            return;
        }

        _material = new ShaderMaterial { Shader = ShaderTwins.Pooled(ShaderCode, "flag-banner") };
        if (_art != null)
        {
            _material.SetShaderParameter("albedo_tex", _art);
        }

        _material.SetShaderParameter("amplitude", RippleAmplitude);
        _material.SetShaderParameter("waves", RippleWaves);
        _mesh = new ImmediateMesh();
        _drawn = new MeshInstance3D
        {
            Name = "banner",
            Mesh = _mesh,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            ExtraCullMargin = RippleAmplitude,
        };
        AddChild(_drawn);
    }

    // The tow line and the banner as two strips about the node, which stands on the tow point.
    private void Rebuild(Vector3 head)
    {
        GlobalTransform = new Transform3D(Basis.Identity, head);
        _mesh!.ClearSurfaces();
        Strip(0, TowSegments, RopeHalfWidth, rope: true);
        Strip(TowSegments, Points - 1, BannerHeight * 0.5f, rope: false);
        _material!.SetShaderParameter("phase", Phase);
    }

    // One strip across the chain, its width square to the point's side. The normal is the side, the
    // axis the ripple pushes along.
    private void Strip(int from, int to, float halfWidth, bool rope)
    {
        var head = _points[0];
        _mesh!.SurfaceBegin(Mesh.PrimitiveType.TriangleStrip, _material);
        for (int i = from; i <= to; i++)
        {
            var side = _sides[i];
            var up = TopOf(i) * halfWidth;
            float u = rope ? -1f : (float)(i - from) / (to - from);
            var at = _points[i] - head;
            _mesh.SurfaceSetNormal(side);
            _mesh.SurfaceSetUV(new Vector2(u, 0f));
            _mesh.SurfaceAddVertex(at + up);
            _mesh.SurfaceSetNormal(side);
            _mesh.SurfaceSetUV(new Vector2(u, 1f));
            _mesh.SurfaceAddVertex(at - up);
        }

        _mesh.SurfaceEnd();
    }
}
