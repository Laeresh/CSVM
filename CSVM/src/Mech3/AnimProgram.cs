using System;
using System.Collections.Generic;
using System.IO;
using Godot;

namespace CSVM.Mech3;

/// <summary>
/// Every animation definition visible to one mission, merged from the two sources that
/// carry them, plus the startanims list that says which run at mission start.
///
/// Why two sources (see docs/formats/anim-definitions.md): the
/// compiled <c>cam_anim.zbd</c>/<c>mis_anim.zbd</c> archives are the better data — typed
/// events, node references resolved to names, and the SI motion scripts, which exist
/// nowhere else — but they are not complete. <c>startanims</c> is reader-only and never
/// appears compiled. Hence: load both, prefer compiled on collision, keep the remainder.
///
/// **The mission zrdr scope is a library, not a manifest** (confirmed against user
/// observation of the original). A mission folder ships reader files it never uses:
/// C1/IA1 carries a <c>zepstate.zrd.json</c> hiding <c>dliner1</c> and <c>cargotrain</c>,
/// but its <c>mis_anim</c> compiles neither — and in the original both ARE present in
/// Instant Action (the passenger-hangar zeppelin and the parked train in the cut). The
/// missions that genuinely hide them, C1/M04, compile both; C1/M02 compiles
/// <c>hk_zep</c>+<c>tethertower</c>, which is the one mission where the original drops the
/// tether tower. Verified over every zepstate in the install:
///
///   C1/IA1, C1B/IA1, C1C/IA1, C2/IA1, C2B/IA1, C4/IA1 → all zepstate defs UNUSED
///   C1/M02 (hk_zep, lkshadow, tethershadow, tethertower), C1/M04 (dliner1, cargotrain),
///   C3/IA1, C3/M02, C3/M03, C4/M03 (cargozep1)          → COMPILED
///
/// C3/IA1 compiling <c>cargozep1</c> is why the rule is "check the compiled set", not
/// "Instant Action ignores zepstate". So a mission-scope reader def applies only when the
/// mission's compiled archive contains it. The shared and chapter scopes stay unconditional
/// — they are the world's own furniture, not a per-mission roster.
/// </summary>
public sealed class AnimProgram
{
    /// <summary>Every definition, compiled-preferred, in load order.</summary>
    public readonly List<AnimDefinition> Defs = new();

    /// <summary>The mission's NEW_GAME_START animation names, in order (startanims.json).
    /// Order matters — C1/IA1 runs hangar3_doors then mp_hangar3_open over the same doors,
    /// and the last write wins.</summary>
    public readonly List<string> StartAnims = new();

    /// <summary>Mission-scope reader defs the mission's compiled manifest does not list, so
    /// were not instantiated (diagnostics — this is the C1/IA1 zeppelin + parked train).</summary>
    public readonly List<string> MissionLibrarySkipped = new();

    private readonly Dictionary<string, List<AnimDefinition>> _byAnimName =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly HashSet<string> _seen = new(StringComparer.OrdinalIgnoreCase);

    public int CompiledCount { get; private set; }
    public int ReaderCount { get; private set; }
    public int ScriptPoolCount { get; private set; }

    /// <summary>
    /// Loads the animation program for one mission. The zrdr paths are the three scopes a
    /// mission sees (shared / chapter / mission); the anim paths are the chapter's
    /// <c>cam_anim</c> and the mission's <c>mis_anim</c> extractions. Any of them may be
    /// missing — a user who has not re-run ExtractAssets.ps1 simply gets the reader-only
    /// behaviour this project had before compiled animations landed.
    /// </summary>
    public static AnimProgram Load(string sharedZrdr, string chapterZrdr, string missionZrdr,
        string chapterAnimPath, string missionAnimPath)
    {
        var program = new AnimProgram();

        // Compiled first, so it wins every collision.
        var missionManifest = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        bool haveMissionManifest = false;
        foreach (var (path, label) in new[] { (chapterAnimPath, "cam_anim"), (missionAnimPath, "mis_anim") })
        {
            if (AnimArchive.Load(path, label) is not { } archive)
                continue;
            program.ScriptPoolCount += archive.ScriptCount;
            bool isMission = label == "mis_anim";
            if (isMission)
                haveMissionManifest = true;
            foreach (var def in archive.Defs)
            {
                if (isMission)
                    missionManifest.Add(KeyOf(def));
                program.Add(def, compiled: true);
            }
        }

        // Then the readers, which fill in what was never compiled. Shared and chapter scopes
        // are unconditional; the MISSION scope is gated by that mission's compiled manifest
        // (see the class remarks — an uncompiled mission-scope def is library content this
        // mission does not instantiate). Gated only when the manifest actually loaded, so an
        // extraction without mis_anim degrades to the previous behaviour rather than to an
        // empty program.
        foreach (var zrdr in new[] { sharedZrdr, chapterZrdr })
            foreach (var def in AnimDefs.LoadArchive(zrdr))
                program.Add(def, compiled: false);

        foreach (var def in AnimDefs.LoadArchive(missionZrdr))
        {
            if (haveMissionManifest && !missionManifest.Contains(KeyOf(def)))
            {
                program.MissionLibrarySkipped.Add($"{def.Name}/{def.AnimName}");
                continue;
            }
            program.Add(def, compiled: false);
        }

        program.LoadStartAnims(missionZrdr);
        return program;
    }

