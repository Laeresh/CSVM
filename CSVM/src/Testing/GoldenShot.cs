using System;
using System.Security.Cryptography;
using System.Text;
using Godot;

namespace CSVM.Testing;

/// <summary>
/// The engine half of the golden-image tripwire: it reduces a captured frame to one md5 and names
/// the hardware that drew it.
///
/// <para><b>The hash is over the RAW pixel buffer</b> (<see cref="Image.GetData"/>), never over the
/// saved PNG. Encoded bytes differ between two byte-identical images — all 881 of this install's C1
/// texture PNGs do — so a PNG hash reports encoder state, not pixels.</para>
///
/// <para><b>Why the adapter travels with the hash.</b> A driver or GPU change legitimately moves
/// every hash at once. Recording the adapter with the numbers turns that from an unexplained mass
/// failure into a one-line explanation, which is the difference between "regenerate" and
/// "stop the line".</para>
/// </summary>
public static class GoldenShot
{
    /// <summary>md5 of the image's raw pixel bytes, lower-case hex.</summary>
    public static string PixelHash(Image image)
    {
        byte[] data = image.GetData();
        byte[] digest = MD5.HashData(data);
        var sb = new StringBuilder(digest.Length * 2);
        foreach (byte b in digest)
        {
            sb.Append(b.ToString("x2", System.Globalization.CultureInfo.InvariantCulture));
        }
        return sb.ToString();
    }

    /// <summary>The GPU that rendered this session, as <c>&lt;adapter&gt; / &lt;api&gt;</c>.
    /// Deliberately one opaque string: it is read by a human comparing two runs, never parsed.</summary>
    public static string Adapter()
    {
        try
        {
            string name = RenderingServer.GetVideoAdapterName();
            string api = RenderingServer.GetVideoAdapterApiVersion();
            return $"{name} / {api}";
        }
        catch (Exception e)
        {
            return $"unknown ({e.GetType().Name})";
        }
    }
}
