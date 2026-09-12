using System;
using System.Collections.Generic;
using System.Linq;
using CSVM.Flight;
using CSVM.Mech3;
using CSVM.Session;
using Godot;

namespace CSVM.Testing;

/// <summary>
/// A ground emplacement's sight line against its own rig and against a stranger's, on bodies this
/// suite builds and stamps into the physics space itself, so the verdict does not depend on what a
/// chapter world's colliders happen to be doing inside one harness frame (BL-831).
/// </summary>
internal static class TurretMountSightlineSuites
{
    private const float BoxSide = 4f;

    // Well clear of any chapter geometry a neighbouring suite may have left standing.
    private static readonly Vector3 SiteAt = new(0f, 6000f, 0f);

    [Suite("turret-own-mount-sightline",
        "a ground emplacement's sight line is not blocked by its own rig (BL-830): an aagun-pattern "
        + "site built under a bare world root with a 4 m trimesh box as its own collider, engulfing "
        + "the gun's node past the 1.5 m skirt, reads a target overhead as open; an identical box "
        + "standing as a separate world node on the same line reads blocked, so the exclusion is the "
        + "gun's own rig and nothing wider")]
    internal static void OwnMountIsNotCover(TestContext ctx)
    {
        ctx.RequireData(ctx.ZrdrPath, $"zrdr archive");
        string texturesPath = SessionPaths.ChapterTextures(ctx.DataRoot, "C1");
        ctx.RequireData(texturesPath, $"C1 textures");
        var weapons = WeaponDefs.Load(ctx.ZrdrPath, null);
        var turretDefs = TurretDefs.Load(ctx.ZrdrPath);
        var aagunDef = turretDefs.All.FirstOrDefault(d => !d.Carried
            && d.NodePatterns.Any(p => p.Count == 1 && p[0] == "aagun**"));
        ctx.Check(aagunDef != null, $"ai.zrd carries the standalone aagun** entry");
        if (aagunDef == null)
        {
            return;
        }

        var textures = new TextureArchive(texturesPath);
        var root = new Node3D { Name = "sightline_root" };
        ProjectilePool? pool = null;
        StaticBody3D? stranger = null;
        TurretController[]? built = null;
        try
        {
            // The rig the entry's PARTS resolve against: turret, gun, firepoint, and one collider
            // body as the site's own child, the shape a world gun's `col` takes.
            var site = Named("aagun90");
            var turret = Named("turret");
            var gun = Named("gun");
            var firepoint = Named("firepoint");
            firepoint.Position = new Vector3(0f, 0f, -2f);
            gun.AddChild(firepoint);
            turret.AddChild(gun);
            site.AddChild(turret);
            var ownBody = Box("col");
            ownBody.Position = new Vector3(0f, BoxSide / 2f, 0f);
            site.AddChild(ownBody);
            root.AddChild(site);
            ctx.Host.AddChild(root);
            root.GlobalPosition = SiteAt;
            ownBody.ForceUpdateTransform();

            var live = new ProjectilePool(textures, null, null);
            pool = live;
            ctx.Host.AddChild(live);
            built = TurretController.BuildEmplacements(turretDefs, weapons,
                (pattern, scope) => scope == null && pattern == "aagun**"
                    ? new[] { site } : Array.Empty<Node3D>(),
                live, root);
            var emplacement = built.FirstOrDefault(t => t.Site == site);
            ctx.Check(emplacement != null, $"the aagun entry builds a gunner on the synthetic site built={built.Length}");
            if (emplacement == null)
            {
                return;
            }

            var owned = emplacement.PlatformColliderRids();
            ctx.Check(owned.Count == 1 && owned.Contains(ownBody.GetRid()),
                $"a gun on the world root owns its own rig as its platform bodies={owned.Count}");

            // Overhead, past the box: the ray leaves the gun's node inside its own box, and the
            // skirt alone ends 0.5 m short of the box's top face.
            var target = site.GlobalPosition + new Vector3(0f, 250f, 120f);
            ctx.Check(!emplacement.OwnSightLineBlocked(target),
                $"the sight line through the gun's own rig reads open");

            // The able-to-fail half: the same box, 30 m out on the same line, as a stranger.
            var span = target + Vector3.Up * 0.2f - emplacement.WorldPosition;
            stranger = Box("stranger");
            root.AddChild(stranger);
            stranger.GlobalPosition = emplacement.WorldPosition + span.Normalized() * 30f;
            stranger.ForceUpdateTransform();
            ctx.Check(emplacement.OwnSightLineBlocked(target),
                $"an identical box standing on the line as a separate world node reads blocked");
            ctx.Check(!owned.Contains(stranger.GetRid()),
                $"…and it is not in the gun's own platform set");
        }
        finally
        {
            pool?.Free();
            ctx.Host.RemoveChild(root);
            root.Free();
            textures.Dispose();
        }
    }

    // A rig node the PARTS lookup can find: the builder reads the world's name meta, not Node.Name.
    private static Node3D Named(string name)
    {
        var node = new Node3D { Name = name };
        node.SetMeta(AnimRuntime.NameMeta, name);
        return node;
    }

    // One trimesh box body on the world layer, two-sided so a ray leaving it from inside registers
    // the way a world mesh's own faces do.
    private static StaticBody3D Box(string name)
    {
        var body = new StaticBody3D { Name = name, CollisionLayer = CollisionLayers.World, CollisionMask = 0 };
        var shape = new BoxMesh { Size = new Vector3(BoxSide, BoxSide, BoxSide) }.CreateTrimeshShape();
        shape.BackfaceCollision = true;
        body.AddChild(new CollisionShape3D { Shape = shape });
        return body;
    }
}
