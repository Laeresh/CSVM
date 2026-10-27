using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CSVM.Mech3;

namespace CSVM.Tooling;

/// <summary>
/// The synthetic run's sounds. Its definitions are the <c>sounds</c> reader of
/// <see cref="SyntheticZrdr"/>. Its WAVs are
/// generated MS ADPCM, one per <see cref="Wavs"/> entry, built in memory. They are packed into the
/// container of <c>docs/formats/zbd-archive.md</c> and opened with <see cref="SoundZbd.Read"/>,
/// the install's reader.
/// <c>SyntheticSoundsTests</c> holds the list to the names the definitions give. Shapes:
/// <c>docs/formats/sounds.md</c>.
/// </summary>
public static class SyntheticSounds
{
    /// <summary>The sample rate every WAV is written at, the 22050 Hz the format page says the
    /// shipped archive holds.</summary>
    public const int Rate = 22050;


    // The archive table's entry size and name field, docs/formats/zbd-archive.md.
    private const int EntrySize = 148;
    private const int NameSize = 64;

    // The peak level of a generated signal, as a share of full scale. It reads plainly in a capture
    // and stays far enough under the clip that the ADPCM step never saturates.
    private const double Level = 0.3;

    /// <summary>Every WAV of the archive: its name, its length in seconds and its tone in Hz, 0 for
    /// noise. Exactly the names the <c>SETS</c> block of the synthetic definitions gives.</summary>
    public static IReadOnlyList<(string Name, double Seconds, double Hz)> Wavs { get; } = new (string, double, double)[]
    {
        ("probe_engine.wav", 1.0, 110),
        ("probe_briefing.wav", 3.0, 196),
        ("probe_fighter_engine.wav", 1.0, 146),
        ("probe_brand_engine.wav", 1.0, 128),
        ("probe_avenger_engine.wav", 1.0, 92),
        ("probe_hawk_engine.wav", 1.0, 138),
        ("probe_gyro_engine.wav", 1.0, 172),
        ("probe_fury_engine.wav", 1.0, 120),
        ("probe_peace_engine.wav", 1.0, 104),
        ("probe_warhawk_engine.wav", 1.0, 86),
        ("probe_kestrel_engine.wav", 1.0, 98),
        ("probe_engine_cp.wav", 1.0, 82),
        ("probe_engine_damaged.wav", 1.0, 0),
        ("probe_rattle.wav", 0.5, 0),
        ("probe_propstart.wav", 1.2, 64),
        ("probe_gunloop.wav", 0.5, 330),
        ("probe_emptyclip.wav", 0.1, 1200),
        ("probe_fire.wav", 0.2, 0),
        ("probe_launch.wav", 0.6, 0),
        ("probe_hit.wav", 0.2, 0),
        ("probe_boom.wav", 1.0, 0),
        ("probe_pass1.wav", 0.3, 880),
        ("probe_pass2.wav", 0.3, 990),
        ("probe_ricochet1.wav", 0.3, 1500),
        ("probe_ricochet2.wav", 0.3, 1700),
        ("probe_windowhit1.wav", 0.25, 0),
        ("probe_windowhit2.wav", 0.25, 0),
        ("probe_radio_line.wav", 3.5, 440),
        ("probe_radio_bark.wav", 1.0, 520),
        ("music_splash_probe.wav", 3.0, 262),
        ("music_prebattle_probe1.wav", 3.0, 294),
        ("music_prebattle_probe2.wav", 3.0, 330),
        ("music_battle_probe1.wav", 3.0, 196),
        ("music_battle_probe2.wav", 3.0, 220),
        ("music_primaryobj_probe1.wav", 2.0, 392),
        ("music_primaryobj_probe2.wav", 2.0, 440),
        ("music_secondaryobj_probe1.wav", 2.0, 494),
        ("music_secondaryobj_probe2.wav", 2.0, 523),
        ("music_tertiaryobj_probe1.wav", 2.0, 587),
        ("music_tertiaryobj_probe2.wav", 2.0, 659),
        ("music_battlesuccess_probe.wav", 2.0, 349),
        ("music_missionsuccess_probe.wav", 2.0, 698),
        ("probe_vo2_WA-Turret-A.wav", 0.6, 300),
        ("probe_vo2_WA-HighDmg-A.wav", 0.6, 310),
        ("probe_vo2_WA-Attack-A.wav", 0.6, 320),
        ("probe_vo2_PR-DngrZn-A.wav", 0.6, 330),
        ("probe_vo2_PR-EnemyDwn-A.wav", 0.6, 340),
        ("probe_vo2_DI-LowDmg-A.wav", 0.6, 350),
        ("probe_vo2_DI-MedDmg-A.wav", 0.6, 360),
        ("probe_vo2_DI-HighDmg-A.wav", 0.6, 370),
        ("probe_vo2_DA-Bail-A.wav", 0.6, 380),
        ("probe_vo2_DA-NoBail-A.wav", 0.6, 390),
        ("probe_vo2_DE-Bail-A.wav", 0.6, 400),
        ("probe_vo2_DE-NoBail-A.wav", 0.6, 410),
        ("probe_vo2_GL-AllyDwn-A.wav", 0.6, 420),
        ("probe_vo2_GL-EnemyDwn-A.wav", 0.6, 430),
        ("probe_vo2_GL-PlyrDwn-A.wav", 0.6, 440),
        ("probe_vo2_TA-FailTail-A.wav", 0.6, 450),
        ("probe_vo2_TA-FailShk-A.wav", 0.6, 460),
        ("probe_vo2_TA-SucShk-A.wav", 0.6, 470),
        ("probe_vo2_DS-Ally-A.wav", 0.6, 480),
        ("probe_vo2_DI-LowDmg-B.wav", 0.6, 490),
        ("probe_vo2_WA-Enemy-12L.wav", 0.6, 500),
        ("probe_vo2_WA-Enemy-12.wav", 0.6, 510),
        ("probe_vo2_WA-Enemy-12H.wav", 0.6, 520),
        ("probe_vo2_WA-Enemy-3L.wav", 0.6, 530),
        ("probe_vo2_WA-Enemy-3.wav", 0.6, 540),
        ("probe_vo2_WA-Enemy-3H.wav", 0.6, 550),
        ("probe_vo2_WA-Enemy-6L.wav", 0.6, 560),
        ("probe_vo2_WA-Enemy-6.wav", 0.6, 570),
        ("probe_vo2_WA-Enemy-6H.wav", 0.6, 580),
        ("probe_vo2_WA-Enemy-9L.wav", 0.6, 590),
        ("probe_vo2_WA-Enemy-9.wav", 0.6, 600),
        ("probe_vo2_WA-Enemy-9H.wav", 0.6, 610),
        ("probe_vo26_WA-Turret-A.wav", 0.6, 180),
        ("probe_vo26_WA-HighDmg-A.wav", 0.6, 190),
        ("probe_vo26_WA-Attack-A.wav", 0.6, 200),
        ("probe_vo26_PR-DngrZn-A.wav", 0.6, 210),
        ("probe_vo26_PR-EnemyDwn-A.wav", 0.6, 220),
        ("probe_vo26_DI-LowDmg-A.wav", 0.6, 230),
        ("probe_vo26_DI-MedDmg-A.wav", 0.6, 240),
        ("probe_vo26_DI-HighDmg-A.wav", 0.6, 250),
        ("probe_vo26_DA-Bail-A.wav", 0.6, 260),
        ("probe_vo26_DA-NoBail-A.wav", 0.6, 270),
        ("probe_vo26_DE-Bail-A.wav", 0.6, 280),
        ("probe_vo26_DE-NoBail-A.wav", 0.6, 290),
        ("probe_vo26_GL-AllyDwn-A.wav", 0.6, 300),
        ("probe_vo26_GL-EnemyDwn-A.wav", 0.6, 310),
        ("probe_vo26_GL-PlyrDwn-A.wav", 0.6, 320),
        ("probe_vo26_TA-FailTail-A.wav", 0.6, 330),
        ("probe_vo26_TA-FailShk-A.wav", 0.6, 340),
        ("probe_vo26_TA-SucShk-A.wav", 0.6, 350),
        ("probe_vo26_DS-Ally-A.wav", 0.6, 360),
        ("probe_vo26_DI-LowDmg-B.wav", 0.6, 370),
    };


