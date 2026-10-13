using System;
using System.Collections.Generic;
using System.Linq;
using CSVM.Extraction;
using CSVM.Flight.Modes;
using CSVM.Flight.Weapons;
using CSVM.Launch;
using CSVM.Mech3;
using CSVM.Net;
using CSVM.Session;
using CSVM.Session.World;
using CSVM.Spec;
using CSVM.UI.Screens;
using CSVM.Utils;
using Godot;

namespace CSVM.Testing;

/// <summary>Capture the Flag between whole sessions in one process, on
/// <see cref="NetCombatSuites"/>'s rig and the chapter's <c>MP2</c> map. The host and the first
/// guest fly lobby team 1 and the second guest team 2. Every flag rule is read on all three
/// machines. The host decides, and each guest applies its table or floats a flag on its own.
/// </summary>
internal static class NetFlagSuites
{
    private const ulong HostSeed = 0xC0FFEE40UL;

    private const int SettleSteps = 20;

    // An ask, the host's decision and its table on every machine, with room to spare.
    private const int AskSteps = 10;

    // A death, the crash camera cut to QuickRespawn, the ask and the grant.
    private const int GrantSteps = 120;

    private const float QuickRespawn = 0.5f;

    // How high over a flag a pilot is put, inside the reach.
    private const float Over = 15f;

    // How high over the field a waiting pilot is parked. It is far above the throw's 80 m climb
    // and every hill, with room for the loops it flies there.
    private const float ParkHeight = 800f;

    // Steady loops at full throttle. A loop comes back to the height it started at, so a parked
    // pilot never descends into the hills.
    private const string TrackedFlight = "--hold=0.3,0,0,1";

    // The banner's flight posed by hand: three seconds level, then two in a level turn at cruise.
    private const float TrailSpeed = 100f;
    private const int TrailLevelSteps = 180;
    private const int TrailTurnSteps = 120;
    private const float TrailYawDegPerS = 30f;

    // How far above the carrier's own pose that flight starts, clear of every hill it crosses.
    private const float ClearOfHills = 400f;

    // A vertical loop and a bit more: seven seconds pitching at 60 deg/s.
    private const float LoopDegPerS = 60f;
    private const int LoopSteps = 420;

    // Aerobatics posed by hand: rolls at 180 deg/s, half loops at 90 deg/s, and a knife-edge turn
    // whose bank wanders 3 degrees.
    private const float RollDegPerS = 180f;
    private const float HalfLoopDegPerS = 90f;
    private const float KnifeEdgeWanderDeg = 3f;

    // How long after a half loop's roll-out or pull-out the banner must hang upright again. It is a
    // half turn at the 90 deg/s righting rate, and half a second to spare.
    private const float RightingBound = 2.5f;

    // Banner frames timed on the guest's own carrier.
    private const int LocalFrames = 600;

    private static readonly int[] Teams = { 1, 1, 2 };

    private static readonly Dictionary<int, string> TeamNames = new() { [1] = "Red Squadron", [2] = "Blue Angels" };

    // An Immelmann, then an aileron roll held inverted between its halves, then a knife-edge turn.
    private static readonly Leg[] ImmelmannAndRoll =
    {
        new("level", 1f, 0f, 0f),
        new("half loop up", 2f, HalfLoopDegPerS, 0f),
        new("roll-out", 1f, 0f, RollDegPerS),
        new("righting", RightingBound, 0f, 0f),
        new("half roll", 1f, 0f, RollDegPerS),
        new("inverted", 1f, 0f, 0f),
        new("half roll back", 1f, 0f, RollDegPerS),
        new("to knife-edge", 1f, 0f, 0.5f * RollDegPerS),
        new("knife-edge turn", 3f, TrailYawDegPerS, 0f, KnifeEdgeWanderDeg),
    };

    // A half roll to inverted, then a half loop pulled down through the vertical.
    private static readonly Leg[] SplitS =
    {
        new("level", 1f, 0f, 0f),
        new("half roll", 1f, 0f, RollDegPerS),
        new("half loop down", 2f, HalfLoopDegPerS, 0f),
        new("righting", RightingBound, 0f, 0f),
    };

    // The rows every flag marker reads, by the keys FlagMarkers names. The side words come first,
    // then each marker's line for the flag's own side and for the other.
    private static readonly string[] MarkerRows =
    {
        FlagMarkers.YourKey, FlagMarkers.EnemyKey, FlagMarkers.CapturedByKey, FlagMarkers.HoldsFlagKey,
        "MSG_MP_YOUR_BASE", "MSG_MP_ENEMY_BASE", "MSG_MP_YOUR_FLAG_BASE", "MSG_MP_ENEMY_FLAG_BASE",
        "MSG_YOUR_FLAG_FLOAT", "MSG_ENEMY_FLAG_FLOAT",
    };

    // Steps that outlast a cooldown, so the next ask of the same seat is not refused by it.
    private static int CooledSteps => (int)(FlagMatch.HomeTakeCooldown / GameClock.FixedDt) + 10;

    [Suite("net-capture-the-flag",
        "three sessions on the chapter's MP2 map, two seats on lobby team 1 and one on team 2: every "
        + "machine builds a flag per team at its cs_flag_n, its base and flag markers reading Your or "
        + "Enemy by the pane's side, and every other seat's aeroplane by its callsign; a guest takes "
        + "the enemy flag at its base, the flag hangs on its "
        + "aeroplane on every machine, the base flag hides and the away marker and the carrier's tag "
        + "name the carrier; bringing it home "
        + "scores score_enemy_flag; a carrier's death floats the flag on every machine; a pilot catches its own "
        + "floating flag and returning it scores score_return_flag; a guest carrier's ejectflag typed into the chat "
        + "floats its flag on every machine through the host and sends no chat line, its marker back on its "
        + "callsign; the uncaught flag goes home "
        + "when its 15 s throw runs out, scoring nobody; and a second capture ends the match on the Score limit by team")]
    internal static void AMatchOfFlagsAcrossThreeMachines(TestContext ctx)
    {
        var spec = Spec(ctx);
        var mesh = LoopbackTransport.Mesh(3, LoopbackConditions.Perfect, new Random(4001));
        var roster = NetCombatSuites.Roster(3, spec).Select((seat, i) => seat with { TeamId = Teams[i] }).ToArray();
        var ambient = NetCombatSuites.Ambient.Save();
        var ends = new List<NetCombatSuites.Ends>();
        try
        {
            for (int i = 0; i < 3; i++)
            {
                ends.Add(NetCombatSuites.Ends.Open(ctx, spec, mesh[i], isHost: i == 0, HostSeed + (ulong)i,
                    i == 0 ? roster : null, teamNames: TeamNames));
            }

            ctx.Check(ends.All(e => e.Built), $"three sessions build in one process ({string.Join(", ", ends.Select(e => e.Built))})");
            if (!ends.All(e => e.Built))
            {
                return;
            }

            var peers = ends.Select(e => e.Session).ToArray();
            if (!Built(ctx, peers))
            {
                return;
            }

            foreach (var rig in peers.SelectMany(p => p.SeatRigs))
            {
                if (rig.Controller is { } pilot)
                {
                    pilot.AutoRespawnAfter = QuickRespawn;
                }
            }

            Lockstep(SettleSteps, peers);
            for (int seat = 0; seat < 3; seat++)
            {
                Park(peers, seat);
            }

            var rows = Messages.Load(ctx.MessagesPath);
            Worded(ctx, rows);
            HomeMarkers(ctx, peers, rows);
            Capture(ctx, peers, rows);
            Lockstep(CooledSteps, peers);
            CatchAndReturn(ctx, peers, rows);
            Lockstep(CooledSteps, peers);
            ThrowRunsOut(ctx, peers);
            Lockstep(CooledSteps, peers);
            SecondCapture(ctx, peers);
        }
        finally
        {
            foreach (var end in Enumerable.Reverse(ends))
            {
                end.Close();
            }

            ambient.Restore();
        }
    }

