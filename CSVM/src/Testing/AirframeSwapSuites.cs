using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CSVM.Flight;
using CSVM.Mech3;
using CSVM.Session;
using CSVM.UI;
using CSVM.Utils;
using Godot;

namespace CSVM.Testing;

/// <summary>The suite over the mission-script host's airframe swap, callback codes 965 to 967: the
/// code CM02's own capture animation authors, driven against that mission's built world, with the
/// player's rig coming off the session's own roster so what is swapped is the flown aircraft.</summary>
internal static class AirframeSwapSuites
{
    // CM02's story position, and the airframe the swap is driven FROM. A Bloodhawk is the campaign's
    // own starting aircraft and shares none of the Balmoral's fit, so every reading below separates
    // the two airframes rather than reading the same numbers twice.
    private const int Cm02Seq = 1;
    private const string StartPlane = "player_bhawk";

    // Where the rig is flown from, and how close the replacement has to land to it. The tolerances
    // are a swap's, not a simulation's: the aircraft is rebuilt at the pose the outgoing one held,
    // so the two readings differ only by the spawn placement's own rounding.
    private const float PoseTolerance = 1f;
    private const float SpeedTolerance = 2f;

    // The hand-over's own tolerances. The placement is arithmetic off one pose, so metres and
    // degrees are generous; the pools are floats carried through two divisions.
    private const float RangeTolerance = 1f;
    private const float BearingTolerance = 1f;
    private const float FractionTolerance = 0.02f;
    private const float PoolTolerance = 0.5f;

    // The played leg's frame. Long enough to outlast the wing walk's own 19.25 s motion, which is
    // what the capture's called definition raises its reveal behind.
    private const float StepDt = 1f / 60f;
    private const float PlayBudgetS = 30f;

    /// <summary>Drives the swap CM02 authors against CM02's own built world: the code comes out of
    /// the mission's compiled definitions, the rig off the session's roster, and the replacement is
    /// read for the named airframe's own stock fit, hardpoint table and armour rather than the
    /// airframe it replaced.</summary>
    internal static void AirframeSwap(TestContext ctx)
    {
        ctx.RequireData(ctx.ZrdrPath, $"zrdr archive");
        ctx.RequireData(ctx.PlanesGamezPath, $"planes gamez");
        var mission = MissionOf(CampaignSequence.Load(ctx.ZrdrPath), Cm02Seq)
            ?? throw new SuiteSkippedException($"cm_sequence carries no story position {Cm02Seq}");
        string chapter = mission.ChapterFolder.ToUpperInvariant();
        string folder = mission.MissionFolder.ToUpperInvariant();
        ctx.RequireData(SessionPaths.ChapterTextures(ctx.DataRoot, chapter), $"{chapter} textures");
        ctx.RequireData(SessionPaths.MissionZrdr(ctx.DataRoot, chapter, folder), $"{chapter}/{folder} zrdr");

        var report = new StringBuilder();
        report.AppendLine($"seq {Cm02Seq} -> {chapter}/{folder}");
        CheckTable(ctx, report);
        CheckHandoverPlan(ctx, mission, report);
        ctx.CutsceneRoots = true;
        try
        {
            ctx.WithWorld(chapter, collision: false, folder,
                world => Drive(ctx, world, chapter, folder, report));
        }
        finally
        {
            ctx.CutsceneRoots = false;
        }

        ctx.WriteArtifact($"test-airframe-swap-{chapter}-{folder}.txt", report.ToString());
        ctx.Note($"drove the swap {chapter}/{folder} authors against that mission's own world");
    }

    // The decoded table against the shipped stat data: each code's def/node pair has to be the pair
    // the airframe itself carries, or the swap would build one airframe and fit another's weapons.
    private static void CheckTable(TestContext ctx, StringBuilder report)
    {
        var seen = new List<int>();
        foreach (var entry in AirframeSwapCodes.Table)
        {
            var stats = PlaneStats.Load(ctx.ZrdrPath, entry.PlaneNode);
            report.AppendLine($"code {entry.Code}: '{entry.PlaneNode}' def '{stats.DefName}' " +
                $"(decoded '{entry.Def}')");
            ctx.Check(string.Equals(stats.DefName, entry.Def, StringComparison.OrdinalIgnoreCase),
                $"code {entry.Code}'s '{entry.PlaneNode}' is the shipped def '{entry.Def}' the decode names");
            ctx.Check(!seen.Contains(entry.Code), $"and code {entry.Code} names exactly one airframe");
            seen.Add(entry.Code);
        }

        ctx.Same(3, seen.Count, $"the host answers three swap codes, one per airframe the data names");
        ctx.Check(AirframeSwapCodes.For(11) == null,
            $"and a cutscene code that is not a swap names no airframe, so the two vocabularies stay apart");
    }

