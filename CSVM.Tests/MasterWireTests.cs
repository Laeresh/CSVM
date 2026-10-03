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

    [Fact]
    public void TheUnlistedMarkIsWrittenOnlyWhenSetAndSurvivesTheClean()
    {
        var listed = new MasterGame { Name = "Open", Kind = MasterWire.DogfightKind };
        var hidden = new MasterGame { Name = "Hidden", Kind = MasterWire.CoopKind, Unlisted = true };

        string open = MasterWire.Write(new MasterMessage { T = MasterWire.Host, Game = listed });
        string secret = MasterWire.Write(new MasterMessage { T = MasterWire.Host, Game = hidden });

        Assert.DoesNotContain("unlisted", open, StringComparison.Ordinal);
        Assert.Contains("\"unlisted\":true", secret, StringComparison.Ordinal);
        Assert.True(MasterWire.TryRead(secret, out var read));
        Assert.True(read.Game!.Unlisted);
        Assert.True(MasterWire.Clean(read.Game).Unlisted);
        Assert.False(MasterWire.Clean(listed).Unlisted);
    }

    [Fact]
    public void AnOlderBuildsListingWithNoMarkIsListed()
    {
        const string older = "{\"t\":\"host\",\"game\":{\"name\":\"Old\",\"kind\":\"dogfight\",\"players\":1,\"cap\":8,"
            + "\"status\":\"waiting\",\"version\":\"0.1\"}}";

        Assert.True(MasterWire.TryRead(older, out var read));
        Assert.False(read.Game!.Unlisted);
    }

    [Fact]
    public void TheProtocolVersionRoundTripsAndAnOlderSenderNamesNone()
    {
        string text = MasterWire.Write(new MasterMessage { T = MasterWire.Join, Code = "ABC-DEF", Protocol = MasterWire.ProtocolVersion });
        const string older = "{\"t\":\"join\",\"code\":\"ABC-DEF\",\"version\":\"0.3\"}";

        Assert.Contains("\"protocol\":1", text, StringComparison.Ordinal);
        Assert.True(MasterWire.TryRead(text, out var read));
        Assert.Equal(MasterWire.ProtocolVersion, read.Protocol);
        Assert.True(MasterWire.TryRead(older, out var old));
        Assert.Null(old.Protocol);
    }

    [Theory]
    [InlineData("{\"games\":[]}", 0, true)]
    [InlineData("{\"games\":[],\"protocol\":1,\"oldest\":1}", 1, true)]
    [InlineData("{\"games\":[],\"protocol\":3,\"oldest\":2}", 2, false)]
    public void AListNamesTheOldestProtocolItsServerServes(string text, int oldest, bool served)
    {
        Assert.NotNull(MasterWire.TryReadList(text, out int read));
        Assert.Equal(oldest, read);
        Assert.Equal(served, MasterWire.Serves(read));
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

    [Theory]
    [InlineData(null, null, MasterAddress.Default)]
    [InlineData(null, "saved.example.org", "https://saved.example.org")]
    [InlineData("flag.example.org", "saved.example.org", "https://flag.example.org")]
    [InlineData("", "saved.example.org", null)]
    public void TheFlagBeatsTheSavedOptionWhichBeatsTheDefault(string? flag, string? saved, string? chosen)
    {
        Assert.Equal(MasterAddress.Parse(chosen), MasterAddress.Choose(flag, saved));
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
