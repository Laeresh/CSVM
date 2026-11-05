using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;
using CSVM.Flight.Airframe;
using CSVM.Utils;

namespace CSVM.Flight.Audio;

/// <summary>
/// The amounts Enhanced Sound remixes the original's own sounds by, every one a TUNE read from
/// <c>CSVM/data/enhanced_sound.json</c>. They are the engine loop's pitch and gain at idle and at
/// full throttle, and the gain on the hits the player's own aircraft takes. The hit confirm's sound,
/// gain, pitch and rate limit follow. A field the file leaves out plays as Original Sound does. The
/// parse reads over a baseline, the way an override file lays its values over these, and a reload
/// replaces <see cref="Current"/> (fields: docs/formats/sounds.md, "Enhanced Sound").
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

    /// <summary>The committed file's location, read through Godot's file access since an exported
    /// build keeps it in the pck.</summary>
    public static string DefaultPath => "res://data/enhanced_sound.json";

    /// <summary>The amounts the running process plays Enhanced Sound at, <see cref="Neutral"/> until
    /// <see cref="Reload"/> reads the file. A live tuner may set it directly; the flight reads it on
    /// every play.</summary>
    public static EnhancedSoundTuning Current { get; internal set; } = Neutral;

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

    /// <summary>Reads <paramref name="path"/> (defaults to <see cref="DefaultPath"/>) into
    /// <see cref="Current"/> and returns it. Never throws: a missing or unreadable file warns and
    /// leaves <see cref="Neutral"/>.</summary>
    public static EnhancedSoundTuning Reload(string? path = null)
    {
        Current = Load(path);
        return Current;
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

        try
        {
            var problems = new List<string>();
            var tuning = Parse(viaGodot ? Godot.FileAccess.GetFileAsBytes(path) : File.ReadAllBytes(path), problems, baseline);
            foreach (var problem in problems)
            {
                Log.Warn("sound", $"enhanced sound: {path}: {problem}");
            }

            Log.Info("sound", $"enhanced sound: read {path}: {tuning.Describe()}");
            return tuning;
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            Log.Warn("sound", $"enhanced sound: {path} could not be read ({ex.Message}), the remix plays at its baseline");
            return baseline;
        }
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
        using var doc = JsonDocument.Parse(json.AsMemory(bom));
        var root = doc.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
        {
            problems.Add("not a JSON object, every field keeps its baseline");
            return baseline;
        }

        return baseline with
        {
            EnginePitchIdle = Number(root, "enginePitchIdle", baseline.EnginePitchIdle, MinPitch, MaxPitch, problems),
            EnginePitchFull = Number(root, "enginePitchFull", baseline.EnginePitchFull, MinPitch, MaxPitch, problems),
            EngineGainIdle = Number(root, "engineGainIdle", baseline.EngineGainIdle, 0f, MaxGain, problems),
            EngineGainFull = Number(root, "engineGainFull", baseline.EngineGainFull, 0f, MaxGain, problems),
            BulletHitGain = Number(root, "bulletHitGain", baseline.BulletHitGain, 0f, MaxGain, problems),
            CanopyHitGain = Number(root, "canopyHitGain", baseline.CanopyHitGain, 0f, MaxGain, problems),
            HitConfirmSound = Text(root, "hitConfirmSound", baseline.HitConfirmSound, problems),
            HitConfirmGain = Number(root, "hitConfirmGain", baseline.HitConfirmGain, 0f, MaxGain, problems),
            HitConfirmPitch = Number(root, "hitConfirmPitch", baseline.HitConfirmPitch, MinPitch, MaxPitch, problems),
            HitConfirmInterval = Number(root, "hitConfirmInterval", baseline.HitConfirmInterval, 0f, MaxInterval, problems),
        };
    }

    /// <summary>One line naming every amount, for the log.</summary>
    public string Describe() => string.Format(
        CultureInfo.InvariantCulture,
        "engine pitch {0:0.###}..{1:0.###} gain {2:0.###}..{3:0.###}, hit gains bullet x{4:0.###} canopy x{5:0.###}, "
        + "hit confirm {6} x{7:0.###} pitch {8:0.###} every {9:0.###} s",
        EnginePitchIdle, EnginePitchFull, EngineGainIdle, EngineGainFull, BulletHitGain, CanopyHitGain,
        HitConfirmSound, HitConfirmGain, HitConfirmPitch, HitConfirmInterval);

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
}