    /// <summary>The chapter/mission compiled-archive paths for a session, preferring the
    /// unpacked sibling directory ExtractAssets.ps1 -Unzip leaves next to each zip (loose
    /// JSON: no decompression per def), exactly like every other loader in this project.</summary>
    public static (string Chapter, string Mission) ArchivePaths(string repoRoot, string chapter, string mission)
    {
        static string Prefer(string zipPath)
        {
            var dir = Path.Combine(Path.GetDirectoryName(zipPath) ?? "",
                Path.GetFileNameWithoutExtension(zipPath));
            return Directory.Exists(dir) ? dir : zipPath;
        }
        return (Prefer(Path.Combine(repoRoot, "extracted", chapter, "cam_anim.zip")),
                Prefer(Path.Combine(repoRoot, "extracted", chapter, mission, "mis_anim.zip")));
    }

    /// <summary>Definitions an ANIMATION_NAME refers to (startanims entries, CALL_ANIMATION
    /// targets). Several defs can share a name — template anims instantiate per object.</summary>
    public IReadOnlyList<AnimDefinition> ByAnimName(string animName) =>
        _byAnimName.TryGetValue(animName, out var list) ? list : Array.Empty<AnimDefinition>();

    /// <summary>A minimal program holding only the definitions reachable from
    /// <paramref name="rootAnimName"/> through CALL_ANIMATION — the transitive call closure —
    /// reusing the same <see cref="AnimDefinition"/> objects (so SI-script resolution through
    /// <c>def.Archive</c> still works). The per-player crash runtime binds THIS, not the whole
    /// world program: bound to a scoped plane subtree, the full program's ~150 generic-named world
    /// defs (<c>healthy</c>/<c>destroyed</c>/light names/…) mis-anchor onto plane parts and run
    /// their reset states on the aircraft. The closure is just the crash def plus the effects it
    /// fires, so nothing irrelevant anchors. StartAnims are left empty (the crash runtime never
    /// auto-starts).</summary>
    public AnimProgram Subset(string rootAnimName) => Subset(new[] { rootAnimName });

    /// <summary>The closure over several roots at once — the world-effects runtime (D32) binds the
    /// union of the impact + destruction effect names, so one runtime serves every effect a hit or a
    /// death calls. Same closure rule as the single-root overload; the reused defs share this
    /// program's script pool.</summary>
    public AnimProgram Subset(IEnumerable<string> rootAnimNames)
    {
        var sub = new AnimProgram();
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var queue = new Queue<string>();
        void Enqueue(string name)
        {
            if (!string.IsNullOrEmpty(name) && visited.Add(name))
                queue.Enqueue(name);
        }
        foreach (var root in rootAnimNames)
            Enqueue(root);
        while (queue.Count > 0)
        {
            foreach (var def in ByAnimName(queue.Dequeue()))
            {
                sub.Add(def, compiled: def.Archive != null);
                foreach (var seq in def.Sequences)
                    foreach (var ev in seq.Events)
                        if (ev.Kind == "CallAnimation" && ev.Data.Str("name") is { } called)
                            Enqueue(called);
                if (def.ResetState is { } reset)
                    foreach (var ev in reset.Events)
                        if (ev.Kind == "CallAnimation" && ev.Data.Str("name") is { } rcalled)
                            Enqueue(rcalled);
            }
        }
        sub.ScriptPoolCount = ScriptPoolCount; // the shared pool the reused defs still reach
        return sub;
    }

