using System.Linq;
using CSVM.Flight.Airframe;
using CSVM.Flight.Audio;
using CSVM.Flight.Camera;
using CSVM.Flight.Weapons;
using CSVM.Mech3;
using CSVM.Session.Roster;
using CSVM.Utils;
using Godot;

namespace CSVM.Testing;

/// <summary>Enhanced Sound and Hit Confirm on a real <see cref="FlightAudio"/> and real rounds in a
/// <see cref="ProjectilePool"/>. Original Sound must write the decoded engine curve and the
/// authored hit levels exactly, and Enhanced Sound the shipped tuning's. The hit confirm must answer
/// a strike on an enemy aircraft alone, at its rate limit and under its row.</summary>
internal static class EnhancedSoundSuites
{
    private const float Dt = 1f / 60f;

    [Suite("enhanced-sound-cues",
        "Enhanced Sound and Hit Confirm: under Original Sound the own engine slot writes the decoded "
        + "curve's pitch and gain exactly at idle and full throttle and the bullet and canopy hits play "
        + "at their authored level; under Enhanced Sound the slot writes the shipped tuning's wider "
        + "pitch span and rising gain, the cockpit loop included, the damaged loop keeps the decoded gain, "
        + "and the hits take the tuning's gains; "
        + "a gun round of the human pilot striking an enemy aircraft sounds one hit confirm at the "
        + "tuning's pitch, two rounds landing together sound one, and a miss, a ground plate, a "
        + "friendly aircraft and an AI pilot's round sound none, a second local pilot's strike sounds on "
        + "that pilot's own mix alone; the confirm follows its row, and "
        + "never set it is on under Enhanced Sound and off under Original Sound")]
    internal static void EnhancedSoundCues(TestContext ctx)
    {
        ctx.RequireData(ctx.Data.Planes, $"planes gamez");
        ctx.RequireData(ctx.Zrdr, $"zrdr archive");
        ctx.RequireData(ctx.Data.Sounds, $"sound archive (soundsh)");
        TextureSet texturesPath = ctx.ChapterTextures("C1");
        ctx.RequireData(texturesPath, $"C1 textures");

        var weapons = WeaponDefs.Load(ctx.Zrdr, null);
        var soundDefs = SoundDefs.Load(ctx.Zrdr);
        var soundGroups = SoundDefs.LoadGroups(ctx.Zrdr);
        var stats = PlaneStats.Load(ctx.Zrdr, ctx.PlaneName);
        var tuning = EnhancedSoundTuning.Reload();
        ctx.Check(tuning != EnhancedSoundTuning.Neutral, $"the shipped tuning reads ({tuning.Describe()})");
        // The synthetic tree names its ricochet group apart from the install's, so there the confirm
        // draws whatever group bullet_hit_sound binds.
        if (ctx.SyntheticData)
        {
            tuning = tuning with { HitConfirmSound = stats.BulletHitSound };
            EnhancedSoundTuning.Current = tuning;
        }

        bool wasEnhanced = SoundModeSetting.EnhancedOn, wasConfirm = SoundModeSetting.HitConfirmOn;
        bool wasCockpitPitched = FlightAudio.CockpitLoopPitched;
        using var archive = new SoundArchive(ctx.Sounds);
        try
        {
            EngineSlot(ctx, archive, soundDefs, soundGroups, stats, weapons, tuning);
            DamagedSlot(ctx, archive, soundDefs, soundGroups, stats, weapons);
            HitLevels(ctx, archive, soundDefs, soundGroups, stats, weapons, tuning);
            HitConfirm(ctx, archive, soundDefs, soundGroups, stats, weapons, tuning, texturesPath);
        }
        finally
        {
            SoundModeSetting.EnhancedOn = wasEnhanced;
            SoundModeSetting.HitConfirmOn = wasConfirm;
            FlightAudio.CockpitLoopPitched = wasCockpitPitched;
            EnhancedSoundTuning.Reload();
        }
    }

