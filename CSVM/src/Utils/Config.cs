using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using CSVM.Flight;
using Godot;

namespace CSVM.Utils;

/// <summary>
/// Dev-facing tuning-override layer. The hand-tuned <c>const</c>s stay in their modules as the
/// DEFAULT (with the rationale comments that explain them); this reads an optional sparse
/// <c>res://config.json</c> and, for any key present there, overrides that default. A missing
/// file, a missing key, or a wrong-typed value all fall through to the default — so deleting
/// config.json reproduces stock behaviour exactly, which is what keeps scripted <c>--screenshot</c>
/// runs byte-identical.
///
/// <para>Access is read-through at the point of use:
/// <c>Config.GetFloat("flightModel.thrustConst", ThrustConst)</c>. Keys are
/// <c>moduleCamelCase.fieldCamelCase</c>, grouped one nesting level in the JSON
/// (<c>{"flightModel": {"thrustConst": 40}}</c>) and flattened to dot-keys internally. Comments
/// and trailing commas are tolerated on input so a hand-edited file can carry notes.</para>
///
/// <para>Every query self-registers <c>(key, default)</c> so <see cref="DumpConfig"/> can emit a
/// fully populated template. A key present in the file but never queried is an orphan (a typo or
/// stale key that silently does nothing) and is warned about by <see cref="ReportOrphans"/>; a
/// queried key absent from a loaded file warns once (its default is used).</para>
///
/// <para>Read-only this pass: nothing writes config.json. <c>res://</c> was chosen so a writable
/// <c>user://</c> layer can later stack under the getters without touching a single call site.</para>
/// </summary>
public static class Config
{
    /// <summary>The tuning file, in the CSVM project folder (<c>res://</c> maps there).</summary>
    public const string DefaultResPath = "res://config.json";

    // Flattened dot-key -> leaf value. The JsonElements point into _doc's buffer, so _doc is kept
    // alive for the process; a re-Load disposes the old one first.
    private static readonly Dictionary<string, JsonElement> _values = new(StringComparer.Ordinal);

    // Every key ever queried -> its boxed default, for --dump-config. Sorted so the dump is stable.
    private static readonly SortedDictionary<string, object> _registry = new(StringComparer.Ordinal);

    // Warn-once dedup sets: a queried-but-missing key, and a present-but-wrong-typed key.
    private static readonly HashSet<string> _warnedMissing = new(StringComparer.Ordinal);
    private static readonly HashSet<string> _warnedType = new(StringComparer.Ordinal);

    private static JsonDocument? _doc;
    private static bool _fileLoaded;   // a config.json existed and parsed (vs. pure-defaults run)

    /// <summary>How many override keys the loaded file supplied (0 when there is no file).</summary>
    public static int OverrideCount => _values.Count;

    /// <summary>How many distinct tunable keys have been queried so far (the --dump-config size).</summary>
    public static int RegisteredCount => _registry.Count;

    /// <summary>Drop every loaded override, so all subsequent reads take their in-code default.
    /// <para>A deterministic run uses this: <c>config.json</c> is a git-ignored dev tuning file, so a
    /// capture that honoured it would be a function of one machine's uncommitted state rather than of
    /// the committed tree — the same shot then differs between a checkout and a worktree, and a
    /// golden hash silently bakes in whatever someone was tuning that day.</para></summary>
    public static void ClearOverrides()
    {
        _values.Clear();
        _doc?.Dispose();
        _doc = null;
        _fileLoaded = false;
        _warnedMissing.Clear();
        _warnedType.Clear();
    }

