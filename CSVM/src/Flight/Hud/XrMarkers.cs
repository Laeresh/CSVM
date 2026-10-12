using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using CSVM.Flight.Camera;
using CSVM.Flight.Modes;
using CSVM.UI.Boards;
using CSVM.Utils;
using Godot;

namespace CSVM.Flight.Hud;

/// <summary>
/// The flight HUD's world markers in a stereo session: the gun pipper, the selection's box and label,
/// and every flight tag (hostile, race, debug). A stereo
/// viewport draws no canvas layer, so the flat HUD's own code draws each listed
/// <see cref="WorldMark"/> into a small surface. A card shows it at its world point, facing the head
/// with the seat's up, at the angle the flat pane gives it. A marker far off the view axis hangs on
/// a ring on the head instead, and the selection there carries the spyglass picture
/// (<see cref="XrSpyglassDisc"/>). Module entry: docs/architecture/Flight.md.
/// </summary>
public sealed partial class XrMarkers : Node3D
{
    /// <summary>Surface texels per flat HUD reference pixel. A reference pixel spans less than one
    /// headset pixel, so the card is drawn finer than shown and filtered down. TUNE
    /// (docs/architecture/Flight.md).</summary>
    public const float CardScale = 1.5f;

    /// <summary>How far ahead of the head the ring hangs, metres along the view axis: about the
    /// headsets' focal distance and the HUD panel's. TUNE.</summary>
    public const float RingDistanceM = 2f;

    /// <summary>The farthest a world card stands, metres, inside the camera's far plane. A marker
    /// beyond it keeps its direction and angle at this distance. Its disparity there is already
    /// zero to a hundredth of a pixel.</summary>
    public const float MaxDistanceM = 10000f;

    /// <summary>The <see cref="Placed.Key"/> of the spyglass picture's disc.</summary>
    public const string SpyglassKey = "spyglass";

    // The ring arrow's shaft in reference pixels: the flat edge marker's 5 percent inset of a pane
    // 1440 tall. That is the shaft of an arrow leaving the top or bottom edge.
    private const float RingShaftRef = 72f;

    // The gap between a ring label and its arrow's tail, reference pixels.
    private const float RingLabelGapRef = 6f;

    // A margin round every drawing on its surface, reference pixels, which holds the drop shadow.
    private const float PadRef = 4f;

    // After the XR origin is placed (2000), the flight rigs and every HUD feed.
    private const int AfterXrOrigin = 2100;

    private readonly List<Card> _world = new();
    private readonly List<Card> _ring = new();
    private readonly List<WorldMark> _marks = new();
    private readonly List<string> _lines = new();
    private readonly List<string> _scratch = new();
    private readonly List<Placed> _placed = new();
    private readonly StringBuilder _key = new();
    private XrRig? _rig;
    private Shader _worldShader = null!;
    private Shader _ringShader = null!;
    private XrSpyglassDisc _disc = null!;
    private Viewport? _filterSource;
    private int _shownWorld = -1;
    private int _shownRing = -1;
    private bool _shownDisc;

    /// <summary>What the last <see cref="Place"/> drew, in placing order. Read by the suite.</summary>
    public IReadOnlyList<Placed> Shown => _placed;

    /// <summary>The spyglass picture's disc on the ring. Read by the suite.</summary>
    public XrSpyglassDisc Disc => _disc;

    /// <summary>Builds the markers for the seat <paramref name="rig"/> flies. The caller adds the node
    /// to the session's world; with no rig it draws only what a caller hands <see cref="Place"/>.</summary>
    public static XrMarkers Build(XrRig? rig)
    {
        var markers = new XrMarkers { Name = "xr_markers", ProcessPriority = AfterXrOrigin, _rig = rig };
        markers._worldShader = new Shader { Code = CardShader(ring: false) };
        markers._ringShader = new Shader { Code = CardShader(ring: true) };
        markers._disc = XrSpyglassDisc.Build();
        markers.AddChild(markers._disc);
        return markers;
    }

