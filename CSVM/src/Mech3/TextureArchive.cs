using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;
using CSVM.Utils;
using Godot;

namespace CSVM.Mech3;

/// <summary>
/// Texture lookup over a mech3ax texture extraction — a ZIP (texture.zbd → PNGs) or a
/// directory of those same PNGs (e.g. from ExtractAssets.ps1 -Unzip).
/// Material texture names come from fixed-width 20-char fields in planes.zbd, so
/// "blo_fusalagebottom.t" must still find "blo_fusalagebottom.png" — hence the
/// prefix fallback.
/// </summary>
public sealed class TextureArchive : IDisposable
{
    private readonly ZipArchive? _zip;
    private readonly string? _dir;
    // baseName (no extension) -> the PNG's retrieval name (a zip entry's FullName, or a file name under _dir).
    private readonly Dictionary<string, string> _byBaseName = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ImageTexture?> _cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, (bool HasAlpha, bool Soft)> _alphaInfo = new(StringComparer.OrdinalIgnoreCase);
    // Distinct texture names this archive failed to resolve, each already logged once.
    private readonly HashSet<string> _reportedMissing = new(StringComparer.OrdinalIgnoreCase);

    // Texture names referenced by gamez meshes that ship in NO archive of a retail
    // install — verified absent across all extracted chapters. The
    // original engine tolerates them (renders neutral), so we do too: a quiet gray
    // fallback instead of the debug magenta, and a one-line data-gap note instead of
    // a lookup-failure warning. Anything NOT on this list that goes missing is likely
    // our own name-resolution failing and stays loud (magenta + the "not found" line).
    private static readonly HashSet<string> KnownAbsentFromGameData = new(StringComparer.OrdinalIgnoreCase)
    {
        "pir_spinner", // referenced by every chapter's gamez (a pirate-zeppelin spinner disc)
        "barngrill",   // C5 only
    };

    /// <summary>True if the name is a texture the retail game data itself lacks (see
    /// KnownAbsentFromGameData) — callers render a neutral fallback, not the debug magenta.</summary>
    public static bool IsKnownAbsent(string materialTextureName) =>
        KnownAbsentFromGameData.Contains(Path.GetFileNameWithoutExtension(materialTextureName));

    /// <summary>Distinct texture names this archive could not resolve, for an end-of-build
    /// summary line. Each was already reported once (one line, no stack trace) by Find.</summary>
    public IReadOnlyCollection<string> MissingTextures => _reportedMissing;

