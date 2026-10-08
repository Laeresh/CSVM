using System;
using System.Buffers.Binary;
using System.Collections.Generic;

namespace CSVM.Net;

/// <summary>
/// The frame every payload rides in over WebRTC, and the sequenced discard, engine-free. A WebRTC
/// data channel has one delivery mode, so the carrier sends reliable or unreliable unordered. The
/// session's channel rides this header instead: the channel, a flags byte, and a sequenced
/// payload's sequence on that channel. The receiver discards a sequenced payload not newer than
/// the newest from that sender on that channel. That is the guarantee
/// <see cref="NetReliability"/> names.
/// </summary>
public sealed class WebRtcFraming
{
    /// <summary>The header's width in bytes.</summary>
    public const int HeaderBytes = 4;

    /// <summary>The flag a sequenced payload carries, which the receiver discards by.</summary>
    public const byte SequencedFlag = 0x01;

    private readonly Dictionary<(int Peer, int Channel), ushort> _sent = new();
    private readonly Dictionary<(int Peer, int Channel), ushort> _newest = new();

    /// <summary>How many sequenced payloads arrived stale and were dropped.</summary>
    public int DiscardedStale { get; private set; }

    /// <summary>Writes the header for <paramref name="channel"/> and <paramref name="payload"/>
    /// behind it, and returns the whole frame. A sequenced payload takes the next sequence of its
    /// channel to <paramref name="peer"/>.</summary>
    public byte[] Frame(int peer, int channel, NetReliability reliability, ReadOnlySpan<byte> payload)
    {
        if (channel is < 0 or > byte.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(channel), channel, "a framed channel is 0 to 255");
        }

        var frame = new byte[HeaderBytes + payload.Length];
        frame[0] = (byte)channel;
        if (reliability == NetReliability.UnreliableSequenced)
        {
            var key = (peer, channel);
            ushort sequence = _sent.TryGetValue(key, out ushort last) ? (ushort)(last + 1) : (ushort)0;
            _sent[key] = sequence;
            frame[1] = SequencedFlag;
            BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(2), sequence);
        }

        payload.CopyTo(frame.AsSpan(HeaderBytes));
        return frame;
    }

    /// <summary>Reads a frame from <paramref name="peer"/>. False for a frame too short to hold a
    /// header. False too for a sequenced payload not newer than the newest from that peer on its
    /// channel, counted in <see cref="DiscardedStale"/>.</summary>
    public bool TryOpen(int peer, byte[] frame, out int channel, out ArraySegment<byte> payload)
    {
        ArgumentNullException.ThrowIfNull(frame);
        channel = 0;
        payload = default;
        if (frame.Length < HeaderBytes)
        {
            return false;
        }

        channel = frame[0];
        if ((frame[1] & SequencedFlag) != 0)
        {
            ushort sequence = BinaryPrimitives.ReadUInt16LittleEndian(frame.AsSpan(2));
            var key = (peer, channel);
            if (_newest.TryGetValue(key, out ushort newest) && !Newer(sequence, newest))
            {
                DiscardedStale++;
                return false;
            }

            _newest[key] = sequence;
        }

        payload = new ArraySegment<byte>(frame, HeaderBytes, frame.Length - HeaderBytes);
        return true;
    }

    /// <summary>Forgets both directions' sequences for <paramref name="peer"/>, which left. A peer
    /// that comes back under the same id starts afresh.</summary>
    public void Forget(int peer)
    {
        foreach (var key in new List<(int Peer, int Channel)>(_sent.Keys))
        {
            if (key.Peer == peer)
            {
                _sent.Remove(key);
            }
        }

        foreach (var key in new List<(int Peer, int Channel)>(_newest.Keys))
        {
            if (key.Peer == peer)
            {
                _newest.Remove(key);
            }
        }
    }

    // Serial number arithmetic over 16 bits: newer when ahead by less than half the range. The
    // sequence then wraps without the payload after the wrap reading as stale.
    private static bool Newer(ushort sequence, ushort newest) => (ushort)(sequence - newest) is > 0 and < 0x8000;
}
