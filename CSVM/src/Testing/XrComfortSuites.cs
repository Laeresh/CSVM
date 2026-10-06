using System;
using System.Collections.Generic;
using System.Text;
using CSVM.Flight.Airframe;
using CSVM.Flight.Camera;
using CSVM.Launch;
using CSVM.Spec;
using CSVM.UI.Boards;
using CSVM.Utils;
using Godot;

namespace CSVM.Testing;

/// <summary>The headset's comfort layers on the stand-in: the fade through black on every cut, the
/// tunnel vignette, and the VR render scale reaching the interface. A real human rig
/// is flown and the comfort quads refreshed after the XR origin, as the composition orders them.
/// Module entries: docs/architecture/UI.md on ComfortQuads, docs/architecture/Flight.md on XrFade and
/// XrVignette.</summary>
internal static class XrComfortSuites
{
    private const float StepDt = 1f / 60f;

    // Level cruise, and a hard roll right held for the whole run.
    private const string Cruise = "0,0,0,0.6";
    private const string Roll = "0,1,0,0.6";

    /// <summary>The first flight frame, a cut the game makes and a view key, each landing on black
    /// and clearing.</summary>
    [Suite("xr-comfort-fade",
        "under a headset the first flight frame, a camera taken by another owner and its hand-back each land on a fully black fade quad that clears over the fade-in, and a view key darkens over the fade-out first, the view changing only on a black frame; the fade quad stands on the headset layer, over the theater unless a board shows")]
    internal static void XrComfortFade(TestContext ctx)
    {
        var report = new StringBuilder();
        bool flown = XrViewSuites.FlyWithHeadset(ctx, PilotViewMode.Cockpit, Cruise, XrChase.Rigid, (clock, plane, rig, _) =>
        {
            bool live = false, board = false;
            var comfort = Comfort(rig, () => live, () => board, () => VrVignette.Off);
            try
            {
                // The rig's first placement is a cut onto the new aircraft, under the load screen.
                Step(clock, plane, rig, comfort);
                ctx.Check(Black(comfort), $"the rig's cut onto the new aircraft lands on black ({comfort.Fade.Alpha})");
                Clear(clock, plane, rig, comfort);
                ctx.Check(comfort.Fade.Alpha == 0f && !comfort.FadeQuad.Visible, $"and clears while the load screen still shows ({comfort.Fade.Alpha})");
                live = true;
                Step(clock, plane, rig, comfort);
                ctx.Check(Black(comfort), $"the first flight frame lands on black ({comfort.Fade.Alpha}, shown {comfort.FadeQuad.Visible}, opacity {Opacity(comfort)})");
                int cleared = Clear(clock, plane, rig, comfort);
                report.AppendLine($"first flight frame: black, clear after {cleared} frames");
                ctx.Check(Within(cleared, XrFade.DefaultInS), $"and clears over the fade-in, {cleared} frames");

                var cuts = new List<string>();
                void OnCut(string why) => cuts.Add(why);
                rig.Cut += OnCut;
                try
                {
                    plane.CameraOwned = true;
                    Step(clock, plane, rig, comfort);
                    ctx.Check(cuts.Count == 1 && Black(comfort), $"another owner taking the camera is a cut that lands on black ({string.Join("; ", cuts)}, {comfort.Fade.Alpha})");
                    Clear(clock, plane, rig, comfort);
                    plane.CameraOwned = false;
                    Step(clock, plane, rig, comfort);
                    ctx.Check(cuts.Count == 2 && Black(comfort), $"and so is the hand-back ({string.Join("; ", cuts)}, {comfort.Fade.Alpha})");
                    Clear(clock, plane, rig, comfort);

                    var view = plane.ViewMode;
                    plane.CameraControl!.StepViewKeys(true, false, false, false, false);
                    float last = -1f;
                    bool rising = true, changedEarly = false;
                    int frames = 0;
                    while (plane.ViewMode == view && frames < 120)
                    {
                        Step(clock, plane, rig, comfort);
                        frames++;
                        if (plane.ViewMode != view)
                        {
                            break;
                        }

                        rising &= comfort.Fade.Alpha > last;
                        changedEarly |= cuts.Count > 2;
                        last = comfort.Fade.Alpha;
                    }

                    report.AppendLine($"view key: {PilotView.Name(view)} to {PilotView.Name(plane.ViewMode)} after {frames} frames, black on the cut frame {Black(comfort)}, cuts {string.Join("; ", cuts)}");
                    ctx.Check(plane.ViewMode == PilotView.Cycle(view), $"the view key lands its view ({PilotView.Name(plane.ViewMode)})");
                    ctx.Check(rising && !changedEarly && Within(frames - 1, XrFade.DefaultOutS),
                        $"the view darkens over the fade-out first, no cut before black ({frames} frames)");
                    ctx.Check(cuts.Count == 3 && Black(comfort), $"and the view changes on a black frame ({comfort.Fade.Alpha}, {cuts.Count} cuts)");
                    cleared = Clear(clock, plane, rig, comfort);
                    report.AppendLine($"view key: clear after {cleared} frames");
                    ctx.Check(Within(cleared, XrFade.DefaultInS), $"then clears over the fade-in, {cleared} frames");
                }
                finally
                {
                    plane.CameraOwned = false;
                    rig.Cut -= OnCut;
                }

                var material = (ShaderMaterial)comfort.FadeQuad.MaterialOverride;
                ctx.Check(comfort.FadeQuad.Layers == SplitScreen.HeadsetLayer && material.RenderPriority == ComfortQuads.FadePriority,
                    $"the fade stands on the headset layer alone, over the theater ({comfort.FadeQuad.Layers}, {material.RenderPriority})");
                board = true;
                Step(clock, plane, rig, comfort);
                ctx.Check(material.RenderPriority == ComfortQuads.FadeUnderBoardPriority
                        && ComfortQuads.FadeUnderBoardPriority < TheaterScreen.SurroundPriority
                        && ComfortQuads.VignettePriority < ComfortQuads.FadeUnderBoardPriority
                        && HeadLockedQuads.PriorityOf(HeadLockedQuads.Tier.MissionEndFade) < ComfortQuads.VignettePriority,
                    $"and under a board's surround while one shows, over every head-locked quad ({material.RenderPriority})");
            }
            finally
            {
                Drop(rig, comfort);
            }
        });
        ctx.Check(flown, $"the cockpit view builds an aircraft to fly");
        ctx.WriteArtifact("test-xr-comfort-fade.txt", report.ToString());
    }