    public TextureArchive(string path)
    {
        if (Directory.Exists(path))
        {
            _dir = path;
            foreach (var file in Directory.EnumerateFiles(path, "*.png"))
                _byBaseName[Path.GetFileNameWithoutExtension(file)] = Path.GetFileName(file);
        }
        else
        {
            _zip = ZipFile.OpenRead(path);
            foreach (var entry in _zip.Entries)
                if (entry.Name.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                    _byBaseName[Path.GetFileNameWithoutExtension(entry.Name)] = entry.FullName;
        }
        // An override naming a texture this archive does not hold would silently colour nothing,
        // which reads exactly like "the object is not drawing" — the answer the flag exists to give.
        TextureDropIn.CheckNames(path, _byBaseName.Keys);
    }

    /// <summary>True if the last texture returned by Find had an alpha channel.</summary>
    public bool LastHadAlpha { get; private set; }

    /// <summary>
    /// True if the last texture's alpha is "soft": a 1-bit scissor cutout at the usual 0.5
    /// threshold would erase it entirely or reduce it to a crude stencil. True for the
    /// original's translucent overlays — baked shadow decals (max alpha ~125/255), cloud
    /// and prop-blur sprites, waterfalls, smoke — which the original engine alpha-blends;
    /// false for genuine cutouts (fences, trees, railings), which scissor correctly.
    /// </summary>
    public bool LastAlphaIsSoft { get; private set; }

    public ImageTexture? Find(string materialTextureName)
    {
        var baseName = Path.GetFileNameWithoutExtension(materialTextureName);
        // A truncated name like "blo_fusalagebottom.t" keeps its bogus extension after
        // GetFileNameWithoutExtension strips ".t"; that is exactly the prefix we want.
        if (_cache.TryGetValue(baseName, out var cached))
        {
            (LastHadAlpha, LastAlphaIsSoft) = _alphaInfo[baseName];
            return cached;
        }

        var name = Resolve(baseName);
        var bytes = name != null ? ReadBytes(name) : null;
        ImageTexture? tex = null;
        LastHadAlpha = false;
        LastAlphaIsSoft = false;
        if (bytes != null)
        {
            var img = new Image();
            if (img.LoadPngFromBuffer(bytes) == Error.Ok)
            {
                LastHadAlpha = ImageHasAlpha(img);
                LastAlphaIsSoft = LastHadAlpha && AlphaIsSoft(img); // before mipmaps: raw pixels only
                // The drop-in instruments repaint the RGB flat and keep everything else — size,
                // format, alpha channel — so the alpha class read just above (and with it the
                // blend/scissor choice, the cutout silhouette and the mip chain) is unchanged.
                if (TextureDropIn.ColorFor(Path.GetFileNameWithoutExtension(name!), baseName) is { } flat)
                {
                    TextureDropIn.Flatten(img, flat);
                }
                img.GenerateMipmaps();
                tex = ImageTexture.CreateFromImage(img);
            }
        }
        else if (_reportedMissing.Add(baseName))
        {
            // Report each distinct miss once. Log.Warn is a plain line — GD.PushWarning would
            // print a full managed stack trace per call in Godot .NET, burying real errors.
            if (IsKnownAbsent(materialTextureName))
            {
                Log.Warn("world", $"texture absent from game data texture={materialTextureName} — gray fallback");
            }
            else
            {
                Log.Warn("world", $"texture not found in archive texture={materialTextureName}");
            }
        }
        _cache[baseName] = tex;
        _alphaInfo[baseName] = (LastHadAlpha, LastAlphaIsSoft);
        return tex;
    }

    /// <summary>The raw PNG as a fresh, un-mipmapped <see cref="Image"/> — the source
    /// <see cref="PlanePainter"/> recolours. Deliberately NOT the ImageTexture cache: that
    /// one is shared across every plane and world instance and must never be mutated, and
    /// its images already carry generated mipmaps. Each call returns a new Image.</summary>
    public Image? FindImage(string materialTextureName)
    {
        var name = Resolve(Path.GetFileNameWithoutExtension(materialTextureName));
        var bytes = name != null ? ReadBytes(name) : null;
        if (bytes == null)
            return null;
        var img = new Image();
        return img.LoadPngFromBuffer(bytes) == Error.Ok ? img : null;
    }

    /// <summary>The archive's texture whose name begins with a zero-padded two-digit
    /// number, e.g. 21 → "21ace_star" — how <c>paint_decalN</c> indexes the gapless 00–49
    /// decal set every chapter ships. Null when out of range. The half-size "_1" LOD twins
    /// are excluded (they share the numeric prefix but are not the decal itself).</summary>
    public string? FindByDecalIndex(int index)
    {
        if (index is < 0 or > 99)
            return null;
        var prefix = index.ToString("00");
        foreach (var name in _byBaseName.Keys)
            if (name.Length > 2 && name.StartsWith(prefix, StringComparison.Ordinal)
                && !char.IsDigit(name[2]) && !name.EndsWith("_1", StringComparison.Ordinal))
                return name;
        return null;
    }

    // Classifies the alpha channel (see LastAlphaIsSoft). Soft when scissoring at 0.5
    // would show (almost) nothing — max alpha below ~140/255 — or when partial alpha
    // dominates and nearly-opaque texels are rare (soft sprites like clouds and smoke,
    // whose scissor cutout is a shredded stencil of only their densest texels).
    private static bool AlphaIsSoft(Image img)
    {
        var rgba = img;
        if (img.GetFormat() != Image.Format.Rgba8)
        {
            rgba = (Image)img.Duplicate();
            rgba.Convert(Image.Format.Rgba8);
        }
        var data = rgba.GetData();
        int max = 0, mid = 0, opaque = 0, total = data.Length / 4;
        if (total == 0)
            return false;
        for (int i = 3; i < data.Length; i += 4)
        {
            int a = data[i];
            if (a > max) max = a;
            if (a >= 200) opaque++;
            else if (a >= 32) mid++;
        }
        if (max < 140)
            return true;
        return opaque < total * 0.30f && mid > total * 0.35f;
    }

    private string? Resolve(string baseName)
    {
        if (_byBaseName.TryGetValue(baseName, out var exact))
            return exact;
        // mech3ax v0.6.1 disambiguates duplicate texture-table entries as "name.-N";
        // the pixel data lives under the original name. The fork indexes textures by
        // position instead and never renames, so this is legacy-tree-only.
        var m = System.Text.RegularExpressions.Regex.Match(baseName, @"^(.*)\.-\d+$");
        if (m.Success && _byBaseName.TryGetValue(m.Groups[1].Value, out var renamed))
            return renamed;
        // Names stored as "prefix\0suffix\0" decode with a period restored at the first
        // zero, so a name whose suffix is empty comes back doubled: "bldhwk_cowling..tif"
        // → base "bldhwk_cowling." → the PNG is "bldhwk_cowling". Only the fork's tree
        // exposes these (v0.6.1 hid them behind the ".-N" renames above); it is the one
        // resolution case the shape change would otherwise have broken.
        var trimmed = baseName.TrimEnd('.');
        if (trimmed.Length != baseName.Length && _byBaseName.TryGetValue(trimmed, out var undoubled))
            return undoubled;
        // fixed-width truncation fallback: unique prefix match
        string? match = null;
        foreach (var (name, retrieval) in _byBaseName)
        {
            if (name.StartsWith(baseName, StringComparison.OrdinalIgnoreCase))
            {
                if (match != null)
                    return null; // ambiguous
                match = retrieval;
            }
        }
        return match;
    }

    private byte[]? ReadBytes(string retrievalName)
    {
        if (_dir != null)
        {
            var p = Path.Combine(_dir, retrievalName);
            return File.Exists(p) ? File.ReadAllBytes(p) : null;
        }
        var entry = _zip!.GetEntry(retrievalName);
        if (entry == null)
            return null;
        using var s = entry.Open();
        using var ms = new MemoryStream();
        s.CopyTo(ms);
        return ms.ToArray();
    }

    private static bool ImageHasAlpha(Image img) =>
        img.GetFormat() is Image.Format.Rgba8 or Image.Format.La8 or Image.Format.Rgba4444 && img.DetectAlpha() != Image.AlphaMode.None;

    public void Dispose() => _zip?.Dispose();
}

/// <summary>
/// The texture drop-in instruments, hooked into <see cref="TextureArchive.Find"/> because that is
/// the one place every consumer — world, clutter, aircraft, puffers, clouds, gauges — resolves a
/// name, so none of them needs to know the mode exists.
///
/// <para><b><c>--tex-override=name[=color]</c></b> answers "is this thing drawing at all?": the
/// named texture comes back a flat loud colour wherever it is used. <b><c>--tex-census</c></b>
/// gives <i>every</i> texture its own flat colour, derived from the name alone, turning a frame
/// into a machine-readable map of which texture painted which pixel.</para>
///
/// <para><b>What is deliberately NOT touched.</b> Only the RGB bytes change: size, pixel format,
/// alpha channel and mip chain are the original's, so a hard-alpha cutout keeps its exact
/// silhouette, a soft-alpha sprite keeps blending, and the material variant the builder picks is
/// the one it would have picked anyway. No shader, material or geometry differs from a normal
/// run — a diagnostic that moved what it measures would answer its own question.</para>
///
/// <para><b>Reading a census frame back.</b> Shading multiplies the flat and fog mixes it toward
/// grey, so pixels are classified by <i>chromaticity</i> — the linear-space colour normalised to
/// its brightest channel, which a scalar shade leaves untouched — with a tolerance, a runner-up
/// margin and a darkness floor. Every colour is generated at full brightness (one channel pinned
/// to 255) to keep that ratio as far above 8-bit quantisation as it can be.</para>
/// </summary>
public static class TextureDropIn
{
    /// <summary>The colour an override with no explicit one gets. Deliberately the same magenta
    /// <c>SceneBuilder</c> paints an unresolvable texture — loud, and never a real texture — so a
    /// pose that may contain a genuine lookup failure wants an explicit colour instead.</summary>
    public static readonly Color DefaultOverride = new(1f, 0f, 1f);

