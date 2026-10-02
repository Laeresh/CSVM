using System;
using System.Collections.Generic;
using CSVM.Effects;
using CSVM.Flight.Airframe;
using CSVM.Flight.Camera;
using CSVM.Flight.Hud;
using CSVM.Flight.Modes;
using CSVM.Flight.Weapons;
using CSVM.Mech3;
using CSVM.Session.Roster;
using CSVM.Session.World;
using CSVM.Spec;
using CSVM.UI.Labs;
using CSVM.UI.Overlays;
using CSVM.UI.Screens;
using CSVM.Utils;
using Godot;

namespace CSVM.Launch;

/// <summary>The inspection side of a session build. It builds the parked-plane view with its
/// damage, livery, mesh and marker labs, the freecam, and the animation lab's stage. It builds the
/// shared world selection with its node and world damage labs, and player 1's flight labs. It
/// builds the debug overlays every observing mode carries. Each is a step the session calls in its
/// build order, reading the <see cref="BuildState"/>; nothing here runs per frame.
/// Module entry: docs/architecture/Launch.md on src/Launch/InspectionLabs.cs.</summary>
internal sealed class InspectionLabs
{
    // The anim lab's timeline strip and transport panel own the bottom of the window; plain
    // freecam has nothing there.
    private const int AnimLabBottomMargin = 252;
    private const int FreecamBottomMargin = 16;

    private readonly Inputs _in;
    // Pickable subtrees that live beside the world content rather than under it, the anim lab's
    // parked --plane= prop. The selection and the node lab walk it beside the world root. It fills
    // during the world build, after those are created.
    private readonly List<Node3D> _selectionExtraRoots = new();

    // The session's shared world selection (--freecam/--anim-lab): the clicked leaf plus its
    // cs_name ancestor ladder, which every inspect tool reads instead of picking for itself.
    private SelectionService? _selection;
    // The node lab (N): tree panel, search, per-node actions and the dependency readout for
    // whatever the selection holds.
    private NodeLab? _nodeLab;
    // The world damage lab (F19): HP slider + kill/reset on the selection's destructible pool,
    // the interactive twin of --damage-test.
    private WorldDamageLab? _worldDamageLab;

    /// <summary>The labs of one session build.</summary>
    public InspectionLabs(Inputs inputs)
    {
        _in = inputs;
    }

    /// <summary>The free-flying camera of <c>--freecam</c> or the animation lab, null in every
    /// other mode.</summary>
    public SpectatorCamera? Spectator { get; private set; }

    /// <summary>The aircraft damage lab (F19): per-part HP sliders on the parked plane's visuals,
    /// or on P1's real damage in flight. Null where the plane has no destroyable parts.</summary>
    public DamageLab? DamageLab { get; private set; }

    /// <summary>The shared selection and its node and world damage labs, in the two modes that
    /// observe a live world with a cursor. They exist before the anim lab, which binds its camera
    /// follow to the selection. Each builds no HUD until something is picked, and joins the tree
    /// in <see cref="AttachLabs"/>.</summary>
    public void BuildWorldLabs(BuildState state, WorldSession world, WorldEffectsFactory effects)
    {
        if (!_in.Spec.Freecam && !_in.Spec.AnimLab)
            return;

        var spec = _in.Spec;
        int bottom = spec.AnimLab ? AnimLabBottomMargin : FreecamBottomMargin;
        _selection = new SelectionService(world.Root, _in.Camera)
        {
            DebugPick = spec.DebugSelect != null ? SelectionService.ParseDebugPick(spec.DebugSelect) : null,
            ExtraRoots = _selectionExtraRoots,
        };
        // The node lab reads that selection. Its camera is resolved through a
        // delegate: the freecam is created further down, after this point.
        _nodeLab = new NodeLab(world.Root, _selection, world.Runtime, world.Program,
            world.Builder.Scene, _in.BuildsCollision)
        {
            ExtraRoots = _selectionExtraRoots,
            CameraSource = () => Spectator,
            DebugSpec = spec.DebugNodeLab,
            BottomMargin = bottom,
        };
        // The world damage lab reads the same selection. Its effects runtime is built
        // on the first damage action, not now, an untouched session pays nothing.
        var damageScene = world.Builder.Scene;
        var damageProgram = world.Program;
        var damageRuntime = world.Runtime;
        _worldDamageLab = new WorldDamageLab(_selection, damageRuntime, _in.BuildsCollision)
        {
            SelectByName = name => _nodeLab?.SelectByName(name) ?? false,
            EffectsSource = () => effects.EnsureWorldEffects(state.Gamez, damageScene, state.Textures,
                damageProgram, damageRuntime),
            DebugSpec = spec.DebugDamage,
            BottomMargin = bottom,
        };
    }

