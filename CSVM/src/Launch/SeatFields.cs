using System.Collections.Generic;
using CSVM.Flight.Weapons;
using CSVM.Mech3;
using CSVM.Session.Campaign;
using CSVM.Spec;
using CSVM.Utils;

namespace CSVM.Launch;

/// <summary>
/// Who sits where at a launch, flying what. Each builder is one host's roster (co-op, lobby
/// Dogfight, command line, or a local match with bots), with each seat's fit and plane. They are
/// pure rules over the door, the wire and the spec. A suite builds a field without a launcher.
/// The flight's own copy of what they build is <see cref="NetFlight"/>'s.
/// Module notes: docs/architecture/Launch.md.
/// </summary>
internal static class SeatFields
{
    /// <summary>The fit a co-op seat flown elsewhere carries: from <paramref name="launched"/> on
    /// the host that launched it, or the host's word through <paramref name="door"/> on a guest.
    /// Stock where neither names one.</summary>
    internal static LoadoutChoice? CoopSeatFitFor(int seat, IReadOnlyList<Net.CoopFit>? launched,
        UI.Menu.NetPlayFeature? door, StockLoadouts stock)
    {
        var fit = launched != null
            ? seat >= 0 && seat < launched.Count ? launched[seat] : default
            : door?.CoopSeatFits.TryGetValue(seat, out var told) == true ? told : default;
        return CampaignLoadout.For(fit, stock);
    }

    /// <summary>The campaign wingman a co-op host's launch names to its guests, read off
    /// <paramref name="profile"/> as the host's own director reads it. A launch with no profile
    /// flies the fresh profile a director without one binds.</summary>
    internal static Net.CoopWingmanMessage CoopWingmanFor(string profile, string? profilesDir)
    {
        var def = profile.Length == 0
            ? CampaignProfileDef.NewProfile(CampaignDirector.CoopGuestPilot)
            : CampaignProfileStore.ForSession(profilesDir).Load(profile);
        if (def == null)
        {
            Log.Warn("core", $"net: co-op profile '{profile}' cannot be read, so no wingman aeroplane is named to the guests");
            return new Net.CoopWingmanMessage(Net.CoopWingmanMessage.NoAirframe, default);
        }

        return CampaignDirector.CoopWingmanOf(def);
    }

    /// <summary>A co-op host's field and each seat's fit, by seat. Its own seats come first, with
    /// the fits its launch carried, the first named by the door's callsign. Then comes every seat a
    /// guest still on the wire was given, in the plane, fit and name its pick carried. A machine's
    /// seats sit side by side.</summary>
    internal static (Net.NetSeat[] Roster, Net.CoopFit[] SeatFits) CoopLaunchField(
        UI.Menu.NetPlayFeature door, Net.INetTransport wire, IReadOnlyList<string> planes,
        IReadOnlyList<LoadoutChoice?> fits, StockLoadouts stock)
    {
        var guests = new List<(int Peer, string Plane, string Name)>();
        var voices = new List<byte>();
        var seatFits = new List<Net.CoopFit>();
        for (int i = 0; i < planes.Count; i++)
        {
            seatFits.Add(CampaignLoadout.FitOf(i < fits.Count ? fits[i] : null, stock));
        }

        foreach (var guest in door.CoopGuests)
        {
            if (System.Linq.Enumerable.Contains(wire.Peers, guest.Peer))
            {
                guests.Add((guest.Peer, Flight.Hangar.StockAirframes.Node(guest.Airframe), guest.Name));
                voices.Add(UI.Menu.PilotVoices.Wire(guest.Voice));
                seatFits.Add(guest.Fit);
            }
        }

        string hostName = Net.SeatRosterMessage.Carried(door.Identity.PlayerName.Trim()).Trim();
        var roster = Net.NetSeats.CoopField(wire.LocalPeer, planes, guests, hostName);
        // The host's first seat is the scripted player and speaks as Nathan Zachary. Its splitscreen
        // seats have no voice, and each guest speaks in the voice its pick carried.
        for (int seat = 0; seat < roster.Length; seat++)
        {
            int guest = seat - planes.Count;
            byte voice = seat == 0 ? UI.Menu.PilotVoices.Wire(UI.Menu.PilotVoices.CoopHost)
                : guest >= 0 && guest < voices.Count ? voices[guest] : (byte)0;
            roster[seat] = roster[seat] with { Voice = voice };
        }

        return (roster, seatFits.ToArray());
    }

