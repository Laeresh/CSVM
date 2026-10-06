using System;

namespace CSVM.UI.Boards;

/// <summary>
/// The headset's comfort fade through black, engine-free. Every cut lands on a black frame. A cut
/// the game makes (<c>XrRig.Cut</c>, the first flight frame after a load) snaps to black on its
/// own frame and fades in. A cut the player asks for, a view key, fades out first. The camera
/// asks <see cref="Cover"/> every frame and cuts once it reports black. Once a frame, after the
/// origin is placed, the fade steps, and its alpha is drawn by <see cref="ComfortQuads"/>
/// (docs/architecture/UI.md).
/// </summary>
public sealed class XrFade
{
    /// <summary>Seconds from clear to black ahead of a cut the player asked for. Short, since the
    /// view key waits on it. TUNE at the headset.</summary>
    public const float DefaultOutS = 0.15f;

    /// <summary>Seconds from black back to clear after a cut. TUNE at the headset.</summary>
    public const float DefaultInS = 0.3f;

    // Set by a caller holding the black this frame: a cut that landed, or a cover still asked for.
    // Step clears it, so a black nobody holds fades in on the next frame.
    private bool _held;

    /// <summary>A fade taking <paramref name="outS"/> to black and <paramref name="inS"/> back. A zero
    /// <paramref name="outS"/> covers at once.</summary>
    public XrFade(float outS = DefaultOutS, float inS = DefaultInS)
    {
        OutS = Math.Max(0f, outS);
        InS = Math.Max(1e-3f, inS);
    }

    /// <summary>Where the fade stands.</summary>
    public enum Phase
    {
        /// <summary>Nothing drawn.</summary>
        Clear,

        /// <summary>Darkening ahead of a cut the player asked for.</summary>
        Out,

        /// <summary>Fully black, the frame a cut lands on.</summary>
        Black,

        /// <summary>Clearing after a cut.</summary>
        In,
    }

    /// <summary>Seconds from clear to black.</summary>
    public float OutS { get; }

    /// <summary>Seconds from black to clear.</summary>
    public float InS { get; }

    /// <summary>The phase after the last <see cref="Step"/>, <see cref="Cut"/> or <see cref="Cover"/>.</summary>
    public Phase State { get; private set; } = Phase.Clear;

    /// <summary>The black's opacity this frame, 0 clear to 1 black, as a canvas fade darkens.</summary>
    public float Alpha { get; private set; }

    /// <summary>Asks for black ahead of a cut. True once the view is black, and the caller cuts this
    /// frame. ⚠ Call it every frame until it answers true. A frame without the call lets the black
    /// clear, so a dropped request never leaves the headset dark.</summary>
    public bool Cover()
    {
        _held = true;
        if (State == Phase.Black)
        {
            return true;
        }

        if (OutS <= 0f)
        {
            Alpha = 1f;
            State = Phase.Black;
            return true;
        }

        State = Phase.Out;
        return false;
    }

    /// <summary>The view cut this frame: black now, held through this frame's <see cref="Step"/>,
    /// then clearing over <see cref="InS"/>.</summary>
    public void Cut()
    {
        Alpha = 1f;
        State = Phase.Black;
        _held = true;
    }

    /// <summary>Advances the fade by <paramref name="dt"/> seconds. Called once a frame, after this
    /// frame's cut and cover requests.</summary>
    public void Step(float dt)
    {
        dt = Math.Max(0f, dt);
        switch (State)
        {
            case Phase.Out when _held:
                Alpha = OutS > 0f ? Math.Min(1f, Alpha + (dt / OutS)) : 1f;
                if (Alpha >= 1f)
                {
                    State = Phase.Black;
                }

                break;
            case Phase.Out:
            case Phase.Black when !_held:
            case Phase.In:
                Alpha = Math.Max(0f, Alpha - (dt / InS));
                State = Alpha > 0f ? Phase.In : Phase.Clear;
                break;
        }

        _held = false;
    }
}
