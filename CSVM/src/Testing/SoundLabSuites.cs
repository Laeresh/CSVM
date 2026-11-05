using System.IO;
using System.Linq;
using CSVM.Flight.Audio;
using CSVM.Launch;
using CSVM.Mech3;
using CSVM.Session.World;
using CSVM.Spec;
using CSVM.Tooling;
using CSVM.UI.Labs;
using CSVM.Utils;
using Godot;

namespace CSVM.Testing;

/// <summary>The sound lab over a real flight. It is built under <c>--sound-lab</c> alone and opened
/// by its key. Its sliders reach the flown engine slot on the next step, and its Save reads back
/// through the tuning file's own reader.</summary>
internal static class SoundLabSuites
{
    private const int Steps = 4;

    [Suite("sound-lab",
        "the sound lab: a flight without --sound-lab builds none and Shift+F9 changes nothing, a "
        + "--sound-lab flight builds one closed lab that Shift+F9 opens; under Enhanced Sound a slider "
        + "flattening the engine pitch or gain curve moves the flown engine slot's pitch or gain to it on "
        + "the next step and the readout names them, while under Original Sound the slot keeps the "
        + "decoded pitch; Save writes every value into a copy of the tuning file, keeps its note, reads "
        + "back to the edited tuning and leaves the shipped file alone")]
    internal static void SoundLabTuning(TestContext ctx)
    {
        ctx.RequireData(ctx.Data.Planes, $"planes gamez");
        ctx.RequireData(ctx.Zrdr, $"zrdr archive");
        ctx.RequireData(ctx.Data.Sounds, $"sound archive (soundsh)");

        var clockWas = GameClock.Current;
        bool wasEnhanced = SoundModeSetting.EnhancedOn;
        var tuningWas = EnhancedSoundTuning.Current;
        byte[] shipped = Godot.FileAccess.GetFileAsBytes(EnhancedSoundTuning.DefaultPath);
        try
        {
            var plain = Open(ctx, false);
            try
            {
                plain.Pane.PushInput(ShiftF9());
                ctx.Check(plain.Built && plain.Session.FindChild("sound_lab", true, false) == null
                          && EnhancedSoundTuning.Current == tuningWas,
                    $"a flight without --sound-lab builds no sound lab and Shift+F9 changes nothing (built={plain.Built})");
            }
            finally
            {
                plain.Close();
            }

            var flight = Open(ctx, true);
            try
            {
                if (!flight.Built || flight.Session.FindChild("sound_lab", true, false) is not SoundLab lab
                    || flight.Session.Rigs[0].Controller is not { Audio: { } audio, Stats: { } stats })
                {
                    ctx.Check(false, $"a --sound-lab flight builds with a sound lab and a flown aircraft that has a mix (built={flight.Built})");
                    return;
                }

                ctx.Check(!lab.IsOpen, $"the lab starts closed");
                flight.Pane.PushInput(ShiftF9());
                ctx.Check(lab.IsOpen, $"Shift+F9 opens it");
                Live(ctx, flight, lab, audio, EngineAudioCurves.SlotIsPitched(SoundDefs.Load(ctx.Zrdr), stats.EngineSound));
                SaveRoundTrip(ctx, lab, stats.BulletHitSound);
            }
            finally
            {
                flight.Close();
            }

            ctx.Check(Godot.FileAccess.GetFileAsBytes(EnhancedSoundTuning.DefaultPath).SequenceEqual(shipped),
                $"the shipped tuning file is unchanged");
        }
        finally
        {
            SoundModeSetting.EnhancedOn = wasEnhanced;
            EnhancedSoundTuning.Current = tuningWas;
            GameClock.Current = clockWas;
        }
    }

    // Each slider sets a flat curve, so the slot's output is that value whatever the throttle and
    // the manoeuvre terms add to the curve's parameter.
    private static void Live(TestContext ctx, Ends flight, SoundLab lab, FlightAudio audio, bool pitched)
    {
        SoundModeSetting.EnhancedOn = false;
        Step(flight);
        float decoded = audio.EnginePitch;
        lab.SliderFor("enginePitchIdle").Value = 1.7;
        lab.SliderFor("enginePitchFull").Value = 1.7;
        Step(flight);
        // Near rather than equal: the decoded curve's manoeuvre terms move with the flight between reads.
        ctx.Check(Mathf.Abs(audio.EnginePitch - decoded) < 0.05f && !Mathf.IsEqualApprox(audio.EnginePitch, 1.7f),
            $"under Original Sound the moved sliders leave the engine slot on the decoded pitch ({audio.EnginePitch:0.000}, was {decoded:0.000})");

        SoundModeSetting.EnhancedOn = true;
        foreach (float pitch in new[] { 0.8f, 1.6f })
        {
            lab.SliderFor("enginePitchIdle").Value = pitch;
            lab.SliderFor("enginePitchFull").Value = pitch;
            Step(flight);
            float want = pitched ? pitch : 1f;
            ctx.Check(Mathf.IsEqualApprox(audio.EnginePitch, want),
                $"the pitch sliders at {pitch:0.00} put the live engine slot at pitch {audio.EnginePitch:0.000} (want {want:0.000}, slot pitched {pitched})");
        }

        var gains = new float[2];
        var levels = new[] { 0.5f, 2f };
        for (int i = 0; i < levels.Length; i++)
        {
            lab.SliderFor("engineGainIdle").Value = levels[i];
            lab.SliderFor("engineGainFull").Value = levels[i];
            Step(flight);
            gains[i] = audio.EngineGain;
        }

        float ratio = gains[0] > 0f ? gains[1] / gains[0] : 0f;
        ctx.Check(Mathf.Abs(ratio - (levels[1] / levels[0])) < 0.01f,
            $"the gain sliders from {levels[0]:0.0} to {levels[1]:0.0} scale the live engine gain {gains[0]:0.000} to {gains[1]:0.000} (x{ratio:0.000})");
        lab._Process(1.0);
        ctx.Check(lab.ReadoutText.Contains(System.FormattableString.Invariant($"gain {audio.EngineGain:0.000}"), System.StringComparison.Ordinal)
                  && lab.ReadoutText.Contains("Enhanced Sound", System.StringComparison.Ordinal),
            $"the readout names the live slot: '{lab.ReadoutText.Replace('\n', ' ')}'");
    }

