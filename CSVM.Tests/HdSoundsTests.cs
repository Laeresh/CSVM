using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using CSVM.Extraction;
using CSVM.Flight.Audio;
using CSVM.Mech3;
using CSVM.Tooling;
using CSVM.Utils;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The sound pack folder's rules. A file replaces the original WAV of its name, matched without
/// case, extension or folder. Its own bytes say whether it is WAV or Ogg. A pack's top-level
/// override lies over the shipped Enhanced Sound values, and a deterministic run reads no pack
/// unless the flag names one.
/// </summary>
public class HdSoundsTests
{
    private static readonly byte[] Ogg = Encoding.ASCII.GetBytes("OggS\0\u0002 not a real stream");

    [Fact]
    public void AFileReplacesTheOriginalOfItsNameWhateverItsCaseExtensionOrFolder()
    {
        var root = TestData.TempDir();
        string loose = Path.Combine(root, "HDSounds", "engines", "props");
        Directory.CreateDirectory(loose);
        File.WriteAllBytes(Path.Combine(loose, "Engine_Bhawk.WAV"), Wav());
        Zip(Path.Combine(root, "HDSounds", "booms.zip"), ("booms/hits/explosion_1.ogg", Ogg), ("booms/readme.txt", Ogg));

        using var pack = HdSounds.Open(root, "HDSounds");

        Assert.Equal(2, pack.Count);
        Assert.True(pack.Replaces("engine_bhawk.wav"));
        Assert.Equal(HdSoundFormat.Wav, pack.For("ENGINE_BHAWK.wav")!.Value.Format);
        Assert.Equal(HdSoundFormat.Ogg, pack.For("explosion_1.wav")!.Value.Format);
        Assert.Equal(HdSoundFormat.Ogg, pack.For(@"sub\Explosion_1.wav")!.Value.Format);
        Assert.False(pack.Replaces("explosion_2.wav"));
        Assert.Null(pack.For("readme.wav"));
        Assert.Equal("explosion_1", HdSounds.KeyFor("Explosion_1.WAV"));
    }

    [Fact]
    public void ALaterPackWinsAName()
    {
        var root = TestData.TempDir();
        Directory.CreateDirectory(Path.Combine(root, "HDSounds", "b_fix"));
        File.WriteAllBytes(Path.Combine(root, "HDSounds", "b_fix", "explosion_1.ogg"), Ogg);
        Zip(Path.Combine(root, "HDSounds", "a_pack.zip"), ("a/explosion_1.wav", Wav()));

        using var pack = HdSounds.Open(root, "HDSounds");

        Assert.Equal(HdSoundFormat.Ogg, pack.For("explosion_1.wav")!.Value.Format);
        Assert.EndsWith("explosion_1.ogg", pack.For("explosion_1.wav")!.Value.Source);
    }

    [Fact]
    public void TheContainerIsReadFromTheBytesAndAFileOfNeitherPlaysTheOriginal()
    {
        Assert.Equal(HdSoundFormat.Wav, HdSounds.Sniff(Wav()));
        Assert.Equal(HdSoundFormat.Ogg, HdSounds.Sniff(Ogg));
        Assert.Null(HdSounds.Sniff(Encoding.ASCII.GetBytes("ID3 an mp3")));
        Assert.Null(HdSounds.Sniff(Encoding.ASCII.GetBytes("RIFF")));

        var root = TestData.TempDir();
        string folder = Path.Combine(root, "HDSounds", "pack");
        Directory.CreateDirectory(folder);
        // Mislabelled the other way round: the bytes decide.
        File.WriteAllBytes(Path.Combine(folder, "a.ogg"), Wav());
        File.WriteAllBytes(Path.Combine(folder, "b.wav"), Encoding.ASCII.GetBytes("ID3 an mp3 renamed"));
        using var pack = HdSounds.Open(root, "HDSounds");

        Assert.Equal(HdSoundFormat.Wav, pack.For("a.wav")!.Value.Format);
        Assert.True(pack.Replaces("b.wav"));
        Assert.Null(pack.For("b.wav"));
    }

