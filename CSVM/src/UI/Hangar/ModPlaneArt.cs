using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using CSVM.Flight.Hangar;
using CSVM.Mech3;
using CSVM.UI.Boards;
using CSVM.Utils;
using Godot;

namespace CSVM.UI.Hangar;

/// <summary>Which picture of a plane mod a menu slot shows.</summary>
public enum ModArtView
{
    /// <summary><c>plane.json</c>'s <c>portrait</c>, else a three-quarter render in the mod's own paint.</summary>
    Portrait,

    /// <summary>A black silhouette from above, nose up, the original's plane-icon sheets' view.</summary>
    Plan,

    /// <summary>A black silhouette from ahead, the original's head-on diagrams' view.</summary>
    HeadOn,
}

/// <summary>
/// The menu pictures of each plane mod. <see cref="Bind"/> decodes the mod's portrait and loads the
/// rest from a cache folder. What the cache lacks it renders once from the grafted model, on a
/// transparent ground. A cache file is named by the mod's id and content hash, so a changed mod
/// renders again. Screens ask <see cref="Picture"/> by node, and a stock node or a mod with no
/// picture answers null, which keeps the parent's art. Format: <c>docs/formats/mod-planes.md</c>,
/// "Menu art".
/// </summary>
public static class ModPlaneArt
{
    /// <summary>The folder under Godot's user folder the renders are cached in.</summary>
    public const string CacheFolder = "mod_art";

    /// <summary>The render's square edge in pixels, above every slot it is drawn into.</summary>
    public const int RenderSize = 512;

    private static readonly ModArtView[] Views = { ModArtView.Portrait, ModArtView.Plan, ModArtView.HeadOn };

    // Where the three-quarter camera stands, from the plane's centre: ahead, left and above.
    private static readonly Vector3 PortraitEye = new(-1.1f, 0.55f, -1f);

    private static Dictionary<string, Image> _images = new(StringComparer.OrdinalIgnoreCase);
    private static (GameZ? Planes, string CacheRoot, List<ModPlane> Pending) _bound = (null, string.Empty, new());
    private static bool _landed;

    /// <summary>The cache file of one view: the id, the hash's first 16 digits and the view.</summary>
    public static string CacheFile(string cacheRoot, string id, string hash, ModArtView view) =>
        Path.Combine(cacheRoot, $"{id}_{hash[..Math.Min(16, hash.Length)]}_{view.ToString().ToLowerInvariant()}.png");

    /// <summary>Whether the view is the mod's own image rather than a render.</summary>
    public static bool FromPortrait(ModPlane mod, ModArtView view) => view == ModArtView.Portrait && mod.Portrait != null;

    /// <summary>A picture of <paramref name="width"/> by <paramref name="height"/> scaled into the box,
    /// aspect kept, centred.</summary>
    public static (float X, float Y, float Width, float Height) Fit(int width, int height, float x, float y, float boxWidth, float boxHeight)
    {
        float scale = Math.Min(boxWidth / width, boxHeight / height);
        float w = width * scale;
        float h = height * scale;
        return (x + ((boxWidth - w) / 2f), y + ((boxHeight - h) / 2f), w, h);
    }

    /// <summary>Takes every mod's portrait and cached renders, and notes the renders the cache lacks
    /// for <see cref="RenderPending"/>. With <paramref name="planes"/> null nothing is noted.</summary>
    public static void Bind(GameZ? planes, ModPlanes mods, string cacheRoot)
    {
        var images = new Dictionary<string, Image>(StringComparer.OrdinalIgnoreCase);
        var pending = new List<ModPlane>();
        foreach (var mod in mods.Planes)
        {
            if (mod.Portrait is { } portrait)
            {
                if (Decode(mod.Read(portrait), portrait) is { } image)
                {
                    images[Key(mod.Id, ModArtView.Portrait)] = image;
                }
                else
                {
                    Log.Warn("ui", $"mod plane {mod.Id}: portrait {portrait} does not decode, the menus show {mod.KindOf}'s art");
                }
            }

            if (!Load(images, mod, cacheRoot) && planes != null)
            {
                pending.Add(mod);
            }
        }

        _images = images;
        _bound = (planes, cacheRoot, pending);
    }