    /// <summary>How far a pixel's chromaticity may sit from a census colour's and still be counted
    /// as it. Measured, not guessed: see docs/cli.md for the sweep this and
    /// <see cref="Separation"/> come from.</summary>
    public const float Tolerance = 0.045f;

    /// <summary>The runner-up must be at least this much further away than the winner, or the pixel
    /// is reported contested rather than credited to a texture it may not belong to. An absolute
    /// gap, not a ratio: a pixel sitting exactly on a flat has a winning distance of ~0, and any
    /// ratio test passes trivially there however close the rival sits.</summary>
    public const float Separation = 0.03f;

    /// <summary>Pixels whose brightest linear channel is below this carry too little signal for a
    /// ratio to survive 8-bit quantisation; they are counted as dark, never classified.</summary>
    public const float DarkFloor = 0.02f;

    private static readonly Dictionary<string, Color> Overrides = new(StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> OverridesSeen = new(StringComparer.OrdinalIgnoreCase);
    // Archive base name -> census colour, for every texture this session actually resolved. The
    // colour is a pure function of the name, so this is a record of what was loaded, not a source
    // of truth that has to exist before a lookup can be answered.
    private static readonly Dictionary<string, Color> Registered = new(StringComparer.OrdinalIgnoreCase);
    // Colour hex -> the first texture that claimed it, so a second claimant is reported, not lost.
    private static readonly Dictionary<string, string> ByColor = new(StringComparer.OrdinalIgnoreCase);
    private static readonly List<string> Extra = new();

    private static bool _census;
    private static bool _flushQueued;
    private static bool _unusedQueued;
    private static string _scratchDir = "";

    /// <summary>True once <c>--tex-census</c> asked for the whole-archive colouring.</summary>
    public static bool CensusActive => _census;

    /// <summary>True when either instrument is on — the flag <see cref="TextureCycler"/> reads to
    /// hold its flipbooks still, and the aircraft paint substitution reads to step aside.</summary>
    public static bool Active => _census || Overrides.Count > 0;

    /// <summary>Where <c>tex_census*.json</c> lands. Set once from the repo root.</summary>
    public static void SetScratchDir(string repoRoot) => _scratchDir = Path.Combine(repoRoot, ".scratch");

    /// <summary>Parses one <c>--tex-override=</c> value: <c>name</c>, or <c>name=color</c> where
    /// the colour is anything Godot's own parser takes (<c>ff0000</c>, <c>#ff0000</c>,
    /// <c>lime</c>). Several may be given by repeating the flag.</summary>
    public static void AddOverride(string spec)
    {
        int eq = spec.LastIndexOf('=');
        string name = eq < 0 ? spec : spec[..eq];
        string colorText = eq < 0 ? "" : spec[(eq + 1)..];
        name = Path.GetFileNameWithoutExtension(name.Trim());
        if (name.Length == 0)
        {
            Log.Warn("world", $"tex override has no texture name spec={spec}");
            return;
        }
        Color color = DefaultOverride;
        if (colorText.Length > 0)
        {
            // A sentinel no colour string can produce, so an unreadable value is told apart from
            // one that happens to equal the default.
            var sentinel = new Color(-1f, -1f, -1f);
            if (Color.HtmlIsValid(colorText))
            {
                color = Color.FromHtml(colorText);
            }
            else if (Color.FromString(colorText, sentinel) is var named && named != sentinel)
            {
                color = named;
            }
            else
            {
                Log.Warn("world", $"tex override colour unreadable texture={name} color={colorText} — using magenta");
            }
        }
        Overrides[name] = color;
        Log.Info("world", $"tex override texture={name} color={Hex(color)}");
    }

    /// <summary>Turns the census on. <paramref name="extraNames"/> are textures to include in a
    /// count report even when this chapter's archive has none of them — the able-to-fail control
    /// for "texture X is visible here".</summary>
    public static void EnableCensus(string extraNames)
    {
        _census = true;
        foreach (string n in extraNames.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            string name = Path.GetFileNameWithoutExtension(n.Trim());
            if (name.Length > 0 && !Extra.Contains(name))
            {
                Extra.Add(name);
            }
        }
        Log.Info("world", $"tex census on tolerance={Tolerance:0.###} separation={Separation:0.###} extra={(Extra.Count == 0 ? "-" : string.Join(",", Extra))}");
    }

    /// <summary>The flat colour a texture must resolve to, or null when neither instrument covers
    /// it. <paramref name="archiveName"/> is the PNG's own base name and owns the identity;
    /// <paramref name="requestedName"/> is the (possibly truncated) name the material asked for,
    /// matched too so either spelling works on the command line.</summary>
    public static Color? ColorFor(string archiveName, string requestedName)
    {
        if (Overrides.Count > 0)
        {
            string? key = Overrides.ContainsKey(archiveName) ? archiveName
                : Overrides.ContainsKey(requestedName) ? requestedName
                : null;
            if (key != null)
            {
                if (OverridesSeen.Add(key))
                {
                    Log.Info("world", $"tex override applied texture={archiveName} requested={requestedName} color={Hex(Overrides[key])}");
                }
                return Overrides[key];
            }
        }
        if (!_census)
        {
            return null;
        }
        var census = ColorForName(archiveName);
        if (!Registered.ContainsKey(archiveName))
        {
            Registered[archiveName] = census;
            string hex = Hex(census);
            // Eight bits per channel cap the palette at ~200k colours, so a few hundred textures
            // do occasionally land on the same one. Said out loud rather than left to be inferred
            // from a count that quietly covers two surfaces.
            if (ByColor.TryGetValue(hex, out var twin))
            {
                Log.Warn("world", $"tex census colour collision texture={archiveName} shares={twin} color={hex} — counts for both are one number");
            }
            else
            {
                ByColor[hex] = archiveName;
            }
            Log.Debug("world", $"tex census texture={archiveName} color={hex}");
            QueueFlush();
        }
        return census;
    }

    /// <summary>True when the aircraft paint substitution must stand aside: a hand-composited skin
    /// would paint over the very colour the instrument put there.</summary>
    public static bool Covers(string requestedName) =>
        Active && (_census || Overrides.ContainsKey(Path.GetFileNameWithoutExtension(requestedName)));

    /// <summary>Warns about an override naming a texture the archive does not hold. Checked per
    /// archive rather than once, because "not in this chapter" is itself the answer sometimes.</summary>
    public static void CheckNames(string archivePath, IEnumerable<string> names)
    {
        if (Overrides.Count == 0)
        {
            return;
        }
        var have = new HashSet<string>(names, StringComparer.OrdinalIgnoreCase);
        foreach (var name in Overrides.Keys)
        {
            if (!have.Contains(name))
            {
                Log.Warn("world", $"tex override names a texture this archive lacks texture={name} archive={Path.GetFileName(archivePath)}");
            }
        }
        // Present in the archive but used by no material is the other half of the same lie, and it
        // is only knowable after the build — end of frame, once every material has resolved.
        if (!_unusedQueued)
        {
            _unusedQueued = true;
            Callable.From(ReportUnusedOverrides).CallDeferred();
        }
    }

    private static void ReportUnusedOverrides()
    {
        _unusedQueued = false;
        foreach (var name in UnusedOverrides())
        {
            Log.Warn("world", $"tex override coloured nothing texture={name} — no material resolved it this session");
        }
    }

    /// <summary>The census colour of any name at all, archive membership irrelevant: a pure hash,
    /// so one texture wears one colour in every chapter and every run. Full brightness (one channel
    /// pinned to 255) and never a grey, because the readback identifies a pixel by its channel
    /// ratios and a grey has none.
    ///
    /// <para>The two free channels are drawn uniformly in <b>linear</b> ratio and converted to
    /// sRGB for storage, so the spread is even in the space the classifier measures — drawing them
    /// uniformly in sRGB bytes instead bunches them into the corners once gamma is undone.</para></summary>
    public static Color ColorForName(string name)
    {
        ulong h = Mix(Fnv1a(name.ToLowerInvariant()));
        // Two free ratios over 0..0.9 against the pinned channel: a continuous space of billions,
        // so an outright colour collision is vanishing and no two textures silently share an
        // identity. Crowding is the real hazard and is reported instead — the map's nearest-
        // neighbour column, and the contested counts in a shot report.
        float a = ((h >> 8) & 0xFFFF) / 65535f * 0.9f;
        float b = ((h >> 32) & 0xFFFF) / 65535f * 0.9f;
        var linear = (int)(h % 3) switch
        {
            0 => new Color(1f, a, b),
            1 => new Color(b, 1f, a),
            _ => new Color(a, b, 1f),
        };
        return linear.LinearToSrgb();
    }

    /// <summary>Repaints every texel's RGB, leaving size, format, alpha and (absent) mipmaps
    /// alone. Works on the raw buffer: a per-pixel setter would marshal tens of millions of calls
    /// across a chapter's textures.</summary>
    public static void Flatten(Image img, Color color)
    {
        var fmt = img.GetFormat();
        if (fmt is not (Image.Format.Rgb8 or Image.Format.Rgba8))
        {
            // Grayscale and packed formats have nowhere to put three channels. Converting is safe
            // here and only here: the alpha class was already read off the original pixels, and
            // the sampler sees the same values either way.
            img.Convert(fmt is Image.Format.La8 or Image.Format.Rgba4444 ? Image.Format.Rgba8 : Image.Format.Rgb8);
            fmt = img.GetFormat();
        }
        int stride = fmt == Image.Format.Rgba8 ? 4 : 3;
        var data = img.GetData();
        byte r = (byte)color.R8, g = (byte)color.G8, b = (byte)color.B8;
        for (int i = 0; i + stride <= data.Length; i += stride)
        {
            data[i] = r;
            data[i + 1] = g;
            data[i + 2] = b;
        }
        img.SetData(img.GetWidth(), img.GetHeight(), false, fmt, data);
    }

    /// <summary>One frame read back as texture coverage. <see cref="ByName"/> is a <b>lower
    /// bound</b> per texture and <see cref="ByName"/> + <see cref="ContestedByName"/> an upper one:
    /// a pixel whose runner-up census colour is nearly as close is never credited outright, because
    /// the renderer's per-vertex tinting moves a flat far enough to swap two crowded neighbours.</summary>
    public sealed class ShotCensus
    {
        public required int Total { get; init; }
        public required int Classified { get; init; }
        public required int Exact { get; init; }
        public required int Contested { get; init; }
        public required int Dark { get; init; }
        public required int Unmatched { get; init; }
        /// <summary>Texture name → pixels credited to it outright.</summary>
        public required Dictionary<string, int> ByName { get; init; }
        /// <summary>Texture name → pixels it won, but not by the required margin.</summary>
        public required Dictionary<string, int> ContestedByName { get; init; }
        /// <summary>Texture name → the runner-up that contested it most.</summary>
        public required Dictionary<string, string> RivalByName { get; init; }
        public required Dictionary<string, int> ExactByName { get; init; }
    }

    /// <summary>Counts, per texture, how many of a rendered frame's pixels that texture painted.
    /// Candidates are every texture the session resolved plus any <c>--tex-census=</c> extras, so a
    /// name that cannot be on screen is measured alongside the ones that can.</summary>
    public static ShotCensus Count(Image image)
    {
        var names = new List<string>(Registered.Count + Extra.Count);
        var chroma = new List<Vector3>(Registered.Count + Extra.Count);
        var exact = new Dictionary<uint, string>();
        foreach (var (name, color) in Registered)
        {
            Add(name, color);
        }
        foreach (var name in Extra)
        {
            if (!Registered.ContainsKey(name))
            {
                Add(name, ColorForName(name));
            }
        }

        void Add(string name, Color color)
        {
            names.Add(name);
            chroma.Add(ChromaSrgb(color));
            exact[Key((byte)color.R8, (byte)color.G8, (byte)color.B8)] = name;
        }

        var img = image;
        if (img.GetFormat() is not (Image.Format.Rgb8 or Image.Format.Rgba8))
        {
            img = (Image)image.Duplicate();
            img.Convert(Image.Format.Rgba8);
        }
        int stride = img.GetFormat() == Image.Format.Rgba8 ? 4 : 3;
        var data = img.GetData();

        // Distinct colours first: a frame holds far fewer of them than pixels, and each one only
        // has to be classified against the candidate set once.
        var histogram = new Dictionary<uint, int>();
        int total = 0;
        for (int i = 0; i + stride <= data.Length; i += stride)
        {
            uint key = Key(data[i], data[i + 1], data[i + 2]);
            histogram.TryGetValue(key, out int n);
            histogram[key] = n + 1;
            total++;
        }

        var byName = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var contestedByName = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var rivalPx = new Dictionary<string, Dictionary<string, int>>(StringComparer.OrdinalIgnoreCase);
        var exactByName = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        int classified = 0, exactPx = 0, contested = 0, dark = 0, unmatched = 0;
        foreach (var (key, count) in histogram)
        {
            if (exact.TryGetValue(key, out var hit))
            {
                exactByName.TryGetValue(hit, out int e);
                exactByName[hit] = e + count;
                exactPx += count;
            }
            var c = new Color(((key >> 16) & 0xFF) / 255f, ((key >> 8) & 0xFF) / 255f, (key & 0xFF) / 255f).SrgbToLinear();
            if (Mathf.Max(c.R, Mathf.Max(c.G, c.B)) < DarkFloor)
            {
                dark += count;
                continue;
            }
            var q = ChromaLinear(c);
            int best = -1, runnerUp = -1;
            float bestD = float.MaxValue, nextD = float.MaxValue;
            for (int i = 0; i < chroma.Count; i++)
            {
                float d = (chroma[i] - q).Length();
                if (d < bestD)
                {
                    nextD = bestD;
                    runnerUp = best;
                    bestD = d;
                    best = i;
                }
                else if (d < nextD)
                {
                    nextD = d;
                    runnerUp = i;
                }
            }
            if (best < 0 || bestD > Tolerance)
            {
                unmatched += count;
                continue;
            }
            if (nextD - bestD < Separation)
            {
                contestedByName.TryGetValue(names[best], out int a);
                contestedByName[names[best]] = a + count;
                if (runnerUp >= 0)
                {
                    if (!rivalPx.TryGetValue(names[best], out var rivals))
                    {
                        rivals = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                        rivalPx[names[best]] = rivals;
                    }
                    rivals.TryGetValue(names[runnerUp], out int rp);
                    rivals[names[runnerUp]] = rp + count;
                }
                contested += count;
                continue;
            }
            byName.TryGetValue(names[best], out int m);
            byName[names[best]] = m + count;
            classified += count;
        }
        var rivalByName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (name, rivals) in rivalPx)
        {
            string top = "";
            int topPx = -1;
            foreach (var (rival, px) in rivals)
            {
                if (px > topPx)
                {
                    topPx = px;
                    top = rival;
                }
            }
            rivalByName[name] = top;
        }
        return new ShotCensus
        {
            Total = total,
            Classified = classified,
            Exact = exactPx,
            Contested = contested,
            Dark = dark,
            Unmatched = unmatched,
            ByName = byName,
            ContestedByName = contestedByName,
            RivalByName = rivalByName,
            ExactByName = exactByName,
        };
    }