    /// <summary>The spyglass's gates for <paramref name="targets"/>' selection, answered off the
    /// head's view. The picture is up while the selection hangs on the ring, armed and inside the
    /// range gate. Returns the picture to show, or null with it down.</summary>
    public static Texture2D? AnswerSpyglass(TargetHud targets, bool shows, IReadOnlyList<WorldMark> marks,
        in Transform3D head)
    {
        if (shows)
        {
            foreach (var mark in marks)
            {
                if (mark.Style != WorldMarkStyle.Selected)
                {
                    continue;
                }

                bool offView = !XrMarkerLaw.OnView(XrMarkerLaw.InHead(head.Basis, mark.At - head.Origin));
                return targets.UpdateSpyglass(mark.Target, mark.At, offView, XrMarkerLaw.SpyglassSide)
                    && targets.Picture is { } picture ? picture.GetTexture() : null;
            }
        }

        targets.ReleaseSpyglass();
        return null;
    }

    public override void _EnterTree() => _filterSource = GetTree().Root;

    public override void _Process(double delta) => Refresh();

    /// <summary>Gathers this frame's markers off the flown pane's HUD and places them for the
    /// tracked head. With no pane, or a HUD hidden (a cutscene, a crash), it draws nothing.
    /// <see cref="_Process"/>'s body.</summary>
    public void Refresh()
    {
        var pane = _rig?.Pane;
        var pilot = pane?.Controller;
        if (_rig == null || pane == null || pilot == null || !IsInstanceValid(pilot) || !IsInstanceValid(pane.Camera))
        {
            Place(Transform3D.Identity, Vector3.Up, 60f, ThemeDB.FallbackFont, Vector3.Zero, 0f, null, Array.Empty<WorldMark>());
            return;
        }

        var hud = pilot.PilotHud;
        var head = _rig.Head.GlobalTransform;
        _marks.Clear();
        Pipper? pipper = null;
        Texture2D? spyglass = null;
        Font? font = null;
        Vector3 planePos = Vector3.Zero;
        float heading = 0f;
        if (hud.Reticle is { } reticle && IsInstanceValid(reticle))
        {
            reticle.MarksInWorld = true;
            if (reticle.Active && Shows(reticle))
            {
                pipper = new Pipper(reticle.ImpactPoint, reticle.Texture);
            }
        }

        if (hud.TargetHud is { } targets && IsInstanceValid(targets))
        {
            targets.MarksInWorld = true;
            font = ChromeType.Face(targets);
            (planePos, heading) = (targets.PlanePos, targets.HeadingDeg);
            bool shows = Shows(targets);
            if (shows)
            {
                targets.WorldMarks(_marks);
            }

            spyglass = AnswerSpyglass(targets, shows, _marks, head);
        }

        Place(head, _rig.Origin.GlobalTransform.Basis.Y, pane.Camera.Fov,
            font ?? ThemeDB.FallbackFont, planePos, heading, pipper, _marks, spyglass);
    }