    /// <summary>The animation debugger's quiet stage. It holds the effect/crash anchor stage, the
    /// mission spawn point and the freecam-style camera. An optional parked plane prop and the
    /// AnimLab node itself go in after them.</summary>
    public void BuildAnimLabStage(BuildState state, WorldSession session, SpawnPicker spawns,
        LiveryResolver livery, ulong masterSeed)
    {
        var spec = _in.Spec;
        var worldRoot = _in.WorldRoot;
        // ⚠ Use ManualAdvance, never SetProcess(false). Godot re-enables processing at READY for a
        // node overriding _Process, and the runtime enters the tree after this line. The world
        // would then run at double speed, on fixed steps plus wall dt.
        session.Runtime.ManualAdvance = true;

        // The lab's effect/crash stage under ONE staging node it moves in front of the camera.
        // ⚠ Keep it indexed as its own subtree. A played def then resolves its puffer hosts and
        // anchors here, not onto a generic world node of the same name.
        var labStage = new Node3D { Name = "lab_stage_anchor" };
        // The template roots come from the crash/damage defs against this stage's own anchor set.
        // That set is built first and parented after, so the lab stages what
        // BuildFlightCrashRuntime derives without changing this subtree's child order.
        var labAnchors = WorldEffectsFactory.BuildCrashAnchorSet();
        int effectRoots = WorldEffectsFactory.BuildEffectStage(state.Gamez, session.Builder.Scene,
            labStage, WorldEffectsFactory.CrashStageRootNames(session.Program, state.Gamez, labAnchors));
        labStage.AddChild(labAnchors);
        session.Root.AddChild(labStage);
        session.Runtime.IndexStage(labStage);
        Log.Info("anim", $"anim-lab: stage, {effectRoots} effect template(s) + player anchor set built + indexed");

        // The spawn the mission would place the player at. The camera starts here, with the
        // interesting part of the map in view, and the optional parked plane sits on it.
        // Resolved once so the camera and plane agree.
        var labSpawns = SpawnPoints.LoadIa(state.MissionZrdrPath, spec.Scenario);
        var (spawnPos, spawnLook) = spawns.ChooseSpawn(labSpawns, state.MissionZrdrPath,
            spawns.ChooseSpawnBase(labSpawns), 0, "");

        // Camera: the freecam SpectatorCamera (RMB look, WASD/QE move), like --freecam,
        // in place of the orbit view, the lab drives it (Frame/FollowNode) on
        // play/pick. Starts at the mission spawn; --pos/--direction override.
        var camPos = spec.CamPos ?? spawnPos;
        var camLook = spec.CamDir is { } labDir ? camPos + labDir : spec.LookAt ?? spawnLook;
        var labCam = new SpectatorCamera(_in.Camera, camPos, camLook)
        {
            ShowReadout = false,
            LockCandidates = _in.LockCandidates,
        };
        // --node=: the mission spawn is meaningless on a single-subtree stage, frame
        // the subject instead, unless the tester placed the eye themselves.
        if (state.NodeAabb is { } nodeBox && spec.CamPos == null && spec.LookAt == null && spec.CamDir == null)
        {
            labCam.Frame(nodeBox);
        }
        worldRoot.AddChild(labCam);
        Spectator = labCam;

        // Optional stage prop: --plane= parks that aircraft at the mission spawn point, in the
        // Fortune Hunters livery and with no FlightController. It carries its docking-hook group
        // and is indexed, or a hook definition resolves nothing and plays placeless.
        if (spec.PlaneNames.Count > 0)
        {
            long mark = StartupProfile.Mark();
            var planesGamez = GameZ.Load(state.PlanesGamezPath);
            StartupProfile.Record("gamez", mark);
            mark = StartupProfile.Mark();
            var parkedBuilder = new PlaneBuilder(planesGamez, state.Textures,
                scheme: livery.SchemeFor(0, state.ZrdrPath, livery.NewPaintRng(),
                    livery.PatternsForPlane(planesGamez, spec.PlaneName)),
                patterns: livery.Patterns, dockingHook: true);
            var parked = parkedBuilder.Build(spec.PlaneName);
            StartupProfile.Record("plane", mark);
            state.MeshInstances += parkedBuilder.MeshInstanceCount;
            worldRoot.AddChild(parked);
            parked.Position = spawnPos;
            if ((spawnLook - spawnPos).LengthSquared() > 1e-6f)
            {
                parked.LookAtFromPosition(spawnPos, spawnLook, Vector3.Up);
            }
            // Indexed the way the effect stage is, since the bootstrap indexed the world first.
            // The RESET_STATE tail is what parks its hook arms.
            session.Runtime.IndexStage(parked);
            state.What += $" + parked '{spec.PlaneName}'";
            // The parked prop hangs beside the world content, outside the selection's walk. Without
            // this extra pick root neither a click nor the lab tree reaches it.
            _selectionExtraRoots.Add(parked);
        }

        var animLab = new AnimLab(session.Runtime, session.Program, labCam,
            labStage, state.Textures, state.Sounds, masterSeed, spec.PlayAnim,
            // On a --node= stage the subject IS the stage and is already framed. A re-aim on
            // every Play swings the camera off the only object there; the tower left the frame
            // on its own destruction.
            autoFrame: spec.CamPos == null && spec.LookAt == null && spec.CamDir == null
                       && state.NodeSubtree == null)
        {
            // Interactive shows the whole lab UI. A scripted --screenshot hides it so the shot
            // stays byte-identical, unless --debug-anim-ui forces it on, as --debug-livery does.
            ShowUi = !_in.CapturePending() || spec.DebugAnimUi,
            // The lab's camera follows whichever rung of the shared selection is current.
            Selection = _selection,
        };
        worldRoot.AddChild(animLab);
        state.AnimLabNode = animLab;
        Log.Info("anim", $"anim-lab: quiet stage, seed {masterSeed}, fixed dt 1/60{(spec.PlayAnim != null ? $", playing '{spec.PlayAnim}'" : "")}, freecam (RMB look, WASD/QE move); transport on the button panel, P pause · . step · R restart · F picker · N node lab; click an object to follow");
        state.What += " + anim lab";
    }