    /// <summary>A network Dogfight host's field and each seat's fit, by seat. Its own seats come
    /// first, the first named by the wire's local callsign and any other by player tag. Each guest
    /// follows in the stock airframe, fit and name its lobby pick carried.
    /// ⚠ A guest with no pick on the wire flies the host's first airframe on the stock fit. That is
    /// the Built-in Dogfight door's only rule. Each seat takes its machine's lobby team from
    /// <paramref name="teamOf"/>, by peer; none leaves every seat on 0.</summary>
    internal static (Net.NetSeat[] Roster, Net.CoopFit[] SeatFits) VersusLaunchField(
        Net.INetTransport wire, IReadOnlyList<string> planes, IReadOnlyList<LoadoutChoice?> fits, StockLoadouts stock,
        Net.NetPlaneRules? rules = null, System.Func<int, byte>? teamOf = null,
        IReadOnlyList<VsBotEntry>? bots = null, IReadOnlyList<string>? callsignPool = null, System.Random? draws = null)
    {
        teamOf ??= _ => 0;
        var seats = new List<Net.NetSeat>(planes.Count + wire.Peers.Count);
        var seatFits = new List<Net.CoopFit>(seats.Capacity);
        var lobby = wire as Net.NetLobby;
        // Cut to the roster's width, so the host's kill lines read what each guest's copy reads.
        string hostName = Net.SeatRosterMessage.Carried((lobby?.LocalCallsign ?? "").Trim()).Trim();
        for (int i = 0; i < planes.Count; i++)
        {
            seats.Add(new Net.NetSeat
            {
                PeerId = wire.LocalPeer,
                SeatIndex = seats.Count,
                TeamId = teamOf(wire.LocalPeer),
                FlownHere = true,
                Callsign = i == 0 && hostName.Length > 0 ? hostName : UI.Boards.SplitScreen.PlayerTag(i),
                Unnamed = i == 0 && hostName.Length == 0,
                PlaneNode = planes[i],
                // Only the first seat has a Player Information answer; a splitscreen seat has none.
                Voice = i == 0 && lobby != null ? lobby.LocalVoice : (byte)0,
            });
            seatFits.Add(CampaignLoadout.FitOf(i < fits.Count ? fits[i] : null, stock));
        }

        var picks = lobby?.Picks;
        foreach (int peer in wire.Peers)
        {
            if (seats.Count >= Net.NetSeats.MaxPlayers)
            {
                break;
            }

            Net.CoopPickMessage chosen = default;
            bool picked = picks != null && picks.TryGetValue(peer, out chosen);
            string name = picked ? chosen.Name.Trim() : "";
            seats.Add(new Net.NetSeat
            {
                PeerId = peer,
                SeatIndex = seats.Count,
                TeamId = teamOf(peer),
                Callsign = name.Length > 0 ? name : $"guest {peer.ToString(System.Globalization.CultureInfo.InvariantCulture)}",
                Unnamed = name.Length == 0,
                PlaneNode = picked ? Flight.Hangar.StockAirframes.Node(chosen.Airframe) : planes[0],
                Voice = picked ? chosen.Voice : (byte)0,
            });
            // The guest's own lobby flies its pick through the same rules, so both ends agree.
            seatFits.Add(picked ? rules?.Enforce(chosen.Fit) ?? chosen.Fit : default);
        }

        // The lobby's bots follow every guest on the stock fit. Random planes and missing callsigns
        // are drawn here, on the host alone, so a guest reads a real plane off the roster.
        if (bots is { Count: > 0 })
        {
            var resolved = Session.Roster.BotSeats.Resolve(bots, System.Linq.Enumerable.Select(seats, seat => seat.Callsign),
                callsignPool ?? System.Array.Empty<string>(), draws ?? new System.Random(Rng.IntSeedFor(Rng.BotField)));
            int people = seats.Count;
            int left = Net.NetSeats.AddBots(seats, wire.LocalPeer, resolved);
            for (int seat = people; seat < seats.Count; seat++)
            {
                seatFits.Add(default);
            }

            WarnBotsLeftOut(left, "lobby bot(s)", "people");
        }

        Net.NetSeats.Validate(seats, wire.LocalPeer);
        return (seats.ToArray(), seatFits.ToArray());
    }

