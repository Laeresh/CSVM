using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using CSVM.Net;

namespace CSVM.Master;

/// <summary>
/// The ICE servers a negotiation is handed, with a TURN credential minted for it under coturn's
/// shared-secret scheme (<c>use-auth-secret</c>): the user name is the expiry as Unix seconds, a
/// colon and a label, and the credential is the Base64 HMAC-SHA1 of that name under the secret.
/// coturn checks both against its own copy of the secret, so no password ships in the game.
/// </summary>
public sealed class TurnCredentials
{
    private readonly MasterOptions _options;
    private readonly TimeProvider _time;

    /// <summary>Credentials minted from <paramref name="options"/> against
    /// <paramref name="time"/>'s clock.</summary>
    public TurnCredentials(MasterOptions options, TimeProvider time)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _time = time ?? throw new ArgumentNullException(nameof(time));
    }

    /// <summary>Whether <see cref="For"/> mints a TURN credential: a secret and TURN URLs are set.
    /// </summary>
    public bool Mints => _options.TurnSecret.Length > 0 && _options.TurnUrls.Count > 0;

    /// <summary>The user name and credential for <paramref name="label"/>, good until
    /// <paramref name="expires"/>, signed with <paramref name="secret"/>.</summary>
    public static (string Username, string Credential) Mint(string secret, string label, DateTimeOffset expires)
    {
        ArgumentNullException.ThrowIfNull(secret);
        string username = $"{expires.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture)}:{label}";
        using var hmac = new HMACSHA1(Encoding.UTF8.GetBytes(secret));
        string credential = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(username)));
        return (username, credential);
    }

    /// <summary>The servers for one negotiation, the TURN entry labelled <paramref name="label"/>:
    /// the STUN entry when any is set, then the TURN entry when <see cref="Mints"/>.</summary>
    public List<MasterIceServer> For(string label)
    {
        var servers = new List<MasterIceServer>();
        if (_options.StunUrls.Count > 0)
        {
            servers.Add(new MasterIceServer { Urls = new List<string>(_options.StunUrls) });
        }

        if (Mints)
        {
            var expires = _time.GetUtcNow().AddMinutes(Math.Max(1, _options.TurnCredentialMinutes));
            var (username, credential) = Mint(_options.TurnSecret, label, expires);
            servers.Add(new MasterIceServer
            {
                Urls = new List<string>(_options.TurnUrls),
                Username = username,
                Credential = credential,
            });
        }

        return servers;
    }
}