    [Suite("ctf-towed-banner",
        "two sessions on the chapter's MP2 map, the guest on team 2 carrying team 1's flag: under Original "
        + "every machine draws the carried flag's billboard and no banner; under Enhanced every machine draws "
        + "a banner in the flag's own art and hides the billboard; a carrier gated into a cloud zone takes its "
        + "banner into that zone; on the host, whose carrier is a replicated "
        + "copy, the banner trails straight behind the tail in level flight and swings off its axis in a "
        + "turn; the same flight repeats the chain bit for bit; the ripple runs faster with airspeed; through "
        + "a vertical loop no point's side turns over against its neighbour; through an Immelmann, an aileron "
        + "roll, a knife-edge turn and a split-S the banner's top edge never turns over between frames, and it "
        + "hangs upright again soon after each half loop; the guest's own carrier tows its banner on the "
        + "shared layer; a live "
        + "switch back to Original swaps the billboard back; and a floating flag tows no banner")]
    internal static void TheCarriedFlagTowsABannerUnderEnhanced(TestContext ctx)
    {
        var spec = Spec(ctx);
        var mesh = LoopbackTransport.Mesh(2, LoopbackConditions.Perfect, new Random(4002));
        var roster = NetCombatSuites.Roster(2, spec).Select((seat, i) => seat with { TeamId = i + 1 }).ToArray();
        var ambient = NetCombatSuites.Ambient.Save();
        var ends = new List<NetCombatSuites.Ends>();
        bool wasEnhanced = GraphicsMode.Enhanced;
        try
        {
            GraphicsMode.Set(false);
            for (int i = 0; i < 2; i++)
            {
                ends.Add(NetCombatSuites.Ends.Open(ctx, spec, mesh[i], isHost: i == 0, HostSeed + 8 + (ulong)i,
                    i == 0 ? roster : null, teamNames: TeamNames));
            }

            var peers = ends.Select(e => e.Session).ToArray();
            bool built = ends.All(e => e.Built) && peers.All(p => p.Dogfight?.Flags != null);
            ctx.Check(built, $"two sessions build in one process, each with its flags ({string.Join(", ", ends.Select(e => e.Built))})");
            if (!built)
            {
                return;
            }

            Lockstep(SettleSteps, peers);
            Park(peers, 0);
            Park(peers, 1);
            Put(peers, seat: 1, peers[1].Dogfight!.Flags!.Flags.HomeOf(1));
            Lockstep(AskSteps, peers);
            Held(ctx, peers, "the team 2 guest takes team 1's flag", team: 1, holder: 1);
            var banners = peers.Select(p => p.Dogfight!.Flags!.Banner(1)).OfType<FlagBanner>().ToArray();
            ctx.Check(banners.Length == 2, $"and every machine holds a banner for it");
            if (banners.Length != 2)
            {
                return;
            }

            BannerSwap(ctx, peers, banners, spec.EmptyStage);
            BannerZone(ctx, peers[0], banners[0]);
            BannerLocal(ctx, peers[1], banners[1]);
            BannerTrail(ctx, ends[0], banners[0], peers[0].SeatRigs[1].Controller!);
            BannerRepeats(ctx);
            BannerLoop(ctx);
            BannerAerobatics(ctx);

            GraphicsMode.Set(true);
            Down(peers, victim: 1, killer: 0);
            DrawAll(banners);
            ctx.Check(peers.All(p => p.Dogfight!.Flags!.Flags.RowOf(1) is { State: FlagState.Floating }) && banners.All(b => !b.Drawn && b.BillboardShown),
                $"ABLE-TO-FAIL CONTROL: under Enhanced the carrier's death floats the flag, which draws its billboard and tows no banner ({Rows(peers)}; {Swap(peers, banners)})");
        }
        finally
        {
            GraphicsMode.Set(wasEnhanced);
            foreach (var end in Enumerable.Reverse(ends))
            {
                end.Close();
            }

            ambient.Restore();
        }
    }

    // Original draws the billboard alone and Enhanced the banner alone, in the flag's own art. A live
    // switch back swaps them again, on every machine.
    private static void BannerSwap(TestContext ctx, GameSession[] peers, FlagBanner[] banners, bool emptyStage)
    {
        DrawAll(banners);
        ctx.Check(banners.All(b => !b.Drawn && b.BillboardShown) && peers.All(p => Billboard(p) is var (shown, of) && shown == of),
            $"under Original every machine draws the billboard and no banner ({Swap(peers, banners)})");
        GraphicsMode.Set(true);
        DrawAll(banners);
        ctx.Check(banners.All(b => b.Drawn && !b.BillboardShown) && peers.All(p => Billboard(p).Drawn == 0),
            $"under Enhanced every machine draws the banner and hides the billboard ({Swap(peers, banners)})");
        ctx.Check(banners.All(b => b.Material?.GetShaderParameter("albedo_tex").AsGodotObject() == b.Art),
            $"and each banner binds the art it took from its flag");
        if (!emptyStage)
        {
            var other = peers[0].Dogfight!.Flags!.Banner(2)?.Art;
            ctx.Check(banners.All(b => b.Art != null) && other != null,
                $"which on the MP2 map is each flag billboard's own texture ({banners[0].Art?.ResourcePath}, team 2 {other?.ResourcePath})");
            var flag = peers[0].Dogfight!.Flags!.CarriedFlag(1);
            int lights = flag?.FindChildren("*", nameof(Light3D), true, false).Count ?? 0;
            ctx.Check(Billboard(peers[0]).Of > 0 && lights == 0,
                $"and the billboard it hides is {Billboard(peers[0]).Of} surface(s) holding no light node ({lights}); its glow is a world light at the flag node, which stays lit");
            ctx.Note($"team 1's banner art {banners[0].Art?.GetSize()} rid {banners[0].Art?.GetRid()}, team 2's {other?.GetSize()} rid {other?.GetRid()}");
        }

        GraphicsMode.Set(false);
        DrawAll(banners);
        ctx.Check(banners.All(b => !b.Drawn && b.BillboardShown) && peers.All(p => Billboard(p) is var (shown, of) && shown == of),
            $"a live switch back to Original draws the billboard again and no banner ({Swap(peers, banners)})");
        GraphicsMode.Set(true);
        DrawAll(banners);
    }

