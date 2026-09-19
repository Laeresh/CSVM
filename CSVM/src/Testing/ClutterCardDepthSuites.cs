using System;
using System.Collections.Generic;
using CSVM.Mech3;
using Godot;

namespace CSVM.Testing;

/// <summary>Whether a clutter card is hidden by what stands between it and the eye, measured on
/// drawn pixels rather than on the material's declared flags. One off-screen pane holds C1's
/// firtree card, a second card of the same kind, and a clutter template's own ground quad, and
/// the occluder is posed once in front of the card under test and once behind it. The behind pose
/// is the control that makes the front pose meaningful: without it "nothing changed" would also
/// pass a card that drew nowhere.</summary>
internal static class ClutterCardDepthSuites
{
    private const string Chapter = "C1";

    // The pane is small on purpose: the assertion is a changed-pixel count, and the field of view
    // is fitted to the card at each range, so a card that wrongly survives moves thousands of them.
    private const int PaneEdge = 96;

    // The two cards the pane holds, in the order the MultiMesh draws them. A MultiMesh is one draw
    // call in buffer order and never sorts, so the nearer card standing first is the arrangement
    // where only its own depth can keep the farther one behind it.
    private const int NearIndex = 0;

    private const int FarIndex = 1;

    // Where an instance goes to be left out of a frame: behind the eye, so it is clipped away.
    private const float ParkZ = 50f;

    // ⚠ A SubViewport's own texture reads back as Rgb8, three bytes to the pixel and no alpha, not
    // the four a window capture gives. A comparison written for four counts three quarters of the
    // buffer and reports every frame as wholly changed.
    private const int Channels = 3;

    // Card ranges in metres. The far pair is where the report was made, a ridge about a kilometre
    // and a half out, which is also where the depth buffer's resolution is thinnest.
    private static readonly float[] Ranges = { 60f, 750f, 1500f };

    private static readonly Vector2I PaneSize = new(PaneEdge, PaneEdge);

    [Suite("clutter-card-depth",
        "a tree card is hidden by the ground quad standing between it and the eye and by the "
        + "opaque body of a nearer card of its own kind, and still draws with both of those behind "
        + "it instead: C1's firtree card rendered into an off-screen pane at three ranges, opaque "
        + "and mid-fade, asserted on changed pixel counts against each pose's own occluder frame")]
    internal static void CardDepth(TestContext ctx)
    {
        string texturesPath = SessionPaths.ChapterTextures(ctx.DataRoot, Chapter);
        string gamezPath = SessionPaths.ChapterGamez(ctx.DataRoot, Chapter);
        ctx.RequireData(texturesPath, $"{Chapter} textures");
        ctx.RequireData(gamezPath, $"{Chapter} gamez");
        ctx.RequireData(ctx.InterpPath, $"interp.json");

        var gamez = GameZ.Load(gamezPath);
        using var textures = new TextureArchive(texturesPath);
        var names = ClutterBuilder.TemplateNames(ctx.InterpPath, Chapter);
        ctx.Check(names.Count > 0, $"{Chapter} registers clutter templates count={names.Count}");
        if (names.Count == 0)
        {
            return;
        }

        var scene = new SceneBuilder(gamez, textures);
        var clutter = new ClutterBuilder(gamez, textures, scene,
            ClutterTemplateSpec.Load(SessionPaths.ChapterZrdr(ctx.DataRoot, Chapter)));
        var built = clutter.Build(names);
        ClutterBuilder.KindExport? card = null;
        foreach (var kind in clutter.ExportedKinds ?? Array.Empty<ClutterBuilder.KindExport>())
        {
            if (!kind.Solid && kind.Texture.StartsWith("firtree", StringComparison.OrdinalIgnoreCase))
            {
                card = kind;
                break;
            }
        }
        built?.Free();
        Blended(ctx, clutter);
        ctx.Check(card != null, $"{Chapter} exports a firtree card kind");
        if (card == null)
        {
            return;
        }

        // The template's own ground node: the polygon the cards are stamped onto, so the occluder
        // carries a real world surface material rather than a stand-in.
        Node3D? plate = null;
        foreach (var name in names)
        {
            if (ClutterBuilder.FindTemplateRoot(gamez, name) is not { } root
                || ClutterBuilder.FirstWithMesh(gamez, root) is not { } ground)
            {
                continue;
            }
            plate = Plate(scene.BuildSubtree(ground));
            if (plate != null)
            {
                break;
            }
        }
        ctx.Check(plate != null, $"a clutter template's ground quad builds as world geometry");
        if (plate == null)
        {
            return;
        }

        var pane = new SubViewport
        {
            Size = PaneSize,
            OwnWorld3D = true,
            World3D = new World3D(),
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
            RenderTargetClearMode = SubViewport.ClearMode.Always,
        };
        var eye = new Camera3D { Fov = 30f, Current = true, Near = 1f, Far = 4000f };
        pane.AddChild(eye);
        ctx.Host.AddChild(pane);

        var cards = Cards(card);
        try
        {
            pane.AddChild(plate);
            pane.AddChild(cards);
            Frame(eye, card, Ranges[0]);
            PosePlate(plate, eye, -Ranges[0] * 0.5f);
            Park(cards, card, NearIndex);
            Park(cards, card, FarIndex);
            plate.Visible = false;
            byte[] bare = Shot(pane);
            plate.Visible = true;
            // A world surface is one-sided, and which of the quad's two faces its winding calls the
            // front is the template's business, so the plate is turned over rather than assumed.
            if (Differing(bare, Shot(pane)) == 0)
            {
                plate.Rotate(Vector3.Right, Mathf.Pi);
            }
            foreach (float range in Ranges)
            {
                foreach (bool full in new[] { true, false })
                {
                    Frame(eye, card, range);
                    Ground(ctx, pane, eye, plate, cards, card, range, full);
                    Ahead(ctx, pane, eye, plate, cards, card, range, full);
                }
            }
        }
        finally
        {
            cards.Free();
            plate.Free();
            pane.Free();
        }
    }

