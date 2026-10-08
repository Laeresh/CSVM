using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CSVM.Mech3;
using CSVM.Net;
using CSVM.Session;
using CSVM.Session.Campaign;
using CSVM.UI;
using CSVM.UI.Boards;
using CSVM.UI.Menu;
using CSVM.UI.Menu.BuiltIn;
using CSVM.UI.Menu.Original;
using CSVM.Video;

namespace CSVM.Testing;

/// <summary>
/// The campaign films across a co-op link, driven on two Original menu hosts over the in-process
/// loopback. Each end plays through its own recorder, standing in for the launcher's cinema, so a
/// suite decides which frame each film stops on.
/// </summary>
internal static class MenuOriginalCoopFilmSuites
{
    private const float Dt = 1f / 60f;
    private const string Suite = "menu-original-coop-film";
    private const string HostPilot = "Filmed";
    private const string GuestOwnPilot = "Lucy";

    [Suite(Suite,
        "Campaign films across a co-op link: the host's chapter film before the cabin plays on the "
        + "guest too and the host's end of it ends the guest's, the closing film after the last "
        + "mission plays on the guest over the host's debrief and a guest's own skip ends only its "
        + "own film, and a guest still showing a film its host named leaves it for the host's launch "
        + "rather than missing it")]
    internal static void TheCoopFilms(TestContext ctx)
    {
        ctx.RequireData(MenuLayout.PathUnder(ctx.DataRoot), $"decoded menu layout");
        var layout = OriginalAvailability.Load(ctx.DataRoot, out var why);
        ctx.Check(layout != null, $"the install's layout passes the availability check ({why ?? "ok"})");
        if (layout == null)
        {
            return;
        }

        string root = Path.Combine(ctx.ScratchDir, Suite);
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }

        var hostStore = new CampaignProfileStore(Path.Combine(root, "HostProfiles"));
        hostStore.Save(Flown(HostPilot, ChapterCinema.MissionsPerChapter - 1));
        var guestStore = new CampaignProfileStore(Path.Combine(root, "GuestProfiles"));
        guestStore.Save(CampaignProfileDef.NewProfile(GuestOwnPilot));
        guestStore.RecordLastPlayed(GuestOwnPilot);

        var mesh = LoopbackTransport.Mesh(2, LoopbackConditions.Perfect, new Random(34));
        var hostDoor = new NetPlayFeature(
            (_, _, _) => mesh[0],
            (_, _) => throw new InvalidOperationException("the host does not join"),
            new RouterAccess(
                port => new UpnpPortMapResult(UpnpPortMapOutcome.Mapped, port, NetDoorAid.ExternalAddress, "suite"),
                _ => { }));
        var guestDoor = new NetPlayFeature(
            (_, _, _) => throw new InvalidOperationException("a guest does not host"),
            (_, _) => mesh[1]);