    /// <summary>The parked-plane static view, of the viewer or a bare plane launch. The model is
    /// built unpainted in the viewer and pre-painted otherwise. The damage and livery labs that only
    /// make sense parked follow. Returns the plane, the session's subject.</summary>
    public Node3D BuildParkedPlane(BuildState state, LiveryResolver livery)
    {
        var spec = _in.Spec;
        var worldRoot = _in.WorldRoot;
        // ⚠ Resolve the scheme once here, so the livery lab below opens on exactly what the plane
        // wears rather than a second roll of --paint=random.
        long mark = StartupProfile.Mark();
        var staticPatterns = livery.PatternsForPlane(state.Gamez, spec.PlaneName);
        var staticScheme = livery.SchemeFor(0, state.ZrdrPath, livery.NewPaintRng(), staticPatterns);
        // In --viewer the LIVERY LAB owns the livery and applies it itself. The model is built
        // bare, so paint has one write path, its Repaint. cockpitInterior rides the same gate as
        // damagePanels, with pcdp4/pcdp6 hidden.
        var builder = new PlaneBuilder(state.Gamez, state.Textures, damagePanels: spec.Viewer,
            scheme: spec.Viewer ? null : staticScheme, patterns: livery.Patterns,
            cockpitInterior: spec.Viewer);
        var plane = builder.Build(spec.PlaneName);
        StartupProfile.Record("plane", mark);
        state.MeshInstances = builder.MeshInstanceCount;
        state.What = $"'{spec.PlaneName}'";

        // Damage lab: per-part HP sliders driving the same DamageVisuals/puffer pipeline as flight.
        // Present in every --viewer session, opened at launch only by --damage.
        if (spec.Viewer)
        {
            mark = StartupProfile.Mark();
            var stats = PlaneStats.Load(state.ZrdrPath, spec.PlaneName);
            StartupProfile.Record("zrdr", mark);
            if (stats.DestroyableParts.Count == 0)
            {
                Log.Info("flight", $"damage lab: '{spec.PlaneName}' ({stats.DefName}) has no destroyable_parts");
            }
            else
            {
                // Stand-in puffers: the parked plane travels no distance, so the authored
                // distance-interval trail defs the flight lab plays would emit nothing here.
                var smoke = Puffer.MakePuffer(state.ZrdrPath, state.Textures, worldRoot, "pufftrails.json", "smokepuffer", ambience: _in.Ambience);
                var fire = Puffer.MakePuffer(state.ZrdrPath, state.Textures, worldRoot, "pufftrails.json", "firepuffer", ambience: _in.Ambience);
                var panelTrails = new List<Puffer>();
                for (int i = 0; i < 8; i++) // pool one per pdp panel, the lab can flip all of them
                    if (Puffer.MakePuffer(state.ZrdrPath, state.Textures, worldRoot, "pufftrails.json", "firepuffer", ambience: _in.Ambience) is { } pt)
                        panelTrails.Add(pt);
                // The healthy↔torn candidate sets from the authored defs, the viewer
                // has no anim program, so the two reader files are loaded directly.
                var pairingDefs = new List<AnimDefinition>();
                pairingDefs.AddRange(AnimDefs.LoadFileDefs(state.ZrdrPath, "player_destruct_reset.json"));
                pairingDefs.AddRange(AnimDefs.LoadFileDefs(state.ZrdrPath, "player-1.json"));
                var visuals = new DamageVisuals(builder.DamagePanels, plane, stats, smoke, fire, panelTrails,
                    DamageVisuals.PanelPairingSets(pairingDefs), cockpitPanels: builder.CockpitDamagePanels);
                // the HUD gauge cluster as a lab toggle (user request): the damage
                // dial mirrors the sliders, blinks on decreases like a flight hit
                var labGauges = GaugeCluster.Build(state.Gamez, spec.PlaneName, state.Textures, stats.DestroyableParts);
                DamageLab = new DamageLab(stats, new ViewerDamageTarget(visuals),
                    spec.DamagePreset, labGauges)
                {
                    StartHidden = !spec.DamageLab, // --damage opens it; plain --viewer waits for F19
                };
                worldRoot.AddChild(DamageLab);
                Log.Info("flight", $"damage lab: {stats.DestroyableParts.Count} part sliders, {visuals.PanelCount} panels, {panelTrails.Count} panel fire trails{(spec.DamageLab ? "" : " (hidden, F19)")}");
                state.What += spec.DamageLab ? " + damage lab" : " + damage lab (F19)";
            }
        }

        // Livery lab (--viewer, L): pattern, RGB sliders and decal slots repainting the parked plane
        // through PlaneBuilder.Repaint. Hidden and unpainted unless --paint named a scheme, so an
        // unadorned --viewer screenshot is unchanged.
        if (spec.Viewer && builder.SkinPrefix != null)
        {
            var lab = new LiveryLab(builder, livery.PaintCatalog(state.ZrdrPath), state.Textures, staticScheme,
                livery.Patterns.PatternsFor(builder.SkinPrefix))
            {
                DebugShow = spec.DebugLivery.HasValue,
                DebugPatternSteps = spec.DebugLivery ?? 0,
            };
            worldRoot.AddChild(lab);
            state.What += " + livery lab";
        }

        if (spec.Viewer)
            state.What += " + mesh lab";
        return plane;
    }