    private static FlightAudio BuildAudio(TestContext ctx, SoundArchive archive,
        System.Collections.Generic.Dictionary<string, SoundDef> defs,
        System.Collections.Generic.Dictionary<string, SoundGroup> groups, PlaneStats stats, WeaponDefs weapons)
    {
        var audio = new FlightAudio();
        audio.Setup(archive, defs, stats, weapons, groups);
        ctx.Host.AddChild(audio);
        return audio;
    }

    private static void EngineSlot(TestContext ctx, SoundArchive archive,
        System.Collections.Generic.Dictionary<string, SoundDef> defs,
        System.Collections.Generic.Dictionary<string, SoundGroup> groups, PlaneStats stats, WeaponDefs weapons,
        EnhancedSoundTuning tuning)
    {
        var audio = BuildAudio(ctx, archive, defs, groups, stats, weapons);
        try
        {
            bool pitched = EngineAudioCurves.SlotIsPitched(defs, stats.EngineSound);
            float baseVol = defs.TryGetValue(stats.EngineSound, out var engineDef) ? engineDef.Volume : 1f;
            var idle = new EngineDrive(0f, 0f, 0f);
            var full = new EngineDrive(1f, 0f, 0f);

            foreach (bool enhanced in new[] { false, true })
            {
                SoundModeSetting.EnhancedOn = enhanced;
                var curve = enhanced ? tuning : null;
                string mode = enhanced ? "Enhanced" : "Original";
                foreach (var (drive, at) in new[] { (idle, "idle"), (full, "full throttle") })
                {
                    audio.Update(Dt, drive, 0.5f, 1f);
                    var want = EngineAudioCurves.Engine(stats, drive, 1f, pitched, curve);
                    ctx.Check(Mathf.IsEqualApprox(audio.EnginePitch, want.Pitch) && audio.EngineGain == want.Volume * baseVol,
                        $"{mode} Sound at {at}: the engine slot writes pitch {audio.EnginePitch:0.000} gain {audio.EngineGain:0.000} (want {want.Pitch:0.000}, {want.Volume * baseVol:0.000})");
                }
            }

            var originalIdle = EngineAudioCurves.Engine(stats, idle, 1f, true);
            var originalFull = EngineAudioCurves.Engine(stats, full, 1f, true);
            var enhancedIdle = EngineAudioCurves.Engine(stats, idle, 1f, true, tuning);
            var enhancedFull = EngineAudioCurves.Engine(stats, full, 1f, true, tuning);
            ctx.Check(enhancedFull.Pitch - enhancedIdle.Pitch > originalFull.Pitch - originalIdle.Pitch
                      && enhancedIdle.Volume < enhancedFull.Volume,
                $"Enhanced Sound's idle and full throttle are further apart: pitch {enhancedIdle.Pitch:0.00}..{enhancedFull.Pitch:0.00} against {originalIdle.Pitch:0.00}..{originalFull.Pitch:0.00}, gain {enhancedIdle.Volume:0.00}..{enhancedFull.Volume:0.00} against {originalIdle.Volume:0.00}..{originalFull.Volume:0.00}");

            // The cockpit loop with its own option off: flat under Original Sound, the remix's curve
            // under Enhanced Sound, which pitches every view.
            if (stats.CockpitEngineSound == null)
            {
                ctx.Note($"{ctx.PlaneName} binds no cockpit loop, so the cockpit view's pitch is not checked");
                return;
            }

            FlightAudio.CockpitLoopPitched = false;
            SoundModeSetting.EnhancedOn = false;
            audio.Update(Dt, full, 0.5f, 1f, firstPersonView: true);
            float flat = audio.EnginePitch;
            SoundModeSetting.EnhancedOn = true;
            audio.Update(Dt, idle, 0.5f, 1f, firstPersonView: true);
            float cockpitIdle = audio.EnginePitch;
            audio.Update(Dt, full, 0.5f, 1f, firstPersonView: true);
            float cockpitFull = audio.EnginePitch;
            ctx.Check(audio.EngineHoldsCockpitStream && flat == 1f
                      && Mathf.IsEqualApprox(cockpitIdle, enhancedIdle.Pitch) && Mathf.IsEqualApprox(cockpitFull, enhancedFull.Pitch),
                $"the cockpit loop plays flat under Original Sound ({flat:0.000}) and on the remix's span under Enhanced Sound ({cockpitIdle:0.000}..{cockpitFull:0.000})");
        }
        finally
        {
            audio.QueueFree();
        }
    }

