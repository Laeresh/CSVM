using System.Linq;
using CSVM.Utils;
using Godot;

namespace CSVM.Flight.Modes;

/// <summary>
/// Best-time persistence for stunt runs. The player's record is one JSON object in
/// <c>stunt_scores.json</c> in <see cref="UserData.Root"/>, never the repo. It is keyed
/// <c>chapter/mission/plane</c> (e.g. <c>C1/IA1/player_bhawk</c>) →
/// <c>{ best: seconds, date: "YYYY-MM-DD" }</c>, a course run's
/// <see cref="KeyFor"/>'s. A pinned or scripted run records into a throwaway store
/// instead (<see cref="ForSession(string, bool)"/>).
///
/// Read through Godot's <see cref="FileAccess"/>, written through <see cref="AtomicFile"/>, and
/// encoded by <see cref="Json.Stringify"/>, which is locale-neutral where <c>ToString()</c> is not.
/// A missing or corrupt file is an empty store, never an exception that breaks the scoreboard.
/// </summary>
public sealed class ScoreStore
{
    private const string StoreFile = "stunt_scores.json";

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
        ForSession(scoresPath, throwaway, System.IO.Path.Combine(UserData.Root, StoreFile));

    /// <summary>The key <paramref name="run"/> records under. A Danger Zone run's is
    /// <c>chapter/mission/plane</c>. A course run's is <c>course/</c> plus the course's chapter, name
    /// and <see cref="Course.Hash"/>, then the plane, so a renamed or edited course starts a fresh
    /// board. Its entry adds <c>penalties</c>, <c>splits</c> and <c>segments</c> beside the best, and
    /// holds the segments alone while no run under it has finished.</summary>
    public static string KeyFor(StuntMission run, string chapter, string mission, string plane) =>
        run.Course is { } c ? $"course/{c.Chapter}/{c.Name}/{c.Hash}/{plane}" : $"{chapter}/{mission}/{plane}";

    /// <summary>The penalty count a run records beside its best: a course run's touches. A Danger
    /// Zone run's is null, and its entry carries no penalties.</summary>
    public static int? PenaltiesOf(StuntMission run) => run.Course != null ? run.PenaltyCount : null;

    /// <summary>The split times a run records beside its best: a course run's clock at each sequence
    /// entry, penalties so far included, in sequence order. A Danger Zone run's is null.</summary>
    public static float[]? SplitsOf(StuntMission run) =>
        run.Course != null ? run.Zones.Select(z => z.CompletedAt).ToArray() : null;

    /// <summary>The time each sequence entry took from the one before, the first's from the clock's
    /// start: the steps between <paramref name="splits"/>. A penalty counts in the segment it landed in.</summary>
    public static float[] SegmentsOf(float[] splits) =>
        splits.Select((t, i) => i > 0 ? t - splits[i - 1] : t).ToArray();

    /// <summary>The faster of <paramref name="stored"/> and <paramref name="flown"/> segment by
    /// segment, any segment past the stored ones taken as flown. Null when the run improves none.</summary>
    public static float[]? FasterSegments(float[]? stored, float[] flown)
    {
        stored ??= System.Array.Empty<float>();
        var faster = new float[System.Math.Max(stored.Length, flown.Length)];
        bool improved = false;
        for (int i = 0; i < faster.Length; i++)
        {
            bool better = i < flown.Length && (i >= stored.Length || flown[i] < stored[i]);
            faster[i] = better ? flown[i] : stored[i];
            improved |= better;
        }

        return improved ? faster : null;
    }

    /// <summary>The stored best total, seconds, for this run key, or null if none is recorded yet.</summary>
    public float? GetBest(string key) => Field(key, "best")?.AsSingle();

    /// <summary>The touch penalties inside the stored best, or null for a key with none recorded,
    /// which every Danger Zone key is.</summary>
    public int? GetPenalties(string key) => Field(key, "penalties")?.AsInt32();

    /// <summary>The stored best's split at each sequence entry, or null for a key with none. A
    /// Danger Zone key carries none, and neither does a course entry stored without them.</summary>
    public float[]? GetSplits(string key) =>
        Field(key, "splits")?.AsFloat32Array() is { Length: > 0 } splits ? splits : null;

    /// <summary>The fastest each segment (<see cref="SegmentsOf"/>) has been flown in any run under
    /// this key, finished or not, or null for none. A key with splits and no segments reads its
    /// best's own.</summary>
    public float[]? GetSegments(string key) =>
        Field(key, "segments")?.AsFloat32Array() is { Length: > 0 } segments ? segments
        : GetSplits(key) is { } splits ? SegmentsOf(splits) : null;

    /// <summary>Records each segment of <paramref name="splits"/>, a run's clock at the entries flown
    /// so far, that beats the stored one, persisting immediately when any does.</summary>
    public void RecordSegments(string key, float[] splits)
    {
        Reread();
        if (FasterSegments(GetSegments(key), SegmentsOf(splits)) is not { } faster)
            return;
        var entry = _data.TryGetValue(key, out var v) && v.VariantType == Variant.Type.Dictionary
            ? v.AsGodotDictionary()
            : new Godot.Collections.Dictionary();
        entry["segments"] = faster;
        _data[key] = entry;
        Save();
    }

    /// <summary>Records <paramref name="total"/> as the new best for <paramref name="key"/> if it
    /// beats the stored one (or there is none), persisting immediately, with
    /// <paramref name="penalties"/> and <paramref name="splits"/> beside it when given. Returns true
    /// when it was a new best (the scoreboard flags NEW BEST), false when a stored time stands,
    /// never worsens a record. The best segments stand as they were.</summary>
    public bool RecordIfBest(string key, float total, int? penalties = null, float[]? splits = null)
    {
        Reread();
        var prev = GetBest(key);
        if (prev.HasValue && total >= prev.Value)
            return false;
        var entry = new Godot.Collections.Dictionary
        {
            { "best", total },
            { "date", Time.GetDateStringFromSystem() },
        };
        if (penalties is { } count)
            entry["penalties"] = count;
        if (splits != null)
            entry["splits"] = splits;
        if (Field(key, "segments") is { } segments)
            entry["segments"] = segments;
        _data[key] = entry;
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
    internal static ScoreStore Load(string storePath) =>
        new(storePath, ReadData(storePath) ?? new Godot.Collections.Dictionary());

    // The file's records, or null when it is absent, unreadable or malformed.
    private static Godot.Collections.Dictionary? ReadData(string storePath)
    {
        if (FileAccess.FileExists(storePath))
        {
            using var f = FileAccess.Open(storePath, FileAccess.ModeFlags.Read);
            if (f != null)
            {
                var parsed = Json.ParseString(f.GetAsText());
                if (parsed.VariantType == Variant.Type.Dictionary)
                    return parsed.AsGodotDictionary();
            }
            else
                Log.Warn("flight", $"stunt scores: could not read {storePath}: {FileAccess.GetOpenError()}");
        }
        return null;
    }

    private Variant? Field(string key, string field) =>
        _data.TryGetValue(key, out var v) && v.VariantType == Variant.Type.Dictionary
            && v.AsGodotDictionary().TryGetValue(field, out var value) ? value : (Variant?)null;

    // A board's store lives the whole session while a run HUD writes segments through its own. A
    // write therefore starts from the file as it stands, or this older copy would undo theirs. ⚠ Do
    // not drop the copy on a failed read: an empty one written back erases every record.
    private void Reread()
    {
        if (_storePath != null && ReadData(_storePath) is { } current)
        {
            _data.Clear();
            _data.Merge(current);
        }
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
