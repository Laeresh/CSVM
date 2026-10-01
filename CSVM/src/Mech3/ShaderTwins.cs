using System;
using System.Collections.Generic;
using System.Linq;
using CSVM.Utils;
using Godot;

namespace CSVM.Mech3;

/// <summary>
/// The generated shaders behind every shader cache, kept one per text, and the materials that wear
/// them. Godot compiles each <see cref="Shader"/> object on its own, so two cache keys whose texts
/// agree share one. A material keeps the key it was made for. A live switch moves it onto that key's
/// shader for the other mode, and no text changes (<see cref="Regenerate"/>).
/// Main thread only, process-lifetime, like the caches.
/// </summary>
internal static class ShaderTwins
{
    /// <summary>The family a fade copy's shader reports.</summary>
    internal const string FadeFamily = "fade-twin";

    // The one shader per text, and its family.
    private static readonly Dictionary<string, Shader> ByText = new(StringComparer.Ordinal);
    private static readonly Dictionary<Shader, string> FamilyByShader = new(ReferenceEqualityComparer.Instance);
    private static readonly List<ModeShader> All = new();

    // Every material a cache shader was handed to, with the key it wears and whether it wears the
    // fade copy. Held until a later session finds nothing else holds it (ReleaseUnused).
    // ⚠ Hold the wrapper; never a WeakReference or an instance id. Godot remakes a material's C#
    // wrapper while the material lives. A weak one loses materials still drawing, and a wrapper
    // remade from an id can release its material twice at exit.
    private static readonly Dictionary<ShaderMaterial, (ModeShader Twins, bool Fade)> Tracked =
        new(ReferenceEqualityComparer.Instance);

    // The mode the tracked materials last followed, null before the first.
    private static bool? _textEnhanced;

    /// <summary>Gets or sets a value indicating whether an Enhanced frame has drawn in this process.
    /// That is when Godot builds the advanced shader variants its passes need.</summary>
    public static bool EnhancedDrawn { get; set; }

    /// <summary>Gets how many shaders have been made in this process, one per text.</summary>
    public static int Made { get; private set; }

    /// <summary>Gets how many times a text was asked for again and found made, each a compile saved.</summary>
    public static int Reused { get; private set; }

    /// <summary>Gets how many shaders had their text rewritten in this process, each one Godot
    /// compiles again. Only a first switch to Enhanced before one has drawn does it.</summary>
    public static int TextRewrites { get; private set; }

    /// <summary>Gets whether every key has its other mode's shader.</summary>
    public static bool OtherModeWarm => All.All(t => t.Has(!GraphicsMode.Enhanced));

    /// <summary>A key's twins, for a cache whose text reads <see cref="GraphicsMode.Enhanced"/>.
    /// <paramref name="label"/> names the key for an instrument.</summary>
    public static ModeShader Make(Func<string> code, string family, string label)
    {
        EnsureCurrent();
        var twins = new ModeShader(code, family, label);
        All.Add(twins);
        return twins;
    }

    /// <summary>Puts <paramref name="twins"/>' standing shader, or its fade copy, on
    /// <paramref name="material"/> and keeps the material on that key across a switch. Every
    /// material a cache shader goes on passes here, and so does one moved to another key.</summary>
    public static ShaderMaterial Follow(ShaderMaterial material, ModeShader twins, bool fade = false)
    {
        material.Shader = fade ? twins.FadeFor(GraphicsMode.Enhanced) : twins.Current;
        Tracked[material] = (twins, fade);
        return material;
    }

    /// <summary>A copy of <paramref name="source"/> that follows the same key, for a caller that
    /// needs its own material (the cockpit gauges' cells).</summary>
    public static ShaderMaterial Copy(ShaderMaterial source)
    {
        var copy = (ShaderMaterial)source.Duplicate();
        if (Tracked.TryGetValue(source, out var follow))
            Tracked[copy] = follow;
        return copy;
    }

