using CSVM.Utils;
using Godot;

namespace CSVM.Testing;

/// <summary>The render half of the fixed-tick simulation, pinned on the rule rather than on any
/// one subsystem's symptom: a realtime frame draws a recorded node BETWEEN its last two simulation
/// poses, a fixed-step frame draws exactly the simulation pose, and the simulation itself never
/// observes a drawn one. The membership assertions are identity checks on the node under test, not
/// a count, because a book that smoothed the wrong node would satisfy any assertion that holds of
/// both (docs/verification.md INSTR-31).</summary>
internal static class RenderPoseSuites
{
    private static readonly Vector3 First = new(100f, 0f, 0f);
    private static readonly Vector3 Second = new(140f, 0f, 0f);

    [Suite("render-poses",
        "a simulation-driven pose is drawn between its last two simulation steps on a realtime " +
        "clock and exactly on its simulation pose on a fixed-step one: the book is keyed on the " +
        "node recorded and leaves every other node alone, a draw lands on the segment between the " +
        "pair at the clock's own physics fraction, the restore every physics callback opens with " +
        "puts the exact simulation pose back so the interpolation cannot feed itself, and a node " +
        "nothing rewrote over a tick is dropped on its final pose rather than tweened toward a " +
        "stale one")]
    internal static void RenderPosesRule(TestContext ctx)
    {
        var saved = GameClock.Current;
        var subject = new Node3D { Name = "subject" };
        var bystander = new Node3D { Name = "bystander" };
        try
        {
            RenderPoses.Clear();

            // --- fixed step: the mode every golden and every scripted capture runs in ----------
            GameClock.Current = new GameClock { Mode = GameClock.RunMode.FixedStep };
            ctx.Same(1000L, (long)(RenderPoses.Fraction * 1000f), $"fixed-step fraction x1000");
            subject.Position = First;
            RenderPoses.Record(subject);
            ctx.Same(0L, RenderPoses.Count, $"fixed-step records nothing");
            subject.Position = Second;
            RenderPoses.Draw();
            ctx.Check(subject.Position == Second, $"fixed-step draw leaves the simulation pose exactly");

            // --- realtime: the only mode where a drawn pose and a simulation pose differ -------
            GameClock.Current = new GameClock { Mode = GameClock.RunMode.Realtime };
            subject.Position = First;
            bystander.Position = First;
            RenderPoses.Record(subject);
            // The identity assertion: dropping THIS node empties the book, so the entry is the
            // subject and not some other node that happens to share its pose.
            ctx.Same(1L, RenderPoses.Count, $"realtime records the node it was handed");
            RenderPoses.Forget(subject);
            ctx.Same(0L, RenderPoses.Count, $"the book is keyed on the subject itself");

            RenderPoses.Record(subject);
            RenderPoses.Restore(1UL);       // opens the next tick: rolls Prev := Curr
            subject.Position = Second;      // the simulation step
            RenderPoses.Record(subject);

            // Driven at chosen fractions, because the engine hands a headless frame 0.000 and a
            // draw pinned only at an endpoint would also pass an implementation that never moved.
            // 40 m between the pair, so each quarter is a whole 10 m of separation.
            foreach (float f in new[] { 0f, 0.25f, 0.5f, 0.75f, 1f })
            {
                RenderPoses.Draw(f, active: true);
                ctx.Check(subject.Position == First.Lerp(Second, f),
                    $"fraction {f:0.00} draws {First.Lerp(Second, f).X:0.0} m, read {subject.Position.X:0.0} m");
                RenderPoses.Restore(1UL);   // back on the simulation pose before the next draw
            }
            ctx.Check(bystander.Position == First, $"a node the book never recorded is untouched");
            ctx.Note($"the frame's own fraction on this host={RenderPoses.Fraction:0.000}");

            // The feedback guard: whatever was drawn, the next physics callback opens on the exact
            // simulation pose, so a held pose or a follower seeded from the node cannot read a
            // drawn one back into the simulation.
            RenderPoses.Restore(1UL);       // the second callback inside the same tick
            ctx.Check(subject.Position == Second, $"the restore returns the exact simulation pose");

            // A motion that ended writes nothing on the next tick, so the node is left on its
            // final simulation pose and dropped rather than tweened back toward the older one.
            RenderPoses.Restore(2UL);       // a fresh tick with no Record following it
            RenderPoses.Draw();
            ctx.Check(subject.Position == Second, $"a finished mover is left on its final pose");
            ctx.Same(0L, RenderPoses.Count, $"a finished mover is dropped from the book");
        }
        finally
        {
            RenderPoses.Clear();
            subject.Free();
            bystander.Free();
            GameClock.Current = saved;
        }
    }
}
