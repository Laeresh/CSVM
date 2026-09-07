using System;
using System.Collections.Generic;
using System.IO;

namespace CSVM.Video;

/// <summary>
/// One packet of an elementary stream as the container carried it: which stream it belongs to,
/// when it is to be presented, and the bytes themselves as a window into the file rather than a
/// copy. <see cref="MpegSystemStream.NoTimestamp"/> stands where a packet carried none, which
/// most do.
/// </summary>
public readonly struct MpegPacket
{
    public MpegPacket(int streamId, double time, ReadOnlyMemory<byte> data)
    {
        StreamId = streamId;
        Time = time;
        Data = data;
    }

    /// <summary>The container's stream id: 0xE0 for the video stream, 0xC0 for the audio one.</summary>
    public int StreamId { get; }

    /// <summary>Presentation timestamp in seconds, or <see cref="MpegSystemStream.NoTimestamp"/>.</summary>
    public double Time { get; }

    /// <summary>The packet payload, a window into the file's own bytes.</summary>
    public ReadOnlyMemory<byte> Data { get; }
}

/// <summary>
/// The MPEG-1 system stream demultiplexer (ISO 11172-1): walks a whole file's packs and packets
/// and separates the video elementary stream from the audio one. The video stream comes back
/// joined into one buffer for <see cref="MpegVideoDecoder"/>; the audio stream stays as packets,
/// because a layer II decoder needs each one's presentation timestamp to keep sound against
/// picture. Both timestamps are on the container's 90 kHz clock, converted to seconds.
/// </summary>
public sealed class MpegSystemStream
{
    /// <summary>The video elementary stream's id in every file this project reads.</summary>
    public const int VideoStreamId = 0xE0;

    /// <summary>The audio elementary stream's id in every file this project reads.</summary>
    public const int AudioStreamId = 0xC0;

    /// <summary>The time a packet that carries no timestamp reports.</summary>
    public const double NoTimestamp = -1.0;

    private const int PackStart = 0xBA;
    private const int SystemHeader = 0xBB;
    private const int EndCode = 0xB9;
    private const int PaddingStream = 0xBE;
    private const int PrivateStream = 0xBD;
    private const int AudioStreamFirst = 0xC0;
    private const int AudioStreamLast = 0xDF;
    private const int VideoStreamFirst = 0xE0;
    private const int VideoStreamLast = 0xEF;

    private MpegSystemStream(byte[] videoStream, double videoStartTime, List<MpegPacket> audioPackets)
    {
        VideoStream = videoStream;
        VideoStartTime = videoStartTime;
        AudioPackets = audioPackets;
    }

    /// <summary>The video elementary stream, every video packet's payload end to end.</summary>
    public byte[] VideoStream { get; }

    /// <summary>The first video packet's presentation timestamp, or zero when none carried one.
    /// It is the offset the decoder puts its frame times on.</summary>
    public double VideoStartTime { get; }

    /// <summary>The audio packets in the order the container carried them, each with its own
    /// timestamp. Decoding them is the layer II decoder's job, not this one's.</summary>
    public IReadOnlyList<MpegPacket> AudioPackets { get; }

    /// <summary>Walks a whole system stream held in memory. Throws when the file holds no video
    /// packets at all, which is what a file that is not an MPEG-1 system stream looks like.</summary>
    public static MpegSystemStream Demux(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        var video = new List<MpegPacket>();
        var audio = new List<MpegPacket>();
        Walk(bytes, video, audio);

        if (video.Count == 0)
        {
            throw new InvalidDataException("no MPEG-1 video packets in the system stream");
        }

        int videoLength = 0;
        foreach (var packet in video)
        {
            videoLength += packet.Data.Length;
        }

        var videoStream = new byte[videoLength];
        int at = 0;
        foreach (var packet in video)
        {
            packet.Data.Span.CopyTo(videoStream.AsSpan(at));
            at += packet.Data.Length;
        }

        double startTime = video[0].Time;
        return new MpegSystemStream(videoStream, startTime == NoTimestamp ? 0.0 : startTime, audio);
    }

