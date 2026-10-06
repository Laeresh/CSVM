using System;
using System.Collections.Generic;
using CSVM.Flight.Hangar;
using CSVM.Net;
using CSVM.Session.Roster;
using CSVM.Spec;

namespace CSVM.UI.Menu;

/// <summary>
/// The bot rows a Dogfight host keeps and the rules every such list follows, engine-free. Both the
/// network lobby (<see cref="DogfightLobby"/>) and the local join board
/// (<see cref="PlayerSetupFeature.Bots"/>) hold one. A new row flies a Random plane at veteran. Its
/// callsign is drawn from the shipped pilot names that no pilot holds, else <c>Bot n</c>. A row keeps
/// its id while rows around it come and go. The owner holds the gates: who may edit, how many
/// pilots its field holds and which team a new row joins.
/// </summary>
public sealed class DogfightBots
{
    private readonly List<DogfightBot> _rows = new();
    private Random? _draws;
    private int _next = 1;

    /// <summary>The callsigns a new row draws from, the shipped pilot names
    /// (<see cref="BotSeats.CallsignPool"/>). Empty, every bot is called <c>Bot n</c>.</summary>
    public IReadOnlyList<string> CallsignPool { get; set; } = Array.Empty<string>();

    /// <summary>The draws a new row's callsign takes, the launch's own bot-field stream unless a
    /// caller pins one.</summary>
    public Random Draws
    {
        get => _draws ??= new Random(CSVM.Utils.Rng.IntSeedFor(CSVM.Utils.Rng.BotField));
        set => _draws = value;
    }

    /// <summary>The rows in the order they were added, the order they are listed and seated in.
    /// The last is the newest.</summary>
    public IReadOnlyList<DogfightBot> Rows => _rows;

    /// <summary>How many rows stand.</summary>
    public int Count => _rows.Count;

    /// <summary>How many more bots a field of <paramref name="pilots"/> takes before it holds
    /// <see cref="NetSeats.MaxPlayers"/>.</summary>
    public static int Room(int pilots) => Math.Max(0, NetSeats.MaxPlayers - pilots);

    /// <summary>Fill-to-N: calls <paramref name="add"/> until <paramref name="field"/> reaches
    /// <paramref name="pilots"/>, people and bots together, or <see cref="NetSeats.MaxPlayers"/>,
    /// or an add is refused. A field already that large takes none, and no row is removed. Answers
    /// how many it added.</summary>
    public static int FillTo(int pilots, Func<int> field, Func<bool> add)
    {
        ArgumentNullException.ThrowIfNull(field);
        ArgumentNullException.ThrowIfNull(add);
        int target = Math.Min(pilots, NetSeats.MaxPlayers);
        int added = 0;
        while (field() < target && add())
        {
            added++;
        }

        return added;
    }

    /// <summary>Adds a row on a Random plane at veteran on <paramref name="team"/>. Its callsign is
    /// drawn from <see cref="CallsignPool"/> where neither a row nor <paramref name="people"/> holds
    /// it, else it is the first free <c>Bot n</c>. The owner has already said there is room.</summary>
    public DogfightBot Add(IReadOnlyList<string> people, byte team)
    {
        ArgumentNullException.ThrowIfNull(people);
        var bot = new DogfightBot(_next++, DrawCallsign(people), DogfightLobbySeat.RandomAirframe, NetBotSkill.Veteran, team);
        _rows.Add(bot);
        return bot;
    }

    /// <summary>Removes row <paramref name="id"/>; false for a row not there.</summary>
    public bool Remove(int id) => _rows.RemoveAll(bot => bot.Id == id) > 0;

    /// <summary>Removes the newest row, the one a person joining a full field takes the place of.
    /// False with no row.</summary>
    public bool DropNewest()
    {
        if (_rows.Count == 0)
        {
            return false;
        }

        _rows.RemoveAt(_rows.Count - 1);
        return true;
    }

    /// <summary>Removes every row.</summary>
    public void Clear() => _rows.Clear();