    /// <summary>The labs shared by every mode that observes the subject, once it has joined the
    /// tree. They are the world selection with its node and damage labs, the viewer's mesh lab and
    /// the marker overlay.</summary>
    public void AttachLabs(BuildState state, Node3D? plane)
    {
        var spec = _in.Spec;
        var worldRoot = _in.WorldRoot;
        // The shared selection joins after the world does. Its pick walk and highlight box read
        // GlobalTransform, which on a detached subtree is identity + error spam.
        if (_selection != null)
        {
            worldRoot.AddChild(_selection);
        }
        // The node lab joins after the selection, so its first _Process (which carries the
        // scripted dump) runs once the selection's own scripted pick has settled.
        if (_nodeLab != null)
        {
            worldRoot.AddChild(_nodeLab);
        }
        // The damage lab joins after the node lab, so a scripted script can select through the
        // node lab's name index on the frame it runs.
        if (_worldDamageLab != null)
        {
            worldRoot.AddChild(_worldDamageLab);
        }
        // Mesh lab (--viewer, M): normals, wireframe, zone boxes, lighting and the cull/normal
        // overrides. ⚠ Build it after the plane joins the tree. It reads geometry back through
        // GlobalTransform, which on a detached node returns identity and logs per call.
        if (spec.Viewer && plane != null)
            worldRoot.AddChild(new MeshLab(plane, PlaneCollider.Build(plane),
                _in.Sun, _in.Env, _in.Camera)
            { DebugSpec = spec.DebugMesh });
        // Marker overlay (--viewer --plane, K): the firepoint, pylon and target gizmos. Only on the
        // parked plane, since a chapter world has no marker rig. It reads each marker's
        // GlobalPosition, so it follows the plane into the tree.
        if (spec.Viewer && !spec.WorldMode && plane != null)
        {
            worldRoot.AddChild(new MarkerOverlay(plane) { StartHidden = !spec.MarkersOverlay });
            state.What += spec.MarkersOverlay ? " + marker overlay" : " + marker overlay (K)";
        }
    }