    // The host's copy of the guest's carrier, posed by hand through a level run and a level turn. The
    // banner draws after each pose, as a frame would, and a still of each goes beside the report.
    private static void BannerTrail(TestContext ctx, NetCombatSuites.Ends host, FlagBanner banner, Flight.Airframe.FlightController carrier)
    {
        var mount = banner.Mount!;
        var watch = new System.Diagnostics.Stopwatch();
        int frames = 0;
        void Fly(IEnumerable<Transform3D> poses)
        {
            foreach (var pose in poses)
            {
                carrier.GlobalTransform = pose;
                watch.Start();
                banner.Draw(GameClock.FixedDt);
                watch.Stop();
                frames++;
            }
        }

        Fly(Flown(carrier.GlobalPosition + (Vector3.Up * ClearOfHills), TrailLevelSteps, 0));
        var (along, lateral) = Trail(banner, mount);
        ctx.Note($"level: {along:0.00} m back, {lateral:0.000} m off the axis");
        ctx.Check(along > 0.99f * FlagBanner.TotalLength && Mathf.Abs(lateral) < 0.05f,
            $"in level flight the banner trails straight behind the tail ({along:0.00} m back, {lateral:0.000} m off its axis)");
        var top = banner.TopOf(banner.Chain.Length - 1);
        ctx.Check(top.Y > 0.99f, $"and hangs upright with the art's top edge up ({top})");
        Still(ctx, host, banner, mount, "ctf-banner-level.png", behind: false);
        var view = Eye(ctx, host, mount, behind: false);
        int callsShown = DrawCalls(view);
        GraphicsMode.Set(false);
        banner.Draw(GameClock.FixedDt);
        int callsHidden = DrawCalls(view);
        view.Free();
        GraphicsMode.Set(true);
        banner.Draw(GameClock.FixedDt);

        Fly(Flown(carrier.GlobalPosition, 0, TrailTurnSteps));
        (along, lateral) = Trail(banner, mount);
        ctx.Note($"turn: {along:0.00} m back, {lateral:0.00} m off the axis (negative is the left, the turn's inside)");
        ctx.Check(Mathf.Abs(lateral) > 1f && along > 0f,
            $"and in a {TrailYawDegPerS:0} deg/s turn it swings off the tail's axis ({lateral:0.00} m to the {(lateral < 0f ? "left, inside" : "right, outside")}, {along:0.00} m back)");
        Still(ctx, host, banner, mount, "ctf-banner-turn.png", behind: true);
        ctx.Note($"draw cost: {watch.Elapsed.TotalMilliseconds * 1000.0 / frames:0.0} us of CPU per banner frame over {frames} frames; draw calls of the side view {callsHidden} with the billboard and {callsShown} with the banner");
    }

    // A level run along -Z, then a level turn to the left, at the trail's speed.
    private static IEnumerable<Transform3D> Flown(Vector3 start, int level, int turn)
    {
        var at = start;
        float heading = 0f;
        for (int i = 0; i < level + turn; i++)
        {
            heading += i < level ? 0f : Mathf.DegToRad(TrailYawDegPerS) * GameClock.FixedDt;
            var basis = new Basis(Vector3.Up, heading);
            at += -basis.Z * (TrailSpeed * GameClock.FixedDt);
            yield return new Transform3D(basis, at);
        }
    }

    // The chain's trailing edge against the tail's axis: how far back along it, and how far off it
    // with the aircraft's left negative.
    private static (float Along, float Lateral) Trail(FlagBanner banner, Node3D mount)
    {
        var chain = banner.Chain.ToArray();
        var pose = mount.GlobalTransform;
        var rel = chain[^1] - chain[0];
        return (rel.Dot(pose.Basis.Z.Normalized()), rel.Dot(pose.Basis.X.Normalized()));
    }

    // Two banners fed one flight end on the same chain and phase to the bit. Over the same steps a
    // faster flight advances the ripple further.
    private static void BannerRepeats(TestContext ctx)
    {
        var flag = new Node3D();
        var banners = new[] { FlagBanner.For(flag), FlagBanner.For(flag), FlagBanner.For(flag), FlagBanner.For(flag) };
        try
        {
            foreach (var banner in banners.Take(2))
            {
                foreach (var pose in Flown(Vector3.Up * ParkHeight, TrailLevelSteps, TrailTurnSteps))
                {
                    banner.Follow(pose, TrailSpeed, GameClock.FixedDt);
                }
            }

            ctx.Check(banners[0].Chain.SequenceEqual(banners[1].Chain) && banners[0].Phase == banners[1].Phase,
                $"the same flight repeats the chain and the ripple phase bit for bit (phase {banners[0].Phase} and {banners[1].Phase})");
            for (int i = 0; i < 10; i++)
            {
                banners[2].Follow(Transform3D.Identity, 40f, GameClock.FixedDt);
                banners[3].Follow(Transform3D.Identity, 120f, GameClock.FixedDt);
            }

            ctx.Check(banners[3].Phase > 2f * banners[2].Phase && banners[2].Phase > 0f,
                $"and the ripple runs faster with airspeed ({banners[2].Phase:0.000} rad at 40 m/s and {banners[3].Phase:0.000} rad at 120 m/s over ten steps)");
        }
        finally
        {
            foreach (var banner in banners)
            {
                banner.Free();
            }

            flag.Free();
        }
    }

    private static void DrawAll(FlagBanner[] banners)
    {
        foreach (var banner in banners)
        {
            banner.Draw(GameClock.FixedDt);
        }
    }

    // How many of the carried flag's surfaces draw on that machine, and how many it has.
    private static (int Drawn, int Of) Billboard(GameSession peer)
    {
        var surfaces = Surfaces(peer.Dogfight!.Flags!.CarriedFlag(1));
        return (surfaces.Count(s => s.Visible), surfaces.Length);
    }

    private static GeometryInstance3D[] Surfaces(Node? root) =>
        root?.FindChildren("*", nameof(GeometryInstance3D), true, false).OfType<GeometryInstance3D>().ToArray()
            ?? Array.Empty<GeometryInstance3D>();

    // The cloud-band gate's zone layer, written by hand on every surface of the host's copy of the
    // carrier, the billboard's included. The banner takes the zone, and the billboard stays hidden.
    private static void BannerZone(TestContext ctx, GameSession host, FlagBanner banner)
    {
        uint zone = ZoneGate.LayerFor(2);
        var hull = Surfaces(host.SeatRigs[1].Controller!.PlaneModel).Where(s => s.Layers == 1u).ToArray();
        ctx.Check(banner.DrawnLayers == 1u && hull.Length > 0,
            $"ABLE-TO-FAIL CONTROL: outside any cloud band the banner draws on the shared world layer ({banner.DrawnLayers}, {hull.Length} carrier surfaces on it)");
        foreach (var surface in hull)
        {
            surface.Layers = zone;
        }

        banner.Draw(GameClock.FixedDt);
        ctx.Check(banner.DrawnLayers == zone && Billboard(host).Drawn == 0,
            $"a carrier gated into cloud zone 2 takes its banner into that zone ({banner.DrawnLayers}), and the zone write on the billboard does not draw it again ({Billboard(host).Drawn} drawn)");
        foreach (var surface in hull)
        {
            surface.Layers = 1u;
        }

        banner.Draw(GameClock.FixedDt);
    }