    // Which card kinds took the archive's soft-alpha verdict and so draw blended. A blended card
    // that writes no depth is drawn in its MultiMesh's buffer order against every other card, which
    // is what puts a far tree over a near one, so the count belongs in the record beside the
    // measurements even where it is zero.
    private static void Blended(TestContext ctx, ClutterBuilder clutter)
    {
        var blended = new List<string>();
        foreach (var kind in clutter.ExportedKinds ?? Array.Empty<ClutterBuilder.KindExport>())
        {
            if (kind.Solid || kind.Material is not ShaderMaterial { Shader: { } shader })
            {
                continue;
            }
            if (shader.Code.Contains("blend_mix", StringComparison.Ordinal))
            {
                blended.Add(kind.Texture);
            }
        }
        string listed = blended.Count == 0 ? "-" : string.Join(",", blended);
        ctx.Note($"{Chapter} card kinds drawing blended count={blended.Count} kinds={listed}");
    }

    // The ground quad as the occluder, at one range and one fade state. The card holds still and
    // the quad moves: once at half the range, where it stands between the card and the eye, and
    // once at one and a half times it, where it does not. Each pose is shot with the card hidden
    // and again with it shown, so the counts are that pose's own difference.
    private static void Ground(TestContext ctx, SubViewport pane, Camera3D eye, Node3D plate,
        MultiMeshInstance3D cards, ClutterBuilder.KindExport card, float range, bool full)
    {
        string at = $"{range:0} m {(full ? "opaque" : "mid-fade")}";
        Park(cards, card, NearIndex);
        Place(cards, card, FarIndex, -range, full);

        PosePlate(plate, eye, -range * 1.5f);
        byte[] behindOnly = Without(pane, cards, card, FarIndex, -range, full);
        byte[] behindBoth = Shot(pane);

        PosePlate(plate, eye, -range * 0.5f);
        byte[] frontOnly = Without(pane, cards, card, FarIndex, -range, full);
        byte[] frontBoth = Shot(pane);

        plate.Visible = false;
        byte[] alone = Shot(pane);
        byte[] bare = Without(pane, cards, card, FarIndex, -range, full);
        plate.Visible = true;

        int drawnPlate = Differing(bare, frontOnly);
        int drawnCard = Differing(bare, alone);
        int hidden = Differing(frontOnly, frontBoth);
        int shown = Differing(behindOnly, behindBoth);

        ctx.Check(drawnPlate > 1000, $"the ground quad fills the pane at {at} pixels={drawnPlate}");
        ctx.Check(drawnCard > 100,
            $"the card draws with nothing in front of it at {at} pixels={drawnCard}");
        ctx.Check(shown > 100,
            $"the card with the ground quad behind it still draws at {at} pixels={shown}");
        ctx.Same(0L, hidden,
            $"the card behind the ground quad changes no pixel of it at {at} pixels={hidden}");
        ctx.Note($"{at} ground: plate={drawnPlate} card={drawnCard} behind={hidden} front={shown}");
    }