    /// <summary>Spectator mode: the live world with no aircraft, seen from a free-flying camera. It
    /// starts where the mission would have spawned the player, or wherever the placement flag put
    /// it.</summary>
    public void BuildFreecam(BuildState state, SpawnPicker spawns)
    {
        var spec = _in.Spec;
        if (!spec.Freecam)
            return;
        Vector3 camPos, camLookAt;
        if (spec.EmptyStage)
        {
            // The empty stage has no mission and therefore no spawn list: look at the grid
            // origin, which is where a --stage=empty subject is put.
            camPos = EmptyStage.CameraPos;
            camLookAt = Vector3.Zero;
        }
        else
        {
            var freecamSpawns = SpawnPoints.LoadIa(state.MissionZrdrPath, spec.Scenario);
            (camPos, camLookAt) = spawns.ChooseSpawn(freecamSpawns, state.MissionZrdrPath,
                spawns.ChooseSpawnBase(freecamSpawns), 0, "");
        }
        if (spec.CamPos is { } cp) camPos = cp;
        // The aim: a direction from wherever the eye ended up, or the named point.
        if (spec.CamDir is { } cd) camLookAt = camPos + cd;
        else if (spec.LookAt is { } la) camLookAt = la;
        Spectator = new SpectatorCamera(_in.Camera, camPos, camLookAt)
        {
            // A scripted --screenshot run wants the frame clean of the overlay.
            ShowReadout = !_in.CapturePending(),
            LockCandidates = _in.LockCandidates,
        };
        _in.WorldRoot.AddChild(Spectator);
        state.What += " + freecam";
        Log.Info("core", $"freecam: spectator camera at ({camPos.X:0}, {camPos.Y:0}, {camPos.Z:0}), hold RMB to look, WASD/QE to move, Shift boost, wheel sets speed; click an object to select it, PgUp/PgDn walk its ancestor ladder (Home/End jump), N opens the node lab, F19 the damage lab on whatever destructible is selected");
    }

    /// <summary>The damage lab in flight (F19), the viewer's panel bound to P1's real damage. A
    /// dialled-in state then drives the HUD and can be flown. Splitscreen binds P1 only, since the
    /// panel is one overlay, not one per pane.</summary>
    public void BuildFlightDamageLab(BuildState state, IReadOnlyList<PlayerRig> rigs, Func<PlaneStats> p1Stats)
    {
        var spec = _in.Spec;
        if (rigs.Count > 0 && rigs[0].Controller is { Damage: not null } p1)
        {
            var stats = p1Stats();
            DamageLab = new DamageLab(stats,
                new FlightDamageTarget(p1, rigs.Count > 1 ? "P1" : null), spec.DamagePreset)
            {
                StartHidden = !spec.DamageLab, // --damage opens it; a plain flight waits for F19
                RightAligned = true,            // the top-left corner is the flight HUD's
            };
            _in.WorldRoot.AddChild(DamageLab);
            Log.Info("flight", $"damage lab: {stats.DestroyableParts.Count} part sliders on the flown plane's armor+HP{(spec.DamageLab ? "" : " (hidden, F19)")}");
            state.What += spec.DamageLab ? " + damage lab" : " + damage lab (F5)";
        }
        else if (spec.DamageLab)
        {
            Log.Info("flight", $"damage lab: '{spec.PlaneName}' has no destroyable_parts");
        }
    }

