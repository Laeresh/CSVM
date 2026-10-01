using System;
using System.Collections.Generic;
using Godot;

namespace CSVM.Flight.Modes;

/// <summary>Where one team's flag is: the original's flag record state word (+0x08).</summary>
public enum FlagState : byte
{
    /// <summary>A pilot carries it.</summary>
    Held = 1,

    /// <summary>It stands at its team's base.</summary>
    Home = 2,

    /// <summary>A downed or ejecting carrier let it go and it is in its throw arc. Each machine
    /// reaches this state on its own, so no table carries it.</summary>
    Floating = 3,
}

/// <summary>What a pilot asks its host for, the original's <c>0x1c</c> request word.</summary>
public enum FlagAsk : byte
{
    /// <summary>Pick the flag up: an enemy flag at its base, or any floating flag.</summary>
    Take = 1,

    /// <summary>Bring the carried flag home: its own team's flag returned, or an enemy's captured.
    /// </summary>
    Home = 2,

    /// <summary>Let the carried flag go, the console's <c>ejectflag</c>. No original <c>0x1c</c>
    /// word: the host relays it, since the original drops it on the typist's machine alone.</summary>
    Eject = 3,
}

/// <summary>One flag as the host's table has it: its team, its state and the seat holding it. The
/// team is also its id on the wire, and <see cref="FlagMatch.NoHolder"/> means no holder.</summary>
public readonly record struct FlagRow(int Team, FlagState State, int Holder);

/// <summary>One flag that moved: its team, the state and holder before, and the state and holder
/// after. What the visuals, the voice, the HUD lines and the host's scoring are driven by.</summary>
public readonly record struct FlagChange(int Team, FlagState From, int HolderBefore, FlagState To, int HolderAfter);

/// <summary>
/// Capture the Flag's rules: one flag per team and the proximity asks a pilot's machine makes. It
/// holds the host's decision on them, a downed carrier's drop with its throw arc, and the points a
/// flag brought home scores. The host alone decides and scores, and every machine reaches
/// <see cref="FlagState.Floating"/> on its own. Decode: docs/org/multiplayer-ctf.md. ⚠ Keep it
/// free of any engine dependency beyond the vector struct, as <see cref="VersusMatch"/>: a unit
/// suite runs it with no engine.
/// </summary>
public sealed class FlagMatch
{
    /// <summary>The seat value meaning nobody holds a flag.</summary>
    public const int NoHolder = -1;

    /// <summary>How close a pilot must be, in metres, to take a flag or bring one home: the
    /// squared 625 at <c>0x6081c0</c> that <c>FUN_00499e50</c> compares against.</summary>
    public const float Reach = 25f;

    /// <summary>Seconds before a pilot that asked for a flag at its base may ask again
    /// (<c>0x6036bc</c>).</summary>
    public const float HomeTakeCooldown = 5f;

    /// <summary>Seconds before a pilot may ask again after any other ask (<c>0x603514</c>).</summary>
    public const float AskCooldown = 4f;

    /// <summary>How long a floating flag flies before the host sends it home: the throw's
    /// <c>RUN_TIME</c> ahead of its <c>Callback 700 + n</c>.</summary>
    public const float ThrowSeconds = 15f;

    /// <summary>The throw arc's gravity, m/s², the <c>flg_throw_n</c> motion's own.</summary>
    public const float ThrowGravity = -7f;

    // The landing response every column contact takes (docs/org/objectMotion.md).
    private const float ContactDamping = 0.2f;

    private readonly List<Flag> _flags = new();
    private readonly Func<int, int> _teamOf;
    private readonly Dictionary<int, float> _cooldown = new();
    private float _clock;

    /// <summary>A match over one flag per <paramref name="homes"/> entry, each seat on the team
    /// <paramref name="teamOf"/> names. With <paramref name="ownFlagHomeToCapture"/> an enemy
    /// flag scores only while the carrier's own flag stands at its base, the host's option.</summary>
    public FlagMatch(IReadOnlyList<(int Team, Vector3 Home)> homes, Func<int, int> teamOf, bool ownFlagHomeToCapture = false)
    {
        ArgumentNullException.ThrowIfNull(homes);
        _teamOf = teamOf ?? throw new ArgumentNullException(nameof(teamOf));
        OwnFlagHomeToCapture = ownFlagHomeToCapture;
        foreach (var (team, home) in homes)
        {
            _flags.Add(new Flag { Team = team, Home = home });
        }
    }

    /// <summary>Whether an enemy flag scores only while the carrier's own flag is at home. The
    /// original captures whatever its own flag is doing.</summary>
    public bool OwnFlagHomeToCapture { get; }