    /// <summary>The vignette: off by default and under any roll, and on, closing in with the
    /// origin's turn rate.</summary>
    [Suite("xr-comfort-vignette",
        "the tunnel vignette is off by default and draws nothing through a hard roll; at Strong it stays open in level cruise and closes in through the roll, its strength following the XR origin's own turn rate and its clear cone shrinking on the quad")]
    internal static void XrComfortVignette(TestContext ctx)
    {
        var report = new StringBuilder();
        ctx.Check(VrVignetteSetting.LevelOf(VrVignetteSetting.Resolve(null, null).Word) == VrVignette.Off,
            $"a launch with nothing saved resolves the vignette off");
        foreach (var (level, hold) in new[] { (VrVignette.Off, Roll), (VrVignette.Strong, Cruise), (VrVignette.Strong, Roll) })
        {
            bool flown = XrViewSuites.FlyWithHeadset(ctx, PilotViewMode.Cockpit, hold, XrChase.Rigid, (clock, plane, rig, _) =>
            {
                var comfort = Comfort(rig, () => true, () => false, () => level);
                try
                {
                    float worstRate = 0f, strongest = 0f, narrowest = XrVignette.OpenHalfAngleDeg;
                    bool shown = false, cone = true;
                    for (int i = 0; i < 120; i++)
                    {
                        Step(clock, plane, rig, comfort);
                        if (i < 30)
                        {
                            continue;
                        }

                        worstRate = Mathf.Max(worstRate, comfort.Vignette.RateDegS);
                        strongest = Mathf.Max(strongest, comfort.Vignette.Strength);
                        narrowest = Mathf.Min(narrowest, comfort.VignetteHalfAngleDeg);
                        shown |= comfort.VignetteQuad.Visible;
                        if (comfort.VignetteQuad.Visible)
                        {
                            float inner = ((ShaderMaterial)comfort.VignetteQuad.MaterialOverride).GetShaderParameter("inner_deg").AsSingle();
                            cone &= Mathf.Abs(inner - comfort.VignetteHalfAngleDeg) < 0.02f;
                        }
                    }

                    string name = $"{level} {(hold == Roll ? "roll" : "cruise")}";
                    report.AppendLine($"{name}: origin rate up to {worstRate:0.#} deg/s, strength up to {strongest:0.###}, clear cone down to {narrowest:0.#} deg, quad shown {shown}");
                    if (level == VrVignette.Off)
                    {
                        ctx.Check(worstRate == 0f && strongest == 0f && !shown, $"{name}: off draws nothing ({strongest:0.###}, shown {shown})");
                        return;
                    }

                    if (hold == Cruise)
                    {
                        ctx.Check(worstRate < XrVignette.OnsetDegS && strongest < 0.05f,
                            $"{name}: level flight turns the view under the onset and leaves the view open ({worstRate:0.#} deg/s, {strongest:0.###})");
                        return;
                    }

                    ctx.Check(worstRate > XrVignette.OnsetDegS, $"{name}: the roll turns the origin at {worstRate:0.#} deg/s");
                    float expect = XrVignette.TargetStrength(worstRate);
                    ctx.Check(shown && cone && strongest > 0.5f * expect && strongest <= expect + 1e-3f,
                        $"{name}: the vignette closes in with the rate ({strongest:0.###} of the curve's {expect:0.###}), the quad's cone {narrowest:0.#} deg");
                }
                finally
                {
                    Drop(rig, comfort);
                }
            });
            ctx.Check(flown, $"the cockpit view builds an aircraft to fly ({level})");
        }

        ctx.WriteArtifact("test-xr-comfort-vignette.txt", report.ToString());
    }