    // A nearer card of the same kind as the occluder, drawn first, with the card under test twice
    // as far away and concentric with it, so the far card's whole quad lies inside the near card's.
    // The near card's holes are not occluders, so the assertion is confined to the pixels its body
    // covers whatever is behind them, found by shooting it over two different backdrops.
    private static void Ahead(TestContext ctx, SubViewport pane, Camera3D eye, Node3D plate,
        MultiMeshInstance3D cards, ClutterBuilder.KindExport card, float range, bool full)
    {
        string at = $"{range:0} m {(full ? "opaque" : "mid-fade")}";
        PosePlate(plate, eye, -range * 2f);
        Park(cards, card, NearIndex);
        Park(cards, card, FarIndex);
        byte[] plateOnly = Shot(pane);
        plate.Visible = false;
        byte[] bare = Shot(pane);

        Place(cards, card, NearIndex, -range * 0.5f, full);
        byte[] nearOnBare = Shot(pane);
        plate.Visible = true;
        byte[] nearOnPlate = Shot(pane);

        Place(cards, card, FarIndex, -range, full);
        byte[] both = Shot(pane);

        Park(cards, card, NearIndex);
        byte[] farOnPlate = Shot(pane);

        // Opaque where the near card drew something and drew the same thing over either backdrop.
        var body = Mask(bare, nearOnBare, nearOnPlate);
        int covered = Count(body);
        int drawnFar = Differing(plateOnly, farOnPlate);
        int through = Differing(nearOnPlate, both, body);

        ctx.Check(covered > 100, $"the near card covers the pane at {at} pixels={covered}");
        ctx.Check(drawnFar > 100, $"the far card draws on its own at {at} pixels={drawnFar}");
        ctx.Same(0L, through,
            $"the far card changes no pixel the near card's body covers at {at} pixels={through}");
        ctx.Note($"{at} ahead: body={covered} far={drawnFar} through={through}");
    }

    // The same pose with one instance left out, so a frame and its own reference differ by that
    // instance alone.
    private static byte[] Without(SubViewport pane, MultiMeshInstance3D cards,
        ClutterBuilder.KindExport card, int index, float z, bool full)
    {
        Park(cards, card, index);
        byte[] shot = Shot(pane);
        Place(cards, card, index, z, full);
        return shot;
    }

    // The pane's field of view fitted to the card at one range, so the same card fills about the
    // same share of it whether it stands sixty metres out or fifteen hundred. The card's own size
    // is fixed: its shader builds the quad from the mesh about the instance's origin and never
    // reads the instance's basis, so a card cannot be scaled up to stand in for a nearer one.
    private static void Frame(Camera3D eye, ClutterBuilder.KindExport card, float range)
    {
        float span = Mathf.Max(card.Width, card.Height);
        eye.Fov = Mathf.Clamp(Mathf.RadToDeg(2f * Mathf.Atan(span * 0.8f / range)), 1f, 60f);
    }

    // The ground quad across the pane at one depth, wide enough to cover it with margin.
    private static void PosePlate(Node3D plate, Camera3D eye, float z)
    {
        var basis = plate.Transform.Basis.Orthonormalized();
        float scale = 6f * Mathf.Abs(z) * Mathf.Tan(Mathf.DegToRad(eye.Fov / 2f));
        plate.Transform = new Transform3D(basis.Scaled(new Vector3(scale, scale, scale)),
            new Vector3(0f, 0f, z));
    }

    // The ground polygon as an occluder: the first mesh under the built subtree, re-hung under a
    // node that stands it on edge across the pane. Its authored centre is cancelled and its span
    // normalised to one unit on the child, so the parent's scale is the plate's width in metres
    // whatever the template's own corner coordinates are.
    private static Node3D? Plate(Node3D? built)
    {
        var found = FirstMesh(built);
        if (found == null || built == null)
        {
            built?.Free();
            return null;
        }
        var box = found.GetAabb();
        var mesh = found.Mesh;
        var material = found.GetActiveMaterial(0);
        float span = Mathf.Max(box.Size.X, box.Size.Z);
        built.Free();
        if (span <= 0f)
        {
            return null;
        }
        var plate = new Node3D
        {
            Transform = new Transform3D(Basis.FromEuler(new Vector3(Mathf.Pi / 2f, 0f, 0f)), Vector3.Zero),
        };
        plate.AddChild(new MeshInstance3D
        {
            Mesh = mesh,
            MaterialOverride = material,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            Transform = new Transform3D(
                Basis.Identity.Scaled(Vector3.One / span),
                -(box.Position + (box.Size / 2f)) / span),
        });
        return plate;
    }

