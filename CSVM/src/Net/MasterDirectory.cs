using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace CSVM.Net;

/// <summary>
/// The games list's master-server half, engine-free. The list is fetched on request, at most once
/// every <see cref="RefreshSeconds"/>, and polled from the menu frame. Every listed game becomes
/// the row a LAN answer is shown as. The fetch arrives as a delegate, so a suite hands in its own
/// text; the shipped one is <c>Launch/MasterServerLink.cs</c>'s. Also the conversions both ends of
/// a listing share.
/// </summary>
public sealed class MasterDirectory
{
    /// <summary>The shortest gap between two fetches, in seconds. The games list asks every second
    /// while it is empty; the server is asked at this pace whatever the list does.</summary>
    public const double RefreshSeconds = 5.0;

    private readonly Func<CancellationToken, Task<string>> _fetch;
    private readonly List<LanGame> _games = new();
    private CancellationTokenSource? _cancel;
    private Task<string>? _pending;
    private double _sinceAsked = double.PositiveInfinity;

    /// <summary>A directory over <paramref name="fetch"/>, which answers the server's games list
    /// as text or throws.</summary>
    public MasterDirectory(Func<CancellationToken, Task<string>> fetch) =>
        _fetch = fetch ?? throw new ArgumentNullException(nameof(fetch));

    /// <summary>The games the last answer listed, as rows. Empty until one arrives.</summary>
    public IReadOnlyList<LanGame> Games => _games;

    /// <summary>Why the last fetch failed, or "" when it did not.</summary>
    public string Fault { get; private set; } = "";

    /// <summary>Whether the last answer said the server no longer serves this build's
    /// <see cref="MasterWire.ProtocolVersion"/>. Its games are then left off the list.</summary>
    public bool Outdated { get; private set; }

    /// <summary>Whether the directory has been asked since it was last told to forget.</summary>
    public bool Asking { get; private set; }

    /// <summary>How many answers have arrived, so a board can tell news from a repeat.</summary>
    public int Answers { get; private set; }

    /// <summary>A listed game as the games list shows it. The code stands in for the address, the
    /// port is 0, and the listing's fields make the advert a LAN host would answer with.</summary>
    public static LanGame ToGame(MasterGame game)
    {
        ArgumentNullException.ThrowIfNull(game);
        var kind = game.Kind switch
        {
            MasterWire.DogfightKind => NetSessionKind.Dogfight,
            MasterWire.CoopKind => NetSessionKind.CampaignCoop,
            _ => NetSessionKind.Unknown,
        };
        var status = game.Status switch
        {
            MasterWire.Waiting => NetSessionStatus.Waiting,
            MasterWire.InMission => NetSessionStatus.InMission,
            MasterWire.Full => NetSessionStatus.Full,
            _ => NetSessionStatus.Unknown,
        };
        byte mission = game.Mission is >= 0 and < SessionAdvertMessage.NoMission ? (byte)game.Mission : SessionAdvertMessage.NoMission;
        var advert = new SessionAdvertMessage(
            kind, mission, (byte)Math.Clamp(game.Players, 0, byte.MaxValue), game.Name, status, (byte)Math.Clamp(game.Cap, 0, byte.MaxValue), game.Password);
        return new LanGame(game.Code, 0, advert, NetBuildVersion.Parse(game.Version), game.Code);
    }

    /// <summary>The listing a host carries for <paramref name="advert"/>, the word its LAN answer
    /// gives too, under <paramref name="version"/>. An <paramref name="unlisted"/> game stays off
    /// the games list; the LAN answer has no such mark.</summary>
    public static MasterGame ListingOf(SessionAdvertMessage advert, NetBuildVersion version, bool unlisted = false) => new()
    {
        Unlisted = unlisted,
        Name = advert.Host ?? "",
        Kind = advert.Kind switch
        {
            NetSessionKind.Dogfight => MasterWire.DogfightKind,
            NetSessionKind.CampaignCoop => MasterWire.CoopKind,
            _ => "",
        },
        Players = advert.Players,
        Cap = advert.Cap,
        Password = advert.Password,
        Status = advert.Status switch
        {
            NetSessionStatus.Waiting => MasterWire.Waiting,
            NetSessionStatus.InMission => MasterWire.InMission,
            NetSessionStatus.Full => MasterWire.Full,
            _ => "",
        },
        Mission = advert.MissionSeq == SessionAdvertMessage.NoMission ? -1 : advert.MissionSeq,
        Version = version.ToString(),
    };

    /// <summary>Asks the server for its list, unless a fetch is under way or the last one left
    /// less than <see cref="RefreshSeconds"/> ago.</summary>
    public void Ask()
    {
        Asking = true;
        if (_pending != null || _sinceAsked < RefreshSeconds)
        {
            return;
        }

        _sinceAsked = 0.0;
        _cancel = new CancellationTokenSource();
        try
        {
            _pending = _fetch(_cancel.Token);
        }
        catch (Exception e) when (e is InvalidOperationException or ArgumentException)
        {
            _pending = Task.FromException<string>(e);
        }
    }

    /// <summary>Advances by <paramref name="dt"/> seconds and takes a finished fetch's answer.
    /// True when the rows or the fault changed.</summary>
    public bool Poll(double dt)
    {
        _sinceAsked += dt;
        if (_pending is not { IsCompleted: true } done)
        {
            return false;
        }

        _pending = null;
        string before = Fault;
        int count = _games.Count;
        if (!Asking)
        {
            return false;
        }

        if (done.IsCompletedSuccessfully && MasterWire.TryReadList(done.Result, out int oldest) is { } listed)
        {
            _games.Clear();
            Outdated = !MasterWire.Serves(oldest);
            foreach (var game in Outdated ? Array.Empty<MasterGame>() : listed)
            {
                _games.Add(ToGame(game));
            }

            Fault = Outdated
                ? $"the master server needs a newer build: it serves protocol {oldest} and later, this build speaks {MasterWire.ProtocolVersion}"
                : "";
            Answers++;
            return true;
        }

        Fault = done.Exception?.GetBaseException().Message is { Length: > 0 } why
            ? $"the master server did not answer: {why}"
            : "the master server's answer was not a games list";
        return Fault != before || _games.Count != count;
    }

    /// <summary>Stops asking and forgets what was listed. A fetch under way is cancelled and its
    /// answer dropped; the next <see cref="Ask"/> fetches at once.</summary>
    public void Forget()
    {
        _cancel?.Cancel();
        _cancel = null;
        _pending = null;
        _games.Clear();
        Fault = "";
        Outdated = false;
        Asking = false;
        _sinceAsked = double.PositiveInfinity;
    }
}