    [Fact]
    public void NoFolderOrAnEmptyNameIsNoPack()
    {
        var root = TestData.TempDir();
        using var absent = HdSounds.Open(root, "HDSounds");
        using var none = HdSounds.Open(root, "");

        Assert.Equal(0, absent.Count);
        Assert.False(absent.HasOverride);
        Assert.Null(absent.For("explosion_1.wav"));
        Assert.Equal(0, none.Count);
    }

    [Theory]
    [InlineData(null, false, "HDSounds", "HDSounds")]
    [InlineData(null, false, "MySounds", "MySounds")]
    [InlineData(null, true, "MySounds", null)]
    [InlineData("Probe", true, "MySounds", "Probe")]
    [InlineData("", false, "MySounds", "")]
    public void ADeterministicRunReadsNoPackUnlessTheFlagNamesOne(string? flag, bool det, string configured, string? expected)
    {
        Assert.Equal(expected, HdSounds.FolderFor(flag, det, configured));
    }

    [Fact]
    public void ThePacksOverrideLiesOverTheShippedFileAndOnlyItsTopLevelFileCounts()
    {
        var root = TestData.TempDir();
        string loose = Path.Combine(root, "HDSounds", "b_pack");
        Directory.CreateDirectory(Path.Combine(loose, "deeper"));
        File.WriteAllText(Path.Combine(loose, "enhanced_sound.json"), "{ \"enginePitchFull\": 2.0, \"hitConfirmSound\": \"window_hit_sg\" }");
        File.WriteAllText(Path.Combine(loose, "deeper", "enhanced_sound.json"), "{ \"enginePitchFull\": 0.5 }");
        File.WriteAllText(Path.Combine(loose, "other.json"), "{ \"enginePitchFull\": 0.5 }");
        Zip(Path.Combine(root, "HDSounds", "a_pack.zip"), ("a/enhanced_sound.json", Encoding.UTF8.GetBytes("{ \"enginePitchIdle\": 0.3 }")));
        var file = EnhancedSoundTuning.Neutral with { EnginePitchIdle = 0.5f, BulletHitGain = 1.5f };

        using var pack = HdSounds.Open(root, "HDSounds");
        var merged = EnhancedSoundTuning.WithPack(file, pack);

        Assert.True(pack.HasOverride);
        Assert.Equal(0, pack.Count);
        Assert.Equal(Path.Combine(loose, "enhanced_sound.json"), pack.OverridePath);
        // The later pack's override wins whole; the zip's earlier one is not layered under it.
        Assert.Equal(file with { EnginePitchFull = 2f, HitConfirmSound = "window_hit_sg" }, merged);
    }

    [Fact]
    public void AnOverrideInsideAZipIsReadButHasNoPathToWrite()
    {
        var root = TestData.TempDir();
        Directory.CreateDirectory(Path.Combine(root, "HDSounds"));
        Zip(Path.Combine(root, "HDSounds", "pack.zip"), ("pack/Enhanced_Sound.JSON", Encoding.UTF8.GetBytes("{ \"bulletHitGain\": 3 }")));

        using var pack = HdSounds.Open(root, "HDSounds");

        Assert.True(pack.HasOverride);
        Assert.Null(pack.OverridePath);
        Assert.Equal(3f, EnhancedSoundTuning.WithPack(EnhancedSoundTuning.Neutral, pack).BulletHitGain);
        using var none = HdSounds.Open(root, "absent");
        var file = EnhancedSoundTuning.Neutral with { BulletHitGain = 2f };
        Assert.Same(file, EnhancedSoundTuning.WithPack(file, none));
    }

    [Fact]
    public void AnOverrideThatIsNotJsonLeavesTheShippedValues()
    {
        var root = TestData.TempDir();
        string loose = Path.Combine(root, "HDSounds", "pack");
        Directory.CreateDirectory(loose);
        File.WriteAllText(Path.Combine(loose, "enhanced_sound.json"), "not json");
        var file = EnhancedSoundTuning.Neutral with { BulletHitGain = 2f };

        using var pack = HdSounds.Open(root, "HDSounds");

        Assert.Equal(file, EnhancedSoundTuning.WithPack(file, pack));
    }

