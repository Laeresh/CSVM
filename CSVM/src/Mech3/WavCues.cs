using System;
using System.Buffers.Binary;
using System.Collections.Generic;

namespace CSVM.Mech3;

/// <summary>
/// The RIFF <c>cue </c> chunk of a WAV: the named points inside the sample stream that a script
/// waits on. The briefing narration is the one consumer (docs/formats/briefing.md): a state's
/// <c>WaitForMarker n</c> blocks until playback reaches cue point <c>n</c>, and the durations
/// around it are authored constants, so these times are the only clock the reveal does not carry
/// itself. Times come back in ascending order, which is what <c>n</c> indexes: 13 of the 24
/// briefing wavs store their points out of time order, and reading them in file order would run
/// a reveal's beats backwards.
/// </summary>
public static class WavCues
{
    /// <summary>Cue-point times in seconds, ascending. Empty when the file carries no cue chunk,
    /// is not a WAV, or is truncated: a caller with no markers degrades, it does not fail.</summary>
    public static IReadOnlyList<double> Read(byte[] wav)
    {
        var times = new List<double>();
        if (wav.Length < 12 || !Tag(wav, 0, "RIFF") || !Tag(wav, 8, "WAVE"))
        {
            return times;
        }

        int rate = 0;
        var offsets = new List<uint>();
        int at = 12;
        while (at + 8 <= wav.Length)
        {
            string id = System.Text.Encoding.ASCII.GetString(wav, at, 4);
            uint size = BinaryPrimitives.ReadUInt32LittleEndian(wav.AsSpan(at + 4));
            int body = at + 8;
            if (size > wav.Length - body)
            {
                break;
            }

            if (id == "fmt " && size >= 8)
            {
                rate = BinaryPrimitives.ReadInt32LittleEndian(wav.AsSpan(body + 4));
            }
            else if (id == "cue ")
            {
                ReadPoints(wav.AsSpan(body, (int)size), offsets);
            }

            at = body + (int)size + ((int)size & 1);
        }

        if (rate <= 0)
        {
            return times;
        }

        foreach (uint offset in offsets)
        {
            times.Add(offset / (double)rate);
        }

        times.Sort();
        return times;
    }

    // A cue point is 24 bytes: id, play-order position, the chunk it indexes, that chunk's start,
    // the block start, and the sample offset. Position and sample offset are equal in every
    // shipped briefing wav, and the sample offset is the field that means "this many samples in".
    private static void ReadPoints(ReadOnlySpan<byte> cue, List<uint> offsets)
    {
        if (cue.Length < 4)
        {
            return;
        }

        uint count = BinaryPrimitives.ReadUInt32LittleEndian(cue);
        for (int i = 0; i < count; i++)
        {
            int point = 4 + (i * 24);
            if (point + 24 > cue.Length)
            {
                return;
            }

            offsets.Add(BinaryPrimitives.ReadUInt32LittleEndian(cue[(point + 20)..]));
        }
    }

    private static bool Tag(byte[] bytes, int at, string tag) =>
        System.Text.Encoding.ASCII.GetString(bytes, at, 4) == tag;
}