    // The remix is the healthy loop's alone, so under Enhanced Sound the damaged loop still writes
    // the decoded curve's gain.
    private static void DamagedSlot(TestContext ctx, SoundArchive archive,
        System.Collections.Generic.Dictionary<string, SoundDef> defs,
        System.Collections.Generic.Dictionary<string, SoundGroup> groups, PlaneStats stats, WeaponDefs weapons)
    {
        if (stats.DamagedEngineSound is not { } damagedName || !defs.TryGetValue(damagedName, out var damagedDef))
        {
            ctx.Note($"{ctx.PlaneName} binds no damaged loop, so its level under Enhanced Sound is not checked");
            return;
        }

        var audio = BuildAudio(ctx, archive, defs, groups, stats, weapons);
        try
        {
            SoundModeSetting.EnhancedOn = true;
            var idle = new EngineDrive(0f, 0f, 0f);
            for (int frame = 0; frame < 600 && audio.EnginePhase != EngineSlotPhase.Damaged; frame++)
            {
                audio.Update(Dt, idle, 0.5f, 0.1f);
            }

            audio.Update(Dt, idle, 0.5f, 0.1f);
            float want = EngineAudioCurves.Engine(stats, idle, 1f, false).Volume * damagedDef.Volume;
            ctx.Check(audio.EngineHoldsDamagedStream && audio.EngineGain == want,
                $"under Enhanced Sound the damaged loop keeps the decoded gain ({audio.EngineGain:0.000}, want {want:0.000})");
        }
        finally
        {
            audio.QueueFree();
        }
    }

    private static void HitLevels(TestContext ctx, SoundArchive archive,
        System.Collections.Generic.Dictionary<string, SoundDef> defs,
        System.Collections.Generic.Dictionary<string, SoundGroup> groups, PlaneStats stats, WeaponDefs weapons,
        EnhancedSoundTuning tuning)
    {
        var audio = BuildAudio(ctx, archive, defs, groups, stats, weapons);
        try
        {
            foreach (bool enhanced in new[] { false, true })
            {
                SoundModeSetting.EnhancedOn = enhanced;
                string mode = enhanced ? "Enhanced" : "Original";
                string? bullet = audio.OnBulletHit();
                float bulletWant = (bullet != null ? defs[bullet].Volume : 0f) * (enhanced ? tuning.BulletHitGain : 1f);
                ctx.Check(bullet != null && audio.LastCueGain == bulletWant,
                    $"{mode} Sound: a round on the own aircraft rings {bullet ?? "nothing"} at {audio.LastCueGain:0.000} (want {bulletWant:0.000})");
                string? glass = audio.OnWindowHit();
                float glassWant = (glass != null ? defs[glass].Volume : 0f) * (enhanced ? tuning.CanopyHitGain : 1f);
                ctx.Check(glass != null && audio.LastCueGain == glassWant,
                    $"{mode} Sound: a canopy hole rings {glass ?? "nothing"} at {audio.LastCueGain:0.000} (want {glassWant:0.000})");
            }
        }
        finally
        {
            audio.QueueFree();
        }
    }