    /// <summary>Every flag as this machine has it, in team order.</summary>
    public IReadOnlyList<FlagRow> Rows
    {
        get
        {
            var rows = new FlagRow[_flags.Count];
            for (int i = 0; i < rows.Length; i++)
                rows[i] = _flags[i].Row;
            return rows;
        }
    }

    /// <summary>The points a change scores its carrier: a flag carried home scores
    /// <see cref="MatchScores.FlagReturn"/> when it is the carrier's own and
    /// <see cref="MatchScores.FlagCapture"/> otherwise. Nothing else scores, a floating flag's return
    /// included (<c>FUN_0049a300</c>).</summary>
    public static int Points(FlagChange change, Func<int, int> teamOf, MatchScores scores)
    {
        ArgumentNullException.ThrowIfNull(teamOf);
        ArgumentNullException.ThrowIfNull(scores);
        if (change.From != FlagState.Held || change.To != FlagState.Home || change.HolderBefore < 0)
            return 0;
        return teamOf(change.HolderBefore) == change.Team ? scores.FlagReturn : scores.FlagCapture;
    }

    /// <summary>The flag of <paramref name="team"/>, or null for a team with none.</summary>
    public FlagRow? RowOf(int team) => Find(team)?.Row;

    /// <summary>Where <paramref name="team"/>'s flag stands at home.</summary>
    public Vector3 HomeOf(int team) => Find(team)?.Home ?? Vector3.Zero;

    /// <summary>Where <paramref name="team"/>'s floating flag is now, null unless it floats.</summary>
    public Vector3? FloatingAt(int team) => Find(team) is { State: FlagState.Floating } flag ? flag.At : null;

    /// <summary>The team whose flag <paramref name="seat"/> carries, 0 for none.</summary>
    public int Carried(int seat)
    {
        foreach (var flag in _flags)
        {
            if (flag.State == FlagState.Held && flag.Holder == seat)
                return flag.Team;
        }

        return 0;
    }

    /// <summary>One pilot's proximity check, the original's per-tick <c>FUN_00499e50</c>, on the
    /// machine that flies <paramref name="seat"/>. It returns what to ask the host, or null, and
    /// starts the seat's cooldown when it asks.</summary>
    public (int Team, FlagAsk Ask)? Check(int seat, Vector3 position)
    {
        int own = _teamOf(seat);
        int carried = Carried(seat);
        bool cooled = !_cooldown.TryGetValue(seat, out float until) || until <= _clock;
        bool ready = carried == 0 && cooled;
        foreach (var flag in _flags)
        {
            if (flag.State == FlagState.Home && flag.Team != own && ready && Near(position, flag.Home))
                return Ask(seat, flag.Team, FlagAsk.Take, HomeTakeCooldown);
            if (flag.State == FlagState.Floating && ready && Near(position, flag.At))
                return Ask(seat, flag.Team, FlagAsk.Take, AskCooldown);
            if (flag.State != FlagState.Held || carried != flag.Team || !cooled)
                continue;

            // A carried own flag goes home at its own base, and an enemy flag at the carrier's.
            if (Near(position, flag.Team == own ? flag.Home : HomeOf(own)) && (flag.Team == own || MayCapture(own)))
                return Ask(seat, flag.Team, FlagAsk.Home, AskCooldown);
        }

        return null;
    }

    /// <summary>The host's decision on one ask, <c>FUN_0049a170</c>. A take while nobody holds the
    /// flag gives it to the asker, so the first asker wins. A home sends it home. The change, or
    /// null when the ask changes nothing.</summary>
    public FlagChange? Decide(int team, FlagAsk ask, int seat)
    {
        if (Find(team) is not { } flag)
            return null;
        if (ask == FlagAsk.Take)
            return flag.State == FlagState.Held ? null : Set(flag, FlagState.Held, seat);
        if (ask != FlagAsk.Home)
            return null;
        if (flag.State == FlagState.Held && flag.Holder >= 0 && _teamOf(flag.Holder) != team && !MayCapture(_teamOf(flag.Holder)))
            return null;
        return Set(flag, FlagState.Home, NoHolder);
    }

    /// <summary>A guest's own take, applied at once without waiting for its host, as the
    /// original's <c>FUN_0049a050</c> does. The host's table corrects it if another pilot won.</summary>
    public FlagChange? TakeAhead(int team, int seat) =>
        Find(team) is { } flag && flag.State != FlagState.Held ? Set(flag, FlagState.Held, seat) : null;

