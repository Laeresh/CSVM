using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CSVM.Extraction;
using CSVM.Flight.Hangar;
using CSVM.Flight.Weapons;
using CSVM.Launch;
using CSVM.Mech3;
using CSVM.Net;
using CSVM.Session.Roster;
using CSVM.Spec;
using CSVM.UI.Menu;
using Xunit;

namespace CSVM.Tests;

/// <summary>A lobby host's launch field holds its bots to the outlaw list as it holds its people.
/// No bot sits on an outlawed airframe or carries an outlawed gun calibre, ammunition, rocket or
/// nitro injector. A militia whose def the list would strip is not drawn, so a drawn bot keeps
/// every weapon its def carries.</summary>
[Trait("Tier", "Quick")]
public sealed class LobbyBotOutlawTests
{
    private const string Warhawk = "player_warhawk";

    private static readonly StockLoadouts Stock =
        StockLoadouts.Load(Path.Combine(TestData.RepoRoot, "CSVM", "data", "stock_loadouts.json"));

    [ExtractedDataFact]
    public void TheLaunchFieldSeatsNoBotOnAnOutlawedAirframeOrWeapon()
    {
        var (zrdr, messages, militias) = Install();
        // The Warhawk, the armor-piercing rocket six militias carry, and the aerial torpedo.
        var list = new NetPlaneRules(false, true, 0).With(NetPlaneRules.AirframeFlag + StockAirframes.IdOf(Warhawk)!.Value, true)
            .With(NetPlaneRules.RocketFlag + Row("wep_05"), true).With(NetPlaneRules.RocketFlag + Row("wep_14"), true);
        var outlawed = new[] { "wep_05", "wep_14" };
        var arms = SeatFields.BotArmsUnder(list, zrdr, messages, Stock);

        int armed = 0;
        for (int seed = 0; seed < 40; seed++)
        {
            var (roster, fits) = Field(list, arms, seed);
            foreach (var (bot, fit) in roster.Zip(fits).Where(seat => seat.First.IsBot))
            {
                var weapons = WeaponsOf(militias, bot);
                Assert.NotEqual(Warhawk, bot.PlaneNode);
                Assert.Contains((bot.Militia, bot.PlaneNode), BotSeats.Pairs);
                Assert.DoesNotContain(weapons, id => outlawed.Contains(id));
                Assert.Equal(fit, list.Enforce(fit));
                armed += weapons.Count > 0 ? 1 : 0;
            }
        }

        Assert.True(armed > 40, $"bots still fly their militia's def where it survives the list ({armed})");

        // ABLE-TO-FAIL CONTROL: the same flags with Outlaw Components clear draw what a field with no
        // rules draws. That field seats Warhawks and the defs the list strips.
        var clear = list with { Outlawing = false };
        Assert.Null(SeatFields.BotArmsUnder(clear, zrdr, messages, Stock));
        bool struck = false;
        for (int seed = 0; seed < 40; seed++)
        {
            var (roster, fits) = Field(clear, null, seed);
            Assert.Equal(Field(null, null, seed).Roster, roster);
            Assert.All(fits.Skip(1), fit => Assert.True(fit.IsStock));
            struck |= roster.Any(bot => bot.IsBot && (bot.PlaneNode == Warhawk || WeaponsOf(militias, bot).Any(outlawed.Contains)));
        }

        Assert.True(struck, "an unticked list seats a bot the ticked list refuses");
    }