    private static void SaveRoundTrip(TestContext ctx, SoundLab lab, string confirmGroup)
    {
        string dir = Path.Combine(ctx.ScratchDir, "sound-lab");
        Directory.CreateDirectory(dir);
        string copy = Path.Combine(dir, "enhanced_sound.json");
        File.WriteAllBytes(copy, Godot.FileAccess.GetFileAsBytes(EnhancedSoundTuning.DefaultPath));
        lab.SavePath = copy;

        lab.SliderFor("hitConfirmInterval").Value = 0.37;
        // The lowest stop, so a value at the slider's end must parse back inside the read range.
        var lowest = lab.SliderFor("hitConfirmPitch");
        lowest.Value = lowest.MinValue;
        lab.SetHitConfirmSound(confirmGroup);
        var edited = lab.Edited;
        string? written = lab.Save();
        var problems = new System.Collections.Generic.List<string>();
        var read = EnhancedSoundTuning.Parse(File.ReadAllBytes(copy), problems);
        ctx.Check(written == copy && problems.Count == 0 && read == lab.Saved && read == lab.Edited
                  && read == EnhancedSoundTuning.Current && read == edited.Written(),
            $"Save reads back to the edited tuning: {read.Describe()} (edited {edited.Describe()}, {problems.Count} problem(s))");
        ctx.Check(read.HitConfirmInterval == 0.37f && read.HitConfirmSound == confirmGroup
                  && File.ReadAllText(copy).Contains("\"_about\"", System.StringComparison.Ordinal),
            $"the saved file holds the moved interval and group and keeps its note");
    }

    private static InputEventKey ShiftF9() => new() { Keycode = Key.F9, Pressed = true, ShiftPressed = true };

    // The sim steps, then one render frame of the flown aircraft, which is where its mix is written.
    private static void Step(Ends flight)
    {
        for (int i = 0; i < Steps; i++)
        {
            flight.Session._PhysicsProcess(GameClock.FixedDt);
        }

        flight.Session.Rigs[0].Controller!._Process(GameClock.FixedDt);
    }

    // One flight on the empty stage in its own pane, which renders nothing: the suite reads the mix.
    private static Ends Open(TestContext ctx, bool soundLab)
    {
        var args = new[] { "--stage=empty", "--det", "--no-pads" };
        var spec = SessionSpec.Parse(soundLab ? args.Append("--sound-lab") : args);
        var pane = new SubViewport
        {
            Size = new Vector2I(640, 480),
            OwnWorld3D = true,
            World3D = new World3D(),
            RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled,
        };
        var camera = new Camera3D { Fov = 60f, Far = 20000f };
        var sun = new DirectionalLight3D { RotationDegrees = new Vector3(-45, 150, 0) };
        pane.AddChild(camera);
        pane.AddChild(sun);
        ctx.Host.AddChild(pane);
        var paths = new BasePaths(RepoRoot: ctx.RepoRoot, DataRoot: ctx.DataRoot) { Game = ctx.Game, Data = ctx.Data };
        var session = new GameSession(spec, new LauncherContext
        {
            Decode = ctx.Decode,
            Paths = paths,
            ProbeRunner = new ProbeRunner(paths),
            CaptureDirector = new CaptureDirector(spec),
            MasterSeed = 1,
            Camera = camera,
            Orbit = new Flight.Camera.OrbitCamera(camera),
            Sun = sun,
            Env = new Godot.Environment(),
            MenuDriven = false,
            MenuPads = null,
            ExitSession = () => { },
            RestartSession = () => { },
        });
        pane.AddChild(session);
        return new Ends(pane, session, session.StartSession());
    }

    private sealed record Ends(SubViewport Pane, GameSession Session, bool Built)
    {
        public void Close()
        {
            Session.Free();
            Pane.Free();
        }
    }
}