    /// <summary>The host's table applied, <c>FUN_0049a300</c>: every row whose state or holder
    /// differs moves. A floating row is this machine's own to reach, so the table never moves one.
    /// </summary>
    public List<FlagChange> Apply(IReadOnlyList<FlagRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        var changes = new List<FlagChange>();
        foreach (var row in rows)
        {
            if (row.State is not (FlagState.Held or FlagState.Home) || Find(row.Team) is not { } flag)
                continue;
            if (Set(flag, row.State, row.State == FlagState.Held ? row.Holder : NoHolder) is { } change)
                changes.Add(change);
        }

        return changes;
    }

    /// <summary>The carrier <paramref name="seat"/> went down or let go: its flag floats from
    /// <paramref name="at"/> at <paramref name="velocity"/>, and comes to rest no lower than
    /// <paramref name="floorY"/>. Run on every machine (<c>FUN_0049ab50</c>). Null when it carried
    /// nothing.</summary>
    public FlagChange? Drop(int seat, Vector3 at, Vector3 velocity, float floorY)
    {
        if (Find(Carried(seat)) is not { } flag)
            return null;
        var change = Set(flag, FlagState.Floating, NoHolder);
        flag.At = at;
        flag.Velocity = velocity;
        flag.FloorY = floorY;
        flag.Flown = 0f;
        flag.Resting = false;
        return change;
    }

    /// <summary>Every flag home with nobody holding it, the host's rematch. The changes it made.
    /// </summary>
    public List<FlagChange> Reset()
    {
        var changes = new List<FlagChange>();
        foreach (var flag in _flags)
        {
            if (Set(flag, FlagState.Home, NoHolder) is { } change)
                changes.Add(change);
        }

        _cooldown.Clear();
        return changes;
    }

    /// <summary>Moves the match clock the cooldowns read and flies each floating flag's arc by
    /// <paramref name="dt"/>. It returns the teams whose throw ran out this step, the flags the host
    /// sends home (<c>FUN_0049a210</c>).</summary>
    public List<int> Advance(float dt)
    {
        _clock += dt;
        var ended = new List<int>();
        foreach (var flag in _flags)
        {
            if (flag.State != FlagState.Floating || flag.Flown >= ThrowSeconds)
                continue;
            flag.Flown += dt;
            Fly(flag, dt);
            if (flag.Flown >= ThrowSeconds)
                ended.Add(flag.Team);
        }

        return ended;
    }

    private static bool Near(Vector3 a, Vector3 b) => a.DistanceSquaredTo(b) <= Reach * Reach;

    // One step of the throw: a ballistic arc under the throw's gravity. On the floor under the drop
    // it takes the column contact's landing response (docs/org/objectMotion.md). Once at rest the
    // flag lies where it landed until its throw runs out.
    private static void Fly(Flag flag, float dt)
    {
        if (flag.Resting)
            return;
        var step = flag.Velocity * dt;
        flag.Velocity += new Vector3(0f, ThrowGravity * dt, 0f);
        var next = flag.At + step;
        if (next.Y < flag.FloorY)
        {
            bool moving = Mathf.Abs(flag.Velocity.X) >= 0.1f || Mathf.Abs(flag.Velocity.Z) >= 0.1f || Mathf.Abs(flag.Velocity.Y) >= 0.5f;
            next.Y = moving ? flag.FloorY + Mathf.Abs(step.Y * 0.5f) : flag.FloorY;
            flag.Velocity *= ContactDamping;
            flag.Resting = ThrowGravity * ThrowGravity > flag.Velocity.LengthSquared();
        }

        flag.At = next;
    }

    private static FlagChange? Set(Flag flag, FlagState state, int holder)
    {
        if (flag.State == state && flag.Holder == holder)
            return null;
        var change = new FlagChange(flag.Team, flag.State, flag.Holder, state, holder);
        flag.State = state;
        flag.Holder = holder;
        if (state != FlagState.Floating)
            flag.Flown = 0f;
        return change;
    }

    private (int Team, FlagAsk Ask) Ask(int seat, int team, FlagAsk ask, float cooldown)
    {
        _cooldown[seat] = _clock + cooldown;
        return (team, ask);
    }

    // The host's option: an enemy flag scores only while the capturing team's own flag is home.
    private bool MayCapture(int team) => !OwnFlagHomeToCapture || Find(team) is not { } own || own.State == FlagState.Home;

    private Flag? Find(int team)
    {
        foreach (var flag in _flags)
        {
            if (flag.Team == team)
                return flag;
        }

        return null;
    }

    private sealed class Flag
    {
        public int Team;
        public FlagState State = FlagState.Home;
        public int Holder = NoHolder;
        public Vector3 Home;
        public Vector3 At;
        public Vector3 Velocity;
        public float FloorY;
        public float Flown;
        public bool Resting;

        public FlagRow Row => new(Team, State, Holder);
    }
}