    // Step 5's mission gate and its placement, read off CM02's own roster rather than named here.
    // The hand-over is keyed on chapter and mission strings inside the executable, so nothing in
    // the shipped data asks for it and only the exe decode says these two missions are special.
    private static void CheckHandoverPlan(TestContext ctx, CampaignMission mission,
        StringBuilder report)
    {
        ctx.Check(AirframeHandover.Resolves(mission.ChapterFolder, mission.MissionFolder),
            $"'{AirframeHandover.WingmanName}' resolves in {mission.ChapterFolder}/{mission.MissionFolder}, one of the two missions the exe names");
        ctx.Check(!AirframeHandover.Resolves(mission.ChapterFolder, "m01"),
            $"and in no other mission of the same chapter, so the hand-over is not a chapter-wide rule");

        var blocks = AiSkills.LoadRoster(SessionPaths.MissionZrdr(ctx.DataRoot,
            mission.ChapterFolder.ToUpperInvariant(), mission.MissionFolder.ToUpperInvariant()));
        var defs = VehicleDefs.Load(ctx.ZrdrPath);
        var nets = Array.Empty<AiNet>();
        var own = PlanNamed(CampaignRosterPlan.Build(blocks, defs, nets),
            AirframeHandover.WingmanName);
        var handed = PlanNamed(
            CampaignRosterPlan.Build(blocks, defs, nets,
                handover: new FlyingAirframe(StartPlane, null)),
            AirframeHandover.WingmanName);
        report.AppendLine($"{AirframeHandover.WingmanName}: own def flies '{own?.PlaneNode ?? "-"}', " +
            $"handed over it flies '{handed?.PlaneNode ?? "-"}'");
        ctx.Check(own != null && handed != null,
            $"the mission's own roster carries a '{AirframeHandover.WingmanName}' block for the swap to hand over to");
        ctx.Check(own!.Inert,
            $"authored deactivated, so it is built out of the world until the swap reveals it");
        ctx.Check(string.Equals(handed!.PlaneNode, StartPlane, StringComparison.OrdinalIgnoreCase),
            $"and in this mission it flies the player's own '{StartPlane}', which is the aeroplane it is about to be given");
        ctx.Check(!string.Equals(own.PlaneNode, StartPlane, StringComparison.OrdinalIgnoreCase),
            $"rather than the '{own.Def}' def's own airframe it flies everywhere else");
    }

    private static void Drive(TestContext ctx, TestWorld world, string chapter, string folder,
        StringBuilder report)
    {
        var authored = SwapCallsIn(world.Session.Program);
        foreach (var (anim, code, root) in authored)
        {
            report.AppendLine($"'{root}-{anim}' authors callback {code} -> " +
                $"'{AirframeSwapCodes.For(code)!.Value.PlaneNode}'");
        }

        ctx.Check(authored.Count > 0,
            $"{chapter}/{folder} authors a swap callback of its own, so the mission needs one");
        var wanted = AirframeSwapCodes.For(authored[0].Code)!.Value;
        ctx.Check(string.Equals(wanted.PlaneNode, "player_balmoral", StringComparison.Ordinal),
            $"and the airframe it names is the Balmoral the mission's capture hands the player");

        WithStagedCapture(ctx, world, chapter, folder, wanted, authored[0],
            staged => RunSwap(ctx, world, staged, wanted, authored[0], report));
        WithStagedCapture(ctx, world, chapter, folder, wanted, authored[0],
            staged => PlayTheCapture(ctx, world, staged, authored[0], report));
    }

    // The team CM02's own roster block authors for the capture, so the stand-in resolves its paint
    // exactly the way a real enemy spawn does (CampaignRoster.SpawnFor's ShippedSkins fork), rather
    // than through a scheme this suite would otherwise have to invent.
    private static int CapturedTeamFor(TestContext ctx, string chapter, string folder, string blockName)
    {
        var blocks = AiSkills.LoadRoster(SessionPaths.MissionZrdr(ctx.DataRoot, chapter, folder));
        foreach (var (name, fields) in blocks)
        {
            if (string.Equals(name, blockName, StringComparison.OrdinalIgnoreCase))
            {
                return AiSkills.RosterTeam(fields) ?? AimAssist.PlayerTeam + 1;
            }
        }

        return AimAssist.PlayerTeam + 1;
    }

