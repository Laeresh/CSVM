using System;

namespace CSVM.Utils;

/// <summary>
/// A master server's address as a player writes it in the options file or on the command line, and
/// the URLs under it. One reader for the option, the flag and the door, so all three accept the
/// same spellings. Plain text work over <see cref="Uri"/>; nothing here opens a connection.
/// </summary>
public static class MasterAddress
{
    /// <summary>The project's own master server, which a player who set none lists games on, so an
    /// unpacked release reaches internet games without editing a file.</summary>
    public const string Default = "https://csvm.gunmuessig.de";

    /// <summary>The address a run uses: the flag's when given, else the saved option's, else
    /// <see cref="Default"/>. An empty or unreadable flag is <c>""</c> and means off.</summary>
    public static Uri? Choose(string? flag, string? saved)
    {
        return Parse(flag ?? saved ?? Default);
    }

    /// <summary>Reads an address: an <c>http</c> or <c>https</c> URL, or a bare host name, which
    /// takes <c>https</c>. The address may carry a path, which every request is made under. Null for
    /// anything else, and for an empty text.</summary>
    public static Uri? Parse(string? text)
    {
        string value = (text ?? "").Trim();
        if (value.Length == 0)
        {
            return null;
        }

        if (!value.Contains("://", StringComparison.Ordinal))
        {
            value = "https://" + value;
        }

        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https")
            || uri.Host.Length == 0 || uri.Query.Length > 0 || uri.Fragment.Length > 0)
        {
            return null;
        }

        return new UriBuilder(uri) { Path = uri.AbsolutePath.TrimEnd('/') + "/" }.Uri;
    }

    /// <summary>The URL of <paramref name="path"/> under <paramref name="server"/>, as
    /// <see cref="Parse"/> read it. A socket's URL takes the WebSocket scheme of the same
    /// security.</summary>
    public static Uri At(Uri server, string path, bool socket = false)
    {
        ArgumentNullException.ThrowIfNull(server);
        var uri = new Uri(server, (path ?? "").TrimStart('/'));
        return socket ? new UriBuilder(uri) { Scheme = uri.Scheme == "https" ? "wss" : "ws", Port = uri.Port }.Uri : uri;
    }
}
