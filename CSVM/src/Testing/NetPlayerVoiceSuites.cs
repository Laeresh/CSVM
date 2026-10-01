using System;
using System.Collections.Generic;
using System.Linq;
using CSVM.Extraction;
using CSVM.Flight.Ai;
using CSVM.Flight.Weapons;
using CSVM.Launch;
using CSVM.Net;
using CSVM.Session;
using CSVM.Session.Roster;
using CSVM.Spec;
using CSVM.Utils;
using Godot;

namespace CSVM.Testing;

/// <summary>A network player's chosen pilot voice across the link. Two sessions are launched through
/// both doors, each player on its own Player Information voice. The lines one machine derives for the
/// other machine's player are read off its radio. The rules are the original's per-peer voice arms
/// (docs/formats/combat-voice.md, "A player's own voice").</summary>
internal static class NetPlayerVoiceSuites
{
    private const string MpMission = "MP1";

    private const ulong HostSeed = 0xC0FFEE29UL;

    // Places in the Voice list: Jack for the host, Gruff Male for the guest, and the VO ids they speak as.
    private const int HostVoice = 1;
    private const int GuestVoice = 5;
    private const int HostPilot = 2;
    private const int GuestPilot = 26;

    private const int SettleSteps = 20;

    // A death, the ask and the grant back, on a link that carries each leg in one step.
    private const int GrantSteps = 120;

    private const float QuickRespawn = 0.5f;

    // How far ahead of the guest's nose the host is set for the taunt, inside and outside the range.
    private const float NearAhead = 300f;
    private const float FarAhead = 2000f;

    [Suite("net-player-voice",
        "a lobby Dogfight launched through both doors over a perfect loopback, sound loaded, the host on "
        + "Jack and the guest on Gruff Male: the roster carries each seat's voice to the guest and both "
        + "machines speak each seat as its chosen pilot; the guest's kill of the host is gloated in the "
        + "guest's voice on the host's machine and the host's kill of the guest in the host's voice on "
        + "the guest's, the killer's own machine saying nothing; a guest that crashes cries in its voice "
        + "on the host and not on its own machine; a hostile player within 1695 m taunts off its nose "
        + "and not from 2000 m; and the DI tiers speak only for a player on the local side")]
    internal static void EachPlayerSpeaksInItsOwnVoice(TestContext ctx)
    {
        ctx.RequireData(ctx.PlanesGamezPath, $"planes gamez");
        ctx.RequireData(ctx.ZrdrPath, $"zrdr archive");
        ctx.RequireData(ctx.SoundsPath, $"sound archive (soundsh)");
        string missionZrdr = SessionPaths.MissionZrdr(ctx.DataRoot, ctx.Chapter, MpMission);
        ctx.RequireData(SessionPaths.ChapterTextures(ctx.DataRoot, ctx.Chapter), $"{ctx.Chapter} textures");
        ctx.RequireData(SessionPaths.ChapterGamez(ctx.DataRoot, ctx.Chapter), $"{ctx.Chapter} gamez");
        ctx.RequireData(missionZrdr, $"{ctx.Chapter}/{MpMission} zrdr");
        // ⚠ Not --mute: a muted session loads no sound defs and so builds no combat voice at all.
        var spec = SessionSpec.Parse(new[]
        {
            "--vs", $"--chapter={ctx.Chapter}", $"--mission={MpMission}", "--players=1", "--no-pads",
        });

        var ambient = NetCombatSuites.Ambient.Save();
        var mesh = LoopbackTransport.Mesh(2, LoopbackConditions.Perfect, new Random(2929));
        var hostDoor = new UI.Menu.NetPlayFeature((_, _, _) => mesh[0], (_, _) => mesh[1]);
        hostDoor.Take(new UI.Menu.NetPlayerInfo { Callsign = "Hostile", Voice = HostVoice, GameName = "Voices" }, game: true);
        var guestDoor = new UI.Menu.NetPlayFeature((_, _, _) => mesh[0], (_, _) => mesh[1]);
        guestDoor.Take(new UI.Menu.NetPlayerInfo { Callsign = "Gruff", Voice = GuestVoice }, game: false);
        NetCombatSuites.Ends? host = null;
        NetCombatSuites.Ends? guest = null;
        try
        {
            hostDoor.OpenDogfightHost(1);
            guestDoor.OpenJoin();
            StepDoors(SettleSteps, hostDoor, guestDoor);
            guestDoor.Dogfight?.Show();
            StepDoors(SettleSteps, hostDoor, guestDoor);
            var hostLaunch = hostDoor.BuildLaunch();
            if (!guestDoor.IsDogfightGuest || hostLaunch == null)
            {
                ctx.Check(false, $"the guest joins the host's Dogfight ({guestDoor.Stage})");
                return;
            }

            var planes = new[] { UI.Hangar.PlanePickerRoster.AirframeNode(UI.Menu.CoopGuestPick.StarterAirframe) };
            var (roster, _) = Launcher.VersusLaunchField(hostLaunch.Transport, planes, new LoadoutChoice?[] { null }, StockLoadouts.Load());
            ctx.Check(roster.Length == 2 && roster[0].Voice == UI.Menu.PilotVoices.Wire(HostVoice)
                      && roster[1].Voice == UI.Menu.PilotVoices.Wire(GuestVoice),
                $"the host's field names its own seat's voice and the voice the guest's pick carried ({string.Join(", ", roster.Select(s => s.Voice))})");
            host = NetCombatSuites.Ends.Open(ctx, spec, hostLaunch.Transport, isHost: true, HostSeed, roster,
                UI.Hangar.PlanePickerRoster.StockAirframes);
            for (int i = 0; i < GrantSteps && !guestDoor.DogfightLaunchDue; i++)
            {
                host.Session._PhysicsProcess(GameClock.FixedDt);
                StepDoors(1, hostDoor, guestDoor);
            }

            var guestLaunch = guestDoor.DogfightLaunchDue ? guestDoor.BuildLaunch() : null;
            if (guestLaunch == null)
            {
                ctx.Check(false, $"the guest's door hears the host's opener");
                return;
            }

            guest = NetCombatSuites.Ends.Open(ctx, spec, guestLaunch.Transport, isHost: false, HostSeed + 1, null,
                UI.Hangar.PlanePickerRoster.StockAirframes);
            ctx.Check(host.Built && guest.Built, $"both sessions build ({host.Built}, {guest.Built})");
            if (!host.Built || !guest.Built)
            {
                return;
            }

            var ends = new Flight(host.Session, guest.Session, hostDoor, guestDoor);
            ends.Fly(SettleSteps);
            if (!Voices(ctx, ends))
            {
                return;
            }

            for (int i = 0; i < 600 && ends.Voices.Any(v => v.Now < AiVoiceDispatcher.MuteWindowS); i++)
            {
                ends.Fly(1);
            }

            foreach (var rig in ends.Sessions.SelectMany(s => s.SeatRigs))
            {
                if (rig.Controller is { } pilot)
                {
                    pilot.AutoRespawnAfter = QuickRespawn;
                }
            }

            Kills(ctx, ends);
            Taunts(ctx, ends);
            Distress(ctx, ends);
        }
        finally
        {
            guest?.Close();
            host?.Close();
            guestDoor.Discard();
            hostDoor.Discard();
            ambient.Restore();
        }
    }

