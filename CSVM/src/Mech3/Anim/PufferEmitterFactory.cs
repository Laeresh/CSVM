using CSVM.Effects;
using CSVM.Utils;
using Godot;

namespace CSVM.Mech3.Anim;

/// <summary>Builds real <see cref="Puffer"/> emitters from a session's textures and parents them.
/// One shared factory for the world, world-effects and per-player crash runtimes, whose needs are
/// byte-identical.
///
/// <para>⚠ <paramref name="parent"/> is the WORLD root, never a per-player crash root: a
/// PUFFER_STATE emitter goes TopLevel (world space) the moment it emits, and parenting it under the
/// controller subtree leaves it drawn-but-unrendered — every particle correctly positioned,
/// <c>IsVisibleInTree</c> true, and nothing on screen. Owning the parent here is what keeps that
/// rule structural instead of prose repeated per role factory.</para>
///
/// <para>⚠ Valid only while <paramref name="textures"/> is open — an emitter bakes its atlas at
/// construction. A caller whose archive dies with its build retires the director's factory rather
/// than leaving this one holding a closed zip handle (see
/// <see cref="EmitterDirector.RetireFactory"/>).</para></summary>
public sealed class PufferEmitterFactory : IEmitterFactory
{
    private readonly TextureArchive _textures;
    private readonly Node _parent;
    // B6: the session's wind, read by every emitter this builds. Still air when a caller has no
    // session to take it from (the suites' fake runtimes).
    private readonly EffectAmbience _ambience;

    public PufferEmitterFactory(TextureArchive textures, Node parent, EffectAmbience? ambience = null)
    {
        _textures = textures;
        _parent = parent;
        _ambience = ambience ?? EffectAmbience.Still;
    }

    public IEmitter? Create(PufferState state, out string? miss)
    {
        // A PUFFER_STATE carrying no textures is an adjust/stop stub that re-asserts a puffer some
        // other event defines — the readers have the same idiom, which is why
        // PufferState.FindInReader tests for a "fully defined" state. There is nothing to build
        // from it; C1's truck1dust_puffer and black_exhaust_puffer are the two here.
        if (state.Textures.Count == 0 && state.TextureSequence.Count == 0)
        {
            miss = $"PufferState(stub, no textures: {state.Name})";
            return null;
        }
        if (Puffer.Create(state, _textures, sustained: true, ambience: _ambience) is not { } puffer)
        {
            // Name it: a puffer whose textures are absent from this chapter's archive is a
            // data-coverage fact worth being able to look up, not an anonymous count.
            miss = $"PufferState(no texture: {state.Name})";
            return null;
        }
        _parent.AddChild(puffer);
        miss = null;
        return new PufferEmitter(puffer);
    }
}

/// <summary>The factory a runtime has after its texture archive was released: it builds nothing,
/// names the miss, and says so once out loud.
///
/// <para>⚠ The warning is the point, and it stays. A null object that swallows the request silently
/// is a world with no fire, no dust and no smoke reading as a clean log — however
/// polite its type name. The bootstrap census cannot cover this: it prints before the first death,
/// ON_CALL sequence or range-deferred def can reach a PUFFER_STATE. Once per runtime rather than
/// once per name, because the condition is one build-time contract rather than one datum per
/// emitter, so the first miss says everything the thousandth would.</para></summary>
public sealed class SpentEmitterFactory : IEmitterFactory
{
    private bool _reported;

    public IEmitter? Create(PufferState state, out string? miss)
    {
        miss = "PufferState(after build)";
        if (!_reported)
        {
            _reported = true;
            Log.Warn("anim", $"puffer '{state.Name}' asked for after the texture archive was released — this runtime builds no further PUFFER_STATE emitters (WorldSession.Options.TexturesOutliveBuild)");
        }
        return null;
    }
}

/// <summary>The real emitter: a <see cref="Puffer"/> behind <see cref="IEmitter"/>.</summary>
internal sealed class PufferEmitter : IEmitter
{
    private readonly Puffer _puffer;

    internal PufferEmitter(Puffer puffer) => _puffer = puffer;

    public int LiveCount => _puffer.LiveCount;

    public bool IsValid => GodotObject.IsInstanceValid(_puffer);

    public void SustainAt(Vector3 worldPos, Basis worldBasis, float dt) =>
        _puffer.Emit(worldPos, worldBasis, dt);

    public void SustainEnd() => _puffer.Stop();

    public void Clear() => _puffer.Clear();

    public void Destroy() => _puffer.QueueFree();
}