    private static MeshInstance3D? FirstMesh(Node? node)
    {
        if (node is MeshInstance3D { Mesh: not null } found)
        {
            return found;
        }
        for (int i = 0; node != null && i < node.GetChildCount(); i++)
        {
            if (FirstMesh(node.GetChild(i)) is { } below)
            {
                return below;
            }
        }
        return null;
    }

    // Two instances of the kind in one MultiMesh, both parked until a pose asks for them.
    private static MultiMeshInstance3D Cards(ClutterBuilder.KindExport kind)
    {
        var mm = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            UseCustomData = true,
            Mesh = kind.Mesh,
            InstanceCount = 2,
        };
        var node = new MultiMeshInstance3D
        {
            Multimesh = mm,
            MaterialOverride = kind.Material,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            ExtraCullMargin = kind.CullMargin,
        };
        Park(node, kind, NearIndex);
        Park(node, kind, FarIndex);
        return node;
    }

    private static void Park(MultiMeshInstance3D node, ClutterBuilder.KindExport kind, int index)
        => Place(node, kind, index, ParkZ, full: true);

    // One card at one depth, planted at its base so lowering it by half its height centres it on
    // the camera axis, which is what makes the near and the far card concentric. A card asked for
    // mid-fade gets a band that makes its alpha exactly one half there, which is the dithered state
    // the far ridge draws in; a full card gets the all-zero band that never fades. Distances are
    // squared, as the shader's own test is.
    private static void Place(MultiMeshInstance3D node, ClutterBuilder.KindExport kind,
        int index, float z, bool full)
    {
        node.Multimesh!.SetInstanceTransform(index, new Transform3D(
            Basis.Identity, new Vector3(0f, -kind.Height / 2f, z)));
        float far = 2f * z * z;
        node.Multimesh.SetInstanceCustomData(index, full
            ? new Color(0f, 0f, 0f, 0f)
            : new Color(0f, far, 1f / far, 0f));
    }

    // The pane's pixels after a forced draw. 3D geometry reaches the rendering server as it enters
    // the tree, so unlike a canvas item it photographs from inside a suite's single frame.
    private static byte[] Shot(SubViewport pane)
    {
        // ⚠ Push every transform to the rendering server first. A Node3D queues its transform
        // notification for the scene tree's next flush, and a suite never reaches one, so an
        // ordinary MeshInstance3D would otherwise draw at the identity and be clipped away.
        Settle(pane);
        // ⚠ Twice. A MultiMesh's instance buffer is uploaded with the frame that follows the write,
        // so a single draw photographs the previous pose and every count comes out of step by one.
        RenderingServer.ForceDraw();
        RenderingServer.ForceDraw();
        var img = pane.GetTexture()?.GetImage();
        return img == null || img.IsEmpty() ? Array.Empty<byte>() : img.GetData();
    }

    private static void Settle(Node node)
    {
        if (node is Node3D spatial)
        {
            spatial.ForceUpdateTransform();
        }
        for (int i = 0; i < node.GetChildCount(); i++)
        {
            Settle(node.GetChild(i));
        }
    }

    // The pixels a subject drew and drew opaquely: different from the empty frame, and the same in
    // both backdrops. A texel the subject only tinted moves with what is behind it and is left out.
    private static bool[] Mask(byte[] empty, byte[] overEmpty, byte[] overBackdrop)
    {
        var mask = new bool[empty.Length / Channels];
        if (overEmpty.Length != empty.Length || overBackdrop.Length != empty.Length)
        {
            return mask;
        }
        for (int i = 0; i + Channels <= empty.Length; i += Channels)
        {
            mask[i / Channels] = Moved(empty, overEmpty, i) && !Moved(overEmpty, overBackdrop, i);
        }
        return mask;
    }

    private static int Count(bool[] mask)
    {
        int n = 0;
        foreach (bool set in mask)
        {
            n += set ? 1 : 0;
        }
        return n;
    }

    private static int Differing(byte[] a, byte[] b, bool[]? mask = null)
    {
        if (a.Length == 0 || a.Length != b.Length)
        {
            return -1;
        }
        int changed = 0;
        for (int i = 0; i + Channels <= a.Length; i += Channels)
        {
            if ((mask == null || mask[i / Channels]) && Moved(a, b, i))
            {
                changed++;
            }
        }
        return changed;
    }

    private static bool Moved(byte[] a, byte[] b, int i)
    {
        for (int c = 0; c < Channels; c++)
        {
            if (a[i + c] != b[i + c])
            {
                return true;
            }
        }
        return false;
    }
}