    /// <summary>Renders what <see cref="Bind"/> noted under <paramref name="host"/>, one synchronous
    /// frame per view, over a chapter's <paramref name="textures"/> (read only when a render is due).
    /// ⚠ Do not call it before the first frame; a render there reads back blank, so the launch defers it.</summary>
    public static void RenderPending(Node host, Func<TextureSet> textures)
    {
        var (planes, cacheRoot, pending) = _bound;
        _bound = (planes, cacheRoot, new List<ModPlane>());
        if (pending.Count == 0)
        {
            return;
        }

        using var archive = new TextureArchive(textures());
        foreach (var mod in pending)
        {
            Render(host, planes!, archive, mod, Views.Where(v => !FromPortrait(mod, v)).ToList(), cacheRoot);
            _landed |= Load(_images, mod, cacheRoot);
        }
    }

    /// <summary>True once after a render landed, so a menu composed before it composes again.</summary>
    public static bool TakeLanded()
    {
        bool landed = _landed;
        _landed = false;
        return landed;
    }

    /// <summary>The mod's picture for <paramref name="view"/>, null for a stock node or none.</summary>
    public static Image? For(string? node, ModArtView view) =>
        node != null && _images.TryGetValue(Key(node, view), out var image) ? image : null;

    /// <summary>The mod's picture fitted into a slot's box, or null to draw the parent's art.</summary>
    public static BoardPicture? Picture(string? node, ModArtView view, float x, float y, float width, float height)
    {
        if (For(node, view) is not { } image)
        {
            return null;
        }

        var (px, py, w, h) = Fit(image.GetWidth(), image.GetHeight(), x, y, width, height);
        return new BoardPicture(new BoardArt(BoardArtLibrary.Held, $"{node}:{view}", 1, image), px, py, Width: w, Height: h);
    }

    private static string Key(string node, ModArtView view) => node + "/" + view.ToString();

    // Takes the mod's cached renders; false when any is missing.
    private static bool Load(Dictionary<string, Image> images, ModPlane mod, string cacheRoot)
    {
        bool all = true;
        foreach (var view in Views.Where(v => !FromPortrait(mod, v)))
        {
            string file = CacheFile(cacheRoot, mod.Id, mod.Hash, view);
            if (File.Exists(file) && Decode(File.ReadAllBytes(file), file) is { } image)
            {
                images[Key(mod.Id, view)] = image;
            }
            else
            {
                all = false;
            }
        }

        return all;
    }

