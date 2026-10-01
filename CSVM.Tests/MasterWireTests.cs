using System;
using CSVM.Net;
using CSVM.Utils;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The master server's wire as the game reads it: the join code's spellings and a listing's
/// limits. Also what a message and a list read as, and the address forms the option and the flag
/// accept. The server compiles the same file, so these hold for both ends.
/// </summary>
[Trait("Tier", "Quick")]
public class MasterWireTests
{
    [Theory]
    [InlineData("ABC-DEF", "ABC-DEF")]
    [InlineData("abc-def", "ABC-DEF")]
    [InlineData("  k7q-x3m ", "K7Q-X3M")]
    [InlineData("K7QX3M", "K7Q-X3M")]
    public void ACodeReadsInEitherCaseWithOrWithoutItsDash(string typed, string written)
    {
        Assert.True(MasterWire.TryCode(typed, out string code));
        Assert.Equal(written, code);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("ABC-DE")]
    [InlineData("ABC-DEFG")]
    [InlineData("AB-CDEF")]
    [InlineData("ABC-DE0")]
    [InlineData("ABC-DEI")]
    [InlineData("10.0.0.7")]
    [InlineData("ABC--DEF")]
    public void AnythingElseIsNotACode(string? typed)
    {
        Assert.False(MasterWire.TryCode(typed, out string code));
        Assert.Equal("", code);
    }

    [Fact]
    public void AMessageRoundTripsAndLeavesItsEmptyFieldsOut()
    {
        var message = new MasterMessage { T = MasterWire.Signal, To = 1, Kind = MasterWire.Candidate, Sdp = "candidate:1 1 UDP", Mid = "0", Index = 0 };

        string text = MasterWire.Write(message);
        Assert.True(MasterWire.TryRead(text, out var read));

        Assert.Equal(MasterWire.Signal, read.T);
        Assert.Equal(1, read.To);
        Assert.Equal("0", read.Mid);
        Assert.Equal(0, read.Index);
        Assert.DoesNotContain("\"game\"", text, StringComparison.Ordinal);
        Assert.Contains("\"t\":\"signal\"", text, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("[1,2]")]
    [InlineData("{\"code\":\"ABC-DEF\"}")]
    [InlineData("{\"t\":\"\"}")]
    public void TextThatIsNotAMessageReadsAsNone(string text)
    {
        Assert.False(MasterWire.TryRead(text, out _));
    }

    [Fact]
    public void AMessagePastTheCapIsNotRead()
    {
        string text = MasterWire.Write(new MasterMessage { T = MasterWire.Signal, Sdp = new string('a', MasterWire.MaxMessageBytes) });

        Assert.False(MasterWire.TryRead(text, out _));
    }

    [Fact]
    public void AListKeepsOnlyGamesWithACodeAndCleansEach()
    {
        string text = "{\"games\":[{\"code\":\"abc-def\",\"name\":\"" + new string('n', 40) + "\",\"kind\":\"dogfight\",\"players\":40,"
            + "\"cap\":8,\"status\":\"waiting\",\"version\":\"0.2\"},{\"code\":\"nope\",\"name\":\"x\"},null]}";

        var games = MasterWire.TryReadList(text);

        var game = Assert.Single(games!);
        Assert.Equal("ABC-DEF", game.Code);
        Assert.Equal(MasterWire.NameLimit, game.Name.Length);
        Assert.Equal(MasterWire.PlayerLimit, game.Players);
        Assert.Null(MasterWire.TryReadList("{\"games\":"));
    }

    [Theory]
    [InlineData("master.example.org", "https://master.example.org/")]
    [InlineData("https://master.example.org", "https://master.example.org/")]
    [InlineData("http://192.0.2.4:8080/", "http://192.0.2.4:8080/")]
    [InlineData("https://example.org/csvm", "https://example.org/csvm/")]
    public void AnAddressReadsAsHttpOrHttps(string text, string read)
    {
        Assert.Equal(read, MasterAddress.Parse(text)!.ToString());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("ftp://example.org")]
    [InlineData("https://example.org/?x=1")]
    [InlineData("ws://example.org")]
    public void AnythingElseIsNoAddress(string text)
    {
        Assert.Null(MasterAddress.Parse(text));
    }

    [Fact]
    public void ThePathsHangUnderTheAddressAndTheSocketTakesItsScheme()
    {
        var secure = MasterAddress.Parse("https://example.org/csvm")!;
        var plain = MasterAddress.Parse("http://192.0.2.4:8080")!;

        Assert.Equal("https://example.org/csvm/api/games", MasterAddress.At(secure, MasterWire.GamesPath).ToString());
        Assert.Equal("wss://example.org/csvm/ws", MasterAddress.At(secure, MasterWire.SocketPath, socket: true).ToString());
        Assert.Equal("ws://192.0.2.4:8080/ws", MasterAddress.At(plain, MasterWire.SocketPath, socket: true).ToString());
    }
}
