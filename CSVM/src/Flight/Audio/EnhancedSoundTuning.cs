using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using CSVM.Extraction;
using CSVM.Flight.Airframe;
using CSVM.Utils;

namespace CSVM.Flight.Audio;

/// <summary>
/// The amounts Enhanced Sound remixes the original's own sounds by, every one a TUNE read from
/// <c>CSVM/data/enhanced_sound.json</c>. They are the engine loop's pitch and gain at idle and at
/// full throttle, and the gain on the hits the player's own aircraft takes. The hit confirm's sound,
/// gain, pitch and rate limit follow. A field the file leaves out plays as Original Sound does. The
/// parse reads over a baseline, as a sound pack's override (<see cref="HdSounds"/>) lies over
/// these, and a reload replaces <see cref="Current"/> (fields: docs/formats/sounds.md, "Enhanced Sound"). The sound lab
/// sets <see cref="Current"/> live and writes the file through <see cref="Save"/>.
/// </summary>
public sealed record EnhancedSoundTuning
{
    /// <summary>Every amount at Original Sound's own value: the decoded engine curve's ends and unity
    /// gains. The hit confirm draws the ricochet group at its own pitch.</summary>
    public static readonly EnhancedSoundTuning Neutral = new();

    // The sane range of a field. A value outside one is a typo, not a mix. The pitch bounds are the
    // mixer's own (EngineAudioCurves.MinPitch/MaxPitch), and four times a gain is +12 dB.
    internal const float MinPitch = 4000f / 22050f;
    internal const float MaxPitch = 55200f / 22050f;
    internal const float MaxGain = 4f;
    internal const float MaxInterval = 2f;

    private static readonly JsonSerializerOptions WrittenJson = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    // The shipped file's tuning while a pack's override is laid over Current, else null.
    private static EnhancedSoundTuning? _withoutPack;

    /// <summary>Every numeric amount in the file's order, with its key, its read range and its
    /// accessors. The parse, the save and the sound lab's sliders all walk this one list.</summary>
    public static IReadOnlyList<Field> Fields { get; } = new Field[]
    {
        new("enginePitchIdle", "Engine pitch at idle", MinPitch, MaxPitch, t => t.EnginePitchIdle, (t, v) => t with { EnginePitchIdle = v }),
        new("enginePitchFull", "Engine pitch at full throttle", MinPitch, MaxPitch, t => t.EnginePitchFull, (t, v) => t with { EnginePitchFull = v }),
        new("engineGainIdle", "Engine gain at idle", 0f, MaxGain, t => t.EngineGainIdle, (t, v) => t with { EngineGainIdle = v }),
        new("engineGainFull", "Engine gain at full throttle", 0f, MaxGain, t => t.EngineGainFull, (t, v) => t with { EngineGainFull = v }),
        new("bulletHitGain", "Bullet hit gain", 0f, MaxGain, t => t.BulletHitGain, (t, v) => t with { BulletHitGain = v }),
        new("canopyHitGain", "Canopy hit gain", 0f, MaxGain, t => t.CanopyHitGain, (t, v) => t with { CanopyHitGain = v }),
        new("hitConfirmGain", "Hit confirm gain", 0f, MaxGain, t => t.HitConfirmGain, (t, v) => t with { HitConfirmGain = v }),
        new("hitConfirmPitch", "Hit confirm pitch", MinPitch, MaxPitch, t => t.HitConfirmPitch, (t, v) => t with { HitConfirmPitch = v }),
        new("hitConfirmInterval", "Hit confirm interval (s)", 0f, MaxInterval, t => t.HitConfirmInterval, (t, v) => t with { HitConfirmInterval = v }),
    };

    /// <summary>The committed file's location, read through Godot's file access since an exported
    /// build keeps it in the pck.</summary>
    public static string DefaultPath => "res://data/enhanced_sound.json";

    /// <summary>The amounts the running process plays Enhanced Sound at, <see cref="Neutral"/> until
    /// <see cref="Reload"/> reads the file. A live tuner may set it directly; the flight reads it on
    /// every play.</summary>
    public static EnhancedSoundTuning Current { get; internal set; } = Neutral;

    /// <summary>The tuning Original Sound's hit confirm reads: <see cref="Current"/>, less a sound
    /// pack's override, which Original Sound never plays.</summary>
    public static EnhancedSoundTuning WithoutPack => _withoutPack ?? Current;

    /// <summary>The engine loop's pitch at idle throttle, the decoded curve's first point.</summary>
    public float EnginePitchIdle { get; init; } = 0.6f;

    /// <summary>The engine loop's pitch at full throttle, the decoded curve's second point.</summary>
    public float EnginePitchFull { get; init; } = 1f;

    /// <summary>The engine loop's curve gain at idle throttle. The decoded curve is flat 1.0.</summary>
    public float EngineGainIdle { get; init; } = 1f;

