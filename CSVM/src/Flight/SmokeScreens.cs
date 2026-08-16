using System;
using System.Collections.Generic;
using System.IO;
using CSVM.Mech3;
using Godot;

namespace CSVM.Flight;

/// <summary>The smoke screen's blend-wash sink, <c>ScreenFlash.PlayBlend</c>'s shape without the
/// start delay (the smoke caller passes none): the victim's player index, the colour, the weight
/// and the duration.</summary>
public delegate void SmokeWashSink(int playerIndex, Color colour, float weight, float durationSeconds);

/// <summary>The smoke screen's per-victim rules with no aircraft in them, decoded from
/// <c>FUN_004b8fd0</c>: the catch test and the human wash's cadence. <see cref="SmokeScreens"/>
/// runs them over the live roster; the numbers here are the routine's own literals.</summary>
public static class SmokeScreenRule
{
    /// <summary>The wash's duration and the per-victim re-arm timer, both 2 s in the routine.</summary>
    public const float WashDurationS = 2f;

    /// <summary>The wash re-applies once the re-arm timer has run below this, so a victim who
    /// stays inside is washed again every 1.5 s, not every 2.</summary>
    public const float WashRearmBelowS = 0.5f;

    /// <summary>The wash weight while the re-arm timer is still running down.</summary>
    public const float WashWeight = 0.9f;

    /// <summary>The wash weight on a first hit, or one after the timer has fully expired.</summary>
    public const float FirstWashWeight = 0.97f;

    /// <summary>The wash the routine hands <c>FUN_0042e9d0</c>: a grey-green
    /// <c>(0.2, 0.29, 0.145)</c> for 2 s with no start delay.</summary>
    public static readonly Color WashColour = new(0.2f, 0.29f, 0.145f);

    /// <summary>Whether a victim at <paramref name="victimPos"/> is inside the screen: strictly
    /// closer than <paramref name="rangeM"/> to the layer, and the unit line from the layer to it
    /// making a dot STRICTLY above <paramref name="halfAngleCos"/> with the layer's backward axis.
    /// A victim standing exactly on the layer normalises to nothing and is not caught.</summary>
    public static bool Catches(Vector3 layerPos, Vector3 layerBackward, Vector3 victimPos,
        float rangeM, float halfAngleCos)
    {
        var to = victimPos - layerPos;
        float distance = to.Length();
        if (distance >= rangeM || distance <= 0f)
            return false;
        return (to / distance).Dot(layerBackward) > halfAngleCos;
    }

    /// <summary>One frame of the human wash's cadence over a victim's re-arm timer: the timer
    /// runs down by <paramref name="dt"/> first (never below zero); then, if the victim is inside
    /// and the timer sits below <see cref="WashRearmBelowS"/>, the wash fires at
    /// <see cref="FirstWashWeight"/> when the timer had reached zero and <see cref="WashWeight"/>
    /// otherwise, and the timer restarts at <see cref="WashDurationS"/>. Returns the weight to
    /// wash at, or null for no wash this frame.</summary>
    public static float? StepWash(ref float rearmS, float dt, bool inside)
    {
        if (rearmS > 0f)
            rearmS = Mathf.Max(0f, rearmS - dt);
        if (!inside || rearmS >= WashRearmBelowS)
            return null;
        float weight = rearmS <= 0f ? FirstWashWeight : WashWeight;
        rearmS = WashDurationS;
        return weight;
    }
}

/// <summary>The three game-wide smoke screen tunables from <c>player.json</c>'s global block
/// (docs/org/ordnanceTypes.md "SMOKE_SCREEN is a stun trap"). They are NOT per-weapon: the
/// original's <c>ai_skill_parameters</c> loader <c>FUN_004735b0</c> writes them into three
/// globals that <c>FUN_004b8fd0</c> reads for every screen. The angle is stored as the cosine of
/// HALF the authored angle, so the shipped 170° means 85° off the layer's backward axis.</summary>
public sealed class SmokeScreenTunables
{
    /// <summary>The loader's defaults for a key that is absent (<c>0x004735b0</c>: 200 m, a
    /// half-angle cosine of 0.8, which is about 73.7° across, and 3 s), the static image's own
    /// numbers rather than anything this port chose.</summary>
    public static readonly SmokeScreenTunables Image = new(200f, 0.8f, 3f);