    /// <summary>The weapon lab in flight, bound to player 1's held aircraft inside a real chapter
    /// world. ⚠ Keep it firing through the session's own fully-wired ProjectilePool. A scene-less
    /// pool is the parked-plane probe's, which never reaches here.</summary>
    public void BuildWeaponLab(BuildState state, IReadOnlyList<PlayerRig> rigs, WeaponDefs weaponDefs)
    {
        var spec = _in.Spec;
        if (spec.WeaponLab && rigs.Count > 0 && rigs[0] is { Controller: { PlaneModel: not null } p1c } labRig)
        {
            // A soak run must never dry up: the lab exists to watch a weapon fire, not to manage
            // ammo. Explicit flags still win, --ammo=N caps the load on purpose.
            p1c.InfiniteAmmo = true;
            // --weapon-fire holds the real trigger, the one free flight pulls. Which one follows
            // the panel's bank, so the lab sets it rather than this call site.
            var lab = new WeaponLab(p1c.PlaneModel, weaponDefs, p1c.Loadout, spec.PlaneName,
                host: p1c, camera: labRig.Camera)
            {
                DebugShow = true,   // the lab IS the session now, the panel is why you launched it
                InitialWeapon = spec.WeaponSelect,
                InitialMount = spec.WeaponMount,
                AutoFireAtStart = spec.WeaponFire,
                CycleFrames = spec.WeaponCycle,
                DebugClickRequested = spec.WeaponClick,
                DebugClick = spec.WeaponClickAt,
                DebugClickAimOnly = spec.WeaponClickAimOnly,
                DebugTarget = spec.WeaponTarget,
                DebugSurface = spec.WeaponSurface,
                StandoffAtStart = spec.WeaponStandoff,
                FreeCameraAtStart = spec.WeaponFreeCamera,
                CameraToggleFrames = spec.WeaponCameraToggle,
            };
            _in.WorldRoot.AddChild(lab);
            // The lab is one overlay on one aircraft, and its camera hand-off takes that rig's
            // camera. In splitscreen it binds P1 and says so, rather than silently leaving the
            // other panes without a panel.
            if (rigs.Count > 1)
            {
                Log.Info("weapons", $"weapon lab: {rigs.Count} players, the lab binds P1's aircraft and P1's pane only; the other panes fly normally");
            }
            Log.Info("weapons", $"weapon lab: '{spec.PlaneName}' held {(spec.EmptyStage ? "on the empty stage" : $"in {spec.Chapter}")}, firing through the session pool{(spec.WeaponFire ? " (--weapon-fire: trigger held)" : "")}{(spec.WeaponCycle > 0 ? $" (--weapon-cycle: a weapon every {spec.WeaponCycle} frames)" : "")}");
            // The authored impact/destruction effects need the world-effects runtime, which is only
            // built when there IS a world program, say so rather than silently drawing stand-ins.
            if (state.WorldScene == null)
            {
                Log.Info("weapons", $"weapon lab: no world program on this stage, impacts fall back to the pool's stand-in burst and rockets fly without their FLYOUT body/trail");
            }
            state.What += " + weapon lab";
        }
        else if (spec.WeaponLab)
        {
            Log.Info("weapons", $"weapon lab: no flight rig to host it (nothing was built to hold)");
        }
    }

