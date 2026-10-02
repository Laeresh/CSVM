using CSVM.Net;
using CSVM.UI.Menu;
using CSVM.Utils;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// What the Game and Player Information boxes ask, off-engine. The Maximum spinner's range is held
/// to each kind's cap, and a name meets the original's OK rule. The seven voices keep their order.
/// The callsign, voice and game name are remembered through the options file.
/// </summary>
public class NetPlayerInfoTests
{
    [Theory]
    [InlineData(NetSessionKind.Dogfight, 8, 8)]
    [InlineData(NetSessionKind.Dogfight, 16, 16)]
    [InlineData(NetSessionKind.Dogfight, 17, 16)]
    [InlineData(NetSessionKind.Dogfight, 1, 2)]
    [InlineData(NetSessionKind.CampaignCoop, 8, 4)]
    [InlineData(NetSessionKind.CampaignCoop, 3, 3)]
    [InlineData(NetSessionKind.CampaignCoop, 0, 2)]
    public void TheMaximumIsHeldToTheSpinnersFloorAndTheKindsCap(NetSessionKind kind, int asked, int held)
    {
        Assert.Equal(held, NetPlayerInfo.ClampPlayers(kind, asked));
    }

    [Fact]
    public void TheCapsAreFourHumansForCoopAndSixteenForADogfight()
    {
        Assert.Equal(NetPlayFeature.CoopHumans, NetPlayerInfo.PlayerCap(NetSessionKind.CampaignCoop));
        Assert.Equal(4, NetPlayerInfo.PlayerCap(NetSessionKind.CampaignCoop));
        Assert.Equal(16, NetPlayerInfo.PlayerCap(NetSessionKind.Dogfight));
        Assert.Equal(8, new NetPlayerInfo().MaxPlayers);
    }

    [Fact]
    public void ACoopHostStartsPrivateADogfightHostPublicAndTheChoiceIsNeverRemembered()
    {
        Assert.True(NetPlayerInfo.DefaultPrivate(NetSessionKind.CampaignCoop));
        Assert.False(NetPlayerInfo.DefaultPrivate(NetSessionKind.Dogfight));
        Assert.Null(new NetPlayerInfo().Private);

        var saved = new OptionsDef();
        new NetPlayerInfo { Callsign = "Laeresh", GameName = "DaRein", Private = true }.Remember(saved, game: true);
        Assert.Null(NetPlayerInfo.Remembered(saved).Private);
    }

    [Theory]
    [InlineData("Laeresh", true)]
    [InlineData(" Ace ", true)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData("\t", false)]
    public void ANameOfSpacesAloneIsRefusedAsTheOriginalRefusesIt(string name, bool valid)
    {
        Assert.Equal(valid, NetPlayerInfo.IsValidName(name));
    }

    [Fact]
    public void TheVoiceListIsTheScriptsSevenVoicesInOrder()
    {
        Assert.Equal(7, PilotVoices.All.Count);
        Assert.Equal(new[] { 48, 2, 24, 29, 44, 26, 31 }, System.Linq.Enumerable.Select(PilotVoices.All, v => (int)v.Speaker));
        Assert.Equal(new[] { 10039, 10040, 10041, 10042, 10043, 10044, 10045 }, System.Linq.Enumerable.Select(PilotVoices.All, v => v.StringId));
        Assert.Equal("Gruff Male", PilotVoices.All[5].Name);
        Assert.Equal(6, PilotVoices.Wire(5));
        Assert.Equal(5, PilotVoices.FromWire(6));

        // ABLE-TO-FAIL CONTROL: outside the list there is no voice on the wire, and none read back.
        Assert.Equal(CoopPickMessage.NoVoice, PilotVoices.Wire(7));
        Assert.Equal(-1, PilotVoices.FromWire(CoopPickMessage.NoVoice));
        Assert.Equal(PilotVoices.Default, PilotVoices.Clamp(12));
    }

    // A row's script value is the pilot VO id its player's aircraft speaks as. It is read off the
    // byte a pick or a seat carries, Nathan Zachary 48 through Texan Male 31.
    [Fact]
    public void EveryVoiceByteSpeaksAsItsRowsPilot()
    {
        var spoken = System.Linq.Enumerable.Select(System.Linq.Enumerable.Range(1, 7), w => PilotVoices.SpeakerFor((byte)w));
        Assert.Equal(new int?[] { 48, 2, 24, 29, 44, 26, 31 }, spoken);
        Assert.Equal(48, PilotVoices.SpeakerFor(PilotVoices.Wire(PilotVoices.CoopHost)));

        // ABLE-TO-FAIL CONTROL: no voice, and a byte past the list, speak as nobody.
        Assert.Null(PilotVoices.SpeakerFor(CoopPickMessage.NoVoice));
        Assert.Null(PilotVoices.SpeakerFor(8));
    }

    [Fact]
    public void TheCallsignVoiceAndGameNameAreRememberedForTheNextSession()
    {
        string dir = TestData.TempDir();
        var store = new OptionsStore(dir);
        var saved = store.Load();
        new NetPlayerInfo { Callsign = "Laeresh", Voice = 5, GameName = "DaRein", MaxPlayers = 12 }.Remember(saved, game: true);
        store.Save(saved);

        // A new session reads the file afresh; the pilot's own name does not win over a saved callsign.
        var next = NetPlayerInfo.Remembered(new OptionsStore(dir).Load(), "Zachary");
        Assert.Equal("Laeresh", next.Callsign);
        Assert.Equal(5, next.Voice);
        Assert.Equal("DaRein", next.GameName);
        Assert.Equal(NetPlayerInfo.DefaultPlayers, next.MaxPlayers);
    }

    [Fact]
    public void APlayerWhoNeverSavedACallsignStartsOnTheirPilotsNameAndARefusedNameIsNeverSaved()
    {
        var fresh = NetPlayerInfo.Remembered(new OptionsDef(), "Montgomery Fairweather");
        Assert.Equal("Montgomery F", fresh.Callsign);
        Assert.Equal(NetPlayerInfo.CallsignLimit, fresh.Callsign.Length);
        Assert.Equal("Montgomery F", fresh.GameName);
        Assert.Equal(PilotVoices.Default, fresh.Voice);

        var def = new OptionsDef { NetCallsign = "Laeresh" };
        new NetPlayerInfo { Callsign = "   ", Voice = 2 }.Remember(def, game: false);
        Assert.Equal("Laeresh", def.NetCallsign);
        Assert.Equal(2, def.NetVoice);
        Assert.Null(def.NetGameName);

        // ABLE-TO-FAIL CONTROL: a valid callsign does replace the saved one.
        new NetPlayerInfo { Callsign = "Ace" }.Remember(def, game: false);
        Assert.Equal("Ace", def.NetCallsign);
    }

    [Fact]
    public void TheOptionsFileDropsANetworkNameOrVoiceItCouldNotHaveWritten()
    {
        var def = OptionsStore.Deserialize(
            "{\"version\":1,\"netCallsign\":\"Laeresh\",\"netVoice\":6,\"netGameName\":\"DaRein\"}");
        Assert.NotNull(def);
        Assert.Equal("Laeresh", def!.NetCallsign);
        Assert.Equal(6, def.NetVoice);
        Assert.Equal("DaRein", def.NetGameName);

        var bad = OptionsStore.Deserialize(
            "{\"version\":1,\"netCallsign\":\"A name far past the box\",\"netVoice\":7,\"netGameName\":3}");
        Assert.NotNull(bad);
        Assert.Null(bad!.NetCallsign);
        Assert.Null(bad.NetVoice);
        Assert.Null(bad.NetGameName);
    }
}
