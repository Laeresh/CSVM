using System.Collections.Generic;
using System.IO;

namespace CSVM.Video;

/// <summary>
/// One of the game's `.mpg` cinemas, opened from its own bytes: the demultiplexer, the video
/// decoder and the file's declared parameters behind one surface whose whole job is to hand out
/// the next picture. Nothing here touches the engine, so a plain unit test can play a whole file.
/// Every parameter comes from this file's own headers rather than from a profile shared with the
/// other nine, because two of the ten differ.
/// Picture and sound come out of the same clock, so nothing above this needs a second time base.
/// </summary>
public sealed class MpegMovie
{
    private readonly MpegSystemStream _stream;
    private readonly MpegVideoDecoder _video;
    private MpegAudioDecoder? _audio;
    private bool _audioOpened;

    private MpegMovie(MpegSystemStream stream)
    {
        _stream = stream;
        _video = new MpegVideoDecoder(stream.VideoStream, stream.VideoStartTime);
    }

    /// <summary>Picture width in samples, as the sequence header declares it.</summary>
    public int Width => _video.Width;

    /// <summary>Picture height in samples, as the sequence header declares it.</summary>
    public int Height => _video.Height;

    /// <summary>Pictures per second, as the sequence header declares it.</summary>
    public double FrameRate => _video.FrameRate;

    /// <summary>Sample aspect ratio; one means square pixels.</summary>
    public double PixelAspectRatio => _video.PixelAspectRatio;

    /// <summary>How many pictures have been handed out so far.</summary>
    public int FramesDecoded => _video.FramesDecoded;

    /// <summary>The audio packets, in container order, each with its presentation timestamp.</summary>
    public IReadOnlyList<MpegPacket> AudioPackets => _stream.AudioPackets;

    /// <summary>Whether the movie carries a sound track this decoder can read.</summary>
    public bool HasAudio => Audio != null;

    /// <summary>Samples per second per channel, or zero when there is no sound track.</summary>
    public int AudioSampleRate => Audio?.SampleRate ?? 0;

    /// <summary>Channels the sound track carries, or zero when there is none. One of the ten
    /// cinemas is mono, so no caller may assume two.</summary>
    public int AudioChannels => Audio?.Channels ?? 0;

    /// <summary>How many sound frames have been handed out so far.</summary>
    public int AudioFramesDecoded => Audio?.FramesDecoded ?? 0;

    // Opened on first use rather than in the constructor, so a movie played silent never pays
    // for joining its audio packets.
    private MpegAudioDecoder? Audio
    {
        get
        {
            if (!_audioOpened)
            {
                _audioOpened = true;
                _audio = MpegAudioDecoder.TryOpen(_stream.AudioStream, _stream.AudioStartTime);
            }

            return _audio;
        }
    }

    /// <summary>Opens a movie from a file the player owns.</summary>
    public static MpegMovie FromFile(string path) => FromBytes(File.ReadAllBytes(path));

    /// <summary>Opens a movie from bytes already in memory. Throws
    /// <see cref="InvalidDataException"/> when they are not an MPEG-1 system stream carrying
    /// MPEG-1 video.</summary>
    public static MpegMovie FromBytes(byte[] bytes) => new MpegMovie(MpegSystemStream.Demux(bytes));

    /// <summary>The next picture in display order, or null at the end of the movie. ⚠ The frame
    /// aliases buffers the decoder writes again on the next call; convert or copy it first.</summary>
    public VideoFrame? NextFrame() => _video.NextFrame();

    /// <summary>The next block of PCM, or null at the end of the sound track and whenever there
    /// is none. ⚠ The frame aliases a buffer the decoder writes again on the next call; copy or
    /// consume it first.</summary>
    public AudioFrame? NextAudioFrame() => Audio?.NextFrame();

    /// <summary>Starts the movie again from its first picture and its first sample, which is
    /// what a looping background needs. Both clocks restart with it.</summary>
    public void Rewind()
    {
        _video.Rewind();
        _audio?.Rewind();
    }
}