    /// <summary>Each seat's custom plane, by seat, null for a stock one. This machine's seats take
    /// <paramref name="customs"/> in menu order. A guest's seat takes the build its lobby pick sent
    /// when <paramref name="rules"/> admit it. Without rules a guest's pick seats no build; a co-op
    /// guest's comes from its host's hangar instead (<see cref="CoopSeatBuilds"/>).
    /// </summary>
    internal static Net.NetPlaneBuild?[] SeatBuildsFor(IReadOnlyList<Net.NetSeat> roster,
        IReadOnlyList<Flight.Hangar.CustomPlaneDef?> customs, Net.INetTransport wire, Net.NetPlaneRules? rules)
    {
        var builds = new Net.NetPlaneBuild?[roster.Count];
        var picks = (wire as Net.NetLobby)?.PickBuilds;
        for (int seat = 0; seat < roster.Count; seat++)
        {
            if (roster[seat].HasPane)
            {
                int menu = Net.NetSeats.LocalOrdinal(roster, seat);
                builds[seat] = menu >= 0 && menu < customs.Count ? Flight.Hangar.CustomPlaneWire.Build(customs[menu]) : null;
                continue;
            }

            // A bot flies a stock plane, and its peer is the host's, whose picks name no bot.
            if (roster[seat].IsBot || rules is not { } admitting || picks == null || !picks.TryGetValue(roster[seat].PeerId, out var build))
            {
                continue;
            }

            // Unreachable from a lobby, which launches only on admitted planes; the log names the case.
            var refusal = admitting.Refuses(build.Airframe, build);
            if (refusal != Net.PlaneRefusal.None)
            {
                Log.Warn("core", $"net: seat {seat.ToString(System.Globalization.CultureInfo.InvariantCulture)}'s custom plane '{build.Name}' is refused ({refusal}), so it flies stock");
                continue;
            }

            builds[seat] = build;
        }

        return builds;
    }

    /// <summary>A co-op host's custom planes, by seat, null for a stock one. Its own seats take
    /// <paramref name="customs"/> in menu order. Each guest's seat takes the build of the hangar
    /// plane it flies, never one the guest brought. A guest's seats are matched in order, so each
    /// of a machine's players flies its own plane.</summary>
    internal static Net.NetPlaneBuild?[] CoopSeatBuilds(IReadOnlyList<Net.NetSeat> roster,
        IReadOnlyList<Flight.Hangar.CustomPlaneDef?> customs, UI.Menu.NetPlayFeature door, Net.INetTransport wire)
    {
        var builds = SeatBuildsFor(roster, customs, wire, null);
        var guests = door.CoopGuests;
        for (int seat = 0; seat < roster.Count; seat++)
        {
            if (roster[seat].FlownHere)
            {
                continue;
            }

            int local = 0;
            for (int earlier = seat - 1; earlier >= 0 && roster[earlier].PeerId == roster[seat].PeerId; earlier--)
            {
                local++;
            }

            foreach (var guest in guests)
            {
                if (guest.Peer == roster[seat].PeerId && guest.Local == local)
                {
                    builds[seat] = guest.Build;
                }
            }
        }

        return builds;
    }

    /// <summary>The custom plane a seat flown elsewhere carries: from <paramref name="launched"/> on
    /// the host that launched it, or the host's word through <paramref name="door"/> on a guest.
    /// Null for a stock seat.</summary>
    internal static Flight.Hangar.CustomPlaneDef? SeatBuildFor(int seat, IReadOnlyList<Net.NetPlaneBuild?>? launched,
        UI.Menu.NetPlayFeature? door)
    {
        var build = launched != null
            ? seat >= 0 && seat < launched.Count ? launched[seat] : null
            : door?.SeatBuilds.TryGetValue(seat, out var told) == true ? told : null;
        return Flight.Hangar.CustomPlaneWire.Def(build);
    }

    /// <summary>A lobby's team names by team number, the form a session reads them in.</summary>
    internal static Dictionary<int, string> TeamNames(IReadOnlyList<Net.LobbyTeamName> teams)
    {
        var names = new Dictionary<int, string>();
        foreach (var team in teams)
        {
            names[team.Number] = team.Name;
        }

        return names;
    }