    // One staged capture: the mission's own world, a flown human rig off the session's roster, the
    // capture animation's aircraft and the hand-over block, then the leg. Two legs share it because
    // a swap replaces the rig it ran on, so neither can read the other's world back.
    private static void WithStagedCapture(TestContext ctx, TestWorld world, string chapter, string folder,
        AirframeSwapCode wanted, (string Anim, int Code, string Root) call, Action<Staged> leg)
    {
        var textures = new TextureArchive(SessionPaths.ChapterTextures(ctx.DataRoot, chapter));
        var pool = new ProjectilePool(textures, null, null);
        ctx.Host.AddChild(pool);
        var pane = new SubViewport();
        ctx.Host.AddChild(pane);
        var rig = new PlayerRig { Index = 0, Camera = ctx.Camera, HudParent = pane, Viewport = pane };
        var rigs = new[] { rig };
        FlightRoster? roster = null;
        var cutscene = new CutsceneController();
        ctx.Host.AddChild(cutscene);
        try
        {
            roster = BuildRoster(ctx, world, chapter, textures, pool, rigs);
            roster.BuildPlayers(rigs);
            var before = rig.Controller ?? throw new InvalidOperationException("no rig was built");
            // A real enemy roster spawn, not an override: team != the player's own, so the AI
            // assembler resolves its paint through ShippedSkins exactly as a real capture would.
            int capturedTeam = CapturedTeamFor(ctx, chapter, folder, call.Root);
            var captured = StageAi(roster, call.Root, wanted.PlaneNode,
                before.WorldPosition + (before.NoseDirection * 300f), inert: false,
                team: capturedTeam, shippedSkins: true);
            var wingman = StageAi(roster, AirframeHandover.WingmanName, StartPlane,
                before.WorldPosition + new Vector3(0f, 0f, 5000f), inert: true);
            Damage(wingman, 0.10f);
            leg(new Staged(roster, rig, before, cutscene, captured, wingman, pool));
        }
        finally
        {
            // Only what this suite put on the host, the two AI rigs it staged included: the host is
            // shared with every suite in the run, and a leaked 'wingman_4' renames the next one.
            var live = rig.Controller;
            var members = new List<FlightController>(roster?.AiAircraft ?? Array.Empty<FlightController>());
            roster?.ClearMembership();
            live?.Free();
            foreach (var ai in members)
            {
                ai.Free();
            }

            cutscene.Free();
            pane.Free();
            pool.Free();
            textures.Dispose();
        }
    }