    // A vertical loop flown with no bank. Each point's side stays on its neighbour's side all the way
    // round. The side taken off the world's up turns over as the chain passes vertical.
    private static void BannerLoop(TestContext ctx)
    {
        var flag = new Node3D();
        var banner = FlagBanner.For(flag);
        try
        {
            var at = Vector3.Up * ParkHeight;
            float pitch = 0f;
            int flips = 0;
            int uprightFlips = 0;
            float worst = 1f;
            for (int step = 0; step < LoopSteps; step++)
            {
                pitch += Mathf.DegToRad(LoopDegPerS) * GameClock.FixedDt;
                var basis = new Basis(Vector3.Right, pitch);
                at += -basis.Z * (TrailSpeed * GameClock.FixedDt);
                banner.Follow(new Transform3D(basis, at), TrailSpeed, GameClock.FixedDt);
                var sides = banner.Sides.ToArray();
                var chain = banner.Chain.ToArray();
                bool flipped = false;
                bool uprightFlipped = false;
                for (int i = 1; i < sides.Length; i++)
                {
                    float dot = sides[i].Dot(sides[i - 1]);
                    worst = Mathf.Min(worst, dot);
                    flipped |= dot <= 0f;
                    uprightFlipped |= Upright(chain, i).Dot(Upright(chain, i - 1)) <= 0f;
                }

                flips += flipped ? 1 : 0;
                uprightFlips += uprightFlipped ? 1 : 0;
            }

            ctx.Note($"loop: {flips} frames with a turned-over side, worst neighbour dot {worst:0.000}; the world-up side turns over in {uprightFlips} of {LoopSteps} frames");
            ctx.Check(flips == 0, $"through a vertical loop no point's side turns over against its neighbour ({flips} frames, worst neighbour dot {worst:0.000})");
            ctx.Check(uprightFlips > 0, $"ABLE-TO-FAIL CONTROL: the side taken off the world's up does turn over in the same loop ({uprightFlips} frames)");
        }
        finally
        {
            banner.Free();
            flag.Free();
        }
    }

    // The side a strip took before the banner carried its frame: across the world's up, with a
    // fixed fallback where the chain stands vertical.
    private static Vector3 Upright(Vector3[] chain, int i)
    {
        var tangent = (chain[Math.Min(i + 1, chain.Length - 1)] - chain[Math.Max(i - 1, 0)]).Normalized();
        var side = tangent.Cross(Vector3.Up);
        return side.LengthSquared() > 1e-6f ? side.Normalized() : Vector3.Right;
    }

    // Two posed flights. The first is an Immelmann, then an aileron roll held inverted between its
    // halves, then a knife-edge turn; the second a split-S. No frame may turn the banner's top edge
    // over, and after each half loop it must hang upright again within the righting bound.
    private static void BannerAerobatics(TestContext ctx)
    {
        var (roll, rollFlips, rollWorst, rollSignedFlips) = Aerobatics(ImmelmannAndRoll);
        var (splitS, splitFlips, splitWorst, _) = Aerobatics(SplitS);
        Framed End(Framed[] frames, string leg) => frames.Last(f => f.Leg == leg);
        var held = roll.Where(f => f.Leg == "inverted").ToArray();
        float Righted(Framed[] frames) => frames.SkipWhile(f => f.Leg != "righting").TakeWhile(f => f.Top <= 0.9f).Count() * GameClock.FixedDt;
        ctx.Note($"aerobatics (Immelmann, split-S): top edge at the half loop's end {End(roll, "half loop up").Top:0.000}, {End(splitS, "half loop down").Top:0.000}; upright {Righted(roll):0.00} s, {Righted(splitS):0.00} s after the roll-out or pull-out; nearer-face control's end tops {End(roll, "righting").Kept:0.000}, {End(splitS, "righting").Kept:0.000}; carrier-signed control {rollSignedFlips} flips");
        ctx.Check(rollFlips == 0 && splitFlips == 0,
            $"through an Immelmann, an aileron roll, a knife-edge turn and a split-S no frame turns the banner's top edge over against the frame before ({rollFlips} and {splitFlips} frames, worst dots {rollWorst:0.000} and {splitWorst:0.000})");
        ctx.Check(End(roll, "righting").Top > 0.9f && End(splitS, "righting").Top > 0.9f,
            $"and within {RightingBound:0.0} s of the roll-out from an Immelmann, or of the pull-out from a split-S, the banner hangs upright again (lowest top edge {End(roll, "righting").Top:0.000} and {End(splitS, "righting").Top:0.000})");
        ctx.Check(held.All(f => f.Top > 0.9f),
            $"and after the Immelmann it hangs upright through the held inverted second ({held.Count(f => f.Top > 0.9f)} of {held.Length} frames)");
        ctx.Check(End(roll, "righting").Kept < 0f && End(splitS, "righting").Kept < 0f,
            $"ABLE-TO-FAIL CONTROL: point 0 carried from its last frame and pulled to its nearer face ends both half loops top-down ({End(roll, "righting").Kept:0.000} and {End(splitS, "righting").Kept:0.000})");
        ctx.Check(rollSignedFlips > 0 && held.All(f => f.Signed < 0f),
            $"ABLE-TO-FAIL CONTROL: point 0 signed by the carrier's right each frame turns over in the roll flight ({rollSignedFlips} frames) and hangs top-down held inverted");
    }

    // One posed flight through a fresh banner, with the frames that turned some point's top edge
    // over against the frame before. Two control rules for point 0 run beside it. One carries the
    // side from its last frame and pulls it to its nearer face; one signs it by the carrier's right.
    private static (Framed[] Frames, int Flips, float Worst, int SignedFlips) Aerobatics(Leg[] legs)
    {
        var flag = new Node3D();
        var banner = FlagBanner.For(flag);
        try
        {
            var frames = new List<Framed>();
            Vector3[]? before = null;
            Vector3 kept = Vector3.Zero;
            Vector3 signedBefore = Vector3.Zero;
            int flips = 0;
            int signedFlips = 0;
            float worst = 1f;
            foreach (var (pose, leg) in Aerobatic(legs))
            {
                banner.Follow(pose, TrailSpeed, GameClock.FixedDt);
                var tops = Enumerable.Range(0, banner.Chain.Length).Select(banner.TopOf).ToArray();
                var chain = banner.Chain.ToArray();
                var tangent = (chain[1] - chain[0]).Normalized();
                var level = tangent.Cross(Vector3.Up);
                float flat = level.Length();
                level = flat > 1e-4f ? level / flat : Vector3.Zero;
                kept = before == null ? -level : kept;
                var side = kept - (tangent * kept.Dot(tangent));
                kept = side.LengthSquared() > 1e-8f ? side.Normalized() : kept;
                float lying = level.Dot(kept);
                kept = kept.Lerp(level * Mathf.Sign(lying), Mathf.Abs(lying) * Mathf.SmoothStep(0.1f, 0.4f, flat)).Normalized();
                var signed = tangent.Cross(level * (level.Dot(pose.Basis.X) < 0f ? -1f : 1f));
                if (before != null)
                {
                    float dot = tops.Zip(before, (a, b) => a.Dot(b)).Min();
                    worst = Mathf.Min(worst, dot);
                    flips += dot <= 0f ? 1 : 0;
                    signedFlips += signed.Dot(signedBefore) < 0f ? 1 : 0;
                }

                frames.Add(new Framed(leg, tops.Min(t => t.Y), tangent.Cross(kept).Y, signed.Y));
                before = tops;
                signedBefore = signed;
            }

            return (frames.ToArray(), flips, worst, signedFlips);
        }
        finally
        {
            banner.Free();
            flag.Free();
        }
    }

