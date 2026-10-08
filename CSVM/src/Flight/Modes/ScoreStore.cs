using CSVM.Utils;
using Godot;

namespace CSVM.Flight.Modes;

/// <summary>
/// Best-time persistence for stunt runs. The player's record is one JSON object in
/// <c>user://stunt_scores.json</c>, the engine's writable user dir, never the repo. It is keyed
/// <c>chapter/mission/plane</c> (e.g. <c>C1/IA1/player_bhawk</c>) →
/// <c>{ best: seconds, date: "YYYY-MM-DD" }</c>. A pinned or scripted run records into a
/// throwaway store instead (<see cref="ForSession(string, bool)"/>).
///
/// Read through Godot's <see cref="FileAccess"/>, written through <see cref="AtomicFile"/>, and
/// encoded by <see cref="Json.Stringify"/>, which is locale-neutral where <c>ToString()</c> is not.
/// A missing or corrupt file is an empty store, never an exception that breaks the scoreboard.
/// </summary>
public sealed class ScoreStore
{
    private const string DefaultStorePath = "user://stunt_scores.json";

    // Null for a throwaway store, which records in memory and never writes a file.
    private readonly string? _storePath;
    private readonly Godot.Collections.Dictionary _data;

    private ScoreStore(string? storePath, Godot.Collections.Dictionary data)
    {
        _storePath = storePath;
        _data = data;
    }

    /// <summary>Whether this store writes nothing, which a scripted run's store does.</summary>
    public bool Throwaway => _storePath == null;

    /// <summary>The store a session's boards record into, from the spec's <c>ScoresPath</c> and
    /// <c>ScoresThrowaway</c>. That is the <c>--scores=</c> file when a launch named one, an empty
    /// throwaway when <paramref name="throwaway"/> holds, else the player's own
    /// <c>user://stunt_scores.json</c>.</summary>
    public static ScoreStore ForSession(string? scoresPath, bool throwaway) =>
        ForSession(scoresPath, throwaway, DefaultStorePath);

    /// <summary>The stored best total, seconds, for this run key, or null if none is recorded yet.</summary>
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
    /// new best (the scoreboard flags NEW BEST), false when a stored time stands, never worsens a
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

    /// <summary><see cref="ForSession(string, bool)"/> over another player store, so a suite can
    /// prove which file a run writes without reading the real one. ⚠ Every session call site goes
    /// through this choice. A session that loads the player path itself lets a scripted run's
    /// synthetic time into the player's record.</summary>
    internal static ScoreStore ForSession(string? scoresPath, bool throwaway, string playerStorePath)
    {
        if (!string.IsNullOrWhiteSpace(scoresPath))
        {
            return Load(scoresPath.Contains("://") ? scoresPath : System.IO.Path.GetFullPath(scoresPath));
        }
        return throwaway
            ? new ScoreStore(null, new Godot.Collections.Dictionary())
            : Load(playerStorePath);
    }

    /// <summary>Loads the store at <paramref name="storePath"/>, or an empty one if the file is
    /// absent, unreadable or malformed. A suite points it at a path under its own scratch
    /// directory, never at the player's own <c>user://stunt_scores.json</c>.</summary>
    internal static ScoreStore Load(string storePath)
    {
        if (FileAccess.FileExists(storePath))
        {
            using var f = FileAccess.Open(storePath, FileAccess.ModeFlags.Read);
            if (f != null)
            {
                var parsed = Json.ParseString(f.GetAsText());
                if (parsed.VariantType == Variant.Type.Dictionary)
                    return new ScoreStore(storePath, parsed.AsGodotDictionary());
            }
            else
                GD.PushWarning($"stunt scores: could not read {storePath}: {FileAccess.GetOpenError()}");
        }
        return new ScoreStore(storePath, new Godot.Collections.Dictionary());
    }

    private void Save()
    {
        if (_storePath == null)
            return;
        string path = _storePath.Contains("://") ? ProjectSettings.GlobalizePath(_storePath) : _storePath;
        try
        {
            AtomicFile.WriteAllText(path, Json.Stringify(_data, "  "));
        }
        catch (System.Exception e) when (e is System.IO.IOException or System.UnauthorizedAccessException)
        {
            Log.Warn("flight", $"stunt scores: could not write {_storePath}: {e.Message}");
        }
    }
}