    /// <summary>The VR render scale: an option word reaches the stand-in interface's eye size, and
    /// back.</summary>
    [Suite("xr-render-scale",
        "the VR render scale resolves its flag over the saved word, and the resolved scale reaches the interface's render target size as the runtime's multiplier would: 70 % of a 1600x1200 eye is 1120x840, and 100 % restores it")]
    internal static void XrRenderScale(TestContext ctx)
    {
        var spec = XrSimSpec.Parse("eye=1600x1200", new List<string>());
        // A RefCounted interface, never registered with the server: it goes with its last reference.
        var sim = new XrSimInterface(spec);
        var word = VrRenderScaleSetting.Resolve(null, "70");
        var eye = XrSession.ApplyRenderScale(sim, VrRenderScaleSetting.ScaleOf(word.Word));
        ctx.Check(word.Source == SettingSource.Saved && eye == new Vector2(1120f, 840f) && sim.GetRenderTargetSize() == eye,
            $"the saved 70 reaches the interface as a 1120x840 eye ({word.Word} from {word.Source}, {eye})");
        word = VrRenderScaleSetting.Resolve("125", "70");
        eye = XrSession.ApplyRenderScale(sim, VrRenderScaleSetting.ScaleOf(word.Word));
        ctx.Check(word.Source == SettingSource.Flag && eye == new Vector2(2000f, 1500f),
            $"the flag beats it ({word.Word}, {eye})");
        eye = XrSession.ApplyRenderScale(sim, VrRenderScaleSetting.ScaleOf(VrRenderScaleSetting.Default));
        ctx.Check(eye == spec.EyePx, $"and the default restores the flag's own eye ({eye})");
    }

    // The quads wired to the rig as the composition wires them. They stay out of the tree: the suite
    // refreshes them itself, after the origin, on every stepped frame.
    private static ComfortQuads Comfort(XrRig rig, Func<bool> live, Func<bool> board, Func<VrVignette> level)
    {
        var fade = new XrFade();
        rig.Fade = fade;
        var comfort = ComfortQuads.Build(fade, () => rig.Origin.GlobalTransform, live, board, level);
        rig.Cut += comfort.OnCut;
        return comfort;
    }

    private static void Drop(XrRig rig, ComfortQuads comfort)
    {
        rig.Cut -= comfort.OnCut;
        rig.Fade = null;
        comfort.Free();
    }

    // A live frame's order: the sim step, the camera step, the XR origin after every writer, then
    // the comfort quads after the theater.
    private static void Step(GameClock clock, FlightController plane, XrRig rig, ComfortQuads comfort)
    {
        clock.BeginFrame(StepDt);
        plane.SimStep(StepDt);
        plane._Process(StepDt);
        rig.Place();
        comfort.Refresh(StepDt);
    }

    // Steps until the fade has cleared and the quad is hidden; the frame count.
    private static int Clear(GameClock clock, FlightController plane, XrRig rig, ComfortQuads comfort)
    {
        int frames = 0;
        while ((comfort.Fade.Alpha > 0f || comfort.FadeQuad.Visible) && frames < 240)
        {
            Step(clock, plane, rig, comfort);
            frames++;
        }

        return frames;
    }

    private static bool Black(ComfortQuads comfort) =>
        comfort.Fade.Alpha == 1f && comfort.FadeQuad.Visible && Opacity(comfort) == 1f;

    private static float Opacity(ComfortQuads comfort) =>
        ((ShaderMaterial)comfort.FadeQuad.MaterialOverride).GetShaderParameter("opacity").AsSingle();

    // Whether a frame count spans a duration at the suite's step, within a frame and a half.
    private static bool Within(int frames, float seconds) =>
        Mathf.Abs((frames * StepDt) - seconds) <= (StepDt * 1.5f);
}
