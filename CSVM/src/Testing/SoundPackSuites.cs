using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using CSVM.Extraction;
using CSVM.Flight.Audio;
using CSVM.Mech3;
using CSVM.Tooling;
using CSVM.UI.Labs;
using CSVM.Utils;
using Godot;

namespace CSVM.Testing;

/// <summary>A sound pack generated into the scratch folder over the run's sound archive. It holds a
/// short WAV named after the flown engine loop's WAV, and an override file. Enhanced Sound must play
/// both, Original Sound neither, and the sound lab must tune the override and leave the shipped file
/// untouched.</summary>
internal static class SoundPackSuites
{
    private const float Dt = 1f / 60f;
    private const int PackRate = 11025;

    // Apart from every shipped value, so a read of the shipped file cannot pass for the override.
    private const float OverridePitchFull = 2.2f;
    private const float OverrideConfirmPitch = 1.9f;

    [Suite("sound-pack",
        "sound packs: with no pack, Enhanced Sound plays the archive's own engine WAV; with a generated "
        + "pack, Enhanced Sound's archive and flown engine slot play the pack's WAV of the same name while "
        + "Original Sound plays the archive's; the pack's override lies over the shipped tuning, so the "
        + "Enhanced engine slot takes its full-throttle pitch and its hit confirm pitch while Original Sound "
        + "keeps the decoded pitch and the shipped confirm pitch; the sound lab over the pack opens with no "
        + "unsaved changes and saves into the override alone, the shipped file unchanged, and an override "
        + "inside a zip turns Save off")]
    internal static void SoundPack(TestContext ctx)
    {
        ctx.RequireData(ctx.Zrdr, $"zrdr archive");
        ctx.RequireData(ctx.Data.Sounds, $"sound archive (soundsh)");

        var defs = SoundDefs.Load(ctx.Zrdr);
        var groups = SoundDefs.LoadGroups(ctx.Zrdr);
        var stats = Flight.Airframe.PlaneStats.Load(ctx.Zrdr, ctx.PlaneName);
        if (!defs.TryGetValue(stats.EngineSound, out var engineDef))
        {
            ctx.Check(false, $"{ctx.PlaneName}'s engine_sound {stats.EngineSound} has a definition");
            return;
        }

        // ⚠ Never point a suite at <dataRoot>/HDSounds: that is the player's own folder.
        string root = Path.Combine(ctx.ScratchDir, "sound-pack");
        if (Directory.Exists(root))
            Directory.Delete(root, true);
        string folder = Path.Combine(root, "HDSounds", "test");
        Directory.CreateDirectory(folder);
        short[] samples = Enumerable.Range(0, PackRate / 10).Select(i => (short)(8000 * Math.Sin(i * 0.2))).ToArray();
        File.WriteAllBytes(Path.Combine(folder, Path.GetFileNameWithoutExtension(engineDef.WavName).ToUpperInvariant() + ".wav"),
            WavWriter.Pcm16(samples, PackRate));
        string overridePath = Path.Combine(folder, HdSounds.OverrideName);
        File.WriteAllText(overridePath, FormattableString.Invariant(
            $"{{ \"_about\": \"a generated pack\", \"enginePitchFull\": {OverridePitchFull}, \"hitConfirmPitch\": {OverrideConfirmPitch} }}"));
        byte[] packData = new byte[samples.Length * 2];
        Buffer.BlockCopy(samples, 0, packData, 0, packData.Length);

        var packWas = HdSounds.Current;
        bool wasEnhanced = SoundModeSetting.EnhancedOn, wasConfirm = SoundModeSetting.HitConfirmOn;
        byte[] shipped = Godot.FileAccess.GetFileAsBytes(EnhancedSoundTuning.DefaultPath);
        using var archive = new SoundArchive(ctx.Sounds);
        using var pack = HdSounds.Open(root, "HDSounds");
        try
        {
            bool IsPack(AudioStream? s) => s is AudioStreamWav w && w.MixRate == PackRate && w.Data.SequenceEqual(packData);

            SoundModeSetting.EnhancedOn = true;
            ctx.Check(archive.Find(engineDef.WavName, engineDef.Looped) is { } plain && !IsPack(plain),
                $"with no pack Enhanced Sound plays the archive's own {engineDef.WavName}");

            HdSounds.Current = pack;
            var file = EnhancedSoundTuning.Load();
            var tuning = EnhancedSoundTuning.Reload();
            ctx.Check(pack.Count == 1 && pack.OverridePath == overridePath,
                $"the generated pack offers one sound and its override ({pack.Count}, {pack.OverridePath})");
            foreach (bool enhanced in new[] { true, false, true })
            {
                SoundModeSetting.EnhancedOn = enhanced;
                bool replaced = IsPack(archive.Find(engineDef.WavName, engineDef.Looped));
                ctx.Check(replaced == enhanced,
                    $"{(enhanced ? "Enhanced" : "Original")} Sound's archive plays {(replaced ? "the pack's" : "the archive's")} {engineDef.WavName}");
            }

            ctx.Check(tuning.EnginePitchFull == OverridePitchFull && tuning.HitConfirmPitch == OverrideConfirmPitch
                      && tuning == file with { EnginePitchFull = OverridePitchFull, HitConfirmPitch = OverrideConfirmPitch }
                      && EnhancedSoundTuning.WithoutPack == file,
                $"the override lies over the shipped tuning: {tuning.Describe()}");

            Flown(ctx, archive, defs, groups, stats, file, tuning, IsPack);
            Lab(ctx, pack, file, tuning, overridePath, groups.Keys.ToList());
            Zipped(ctx, root, file);
            ctx.Check(Godot.FileAccess.GetFileAsBytes(EnhancedSoundTuning.DefaultPath).SequenceEqual(shipped),
                $"the shipped tuning file is unchanged");
        }
        finally
        {
            HdSounds.Current = packWas;
            SoundModeSetting.EnhancedOn = wasEnhanced;
            SoundModeSetting.HitConfirmOn = wasConfirm;
            EnhancedSoundTuning.Reload();
        }
    }