    private static Image? Decode(byte[]? bytes, string path)
    {
        if (bytes == null)
        {
            return null;
        }

        var image = new Image();
        var err = Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".png" => image.LoadPngFromBuffer(bytes),
            ".jpg" or ".jpeg" => image.LoadJpgFromBuffer(bytes),
            ".webp" => image.LoadWebpFromBuffer(bytes),
            ".tga" => image.LoadTgaFromBuffer(bytes),
            ".bmp" => image.LoadBmpFromBuffer(bytes),
            _ => Error.FileUnrecognized,
        };
        return err == Error.Ok && !image.IsEmpty() ? image : null;
    }

    // One build of the grafted airframe in a private world, one synchronous frame per view.
    private static void Render(Node host, GameZ planes, TextureArchive textures, ModPlane mod, IReadOnlyList<ModArtView> views, string cacheRoot)
    {
        if (DisplayServer.GetName() == "headless")
        {
            Log.Info("ui", $"mod plane {mod.Id}: no renderer, the menus show {mod.KindOf}'s art until a run renders it");
            return;
        }

        Node3D model;
        try
        {
            model = new PlaneBuilder(planes, textures).Build(mod.Id);
        }
        catch (Exception e) when (e is InvalidOperationException or InvalidDataException or KeyNotFoundException)
        {
            Log.Warn("ui", $"mod plane {mod.Id}: the menu render did not build ({e.Message})");
            return;
        }

        var view = new SubViewport
        {
            Size = new Vector2I(RenderSize, RenderSize),
            TransparentBg = true,
            OwnWorld3D = true,
            Msaa3D = Viewport.Msaa.Msaa4X,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled,
        };
        var camera = new Camera3D { Projection = Camera3D.ProjectionType.Orthogonal, Current = true };
        var sun = new DirectionalLight3D { LightEnergy = 1.2f };
        var world = new WorldEnvironment
        {
            Environment = new Godot.Environment
            {
                BackgroundMode = Godot.Environment.BGMode.ClearColor,
                AmbientLightSource = Godot.Environment.AmbientSource.Color,
                AmbientLightColor = Colors.White,
                AmbientLightEnergy = 0.5f,
            },
        };
        view.AddChild(world);
        view.AddChild(sun);
        view.AddChild(camera);
        view.AddChild(model);
        host.AddChild(view);
        try
        {
            sun.LookAtFromPosition(Vector3.Zero, new Vector3(1f, -1.5f, 0.6f), Vector3.Up);
            var bounds = Bounds(model);
            Directory.CreateDirectory(cacheRoot);
            foreach (var shot in views)
            {
                Aim(camera, bounds, shot);
                if (Shoot(view) is not { } image)
                {
                    Log.Warn("ui", $"mod plane {mod.Id}: the menu render read back nothing, the menus show {mod.KindOf}'s art");
                    return;
                }

                if (shot != ModArtView.Portrait)
                {
                    Blacken(image);
                }

                string file = CacheFile(cacheRoot, mod.Id, mod.Hash, shot);
                image.SavePng(file);
                Log.Info("ui", $"mod plane {mod.Id}: rendered its {shot} menu art, {image.GetWidth().ToString(CultureInfo.InvariantCulture)}x{image.GetHeight().ToString(CultureInfo.InvariantCulture)}, to {file}");
            }
        }
        finally
        {
            host.RemoveChild(view);
            view.Free();
        }
    }

    private static Image? Shoot(SubViewport view)
    {
        Settle(view);
        view.RenderTargetUpdateMode = SubViewport.UpdateMode.Always;
        RenderingServer.ForceDraw();
        RenderingServer.ForceDraw();
        var image = view.GetTexture()?.GetImage();
        view.RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled;
        if (image == null || image.IsEmpty())
        {
            return null;
        }

        image.Convert(Image.Format.Rgba8);
        var used = image.GetUsedRect();
        return used.Size.X > 0 && used.Size.Y > 0 ? image.GetRegion(used) : null;
    }

    // A render outside the frame loop reaches no transform flush, so every pose is pushed first.
    private static void Settle(Node node)
    {
        (node as Node3D)?.ForceUpdateTransform();
        foreach (var child in node.GetChildren())
        {
            Settle(child);
        }
    }

    // The visible meshes' box in the model's frame: hidden wreck, panels and flares stay out.
    private static Aabb Bounds(Node3D model)
    {
        Aabb? box = null;
        foreach (var mesh in Meshes(model))
        {
            var local = mesh.GlobalTransform * mesh.GetAabb();
            box = box is { } b ? b.Merge(local) : local;
        }

        return box ?? new Aabb(-Vector3.One, Vector3.One * 2f);
    }

    private static IEnumerable<MeshInstance3D> Meshes(Node node)
    {
        foreach (var child in node.GetChildren())
        {
            if (child is MeshInstance3D mesh && mesh.IsVisibleInTree())
            {
                yield return mesh;
            }

            foreach (var inner in Meshes(child))
            {
                yield return inner;
            }
        }
    }

    // An orthographic camera on the view's axis, sized to the box's projected extent.
    private static void Aim(Camera3D camera, Aabb bounds, ModArtView view)
    {
        var (eye, up) = view switch
        {
            ModArtView.Plan => (Vector3.Up, Vector3.Forward),
            ModArtView.HeadOn => (Vector3.Forward, Vector3.Up),
            _ => (PortraitEye.Normalized(), Vector3.Up),
        };
        var centre = bounds.GetCenter();
        float reach = bounds.Size.Length();
        camera.LookAtFromPosition(centre + (eye * reach * 2f), centre, up);
        var basis = camera.GlobalTransform.Basis;
        float extent = 0f;
        for (int i = 0; i < 8; i++)
        {
            var offset = bounds.GetEndpoint(i) - centre;
            extent = Math.Max(extent, Math.Max(Math.Abs(offset.Dot(basis.X)), Math.Abs(offset.Dot(basis.Y))));
        }

        camera.Size = Math.Max(extent * 2.1f, 0.01f);
        camera.Near = reach * 0.5f;
        camera.Far = reach * 4f;
    }

    // The silhouette views keep the render's coverage and drop its colour, as the icon sheets do.
    private static void Blacken(Image image)
    {
        byte[] data = image.GetData();
        for (int i = 0; i < data.Length; i += 4)
        {
            data[i] = data[i + 1] = data[i + 2] = 0;
        }

        image.SetData(image.GetWidth(), image.GetHeight(), false, Image.Format.Rgba8, data);
    }
}