    // The roster reached the guest with both voices, and each machine speaks both seats as the
    // chosen pilots. Every speaker's talker roll is made certain, so a line either passes the rules
    // or is refused by them, never by chance.
    private static bool Voices(TestContext ctx, Flight ends)
    {
        ctx.Check(ends.Guest.NetSeats.Count == 2 && ends.Guest.NetSeats[0].Voice == UI.Menu.PilotVoices.Wire(HostVoice)
                  && ends.Guest.NetSeats[1].Voice == UI.Menu.PilotVoices.Wire(GuestVoice),
            $"the guest's copy of the roster carries both seats' voices ({string.Join(", ", ends.Guest.NetSeats.Select(s => s.Voice))})");
        if (ends.Host.AiVoice == null || ends.Guest.AiVoice == null)
        {
            ctx.Check(false, $"both ends build the combat voice");
            return false;
        }

        foreach (var (session, name) in new[] { (ends.Host, "host"), (ends.Guest, "guest") })
        {
            var dispatcher = session.AiVoice!.Dispatcher;
            int? first = dispatcher.Find(session.SeatRigs[0].Controller!.PlayerIndex)?.VoId;
            int? second = dispatcher.Find(session.SeatRigs[1].Controller!.PlayerIndex)?.VoId;
            ctx.Check(first == HostPilot && second == GuestPilot,
                $"the {name}'s machine speaks the host's seat as VO id {HostPilot} and the guest's as {GuestPilot} ({first}, {second})");
            ctx.Check(dispatcher.Speakers.Count == 2 && dispatcher.Speakers.All(s => s.TalkerChance > 0f),
                $"and both players roll the session's talker chance, not zero ({string.Join(", ", dispatcher.Speakers.Select(s => s.TalkerChance.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)))})");
            foreach (var speaker in dispatcher.Speakers)
            {
                speaker.TalkerChance = 1f;
            }
        }

        return true;
    }