    /// <summary>A translucent copy of <paramref name="source"/> for one instance's fade, its shader
    /// the fade copy of the key it wears. Null when the text has no <c>ALPHA</c> path to take the
    /// line. A material no cache made gets a fade shader of its own text, which no switch moves.
    /// ⚠ Install it per instance, never into the shared caches: ALPHA moves it to the transparent
    /// pass.</summary>
    public static ShaderMaterial? FadeCopy(ShaderMaterial source)
    {
        if (source.Shader is not { } shader || FadeCode(shader.Code) is not { } code)
            return null;
        var copy = (ShaderMaterial)source.Duplicate();
        if (Tracked.TryGetValue(source, out var follow) && !follow.Fade)
            return Follow(copy, follow.Twins, fade: true);
        copy.Shader = Pooled(code, FadeFamily);
        return copy;
    }

    /// <summary>Moves every tracked material onto its key's shader for the current
    /// <see cref="GraphicsMode.Enhanced"/>, making a shader not made yet. No shader's text changes,
    /// so nothing Godot compiled is thrown away. The exception is a first switch to Enhanced before
    /// an Enhanced frame has drawn, which rewrites in place (<see cref="EnhancedDrawn"/>).</summary>
    public static RegenerateStats Regenerate()
    {
        _textEnhanced = GraphicsMode.Enhanced;
        long start = System.Diagnostics.Stopwatch.GetTimestamp();
        int made = Made;
        int retexted = 0;
        // ⚠ Until an Enhanced frame has drawn, Godot has not built the advanced variants its passes
        // need, and building them later covers every live shader. A second twin here would double
        // that, so the one live shader takes the Enhanced text instead.
        if (GraphicsMode.Enhanced && !EnhancedDrawn)
            retexted = RetextInPlace(true);
        var dead = Tracked.Keys.Where(m => !GodotObject.IsInstanceValid(m)).ToList();
        foreach (var material in dead)
            Tracked.Remove(material);
        int moved = 0;
        foreach (var (material, follow) in Tracked)
        {
            var target = follow.Fade ? follow.Twins.FadeFor(GraphicsMode.Enhanced) : follow.Twins.Current;
            if (ReferenceEquals(material.Shader, target))
                continue;
            material.Shader = target;
            moved++;
        }
        return new RegenerateStats(All.Count, Tracked.Count, moved, Made - made, retexted,
            System.Diagnostics.Stopwatch.GetElapsedTime(start).TotalMilliseconds);
    }

    /// <summary>Moves the tracked materials when the mode moved since they last followed. Every cache
    /// getter calls it first, so no cache hands out the other mode's shader.</summary>
    public static void EnsureCurrent()
    {
        if (_textEnhanced != GraphicsMode.Enhanced)
            Regenerate();
    }

    /// <summary>Makes every key's other-mode shader and has Godot compile it now, so a later switch
    /// finds it compiled. <paramref name="budgetMs"/> bounds one call. Returns how many keys it
    /// warmed.</summary>
    public static int WarmOtherMode(double budgetMs = double.PositiveInfinity)
    {
        bool other = !GraphicsMode.Enhanced;
        long start = System.Diagnostics.Stopwatch.GetTimestamp();
        int warmed = 0;
        foreach (var twins in All)
        {
            if (twins.Has(other))
                continue;
            // Asking for the RID has Godot create the shader and start its compile now.
            twins.For(other).GetRid();
            warmed++;
            if (System.Diagnostics.Stopwatch.GetElapsedTime(start).TotalMilliseconds >= budgetMs)
                break;
        }
        return warmed;
    }

    /// <summary>Drops every tracked material nothing but this table holds any more. A new session
    /// calls it before it builds, when the last one's world is gone. ⚠ Never during a build or a
    /// switch: a builder makes its materials before it puts them on meshes.</summary>
    public static void ReleaseUnused()
    {
        var unused = Tracked.Keys.Where(m => !GodotObject.IsInstanceValid(m) || m.GetReferenceCount() <= 1).ToList();
        foreach (var material in unused)
            Tracked.Remove(material);
    }