    // The flown engine slot and the hit confirm, each built under its own mode. A flight reads its
    // streams once, at setup.
    private static void Flown(TestContext ctx, SoundArchive archive, Dictionary<string, SoundDef> defs,
        Dictionary<string, SoundGroup> groups, Flight.Airframe.PlaneStats stats, EnhancedSoundTuning file,
        EnhancedSoundTuning tuning, Func<AudioStream?, bool> isPack)
    {
        bool pitched = EngineAudioCurves.SlotIsPitched(defs, stats.EngineSound);
        var full = new EngineDrive(1f, 0f, 0f);
        foreach (bool enhanced in new[] { true, false })
        {
            SoundModeSetting.EnhancedOn = enhanced;
            SoundModeSetting.HitConfirmOn = true;
            string mode = enhanced ? "Enhanced" : "Original";
            var audio = new FlightAudio();
            audio.Setup(archive, defs, stats, null, groups);
            ctx.Host.AddChild(audio);
            try
            {
                audio.Update(Dt, full, 0.5f, 1f);
                float want = EngineAudioCurves.Engine(stats, full, 1f, pitched, enhanced ? tuning : null).Pitch;
                ctx.Check(isPack(audio.EngineStream) == enhanced && audio.EngineSounding,
                    $"{mode} Sound's flown engine slot sounds {(isPack(audio.EngineStream) ? "the pack's" : "the archive's")} loop");
                ctx.Check(Mathf.IsEqualApprox(audio.EnginePitch, want),
                    $"{mode} Sound's engine slot at full throttle writes pitch {audio.EnginePitch:0.000} (want {want:0.000}, slot pitched {pitched})");
                audio.OnHitConfirm(1000.0);
                float confirm = enhanced ? OverrideConfirmPitch : file.HitConfirmPitch;
                ctx.Check(audio.HitConfirmPitchScale == confirm,
                    $"{mode} Sound's hit confirm stands at pitch {audio.HitConfirmPitchScale:0.000} (want {confirm:0.000})");
            }
            finally
            {
                audio.QueueFree();
            }
        }

        if (!pitched)
            ctx.Note($"{stats.EngineSound} carries no FREQUENCY, so the override's engine pitch is not heard on it");
    }

    private static void Lab(TestContext ctx, HdSounds pack, EnhancedSoundTuning file, EnhancedSoundTuning tuning,
        string overridePath, IReadOnlyList<string> groups)
    {
        var target = EnhancedSoundTuning.SaveTargetFor(pack, file, EnhancedSoundTuning.SourceTreePath());
        var lab = new SoundLab(target, EnhancedSoundTuning.Current, () => null, groups);
        // In the tree, as a flight's lab is: a slider outside one moves without telling the lab.
        ctx.Host.AddChild(lab);
        try
        {
            ctx.Check(target.Path == overridePath && target.OffReason == null && target.Under == file && lab.Saved == tuning && lab.Edited == lab.Saved,
                $"the lab over the pack opens on the override with no unsaved changes (save {target.Path})");
            lab.SliderFor("engineGainFull").Value = 1.37;
            string? written = lab.Save();
            string text = File.ReadAllText(overridePath);
            var readBack = EnhancedSoundTuning.WithPack(file, pack);
            ctx.Check(written == overridePath && readBack == lab.Saved && readBack.EngineGainFull == 1.37f,
                $"Save writes the override, which reads back to the lab's values: {readBack.Describe()}");
            ctx.Check(text.Contains("\"_about\"", StringComparison.Ordinal) && text.Contains("\"engineGainFull\"", StringComparison.Ordinal)
                      && !text.Contains("\"bulletHitGain\"", StringComparison.Ordinal),
                $"the override holds its own fields and the moved one, not the shipped file's: {text.ReplaceLineEndings(" ")}");
        }
        finally
        {
            lab.Free();
        }
    }

    private static void Zipped(TestContext ctx, string root, EnhancedSoundTuning file)
    {
        string zipFolder = Path.Combine(root, "Zipped");
        Directory.CreateDirectory(zipFolder);
        using (var zip = ZipFile.Open(Path.Combine(zipFolder, "pack.zip"), ZipArchiveMode.Create))
        {
            using var writer = new StreamWriter(zip.CreateEntry("pack/" + HdSounds.OverrideName).Open());
            writer.Write("{ \"bulletHitGain\": 3 }");
        }

        using var zipped = HdSounds.Open(root, "Zipped");
        string neverWritten = Path.Combine(root, "never-written.json");
        var target = EnhancedSoundTuning.SaveTargetFor(zipped, file, neverWritten);
        var lab = new SoundLab(target, target.Saved, () => null, Array.Empty<string>());
        try
        {
            ctx.Check(target.Path == null && target.OffReason != null && target.Saved.BulletHitGain == 3f && lab.Save() == null && !File.Exists(neverWritten),
                $"an override inside a zip is read but turns Save off: {target.OffReason}");
        }
        finally
        {
            lab.Free();
        }
    }
}