    /// <summary>Places this frame's cards for a head at <paramref name="head"/> in a seat whose up is
    /// <paramref name="seatUp"/>, sized for the flat view <paramref name="fovDeg"/> tall. The clock
    /// bearing of a ring label reads off <paramref name="planePos"/> and
    /// <paramref name="headingDeg"/>, as the flat edge marker's does. A selection on the ring shows
    /// <paramref name="spyglass"/> inside its arrow. Unused cards hide.</summary>
    public void Place(in Transform3D head, Vector3 seatUp, float fovDeg, Font font, Vector3 planePos,
        float headingDeg, Pipper? pipper, IReadOnlyList<WorldMark> marks, Texture2D? spyglass = null)
    {
        _placed.Clear();
        float perRef = XrMarkerLaw.TangentPerReferencePixel(fovDeg);
        int fontSize = TargetHud.MarkerFontSize(CardScale);
        int world = 0, ring = 0;
        bool disc = false;
        if (pipper is { } p)
        {
            var card = Take(_world, world++, ring: false);
            if (Resize(card, "pipper", new Vector2(ImpactReticle.RefSize, ImpactReticle.RefSize)) is { } size)
            {
                var tex = p.Texture;
                Paint(card, into => into.DrawTextureRect(tex, new Rect2(Vector2.Zero, size), false));
            }

            PlaceWorld(card, p.At, head, seatUp, perRef);
        }

        foreach (var mark in marks)
        {
            var inHead = XrMarkerLaw.InHead(head.Basis, mark.At - head.Origin);
            if (XrMarkerLaw.OnView(inHead))
            {
                var card = Take(_world, world++, ring: false);
                PaintInView(card, mark, font, fontSize);
                PlaceWorld(card, mark.At, head, seatUp, perRef);
                continue;
            }

            var dir = XrMarkerLaw.RingDirection(inHead);
            var arrow = Take(_ring, ring++, ring: true);
            PaintArrow(arrow, mark);
            var label = Take(_ring, ring++, ring: true);
            PaintLabel(label, mark, EdgeMarker.ClockHour(planePos, headingDeg, mark.At), font, fontSize);
            PlaceRing(arrow, dir, dir, Vector2.Zero, perRef);
            // The selection's picture stands inside its arrow, and its label steps past the picture.
            bool picture = !disc && spyglass != null && mark.Style == WorldMarkStyle.Selected;
            float clear = picture ? 2f * XrMarkerLaw.SpyglassRadiusRef : 0f;
            var inward = (RingLabelGapRef + clear + XrMarkerLaw.Reach(dir, label.SizeRef * 0.5f)) * -dir;
            var along = new Vector2(-dir.Y, dir.X) * (mark.Stagger * TargetHud.StaggerStepRef);
            PlaceRing(label, dir, Vector2.Right, inward + along, perRef);
            if (picture)
            {
                PlaceDisc(dir, perRef, spyglass!, mark.Color);
                disc = true;
            }
        }

        Hide(_world, world);
        Hide(_ring, ring);
        if (!disc)
        {
            _disc.Hide();
        }

        if (world != _shownWorld || ring != _shownRing || disc != _shownDisc)
        {
            _shownWorld = world;
            _shownRing = ring;
            _shownDisc = disc;
            string pipped = pipper is { } shown ? $", pipper at {head.Origin.DistanceTo(shown.At).ToString("0", CultureInfo.InvariantCulture)} m" : "";
            Log.Debug("flight", $"xr markers: {world} card(s) in the world, {ring} on the ring{pipped}{(disc ? ", the spyglass up" : "")}");
        }
    }

    private static bool Shows(CanvasItem item) =>
        item.IsVisibleInTree() && item.GetCanvasLayerNode() is not { Visible: false };

    private static void Hide(List<Card> cards, int from)
    {
        for (int i = from; i < cards.Count; i++)
        {
            if (cards[i].Quad.Visible)
            {
                cards[i].Quad.Visible = false;
                cards[i].Surface.RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled;
            }
        }
    }

    // A card's surface pictured as the canvas would draw it, from premultiplied sRGB. Four taps cross
    // the pixel's footprint for the mipmaps a viewport texture lacks. It mixes over the frame in gamma
    // space as a canvas layer does. A ring card is placed in the head's frame, so it rides the head.
    private static string CardShader(bool ring) => $$"""
        shader_type spatial;
        render_mode blend_mix, unshaded, cull_disabled, depth_draw_never, depth_test_disabled, shadows_disabled, fog_disabled;
        #include "res://shaders/csky_srgb.gdshaderinc"

        uniform sampler2D card : filter_linear, repeat_disable;
        uniform sampler2D screen_texture : hint_screen_texture, filter_nearest;
        uniform vec3 view_centre = vec3(0.0, 0.0, -2.0);
        uniform vec2 axis = vec2(1.0, 0.0);
        uniform float units = 0.001;

        void vertex() {
        {{(ring ? """
                vec2 local = (VERTEX.x * axis + VERTEX.y * vec2(-axis.y, axis.x)) * units;
                POSITION = PROJECTION_MATRIX * vec4(view_centre + vec3(local, 0.0), 1.0);
            """ : "")}}
        }

        void fragment() {
            vec2 dx = dFdx(UV) * 0.25;
            vec2 dy = dFdy(UV) * 0.25;
            vec4 c = 0.25 * (texture(card, UV - dx - dy) + texture(card, UV + dx - dy)
                + texture(card, UV - dx + dy) + texture(card, UV + dx + dy));
            vec3 straight = c.a > 0.0 ? c.rgb / c.a : vec3(0.0);
            ALBEDO = csky_srgb_to_linear(clamp(straight, 0.0, 1.0));
            ALPHA = c.a;
        {{Effects.MultiMeshEmitterRenderer.GammaMixComposite}}
        }
        """;