    // The flight's poses from level along -Z, each leg flown in body rates at the trail's speed. A
    // leg's wander rocks the bank by that many degrees once a second.
    private static IEnumerable<(Transform3D Pose, string Leg)> Aerobatic(Leg[] legs)
    {
        var basis = Basis.Identity;
        var at = Vector3.Up * ParkHeight;
        foreach (var leg in legs)
        {
            float rocked = 0f;
            for (int i = 0; i < Mathf.RoundToInt(leg.Seconds / GameClock.FixedDt); i++)
            {
                float rock = leg.WanderDeg * Mathf.Sin(Mathf.Tau * i * GameClock.FixedDt);
                float roll = Mathf.DegToRad((leg.RollDegPerS * GameClock.FixedDt) + rock - rocked);
                rocked = rock;
                basis = (basis * new Basis(Vector3.Right, Mathf.DegToRad(leg.PitchDegPerS * GameClock.FixedDt))
                    * new Basis(Vector3.Forward, roll)).Orthonormalized();
                at += -basis.Z * (TrailSpeed * GameClock.FixedDt);
                yield return (new Transform3D(basis, at), leg.Name);
            }
        }
    }

    // The guest's own carrier, whose airframe its seat stamps onto the seat's own layer, so no surface
    // carries a cloud zone. The banner draws on the shared layer, and the note times its frame.
    private static void BannerLocal(TestContext ctx, GameSession guest, FlagBanner banner)
    {
        var model = guest.SeatRigs[1].Controller!.PlaneModel;
        var flag = guest.Dogfight!.Flags!.CarriedFlag(1);
        int zoned = Surfaces(model).Count(s => flag?.IsAncestorOf(s) != true && (s.Layers & (ZoneGate.LayerBand | 1u)) != 0);
        var watch = System.Diagnostics.Stopwatch.StartNew();
        for (int i = 0; i < LocalFrames; i++)
        {
            banner.Draw(GameClock.FixedDt);
        }

        watch.Stop();
        ctx.Check(zoned == 0 && banner.Drawn && banner.DrawnLayers == 1u,
            $"the guest's own carrier, none of its airframe's surfaces on a zone or the shared layer ({zoned} of {Surfaces(model).Length}), tows its banner on the shared layer ({banner.DrawnLayers})");
        ctx.Note($"local carrier draw cost: {watch.Elapsed.TotalMilliseconds * 1000.0 / LocalFrames:0.0} us of CPU per banner frame over {LocalFrames} frames");
    }

    private static string Swap(GameSession[] peers, FlagBanner[] banners) =>
        string.Join(" | ", banners.Select((b, i) => $"banner={b.Drawn} billboard={b.BillboardShown} billboard surfaces {Billboard(peers[i]).Drawn}/{Billboard(peers[i]).Of}"));

    // A viewport of its own on the host's world, so the seat's cockpit pass stays out of the picture.
    // Its eye stands beside the carrier, or above and behind it for the turn.
    private static SubViewport Eye(TestContext ctx, NetCombatSuites.Ends host, Node3D mount, bool behind)
    {
        var pose = mount.GlobalTransform;
        var back = pose.Basis.Z.Normalized();
        var right = pose.Basis.X.Normalized();
        var middle = pose.Origin + (back * (0.5f * FlagBanner.TotalLength));
        var eye = behind ? middle + (Vector3.Up * 22f) + (back * 38f) : middle + (right * 32f) + (Vector3.Up * 6f);
        var view = new SubViewport
        {
            Size = new Vector2I(960, 540),
            World3D = host.Pane.FindWorld3D(),
            RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled,
        };
        // The seat's own camera mask, which keeps its first-person dressing out of any other eye.
        var seat = host.Pane.GetChildren().OfType<Camera3D>().FirstOrDefault();
        var camera = new Camera3D
        {
            Fov = 60f,
            Far = 20000f,
            Current = true,
            Environment = seat?.Environment,
            CullMask = seat?.CullMask ?? 1u,
        };
        view.AddChild(camera);
        ctx.Host.AddChild(view);
        camera.MakeCurrent();
        camera.LookAtFromPosition(eye, middle, Vector3.Up);
        return view;
    }

    // A still of the carrier and its banner, saved as a PNG when the process draws at all.
    private static void Still(TestContext ctx, NetCombatSuites.Ends host, FlagBanner banner, Node3D mount, string name, bool behind)
    {
        var view = Eye(ctx, host, mount, behind);
        try
        {
            Settle(host.Pane);
            Settle(view);
            if (Render(view) is not { } image)
            {
                ctx.Note($"{name}: no image, the process draws nothing");
                return;
            }

            System.IO.Directory.CreateDirectory(ctx.ScratchDir);
            string path = System.IO.Path.Combine(ctx.ScratchDir, name);
            image.SavePng(path);
            ctx.Note($"still {path}, banner drawn={banner.Drawn}");
        }
        finally
        {
            view.Free();
        }
    }

    private static Image? Render(SubViewport pane)
    {
        pane.RenderTargetUpdateMode = SubViewport.UpdateMode.Always;
        RenderingServer.ForceDraw();
        RenderingServer.ForceDraw();
        var image = pane.GetTexture()?.GetImage();
        pane.RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled;
        return image == null || image.IsEmpty() ? null : image;
    }

    // A suite never reaches the frame's transform flush, so every pose a render reads is pushed first.
    private static void Settle(Node node)
    {
        (node as Node3D)?.ForceUpdateTransform();
        foreach (var child in node.GetChildren())
        {
            Settle(child);
        }
    }

    private static int DrawCalls(SubViewport pane)
    {
        Settle(pane);
        Render(pane);
        return (int)RenderingServer.ViewportGetRenderInfo(pane.GetViewportRid(),
            RenderingServer.ViewportRenderInfoType.Visible, RenderingServer.ViewportRenderInfo.DrawCallsInFrame);
    }

    // Two captures end it and nothing short of them can: team 2 scores only by its flags.
    private static int ScoreTarget(TestContext ctx) => 2 * MatchScores.Load(ctx.ZrdrPath).FlagCapture;

    private static SessionSpec Spec(TestContext ctx)
    {
        ctx.RequireData(ctx.PlanesGamezPath, $"planes gamez");
        ctx.RequireData(ctx.ZrdrPath, $"zrdr archive");
        return SessionSpec.Parse(new[] { "--vs" }
            .Concat(NetCombatSuites.Arena(ctx, mission: SessionSpec.CtfMission))
            .Concat(new[] { "--players=1", "--mute", "--no-pads", "--ctf", TrackedFlight, $"--vs-kills={ScoreTarget(ctx)}" })
            .ToArray());
    }

    // Two flags on every machine, at the same homes, each at its base with its carried twin hidden.
    private static bool Built(TestContext ctx, GameSession[] peers)
    {
        var rows = peers.Select(p => p.Dogfight?.Flags?.Flags.Rows).ToArray();
        ctx.Check(rows.All(r => r is { Count: 2 } && r.All(f => f.State == FlagState.Home && f.Holder == FlagMatch.NoHolder)),
            $"every machine builds two flags at home from cs_flag_1 and cs_flag_2 ({string.Join(" | ", rows.Select(Describe))})");
        if (!rows.All(r => r is { Count: 2 }))
        {
            return false;
        }

        var homes = peers.Select(p => p.Dogfight!.Flags!.Flags.HomeOf(1)).ToArray();
        ctx.Check(homes.All(h => h.DistanceTo(homes[0]) < 0.01f) && homes[0].DistanceTo(peers[0].Dogfight!.Flags!.Flags.HomeOf(2)) > 2f * FlagMatch.Reach,
            $"and every machine stands team 1's flag on the same home, out of reach of team 2's ({string.Join(" | ", homes)})");
        return true;
    }