    /// <summary>The engine loop's curve gain at full throttle.</summary>
    public float EngineGainFull { get; init; } = 1f;

    /// <summary>The gain on top of <c>bullet_hit_sound</c>'s own VOLUME, a round landing on the
    /// player's own aircraft.</summary>
    public float BulletHitGain { get; init; } = 1f;

    /// <summary>The gain on top of <c>window_hit_sg</c>'s own VOLUME, a canopy hole opening.</summary>
    public float CanopyHitGain { get; init; } = 1f;

    /// <summary>The SOUND_GROUPS name the hit confirm draws from, one of the original's hit sounds.</summary>
    public string HitConfirmSound { get; init; } = "bullet_hit_sg";

    /// <summary>The gain on top of the drawn member's own VOLUME.</summary>
    public float HitConfirmGain { get; init; } = 1f;

    /// <summary>The hit confirm's pitch scale, which sets it apart from the same group heard as a hit
    /// on the player's own aircraft.</summary>
    public float HitConfirmPitch { get; init; } = 1f;

    /// <summary>The least time between two hit confirms, in sim seconds, so a burst landing
    /// together sounds once.</summary>
    public float HitConfirmInterval { get; init; } = 0.1f;

    /// <summary>The engine slot's pitch curve under Enhanced Sound: the decoded
    /// <paramref name="decoded"/> curve's throttle points, this tuning's two pitches.</summary>
    public SoundCurve PitchCurve(SoundCurve decoded) =>
        new(decoded.MinX, EnginePitchIdle, decoded.MaxX, EnginePitchFull);

    /// <summary>The engine slot's gain curve under Enhanced Sound, on the decoded curve's throttle
    /// points like <see cref="PitchCurve"/>.</summary>
    public SoundCurve GainCurve(SoundCurve decoded) =>
        new(decoded.MinX, EngineGainIdle, decoded.MaxX, EngineGainFull);

    /// <summary>Reads <paramref name="path"/> (defaults to <see cref="DefaultPath"/>) and the
    /// running sound pack's override over it into <see cref="Current"/>, and returns it. Never
    /// throws: a missing or unreadable file warns and leaves its baseline.</summary>
    public static EnhancedSoundTuning Reload(string? path = null)
    {
        var file = Load(path);
        Current = WithPack(file, HdSounds.Current);
        _withoutPack = HdSounds.Current.HasOverride ? file : null;
        return Current;
    }

    /// <summary><paramref name="file"/> with <paramref name="pack"/>'s override laid over it, or
    /// <paramref name="file"/> itself when the pack carries none.</summary>
    public static EnhancedSoundTuning WithPack(EnhancedSoundTuning file, HdSounds pack)
    {
        if (!pack.HasOverride)
        {
            return file;
        }

        string source = pack.OverrideSource!;
        return Read(source, () => pack.OverrideBytes() ?? throw new IOException("the override file could not be read"), file);
    }

    /// <summary>The tuning <paramref name="path"/> holds over <paramref name="baseline"/>
    /// (<see cref="Neutral"/> when null). Never throws: a missing or unreadable file warns and
    /// yields the baseline.</summary>
    public static EnhancedSoundTuning Load(string? path = null, EnhancedSoundTuning? baseline = null)
    {
        path ??= DefaultPath;
        baseline ??= Neutral;
        bool viaGodot = path.StartsWith("res://", StringComparison.Ordinal);
        if (viaGodot ? !Godot.FileAccess.FileExists(path) : !File.Exists(path))
        {
            Log.Warn("sound", $"enhanced sound: {path} not found, the remix plays at Original Sound's values");
            return baseline;
        }

        return Read(path, () => viaGodot ? Godot.FileAccess.GetFileAsBytes(path) : File.ReadAllBytes(path), baseline);
    }

    /// <summary>Parses the file's bytes over <paramref name="baseline"/>, with no file access and
    /// no engine calls. Throws <see cref="JsonException"/> on bytes that are not JSON. A field left
    /// out keeps the baseline's value; a field present but malformed or out of range does too, and
    /// is named in <paramref name="problems"/>.</summary>
    public static EnhancedSoundTuning Parse(byte[] json, List<string> problems, EnhancedSoundTuning? baseline = null)
    {
        baseline ??= Neutral;
        // An editor may save the file with a byte order mark, which the JSON reader refuses.
        int bom = json.Length >= 3 && json[0] == 0xEF && json[1] == 0xBB && json[2] == 0xBF ? 3 : 0;
        // Comments are skipped as Save's read (VegetationSidecar.ParseWhole) skips them.
        using var doc = JsonDocument.Parse(json.AsMemory(bom), new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip });
        var root = doc.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
        {
            problems.Add("not a JSON object, every field keeps its baseline");
            return baseline;
        }