    // Outlawed slug strips every militia def's gun, so each bot flies its airframe's stock arms. With
    // high explosive outlawed too, it takes the first ammunition and rocket the list allows.
    [ExtractedDataFact]
    public void ABotTheListStripsOfItsDefFliesTheStockArmsOnALegalFit()
    {
        var (zrdr, messages, militias) = Install();
        var list = new NetPlaneRules(false, true, 0).With(NetPlaneRules.AmmoFlag, true).With(NetPlaneRules.RocketFlag + Row("wep_06"), true);

        var (roster, fits) = Field(list, SeatFields.BotArmsUnder(list, zrdr, messages, Stock), 3);

        var bots = roster.Zip(fits).Where(seat => seat.First.IsBot).ToList();
        Assert.Equal(12, bots.Count);
        Assert.All(bots, seat => Assert.Empty(WeaponsOf(militias, seat.First)));
        Assert.All(bots, seat => Assert.Equal((1, Row("wep_05") + 1), (seat.Second.AmmoAt(0), seat.Second.OrdnanceAt(0))));
        Assert.All(bots, seat => Assert.Equal(seat.Second, list.Enforce(seat.Second)));

        // ABLE-TO-FAIL CONTROL: the original's reset of the stock fit leaves the same bot no gun.
        Assert.Equal(NetPlaneRules.NoAmmo, list.Enforce(default).AmmoAt(0));
    }

    // The outlaw list's rocket rows are the Ammo Selection rocket table's, langui 3380+i, whose
    // row 10 is Aerial Torpedoes: Black Hat's wep_14. Ticking it leaves the Black Hat Warhawk out.
    [ExtractedDataFact]
    public void TheTorpedoRowOutlawsTheBlackHatWarhawk()
    {
        var (zrdr, messages, _) = Install();
        Assert.Equal(10, Row("wep_14"));
        byte blackHat = BotSeats.Pairs.Single(pair => BotSeats.IsTorpedoPair(pair.Militia, pair.Plane)).Militia;
        var torpedo = new NetPlaneRules(false, true, 0).With(NetPlaneRules.RocketFlag + 10, true);

        Assert.False(SeatFields.BotArmsUnder(torpedo, zrdr, messages, Stock)!(blackHat, Warhawk));

        // ABLE-TO-FAIL CONTROL: the row beside it leaves the Warhawk its torpedoes.
        Assert.True(SeatFields.BotArmsUnder(torpedo.With(NetPlaneRules.RocketFlag + 10, false).With(NetPlaneRules.RocketFlag + 9, true),
            zrdr, messages, Stock)!(blackHat, Warhawk));
    }

    // Only the Black Hat Warhawk carries torpedoes, under the one-per-team cap. A list that allows
    // the torpedo row alone leaves a stock-armed bot's pylons empty.
    [Fact]
    public void TheLegalFitNeverFillsThePylonsWithTorpedoes()
    {
        var onlyTorpedo = Enumerable.Range(0, 10).Aggregate(new NetPlaneRules(false, true, 0), (rules, row) => rules.With(NetPlaneRules.RocketFlag + row, true));

        var fit = BotSeats.LegalFit(onlyTorpedo);

        Assert.All(Enumerable.Range(0, CoopFit.Cells), cell => Assert.Equal(NetPlaneRules.NoRocket + 1, fit.OrdnanceAt(cell)));
        Assert.Equal(fit, onlyTorpedo.Enforce(fit));

        // ABLE-TO-FAIL CONTROL: one other row let back in fills every pylon with it.
        var nine = BotSeats.LegalFit(onlyTorpedo.With(NetPlaneRules.RocketFlag + 9, false));
        Assert.All(Enumerable.Range(0, CoopFit.Cells), cell => Assert.Equal(10, nine.OrdnanceAt(cell)));
    }

