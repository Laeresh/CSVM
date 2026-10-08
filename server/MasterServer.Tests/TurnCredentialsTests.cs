using System;
using CSVM.Net;
using Xunit;

namespace CSVM.Master.Tests;

/// <summary>coturn's shared-secret credential, against a vector worked out apart from this code,
/// and the ICE list a negotiation is handed for each way the settings can stand.</summary>
public class TurnCredentialsTests
{
    [Fact]
    public void ACredentialIsTheBase64HmacSha1OfTheExpiryAndLabel()
    {
        // Worked out with .NET Framework's HMACSHA1 in Windows PowerShell 5.1, not this code.
        var (username, credential) = TurnCredentials.Mint(
            "north-shore-secret", "ABC-DEF.2", DateTimeOffset.FromUnixTimeSeconds(1_700_000_000));

        Assert.Equal("1700000000:ABC-DEF.2", username);
        Assert.Equal("4xSuAGyxOnDCNsUPKTSfPzm6SgY=", credential);
    }

    [Fact]
    public void NoIceSettingsHandOutNothing()
    {
        var ice = new TurnCredentials(new MasterOptions(), new ManualClock());

        Assert.Empty(ice.For("x"));
    }

    [Fact]
    public void TurnWithoutASecretHandsOutStunAlone()
    {
        var options = new MasterOptions { Stun = "stun:a.example:3478", Turn = "turn:a.example:3478" };

        var server = Assert.Single(new TurnCredentials(options, new ManualClock()).For("x"));

        Assert.Equal("stun:a.example:3478", Assert.Single(server.Urls));
        Assert.Null(server.Username);
        Assert.Null(server.Credential);
    }

    [Fact]
    public void ATurnEntryExpiresTheConfiguredMinutesFromNow()
    {
        var clock = new ManualClock();
        var options = new MasterOptions
        {
            Stun = "stun:a.example:3478",
            Turn = "turn:a.example:3478?transport=udp, turn:a.example:3478?transport=tcp",
            TurnSecret = "north-shore-secret",
            TurnCredentialMinutes = 90,
        };

        var servers = new TurnCredentials(options, clock).For("ABC-DEF.2");

        Assert.Equal(2, servers.Count);
        var turn = servers[1];
        Assert.Equal(new[] { "turn:a.example:3478?transport=udp", "turn:a.example:3478?transport=tcp" }, turn.Urls);
        long expiry = clock.Now.AddMinutes(90).ToUnixTimeSeconds();
        Assert.Equal($"{expiry}:ABC-DEF.2", turn.Username);
        Assert.Equal(TurnCredentials.Mint("north-shore-secret", "ABC-DEF.2", clock.Now.AddMinutes(90)).Credential, turn.Credential);
    }

    [Fact]
    public void TheIceListSurvivesTheWire()
    {
        var options = new MasterOptions { Stun = "stun:a.example:3478", Turn = "turn:a.example:3478", TurnSecret = "s" };
        var message = new MasterMessage { T = MasterWire.Joined, Peer = 2, Ice = new TurnCredentials(options, new ManualClock()).For("l") };

        Assert.True(MasterWire.TryRead(MasterWire.Write(message), out var read));

        Assert.Equal(2, read.Ice!.Count);
        Assert.Equal(message.Ice[1].Credential, read.Ice[1].Credential);
        Assert.DoesNotContain("\"from\"", MasterWire.Write(message), StringComparison.Ordinal);
    }
}
