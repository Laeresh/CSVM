using System.Collections.Generic;
using CSVM.Mech3;
using CSVM.Utils;
using Godot;

namespace CSVM.Effects;

/// <summary>
/// Enhanced Graphics only: a soft volumetric bank inside each authored <c>fvol*</c> volume, under
/// the cloud cards <see cref="FogVolumeClutter"/> lays over the same geometry. Each volume's bounds
/// go down as <see cref="FogVolume"/> boxes over one shared <see cref="FogMaterial"/>, filling the
/// Environment's froxel fog, so the sun scatters through the deck.
/// ⚠ Nothing here is authored: the original renders no volumetric fog, so every constant is TUNE
/// over the volumes' own bounds and the zone colour. The faithful path builds nothing
/// and leaves the Environment flag off, so no pinned shot can move. Volumes and their bounds: docs/formats/fogvol.md.
/// </summary>
public sealed partial class FogVolumeBanks : Node3D
{
    // ⚠ TUNE, enhanced only, in extinction per metre. The authored deck slab is about 120 m thick.
    // A climb straight through one scatters out about a fifth of what is behind it, and the cards
    // keep their shape. At twice this the deck's underside washes to a flat sun-coloured tint,
    // the bank drawing itself instead of its cloud.
    private const float BankDensity = 0.002f;

    // ⚠ Keep at 0, and the tiling below is why: an edge fade softens every box face it is given,
    // including the faces where two tiles of one authored volume meet, which would draw that grid
    // into the bank. Uniform density to the authored wall instead, where the cards stand anyway.
    private const float BankEdgeFade = 0f;

    // ⚠ Measured against this engine, not a preference: a FogVolume box much larger than this
    // contributes nothing at all to the froxel pass, silently and with no error. C1's own slab
    // pieces are 2048 and 8192 m across; at 8192 the frame is pixel-identical to one with no banks
    // at all, at 2048 the bank renders whether or not the camera stands over it. So each authored
    // volume is laid down as a grid of tiles no wider than this, which is seamless at edge fade 0.
    private const float MaxTileExtent = 2048f;

    // TUNE: uniform density inside the box. A height falloff would thin the bank toward the
    // volume's own top, which is where the authored cards sit and where the bank is wanted.
    private const float BankHeightFalloff = 0f;

    // ⚠ TUNE, and the reason the banks carry the whole density: a non-zero global density is fog
    // everywhere, including the air the authored csky_fog_* ramp already grades, so the two would
    // haze the same metres twice.
    private const float GlobalDensity = 0f;

    // ⚠ TUNE, metres, the froxel buffer's reach. Long enough that a bank read from just outside
    // its own volume still fills the frame, short enough that the far slices stay small. Geometry
    // past it is not spared: it reads the last slice, so the horizon dome takes the whole bank.
    private const float FroxelLength = 1024f;

    // TUNE: how hard the froxel slices bunch toward the camera, Godot's own default. The bank's
    // detail is wanted where the camera is, and the far slices only need to hold a flat tint.
    private const float FroxelDetailSpread = 2f;

    // TUNE: how far forward light scatters through the bank, the term that brightens it as the eye
    // swings into the sun. Mist and fog scatter slightly forward; at 0 a bank looking into the sun
    // reads the same as one looking away from it, which is the whole point of the item.
    private const float FroxelAnisotropy = 0.5f;

    // TUNE: how much of the zone's authored SUNLIGHT_AMBIENT reaches the bank. Full, because a
    // night bank lit by the capped sun alone goes black and stops reading as cloud.
    private const float FroxelAmbientInject = 1f;

    // ⚠ Keep at 1. Godot applies this factor to a FogVolume as well as to the background. A lower
    // value fades the banks out where this item is judged, looking up at the deck against the sky.
    // The horizon dome is geometry, so this factor does not reach it.
    private const float FroxelSkyAffect = 1f;

    // ⚠ TUNE, and the whole per-frame cost of the pass: the froxel buffer is size x size x depth
    // cells whatever the fog reaches. Godot's own defaults, kept rather than raised, because the
    // bank is a soft body with no detail to resolve and this stack owes D32 a 20% budget.
    private const int FroxelVolumeSize = 64;
    private const int FroxelVolumeDepth = 64;

    private readonly FogMaterial _material = new();

    /// <summary>How many banks were built, one per authored volume. The count a suite pins against
    /// the chapter's own <c>fvol*</c> census.</summary>
    public int BankCount { get; private set; }

    /// <summary>How many <see cref="FogVolume"/> nodes carry those banks. One per bank until an
    /// authored volume is wider than <see cref="MaxTileExtent"/>, which the deck slabs all are.</summary>
    public int TileCount { get; private set; }