    [Fact]
    public void SavingIntoAnOverrideWritesOnlyWhatDiffersFromTheShippedFileOrWhatItAlreadyHeld()
    {
        var shipped = EnhancedSoundTuning.Neutral with { BulletHitGain = 1.5f, CanopyHitGain = 2f };
        string existing = "{ \"_about\": \"my pack\", \"canopyHitGain\": 2 }";
        var edited = shipped with { EnginePitchFull = 1.8f };

        string written = EnhancedSoundTuning.WithValues(existing, edited, shipped);
        var problems = new List<string>();
        var readBack = EnhancedSoundTuning.Parse(Encoding.UTF8.GetBytes(written), problems, shipped);

        Assert.Contains("\"_about\"", written);
        Assert.Contains("\"canopyHitGain\"", written);
        Assert.Contains("\"enginePitchFull\"", written);
        Assert.DoesNotContain("\"bulletHitGain\"", written);
        Assert.DoesNotContain("\"hitConfirmSound\"", written);
        Assert.Empty(problems);
        Assert.Equal(edited.Written(), readBack);
    }

    [Fact]
    public void APackWavOfMoreThanTwoChannelsIsRefusedSoTheOriginalPlays()
    {
        Assert.Equal(1, WavFile.ParseMonoOrStereo(Wav()).Channels);
        Assert.Equal(2, WavFile.ParseMonoOrStereo(WithChannels(Wav(), 2)).Channels);
        Assert.Throws<InvalidDataException>(() => WavFile.ParseMonoOrStereo(WithChannels(Wav(), 4)));
    }

    [Fact]
    public void APackWavWithNoFramesIsRefusedSoTheOriginalPlays()
    {
        Assert.Throws<InvalidDataException>(() => WavFile.ParseMonoOrStereo(WavWriter.Pcm16(Array.Empty<short>(), 22050)));
    }

    [Fact]
    public void APackWavTheDecoderCannotReadPlaysTheOriginalRatherThanThrowing()
    {
        // Block align 0 (the file's bytes 32-33) divides by zero in an unguarded MS ADPCM decoder.
        var adpcm = WavWriter.MsAdpcm(new short[] { 0, 1000, -1000, 0 }, 22050);
        adpcm[32] = adpcm[33] = 0;
        var root = TestData.TempDir();
        string folder = Path.Combine(root, "HDSounds", "pack");
        Directory.CreateDirectory(folder);
        File.WriteAllBytes(Path.Combine(folder, "boom.wav"), adpcm);
        var packWas = HdSounds.Current;
        bool wasEnhanced = SoundModeSetting.EnhancedOn;
        using var pack = HdSounds.Open(root, "HDSounds");
        using var archive = new SoundArchive(_ => null);
        try
        {
            HdSounds.Current = pack;
            SoundModeSetting.EnhancedOn = true;
            // The archive lacks boom.wav as well, so the original's answer is null and no Godot stream is built.
            Assert.Null(archive.Find("boom.wav", looped: false, warn: false));
        }
        finally
        {
            HdSounds.Current = packWas;
            SoundModeSetting.EnhancedOn = wasEnhanced;
        }
    }

    // The canonical fmt chunk's channel count sits at the file's byte 22.
    private static byte[] WithChannels(byte[] wav, byte channels)
    {
        wav[22] = channels;
        return wav;
    }

    private static byte[] Wav() => WavWriter.Pcm16(new short[] { 0, 1000, -1000, 0 }, 22050);

    private static void Zip(string path, params (string Name, byte[] Body)[] entries)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var zip = ZipFile.Open(path, ZipArchiveMode.Create);
        foreach (var (name, body) in entries)
        {
            using var stream = zip.CreateEntry(name).Open();
            stream.Write(body);
        }
    }
}