    /// <summary>The archive of every <see cref="Wavs"/> entry, built afresh in memory.</summary>
    public static SoundZbd Zbd()
    {
        var payloads = Wavs.Select(w => WavWriter.MsAdpcm(Signal(w.Name, w.Seconds, w.Hz), Rate)).ToList();
        int body = payloads.Sum(p => p.Length);
        var bytes = new byte[body + (Wavs.Count * EntrySize) + 8];
        int at = 0;
        int entry = body;
        for (int i = 0; i < Wavs.Count; i++)
        {
            payloads[i].CopyTo(bytes, at);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(entry), (uint)at);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(entry + 4), (uint)payloads[i].Length);
            Encoding.ASCII.GetBytes(Wavs[i].Name).CopyTo(bytes.AsSpan(entry + 8, NameSize));
            at += payloads[i].Length;
            entry += EntrySize;
        }

        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(entry), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(entry + 4), (uint)Wavs.Count);
        return SoundZbd.Read(new MemoryStream(bytes, writable: false), "synthetic soundsh.zbd");
    }

    /// <summary>The samples for one entry: a tone at <paramref name="hz"/> with its octave
    /// at a quarter weight, or seeded white noise when <paramref name="hz"/> is 0. The noise is
    /// seeded by the name, so a rebuild writes the same bytes.</summary>
    public static short[] Signal(string name, double seconds, double hz)
    {
        int count = Math.Max((int)Math.Round(seconds * Rate), 2);
        var samples = new short[count];
        uint state = 2166136261;
        foreach (char c in name)
        {
            state = (state ^ c) * 16777619;
        }

        for (int i = 0; i < count; i++)
        {
            double value;
            if (hz > 0)
            {
                double phase = 2 * Math.PI * hz * i / Rate;
                value = (0.75 * Math.Sin(phase)) + (0.25 * Math.Sin(2 * phase));
            }
            else
            {
                state = (state * 1664525) + 1013904223;
                value = ((state >> 8) / (double)(1 << 24) * 2) - 1;
            }

            samples[i] = (short)Math.Round(value * Level * short.MaxValue);
        }

        return samples;
    }
}
