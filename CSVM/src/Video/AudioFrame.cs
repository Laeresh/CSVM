namespace CSVM.Video;

/// <summary>
/// One decoded layer II frame: 1152 PCM samples per channel as floats, interleaved by channel,
/// with the moment the first of them is heard in seconds on the container's clock. Samples are
/// nominally within plus or minus one, and loud material can leave one slightly outside.
/// A decoder hands out the same frame over and over, so it is valid only until the next one is
/// asked for.
/// </summary>
public sealed class AudioFrame
{
    internal AudioFrame(int channels, int sampleRate)
    {
        Channels = channels;
        SampleRate = sampleRate;
        Samples = new float[channels * AudioLayer2Tables.SamplesPerFrame];
    }

    /// <summary>Channels the stream carries: one for a mono file, two otherwise.</summary>
    public int Channels { get; }

    /// <summary>Samples per second per channel, as the frame header declares it.</summary>
    public int SampleRate { get; }

    /// <summary>Samples in this frame per channel, which layer II fixes at 1152.</summary>
    public int SamplesPerChannel => AudioLayer2Tables.SamplesPerFrame;

    /// <summary>When the first sample is heard, in seconds on the container's clock.</summary>
    public double Time { get; internal set; }

    /// <summary>How long the frame sounds for, in seconds.</summary>
    public double Duration => SamplesPerChannel / (double)SampleRate;

    /// <summary>The samples, channel-interleaved: left, right, left, right for a stereo file
    /// and one run of 1152 for a mono one.</summary>
    public float[] Samples { get; }
}