    private SmokeScreenTunables(float rangeM, float halfAngleCos, float stunIntervalS)
    {
        RangeM = rangeM;
        HalfAngleCos = halfAngleCos;
        StunIntervalS = stunIntervalS;
    }

    /// <summary><c>smokescreen_stun_range</c>, metres, raw. 600 shipped.</summary>
    public float RangeM { get; }

    /// <summary><c>smokescreen_stun_angle</c> as the original stores it:
    /// <c>cos(angle × π/180 × 0.5)</c>. 170° shipped, so about 0.087.</summary>
    public float HalfAngleCos { get; }

    /// <summary><c>smokescreen_stun_interval</c>, seconds: the AI stun's duration, refreshed
    /// every frame the pilot stays inside. 5 shipped.</summary>
    public float StunIntervalS { get; }

    /// <summary>The stored cosine for an authored full angle in degrees, the loader's own
    /// arithmetic (<c>0x004735b0</c>).</summary>
    public static float HalfAngleCosOf(float fullAngleDeg) =>
        Mathf.Cos(fullAngleDeg * Mathf.Pi / 180f * 0.5f);

    /// <summary>Builds from the three authored values, for a suite or a caller with the numbers
    /// in hand.</summary>
    public static SmokeScreenTunables From(float rangeM, float fullAngleDeg, float stunIntervalS) =>
        new(rangeM, HalfAngleCosOf(fullAngleDeg), stunIntervalS);

    /// <summary>Reads the three keys off <c>player.json</c> in the shared zrdr scope, taking
    /// <see cref="Image"/>'s value for any key that is absent, as the original's loader does.
    /// Throws when the file itself cannot be read: a miss is a wrong path, not a default to paper
    /// over.</summary>
    public static SmokeScreenTunables Load(string zrdrPath)
    {
        if (Zrdr.LoadFile(zrdrPath, "player.json")[0] is not List<object?> playerList)
            throw new InvalidDataException("player.json: unexpected root shape");
        var player = ZrdrDict.FromAlternating(playerList);
        float range = player.TryFloat("smokescreen_stun_range", out float r) ? r : Image.RangeM;
        float cos = player.TryFloat("smokescreen_stun_angle", out float a) ? HalfAngleCosOf(a) : Image.HalfAngleCos;
        float interval = player.TryFloat("smokescreen_stun_interval", out float s) ? s : Image.StunIntervalS;
        return new SmokeScreenTunables(range, cos, interval);
    }
}

/// <summary>The world's active smoke screens (docs/org/ordnanceTypes.md "SMOKE_SCREEN is a stun
/// trap"): the launch path spawns no round for a <c>SMOKE_SCREEN</c> weapon, it calls
/// <see cref="Lay"/>, and every sim step while a screen's <c>TIME</c> runs it walks the roster
/// and hits every in-play aircraft other than the layer inside the cone about the layer's LIVE
/// backward axis. A human victim gets the grey-green wash on its own pane, an AI victim gets
/// <see cref="FlightController.TryStunPilot"/> for the stun interval, refreshed every step it
/// stays inside. It is not an occluder: nothing here reads or writes visibility, targeting or
/// collision.
/// ⚠ The wash cooldown is per victim per screen. The original keeps one slot per screen because
/// it has one player; per pane is the plan's Decision 2, so viewer 1 in the cloud does not
/// silence viewer 3's wash.</summary>
public sealed class SmokeScreens
{
    private readonly SmokeScreenTunables _tunables;
    private readonly Func<IReadOnlyList<FlightController>> _roster;
    private readonly SmokeWashSink? _wash;
    private readonly List<Screen> _screens = new();

    /// <summary>One registry with a roster it walks and a wash sink for its human victims. The
    /// roster delegate is read every step, so aircraft that spawn after a screen is laid are
    /// walked too. A null sink means humans inside see nothing, as in a headless run.</summary>
    public SmokeScreens(SmokeScreenTunables tunables, Func<IReadOnlyList<FlightController>> roster,
        SmokeWashSink? wash)
    {
        _tunables = tunables;
        _roster = roster;
        _wash = wash;
    }