    // Each kill is gloated by its killer, in the killer's voice, on the victim's machine alone. A
    // crash with nobody to charge is the dying player's cry on the other machine alone.
    private static void Kills(TestContext ctx, Flight ends)
    {
        ends.HoldTaunts(true);
        var hostSeat = ends.Host.SeatRigs[0].Controller!;
        var guestSeat = ends.Guest.SeatRigs[1].Controller!;
        int hostId = hostSeat.PlayerIndex;
        int guestId = guestSeat.PlayerIndex;

        var (onHost, onGuest) = ends.Listen(() => hostSeat.DebugForceCrash(guestId));
        ctx.Check(onHost.Count(l => l.Trigger == AiVoiceDispatcher.GlAllyDwn && Speaks(l, GuestPilot)) == 1,
            $"the guest's kill of the host is gloated once in the guest's voice on the host's machine ({Shown(onHost)})");
        ctx.Check(onGuest.Count == 0,
            $"ABLE-TO-FAIL CONTROL: the killer's own machine says nothing for its kill ({Shown(onGuest)})");

        (onHost, onGuest) = ends.Listen(() => guestSeat.DebugForceCrash(hostId));
        ctx.Check(onGuest.Count(l => l.Trigger == AiVoiceDispatcher.GlAllyDwn && Speaks(l, HostPilot)) == 1,
            $"the host's kill of the guest is gloated once in the host's voice on the guest's machine ({Shown(onGuest)})");
        ctx.Check(onHost.Count == 0,
            $"and the host's own machine says nothing for it ({Shown(onHost)})");

        (onHost, onGuest) = ends.Listen(() => guestSeat.DebugForceCrash());
        ctx.Check(onHost.Count(l => l.Trigger == AiVoiceDispatcher.DeEnemy && Speaks(l, GuestPilot)) == 1,
            $"a guest crashing with nobody to charge cries DE in its own voice on the host's machine ({Shown(onHost)})");
        ctx.Check(onGuest.Count == 0,
            $"ABLE-TO-FAIL CONTROL: and not on its own machine, which voices no local player's death ({Shown(onGuest)})");
    }

    // The host is set ahead of the guest's copy on the host's machine. From 2000 m the guest says
    // nothing. From 300 m inside its nose cone it taunts TA-FailShk in its own voice.
    private static void Taunts(TestContext ctx, Flight ends)
    {
        var copy = ends.Host.SeatRigs[1].Controller!;
        var hostSeat = ends.Host.SeatRigs[0].Controller!;
        ends.WaitQuiet();
        ctx.Check(copy.InPlay && hostSeat.InPlay, $"both seats fly again on the host's machine ({copy.InPlay}, {hostSeat.InPlay})");
        var said = new List<(string Tag, int Trigger, string Clip)>();
        void Take(string tag, int trigger, string clip) => said.Add((tag, trigger, clip));
        ends.Host.AiVoice!.LinePlayed += Take;
        try
        {
            ends.HoldTaunts(false);
            hostSeat.PlaceHeld(copy.WorldPosition + (copy.NoseDirection * FarAhead), copy.WorldPosition);
            ends.Host.AiVoice.RaiseHumanTaunts();
            ctx.Check(said.Count == 0,
                $"ABLE-TO-FAIL CONTROL: a hostile player {FarAhead:0} m ahead is out of taunt range ({Shown(said)})");
            hostSeat.PlaceHeld(copy.WorldPosition + (copy.NoseDirection * NearAhead), copy.WorldPosition);
            ends.Host.AiVoice.RaiseHumanTaunts();
            ctx.Check(said.Count(l => l.Trigger == AiVoiceDispatcher.TaFailShk && Speaks(l, GuestPilot)) == 1,
                $"a hostile player {NearAhead:0} m ahead of the guest's nose draws its TA-FailShk taunt in the guest's voice ({Shown(said)})");
        }
        finally
        {
            ends.HoldTaunts(true);
            ends.Host.AiVoice.LinePlayed -= Take;
        }
    }