    // The guest on team 2 takes team 1's flag at its base, and brings it to its own.
    private static void Capture(TestContext ctx, GameSession[] peers, Messages rows)
    {
        Put(peers, seat: 2, peers[2].Dogfight!.Flags!.Flags.HomeOf(1));
        Lockstep(AskSteps, peers);
        Held(ctx, peers, "the team 2 guest takes team 1's flag at its base", team: 1, holder: 2);
        HeldMarkers(ctx, peers, rows);
        var spoken = peers.Select(p => string.Join(",", p.Dogfight!.Flags!.Spoken)).ToArray();
        ctx.Check(peers[0].Dogfight!.Flags!.Spoken.Contains("snd_CTFlost") && peers[1].Dogfight!.Flags!.Spoken.Contains("snd_CTFlost")
                  && peers[2].Dogfight!.Flags!.Spoken.Contains("snd_CTFstolen") && !peers[2].Dogfight!.Flags!.Spoken.Contains("snd_CTFlost"),
            $"team 1's machines hear their flag lost and team 2's hears it stolen ({string.Join(" | ", spoken)})");
        ctx.Check(peers.All(p => p.Dogfight!.Flags!.LinesPosted > 0),
            $"and every machine posts the flag line ({string.Join(",", peers.Select(p => p.Dogfight!.Flags!.LinesPosted))})");

        // ABLE-TO-FAIL CONTROL: inside the take's cooldown the carrier at its own base asks nothing.
        int before = peers[0].Dogfight!.Match.ScoreOf(2);
        Put(peers, seat: 2, peers[2].Dogfight!.Flags!.Flags.HomeOf(2));
        Lockstep(AskSteps, peers);
        Held(ctx, peers, "ABLE-TO-FAIL CONTROL: at its own base inside the take's 5 s cooldown the flag stays held", team: 1, holder: 2);
        Park(peers, seat: 2);
        Lockstep(CooledSteps, peers);
        Put(peers, seat: 2, peers[2].Dogfight!.Flags!.Flags.HomeOf(2));
        Lockstep(AskSteps, peers);
        Home(ctx, peers, "carried to team 2's base it goes home on every machine", team: 1);
        ctx.Check(peers.All(p => p.Dogfight!.Match.ScoreOf(2) == before + p.Dogfight!.Match.Scores.FlagCapture && p.Dogfight!.Match.KillsOf(2) == 0),
            $"and the capture scores its carrier {peers[0].Dogfight!.Match.Scores.FlagCapture} on every machine ({Scores(peers)})");
        ctx.Check(peers.All(p => !p.Dogfight!.Match.Completed),
            $"ABLE-TO-FAIL CONTROL: one capture is short of the {ScoreTarget(ctx)}-point target and the match runs ({Scores(peers)})");
        Park(peers, seat: 2);
    }

    // The carrier goes down with the flag and it floats everywhere. The host's own pilot on team 1
    // catches it and returns it.
    private static void CatchAndReturn(TestContext ctx, GameSession[] peers, Messages rows)
    {
        Put(peers, seat: 2, peers[2].Dogfight!.Flags!.Flags.HomeOf(1));
        Lockstep(AskSteps, peers);
        Held(ctx, peers, "taken again", team: 1, holder: 2);
        Down(peers, victim: 2, killer: 0);
        ctx.Check(peers.All(p => p.Dogfight!.Flags!.Flags.RowOf(1) is { State: FlagState.Floating, Holder: FlagMatch.NoHolder }),
            $"the carrier's death floats the flag on every machine ({Rows(peers)})");
        FloatingMarkers(ctx, peers, rows);

        // Over the flag rather than on it: a flag at rest lies against a base's buildings.
        int before = peers[0].Dogfight!.Match.ScoreOf(0);
        if (peers[0].Dogfight!.Flags!.Flags.FloatingAt(1) is { } floating)
        {
            var over = floating + (Vector3.Up * Over);
            peers[0].SeatRigs[0].Controller!.RespawnAt(over, over + (Vector3.Right * 100f));
        }

        Lockstep(1, peers);
        Park(peers, seat: 0);
        Lockstep(AskSteps, peers);
        Held(ctx, peers, "the host's team 1 pilot catches its own floating flag", team: 1, holder: 0);
        ctx.Check(peers[0].Dogfight!.Flags!.Spoken.Contains("snd_CTFscoreCapt") && !peers[2].Dogfight!.Flags!.Spoken.Contains("snd_CTFscoreCapt"),
            $"and its own team hears it caught ({string.Join(" | ", peers.Select(p => string.Join(",", p.Dogfight!.Flags!.Spoken)))})");
        Lockstep(CooledSteps, peers);
        Put(peers, seat: 0, peers[0].Dogfight!.Flags!.Flags.HomeOf(1));
        Lockstep(AskSteps, peers);
        Home(ctx, peers, "returned to its own base", team: 1);
        ctx.Check(peers.All(p => p.Dogfight!.Match.ScoreOf(0) == before + p.Dogfight!.Match.Scores.FlagReturn),
            $"and the return scores its carrier {peers[0].Dogfight!.Match.Scores.FlagReturn} on every machine ({Scores(peers)})");
        Park(peers, seat: 0);
    }

    // The guest carrier types the console's ejectflag into its chat. The host floats the flag and
    // relays it, so it floats everywhere while the carrier flies on. Nobody catches it, and it goes
    // home at the end of its throw, on the host's word.
    private static void ThrowRunsOut(TestContext ctx, GameSession[] peers)
    {
        Put(peers, seat: 2, peers[2].Dogfight!.Flags!.Flags.HomeOf(1));
        Lockstep(AskSteps, peers);
        Held(ctx, peers, "taken a third time", team: 1, holder: 2);
        var posted = peers.Select(p => p.Wire.Chat!.Chat.Posted).ToArray();
        var sent = peers.Select(p => p.Wire.Chat!.LinesSent).ToArray();
        Type(peers, seat: 1, NetChatLink.EjectFlagCommand);
        Lockstep(AskSteps, peers);
        Held(ctx, peers, "ABLE-TO-FAIL CONTROL: the console's ejectflag from a pilot carrying nothing leaves the flag held", team: 1, holder: 2);

        // Parked only once the relay is back, so the flag floats over team 1's base everywhere. The
        // take's cooldown stops the carrier catching it meanwhile.
        Type(peers, seat: 2, NetChatLink.EjectFlagCommand);
        Lockstep(AskSteps, peers);
        Park(peers, seat: 2);
        ctx.Check(peers.All(p => p.Dogfight!.Flags!.Flags.RowOf(1) is { State: FlagState.Floating, Holder: FlagMatch.NoHolder }),
            $"the carrier's ejectflag floats the flag on every machine ({Rows(peers)})");
        ctx.Check(peers[2].SeatRigs[2].Controller is { Crashed: false, Destroyed: false } && peers.All(p => p.SeatRigs[2].Controller!.MarkerName == null),
            $"and the carrier flies on untagged");
        var named = new[] { 0, 1 }.Select(m => NetTeamSuites.RefOf(NetTeamSuites.Cycles(peers[m], m),
            s => ReferenceEquals(s, peers[m].SeatRigs[2].Controller))?.DisplayName ?? "missing").ToArray();
        ctx.Check(named.All(n => n == peers[0].NetSeats[2].Callsign),
            $"and the other machines read it by its callsign again, not its airframe ({string.Join(" | ", named)})");
        ctx.Check(peers.Select((p, i) => p.Wire.Chat!.Chat.Posted == posted[i] && p.Wire.Chat!.LinesSent == sent[i]).All(same => same),
            $"and neither console line was posted or sent as chat ({string.Join(",", peers.Select(p => $"{p.Wire.Chat!.Chat.Posted}/{p.Wire.Chat!.LinesSent}"))})");
        var scores = peers.Select(p => Enumerable.Range(0, 3).Select(p.Dogfight!.Match.ScoreOf).ToArray()).ToArray();
        int flown = AskSteps;
        Lockstep((int)(FlagMatch.ThrowSeconds / GameClock.FixedDt) - flown - AskSteps, peers);
        ctx.Check(peers.All(p => p.Dogfight!.Flags!.Flags.RowOf(1) is { State: FlagState.Floating }),
            $"ABLE-TO-FAIL CONTROL: short of its 15 s throw the flag still floats on every machine ({Rows(peers)})");
        Lockstep(4 * AskSteps, peers);
        Home(ctx, peers, "once the throw runs out", team: 1);
        ctx.Check(peers.Select((p, i) => Enumerable.Range(0, 3).Select(p.Dogfight!.Match.ScoreOf).SequenceEqual(scores[i])).All(same => same),
            $"and a floating flag's return scores nobody ({Scores(peers)})");
    }

