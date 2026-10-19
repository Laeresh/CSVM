using System;
using System.Collections.Generic;
using CSVM.Net;
using CSVM.Spec;
using CSVM.UI.Boards;
using CSVM.UI.Screens;

namespace CSVM.UI.Menu.Original;

/// <summary>
/// The seat rules every Original screen shares: joining (<see cref="JoiningOpen"/>), a later
/// seat's frame, and the seat strip over the campaign and Instant Action. The two local
/// match screens behind the Free Flight and Dogfight doors are <see cref="OriginalLocalLobby"/>.
/// </summary>
public sealed partial class OriginalShell
{
    /// <summary>The Dogfight door's key on the top level.</summary>
    public const string DogfightKey = "DOGFIGHT";

    // The Dogfight door under the Free Flight door, one plaque plus air below it.
    private const float DogfightDoorY = DoorY + 36f;

    // A remote guest's chip spans two pitches, since its tag carries the network mark.
    private const int RemoteChipPitches = 2;

    // A Ready chip's word is longer than the network mark and would wrap in two pitches.
    private const int ReadyChipPitches = 3;

    // The focused chip's lit cell over the strip's ground: which seat's flight check is the one on
    // show.
    private const byte CampaignStripLit = 0xFF;
    private const float CampaignStripLitAlpha = 0.22f;

    // The strip's ground: dark under every chip, since the four identity colours are light. The
    // Instant Action paper needs a more opaque plate than a painted panel does to hold them.
    private const float CampaignStripGround = 0.45f;
    private const float CampaignStripGroundOnPaper = 0.72f;

    // A Ready chip's cell, a green wash over the dark ground so the mark reads before its word.
    private const byte CampaignStripReadyRed = 0x30;
    private const byte CampaignStripReadyGreen = 0xB0;
    private const byte CampaignStripReadyBlue = 0x40;
    private const float CampaignStripReadyAlpha = 0.45f;

    /// <summary>Whether the screen showing is where a pad signs onto a seat, which is the join
    /// board and nowhere else (<see cref="OriginalJoinBoard"/>). Every other screen reads the
    /// roster the board wrote. Who is flying is settled by players saying so, not by whichever
    /// flight screen happened to open.</summary>
    public bool JoiningOpen => _screen == OriginalScreen.JoinBoard;

    // The strip's focused line: the seat picking on the per-seat screen, else the seat whose flight
    // check shows, else none. The ammo and plane screens the check opens name no seat of their own,
    // their strip standing for the campaign rather than for a check.
    private int StripFocus =>
        _screen == OriginalScreen.SeatPlane ? PickingSeat
        : _screen == OriginalScreen.CampaignFlightCheck ? Campaign.CheckSeat
        : -1;

    private PlayerSeat? Seat0 => _setup.Seats.Count > 0 ? _setup.Seats[0] : null;

    /// <summary>Applies one frame of one seat's commands. A seat drives its walk's screens while it
    /// is the one picking, the per-seat aircraft screen and the Weapon Loadout opened from it, and
    /// its own check on the campaign, the check and the ammo and plane screens it opens; seat 0
    /// drives every other screen, its frame reduced to its pointer on a screen standing for another
    /// seat (see <see cref="SeatZeroFrame"/>). A later seat anywhere else can only leave with Back,
    /// from any screen, as a guest may.</summary>
    public OriginalStep StepSeat(int index, MenuCommands commands)
    {
        ArgumentNullException.ThrowIfNull(commands);
        if (index == 0)
        {
            return ApplyFrame(SeatZeroFrame(commands));
        }

        var seats = _setup.Seats;
        if (index < 0 || index >= seats.Count)
        {
            return new OriginalStep(Array.Empty<string>(), null, false);
        }

        var seat = seats[index];
        bool own = OnSeatWalk ? ReferenceEquals(seat, _pickingSeat) : Campaign.CheckSeat == index;
        if (own)
        {
            return ApplyFrame(commands);
        }

        bool changed = commands.Back && _setup.Unjoin(seat);
        return new OriginalStep(Array.Empty<string>(), null, changed);
    }

    // A player with a callsign is chipped by it, the chip widened a pitch for every three
    // characters past the player tag's two.
    private static CampaignChip Chip(int seat, bool own, bool ready, string callsign = "")
    {
        string mark = ready ? SeatStrip.ReadyMark : own ? "" : SeatStrip.RemoteMark;
        int pitches = ready ? ReadyChipPitches : mark.Length > 0 ? RemoteChipPitches : 1;
        string name = callsign.Trim();
        if (name.Length == 0)
        {
            return new CampaignChip(SplitScreen.PlayerTag(seat) + mark, seat, pitches, own, ready);
        }

        pitches += (Math.Max(0, name.Length - 2) + 2) / 3;
        return new CampaignChip(name + mark, seat, pitches, own, ready);
    }