    private static string Html(Color c) => c.ToHtml(true);

    // A point in the head's frame on the ring at `dir`, shifted by `offsetRef` reference pixels.
    private static Vector3 RingCentre(Vector2 dir, Vector2 offsetRef, float metresPerRef)
    {
        var onRing = dir * (Mathf.Tan(Mathf.DegToRad(XrMarkerLaw.RingDeg)) * RingDistanceM);
        return new Vector3(onRing.X + (offsetRef.X * metresPerRef), onRing.Y + (offsetRef.Y * metresPerRef), -RingDistanceM);
    }

    // The card at `index`, built on first use: a transparent 2D surface, the control the flat code
    // draws into, and the quad that shows it.
    private Card Take(List<Card> cards, int index, bool ring)
    {
        if (index < cards.Count)
        {
            return cards[index];
        }

        var surface = new SubViewport
        {
            Name = $"{(ring ? "ring" : "world")}_card_{index}",
            TransparentBg = true,
            Disable3D = true,
            GuiDisableInput = true,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled,
        };
        if (_filterSource != null)
        {
            surface.CanvasItemDefaultTextureFilter = _filterSource.CanvasItemDefaultTextureFilter;
            surface.Msaa2D = _filterSource.Msaa2D;
        }

        var canvas = new CardCanvas { MouseFilter = Control.MouseFilterEnum.Ignore };
        surface.AddChild(canvas);
        AddChild(surface);
        var material = new ShaderMaterial
        {
            Shader = ring ? _ringShader : _worldShader,
            RenderPriority = ring ? HeadLockedQuads.MarkerRingPriority : HeadLockedQuads.WorldMarkerPriority,
        };
        material.SetShaderParameter("card", surface.GetTexture());
        var mesh = new QuadMesh { Size = Vector2.One };
        var quad = new MeshInstance3D
        {
            Name = surface.Name + "_quad",
            Mesh = mesh,
            MaterialOverride = material,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            // Off layer 1, which the spyglass picture draws; the flat disc shows no marker.
            Layers = SplitScreen.HeadOverlayLayer,
            Visible = false,
        };
        if (ring)
        {
            // The shader places it on the head, so its own bounds mean nothing. Big enough that no
            // camera anywhere culls it.
            quad.CustomAabb = new Aabb(new Vector3(-1e7f, -1e7f, -1e7f), new Vector3(2e7f, 2e7f, 2e7f));
        }

        AddChild(quad);
        var card = new Card(surface, canvas, quad, mesh, material);
        cards.Add(card);
        return card;
    }

    // Sizes a card for a new drawing and answers its surface size in texels. Null means the card
    // already shows `key`. `sizeRef` is the drawing's size in reference pixels.
    private Vector2? Resize(Card card, string key, Vector2 sizeRef)
    {
        if (key == card.Key)
        {
            return null;
        }

        var texels = new Vector2I(Mathf.CeilToInt(sizeRef.X * CardScale), Mathf.CeilToInt(sizeRef.Y * CardScale));
        card.Key = key;
        card.SizeRef = new Vector2(texels.X, texels.Y) / CardScale;
        card.Surface.Size = texels;
        card.Canvas.Size = new Vector2(texels.X, texels.Y);
        card.Mesh.Size = card.SizeRef;
        return new Vector2(texels.X, texels.Y);
    }