    // The second capture reaches the target on team 2's total and ends the match everywhere.
    private static void SecondCapture(TestContext ctx, GameSession[] peers)
    {
        Put(peers, seat: 2, peers[2].Dogfight!.Flags!.Flags.HomeOf(1));
        Lockstep(AskSteps, peers);
        Held(ctx, peers, "taken for the second capture", team: 1, holder: 2);
        Park(peers, seat: 2);
        Lockstep(CooledSteps, peers);
        Put(peers, seat: 2, peers[2].Dogfight!.Flags!.Flags.HomeOf(2));
        Lockstep(AskSteps, peers);
        ctx.Check(peers.All(p => p.Dogfight!.Match.TeamScoreOf(2) == ScoreTarget(ctx) && p.Dogfight!.Match.Completed && p.Dogfight!.End == NetMatchEnd.ScoreTarget),
            $"the second capture ends the match on the Score limit by team on every machine ({Scores(peers)}; {string.Join(", ", peers.Select(p => p.Dogfight!.End))})");
        var titles = peers.Select(p => VersusBoard.Title(p.Dogfight!.Match)).ToArray();
        ctx.Check(titles.All(t => t == "BLUE ANGELS WINS"),
            $"and every machine's board names the capturing team ({string.Join(" | ", titles)})");
    }

    // Every marker line below is read off the run's own table, so a missing row would compare
    // the key against itself. Each side's two lines must also differ, or no read could tell the
    // sides apart.
    private static void Worded(TestContext ctx, Messages rows)
    {
        var missing = MarkerRows.Where(key => rows.Get(key) == key).ToArray();
        var pairs = new[] { ("MSG_MP_YOUR_BASE", "MSG_MP_ENEMY_BASE"), ("MSG_MP_YOUR_FLAG_BASE", "MSG_MP_ENEMY_FLAG_BASE"),
            ("MSG_YOUR_FLAG_FLOAT", "MSG_ENEMY_FLAG_FLOAT"), (FlagMarkers.YourKey, FlagMarkers.EnemyKey) };
        var alike = pairs.Where(p => rows.Get(p.Item1) == rows.Get(p.Item2)).Select(p => p.Item1).ToArray();
        ctx.Check(missing.Length == 0 && alike.Length == 0,
            $"the message table words every flag marker line, each side apart (missing {string.Join(",", missing)}; alike {string.Join(",", alike)})");
    }

    // Both flags home. Each base and each flag at its base reads "Your" to its own team's pane and
    // "Enemy" to the other's, under its team's name. No away marker stands.
    private static void HomeMarkers(TestContext ctx, GameSession[] peers, Messages rows)
    {
        Reads(ctx, peers, "every pane reads team 1's base as Your Base on team 1 and Enemy Base on team 2, "
            + "where the map's own table labels it Team 1 on every machine", "ctf_1",
            m => $"{Side(m, 1, rows.Get("MSG_MP_YOUR_BASE"), rows.Get("MSG_MP_ENEMY_BASE"))}|Red Squadron",
            m => $"{Side(m, 1, "Your Base", "Enemy Base")}|Red Squadron");
        Reads(ctx, peers, "and team 2's base the other way round", "ctf_2",
            m => $"{Side(m, 2, rows.Get("MSG_MP_YOUR_BASE"), rows.Get("MSG_MP_ENEMY_BASE"))}|Blue Angels",
            m => $"{Side(m, 2, "Your Base", "Enemy Base")}|Blue Angels");
        Reads(ctx, peers, "every pane reads team 1's flag at its base by side", "cs_flag_1",
            m => $"{Side(m, 1, rows.Get("MSG_MP_YOUR_FLAG_BASE"), rows.Get("MSG_MP_ENEMY_FLAG_BASE"))}|Red Squadron",
            m => $"{Side(m, 1, "Your Flag At Base", "Enemy Flag At Base")}|Red Squadron");
        Reads(ctx, peers, "and no pane has an away marker for a flag at home", "cs_flg_light1", _ => "off");
        NetTeamSuites.PilotNames(ctx, peers, "with no flag taken, every pane reads every other seat's aeroplane by its callsign");
    }

    // Team 1's flag held by seat 2. Its at-base marker is off and its away marker names the carrier
    // by side. The carrier's own name line is its tag.
    private static void HeldMarkers(TestContext ctx, GameSession[] peers, Messages rows)
    {
        string carrier = peers[0].NetSeats[2].Callsign;
        string your = rows.Get(FlagMarkers.YourKey);
        string enemy = rows.Get(FlagMarkers.EnemyKey);
        Reads(ctx, peers, "with the flag taken no pane marks it at its base", "cs_flag_1", _ => "off");
        Reads(ctx, peers, "and every pane marks it away, captured by its carrier, by side", "cs_flg_light1",
            m => $"{Messages.Fill(rows.Get(FlagMarkers.CapturedByKey), Side(m, 1, your, enemy), carrier)}|Red Squadron",
            m => $"{Side(m, 1, "Your", "Enemy")} Flag Captured by {carrier}|Red Squadron");
        Reads(ctx, peers, "ABLE-TO-FAIL CONTROL: team 2's flag still stands at its base on every pane", "cs_flag_2",
            m => $"{Side(m, 2, rows.Get("MSG_MP_YOUR_FLAG_BASE"), rows.Get("MSG_MP_ENEMY_FLAG_BASE"))}|Blue Angels",
            m => $"{Side(m, 2, "Your Flag At Base", "Enemy Flag At Base")}|Blue Angels");

        var tags = new[] { 0, 1 }.Select(m => NetTeamSuites.RefOf(NetTeamSuites.Cycles(peers[m], m),
            s => ReferenceEquals(s, peers[m].SeatRigs[2].Controller))?.DisplayName ?? "missing").ToArray();
        string tag = Messages.Fill(rows.Get(FlagMarkers.HoldsFlagKey), carrier, your);
        ctx.Check(tags.All(t => t == tag),
            $"and team 1's panes read the carrier's own marker as holding their flag, in the message table's line ({string.Join(" | ", tags)})");
        if (!ctx.SyntheticData)
        {
            ctx.Check(tags.All(t => t == $"{carrier}  Holds Your flag"),
                $"and team 1's panes read the carrier's own marker as holding their flag ({string.Join(" | ", tags)})");
        }

        var near = NetTeamSuites.RefOf(NetTeamSuites.Cycles(peers[0], 0),
            s => s is ObjectiveSite { Node: "cs_flg_light1" })?.Position.DistanceTo(peers[0].SeatRigs[2].Controller!.WorldPosition);
        ctx.Check(near is < 30f, $"and the away marker stands on the carrier ({near?.ToString("0.0") ?? "none"} m)");
    }