    private static void HitConfirm(TestContext ctx, SoundArchive archive,
        System.Collections.Generic.Dictionary<string, SoundDef> defs,
        System.Collections.Generic.Dictionary<string, SoundGroup> groups, PlaneStats stats, WeaponDefs weapons,
        EnhancedSoundTuning tuning, TextureSet texturesPath)
    {
        WeaponDef? gun = weapons.All.FirstOrDefault(w => w.IsGun && w.ArmorDamage is > 0f);
        var stock = StockLoadouts.Load().All.Values.FirstOrDefault(d => d.Model == ctx.PlaneName);
        ctx.Check(gun != null && stock != null, $"a damaging gun and the stock loadout of {ctx.PlaneName} exist");
        if (gun == null || stock == null)
        {
            return;
        }

        var textures = new TextureArchive(texturesPath);
        var pool = new ProjectilePool(textures, archive, defs, soundGroups: groups);
        ctx.Host.AddChild(pool);
        var rigs = new System.Collections.Generic.List<FlightController>();
        StaticBody3D? plate = null;
        FlightAudio? audio = null;
        FlightAudio? secondAudio = null;
        try
        {
            FlightController Rig(int index, Vector3 at, bool human)
            {
                var model = new PlaneBuilder(ctx.Planes, textures).Build(ctx.PlaneName);
                var rig = new FlightController
                {
                    PlaneModel = model,
                    Collider = PlaneCollider.Build(model),
                    Damage = new PlaneDamage(stats.DestroyableParts),
                    PlayerIndex = index,
                    IsHumanPiloted = human,
                    Projectiles = pool,
                    UseKeyboard = false,
                    PadDevices = System.Array.Empty<int>(),
                    AllowPause = false,
                };
                rig.AddChild(model);
                rig.Loadout = Loadout.Bind(stock, model, weapons);
                rig.Setup(new FlightModel(stats), null, new CamParams(), at, at + Vector3.Forward);
                ctx.Host.AddChild(rig);
                rig.Held = true;
                rig.PlaceHeld(at, at + Vector3.Forward);
                rigs.Add(rig);
                return rig;
            }

            var pilot = Rig(0, new Vector3(0f, 500f, 3000f), human: true);
            audio = BuildAudio(ctx, archive, defs, groups, stats, weapons);
            pilot.Audio = audio;
            var second = Rig(1, new Vector3(0f, 500f, 4000f), human: true);
            second.Team = pilot.Team;
            secondAudio = BuildAudio(ctx, archive, defs, groups, stats, weapons);
            second.Audio = secondAudio;
            var enemyAt = new Vector3(0f, 500f, 0f);
            var friendAt = new Vector3(400f, 500f, 0f);
            var groundAt = new Vector3(800f, 500f, 0f);
            var missAt = new Vector3(1200f, 500f, 0f);
            var enemy = Rig(FlightRoster.ShooterIdBase, enemyAt, human: false);
            enemy.Team = pilot.Team + 1;
            var friend = Rig(FlightRoster.ShooterIdBase + 1, friendAt, human: false);
            friend.Team = pilot.Team;
            plate = CombatSuites.Plate("hit-confirm-plate", new Vector3(20f, 20f, 1f), groundAt);
            ctx.Host.AddChild(plate);
            ctx.Check(AimAssist.Hostile(pilot.Team, enemy.Team) && !AimAssist.Hostile(pilot.Team, friend.Team),
                $"the enemy's team {enemy.Team} is hostile to the pilot's {pilot.Team}, the friend's is not");

            // Rounds from 10 m short of a target along +Z, then steps enough to land and outlast the
            // rate limit. Each volley is judged on its own.
            int Volley(Vector3 target, int rounds, int shooter = 0)
            {
                int before = audio.HitConfirms;
                var muzzle = new Transform3D(Basis.LookingAt(Vector3.Back, Vector3.Up), target + new Vector3(0f, 0f, -10f));
                for (int i = 0; i < rounds; i++)
                {
                    pool.Spawn(gun, muzzle, Vector3.Zero, shooter);
                }

                for (int i = 0; i < 30; i++)
                {
                    pool.SimStep(Dt);
                }

                pool.Clear();
                return audio.HitConfirms - before;
            }

            SoundModeSetting.Apply(SoundModeSetting.Enhanced, null);
            float enemyHealth = enemy.Damage!.WholeArmor + enemy.Damage.WholeHealth;
            int onEnemy = Volley(enemyAt, 1);
            ctx.Check(onEnemy == 1 && enemy.Damage.WholeArmor + enemy.Damage.WholeHealth < enemyHealth,
                $"a round striking the enemy sounds one hit confirm ({onEnemy})");
            ctx.Check(Mathf.IsEqualApprox(audio.HitConfirmPitchScale, tuning.HitConfirmPitch)
                      && Mathf.IsEqualApprox(audio.LastCueGain, LastConfirmLevel(defs, groups, tuning) ?? audio.LastCueGain),
                $"at the tuning's pitch {audio.HitConfirmPitchScale:0.00} and gain over its member's VOLUME ({audio.LastCueGain:0.000})");
            int together = Volley(enemyAt, 2);
            ctx.Check(together == 1, $"two rounds landing on the same frame sound one, the rate limit ({together})");
            ctx.Check(Volley(missAt, 1) == 0, $"a miss sounds none");
            ctx.Check(Volley(groundAt, 1) == 0, $"a round on a ground plate sounds none");
            ctx.Check(Volley(friendAt, 1) == 0, $"a round on a friendly aircraft sounds none");
            ctx.Check(Volley(enemyAt, 1, shooter: FlightRoster.ShooterIdBase + 1) == 0,
                $"an AI pilot's round on the same enemy sounds none on the pilot's mix");
            int secondBefore = secondAudio.HitConfirms;
            int onFirst = Volley(enemyAt, 1, shooter: second.PlayerIndex);
            int onSecond = secondAudio.HitConfirms - secondBefore;
            ctx.Check(onSecond == 1 && onFirst == 0,
                $"a second local pilot's strike sounds on that pilot's own mix ({onSecond}) and not the first's ({onFirst})");

            SoundModeSetting.HitConfirmOn = false;
            ctx.Check(Volley(enemyAt, 1) == 0, $"with its row off, a strike on the enemy sounds none");
            SoundModeSetting.Apply(SoundModeSetting.Original, null);
            ctx.Check(Volley(enemyAt, 1) == 0, $"never set, it is off under Original Sound");
            SoundModeSetting.Apply(SoundModeSetting.Original, true);
            ctx.Check(Volley(enemyAt, 1) == 1, $"and set on, it sounds under Original Sound too");
            SoundModeSetting.Apply(SoundModeSetting.Enhanced, null);
            ctx.Check(Volley(enemyAt, 1) == 1, $"never set, it is on under Enhanced Sound");
        }
        finally
        {
            foreach (var rig in rigs)
            {
                rig.QueueFree();
            }

            plate?.QueueFree();
            audio?.QueueFree();
            secondAudio?.QueueFree();
            pool.QueueFree();
        }
    }

    // The level the confirm's group plays at when every member carries one VOLUME, whichever is
    // drawn. Null when the members differ.
    private static float? LastConfirmLevel(System.Collections.Generic.Dictionary<string, SoundDef> defs,
        System.Collections.Generic.Dictionary<string, SoundGroup> groups, EnhancedSoundTuning tuning)
    {
        if (!groups.TryGetValue(tuning.HitConfirmSound, out var group))
        {
            return null;
        }

        var volumes = group.Members.Select(m => defs.TryGetValue(m.Name, out var d) ? d.Volume : -1f).Distinct().ToArray();
        return volumes.Length == 1 ? volumes[0] * tuning.HitConfirmGain : null;
    }
}