    /// <summary>Parse <paramref name="resPath"/> into the override dictionary. Missing file → no
    /// overrides (all in-code defaults). Malformed JSON, or a non-object root → one error line and
    /// no overrides; never throws, because a dev tool must not crash the game over a stray comma.</summary>
    public static void Load(string resPath = DefaultResPath)
    {
        _values.Clear();
        _doc?.Dispose();
        _doc = null;
        _fileLoaded = false;
        _warnedMissing.Clear();
        _warnedType.Clear();

        string path = ProjectSettings.GlobalizePath(resPath);
        if (!File.Exists(path))
        {
            Log.Info("core", $"config absent file={resPath} — using in-code defaults");
            return;
        }
        try
        {
            var opts = new JsonDocumentOptions
            {
                CommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true,
            };
            var doc = JsonDocument.Parse(File.ReadAllBytes(path), opts);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                Log.Error("core", $"config root is not a JSON object file={resPath} — ignoring the file");
                doc.Dispose();
                return;
            }
            _doc = doc;
            Flatten(doc.RootElement, "", _values);
            _fileLoaded = true;
            Log.Info("core", $"config loaded overrides={_values.Count} file={resPath}");
        }
        catch (JsonException e)
        {
            Log.Error("core", $"config is not valid JSON file={resPath} — using in-code defaults", e);
            _values.Clear();
        }
    }

    /// <summary>Override for <paramref name="key"/> if present and a number, else
    /// <paramref name="fallback"/> (returned verbatim, so an absent file is byte-identical).</summary>
    public static float GetFloat(string key, float fallback)
    {
        Register(key, fallback);
        if (TryLeaf(key, out var el))
        {
            if (el.ValueKind == JsonValueKind.Number && el.TryGetSingle(out float v))
            {
                return v;
            }
            WarnType(key, "number", el.ValueKind);
        }
        return fallback;
    }

    /// <summary>Override for <paramref name="key"/> if present and an integer, else the fallback.</summary>
    public static int GetInt(string key, int fallback)
    {
        Register(key, fallback);
        if (TryLeaf(key, out var el))
        {
            if (el.ValueKind == JsonValueKind.Number && el.TryGetInt32(out int v))
            {
                return v;
            }
            WarnType(key, "integer", el.ValueKind);
        }
        return fallback;
    }

    /// <summary>Override for <paramref name="key"/> if present and a bool, else the fallback.</summary>
    public static bool GetBool(string key, bool fallback)
    {
        Register(key, fallback);
        if (TryLeaf(key, out var el))
        {
            if (el.ValueKind == JsonValueKind.True)
            {
                return true;
            }
            if (el.ValueKind == JsonValueKind.False)
            {
                return false;
            }
            WarnType(key, "bool", el.ValueKind);
        }
        return fallback;
    }

    /// <summary>Override for <paramref name="key"/> if present and a string, else the fallback.</summary>
    public static string GetString(string key, string fallback)
    {
        Register(key, fallback);
        if (TryLeaf(key, out var el))
        {
            if (el.ValueKind == JsonValueKind.String)
            {
                return el.GetString() ?? fallback;
            }
            WarnType(key, "string", el.ValueKind);
        }
        return fallback;
    }

    /// <summary>Warn loudly about every key present in config.json that no getter ever queried —
    /// a typo or a stale key that is silently doing nothing. Call after the tunable code paths have
    /// been exercised (the startup registry warmup does this), so the registry is populated.</summary>
    public static void ReportOrphans()
    {
        foreach (string key in _values.Keys)
        {
            if (!_registry.ContainsKey(key))
            {
                Log.Warn("core", $"config key matches no tunable key={key} — ignored (typo? wrong block?)");
            }
        }
    }

    /// <summary>Exercise each Config-wired module's tunable reads once, with a throwaway instance and
    /// no game data, so Config's registry knows the full key set. That lets <see cref="ReportOrphans"/>
    /// flag config.json typos at startup and <c>--dump-config</c> emit a complete template — without a
    /// built world. Read-through means the reads register on execution, so a single dummy step is the
    /// cheapest way to run them. Add a line here as each module is wired to Config.</summary>
    public static void WarmTuningRegistry()
    {
        try
        {
            var fm = new FlightModel(new PlaneStats());
            fm.Reset(Vector3.Zero, Basis.Identity, 100f, 1f);
            fm.Step(default, 1f / 60f);
            // ProjectilePool reads this only on a live rocket shot, which the warmup never fires —
            // register it here so --dump-config still documents the weapon-fire tunable.
            GetFloat("weapons.rocketSpeedScale", ProjectilePool.RocketSpeedScale);
            // Tracer look (C23) reads only from a live Spawn/RenderTracers, which the warmup never
            // drives (no ProjectilePool here) — register them here so --dump-config documents them.
            GetFloat("weapons.tracerLength", ProjectilePool.TracerLength);
            GetFloat("weapons.tracerWidth", ProjectilePool.TracerWidth);
            GetFloat("weapons.tracerBrightness", ProjectilePool.TracerBrightness);
            GetFloat("weapons.tracerMinPixels", ProjectilePool.TracerMinPixels);
            // The gun-ammo / ordnance testing caps are read only when a plane binds its loadout, which
            // the warmup never does — register them here so --dump-config still documents them.
            GetInt("weapons.gunAmmoCap", FlightController.GunAmmoCapDefault);
            GetInt("weapons.ordnanceCap", FlightController.OrdnanceCapDefault);
            // The whine mix gain is read only from a live FlightAudio.Update, which the warmup never
            // drives (no SoundArchive here) — register it here so --dump-config still documents it.
            GetFloat("flightAudio.whineMixGain", FlightAudio.WhineMixGain);
            // Same reason as whineMixGain: the damaged-engine loop is only read from a live
            // FlightAudio.Update with damage data, which the warmup never drives.
            GetFloat("flightAudio.damagedEngineMixGain", FlightAudio.DamagedEngineMixGain);
            // Same reason: the engine dual-stack detune ratio (BL-078) is only read from a live
            // FlightAudio.Update, which the warmup never drives.
            GetFloat("flightAudio.engineDetuneRatio", FlightAudio.EngineDetuneRatio);
            // Puffer emitters read these at Init, which the warmup never reaches (an emitter needs
            // a texture archive) — register them here so --dump-config still documents them.
            GetFloat("puffer.burstSizeScale", Effects.Puffer.SizeScaleDefault);
            GetFloat("puffer.trailSizeScale", Effects.Puffer.SizeScaleDefault);
            GetFloat("puffer.sustainSizeScale", Effects.Puffer.SizeScaleDefault);
        }
        catch (Exception e)
        {
            GD.PushWarning($"config: tuning-registry warmup failed ({e.Message}); --dump-config may be incomplete");
        }
    }

    /// <summary>Write a fully-populated template (every registered key, nested by block, its default
    /// as the value) to <paramref name="path"/>. Copy it to <c>res://config.json</c> and edit the
    /// values you want to override; delete the rest (the file is meant to stay sparse).</summary>
    public static void DumpConfig(string path)
    {
        // Rebuild the nested object from the dot-keyed registry.
        var root = new SortedDictionary<string, object>(StringComparer.Ordinal);
        foreach (var (key, def) in _registry)
        {
            string[] parts = key.Split('.');
            var node = root;
            for (int i = 0; i < parts.Length - 1; i++)
            {
                if (!node.TryGetValue(parts[i], out object? child) || child is not SortedDictionary<string, object> dict)
                {
                    dict = new SortedDictionary<string, object>(StringComparer.Ordinal);
                    node[parts[i]] = dict;
                }
                node = dict;
            }
            node[parts[^1]] = def;
        }

        string? dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }
        File.WriteAllText(path, JsonSerializer.Serialize(root, new JsonSerializerOptions { WriteIndented = true }));
    }

    // Walk the nested object into dot-keys; a nested object recurses, any scalar/array leaf is stored.
    private static void Flatten(JsonElement obj, string prefix, Dictionary<string, JsonElement> into)
    {
        foreach (var prop in obj.EnumerateObject())
        {
            string key = prefix.Length == 0 ? prop.Name : $"{prefix}.{prop.Name}";
            if (prop.Value.ValueKind == JsonValueKind.Object)
            {
                Flatten(prop.Value, key, into);
            }
            else
            {
                into[key] = prop.Value;
            }
        }
    }

    // Record the key's default the first time it is queried (for the template dump).
    private static void Register(string key, object fallback)
    {
        if (!_registry.ContainsKey(key))
        {
            _registry[key] = fallback;
        }
    }

    // Fetch a present override, or warn once (only when a file was actually loaded — with no file
    // the single Load() line already says "using defaults", and warning per key would be noise).
    private static bool TryLeaf(string key, out JsonElement el)
    {
        if (_values.TryGetValue(key, out el))
        {
            return true;
        }
        // Info, not Warn: a sparse config.json is the intended shape, so most of these misses are
        // expected and must not read as problems. (Log.Warn is plain text either way — the reason
        // this was never GD.PushWarning is that Godot appends a useless C# stack trace to each.)
        if (_fileLoaded && _warnedMissing.Add(key))
        {
            Log.Info("core", $"config key absent key={key} — using its in-code default");
        }
        return false;
    }

    private static void WarnType(string key, string want, JsonValueKind got)
    {
        if (_warnedType.Add(key))
        {
            Log.Warn("core", $"config key is wrong-typed key={key} got={got} want={want} — using its in-code default");
        }
    }
}