    // The carrier down: the away marker reads floating by side and the carrier's tag is gone.
    private static void FloatingMarkers(TestContext ctx, GameSession[] peers, Messages rows)
    {
        Reads(ctx, peers, "every pane marks the floating flag by side", "cs_flg_light1",
            m => $"{Side(m, 1, rows.Get("MSG_YOUR_FLAG_FLOAT"), rows.Get("MSG_ENEMY_FLAG_FLOAT"))}|Red Squadron",
            m => $"{Side(m, 1, "Your Flag Floating", "Enemy Flag Floating")}|Red Squadron");
        ctx.Check(peers.All(p => p.SeatRigs[2].Controller!.MarkerName == null),
            $"and no machine still tags the downed carrier");
    }

    // The line a pane on machine `machine` should read for a marker of `team`'s flag.
    private static string Side(int machine, int team, string your, string enemy) =>
        Teams[machine] == team ? your : enemy;

    // A marker read on every pane against the lines the run's table words. On a real extraction it
    // is also read against the shipped wording, `shipped`, which an invented table does not carry.
    private static void Reads(TestContext ctx, GameSession[] peers, string what, string key, Func<int, string> want,
        Func<int, string>? shipped = null)
    {
        var got = peers.Select((p, m) => NetTeamSuites.RefOf(NetTeamSuites.Cycles(p, m),
                s => s is ObjectiveSite site && site.Node.Equals(key, StringComparison.OrdinalIgnoreCase)) is { } t
            ? $"{t.Category}|{t.DisplayName}"
            : "off").ToArray();
        string reading = string.Join(" | ", got);
        if (shipped == null)
        {
            ctx.Check(got.Select((g, m) => g == want(m)).All(ok => ok), $"{what} ({reading})");
            return;
        }

        ctx.Check(got.Select((g, m) => g == want(m)).All(ok => ok), $"{what}, in the message table's lines ({reading})");
        if (!ctx.SyntheticData)
        {
            ctx.Check(got.Select((g, m) => g == shipped(m)).All(ok => ok), $"{what} ({reading})");
        }
    }

    private static void Held(TestContext ctx, GameSession[] peers, string what, int team, int holder)
    {
        ctx.Check(peers.All(p => p.Dogfight!.Flags!.Flags.RowOf(team) is { State: FlagState.Held } row && row.Holder == holder),
            $"{what}: seat {holder} holds team {team}'s flag on every machine ({Rows(peers)})");
        ctx.Check(peers.All(p => p.SeatRigs[holder].Controller is { } pilot && p.Dogfight!.Flags!.CarriedFlag(team) is { Visible: true } flag && pilot.IsAncestorOf(flag)),
            $"and the flag hangs on seat {holder}'s aeroplane on every machine ({Hung(peers, team)})");
    }

    private static void Home(TestContext ctx, GameSession[] peers, string what, int team)
    {
        ctx.Check(peers.All(p => p.Dogfight!.Flags!.Flags.RowOf(team) is { State: FlagState.Home, Holder: FlagMatch.NoHolder }),
            $"{what}: team {team}'s flag stands at home on every machine ({Rows(peers)})");
        ctx.Check(peers.All(p => p.Dogfight!.Flags!.CarriedFlag(team) is not { Visible: true }),
            $"and no machine still draws it away from its base ({Hung(peers, team)})");
    }

    // Where each machine's carried flag hangs and whether it draws, for a failure message.
    private static string Hung(GameSession[] peers, int team) =>
        string.Join(" | ", peers.Select(p => p.Dogfight!.Flags!.CarriedFlag(team) is { } flag
            ? $"{flag.GetParent()?.Name ?? "-"}/{flag.GetParent()?.GetParent()?.Name ?? "-"} visible={flag.Visible}"
            : "no node"));

    // A seat put over a point on the machine that flies it. Every other machine sees it by its pose.
    private static void Put(GameSession[] peers, int seat, Vector3 at)
    {
        var over = at + (Vector3.Up * Over);
        peers[seat].SeatRigs[seat].Controller!.RespawnAt(over, over + (Vector3.Right * 100f));
    }

    // A seat put high over the field while the suite waits out a cooldown or a throw. There it is
    // out of every flag's reach and clear of the bases' buildings.
    private static void Park(GameSession[] peers, int seat)
    {
        var home = peers[seat].Dogfight!.Flags!.Flags.HomeOf(1);
        var high = home + (Vector3.Up * ParkHeight) + ((Vector3.Left + Vector3.Forward) * (ParkHeight * seat));
        peers[seat].SeatRigs[seat].Controller!.RespawnAt(high, high + (Vector3.Right * 100f));
    }

    // A line typed into the chat on the machine that flies the seat, and sent.
    private static void Type(GameSession[] peers, int seat, string text)
    {
        var link = peers[seat].Wire.Chat!;
        link.Open(seat, team: false);
        foreach (char c in text)
        {
            link.Chat.Type(c);
        }

        link.Submit();
    }

    private static void Down(GameSession[] peers, int victim, int killer)
    {
        var owner = peers[victim];
        owner.SeatRigs[victim].Controller!.DebugForceCrash(owner.SeatRigs[killer].Controller!.PlayerIndex);
        Lockstep(GrantSteps, peers);
        Park(peers, victim);
    }

    private static void Lockstep(int steps, params GameSession[] sessions)
    {
        for (int i = 0; i < steps; i++)
        {
            foreach (var session in sessions)
            {
                session._PhysicsProcess(GameClock.FixedDt);
            }
        }
    }

    private static string Describe(IReadOnlyList<FlagRow>? rows) =>
        rows == null ? "none" : string.Join(" ", rows.Select(r => $"{r.Team}:{r.State}/{r.Holder}"));

    private static string Rows(GameSession[] peers) =>
        string.Join(" | ", peers.Select(p => Describe(p.Dogfight?.Flags?.Flags.Rows)));

    private static string Scores(GameSession[] peers) =>
        string.Join(" | ", peers.Select(p => string.Join(",", Enumerable.Range(0, 3).Select(p.Dogfight!.Match.ScoreOf))
            + $" teams {p.Dogfight!.Match.TeamScoreOf(1)}/{p.Dogfight!.Match.TeamScoreOf(2)}"));

    // One leg of a posed flight: its length, and its pitch and roll rates in the aircraft's own axes.
    private readonly record struct Leg(string Name, float Seconds, float PitchDegPerS, float RollDegPerS, float WanderDeg = 0f);

    // One frame of a posed flight: the lowest top edge over the chain, and point 0's top under each
    // control rule.
    private readonly record struct Framed(string Leg, float Top, float Kept, float Signed);
}