    /// <summary>
    /// Every sound-definition name any <c>SOUND_NODE</c> event in this program can ask for,
    /// deduplicated — including the ones only reachable at runtime. Feeds
    /// <see cref="WorldSounds.Prewarm"/> so those streams are decoded while the sound archive is
    /// still open; see that method for why the emitter-created cache alone is not sufficient.
    ///
    /// <para>Walks <see cref="AnimSequence.OnCallOnly"/> sequences and <c>ResetState</c> too:
    /// reachability is the question here, not what runs at bootstrap, and a name missed costs a
    /// permanently silent emitter while a name over-decoded costs one WAV. The whole install
    /// uses 10 distinct SOUND_NODE names, so the upper bound is trivial either way.</para>
    /// </summary>
    public IEnumerable<string> SoundNodeNames() => SoundNamesOfKind("SoundNode");

    /// <summary>
    /// Every sound name a one-shot <c>SOUND</c> event in this program can ask for — a sounds.json
    /// definition OR a <c>SOUND_GROUPS</c> name — deduplicated, including the runtime-only
    /// (death/damage sequence) sites. Feeds <see cref="WorldSounds.Prewarm"/>, which expands a
    /// group name to its members before decoding. Same reachability rule as
    /// <see cref="SoundNodeNames"/>: what the program can reach, not what runs at bootstrap.
    /// </summary>
    public IEnumerable<string> OneShotSoundNames() => SoundNamesOfKind("Sound");

    /// <summary>The SI script a def's OBJECT_MOTION_SI_SCRIPT slot refers to. The event
    /// carries an index into the def's own <c>si_script_ids</c>, which in turn indexes its
    /// archive's script pool — so both hops happen here.</summary>
    public SiScript? ScriptFor(AnimDefinition def, int slot)
    {
        if (def.Archive == null || slot < 0 || slot >= def.SiScriptIds.Length)
            return null;
        return def.Archive.Script(def.SiScriptIds[slot]);
    }

    /// <summary>Definition identity: (anchor name, animation name) — exactly how the compiled
    /// extraction names its files, so a reader def and its compiled twin share a key.</summary>
    private static string KeyOf(AnimDefinition def) => $"{def.Name}\0{def.AnimName}";

    private void Add(AnimDefinition def, bool compiled)
    {
        var key = KeyOf(def);
        if (!_seen.Add(key))
            return;
        Defs.Add(def);
        if (compiled) CompiledCount++; else ReaderCount++;
        if (!string.IsNullOrEmpty(def.AnimName))
        {
            if (!_byAnimName.TryGetValue(def.AnimName!, out var list))
                _byAnimName[def.AnimName!] = list = new List<AnimDefinition>();
            list.Add(def);
        }
    }

    private IEnumerable<string> SoundNamesOfKind(string kind)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var def in Defs)
        {
            foreach (var name in NamesIn(def.Sequences, kind))
            {
                if (seen.Add(name))
                {
                    yield return name;
                }
            }
            if (def.ResetState is { } reset)
            {
                foreach (var name in NamesIn(new[] { reset }, kind))
                {
                    if (seen.Add(name))
                    {
                        yield return name;
                    }
                }
            }
            // The compiled destruction slot is dispatched at death (RunDeathSequence) — same
            // reachability rule as the death/damage sequences above it.
            if (def.DeathSlot is { } slot)
            {
                foreach (var name in NamesIn(new[] { slot }, kind))
                {
                    if (seen.Add(name))
                    {
                        yield return name;
                    }
                }
            }
        }

        static IEnumerable<string> NamesIn(IEnumerable<AnimSequence> sequences, string kind)
        {
            foreach (var seq in sequences)
            {
                foreach (var ev in seq.Events)
                {
                    if (ev.Kind == kind && ev.Data.Str("name") is { } name)
                    {
                        yield return name;
                    }
                }
            }
        }
    }

    // startanims.json: [["NEW_GAME_START", [[name], [name], …], "LOAD_GAME_START", …]]
    private void LoadStartAnims(string missionZrdr)
    {
        try
        {
            var root = Zrdr.LoadFile(missionZrdr, "startanims.json");
            if (root.Count > 0 && root[0] is List<object?> outer)
                for (int i = 0; i + 1 < outer.Count; i++)
                    if (outer[i] is string k && k.Equals("NEW_GAME_START", StringComparison.OrdinalIgnoreCase)
                        && outer[i + 1] is List<object?> list)
                        foreach (var entry in list)
                            if (entry is List<object?> { Count: > 0 } e && e[0] is string name)
                                StartAnims.Add(name);
        }
        catch (Exception)
        {
            // startanims.json is optional (not every mission folder has one)
        }
    }
}
