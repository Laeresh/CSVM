using CSVM.Mech3;

namespace CSVM.Session;

/// <summary>
/// The decoded egen launch cycle for ONE generator: pure state over <c>Step</c> calls, no
/// clocks, no randomness, no node reads, so the timing law is unit-testable off-engine. The law
/// (docs/formats/mission-entities.md "The generator cycle"): the timer always advances; a blocked
/// generator is HELD, never cancelled (timer and wave counter untouched), so a held spawn fires
/// the moment the block clears. <c>ind_period</c> is the gap between individuals inside a wave;
/// the gap between waves is <c>ind_period + wave_period</c> (they compose, they do not alternate).
///
/// <para>⚠ The capacity check is a STAND-IN. The decoded rule
/// (<c>wave_size − spawnedThisWave &gt; capacityRemaining</c>) with the shipped <c>capacity 0</c>
/// would hold every generator in the install forever, yet zeppelins launch in the original: an
/// unresolved discrepancy (the capacity puzzle, docs/formats/mission-entities.md). This class
/// applies the decoded rule only when <c>capacity &gt; 0</c> and disables the check at
/// <c>capacity ≤ 0</c>, pending the egen.zbd raw-byte read. That is NOT a decode of "0 means
/// unlimited"; it is the only reading that lets shipped data fire at all.</para>
///
/// <para>The hangar door is F20's (it needs a zeppelin model); its decoded HARDCODED timings are
/// recorded here as constants so F20 finds them beside the cycle they choreograph.</para>
/// </summary>
public sealed class GeneratorCycle
{
    /// <summary>The door opens this many seconds before a due spawn (hardcoded in the original).</summary>
    public const float DoorLeadSeconds = 4f;

    /// <summary>Minimum seconds the door stays open once opened (hardcoded in the original).</summary>
    public const float DoorMinOpenSeconds = 4f;

    /// <summary>The door closes early only when the next spawn is more than this many seconds
    /// away (hardcoded in the original), so a fast-cycling generator leaves its hangar open.</summary>
    public const float DoorEarlyCloseGapSeconds = 8f;

    private readonly int _capacity;
    private readonly int _maxActive;
    private readonly int _waveSize;
    private readonly float _wavePeriod;
    private readonly float _indPeriod;
    private readonly float? _minAltitude;

    private int _capacityRemaining;
    private int _spawnedThisWave;
    private float _timer;
    private float _nextEvent;

    public GeneratorCycle(int capacity, int maxActive, int waveSize, float wavePeriod,
        float indPeriod, float? minAltitude)
    {
        _capacity = capacity;
        _maxActive = maxActive;
        _waveSize = waveSize;
        _wavePeriod = wavePeriod;
        _indPeriod = indPeriod;
        _minAltitude = minAltitude;
        _capacityRemaining = capacity;
        // The decode does not pin the FIRST threshold; load is treated as "a wave just
        // completed" (the full inter-wave gap), the conservative reading until F20 confirms.
        _nextEvent = indPeriod + wavePeriod;
    }

    public GeneratorCycle(EnemyGeneratorDef def)
        : this(def.Capacity, def.MaxActive, def.WaveSize, def.WavePeriod, def.IndPeriod,
            def.MinAltitude)
    {
    }

    /// <summary>Spawns still alive, the <c>active</c> count of the blocking rule. A spawn's
    /// death frees its slot via <see cref="SpawnRemoved"/>.</summary>
    public int Active { get; private set; }

    /// <summary>Permanently off: the host's death disables its generator for good.</summary>
    public bool Disabled { get; private set; }

    /// <summary>Seconds since the last spawn (or load); compared against <see cref="NextEvent"/>.</summary>
    public float Timer => _timer;

    /// <summary>The threshold the timer must reach for the next spawn.</summary>
    public float NextEvent => _nextEvent;

    /// <summary>The host died: disable permanently (decoded rule, never re-enabled).</summary>
    public void HostDied() => Disabled = true;

    /// <summary>A spawned aircraft left the fight (shot down); its max_active slot frees.</summary>
    public void SpawnRemoved()
    {
        if (Active > 0)
        {
            Active--;
        }
    }

    /// <summary>Advances the cycle one sim step. True exactly when a spawn is due this step,
    /// at most one per call, matching the original's one-spawn-per-tick shape. A blocked step
    /// still advances the timer (hold, not cancel), so the spawn fires on the first unblocked
    /// step at or past the threshold. <paramref name="hostAltitude"/> is compared against the
    /// authored gate; pass anything when <see cref="EnemyGeneratorDef.MinAltitude"/> is null.</summary>
    public bool Step(float dt, float hostAltitude)
    {
        if (Disabled)
        {
            return false;
        }
        _timer += dt;
        if (Blocked(hostAltitude) || _timer < _nextEvent)
        {
            return false;
        }
        if (_capacity > 0)
        {
            _capacityRemaining--;
        }
        Active++;
        _spawnedThisWave++;
        _timer = 0f;
        if (_spawnedThisWave >= _waveSize)
        {
            _spawnedThisWave = 0;
            _nextEvent = _indPeriod + _wavePeriod;
        }
        else
        {
            _nextEvent = _indPeriod;
        }
        return true;
    }

    private bool Blocked(float hostAltitude) =>
        (_waveSize - _spawnedThisWave) + Active > _maxActive
        || (_capacity > 0 && _waveSize - _spawnedThisWave > _capacityRemaining)
        || (_minAltitude is float gate && hostAltitude < gate);
}