    // The swap itself, driven through the host the runtime dispatches a CALLBACK to, then read on
    // both sides: what the aircraft was, what it became, and what carried across.
    private static void RunSwap(TestContext ctx, TestWorld world, Staged staged,
        AirframeSwapCode wanted, (string Anim, int Code, string Root) call, StringBuilder report)
    {
        var (roster, rig, before, cutscene, captured, wingman, pool) = staged;
        string anim = call.Anim;
        cutscene.BindWorld(world.Runtime);
        cutscene.BindRigs(rigs: new[] { rig }, aiPlanes: Array.Empty<FlightController>);
        cutscene.HostDefinitions(new[] { anim });
        // The whole swap, the way the session runs it: this mission is one of the two that resolve
        // the hand-over name, so the seam is armed for it here too.
        cutscene.SwapAirframe = order => roster.RunSwap(rig, order, handsOver: true);

        var wasPos = before.WorldPosition;
        var wasNose = before.NoseDirection;
        float wasSpeed = before.WorldVelocity.Length();
        report.AppendLine(Describe("before", before));
        ctx.Check(before.Loadout != null && before.Damage != null,
            $"the flown aircraft arrives with a bound fit and a damage ledger to swap out of");
        string wasDef = before.Loadout!.Def.Def;
        ctx.Check(!string.Equals(wasDef, wanted.Def, StringComparison.OrdinalIgnoreCase),
            $"and it is not already '{wanted.Def}', so the swap has something to change");

        var (armorLeft, healthLeft) = Damage(captured, 0.25f);
        float outgoingArmor = before.Damage!.WholeArmor;
        float outgoingHealth = before.Damage!.WholeHealth;
        report.AppendLine($"capture '{call.Root}': armour {armorLeft * 100f:0}% structure {healthLeft * 100f:0}% of its own maxima");
        int registered = pool.NearMissTargets.Count;

        ctx.Check(cutscene.Host(wanted.Code, anim, call.Root),
            $"the mission-script host answers callback {wanted.Code} rather than declining it");
        var after = rig.Controller ?? throw new InvalidOperationException("the swap built no aircraft");
        report.AppendLine(Describe("after", after));
        CheckCapturedHull(ctx, captured, after, armorLeft, healthLeft, report);
        CheckLivery(ctx, before, captured, after, report);
        CheckHandover(ctx, wingman, wasPos, wasNose, outgoingArmor, outgoingHealth, report);

        ctx.Check(!ReferenceEquals(before, after),
            $"the swap replaces the player's aircraft rather than editing the one that was flying");
        ctx.Check(!GodotObject.IsInstanceValid(before) || !before.IsInsideTree(),
            $"and the aircraft it replaced is out of the world, not left flying beside it");

        CheckAirframe(ctx, ctx.ZrdrPath, after, wanted, wasDef, report);
        ctx.Check(after.WorldPosition.DistanceTo(wasPos) < PoseTolerance,
            $"the replacement starts where the aircraft it replaced was flying");
        ctx.Check(after.NoseDirection.Dot(wasNose) > 0.99f,
            $"pointed the way it was pointed");
        ctx.Check(Mathf.Abs(after.WorldVelocity.Length() - wasSpeed) < SpeedTolerance,
            $"and at the speed it was flying, so the swap is not a respawn");
        ctx.Same(before.PlayerIndex, after.PlayerIndex,
            $"the pilot keeps their shooter id, which is what a round already in the air scores to");
        ctx.Same(registered, pool.NearMissTargets.Count,
            $"and the pool holds the registrations it held before, so the old rig left exactly one behind and the replacement took its place");

        // The cutscene flags codes 965 to 967 set are the player vehicle's own +0x91d/+0x91e pair
        // and the chrome off: the state code 11 and code 2 assert between them
        // (docs/formats/anim-definitions/cutscenes.md).
        ctx.Check(cutscene.OutOfFlight && cutscene.Presenting,
            $"the swap sets the cutscene flags, so the player is out of flight with the chrome down while the capture plays");
        ctx.Check(after.Held && after.Inert,
            $"and that state lands on the aircraft the swap built, not on the one it replaced");
    }

    // The capture as a flown session runs it: the mission's own definition played through the
    // runtime on a realtime clock, with the AI list the park and reveal codes actually see. The
    // dispatched leg raises 967 by itself, so it can see neither the 913/914 pair the capture's own
    // wing walk wraps that code in; this leg isolates the authored swap choreography.
    private static void PlayTheCapture(TestContext ctx, TestWorld world, Staged staged,
        (string Anim, int Code, string Root) call, StringBuilder report)
    {
        var (roster, rig, before, cutscene, captured, _, _) = staged;
        Damage(captured, 0.25f);
        var savedClock = GameClock.Current;
        var savedHost = world.Runtime.CallbackHost;
        try
        {
            var clock = new GameClock { Mode = GameClock.RunMode.Realtime };
            GameClock.Current = clock;
            cutscene.BindWorld(world.Runtime);
            cutscene.HostDefinitions(ClosureOf(world, call.Anim));
            cutscene.BindRigs(new[] { rig }, () => roster.AiAircraft);
            cutscene.SwapAirframe = order => roster.RunSwap(rig, order, handsOver: true);
            world.Runtime.CallbackHost = cutscene.Host;
            world.Runtime.Play(call.Anim);

            bool swapped = false;
            bool hiddenAtSwap = false;
            bool shownAgain = false;
            float shownAtS = -1f;
            for (int i = 0; i < (int)(PlayBudgetS / StepDt); i++)
            {
                clock.BeginFrame(StepDt);
                world.Runtime.Advance(StepDt);
                cutscene.Tick();
                if (!swapped && !ReferenceEquals(rig.Controller, before))
                {
                    swapped = true;
                    hiddenAtSwap = captured.Inert;
                }

                // ⚠ Read the hidden bit, never InPlay: a stand-in flown into the sea reads out of
                // the world for a reason this leg is not about (INSTR-10).
                if (swapped && !captured.Inert && !shownAgain)
                {
                    shownAgain = true;
                    shownAtS = i * StepDt;
                }
            }

            report.AppendLine($"played '{call.Anim}' for {PlayBudgetS:0} s on a realtime clock: " +
                $"codes {string.Join(",", cutscene.Codes)}, swapped={swapped} " +
                $"hidden-at-swap={hiddenAtSwap} shown-again={shownAgain} at {shownAtS:0.##} s, " +
                $"'{call.Root}' end inert={captured.Inert} crashed={captured.Crashed}");
            ctx.Check(swapped,
                $"playing the mission's own '{call.Anim}' reaches callback {call.Code} through the runtime, the way a flown session reaches it");
            ctx.Check(hiddenAtSwap,
                $"and the capture animation's own aircraft is hidden as the swap runs");
            ctx.Check(!shownAgain,
                $"and stays hidden for the rest of the episode, rather than coming back in front of the player when the wing walk reveals the parked AI");
        }
        finally
        {
            world.Runtime.CallbackHost = savedHost;
            GameClock.Current = savedClock;
        }
    }