    /// <summary>Renames row <paramref name="id"/>, cut at the Callsign box's 12 characters.
    /// Refused for a blank name and for one another row or <paramref name="people"/> holds.</summary>
    public bool Rename(int id, string callsign, IReadOnlyList<string> people)
    {
        ArgumentNullException.ThrowIfNull(people);
        string clipped = BotSeats.ClipName(callsign);
        if (clipped.Length == 0 || Holds(people, clipped))
        {
            return false;
        }

        foreach (var bot in _rows)
        {
            if (bot.Id != id && string.Equals(bot.Callsign, clipped, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return Edit(id, bot => bot with { Callsign = clipped });
    }

    /// <summary>Redraws the callsign of every row whose name one of <paramref name="people"/> holds,
    /// as <see cref="Add"/> draws, since a person keeps the name they joined with. Answers how many
    /// rows it renamed.</summary>
    public int YieldNames(IReadOnlyList<string> people)
    {
        ArgumentNullException.ThrowIfNull(people);
        int renamed = 0;
        for (int i = 0; i < _rows.Count; i++)
        {
            if (Holds(people, _rows[i].Callsign))
            {
                _rows[i] = _rows[i] with { Callsign = DrawCallsign(people) };
                renamed++;
            }
        }

        return renamed;
    }

    /// <summary>Puts row <paramref name="id"/> on one of the eleven stock airframes or on
    /// <see cref="DogfightLobbySeat.RandomAirframe"/>.</summary>
    public bool SetAirframe(int id, int airframe) =>
        (airframe is >= 0 and < DogfightLobby.AirframeCount || airframe == DogfightLobbySeat.RandomAirframe)
        && Edit(id, bot => bot with { Airframe = (byte)airframe });

    /// <summary>Sets row <paramref name="id"/>'s skill tier.</summary>
    public bool SetSkill(int id, NetBotSkill skill) =>
        skill is >= NetBotSkill.Novice and <= NetBotSkill.Ace && Edit(id, bot => bot with { Skill = skill });

    /// <summary>Moves row <paramref name="id"/> to lobby team <paramref name="team"/>. The owner
    /// says which teams stand.</summary>
    public bool SetTeam(int id, byte team) => Edit(id, bot => bot with { Team = team });

    /// <summary>Puts every row on <paramref name="team"/> on no team, as a disband must, or a later
    /// team minted with the same number would take them unasked.</summary>
    public void ClearTeam(byte team)
    {
        for (int i = 0; i < _rows.Count; i++)
        {
            if (_rows[i].Team == team)
            {
                _rows[i] = _rows[i] with { Team = 0 };
            }
        }
    }

    /// <summary>Row <paramref name="id"/>, or null.</summary>
    public DogfightBot? ById(int id)
    {
        foreach (var bot in _rows)
        {
            if (bot.Id == id)
            {
                return bot;
            }
        }

        return null;
    }

    /// <summary>The rows as a launch seats them. A Random plane is null, which the host draws
    /// (<see cref="BotSeats.Resolve"/>), and a stock one is its node.</summary>
    public IReadOnlyList<VsBotEntry> LaunchEntries()
    {
        var entries = new VsBotEntry[_rows.Count];
        for (int i = 0; i < entries.Length; i++)
        {
            var bot = _rows[i];
            entries[i] = new VsBotEntry(bot.RandomPlane ? null : StockAirframes.Node(bot.Airframe), bot.Skill, bot.Team, bot.Callsign);
        }

        return entries;
    }

    private static bool Holds(IReadOnlyList<string> names, string name)
    {
        foreach (string held in names)
        {
            if (string.Equals(held, name, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    // A pilot name no row and no person holds, at random from the pool, else the first "Bot n".
    private string DrawCallsign(IReadOnlyList<string> people)
    {
        var held = new List<string>(people);
        foreach (var bot in _rows)
        {
            held.Add(bot.Callsign);
        }

        var free = new List<string>();
        foreach (string name in CallsignPool)
        {
            if (BotSeats.ClipName(name) is { Length: > 0 } clipped && !Holds(held, clipped) && !Holds(free, clipped))
            {
                free.Add(clipped);
            }
        }

        if (free.Count > 0)
        {
            return free[Draws.Next(free.Count)];
        }

        for (int n = 1; ; n++)
        {
            string fallback = SessionSpec.BotCallsign(n);
            if (!Holds(held, fallback))
            {
                return fallback;
            }
        }
    }

    // One row changed in place, keeping its place in the list.
    private bool Edit(int id, Func<DogfightBot, DogfightBot> change)
    {
        int at = _rows.FindIndex(bot => bot.Id == id);
        if (at < 0)
        {
            return false;
        }

        _rows[at] = change(_rows[at]);
        return true;
    }
}