    /// <summary>A local Dogfight's seat roster: its panes, then the spec's bots (the command line's or
    /// the join board's), all flown here with no wire. Null without bots, so such a match keeps its
    /// panes as its seats. Random planes and blank callsigns are drawn as a host draws them.</summary>
    internal static Net.NetSeat[]? LocalVersusField(SessionSpec spec, string messagesPath)
    {
        if (!spec.Versus || spec.VsBots.Count == 0)
        {
            return null;
        }

        var seats = Net.NetSeats.LocalPanes(spec.Players);
        int left = Net.NetSeats.AddBots(seats, Net.NetSeats.OfflinePeer, ResolveBots(spec, seats, messagesPath));
        Net.NetSeats.Validate(seats, Net.NetSeats.OfflinePeer);
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        Log.Info("core", $"local roster of {seats.Count} seat(s), {seats.Count - spec.Players} of them bots{(left > 0 ? $", {left.ToString(inv)} left out of the {Net.NetSeats.MaxPlayers.ToString(inv)}-seat field" : "")}");
        LogBotSeats("local", seats);
        return seats.ToArray();
    }

    /// <summary>A command-line host's roster: this machine's own seats, then one per peer that got
    /// in, then the spec's bots. The remote seats fly the local pilot's airframe, the same limit a
    /// menu host has.</summary>
    internal static Net.NetSeat[] CliHostField(SessionSpec spec, Net.INetTransport wire, string messagesPath)
    {
        var seats = new List<Net.NetSeat>();
        for (int i = 0; i < spec.Players && seats.Count < Net.NetSeats.MaxPlayers; i++)
        {
            seats.Add(new Net.NetSeat
            {
                PeerId = wire.LocalPeer,
                SeatIndex = seats.Count,
                FlownHere = true,
                Callsign = UI.Boards.SplitScreen.PlayerTag(i),
                Unnamed = i == 0,
                PlaneNode = i < spec.PlaneNames.Count ? spec.PlaneNames[i] : spec.PlaneName,
            });
        }

        foreach (int peer in wire.Peers)
        {
            if (seats.Count >= Net.NetSeats.MaxPlayers)
            {
                break;
            }

            seats.Add(new Net.NetSeat
            {
                PeerId = peer,
                SeatIndex = seats.Count,
                Callsign = $"guest {peer.ToString(System.Globalization.CultureInfo.InvariantCulture)}",
                Unnamed = true,
                PlaneNode = spec.PlaneName,
            });
        }

        // The command line's bots, after every guest. Random planes and callsigns are drawn here,
        // on the host alone. The roster carries real ones to every guest, which seats no bot.
        int left = Net.NetSeats.AddBots(seats, wire.LocalPeer, ResolveBots(spec, seats, messagesPath));
        WarnBotsLeftOut(left, "bot(s)", "guests");
        Net.NetSeats.Validate(seats, wire.LocalPeer);
        Log.Info("core", $"net: host roster of {seats.Count} seat(s), {System.Linq.Enumerable.Count(seats, s => s.IsBot)} of them bots");
        LogBotSeats("net", seats);
        return seats.ToArray();
    }

    /// <summary>The spec's bots resolved for a field whose people are
    /// <paramref name="people"/>. The pilot names are read once from the message table, which
    /// nothing has loaded before the session builds; a missing table seats "Bot n". The draws take
    /// a stream of their own, a function of the master seed alone.</summary>
    internal static IReadOnlyList<Net.SeatedBot> ResolveBots(
        SessionSpec spec, IReadOnlyList<Net.NetSeat> people, string messagesPath)
    {
        if (spec.VsBots.Count == 0)
        {
            return System.Array.Empty<Net.SeatedBot>();
        }

        var pool = Session.Roster.BotSeats.CallsignPool(Messages.Load(messagesPath));
        return Session.Roster.BotSeats.Resolve(spec.VsBots,
            System.Linq.Enumerable.Select(people, seat => seat.Callsign), pool,
            new System.Random(Rng.IntSeedFor(Rng.BotField)));
    }

    // One line per bot seat of a roster just built, prefixed by where it was built.
    private static void LogBotSeats(string where, IReadOnlyList<Net.NetSeat> seats)
    {
        foreach (var bot in seats)
        {
            if (bot.IsBot)
            {
                Log.Info("core", $"{where}: bot seat {bot.SeatIndex} '{bot.Callsign}' flies {bot.PlaneNode} at {bot.Skill.ToString().ToLowerInvariant()}");
            }
        }
    }

    // The warning for the bots a full field left out once the people named took their seats.
    private static void WarnBotsLeftOut(int left, string bots, string people)
    {
        if (left > 0)
        {
            Log.Warn("core", $"net: {left} {bots} left out, the {people} filled the {Net.NetSeats.MaxPlayers}-seat field");
        }
    }
}