    // Every militia def's AI gun is wep_130 (.30) or wep_140 (.40). Outlawing .40 leaves out the
    // defs carrying wep_140 even on airframes the list admits, which pins the calibre-to-row mapping.
    [ExtractedDataFact]
    public void AnOutlawedCalibreLeavesOutTheDefsWhoseAiGunCarriesIt()
    {
        var (zrdr, messages, militias) = Install();
        Assert.Equal((30, 40), (WeaponDefs.Load(zrdr).Get("wep_130")!.Caliber, WeaponDefs.Load(zrdr).Get("wep_140")!.Caliber));
        var list = new NetPlaneRules(false, true, 0).With(NetPlaneRules.GunFlag + 1, true);
        var arms = SeatFields.BotArmsUnder(list, zrdr, messages, Stock);

        int thirties = 0;
        bool struck = false;
        for (int seed = 0; seed < 40; seed++)
        {
            var (roster, _) = Field(list, arms, seed);
            Assert.All(roster.Where(seat => seat.IsBot), bot => Assert.DoesNotContain("wep_140", WeaponsOf(militias, bot)));
            thirties += roster.Count(seat => seat.IsBot && WeaponsOf(militias, seat).Contains("wep_130"));

            // ABLE-TO-FAIL CONTROL: ticking nothing seats a .40 def on an airframe the list admits.
            var (clear, _) = Field(list with { Outlawing = false }, null, seed);
            struck |= clear.Any(seat => seat.IsBot && BotSeats.AirframeAdmitted(list, seat.PlaneNode) && WeaponsOf(militias, seat).Contains("wep_140"));
        }

        Assert.True(thirties > 0, "a def on a .30 gun still flies");
        Assert.True(struck, "an unticked list seats a def whose gun the ticked list outlaws");
    }

    // A host on an admitted custom plane can launch under rules that refuse all eleven stock
    // airframes. No airframe is left for a bot, so the field leaves the bots out.
    [Fact]
    public void RulesThatRefuseEveryStockAirframeSeatNoBot()
    {
        var none = Enumerable.Range(0, 11).Aggregate(new NetPlaneRules(true, true, 0), (rules, airframe) => rules.With(NetPlaneRules.AirframeFlag + airframe, true));

        var (roster, fits) = Field(none, null, 1);

        Assert.Equal((1, 1), (roster.Length, fits.Length));
        Assert.False(roster[0].IsBot);

        // ABLE-TO-FAIL CONTROL: one airframe let back in seats all twelve bots on it.
        var (seated, _) = Field(none.With(NetPlaneRules.AirframeFlag + StockAirframes.IdOf("player_fury")!.Value, false), null, 1);
        Assert.Equal(12, seated.Count(seat => seat.IsBot && seat.PlaneNode == "player_fury"));
    }

    // A seat's injector comes from its custom build alone (HumanFlightAdapter), and a bot seats none,
    // so it flies its airframe's stock engine. Were that engine nitrous, the row would refuse every
    // airframe and seat no bot. A guest's build reaches its seat through a real lobby, as at launch.
    [Fact]
    public void TheNitroRowSeatsNoBotAndNoGuestOnAnInjectorEngine()
    {
        var mesh = LoopbackTransport.Mesh(2, new LoopbackConditions(0.0, 0.0, 0.0), new Random(2));
        var wires = new[] { new NetLobby(mesh[0]), new NetLobby(mesh[1]) };
        var host = new DogfightLobby(wires[0], () => "Host");
        var guest = new DogfightLobby(wires[1], () => "Lucy", 0);
        guest.Show();
        Assert.True(host.SetAllowCustomPlanes(true) && host.SetOutlawComponents(true));
        Settle(wires, host, guest);
        Assert.True(guest.PickCustom(new NetPlaneBuild { Name = "Bee", Airframe = 2, Engine = 4 }, default));
        Settle(wires, host, guest);

        // ABLE-TO-FAIL CONTROL: with the row clear, the guest's injector build comes back on its seat.
        // It runs through the same field and check the bots meet.
        var (open, openBuilds) = LobbyField(wires[0], host.Rules, 0);
        Assert.True(open[1] is { IsBot: false } && CarriesNitro(openBuilds[1]), "the guest's build carries the injector");
        Assert.DoesNotContain(Enumerable.Range(0, open.Length), seat => open[seat].IsBot && CarriesNitro(openBuilds[seat]));

        Assert.True(host.SetOutlawed(NetPlaneRules.NitroFlag, true));
        Settle(wires, host, guest);
        Assert.Equal(PlaneRefusal.Engine, guest.Refusal);
        for (int seed = 0; seed < 40; seed++)
        {
            var (roster, builds) = LobbyField(wires[0], host.Rules, seed);
            Assert.Equal(12, roster.Count(seat => seat.IsBot));
            Assert.All(builds, build => Assert.False(CarriesNitro(build), "a seat carries the injector"));
            Assert.All(roster.Where(seat => seat.IsBot), bot => Assert.True(host.Rules.AdmitsStock(StockAirframes.IdOf(bot.PlaneNode)!.Value)));
        }
    }