    /// <summary>The tunables every screen runs on.</summary>
    public SmokeScreenTunables Tunables => _tunables;

    /// <summary>How many screens are running (their <c>TIME</c> not yet run out).</summary>
    public int ActiveCount => _screens.Count;

    /// <summary>Whether <paramref name="layer"/> has a screen running.</summary>
    public bool IsLaying(FlightController layer)
    {
        foreach (var s in _screens)
        {
            if (ReferenceEquals(s.Layer, layer))
                return true;
        }
        return false;
    }

    /// <summary>The launch-side entry: a <c>SMOKE_SCREEN</c> weapon leaving <paramref name="layer"/>
    /// lays a screen for <paramref name="timeSeconds"/> (the weapon's <c>SmokeScreenTime</c>,
    /// <c>TIME</c>). Nothing else is spawned. A non-positive time lays nothing. Two screens from
    /// one layer both run; each is its own object in the original's list too.</summary>
    public void Lay(FlightController layer, float timeSeconds)
    {
        if (timeSeconds <= 0f)
            return;
        _screens.Add(new Screen(layer, timeSeconds));
    }

    /// <summary>Drops every screen: the session teardown and a suite's reset.</summary>
    public void Clear() => _screens.Clear();

    /// <summary>One sim step for every screen: the timer runs down first, a screen whose layer is
    /// no longer in play ends on the spot, and a screen still running walks the roster.</summary>
    public void SimStep(float dt)
    {
        if (_screens.Count == 0)
            return;
        var roster = _roster();
        for (int i = _screens.Count - 1; i >= 0; i--)
        {
            var screen = _screens[i];
            if (!screen.Layer.InPlay)
                screen.RemainingS = -1f;
            else
                screen.RemainingS -= dt;
            if (screen.RemainingS <= 0f)
            {
                _screens.RemoveAt(i);
                continue;
            }
            Walk(screen, roster, dt);
        }
    }

    // The per-screen walk of FUN_004b8fd0: every in-play aircraft other than the layer, tested
    // against the layer's position and backward axis as they stand THIS step (the routine reads
    // both through the layer object each frame, never a pose captured at the lay). The human
    // branch keys on the victim being the player; ours keys on the seat, so a wash goes to
    // whichever pane flies the victim, and the stun's own guards refuse a human.
    private void Walk(Screen screen, IReadOnlyList<FlightController> roster, float dt)
    {
        var layer = screen.Layer;
        var layerPos = layer.WorldPosition;
        var backward = -layer.NoseDirection;
        for (int i = 0; i < roster.Count; i++)
        {
            var victim = roster[i];
            if (ReferenceEquals(victim, layer) || !victim.InPlay)
                continue;
            bool inside = SmokeScreenRule.Catches(layerPos, backward, victim.WorldPosition,
                _tunables.RangeM, _tunables.HalfAngleCos);
            if (victim.IsHumanPiloted)
            {
                float rearm = screen.RearmFor(victim.PlayerIndex);
                float? weight = SmokeScreenRule.StepWash(ref rearm, dt, inside);
                screen.SetRearm(victim.PlayerIndex, rearm);
                if (weight is { } w)
                    _wash?.Invoke(victim.PlayerIndex, SmokeScreenRule.WashColour, w, SmokeScreenRule.WashDurationS);
            }
            else if (inside)
            {
                victim.TryStunPilot(_tunables.StunIntervalS);
            }
        }
    }

    private sealed class Screen
    {
        public readonly FlightController Layer;
        public float RemainingS;
        private readonly Dictionary<int, float> _rearm = new();

        public Screen(FlightController layer, float remainingS)
        {
            Layer = layer;
            RemainingS = remainingS;
        }

        public float RearmFor(int playerIndex) => _rearm.TryGetValue(playerIndex, out float t) ? t : 0f;

        public void SetRearm(int playerIndex, float rearmS) => _rearm[playerIndex] = rearmS;
    }
}