    // Every definition the capture can reach, its own plus the CALL_ANIMATION closure: what the
    // session's own host answers for, and the only way the called wing walk's codes are hosted.
    private static IReadOnlyList<string> ClosureOf(TestWorld world, string anim)
    {
        var names = new List<string>();
        foreach (var def in world.Session.Program.Subset(new[] { anim }).Defs)
        {
            if (def.AnimName is { } name && !names.Contains(name))
            {
                names.Add(name);
            }
        }

        return names;
    }

    // One roster aircraft standing in for a block this suite's world builds no roster for: the
    // capture animation's own aircraft, and the block the outgoing aeroplane is handed to. Their
    // names are what the swap resolves them by, and that is all it reads them for.
    // ⚠ Staged facing BACKWARDS and damaged where the reveal is the subject: an aircraft parked
    // on the player's own heading with full pools passes the placement and pool checks without the
    // hand-over having run at all (INSTR-10).
    private static FlightController StageAi(FlightRoster roster, string name, string planeNode,
        Vector3 at, bool inert, PaintScheme? scheme = null, int team = AimAssist.PlayerTeam,
        bool shippedSkins = false)
    {
        var aim = at - Vector3.Forward;
        return roster.SpawnAi(new AiSpawn(planeNode, at, aim, AiPilot.HoldingCourse(at, aim),
            Scheme: scheme, Team: team, Inert: inert, ShippedSkins: shippedSkins, NodeName: name));
    }

    // Whether two rigs' painted output actually matches, read off the painter rather than the
    // scheme record alone (the rebuild's paint is what a player sees, and a record could match by
    // field while the model it built stayed unpainted or vice versa). Neither having a painter is
    // itself a match: a ShippedSkins reading can legitimately resolve to no paint at all, and that
    // null has to carry as faithfully as a real scheme would.
    private static bool PaintMatches(FlightController a, FlightController b)
    {
        bool aPainted = a.Painter != null;
        bool bPainted = b.Painter != null;
        if (aPainted != bPainted)
            return false;
        if (!aPainted)
            return true;
        // The tail decal placeholder every Balmoral ships (docs/formats/paint.md) is the fixed
        // point both painters are asked to resolve, so the comparison is real composited pixels.
        return ImagesEqual(a.Painter!.Substitute("bal_taillogo", null),
            b.Painter!.Substitute("bal_taillogo", null));
    }

    private static bool ImagesEqual(ImageTexture? a, ImageTexture? b)
    {
        if (a == null || b == null)
            return ReferenceEquals(a, b);
        var ia = a.GetImage();
        var ib = b.GetImage();
        if (ia == null || ib == null || ia.GetWidth() != ib.GetWidth() || ia.GetHeight() != ib.GetHeight())
            return false;
        return ia.GetData().SequenceEqual(ib.GetData());
    }

    // Shoots the stand-in down and answers what is left. Two hits, not one: armour still standing
    // against a hit that carries no armour damage nulls the health damage outright, so structure
    // is only reachable once the armour is spent (docs/org/vehicleDamage.md). An AI aircraft
    // resolves no zones, so both spends land on the whole pair (BL-386).
    private static (float Armor, float Health) Damage(FlightController plane, float health)
    {
        var hull = plane.Damage!;
        hull.Apply("nose", 0f, hull.WholeArmorMax);
        hull.Apply("nose", hull.WholeHealthMax * (1f - health), 0f);
        return (hull.WholeArmorMax > 0f ? hull.WholeArmor / hull.WholeArmorMax : 1f,
            hull.WholeHealthMax > 0f ? hull.WholeHealth / hull.WholeHealthMax : 1f);
    }