    // Draws a resized card's surface once, at CardScale with the drawing's anchor at its centre.
    private void Paint(Card card, Action<CanvasItem> paint)
    {
        card.Canvas.Paint = paint;
        card.Canvas.QueueRedraw();
        card.Surface.RenderTargetUpdateMode = SubViewport.UpdateMode.Once;
    }

    // An on-view marker as the flat pane draws it on screen. The label block always sits below the
    // box, since the headset has no pane bottom to flip it at.
    private void PaintInView(Card card, WorldMark mark, Font font, int fontSize)
    {
        _lines.Clear();
        if (mark.Style == WorldMarkStyle.Selected)
        {
            TargetHud.LabelLines(mark.Target, null, _lines);
        }
        else
        {
            _lines.Add(mark.Tag);
        }

        _key.Clear().Append("v|").Append((int)mark.Style).Append('|').Append(Html(mark.Color)).Append('|')
            .Append(mark.Boxed).Append('|').Append(fontSize);
        float halfW = 10f + PadRef;
        foreach (var line in _lines)
        {
            _key.Append('|').Append(line);
            halfW = Mathf.Max(halfW, (font.GetStringSize(line, HorizontalAlignment.Left, -1f, fontSize).X / CardScale * 0.5f) + PadRef);
        }

        float lineHalf = font.GetHeight(fontSize) / CardScale * 0.5f;
        float below = 11f + ((_lines.Count - 1) * TargetHud.LabelPitchRef) + lineHalf;
        float halfH = Mathf.Max(below, 22f + lineHalf) + PadRef;
        if (Resize(card, _key.ToString(), new Vector2(halfW, halfH) * 2f) is { } size)
        {
            var scratch = _scratch;
            Paint(card, into => TargetHud.DrawInView(into, font, mark, size * 0.5f, float.PositiveInfinity,
                CardScale, fontSize, scratch));
        }
    }

    // An off-view marker's arrow, pointing along +x from the surface's centre; the ring turns it onto
    // the bearing. It is the targeting HUD's shaft and head.
    private void PaintArrow(Card card, WorldMark mark)
    {
        var color = mark.Color;
        _key.Clear().Append("a|").Append(Html(color));
        if (Resize(card, _key.ToString(), new Vector2(RingShaftRef + PadRef, 5f + PadRef) * 2f) is not { } size)
        {
            return;
        }

        var centre = size * 0.5f;
        Paint(card, into => TargetHud.DrawArrow(into, centre + new Vector2(RingShaftRef * CardScale, 0f), centre,
            Vector2.Right, CardScale, color));
    }

    // An off-view marker's label, the flat edge label's lines centred on the surface.
    private void PaintLabel(Card card, WorldMark mark, int clockHour, Font font, int fontSize)
    {
        _lines.Clear();
        TargetHud.EdgeLines(mark, clockHour, _lines);
        _key.Clear().Append("l|").Append(Html(mark.Color)).Append('|').Append(fontSize);
        float halfW = PadRef;
        foreach (var line in _lines)
        {
            _key.Append('|').Append(line);
            halfW = Mathf.Max(halfW, (font.GetStringSize(line, HorizontalAlignment.Left, -1f, fontSize).X / CardScale * 0.5f) + PadRef);
        }

        float span = (_lines.Count - 1) * TargetHud.LabelPitchRef;
        float halfH = (span * 0.5f) + (font.GetHeight(fontSize) / CardScale * 0.5f) + PadRef;
        if (Resize(card, _key.ToString(), new Vector2(halfW, halfH) * 2f) is not { } size)
        {
            return;
        }

        var lines = new List<string>(_lines);
        var color = mark.Color;
        var first = new Vector2(size.X * 0.5f, (size.Y * 0.5f) - (span * 0.5f * CardScale));
        Paint(card, into => TargetHud.DrawLabelBlock(into, font, first, lines, color, CardScale, fontSize));
    }