    // A falling hull speaks the DI tiers only for a player on the local side. The guest's copy is
    // hostile to the host in a Dogfight, and set on the host's team it is a friend.
    private static void Distress(TestContext ctx, Flight ends)
    {
        var copy = ends.Host.SeatRigs[1].Controller!;
        int team = copy.Team;
        ends.WaitQuiet();
        var said = new List<(string Tag, int Trigger, string Clip)>();
        void Take(string tag, int trigger, string clip) => said.Add((tag, trigger, clip));
        ends.Host.AiVoice!.LinePlayed += Take;
        try
        {
            ends.Host.AiVoice.TakeRemotePlayerHull(copy, 0.2f);
            ctx.Check(said.Count == 0, $"ABLE-TO-FAIL CONTROL: a hostile player's falling hull says nothing ({Shown(said)})");
            copy.Team = ends.Host.SeatRigs[0].Controller!.Team;
            ends.Host.AiVoice.TakeRemotePlayerHull(copy, 1f);
            ends.Host.AiVoice.TakeRemotePlayerHull(copy, 0.2f);
            ctx.Check(said.Count(l => l.Trigger == AiVoiceDispatcher.DiHighDmg && Speaks(l, GuestPilot)) == 1,
                $"the same fall on the local side speaks DI-HighDmg in the guest's voice ({Shown(said)})");
        }
        finally
        {
            copy.Team = team;
            ends.Host.AiVoice.LinePlayed -= Take;
        }
    }

    private static bool Speaks((string Tag, int Trigger, string Clip) line, int pilot) =>
        line.Clip.Contains($"_id{pilot}_", StringComparison.OrdinalIgnoreCase)
        || line.Clip.EndsWith($"_id{pilot}_random", StringComparison.OrdinalIgnoreCase);

    private static string Shown(IEnumerable<(string Tag, int Trigger, string Clip)> lines) =>
        string.Join("; ", lines.Select(l => $"#{l.Trigger} {l.Clip}"));

    private static void StepDoors(int steps, params UI.Menu.NetPlayFeature[] doors)
    {
        for (int i = 0; i < steps; i++)
        {
            foreach (var door in doors)
            {
                door.Step(GameClock.FixedDt);
            }
        }
    }

    // Both machines of one flight, stepped the way the launcher runs them: each session, then each door.
    private sealed record Flight(GameSession Host, GameSession Guest, UI.Menu.NetPlayFeature HostDoor, UI.Menu.NetPlayFeature GuestDoor)
    {
        public GameSession[] Sessions => new[] { Host, Guest };

        public AiVoiceRuntime[] Voices => new[] { Host.AiVoice!, Guest.AiVoice! };

        public void Fly(int steps)
        {
            for (int i = 0; i < steps; i++)
            {
                Host._PhysicsProcess(GameClock.FixedDt);
                Guest._PhysicsProcess(GameClock.FixedDt);
                StepDoors(1, HostDoor, GuestDoor);
            }
        }

        // What each machine's radio takes while one event plays out, after both seats are flying
        // and every earlier line has left the channel.
        public (List<(string Tag, int Trigger, string Clip)> OnHost, List<(string Tag, int Trigger, string Clip)> OnGuest) Listen(Action act)
        {
            Fly(GrantSteps);
            WaitQuiet();
            var onHost = new List<(string, int, string)>();
            var onGuest = new List<(string, int, string)>();
            void TakeHost(string tag, int trigger, string clip) => onHost.Add((tag, trigger, clip));
            void TakeGuest(string tag, int trigger, string clip) => onGuest.Add((tag, trigger, clip));
            Host.AiVoice!.LinePlayed += TakeHost;
            Guest.AiVoice!.LinePlayed += TakeGuest;
            try
            {
                act();
                Fly(SettleSteps);
            }
            finally
            {
                Host.AiVoice.LinePlayed -= TakeHost;
                Guest.AiVoice.LinePlayed -= TakeGuest;
            }

            return (onHost, onGuest);
        }

        // Until no speaker on either machine holds its radio, so a refusal for talking cannot stand
        // in for a rule.
        public void WaitQuiet()
        {
            for (int i = 0; i < 900 && Voices.Any(v => v.Dispatcher.Speakers.Any(s => v.Dispatcher.IsTalking?.Invoke(s.Id) == true)); i++)
            {
                Fly(1);
            }
        }

        // The taunt pair fires whenever the two seats' own flight brings them near. It is held off
        // while a kill is read, and opened again for the taunt's own reading.
        public void HoldTaunts(bool hold)
        {
            foreach (var speaker in Voices.SelectMany(v => v.Dispatcher.Speakers))
            {
                speaker.NextAllowed[AiVoiceDispatcher.TaFailTail] = hold ? float.MaxValue : 0f;
                speaker.NextAllowed[AiVoiceDispatcher.TaFailShk] = hold ? float.MaxValue : 0f;
            }
        }
    }
}