    /// <summary>Whether some key holds <paramref name="shader"/>. For a suite.</summary>
    public static bool IsKeyShader(Shader? shader) => shader != null && All.Any(t => t.Slots().Contains(shader));

    /// <summary>Whether <paramref name="material"/> follows a key. For a suite.</summary>
    public static bool IsTracked(ShaderMaterial material) => Tracked.ContainsKey(material);

    /// <summary>Whether <paramref name="material"/> follows a key and wears a shader other than that
    /// key's for the standing mode. For a suite.</summary>
    public static bool IsStale(ShaderMaterial material) =>
        Tracked.TryGetValue(material, out var follow) && !ReferenceEquals(material.Shader,
            follow.Fade ? follow.Twins.FadeFor(GraphicsMode.Enhanced) : follow.Twins.Current);

    /// <summary>Which family made a shader, or null for one no cache made. For an instrument.</summary>
    public static string? FamilyOf(Shader? shader) =>
        shader != null && FamilyByShader.TryGetValue(shader, out var family) ? family : null;

    /// <summary>Every shader made, with its family, for an instrument.</summary>
    public static IEnumerable<(Shader Shader, string Family)> Registered() =>
        FamilyByShader.Select(kv => (kv.Key, kv.Value));

    /// <summary>Keys whose standing-mode texts agree, by family, for an instrument: how many keys
    /// share a shader with another key.</summary>
    public static string SharedCensus()
    {
        bool enhanced = GraphicsMode.Enhanced;
        var groups = All.Where(t => t.Has(enhanced)).GroupBy(t => t.Peek(enhanced)!, ReferenceEqualityComparer.Instance)
            .Where(g => g.Count() > 1).ToList();
        var byFamily = groups.GroupBy(g => g.First().Family).Select(f => $"{f.Key}:{f.Sum(g => g.Count() - 1)}");
        var examples = groups.Take(4).Select(g => string.Join("=", g.Select(t => t.Label)));
        return $"keys={All.Count} sharing=[{string.Join(" ", byFamily)}] e.g.=[{string.Join(" ", examples)}]";
    }

    /// <summary>The one shader with this text, made on first use. For a key's twins, and for a
    /// shader whose text no mode moves, which a caller would otherwise make once per instance.</summary>
    internal static Shader Pooled(string text, string family)
    {
        if (ByText.TryGetValue(text, out var known))
        {
            Reused++;
            return known;
        }
        var shader = new Shader { Code = text };
        ByText[text] = shader;
        FamilyByShader[shader] = family;
        Made++;
        return shader;
    }

    // A fade copy's text off its source's: the source with an ALPHA line before its closing brace.
    // Null when the code cannot take it (no csky_opacity preamble, or no col local).
    internal static string? FadeCode(string code)
    {
        if (!code.Contains(SceneBuilder.InstanceUniformsInclude, StringComparison.Ordinal)
            || !code.Contains("vec4 col = ", StringComparison.Ordinal))
            return null;
        int close = code.LastIndexOf('}');
        if (close < 0)
            return null;
        return code[..close] + $"    ALPHA = col.a{SceneBuilder.OpacityTerm};\n" + code[close..];
    }

    // Gives each other-mode shader that only keys lacking this mode hold the first such key's text
    // in place. Every further text among them is made or found. Returns how many it rewrote.
    // ⚠ Never rewrite a shader another key holds in another slot; its text must not move.
    private static int RetextInPlace(bool enhanced)
    {
        var holders = new Dictionary<Shader, int>(ReferenceEqualityComparer.Instance);
        foreach (var twins in All)
        {
            foreach (var shader in twins.Slots())
            {
                if (shader != null)
                    holders[shader] = holders.GetValueOrDefault(shader) + 1;
            }
        }
        var groups = All.Where(t => !t.Has(enhanced) && t.Has(!enhanced))
            .GroupBy(t => t.Peek(!enhanced)!, ReferenceEqualityComparer.Instance);
        int rewrote = 0;
        foreach (var group in groups)
        {
            var from = (Shader)group.Key!;
            bool free = holders[from] == group.Count();
            bool rewritten = false;
            foreach (var twins in group)
            {
                string text = twins.TextFor(enhanced);
                if (free && !rewritten && !ByText.ContainsKey(text))
                {
                    ByText.Remove(from.Code);
                    from.Code = text;
                    ByText[text] = from;
                    TextRewrites++;
                    rewrote++;
                    rewritten = true;
                    twins.Set(enhanced, from);
                }
                else
                {
                    twins.Set(enhanced, Pooled(text, twins.Family));
                }
            }
            // The other mode's shader now carries this mode's text, so no key keeps it there.
            if (rewritten)
            {
                foreach (var twins in group)
                    twins.Forget(!enhanced);
            }
        }
        return rewrote;
    }