    // Code 967's first half: the aircraft the capture animation belongs to is out of the world, and
    // what is left of its hull is what the player's new one is flying on.
    private static void CheckCapturedHull(TestContext ctx, FlightController captured,
        FlightController after, float armor, float health, StringBuilder report)
    {
        var hull = after.Damage!;
        float gotArmor = hull.WholeArmorMax > 0f ? hull.WholeArmor / hull.WholeArmorMax : 1f;
        float gotHealth = hull.WholeHealthMax > 0f ? hull.WholeHealth / hull.WholeHealthMax : 1f;
        report.AppendLine($"inherited hull: armour {gotArmor * 100f:0}% structure {gotHealth * 100f:0}%");
        ctx.Check(captured.Inert,
            $"the aircraft the capture animation belongs to is hidden, not left flying beside the player");
        ctx.Check(Mathf.Abs(gotArmor - armor) < FractionTolerance
                  && Mathf.Abs(gotHealth - health) < FractionTolerance,
            $"and the new hull carries that aircraft's own armour and structure fractions, so a Balmoral shot half to pieces is the one the player inherits");
        ctx.Check(gotHealth < 1f - FractionTolerance,
            $"which leaves the player damaged rather than handing them a pristine airframe and making the ending easier than the original's");
    }

    // The rebuilt rig's actual painted output matches the captured aircraft's, read off the
    // painter rather than the scheme record alone, and the match is not a coincidence of the two
    // happening to share a pattern: the captured stand-in is a real enemy roster spawn (team != the
    // player's own), so its ShippedSkins reading is what CampaignRoster.SpawnFor gives every
    // campaign enemy, distinct from the player's own default Fortune Hunters paint.
    private static void CheckLivery(TestContext ctx, FlightController before,
        FlightController captured, FlightController after, StringBuilder report)
    {
        report.AppendLine($"livery: was '{before.Scheme?.Label ?? "-"}' (painted={before.Painter != null}), " +
            $"captured '{captured.Scheme?.Label ?? "-"}' (shipped-skins={captured.ShippedSkins}, " +
            $"painted={captured.Painter != null}), rebuilt '{after.Scheme?.Label ?? "-"}' " +
            $"(painted={after.Painter != null})");
        ctx.Check(!PaintMatches(before, captured),
            $"the capture stand-in's own paint is not the player's own, so a match below cannot be coincidence");
        ctx.Check(PaintMatches(after, captured),
            $"the rebuilt rig's actual painted output matches the captured aircraft's, as the original reads at the controls");
    }

    // Step 5: the aeroplane the player just left, in the hands of wingman_4 and visible.
    private static void CheckHandover(TestContext ctx, FlightController wingman, Vector3 wasPos,
        Vector3 wasNose, float armor, float health, StringBuilder report)
    {
        var offset = wingman.WorldPosition - wasPos;
        var flatNose = new Vector3(wasNose.X, 0f, wasNose.Z).Normalized();
        var flatOffset = new Vector3(offset.X, 0f, offset.Z);
        float bearing = Mathf.RadToDeg(flatNose.SignedAngleTo(flatOffset.Normalized(), Vector3.Up));
        report.AppendLine($"{AirframeHandover.WingmanName}: {offset.Length():0.#} m off at {bearing:0.#} deg, " +
            $"armour {wingman.Damage?.WholeArmor ?? 0f:0.#}/{armor:0.#} structure {wingman.Damage?.WholeHealth ?? 0f:0.#}/{health:0.#}");
        ctx.Check(!wingman.Inert && wingman.InPlay,
            $"'{AirframeHandover.WingmanName}' is revealed by the swap rather than left built out of the world");
        ctx.Check(Mathf.Abs(offset.Length() - AirframeHandover.RangeM) < RangeTolerance,
            $"{AirframeHandover.RangeM:0} m from where the player was flying");
        ctx.Check(Mathf.Abs(bearing - AirframeHandover.BearingDeg) < BearingTolerance,
            $"at {AirframeHandover.BearingDeg:0} degrees off that aircraft's own nose");
        ctx.Check(wingman.NoseDirection.Dot(flatNose) > 0.99f,
            $"pointed the way the player was pointed, so the two are flying alongside rather than converging");
        // ⚠ Against the sums CAPPED at this aircraft's own maxima: one airframe reads a zone-sum on
        // a human rig and the AI def's own authored pair here (BL-386), so the cap does real work.
        ctx.Check(wingman.Damage is { } hull
                  && Mathf.Abs(hull.WholeArmor - Mathf.Min(armor, hull.WholeArmorMax)) < PoolTolerance
                  && Mathf.Abs(hull.WholeHealth - Mathf.Min(health, hull.WholeHealthMax)) < PoolTolerance,
            $"carrying the armour and structure sums measured off the aeroplane the player left, not a repaired hull");
    }

