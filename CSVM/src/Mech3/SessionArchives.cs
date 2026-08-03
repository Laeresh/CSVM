using System.Collections.Generic;
using System.IO;
using CSVM.Utils;
using Godot;

namespace CSVM.Mech3;

/// <summary>Why the archives are being opened — fixes the <see cref="WorldSession.Options"/>
/// lifetime flags that come back with them, so a caller cannot forget one (`BL-241`'s own fix
/// note: the harness forgot <c>TexturesOutliveBuild</c>).</summary>
public enum ArchiveIntent
{
    /// <summary>A game/viewer/flight session: the texture archive belongs to the session (freed on
    /// return-to-menu), so the world runtime keeps baking `PUFFER_STATE` atlases at RUNTIME
    /// (`BL-234`) — <c>TexturesOutliveBuild = true</c>. The sound archive stays a `using` local of
    /// the build (the prewarm is what makes that survivable) — <c>SoundsOutliveBuild = false</c>.</summary>
    Session,

    /// <summary>The animation lab: both archives outlive the build — the lab node owns their
    /// disposal so effects can build at any playhead time.</summary>
    Lab,

    /// <summary>A test-harness suite: both archives are `using` locals of the build, scoped
    /// entirely to it — neither flag is set.</summary>
    Suite,
}

/// <summary>The five archives one chapter world needs, opened together because every caller
/// (`GameSession`, the anim lab, the test harness) opens the same five — plus the
/// <see cref="WorldSession.Options"/> lifetime flags <see cref="ArchiveIntent"/> implies, so a
/// caller sets them by naming its intent, not by hand.</summary>
public sealed class SessionArchives
{
    public required GameZ Gamez { get; init; }
    public required TextureArchive Textures { get; init; }
    public SoundArchive? Sounds { get; init; }
    public Dictionary<string, SoundDef>? SoundDefs { get; init; }
    public Dictionary<string, SoundGroup>? SoundGroups { get; init; }

    /// <summary>See <see cref="WorldSession.Options.TexturesOutliveBuild"/>.</summary>
    public required bool TexturesOutliveBuild { get; init; }

    /// <summary>See <see cref="WorldSession.Options.SoundsOutliveBuild"/>.</summary>
    public required bool SoundsOutliveBuild { get; init; }

    /// <summary>Opens gamez, textures, sounds, sound defs and sound groups for one chapter build,
    /// timing each into <see cref="StartupProfile"/> exactly as <c>WorldSession.Build</c>'s own
    /// phases do — <c>Record</c> is a no-op with no session under measurement (the test harness),
    /// so the calls are unconditional here too.
    /// <para>⚠ The sound archive is scoped to the build and the texture archive is not — that
    /// asymmetry is deliberate (see <see cref="ArchiveIntent"/>'s members) and is reproduced per
    /// intent, never normalised.</para></summary>
    public static SessionArchives OpenFor(ArchiveIntent intent, string gamezPath, string texturesPath,
        string soundsPath, string zrdrPath, bool mute)
    {
        long mark = StartupProfile.Mark();
        var gamez = GameZ.Load(gamezPath);
        StartupProfile.Record("gamez", mark);

        mark = StartupProfile.Mark();
        var textures = new TextureArchive(texturesPath);
        StartupProfile.Record("textures", mark);

        bool haveSounds = !mute && (File.Exists(soundsPath) || Directory.Exists(soundsPath));
        mark = StartupProfile.Mark();
        var sounds = haveSounds ? new SoundArchive(soundsPath) : null;
        StartupProfile.Record("sounds", mark);

        mark = StartupProfile.Mark();
        var loadedSoundDefs = haveSounds ? Mech3.SoundDefs.Load(zrdrPath) : null;
        var loadedSoundGroups = haveSounds ? Mech3.SoundDefs.LoadGroups(zrdrPath) : null;
        StartupProfile.Record("zrdr", mark);

        if (!mute && !haveSounds)
            GD.PushWarning($"sound archive not found, flying silent: {soundsPath}");

        return new SessionArchives
        {
            Gamez = gamez,
            Textures = textures,
            Sounds = sounds,
            SoundDefs = loadedSoundDefs,
            SoundGroups = loadedSoundGroups,
            TexturesOutliveBuild = intent != ArchiveIntent.Suite,
            SoundsOutliveBuild = intent == ArchiveIntent.Lab,
        };
    }
}