    // An install whose defs cannot be read must not leave the arms unjudged. The draw keeps to
    // Fortune Hunter, which no shipped def is titled for, so its bots fly the legal stock fit.
    [ExtractedDataFact]
    public void UnreadableDefsFailClosedToFortuneHunter()
    {
        var (zrdr, messages, _) = Install();
        var list = new NetPlaneRules(false, true, 0).With(NetPlaneRules.RocketFlag + 9, true);
        byte hunter = (byte)InstantAction.Militias.ToList().FindIndex(m => m.Name == InstantAction.PlayerMilitia);
        byte medusa = (byte)InstantAction.Militias.ToList().FindIndex(m => m.Name == "Medusa");

        var closed = SeatFields.BotArmsUnder(list, ZrdrSet.None, messages, Stock)!;

        Assert.True(closed(hunter, "player_kestrel"));
        Assert.False(closed(medusa, "player_kestrel"));

        // ABLE-TO-FAIL CONTROL: the readable install admits Medusa's Kestrel, whose flak rocket the list allows.
        Assert.True(SeatFields.BotArmsUnder(list, zrdr, messages, Stock)!(medusa, "player_kestrel"));
    }

    private static (ZrdrSet Zrdr, Messages Messages, MilitiaFits Militias) Install()
    {
        var zrdr = TestZrdr.Root;
        var messages = Messages.FromExtracted(Path.Combine(TestData.ExtractedRoot!, "messages.json"));
        return (zrdr, messages, MilitiaFits.Load(zrdr, messages));
    }

    // Three bots are hand-set on the Warhawk so an outlawed pick meets the draw; the rest are Random.
    private static (NetSeat[] Roster, CoopFit[] Fits) Field(NetPlaneRules? rules, Func<byte, string, bool>? arms, int seed,
        INetTransport? wire = null)
    {
        wire ??= LoopbackTransport.Mesh(1, new LoopbackConditions(0.0, 0.0, 0.0), new Random(1))[0];
        var bots = Enumerable.Range(0, 12).Select(i => new VsBotEntry(i < 3 ? Warhawk : null, NetBotSkill.Veteran, 0, "")).ToArray();
        return SeatFields.VersusLaunchField(wire, new[] { "player_fury" }, new LoadoutChoice?[] { null }, Stock, rules,
            bots: bots, draws: new Random(seed), botArms: arms);
    }

    // The field a lobby host launches and each seat's custom build, as NetFlight takes them.
    private static (NetSeat[] Roster, NetPlaneBuild?[] Builds) LobbyField(NetLobby wire, NetPlaneRules rules, int seed)
    {
        var (roster, _) = Field(rules, null, seed, wire);
        return (roster, SeatFields.SeatBuildsFor(roster, Array.Empty<CustomPlaneDef?>(), wire, rules));
    }

    // Each end steps its socket and then its lobby, four times over.
    private static void Settle(NetLobby[] wires, DogfightLobby host, DogfightLobby guest)
    {
        for (int round = 0; round < 4; round++)
        {
            wires[0].Step(0.016);
            host.Step();
            wires[1].Step(0.016);
            guest.Step();
        }
    }

    private static List<string> WeaponsOf(MilitiaFits militias, NetSeat bot) =>
        InstantAction.MilitiaAt(bot.Militia) is { } militia
            ? militias.WeaponsFor(militia.Name, bot.PlaneNode).Select(slot => slot.WeaponId).ToList()
            : new List<string>();

    // Mirrors HumanFlightAdapter.Assemble's reading of a seat's build.
    private static bool CarriesNitro(NetPlaneBuild? build) =>
        CustomPlaneWire.Def(build) is { } custom && CustomPlaneBuild.HasNitrous(custom);

    private static int Row(string id) => Stock.Options.PylonOrdnance.Select(option => option.Id).ToList().IndexOf(id);
}