    // What the replacement is: the named airframe's own def, its own stock hardpoint table, and its
    // own armour pools. The comparison is against the airframe LEFT as much as against the data,
    // because a swap that changed the model and kept the previous fit would read right on its own.
    private static void CheckAirframe(TestContext ctx, string zrdrPath, FlightController after,
        AirframeSwapCode wanted, string wasDef, StringBuilder report)
    {
        var stats = PlaneStats.Load(zrdrPath, wanted.PlaneNode);
        var stock = PlaneDamage.For(stats);
        ctx.Check(after.Loadout is { } fit
                  && string.Equals(fit.Def.Def, wanted.Def, StringComparison.OrdinalIgnoreCase),
            $"the player is flying '{wanted.Def}', the def callback {wanted.Code} names");
        var loadout = after.Loadout!;
        int hardpoints = loadout.Hardpoints.Count;
        var wasStock = StockLoadouts.Load().For(wasDef);
        report.AppendLine($"hardpoints: {hardpoints} on '{wanted.Def}', " +
            $"{(wasStock?.Hardpoints?.Count ?? 0)} authored on '{wasDef}'");
        ctx.Check(hardpoints > 0, $"with '{wanted.Def}'s own hardpoint table bound to its pylons");
        ctx.Check(hardpoints != (wasStock?.Hardpoints?.Count ?? 0),
            $"and that table is the new airframe's, not the '{wasDef}' table the aircraft carried in");
        int full = 0;
        foreach (var hardpoint in loadout.Hardpoints)
        {
            full += hardpoint.Ammo == hardpoint.Capacity ? 1 : 0;
        }

        ctx.Same(hardpoints, full,
            $"every pylon arrives at its own capacity: the ordnance is the airframe's, so nothing of the previous count carries over");

        ctx.Check(after.Damage != null, $"the replacement carries a damage ledger");
        var damage = after.Damage!;
        report.AppendLine($"armour: whole {damage.WholeArmorMax:0.#}/{damage.WholeHealthMax:0.#} " +
            $"over {damage.Parts.Count} zone(s); the airframe's own is " +
            $"{stock.WholeArmorMax:0.#}/{stock.WholeHealthMax:0.#} over {stock.Parts.Count}");
        ctx.Check(Mathf.IsEqualApprox(damage.WholeArmorMax, stock.WholeArmorMax)
                  && Mathf.IsEqualApprox(damage.WholeHealthMax, stock.WholeHealthMax),
            $"whose armour and health pools are '{wanted.PlaneNode}'s own, off its own stat rows");
        ctx.Same(stock.Parts.Count, damage.Parts.Count,
            $"over that airframe's own damage zones, which is the ladder the capture's aircraft has to fly on");
        ctx.Check(damage.WorstFraction < 1f,
            $"whose zones start at what is left of the CAPTURED aircraft's hull rather than at the record's own full pools, which is the whole of the swap's difficulty");
    }

    private static string Describe(string when, FlightController controller) =>
        $"{when}: def='{controller.Loadout?.Def.Def ?? "-"}' " +
        $"hardpoints={controller.Loadout?.Hardpoints.Count ?? 0} " +
        $"guns={controller.Loadout?.Guns.Count ?? 0} " +
        $"armour={controller.Damage?.WholeArmorMax ?? 0f:0.#} " +
        $"zones={controller.Damage?.Parts.Count ?? 0} " +
        $"pos={controller.WorldPosition} speed={controller.WorldVelocity.Length():0.#}";