    /// <summary>The post-build overlays: the freecam/anim-lab mesh lab on the selection, the
    /// collider and class overlays, the map edge's tile grid and the node-name labels.</summary>
    public void BuildOverlays(BuildState state, Node3D? plane, IReadOnlyList<PlayerRig> rigs,
        MapEdgeExtender? edgeExtender)
    {
        var spec = _in.Spec;
        var worldRoot = _in.WorldRoot;
        // Mesh lab on the selection (M), the freecam/anim-lab twin of the viewer's lab. It owns no
        // subtree until M attaches it to the selection, and restores that subtree when M lets go.
        // Until then it builds and changes nothing.
        if (_selection != null)
        {
            worldRoot.AddChild(new MeshLab(_selection, _in.Sun, _in.Env, _in.Camera)
            {
                DebugSpec = spec.DebugMesh,
                // A scripted capture is about the geometry, not the panel over it.
                ShowPanel = !_in.CapturePending(),
            });
        }

        // Collider wireframes (F20), built in the modes that observe a live world. It draws what the
        // collision build produced, and says so loudly when the mode built none rather than
        // rendering an empty overlay.
        if ((spec.Freecam || spec.AnimLab || spec.Fly) && plane != null)
        {
            var planeColliders = new List<(Node3D, PlaneCollider)>();
            foreach (var rig in rigs)
            {
                // ⚠ Collider.Parts.Local is in the plane MODEL's parent frame, not the model's own.
                // The model root carries its own GameZ transform, so boxes drawn as its children
                // would apply it twice.
                if (rig.Controller is { Collider: { } airframe, PlaneModel: { } } controller)
                {
                    planeColliders.Add((controller, airframe));
                }
            }
            worldRoot.AddChild(new ColliderOverlay(plane, _in.BuildsCollision)
            {
                DebugShow = spec.ShowColliders,
                Planes = planeColliders,
                // What each surface id resolves to on contact, asked of this session's own program.
                // The overlay colours by the id a touch will select, and the bound program decides
                // which ids have a def of their own.
                ResolvedSurfaceIds = state.CrashProgram != null
                    ? EffectCatalogue.ResolvedSurfaceIds(state.CrashProgram)
                    : null,
            });
            Log.Info("world", $"collider overlay ready (F20){(_in.BuildsCollision ? "" : ", but this mode built NO collision; relaunch with --collision")}");

            // Colour-by-class overlay (F21): same mode set as the collider overlay, since it reads
            // the same live world, a findable-targets view, not a collision one.
            worldRoot.AddChild(new ClassOverlay(plane, state.Gamez, state.WorldRuntime)
            {
                DebugShow = spec.ShowClassOverlay,
            });
            Log.Info("world", $"class overlay ready (F21)");
        }
        else if (spec.ForceCollision && spec.WorldMode)
        {
            // The static viewer builds the bodies but no overlay, so it says so rather than leaving
            // F20 to do nothing.
            Log.Info("world", $"--collision built the world's colliders, but the F20 collider overlay is not built in this mode, use --freecam to see them");
        }

        // Map-edge tile grid (--debug-tilegrid, flag-only). Gated on the extender rather than a
        // mode list, because "there is a continuation to colour" is exactly the precondition.
        if (edgeExtender != null && plane != null)
        {
            worldRoot.AddChild(new TileGridOverlay(plane, edgeExtender)
            {
                DebugShow = spec.ShowTileGrid,
            });
            Log.Info("world", $"tile-grid overlay ready (--debug-tilegrid)");
        }
        else if (spec.ShowTileGrid)
        {
            Log.Warn("world", $"--debug-tilegrid: this mode builds no map-edge continuation, so there is no tile grid to colour (it exists in --fly, --freecam, and a --sky-zone viewer)");
        }

        // Node-name labels (T), in both the viewer and flight, over the whole session subtree, so
        // the world and the aircraft are labelled alike. Off until pressed, and it builds nothing
        // until then. In splitscreen the selection follows P1 but the labels render in every pane.
        var flownPlanes = new List<Node3D>();
        foreach (var rig in rigs)
            if (rig.Controller?.PlaneModel is { } flown)
                flownPlanes.Add(flown);
        worldRoot.AddChild(new NodeLabels(worldRoot, rigs.Count > 0 ? rigs[0].Camera : _in.Camera)
        {
            InitialMode = spec.DebugNames == null ? NodeLabels.Mode.Off : NodeLabels.ParseMode(spec.DebugNames),
            // The flown aircraft sits metres from the camera while the world is hundreds of
            // metres away, so without this it wins every label slot. Empty in --viewer, where
            // the parked aircraft IS the subject.
            Deprioritise = flownPlanes,
        });
    }

    /// <summary>What one session's labs are built over.</summary>
    internal sealed class Inputs
    {
        public SessionSpec Spec = null!;
        public Node3D WorldRoot = null!;
        // The persistent main camera, sun and Environment the Launcher owns.
        public Camera3D Camera = null!;
        public DirectionalLight3D Sun = null!;
        public Godot.Environment? Env;
        public EffectAmbience Ambience = null!;
        // Whether a scripted capture is pending, which hides the labs' panels from the shot.
        public Func<bool> CapturePending = null!;
        public Func<IReadOnlyList<Node3D>> LockCandidates = null!;
        // Whether this session builds colliders, the one definition every lab reads.
        public bool BuildsCollision;
    }
}