        var hostExits = new List<MenuExit>();
        var guestExits = new List<MenuExit>();
        End? host = null;
        End? guest = null;
        string? options = MenuSuiteHost.ScratchOptions(ctx, Suite);
        try
        {
            host = Open(ctx, layout, hostDoor, hostStore, hostExits);
            guest = Open(ctx, layout, guestDoor, guestStore, guestExits);
            if (host == null || guest == null)
            {
                return;
            }

            host.Shell.Campaign.OpenCampaignOver(hostStore, MenuSuiteHost.ScratchPlanes(ctx, Suite));
            host.Shell.Campaign.ShowCabin(HostPilot);
            ctx.Check(host.Chapter.Plays == 0 && host.Shell.Screen == OriginalScreen.CampaignCabin,
                $"a host seated inside a chapter opens the cabin with no film ({host.Chapter.Plays}, {host.Shell.Screen})");
            ClickRow(ctx, host, OriginalCampaignScreen.CoopDoorKey);
            MenuSuiteHost.AnswerNetInfo(host.Shell, key => ClickRow(ctx, host, key), HostPilot);
            AwaitMapping(hostDoor);
            ClickRow(ctx, guest, OriginalShell.MultiplayerKey);
            guest.Door.OpenJoin();
            Pump(host, guest, frames: 6);
            ctx.Check(guest.Door.IsCoopGuest && guest.Shell.Screen == OriginalScreen.CampaignCabin,
                $"the joined guest stands on the host's cabin ({guest.Door.Stage}, {guest.Shell.Screen})");

            ChapterFilm(ctx, host, guest, hostStore);
            ClosingFilm(ctx, host, guest, hostStore);
            LaunchEndsAFilm(ctx, host, guest, hostStore, hostExits, guestExits);
        }
        finally
        {
            host?.Host.Deactivate();
            guest?.Host.Deactivate();
            hostDoor.Discard();
            guestDoor.Discard();
            Godot.Input.MouseMode = Godot.Input.MouseModeEnum.Visible;
            CSVM.Utils.OptionsStore.DirectoryOverride = options;
            MenuSuiteHost.DropScratchPlanes(ctx, Suite);
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    // The host flies the chapter's last mission and returns to its book. RETURN TO CABIN then plays
    // the next chapter's film on both ends, and the host's end of it ends the guest's.
    private static void ChapterFilm(TestContext ctx, End host, End guest, CampaignProfileStore hostStore)
    {
        int seq = ChapterCinema.MissionsPerChapter - 1;
        hostStore.Save(Flown(HostPilot, ChapterCinema.MissionsPerChapter));
        host.Host.Show(new DebriefReturn(HostPilot, seq, MissionWon: true));
        Pump(host, guest, frames: 4);
        ctx.Check(guest.Shell.Screen == OriginalScreen.CampaignScrapbook && guest.Chapter.Plays == 0,
            $"ABLE-TO-FAIL CONTROL: the guest follows the host's debrief and plays no film yet ({guest.Shell.Screen}, {guest.Chapter.Plays})");
        ClickRow(ctx, host, nameof(BoardButton.ReturnToCabin));
        ctx.Check(host.Chapter is { Plays: 1, Running: true, Name: "chap2" },
            $"the host's RETURN TO CABIN plays the second chapter's film ({host.Chapter.Name})");
        Pump(host, guest, frames: 4);
        ctx.Check(guest.Chapter is { Plays: 1, Running: true, Name: "chap2" },
            $"the guest plays the same film while the host's plays ({guest.Chapter.Plays}, {guest.Chapter.Name})");
        ctx.Check(guest.Chapter.Skip == ChapterCinema.Keys,
            $"under the chapter film's own skip presses ({guest.Chapter.Skip})");
        host.Chapter.End();
        Pump(host, guest, frames: 4);
        ctx.Check(host.Shell.Screen == OriginalScreen.CampaignCabin,
            $"the host's film ends onto its cabin ({host.Shell.Screen})");
        ctx.Check(guest.Chapter is { Running: false, Stops: 1 },
            $"and the host's end stops the guest's film ({guest.Chapter.Running}, {guest.Chapter.Stops} stop(s))");
        ctx.Check(guest.Shell.Screen == OriginalScreen.CampaignCabin,
            $"which then stands on the host's cabin ({guest.Shell.Screen})");
        Pump(host, guest, frames: 4);
        ctx.Check(guest.Chapter.Plays == 1, $"the film's end does not play it again ({guest.Chapter.Plays})");
    }

    // The host wins the campaign's last mission: the closing film plays in front of both books. The
    // guest's own skip ends its film alone, and the host's later end stops nothing more on the guest.
    private static void ClosingFilm(TestContext ctx, End host, End guest, CampaignProfileStore hostStore)
    {
        int last = CampaignSequence.MissionCount - 1;
        hostStore.Save(Flown(HostPilot, CampaignSequence.MissionCount));
        host.Host.Show(new DebriefReturn(HostPilot, last, MissionWon: true));
        ctx.Check(host.Closing is { Plays: 1, Running: true, Name: ClosingCinema.Name },
            $"the host's last win plays the closing film ({host.Closing.Name})");
        Pump(host, guest, frames: 4);
        ctx.Check(guest.Closing is { Plays: 1, Running: true, Name: ClosingCinema.Name },
            $"the guest plays it too ({guest.Closing.Plays}, {guest.Closing.Name})");
        ctx.Check(guest.Closing.Skip == ClosingCinema.Keys,
            $"under the closing film's own skip presses ({guest.Closing.Skip})");
        ctx.Check(guest.Shell.Screen == OriginalScreen.CampaignScrapbook,
            $"with the host's debrief behind it ({guest.Shell.Screen})");

        guest.Closing.End();
        Pump(host, guest, frames: 4);
        ctx.Check(!guest.Closing.Running && host.Closing.Running && host.Door.HostFlow.FilmShown != null,
            $"the guest's own skip ends its film and leaves the host's playing ({guest.Closing.Running}, {host.Closing.Running})");
        host.Closing.End();
        Pump(host, guest, frames: 4);
        ctx.Check(host.Shell.Screen == OriginalScreen.CampaignScrapbook && host.Door.HostFlow.FilmShown == null,
            $"the host's film ends onto its book ({host.Shell.Screen})");
        ctx.Check(guest.Closing is { Plays: 1, Stops: 0 },
            $"and its end finds no film on the guest to stop ({guest.Closing.Plays} play(s), {guest.Closing.Stops} stop(s))");
    }

    // A guest still showing a film its host named when that host launches. The host's own film
    // cannot outlast the launch, so the word is sent bare, as one whose end never reached the guest.
    private static void LaunchEndsAFilm(
        TestContext ctx, End host, End guest, CampaignProfileStore hostStore, List<MenuExit> hostExits, List<MenuExit> guestExits)
    {
        hostStore.Save(Flown(HostPilot, ChapterCinema.MissionsPerChapter + 1));
        host.Shell.Campaign.ShowCabin(HostPilot);
        host.Door.ShowCoopFilm(NetCoopFilm.Chapter, 3);
        Pump(host, guest, frames: 4);
        ctx.Check(guest.Chapter is { Running: true, Name: "chap3" },
            $"the guest plays the film its host named ({guest.Chapter.Name})");

        ClickRow(ctx, host, nameof(BoardButton.NextMission));
        Pump(host, guest, frames: 3);
        ClickRow(ctx, host, nameof(BoardButton.GoToFlightCheck));
        Pump(host, guest, frames: 3);
        ctx.Check(guest.Shell.Screen == OriginalScreen.CampaignFlightCheck && guest.Chapter.Running,
            $"the guest's board follows the host's check behind its film ({guest.Shell.Screen})");

        // The film holds the guest's presses, so its Ready is given at the door.
        guest.Door.Pick.Set(guest.Door.Pick.Airframe, true, guest.Door.Pick.Fit);
        Pump(host, guest, frames: 4);
        ClickRow(ctx, host, nameof(BoardButton.FlyMission));
        if (hostExits.LastOrDefault() is not CampaignMissionExit { Net: { } wire })
        {
            ctx.Check(false, $"the host flies once the guest is Ready ({hostExits.Count} exit(s), all Ready {host.Door.CoopAllReady})");
            return;
        }

        var own = new[] { Flight.Hangar.StockAirframes.Node(CoopGuestPick.StarterAirframe) };
        var (roster, _) = CSVM.Launch.Launcher.CoopLaunchField(
            host.Door, wire.Transport, own, Array.Empty<Flight.Weapons.LoadoutChoice?>(), Flight.Weapons.StockLoadouts.Load());
        _ = NetSession.Host((NetLobby)wire.Transport, roster, 7UL);
        int before = guestExits.Count;
        for (int frame = 0; frame < 8 && guestExits.Count == before; frame++)
        {
            wire.Transport.Step(Dt);
            host.Door.Step(Dt);
            guest.Host.Tick(Dt);
        }

        ctx.Check(guestExits.Skip(before).OfType<CampaignMissionExit>().Any(),
            $"the guest launches behind its host with the film still up ({guestExits.Count - before} exit(s))");
        ctx.Check(guest.Chapter is { Running: false, Stops: 2 },
            $"and the launch ends the guest's film rather than flying under it ({guest.Chapter.Running}, {guest.Chapter.Stops} stop(s))");
    }

    // A profile that has completed its first flown missions, written through the progression rules
    // the way a mission director writes one.
    private static CampaignProfileDef Flown(string name, int flown)
    {
        var profile = CampaignProfileDef.NewProfile(name);
        for (int seq = 0; seq < flown; seq++)
        {
            CampaignProgression.Record(profile, new MissionAttempt(
                seq, CampaignProgression.PrimaryObjectiveMask, 300_000, 400, 120,
                profile.Planes[0].Airframe, profile.Planes[0].Name));
        }

        return profile;
    }

    private static End? Open(TestContext ctx, MenuLayout layout, NetPlayFeature door, CampaignProfileStore profiles, List<MenuExit> exits)
    {
        var seat = new ScriptedSeat();
        var registry = new PresentationRegistry();
        registry.Register(PresentationId.BuiltIn, () => new BuiltInPresentation(
            ctx.Host, ctx.ZrdrPath, ctx.DataRoot, string.Empty, new MenuInput { Keyboard = true }));
        registry.Register(PresentationId.Original, () => new OriginalPresentation(
            ctx.Host, ctx.DataRoot, layout, string.Empty, new MenuInput { Keyboard = true })
        {
            CampaignProfiles = profiles,
        });
        var chapter = new FilmRecorder();
        var closing = new FilmRecorder();
        var menu = new MenuHost(registry, new MenuSuiteHost.SilentMenuAudio(), exits.Add);
        MenuSuiteHost.AddFeatures(menu, ctx.DataRoot,
            chapterCinema: new ChapterCinema(chapter.Play, chapter.Stop),
            closingCinema: new ClosingCinema(closing.Play, closing.Stop),
            netDoor: door);
        menu.AddSeat(seat);
        menu.Select(forceBuiltIn: false, cliOverride: "original");
        menu.Show(MenuReturnDestination.TopLevel);
        var shell = (menu.Active as OriginalPresentation)?.Shell;
        ctx.Check(shell is { Screen: OriginalScreen.TopLevel }, $"each end shows Original on the top level ({shell?.Screen})");
        if (shell == null)
        {
            menu.Deactivate();
            return null;
        }

        return new End(menu, seat, shell, chapter, closing);
    }

    // The mapping lands on a worker thread, so the wait is on the wall clock rather than a count.
    private static void AwaitMapping(NetPlayFeature door)
    {
        var waited = System.Diagnostics.Stopwatch.StartNew();
        while (door.Router.PortMap == null && waited.Elapsed.TotalSeconds < 20.0)
        {
            door.Step(0.0);
            System.Threading.Thread.Sleep(1);
        }
    }

    private static void Pump(End host, End guest, int frames)
    {
        for (int i = 0; i < frames; i++)
        {
            host.Host.Tick(Dt);
            guest.Host.Tick(Dt);
        }
    }

    // A pointer click on the row under that key, read fresh: the press arms it and the release fires.
    private static void ClickRow(TestContext ctx, End end, string key)
    {
        var row = end.Shell.Rows.FirstOrDefault(r => r.Key == key);
        ctx.Check(row != null, $"the showing screen carries {key} ({end.Shell.Screen})");
        if (row == null)
        {
            return;
        }

        var size = ctx.Host.GetViewport().GetVisibleRect().Size;
        var fit = BoardFit.For(size.X, size.Y);
        float x = fit.X(row.X + Math.Min(5f, row.Width / 2f));
        float y = fit.Y(row.Y + Math.Min(5f, row.Height / 2f));
        end.Seat.Enqueue(new MenuCommands { Pointer = new MenuPointer(x, y, true, true, 0) });
        end.Host.Tick(Dt);
        end.Seat.Enqueue(new MenuCommands { Pointer = new MenuPointer(x, y, false, false, 0) });
        end.Host.Tick(Dt);
    }

    private sealed record End(MenuHost Host, ScriptedSeat Seat, OriginalShell Shell, FilmRecorder Chapter, FilmRecorder Closing)
    {
        public NetPlayFeature Door => Host.Features.Get<NetPlayFeature>();
    }

    // The stand-in for the launcher's cinema: it records what it was asked to play and when it was
    // told to stop. End is the film stopping on its own side, played out or skipped by its viewer.
    private sealed class FilmRecorder
    {
        private Action? _then;

        public string? Name { get; private set; }

        public CinemaSkip Skip { get; private set; }

        public int Plays { get; private set; }

        public int Stops { get; private set; }

        public bool Running { get; private set; }

        public void Play(string name, Action then, CinemaSkip skip)
        {
            Name = name;
            Skip = skip;
            Plays++;
            Running = true;
            _then = then;
        }

        public void Stop()
        {
            Stops++;
            End();
        }

        public void End()
        {
            if (!Running)
            {
                return;
            }

            Running = false;
            _then?.Invoke();
        }
    }

    private sealed class ScriptedSeat : IMenuInputSource
    {
        private readonly Queue<MenuCommands> _frames = new();

        public string DeviceLabel => "scripted";

        public bool CapturingText { get; set; }

        public void Enqueue(MenuCommands frame) => _frames.Enqueue(frame);

        public MenuCommands Poll(float dt) => _frames.Count > 0 ? _frames.Dequeue() : MenuCommands.None;

        public void Prime()
        {
        }
    }
}