    // Every definition in the mission's compiled program that raises one of the swap codes, with the
    // code it raises. Read out of the shipped data rather than named here: what the mission asks for
    // is the mission's own business, and this suite only drives it.
    private static List<(string Anim, int Code, string Root)> SwapCallsIn(AnimProgram program)
    {
        var found = new List<(string, int, string)>();
        foreach (var def in program.Defs)
        {
            if (def.AnimName is not { } anim)
            {
                continue;
            }

            string root = def.RootName is { Length: > 0 } named ? named : def.Name;

            foreach (var sequence in def.Sequences)
            {
                foreach (var ev in sequence.Events)
                {
                    if (!string.Equals(ev.Kind, "Callback", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    int code = (int)(ev.Data.Num("value") ?? -1f);
                    if (AirframeSwapCodes.For(code) != null)
                    {
                        found.Add((anim, code, root));
                    }
                }
            }
        }

        return found;
    }

    private static FlightRoster BuildRoster(TestContext ctx, TestWorld world, string chapter,
        TextureArchive textures, ProjectilePool pool, IReadOnlyList<PlayerRig> rigs)
    {
        var spec = SessionSpec.Parse(new[] { $"--plane={StartPlane}" });
        var planesGamez = GameZ.Load(ctx.PlanesGamezPath);
        var resources = new AircraftAssemblyResources
        {
            PlanesGamez = planesGamez,
            StatsFor = plane => PlaneStats.Load(ctx.ZrdrPath, plane),
            AiStatsFor = (plane, aiDef) => PlaneStats.LoadForAi(ctx.ZrdrPath, plane, aiDef),
            CamParamsFor = _ => new CamParams(),
            PaintRng = new RandomNumberGenerator(),
            ZrdrPath = ctx.ZrdrPath,
            StockLoadouts = StockLoadouts.Load(),
            WeaponDefs = WeaponDefs.Load(ctx.ZrdrPath, null),
            WeaponMessages = Messages.Load(ctx.MessagesPath),
            Textures = textures,
            Shakes = ShakeDefs.Load(ctx.ZrdrPath),
        };
        return new FlightRoster(FlightRosterPolicy.From(spec),
            new LiveryResolver(spec, Path.Combine(ctx.DataRoot, "extracted", "rof")),
            new WorldEffectsFactory(spec, ctx.Host, () => Vector3.Zero), ctx.Host, resources,
            new FlightWorldBindings
            {
                Projectiles = pool,
                Gamez = planesGamez,
                ChapterZrdrPath = SessionPaths.ChapterZrdr(ctx.DataRoot, chapter),
            },
            new HumanRosterBindings
            {
                RigCount = rigs.Count,
                Rigs = rigs,
                PauseState = new PauseState(),
                MenuInputFor = _ => new MenuInput(),
                ExitSession = () => { },
            }, new SwapFlightStarts());
    }

    private static RosterSpawnPlan? PlanNamed(CampaignRosterPlan plan, string name)
    {
        foreach (var spawn in plan.Spawns)
        {
            if (string.Equals(spawn.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                return spawn;
            }
        }

        return null;
    }

    private static CampaignMission? MissionOf(IReadOnlyList<CampaignMission> missions, int seq)
    {
        foreach (var mission in missions)
        {
            if (mission.Seq == seq)
            {
                return mission;
            }
        }

        return null;
    }

    // The actors one staged capture hands its leg: the session-shaped roster and rig, the aircraft
    // that was flying before the swap, the host, the capture animation's own aircraft, the block the
    // outgoing aeroplane goes to, and the pool the registrations are counted in.
    private sealed record Staged(
        FlightRoster Roster,
        PlayerRig Rig,
        FlightController Before,
        CutsceneController Cutscene,
        FlightController Captured,
        FlightController Wingman,
        ProjectilePool Pool);

    // One start, high enough over the mission's own terrain that the aircraft is flying rather than
    // resolving a ground contact on the frame the swap rebuilds it.
    private sealed class SwapFlightStarts : IFlightStarts
    {
        public IReadOnlyList<FlightStart> ChooseStarts(IReadOnlyList<SpawnPoint>? spawns,
            string missionZrdrPath, int spawnBase, int playerCount)
        {
            var starts = new FlightStart[playerCount];
            for (int i = 0; i < playerCount; i++)
            {
                var position = new Vector3(i * 60f, 1200f, 0f);
                starts[i] = new FlightStart(position, position + Vector3.Forward, 0.5f, 90f);
            }

            return starts;
        }
    }
}