    /// <summary>What one <see cref="Regenerate"/> did. It names the keys, the tracked materials and
    /// how many moved, the shaders made, the shaders rewritten and the wall time.</summary>
    internal readonly record struct RegenerateStats(int Entries, int Tracked, int Moved, int TwinsMade, int Retexted, double Ms);
}

/// <summary>
/// One generated shader per graphics mode for one cache key, made on first use. The text is a pure
/// function of the key and <see cref="GraphicsMode.Enhanced"/>, written under the mode it is for.
/// Two keys whose texts agree in a mode share one <see cref="Shader"/> there (<see cref="ShaderTwins"/>).
/// </summary>
internal sealed class ModeShader
{
    private readonly Func<string> _code;
    private Shader? _original;
    private Shader? _enhanced;
    private Shader? _fadeOriginal;
    private Shader? _fadeEnhanced;

    internal ModeShader(Func<string> code, string family, string label)
    {
        _code = code;
        Family = family;
        Label = label;
    }

    /// <summary>Gets the cache family that made it, for an instrument.</summary>
    public string Family { get; }

    /// <summary>Gets the cache key it stands for, for an instrument.</summary>
    public string Label { get; }

    /// <summary>Gets the standing mode's shader.</summary>
    public Shader Current => For(GraphicsMode.Enhanced);

    /// <summary>Whether the mode's shader is made yet.</summary>
    public bool Has(bool enhanced) => Peek(enhanced) != null;

    /// <summary>The mode's shader, made on first use.</summary>
    public Shader For(bool enhanced) => Peek(enhanced) ?? Set(enhanced, ShaderTwins.Pooled(TextFor(enhanced), Family));

    /// <summary>The translucent copy of the mode's shader, its appended <c>ALPHA</c> line driving a
    /// runtime fade. A text with no ALPHA path keeps its piece opaque, so a fade across a switch shows
    /// it whole rather than in the other mode.</summary>
    public Shader FadeFor(bool enhanced)
    {
        if ((enhanced ? _fadeEnhanced : _fadeOriginal) is { } known)
            return known;
        string source = For(enhanced).Code;
        var fade = ShaderTwins.Pooled(ShaderTwins.FadeCode(source) ?? source, ShaderTwins.FadeFamily);
        if (enhanced)
            _fadeEnhanced = fade;
        else
            _fadeOriginal = fade;
        return fade;
    }

    internal Shader? Peek(bool enhanced) => enhanced ? _enhanced : _original;

    internal Shader Set(bool enhanced, Shader shader)
    {
        if (enhanced)
            _enhanced = shader;
        else
            _original = shader;
        return shader;
    }

    internal void Forget(bool enhanced)
    {
        if (enhanced)
            _enhanced = null;
        else
            _original = null;
    }

    // Every shader it holds: each mode's, then each mode's fade copy.
    internal IEnumerable<Shader?> Slots() => new[] { _original, _enhanced, _fadeOriginal, _fadeEnhanced };

    // The text under the mode it is for, the flag put back after.
    internal string TextFor(bool enhanced)
    {
        bool standing = GraphicsMode.Enhanced;
        GraphicsMode.Set(enhanced);
        try
        {
            return _code();
        }
        finally
        {
            GraphicsMode.Set(standing);
        }
    }
}