    /// <summary>Counts a saved frame and writes the report beside the census map. Called from the
    /// screenshot site, so one <c>--det --tex-census --screenshot</c> run answers "≥N px of texture
    /// X from pose Y" without a second tool.</summary>
    public static void CountShot(Image image, string shotPath)
    {
        if (!_census)
        {
            return;
        }
        Flush();
        var r = Count(image);
        // Ranked by the upper bound, so a texture whose coverage is mostly contested still shows up
        // near the top instead of hiding behind a confident but smaller neighbour.
        var seen = new List<string>();
        foreach (var name in r.ByName.Keys)
        {
            seen.Add(name);
        }
        foreach (var name in r.ContestedByName.Keys)
        {
            if (!r.ByName.ContainsKey(name))
            {
                seen.Add(name);
            }
        }
        int Upper(string n)
        {
            r.ByName.TryGetValue(n, out int p);
            r.ContestedByName.TryGetValue(n, out int a);
            return p + a;
        }
        seen.Sort((x, y) => Upper(y).CompareTo(Upper(x)));
        var json = new StringBuilder();
        json.AppendLine("{");
        json.AppendLine($"  \"shot\": {Quote(shotPath)},");
        json.AppendLine($"  \"tolerance\": {Tolerance.ToString("0.####", CultureInfo.InvariantCulture)},");
        json.AppendLine($"  \"separation\": {Separation.ToString("0.####", CultureInfo.InvariantCulture)},");
        json.AppendLine($"  \"pixels\": {r.Total},");
        json.AppendLine($"  \"classified\": {r.Classified},");
        json.AppendLine($"  \"exact\": {r.Exact},");
        json.AppendLine($"  \"contested\": {r.Contested},");
        json.AppendLine($"  \"dark\": {r.Dark},");
        json.AppendLine($"  \"unmatched\": {r.Unmatched},");
        json.AppendLine("  \"textures\": [");
        for (int i = 0; i < seen.Count; i++)
        {
            r.ByName.TryGetValue(seen[i], out int px);
            r.ContestedByName.TryGetValue(seen[i], out int cont);
            r.ExactByName.TryGetValue(seen[i], out int e);
            r.RivalByName.TryGetValue(seen[i], out var rival);
            json.AppendLine($"    {{\"name\": {Quote(seen[i])}, \"px\": {px}, \"contested\": {cont}, "
                + $"\"exact\": {e}, \"rival\": {Quote(rival ?? "")}}}"
                + (i == seen.Count - 1 ? "" : ","));
        }
        json.AppendLine("  ],");
        // Every candidate that painted nothing, named rather than merely absent: "0 px" is the
        // answer this instrument is asked for as often as a positive count.
        var zero = new List<string>();
        foreach (var name in Registered.Keys)
        {
            if (Upper(name) == 0)
            {
                zero.Add(name);
            }
        }
        foreach (var name in Extra)
        {
            if (Upper(name) == 0 && !Registered.ContainsKey(name))
            {
                zero.Add(name);
            }
        }
        zero.Sort(StringComparer.OrdinalIgnoreCase);
        json.AppendLine($"  \"absent\": [{string.Join(", ", zero.ConvertAll(Quote))}]");
        json.AppendLine("}");
        string path = Path.Combine(ScratchDir(), $"tex_census_{Path.GetFileNameWithoutExtension(shotPath)}.json");
        Directory.CreateDirectory(ScratchDir());
        File.WriteAllText(path, json.ToString());
        Log.Info("world", $"tex census shot={Path.GetFileName(shotPath)} px={r.Total} confident={r.Classified} contested={r.Contested} exact={r.Exact} dark={r.Dark} unmatched={r.Unmatched} textures={seen.Count} report={path}");
        for (int i = 0; i < seen.Count && i < 12; i++)
        {
            r.ByName.TryGetValue(seen[i], out int px);
            r.ContestedByName.TryGetValue(seen[i], out int cont);
            r.RivalByName.TryGetValue(seen[i], out var rival);
            Log.Info("world", $"tex census top texture={seen[i]} px={px} contested={cont} rival={(cont > 0 ? rival ?? "-" : "-")}");
        }
    }

    /// <summary>Writes the name→colour map. Deferred and coalesced: textures resolve in bursts
    /// during a world build, and the map carries a nearest-neighbour column that is quadratic in
    /// the entry count.</summary>
    public static void Flush()
    {
        _flushQueued = false;
        if (!_census || Registered.Count == 0)
        {
            return;
        }
        var names = new List<string>(Registered.Keys);
        names.Sort(StringComparer.OrdinalIgnoreCase);
        var chroma = new List<Vector3>(names.Count);
        foreach (var n in names)
        {
            chroma.Add(ChromaSrgb(Registered[n]));
        }
        var json = new StringBuilder();
        json.AppendLine("{");
        json.AppendLine($"  \"tolerance\": {Tolerance.ToString("0.####", CultureInfo.InvariantCulture)},");
        json.AppendLine($"  \"separation\": {Separation.ToString("0.####", CultureInfo.InvariantCulture)},");
        json.AppendLine($"  \"count\": {names.Count},");
        json.AppendLine($"  \"collisions\": {names.Count - ByColor.Count},");
        json.AppendLine("  \"textures\": [");
        for (int i = 0; i < names.Count; i++)
        {
            float nearest = float.MaxValue;
            string nearestName = "";
            for (int j = 0; j < names.Count; j++)
            {
                if (j == i)
                {
                    continue;
                }
                float d = (chroma[i] - chroma[j]).Length();
                if (d < nearest)
                {
                    nearest = d;
                    nearestName = names[j];
                }
            }
            var c = Registered[names[i]];
            json.AppendLine($"    {{\"name\": {Quote(names[i])}, \"color\": {Quote(Hex(c))}, "
                + $"\"rgb\": [{c.R8}, {c.G8}, {c.B8}], \"nearest\": {Quote(nearestName)}, "
                + $"\"nearest_dist\": {(names.Count > 1 ? nearest : 0f).ToString("0.####", CultureInfo.InvariantCulture)}}}"
                + (i == names.Count - 1 ? "" : ","));
        }
        json.AppendLine("  ]");
        json.AppendLine("}");
        string path = Path.Combine(ScratchDir(), "tex_census.json");
        Directory.CreateDirectory(ScratchDir());
        File.WriteAllText(path, json.ToString());
        Log.Info("world", $"tex census map textures={names.Count} collisions={names.Count - ByColor.Count} file={path}");
    }

    /// <summary>Override names that never matched a resolved texture — the loud half of the
    /// answer, since an override that coloured nothing looks exactly like an object that never
    /// drew.</summary>
    public static IReadOnlyList<string> UnusedOverrides()
    {
        var unused = new List<string>();
        foreach (var name in Overrides.Keys)
        {
            if (!OverridesSeen.Contains(name))
            {
                unused.Add(name);
            }
        }
        return unused;
    }

    private static void QueueFlush()
    {
        if (_flushQueued)
        {
            return;
        }
        _flushQueued = true;
        // End of this frame, once the build burst has stopped adding names.
        Callable.From(Flush).CallDeferred();
    }

    private static string ScratchDir() =>
        _scratchDir.Length > 0 ? _scratchDir : Path.GetFullPath(Path.Combine(ProjectSettings.GlobalizePath("res://"), "..", ".scratch"));

    // The identity a shaded pixel keeps: the linear colour divided by its brightest channel, which
    // any scalar dimming leaves exactly where it was.
    private static Vector3 ChromaSrgb(Color srgb) => ChromaLinear(srgb.SrgbToLinear());

    private static Vector3 ChromaLinear(Color linear)
    {
        float max = Mathf.Max(linear.R, Mathf.Max(linear.G, linear.B));
        return max <= 0f ? Vector3.Zero : new Vector3(linear.R / max, linear.G / max, linear.B / max);
    }

    private static uint Key(byte r, byte g, byte b) => ((uint)r << 16) | ((uint)g << 8) | b;

    private static string Hex(Color c) => $"{c.R8:x2}{c.G8:x2}{c.B8:x2}";

    private static string Quote(string s) =>
        "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";

    private static ulong Fnv1a(string s)
    {
        ulong h = 14695981039346656037UL;
        foreach (char c in s)
        {
            h = (h ^ (byte)c) * 1099511628211UL;
            h = (h ^ (byte)(c >> 8)) * 1099511628211UL;
        }
        return h;
    }

    private static ulong Mix(ulong z)
    {
        z += 0x9E3779B97F4A7C15UL;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }
}