    private static void Walk(byte[] bytes, List<MpegPacket> video, List<MpegPacket> audio)
    {
        int position = 0;
        while (true)
        {
            int code = NextStartCode(bytes, ref position);
            if (code < 0 || code == EndCode)
            {
                return;
            }

            if (code == PackStart)
            {
                position = SkipPackHeader(bytes, position);
                continue;
            }

            if (code == SystemHeader || code == PaddingStream || code == PrivateStream)
            {
                position = SkipSizedBlock(bytes, position);
                continue;
            }

            if (IsElementary(code))
            {
                position = ReadPacket(bytes, position, code, video, audio);
            }
        }
    }

    private static bool IsElementary(int code) =>
        (code >= AudioStreamFirst && code <= AudioStreamLast)
        || (code >= VideoStreamFirst && code <= VideoStreamLast);

    // Leaves the cursor just past the four bytes of the code and returns the code's own byte,
    // or -1 at the end of the file.
    private static int NextStartCode(byte[] bytes, ref int position)
    {
        for (int at = position; at + 3 < bytes.Length; at++)
        {
            if (bytes[at] == 0x00 && bytes[at + 1] == 0x00 && bytes[at + 2] == 0x01)
            {
                position = at + 4;
                return bytes[at + 3];
            }
        }

        position = bytes.Length;
        return -1;
    }

    // An MPEG-1 pack header is a fixed eight bytes of clock reference and mux rate, marked by
    // the leading 0010. MPEG-2's spells its own length instead, and nothing here reads one.
    private static int SkipPackHeader(byte[] bytes, int position)
    {
        if (position < bytes.Length && (bytes[position] & 0xF0) == 0x20)
        {
            return position + 8;
        }

        return position;
    }

    private static int SkipSizedBlock(byte[] bytes, int position)
    {
        if (position + 1 >= bytes.Length)
        {
            return bytes.Length;
        }

        return position + 2 + (bytes[position] << 8) + bytes[position + 1];
    }

    private static int ReadPacket(
        byte[] bytes, int position, int streamId, List<MpegPacket> video, List<MpegPacket> audio)
    {
        if (position + 1 >= bytes.Length)
        {
            return bytes.Length;
        }

        int length = (bytes[position] << 8) + bytes[position + 1];
        int at = position + 2;
        int end = Math.Min(at + length, bytes.Length);
        double time = ReadPacketHeader(bytes, ref at, end);
        if (at <= end)
        {
            var packet = new MpegPacket(streamId, time, bytes.AsMemory(at, end - at));
            if (streamId >= VideoStreamFirst)
            {
                video.Add(packet);
            }
            else
            {
                audio.Add(packet);
            }
        }

        return end;
    }

    // The packet header ahead of the payload: stuffing bytes, an optional buffer bound, then
    // either a presentation timestamp, a pair of them, or the single byte that says neither.
    private static double ReadPacketHeader(byte[] bytes, ref int at, int end)
    {
        while (at < end && bytes[at] == 0xFF)
        {
            at++;
        }

        if (at < end && (bytes[at] & 0xC0) == 0x40)
        {
            at += 2;
        }

        if (at >= end)
        {
            return NoTimestamp;
        }

        int marker = bytes[at] & 0xF0;
        if (marker == 0x20 && at + 5 <= end)
        {
            double time = ReadTimestamp(bytes, at);
            at += 5;
            return time;
        }

        if (marker == 0x30 && at + 10 <= end)
        {
            double time = ReadTimestamp(bytes, at);
            at += 10;
            return time;
        }

        if (bytes[at] == 0x0F)
        {
            at++;
        }

        return NoTimestamp;
    }

    // A 33-bit value on the 90 kHz system clock, split across five bytes by marker bits.
    private static double ReadTimestamp(byte[] bytes, int at)
    {
        long clock = (long)(bytes[at] & 0x0E) << 29;
        clock |= (long)bytes[at + 1] << 22;
        clock |= (long)(bytes[at + 2] & 0xFE) << 14;
        clock |= (long)bytes[at + 3] << 7;
        clock |= (long)(bytes[at + 4] & 0xFE) >> 1;
        return clock / 90000.0;
    }
}
