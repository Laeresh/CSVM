using Godot;

namespace CrimsonSkies.Flight;

/// <summary>
/// Best-time persistence for stunt runs (Milestone 2.5 item 3). One JSON object in
/// <c>user://stunt_scores.json</c> — the engine's writable user dir, never the repo (the hard
/// no-assets rule and, besides, scores are per-player) — keyed <c>chapter/mission/plane</c>
/// (e.g. <c>C1/IA1/player_bhawk</c>) → <c>{ best: seconds, date: "YYYY-MM-DD" }</c>.
///
/// Read/written through Godot's <see cref="FileAccess"/> + <see cref="Json"/> rather than
/// System.Text.Json: only the Godot API resolves the <c>user://</c> scheme. A missing or
/// corrupt file is an empty store (a first run has no best), never an exception — a persistence
/// hiccup must not break the scoreboard.
/// </summary>
public sealed class ScoreStore
{
    private const string StorePath = "user://stunt_scores.json";

    private readonly Godot.Collections.Dictionary _data;

    private ScoreStore(Godot.Collections.Dictionary data) => _data = data;

    /// <summary>Loads the store, or an empty one if the file is absent/unreadable/malformed.</summary>
    public static ScoreStore Load()
    {
        if (FileAccess.FileExists(StorePath))
        {
            using var f = FileAccess.Open(StorePath, FileAccess.ModeFlags.Read);
            if (f != null)
            {
                var parsed = Json.ParseString(f.GetAsText());
                if (parsed.VariantType == Variant.Type.Dictionary)
                    return new ScoreStore(parsed.AsGodotDictionary());
            }
            else
                GD.PushWarning($"stunt scores: could not read {StorePath}: {FileAccess.GetOpenError()}");
        }
        return new ScoreStore(new Godot.Collections.Dictionary());
    }

    /// <summary>The stored best total, seconds, for this run key — or null if none is recorded yet.</summary>
    public float? GetBest(string key)
    {
        if (_data.TryGetValue(key, out var v) && v.VariantType == Variant.Type.Dictionary)
        {
            var entry = v.AsGodotDictionary();
            if (entry.TryGetValue("best", out var best))
                return best.AsSingle();
        }
        return null;
    }

    /// <summary>Records <paramref name="total"/> as the new best for <paramref name="key"/> if it
    /// beats the stored one (or there is none), persisting immediately. Returns true when it was a
    /// new best (the scoreboard flags NEW BEST), false when a stored time stands — never worsens a
    /// record.</summary>
    public bool RecordIfBest(string key, float total)
    {
        var prev = GetBest(key);
        if (prev.HasValue && total >= prev.Value)
            return false;
        _data[key] = new Godot.Collections.Dictionary
        {
            { "best", total },
            { "date", Time.GetDateStringFromSystem() },
        };
        Save();
        return true;
    }

    private void Save()
    {
        using var f = FileAccess.Open(StorePath, FileAccess.ModeFlags.Write);
        if (f == null)
        {
            GD.PushWarning($"stunt scores: could not write {StorePath}: {FileAccess.GetOpenError()}");
            return;
        }
        f.StoreString(Json.Stringify(_data, "  "));
    }
}