    /// <summary>The extinction per metre every bank carries.</summary>
    public float Density => _material.Density;

    /// <summary>The scattering colour last written, linear, as the renderer takes it. Published
    /// because a material property cannot be read back off a running renderer any other way.</summary>
    public Color Albedo => _material.Albedo;

    /// <summary>Builds one bank per authored volume. Returns null on the faithful presentation, for a
    /// chapter with no <c>fvol*</c> volume, and where <c>fogvol.zrd</c> arms the whiteout. Add the
    /// result to the world root at identity, the boxes carry absolute world coordinates.</summary>
    public static FogVolumeBanks? Create(IReadOnlyList<FogVolumeBox> volumes, FogVolumeSpec? spec)
    {
        // ⚠ No bank where the whiteout is armed (C5's street prisms). The lit world writes its own
        // FOG and so takes no froxel fog. The bank reaches only the horizon dome and the sky.
        // They go black behind a city that keeps its zone fog: a hard edge and a skyline.
        if (!GraphicsMode.Enhanced || volumes.Count == 0 || FogVolumeWhiteout.From(spec, volumes).Armed)
        {
            return null;
        }
        var banks = new FogVolumeBanks { Name = "fog_volume_banks" };
        banks.Build(volumes);
        return banks;
    }

    /// <summary>Turns the Environment's froxel fog on for a world that built banks, and off for one
    /// that did not. Off is not an optimisation: the pass costs its froxel buffer every frame
    /// wherever it is on, and seven chapters author no volume at all. One Environment outlives every
    /// session (<c>Launcher</c>), so a chapter with no banks must actively clear it.</summary>
    public static void ApplyFroxelFog(Godot.Environment env, bool enabled)
    {
        env.VolumetricFogEnabled = enabled;
        if (!enabled)
        {
            return;
        }
        env.VolumetricFogDensity = GlobalDensity;
        env.VolumetricFogLength = FroxelLength;
        env.VolumetricFogDetailSpread = FroxelDetailSpread;
        env.VolumetricFogAnisotropy = FroxelAnisotropy;
        env.VolumetricFogAmbientInject = FroxelAmbientInject;
        env.VolumetricFogSkyAffect = FroxelSkyAffect;
        // The banks scatter the zone's own light; nothing here emits, or a night bank would glow
        // with no source, and the world's glow threshold is reserved for the glow-arm sprites.
        env.VolumetricFogEmission = Colors.Black;
        env.VolumetricFogGIInject = 0f;
        RenderingServer.EnvironmentSetVolumetricFogVolumeSize(FroxelVolumeSize, FroxelVolumeDepth);
    }

    /// <summary>The scattering colour for one applied zone, the zone's own <c>FOG_COLOR</c>. Called
    /// on every zone apply, so a mid-flight zone change carries the bank with it.</summary>
    public void ApplyZone(Color zoneFogColor)
    {
        // ⚠ Linearise: the authored value is a DX7 framebuffer colour, and a material albedo is
        // taken by the renderer as it stands, the same conversion WeatherRig.WriteSkyColor makes.
        _material.Albedo = zoneFogColor.SrgbToLinear();
    }

    private void Build(IReadOnlyList<FogVolumeBox> volumes)
    {
        _material.Density = BankDensity;
        _material.EdgeFade = BankEdgeFade;
        _material.HeightFalloff = BankHeightFalloff;
        _material.Emission = Colors.Black;
        // A sane albedo before any zone is applied: a session with no weather.json never reaches
        // ApplyZone, and a black-albedo bank scatters nothing at all.
        ApplyZone(Colors.White);
        foreach (var volume in volumes)
        {
            BankCount++;
            var box = volume.Box;
            int tilesX = Mathf.Max(1, Mathf.CeilToInt(box.Size.X / MaxTileExtent));
            int tilesZ = Mathf.Max(1, Mathf.CeilToInt(box.Size.Z / MaxTileExtent));
            var step = new Vector3(box.Size.X / tilesX, box.Size.Y, box.Size.Z / tilesZ);
            for (int ix = 0; ix < tilesX; ix++)
            {
                for (int iz = 0; iz < tilesZ; iz++)
                {
                    var corner = box.Position + new Vector3(step.X * ix, 0f, step.Z * iz);
                    AddChild(new FogVolume
                    {
                        Name = $"bank_{volume.Name}_{ix}_{iz}",
                        Shape = RenderingServer.FogVolumeShape.Box,
                        Size = step,
                        Position = corner + (step * 0.5f),
                        Material = _material,
                    });
                    TileCount++;
                }
            }
        }
    }
}