    // A world card at `at`, facing the head with the seat's up, a reference pixel spanning the angle
    // the flat view gives it. Beyond MaxDistanceM it keeps its bearing and angle at that distance.
    private void PlaceWorld(Card card, Vector3 at, in Transform3D head, Vector3 seatUp, float perRef)
    {
        var eye = head.Origin;
        float distance = eye.DistanceTo(at);
        if (distance > MaxDistanceM)
        {
            at = eye + ((at - eye) * (MaxDistanceM / distance));
            distance = MaxDistanceM;
        }

        float metresPerRef = perRef * distance;
        var axes = XrMarkerLaw.Facing(at, eye, seatUp, head.Basis.Y);
        var pose = new Transform3D(new Basis(axes.X * metresPerRef, axes.Y * metresPerRef, axes.Z * metresPerRef), at);
        card.Quad.GlobalTransform = pose;
        card.Quad.Visible = true;
        _placed.Add(new Placed(card.Key, false, pose, Vector3.Zero, Vector2.Right, metresPerRef, card.SizeRef));
    }

    // A ring card on the ring at `dir`, shifted by `offsetRef` reference pixels in the view plane and
    // turned so its +x lies along `axis`.
    private void PlaceRing(Card card, Vector2 dir, Vector2 axis, Vector2 offsetRef, float perRef)
    {
        float metresPerRef = perRef * RingDistanceM;
        var centre = RingCentre(dir, offsetRef, metresPerRef);
        card.Material.SetShaderParameter("view_centre", centre);
        card.Material.SetShaderParameter("axis", axis);
        card.Material.SetShaderParameter("units", metresPerRef);
        card.Quad.Visible = true;
        _placed.Add(new Placed(card.Key, true, Transform3D.Identity, centre, axis, metresPerRef, card.SizeRef));
    }

    // The spyglass picture on the ring at `dir`, upright in the head's frame like a ring label.
    private void PlaceDisc(Vector2 dir, float perRef, Texture2D picture, Color rim)
    {
        float metresPerRef = perRef * RingDistanceM;
        float radius = XrMarkerLaw.SpyglassRadiusRef;
        var centre = RingCentre(dir, XrMarkerLaw.SpyglassOffsetRef(dir), metresPerRef);
        _disc.Show(centre, metresPerRef, radius, picture, rim);
        _placed.Add(new Placed(SpyglassKey, true, Transform3D.Identity, centre, Vector2.Right, metresPerRef,
            new Vector2(2f * radius, 2f * radius)));
    }

    /// <summary>The pipper a frame draws: the impact point and the pipper picture.</summary>
    public readonly record struct Pipper(Vector3 At, Texture2D Texture);

    /// <summary>One card a frame drew. A world card's <paramref name="Pose"/> is its global transform.
    /// Its axes carry the card's size in metres per reference pixel. A ring card's
    /// <paramref name="ViewCentre"/> and <paramref name="Axis"/> place it in the head's frame.
    /// <paramref name="SizeRef"/> is the drawing's size in reference pixels.</summary>
    public readonly record struct Placed(string Key, bool Ring, Transform3D Pose, Vector3 ViewCentre,
        Vector2 Axis, float MetresPerRef, Vector2 SizeRef);

    // One card: its surface, the control drawn into it, and the quad showing it.
    private sealed class Card
    {
        public Card(SubViewport surface, CardCanvas canvas, MeshInstance3D quad, QuadMesh mesh, ShaderMaterial material)
        {
            Surface = surface;
            Canvas = canvas;
            Quad = quad;
            Mesh = mesh;
            Material = material;
        }

        public SubViewport Surface { get; }

        public CardCanvas Canvas { get; }

        public MeshInstance3D Quad { get; }

        public QuadMesh Mesh { get; }

        public ShaderMaterial Material { get; }

        public string Key { get; set; } = string.Empty;

        public Vector2 SizeRef { get; set; }
    }

    // The control a card's surface draws: whatever the flat code paints into it.
    private sealed partial class CardCanvas : Control
    {
        public Action<CanvasItem>? Paint { get; set; }

        public override void _Draw() => Paint?.Invoke(this);
    }
}