        var tuning = baseline with { HitConfirmSound = Text(root, "hitConfirmSound", baseline.HitConfirmSound, problems) };
        foreach (var field in Fields)
        {
            tuning = field.With(tuning, Number(root, field.Key, field.Get(baseline), field.Min, field.Max, problems));
        }

        return tuning;
    }

    /// <summary>The project's copy of the file on disk while the game runs from the source tree. Null
    /// in an exported build, whose copy lives in the pck and cannot be written.</summary>
    public static string? SourceTreePath() =>
        Godot.OS.HasFeature("editor") ? Godot.ProjectSettings.GlobalizePath(DefaultPath) : null;

    /// <summary>Where a tuner saves and the values it starts from. Under <paramref name="pack"/>'s
    /// override it starts from <paramref name="file"/> with the override over it and writes the
    /// override alone, or nothing when it sits in a zip. Otherwise it starts from
    /// <paramref name="file"/> and writes <paramref name="sourceTreePath"/>.</summary>
    public static SaveTarget SaveTargetFor(HdSounds pack, EnhancedSoundTuning file, string? sourceTreePath)
    {
        if (!pack.HasOverride)
        {
            return sourceTreePath != null
                ? SaveTarget.To(sourceTreePath, file, null)
                : SaveTarget.Off("an exported build carries the tuning inside the pck", file, null);
        }

        var packed = WithPack(file, pack);
        return pack.OverridePath is { } overridePath
            ? SaveTarget.To(overridePath, packed, file)
            : SaveTarget.Off($"the sound pack's override {pack.OverrideSource} sits inside a zip, which Save never writes; unzip that pack into a folder to tune it", packed, file);
    }

    /// <summary>This tuning as the file holds it: every number at <see cref="Field.Rounded"/>
    /// precision, so a saved file reads back to exactly this value.</summary>
    public EnhancedSoundTuning Written()
    {
        var written = this;
        foreach (var field in Fields)
        {
            written = field.With(written, field.Rounded(field.Get(this)));
        }

        return written;
    }

    /// <summary>The file's text with every field set to <paramref name="tuning"/>'s
    /// <see cref="Written"/> value in place. Every other key, the <c>_about</c> note included, keeps
    /// its place, and a field the file lacks is appended. Throws <see cref="InvalidDataException"/>
    /// when <paramref name="existing"/> is not a JSON object, so a Save never replaces a file it
    /// cannot read. With <paramref name="under"/>, the file an override lies over, a field the file
    /// lacks and <paramref name="tuning"/> leaves at <paramref name="under"/>'s value stays out.</summary>
    public static string WithValues(string? existing, EnhancedSoundTuning tuning, EnhancedSoundTuning? under = null)
    {
        var root = Mech3.VegetationSidecar.ObjectToRewrite(existing, "tuning");
        var written = tuning.Written();
        var values = new Dictionary<string, JsonNode>(StringComparer.Ordinal);
        foreach (var field in Fields)
        {
            values[field.Key] = JsonValue.Create(Field.Decimal(field.Get(written)));
        }

        values["hitConfirmSound"] = JsonValue.Create(written.HitConfirmSound);

        // Rebuilt key by key, since a JsonObject cannot replace a member in its place.
        var rebuilt = new JsonObject();
        foreach (var (key, node) in root)
        {
            rebuilt[key] = values.Remove(key, out var value) ? value : node?.DeepClone();
        }

        var unchanged = under?.Written();
        foreach (var (key, value) in values)
        {
            if (unchanged == null || !SameAs(key, written, unchanged))
            {
                rebuilt[key] = value;
            }
        }

        return rebuilt.ToJsonString(WrittenJson) + System.Environment.NewLine;
    }

    /// <summary>Writes <paramref name="tuning"/> into the file at <paramref name="diskPath"/> through
    /// <see cref="WithValues"/> and <see cref="AtomicFile"/>, creating it when absent, and returns the
    /// values written.</summary>
    public static EnhancedSoundTuning Save(string diskPath, EnhancedSoundTuning tuning, EnhancedSoundTuning? under = null)
    {
        string? existing = File.Exists(diskPath) ? File.ReadAllText(diskPath, Encoding.UTF8) : null;
        AtomicFile.WriteAllText(diskPath, WithValues(existing, tuning, under));
        return tuning.Written();
    }

    /// <summary>One line naming every amount, for the log.</summary>
    public string Describe() => string.Format(
        CultureInfo.InvariantCulture,
        "engine pitch {0:0.###}..{1:0.###} gain {2:0.###}..{3:0.###}, hit gains bullet x{4:0.###} canopy x{5:0.###}, "
        + "hit confirm {6} x{7:0.###} pitch {8:0.###} every {9:0.###} s",
        EnginePitchIdle, EnginePitchFull, EngineGainIdle, EngineGainFull, BulletHitGain, CanopyHitGain,
        HitConfirmSound, HitConfirmGain, HitConfirmPitch, HitConfirmInterval);

    private static EnhancedSoundTuning Read(string origin, Func<byte[]> bytes, EnhancedSoundTuning baseline)
    {
        try
        {
            var problems = new List<string>();
            var tuning = Parse(bytes(), problems, baseline);
            foreach (var problem in problems)
            {
                Log.Warn("sound", $"enhanced sound: {origin}: {problem}");
            }

            Log.Info("sound", $"enhanced sound: read {origin}: {tuning.Describe()}");
            return tuning;
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            Log.Warn("sound", $"enhanced sound: {origin} could not be read ({ex.Message}), the remix plays at its baseline");
            return baseline;
        }
    }

    private static bool SameAs(string key, EnhancedSoundTuning a, EnhancedSoundTuning b)
    {
        foreach (var field in Fields)
        {
            if (field.Key == key)
            {
                return field.Get(a) == field.Get(b);
            }
        }

        return a.HitConfirmSound == b.HitConfirmSound;
    }

    private static float Number(JsonElement root, string field, float fallback, float min, float max, List<string> problems)
    {
        if (!root.TryGetProperty(field, out var value))
        {
            return fallback;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetSingle(out float number)
            && float.IsFinite(number) && number >= min && number <= max)
        {
            return number;
        }

        problems.Add(string.Format(CultureInfo.InvariantCulture,
            "\"{0}\" is not a number from {1:0.###} to {2:0.###}, it keeps {3:0.###}", field, min, max, fallback));
        return fallback;
    }

    private static string Text(JsonElement root, string field, string fallback, List<string> problems)
    {
        if (!root.TryGetProperty(field, out var value))
        {
            return fallback;
        }

        if (value.ValueKind == JsonValueKind.String && value.GetString() is { Length: > 0 } text)
        {
            return text;
        }

        problems.Add($"\"{field}\" is not a sound group name, it keeps {fallback}");
        return fallback;
    }

    /// <summary>One numeric amount: its file key, its label in the lab and the range the parse reads.
    /// The two accessors read it from a tuning and write it into one.</summary>
    public sealed record Field(string Key, string Label, float Min, float Max,
        Func<EnhancedSoundTuning, float> Get, Func<EnhancedSoundTuning, float, EnhancedSoundTuning> With)
    {
        // Three decimals keep a file a person reads by eye, and reach far finer than an ear tells apart.
        private const int Places = 3;
        private const decimal Scale = 1000m;

        /// <summary>The value Original Sound plays, <see cref="Neutral"/>'s.</summary>
        public float Default => Get(Neutral);

        /// <summary><paramref name="value"/> at the file's precision. A value that rounding carries
        /// past a bound is rounded toward the bound instead, so the file reads back in range.</summary>
        public float Rounded(float value)
        {
            decimal rounded = Decimal(value);
            if ((float)rounded < Min)
            {
                rounded = Math.Ceiling((decimal)Min * Scale) / Scale;
            }
            else if ((float)rounded > Max)
            {
                rounded = Math.Floor((decimal)Max * Scale) / Scale;
            }

            return (float)rounded;
        }

        /// <summary><paramref name="value"/> as the decimal the file writes.</summary>
        internal static decimal Decimal(float value) => Math.Round((decimal)value, Places, MidpointRounding.AwayFromZero);
    }

    /// <summary>Where a tuner's Save writes and the tuning it starts from (<see cref="SaveTargetFor"/>).
    /// <see cref="OffReason"/> is set exactly when <see cref="Path"/> is null.</summary>
    public sealed record SaveTarget
    {
        private SaveTarget(string? path, string? offReason, EnhancedSoundTuning saved, EnhancedSoundTuning? under)
        {
            Path = path;
            OffReason = offReason;
            Saved = saved;
            Under = under;
        }

        /// <summary>The file Save writes, or null when Save is off.</summary>
        public string? Path { get; }

        /// <summary>Why Save is off, null when it writes <see cref="Path"/>.</summary>
        public string? OffReason { get; }

        /// <summary>The tuning the target holds, which a tuner starts from.</summary>
        public EnhancedSoundTuning Saved { get; }

        /// <summary>The shipped file's tuning when the target is an override that lies over it, else null.</summary>
        public EnhancedSoundTuning? Under { get; }

        /// <summary>A target Save writes at <paramref name="path"/>.</summary>
        public static SaveTarget To(string path, EnhancedSoundTuning saved, EnhancedSoundTuning? under) => new(path, null, saved, under);

        /// <summary>A target Save refuses, for <paramref name="reason"/>.</summary>
        public static SaveTarget Off(string reason, EnhancedSoundTuning saved, EnhancedSoundTuning? under) => new(null, reason, saved, under);
    }
}