    // The chip row in the Original presentation's own space, drawn over a campaign board only once
    // a second seat has joined. A solo campaign shows the authored screen alone. No device and no
    // pick status: the picks are the flight field's, and the local lobby's list names devices.
    // ⚠ Pass onPaper on the Instant Action screen. Its background is light, and a chip's colour
    // reads only over a dark ground.
    private BoardPanel? CampaignSeatPanel(bool onPaper = false)
    {
        var chips = CampaignChips();
        if (chips.Count < 2)
        {
            return null;
        }

        // A co-op guest's checks count its own players, and its chips the host's whole field.
        int current = StripFocus;
        if (current >= 0 && Campaign.IsGuest && _net is { IsCoopGuest: true, CoopFlow: { } flow })
        {
            current += flow.Slot;
        }

        int pitches = 0;
        foreach (var chip in chips)
        {
            pitches += chip.Pitches;
        }

        float width = pitches * SeatStrip.Pitch;
        float left = BoardFit.AuthoredWidth - SeatStrip.Inset - width;
        float top = SeatStrip.Inset - SeatStrip.Pad;
        var fills = new List<BoardFill>(2)
        {
            new BoardFill(left - SeatStrip.Pad, top, width + (2f * SeatStrip.Pad), SeatStrip.Ground,
                0, 0, 0, onPaper ? CampaignStripGroundOnPaper : CampaignStripGround),
        };

        var lines = new List<BoardLine>(chips.Count);
        float x = left;
        for (int i = 0; i < chips.Count; i++)
        {
            var chip = chips[i];
            float chipWidth = chip.Pitches * SeatStrip.Pitch;
            if (chip.Ready)
            {
                fills.Add(new BoardFill(x, top, chipWidth, SeatStrip.Ground, CampaignStripReadyRed, CampaignStripReadyGreen,
                    CampaignStripReadyBlue, CampaignStripReadyAlpha));
            }
            else if (chip.Local && chip.Seat == current)
            {
                fills.Add(new BoardFill(x, top, chipWidth, SeatStrip.Ground,
                    CampaignStripLit, CampaignStripLit, CampaignStripLit, CampaignStripLitAlpha));
            }

            lines.Add(new BoardLine(chip.Text, x, SeatStrip.Inset, chipWidth,
                SeatStrip.Font, SeatStrip.Ink(chip.Seat), -1, false, BoardJustify.Center));
            x += chipWidth;
        }

        return new BoardPanel(fills, Array.Empty<BoardPicture>(), lines);
    }

    // The campaign strip's chips, player 1 first. A human at another machine takes a chip but no
    // pane, and its chip says so. In co-op a guest's chip says Ready instead once it is, as the
    // original lobby's list marks a player. A co-op guest's own strip is the host's field.
    private List<CampaignChip> CampaignChips()
    {
        var chips = new List<CampaignChip>(NetSeats.MaxPlayers);
        if (Campaign.IsGuest && _net is { IsCoopGuest: true, CoopFlow: { } flow } guest)
        {
            for (int slot = 0; slot < Math.Min((int)flow.Humans, NetSeats.MaxPlayers); slot++)
            {
                int local = slot - flow.Slot;
                bool own = local >= 0 && local < guest.CoopSeats;
                bool ready = own ? guest.CoopReadyAt(local) : flow.IsReady(slot);
                chips.Add(Chip(slot, own, ready, guest.CoopSeatName(slot)));
            }

            return chips;
        }

        // A co-op host's own first seat goes by its callsign, as a lobby host's row does. A player
        // its cap sits out has no chip.
        var seats = _setup.Seats;
        int flown = _net is { IsCoopHost: true } coopHost ? Math.Min(seats.Count, coopHost.CoopSeats) : seats.Count;
        for (int i = 0; i < flown; i++)
        {
            chips.Add(Chip(i, true, false, _net is { IsCoopHost: true } open ? open.CoopSeatName(i) : ""));
        }

        if (_net is { IsCoopHost: true } host)
        {
            foreach (var coop in host.CoopGuests)
            {
                if (chips.Count < NetSeats.MaxPlayers)
                {
                    chips.Add(Chip(chips.Count, false, coop.Ready, coop.Name));
                }
            }
        }

        return chips;
    }

    // One chip on the campaign strip: its words, its colour and how many pitches it spans. It also
    // says whether it is a seat at this machine, and whether it is Ready.
    private readonly record struct CampaignChip(string Text, int Seat, int Pitches, bool Local, bool Ready);
}
