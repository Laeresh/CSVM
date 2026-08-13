using System;
using System.Collections.Generic;
using CSVM.Utils;
using Godot;

namespace CSVM.Mech3;

/// <summary>
/// Builds one chapter world and binds its animation program to it: the world+anim half of
/// <see cref="CSVM.Session.GameSession"/>'s session build. Extracted from that class
/// so the <c>--anim-lab</c> mode builds the same world+runtime a
/// normal flight/viewer session does, without duplicating any of it.
///
/// <para>Does the load → <see cref="WorldBuilder"/> → clutter → mission setup →
/// <see cref="AnimProgram"/> → <see cref="AnimRuntime"/> collaborator wiring → <c>Bind</c> →
/// sound-prewarm core. It deliberately stops before the per-rig horizon, weather, edge-extender,
/// and unplaced-entity watch — those are per-view and stay in the caller, which drives them off
/// the returned <see cref="Builder"/>. It also does NOT add <see cref="Root"/> to the scene tree;
/// the caller owns that, and the effect siblings (world sounds, puffers) go under the caller's
/// <see cref="Options.EffectsParent"/> exactly as before.</para>
///
/// <para><b>Disposal-lifetime contract (semantics, not incidental).</b> A puffer bakes its atlas
/// at construction and world-sound streams decode on demand, so both the <c>IEmitterFactory</c> and
/// the sound <c>Loader</c> outlive this build holding a reference to an archive whose zip handle
/// the caller may close. Each is therefore retired after the bootstrap **unless the caller says it
/// owns that archive for longer** — <see cref="Options.TexturesOutliveBuild"/> and
/// <see cref="Options.SoundsOutliveBuild"/>. The two are separate because the two lifetimes are:
/// a game session hands the <see cref="TextureArchive"/> to the session (freed on return-to-menu)
/// while its <see cref="SoundArchive"/> stays a <c>using</c> local of the build, and only the lab
/// keeps both. Sounds are also prewarmed while the archive is open regardless, which is what makes
/// clearing the loader survivable; puffers have no equivalent, since a puffer bakes per authored
/// state rather than per name — so a retired factory is no fire, no dust, no smoke for
/// every <c>PUFFER_STATE</c> reached after the bootstrap, which is why it says so once.</para>
///
/// <para><b>The <c>--node=</c> stage is the same pipeline with three steps switched off.</b>
/// <see cref="Options.NodeSubtree"/> replaces the world build with one named gamez subtree
/// (<see cref="WorldBuilder.BuildNode"/>), skips the mission setup script and the clutter pass, and
/// turns on the animation runtime's bind census — the program still loads and still binds, because
/// what a partial world does to the bind is the whole question that stage exists to answer.</para>
///
/// <para><b>The phase boundaries are a reported contract.</b> Each step above records its own
/// span into <see cref="StartupProfile"/> (<c>zrdr</c>, <c>world</c>, <c>clutter</c>, <c>anim</c>,
/// <c>bind</c>, <c>prewarm</c>) and those spans are the bulk of the <c>[perf] startup</c> line's
/// accounting. Reordering or merging a step means moving its <c>Record</c> call with it — a phase
/// silently dropped does not read as missing, it reads as a shrinking <c>rest</c>.</para>
/// </summary>
public sealed class WorldSession
{
    private WorldSession() { }

    /// <summary>The built world subtree (the viewer's <c>_plane</c> in world mode): the
    /// <c>world1</c> node with the texture cycler, clutter, any debug dzpaths, and the
    /// <see cref="AnimRuntime"/> as children. <b>Not</b> yet added to the scene tree — the caller
    /// adds it under <see cref="Options.EffectsParent"/>.</summary>
    public Node3D Root { get; private set; } = null!;

    public AnimRuntime Runtime { get; private set; } = null!;

    /// <summary>The merged animation program (compiled + reader defs). Held so the caller can read
    /// crash-effect params off it and so the lab can drive its picker/timeline from it.</summary>
    public AnimProgram Program { get; private set; } = null!;

    /// <summary>The world builder, kept so the caller can run the per-view steps that stay outside
    /// this class: the unplaced-entity watch, the edge extender, the per-rig horizon, and the final
    /// mesh/collider/scroll counts.</summary>
    public WorldBuilder Builder { get; private set; } = null!;

    /// <summary>The clutter builder (null when the chapter ships no clutter templates), kept for
    /// the edge extender.</summary>
    public ClutterBuilder? Clutter { get; private set; }

    /// <summary>The world's cloudlayer overcast, if any — moved to follow the player by the
    /// caller.</summary>
    public Node3D? CloudDeck { get; private set; }

    /// <summary>The session's LIGHT_STATE point lights. The caller owns it (disposes it on
    /// teardown) so a rebuild drops the previous world's lights.</summary>
    public WorldLights Lights { get; private set; } = null!;

    /// <summary>Build the world named <c>world1</c> and bind its animation program. The archives
    /// are the caller's <c>using</c> locals — see the disposal-lifetime contract on the class.</summary>
    public static WorldSession Build(Options o, GameZ gamez, TextureArchive textures,
        SoundArchive? sounds, Dictionary<string, SoundDef>? soundDefs,
        IReadOnlyDictionary<string, SoundGroup>? soundGroups = null)
    {
        var s = new WorldSession();

        // The engine's per-mission world setup script (interp support\<ch>\<mis>.gw): which of the
        // chapter's entities this mission shows, and which of its surfaces animate their UVs.
        // Loaded before the build because the scroll rates are part of the material cache key (see
        // MissionSetup.ScrollByModel); the entity half is applied afterwards, as the animation
        // runtime's bootstrap pass 0.
        long mark = StartupProfile.Mark();
        MissionSetup? missionSetup = null;
        if (o.NodeSubtree != null)
        {
            // The node stage deliberately skips it: nearly every verb would name a node this
            // subtree does not contain, and the one thing the script reliably WOULD do is switch
            // the requested subject off (C1/IA1 hides `hk_zep`). So a --node= build shows the
            // subtree in its gamez base state, not in this mission's state.
            Log.Info("world", $"node stage: mission setup skipped for {o.Chapter}/{o.Mission} — the subtree renders in its gamez base state");
        }
        else
        {
            missionSetup = MissionSetup.Load(o.InterpPath, o.Chapter, o.Mission);
            if (missionSetup == null)
            {
                GD.Print($"mission setup: no script for {o.Chapter}/{o.Mission} ({o.InterpPath})");
            }
        }
        StartupProfile.Record("zrdr", mark);
        // The EFFECTS flipbooks (effects.zrd) are installed on their target materials BEFORE the
        // build, because a material's frame list is part of what SceneBuilder registers with the
        // TextureCycler as it builds. Same ordering the engine uses. See EffectCycles.
        var effectCycles = EffectCycles.Apply(gamez, o.ZrdrPath);
        if (effectCycles.Count > 0)
        {
            GD.Print($"effect cycles: {effectCycles.Count} material(s): " + string.Join(", ", effectCycles));
        }
        mark = StartupProfile.Mark();
        var builder = new WorldBuilder(gamez, textures, collision: o.Collision,
            scrollOverrides: missionSetup?.ScrollByModel(gamez),
            debugClutterFlag: o.DebugClutterFlag);
        s.Builder = builder;
        // every chapter has exactly one world node; --node= replaces it with one named subtree
        var root = o.NodeSubtree is { } only
            ? builder.BuildNode(gamez, only)
            : builder.Build("world1");
        StartupProfile.Record("world", mark);
        s.Root = root;
        // The original's material texture flipbooks (animated water/surf/wake/splash and the
        // walking crowd). Parented to the world so a session teardown takes it too.
        if (builder.Cycler.Count > 0)
        {
            builder.Cycler.Debug = o.DebugAnim;
            root.AddChild(builder.Cycler);
            GD.Print($"texture cycles: {builder.Cycler.Count} animated material(s): " + string.Join(", ", builder.Cycler.Summary));
        }
        s.CloudDeck = builder.CloudDeck;     // the cloudlayer overcast, moved to follow the player

        // --debug-dzpaths: the mission's danger-zone route ribbons (world build skips them —
        // AI/route data the original never renders). Debug inspection only.
        if (o.DebugDzPaths && builder.BuildDzPaths() is { } dzpaths)
        {
            root.AddChild(dzpaths);
            GD.Print("debug: dzpaths route ribbons built");
        }

        // Clutter: forest trees / river bushes, and C2/C5's 3D city-block buildings. The chapter's
        // boot script names the templates; ClutterBuilder stamps them onto every matching-textured
        // world polygon (see Clutter.cs). Sprites are never solid — a billboard has no side to hit
        // (user decision; the "trees are hittable" justification rested on a misread of
        // `spruce_destroy`, which is the Spruce Goose) — but the 3D decorations are, in flight,
        // since they are real geometry (also a user decision).
        mark = StartupProfile.Mark();
        ClutterBuilder? clutterBuilder = null;
        Node3D? clutterRoot = null;
        // Clutter stamps onto matching-textured world terrain, of which a --node= stage has none.
        // --clutter-templates= replaces the chapter's registered set outright, so one district can
        // be A/B'd against the original; the per-polygon no_clutter gate still applies either way.
        // --no-clutter still wins, since it is the stronger statement.
        var clutterNames = o.NodeSubtree != null || o.NoClutter
            ? new List<string>()
            : o.ClutterTemplates is { } wanted
                ? ClutterBuilder.OverrideTemplateNames(gamez, wanted)
                : ClutterBuilder.TemplateNames(o.InterpPath, o.Chapter);
        if (o.NoClutter)
        {
            GD.Print("clutter: skipped (--no-clutter)");
        }
        else if (clutterNames.Count > 0)
        {
            // templates.zrd drives `substitute` and `scale_range` (docs/formats/templates.md).
            // Null when the chapter ships no such file — every decoration then stands at its
            // authored model and size, which is also what an empty file (C1C/C2B) produces.
            var clutterProps = ClutterTemplateSpec.Load(SessionPaths.ChapterZrdr(o.DataRoot, o.Chapter));
            clutterBuilder = new ClutterBuilder(gamez, textures, builder.Scene, clutterProps);
            if (clutterBuilder.Build(clutterNames, collision: o.Collision) is { } clutter)
            {
                root.AddChild(clutter);
                clutterRoot = clutter;
                GD.Print($"clutter: {clutterBuilder.InstanceCount} sprites"
                         + (clutterBuilder.SolidCount > 0
                             ? $" + {clutterBuilder.SolidCount} 3D decorations"
                               + (clutterBuilder.SolidCollisionShapes > 0
                                   // Shared shapes: N distinct shapes / T distinct triangles,
                                   // attached M times — the distinct totals, not the expanded
                                   // per-attachment triangle count.
                                   ? $" ({clutterBuilder.SolidCollisionShapes} shared collision shapes"
                                     + $", {clutterBuilder.SolidCollisionTriangles} tris"
                                     + $", {clutterBuilder.SolidCollisionInstances} attachments)"
                                   : "")
                             : "")
                         + $" ({clutterBuilder.Summary})");
            }
        }
        else if (o.NodeSubtree == null)
        {
            GD.Print($"clutter: no templates for {o.Chapter} ({o.InterpPath})");
        }
        StartupProfile.Record("clutter", mark);
        s.Clutter = clutterBuilder;

        // --debug-clutterflag: force every clutter population blue, and print the census that
        // explains the picture. The blue is the one colour the world shader cannot express by
        // itself — a decoration's OWN polygons are unflagged, so under the flag colours a whole
        // city block would read as clutter-eligible ground, which is exactly how an earlier
        // throwaway probe was misread.
        if (o.DebugClutterFlag)
        {
            int painted = clutterRoot != null ? TintClutterBlue(clutterRoot) : 0;
            GD.Print("debug: --debug-clutterflag view — world polygons "
                     + $"no_clutter={builder.Scene.FlaggedPolygonCount} (red), "
                     + $"clear={builder.Scene.ClearPolygonCount} (green), over the models built for "
                     + $"{o.Chapter}; clutter blue ({painted} multimesh"
                     + (painted == 1 ? ")" : "es)"));
        }

        // Animations: bind the mission's animation program to the built world and run it. Base
        // states first (hides the destroyed building variants behind their healthy twins, and the
        // zeppelins/trains this mission deactivates), then the ON_STARTUP definitions and the
        // mission's startanims — which now *play* rather than being posed at their end state, so
        // hangar doors swing and the C1 train drives its SI-script track loop. The program merges
        // the compiled cam_anim/mis_anim archives (richer, and the only source of SI scripts) with
        // the three zrdr scopes (the only source of zepstate/startanims).
        mark = StartupProfile.Mark();
        var chapterZrdrPath = SessionPaths.ChapterZrdr(o.DataRoot, o.Chapter);
        var (chapterAnimPath, missionAnimPath) =
            AnimProgram.ArchivePaths(o.DataRoot, o.Chapter, o.Mission);
        var animProgram = AnimProgram.Load(o.ZrdrPath, chapterZrdrPath, o.MissionZrdrPath,
            chapterAnimPath, missionAnimPath);
        StartupProfile.Record("anim", mark);
        s.Program = animProgram;
        // The runtime builds PUFFER_STATE emitters through this factory rather than holding the
        // TextureArchive: an emitter bakes its atlas at construction. Retired right after the
        // bootstrap only when `textures` dies with the caller's build scope — see
        // Options.TexturesOutliveBuild.
        var lights = new WorldLights();
        s.Lights = lights;
        // The world runtime's template stage, sealed at construction: the
        // ambient world pools nothing and stages nothing hidden — its templates ARE the world's own
        // nodes — and only the animation debugger's quiet stage relocates a called template onto the
        // call site, which is why that one flag is an Option rather than a constant.
        var animRuntime = new AnimRuntime(AnimRuntime.NewTemplateStage(
            placesCalled: o.PlacesCalledTemplates, debugMotions: o.DebugAnim))
        {
            DebugMotions = o.DebugAnim,
            QualityLod = o.AnimLod,
            AutoStart = o.AutoStart,
            Seed = o.RuntimeSeed,
            Setup = missionSetup,
            EmitterFactory = o.EmitterFactory ?? new Anim.PufferEmitterFactory(textures, o.EffectsParent),
            // Where LIGHT_STATE spill reaches the fullbright world shader. Owned by the caller so a
            // teardown drops the previous world's lights.
            Lights = lights,
            // The world's ambient SOUND_NODE emitters. Null when muted or soundless, which makes
            // the whole feature inert rather than half-built.
            Sounds = soundDefs != null && sounds != null
                ? new WorldSounds(soundDefs, soundGroups)
                {
                    Loader = (d, warn) => sounds.Find(d.WavName, d.Looped, warn),
                    Debug = o.DebugAnim,
                }
                : null,
            // PLAYER_RANGE conditions measure from the player, resolved per call because no camera
            // exists yet here.
            PlayerPosition = o.PlayerPosition,
            PlayerPositions = o.PlayerPositions,
            // On a single-subtree stage most definitions legitimately resolve nothing, so the bind
            // has to SAY which of "no handler ever fires" and "the node is not here" happened —
            // from outside they are the same still object.
            ReportResolution = o.NodeSubtree != null,
            // MaxRootLift's premise is a whole-world node count; one subtree drops under the cap
            // and lets generic ANIMATION_ROOT_NAMEs anchor definitions that have nothing to do
            // with it. Refused here so the lab's picker lists what actually belongs to the stage.
            SuppressRootLift = o.NodeSubtree != null,
        };
        s.Runtime = animRuntime;
        // The emitter pool has to be in the tree before the bootstrap builds into it.
        if (animRuntime.Sounds is { } worldSounds)
        {
            o.EffectsParent.AddChild(worldSounds);
            worldSounds.SetListener(o.PlayerPosition);
        }
        // A death-triggered CALL_ANIMATION whose callee anchors on a "library root" gamez node —
        // staged with the game but never PLACED in it (docs/formats/gamez.md; GameZ.IsLibraryRoot)
        // — needs that root built before AnimRuntime can drive it: WorldBuilder's own walk never
        // reaches it (C2's facade panels' shared `facdsticks` template is exactly this
        // shape). Built LAZILY, the first time a call actually needs it, rather than eagerly with
        // the rest of the ~150-member library: eager construction has no reliable way to also
        // exclude every OTHER subsystem that already claims some of these same roots by name — the
        // effects/crash runtimes' own `EffectStageRoots`/`EffectTemplateRoots` staging, and
        // `Projectile`'s own on-demand weapon-model `BuildSubtree` calls — and at least one library
        // root (`genx12`) must stay UNBUILT on purpose (its own `Targets` rescue redirects onto the
        // caller's subtree instead; see `AnimRuntime`'s `CallAnimation` case). Lazy-on-call is
        // naturally scoped to exactly the defs an anim actually calls, so it can never duplicate or
        // pre-empt any of them — observationally identical to the original's own loading strategy
        // from the cockpit either way (every library root starts parked and inert regardless of
        // when its node is constructed).
        //
        // POOLED, not one shared copy: the original runs several call sites' copies of one
        // template in parallel (measured from original-game footage — several broken facade panels'
        // four-log sets airborne at once, not "latest wins"). Pool SIZE is the same three-layer
        // answer `EffectPools`/`effect_pools.json` already gives the effects-runtime side: the
        // gamez census is checked FIRST for an authored duplicate-copy count (several effect
        // templates ship exactly that — sonic_ring/sonic_flare x5, flame_ball_01-_03, etc. — see
        // docs/formats/gamez.md), and only when a template ships exactly one record (facdsticks
        // does) does the count fall to `effect_pools.json`'s own `localCallRoots` TUNE entry, one
        // config surface for every pool this engine invents rather than one number per subsystem.
        // No authored-duplicate lookup is wired here YET because nothing today calls a
        // multi-record library root through this path — see that file's own remark before adding
        // one. Each caller (keyed by its own anchor) keeps its OWN copy across repeat calls (a
        // re-killed panel gets its copy back, not a fresh one) and a pool at capacity recycles its
        // oldest — the same "later call wins" collapse the single-copy path always had, now
        // bounded to the wrap instead of every call. `IndexPooledCopy` both indexes the new
        // subtree by NAME only (never `_byIndex` — every copy shares the source's compiled
        // indices, so a second copy claiming `_byIndex` would silently steal the first copy's
        // node references, see that method's own remark) and RESET_STATE-poses whatever anchors
        // on it — the same "arrives hidden until summoned" pass the anim-lab's own effect-template
        // stage gets via `IndexStage`. A no-op for `--node=`, which deliberately builds only the
        // requested subtree.
        if (o.NodeSubtree == null)
        {
            var pools = EffectPools.Load();
            var libraryPools = new Dictionary<string, List<(Node3D Copy, ulong Owner)>>(StringComparer.OrdinalIgnoreCase);
            animRuntime.ResolveLibraryRoot = (name, callAnchor) =>
            {
                if (!libraryPools.TryGetValue(name, out var copies))
                {
                    if (gamez.FindByName(name) is not { } firstLookup || !gamez.IsLibraryRoot(firstLookup))
                        return null;
                    copies = new List<(Node3D, ulong)>();
                    libraryPools[name] = copies;
                }
                ulong ownerId = callAnchor.GetInstanceId();
                foreach (var (copy, owner) in copies)
                    if (owner == ownerId)
                        return copy; // this caller's own prior copy — reuse it, not a fresh one
                if (gamez.FindByName(name) is not { } gzNode)
                    return null; // unreachable past the first successful lookup above
                if (copies.Count < pools.LocalCallPoolSize(name))
                {
                    if (builder.Scene.BuildSubtree(gzNode, collisionSkip: _ => true) is not { } subtree)
                        return null;
                    subtree.Transform = Transform3D.Identity;
                    root.AddChild(subtree);
                    animRuntime.IndexPooledCopy(subtree);
                    copies.Add((subtree, ownerId));
                    return subtree;
                }
                // Pool at capacity: recycle the oldest-owned copy (round-robin), same wrap the
                // single-copy path always had.
                var (recycled, _) = copies[0];
                copies.RemoveAt(0);
                copies.Add((recycled, ownerId));
                return recycled;
            };
        }
        mark = StartupProfile.Mark();
        animRuntime.Bind(root, animProgram);
        StartupProfile.Record("bind", mark);
        foreach (string line in animRuntime.ResolutionLines())
        {
            Log.Info("anim", $"{line}");
        }
        // Only the REAL factory this method built itself is tied to `textures`'s scope — a
        // caller-supplied one (the harness's CountingEmitterFactory) holds no archive reference at
        // all, so it needs no retirement and TexturesOutliveBuild is not its caller's concern.
        if (o.EmitterFactory == null && !o.TexturesOutliveBuild)
        {
            animRuntime.Emitters.RetireFactory();
        }
        // Same rule as the puffer factory: the zip handle dies with the caller's build scope. The
        // decoded streams stay cached in WorldSounds, so an emitter created later reusing a name
        // already heard still works — but "already heard" is not enough on its own. Most SOUND_NODE
        // events are first reached at RUNTIME (an OnCall def, or a CallSequence that lands a frame
        // after bootstrap, like C1's police siren), i.e. always after this line. So decode
        // everything the program can ask for first.
        if (animRuntime.Sounds is { } builtSounds)
        {
            mark = StartupProfile.Mark();
            int prewarmed = builtSounds.Prewarm(animProgram.SoundNodeNames());
            // The one-shot SOUND streams too (destruction/damage audio) — first reached at runtime
            // from a death or damage sequence, always after this scope closes. Prewarm expands a
            // SOUND_GROUPS name to its members.
            prewarmed += builtSounds.Prewarm(animProgram.OneShotSoundNames());
            StartupProfile.Record("prewarm", mark);
            if (prewarmed > 0)
            {
                GD.Print($"anim: prewarmed {prewarmed} sound stream(s) before the archive closed");
            }
            if (!o.SoundsOutliveBuild)
            {
                builtSounds.Loader = null;
            }
        }
        root.AddChild(animRuntime);

        return s;
    }

    /// <summary>Stamps <see cref="SceneBuilder.ClutterColor"/> onto every clutter draw under
    /// <paramref name="clutter"/> as a full-strength <see cref="SceneBuilder.TintParam"/>, and
    /// returns how many it painted. Per instance rather than per material because both clutter
    /// paths are MultiMeshes sharing the placed world's materials — the sprite cards' own shader
    /// and, for the 3D decorations, literally the world's — so a material-level colour would
    /// repaint the ground with them.</summary>
    private static int TintClutterBlue(Node3D clutter)
    {
        int painted = 0;
        foreach (var child in clutter.GetChildren())
        {
            if (child is MultiMeshInstance3D mmi)
            {
                mmi.SetInstanceShaderParameter(SceneBuilder.TintParam, SceneBuilder.ClutterColor);
                painted++;
            }
            else if (child is Node3D nested)
            {
                painted += TintClutterBlue(nested);
            }
        }
        return painted;
    }

    /// <summary>Build settings that vary by mode; the loaded archives are passed to
    /// <see cref="Build"/> separately.</summary>
    public sealed class Options
    {
        public required string DataRoot { get; init; }
        public required string Chapter { get; init; }
        public required string Mission { get; init; }
        public required string ZrdrPath { get; init; }
        public required string InterpPath { get; init; }
        public required string MissionZrdrPath { get; init; }

        /// <summary>Parent for the effect siblings the bootstrap builds — the world's ambient
        /// SOUND_NODE emitters and every PUFFER_STATE emitter. In the viewer this is the session
        /// root (<c>_worldRoot</c>), a sibling of <see cref="Root"/>, not <see cref="Root"/>
        /// itself.</summary>
        public required Node3D EffectsParent { get; init; }

        /// <summary>Where PLAYER_RANGE conditions and the sound listener measure from. Resolved
        /// per call because no camera exists yet at build time; player 1's camera is the honest
        /// answer in every mode (chase cam, free camera, or the orbit eye).</summary>
        public required Func<Vector3> PlayerPosition { get; init; }

        /// <summary>Every player's position, for the EXECUTION_BY_RANGE proximity gate — the
        /// aircraft themselves in flight, not the chase cameras (a chase camera trails ~25 m
        /// behind, which is most of the spiderweb's 50 m radius). Null → the gate falls back
        /// to <see cref="PlayerPosition"/>.</summary>
        public Func<IReadOnlyList<Vector3>>? PlayerPositions { get; init; }

        /// <summary>Build world colliders (true in flight; false for a static or lab view).</summary>
        public bool Collision { get; init; }

        public bool DebugAnim { get; init; }
        public int AnimLod { get; init; } = AnimRuntime.HighLod;
        public bool DebugDzPaths { get; init; }

        /// <summary><c>--no-clutter</c>: skip the ground-clutter build (the tree/bush cards and the
        /// 3D city-block decorations) entirely, leaving the ground they stand on visible.</summary>
        public bool NoClutter { get; init; }

        /// <summary><c>--debug-clutterflag</c>: build the world recoloured by the decoded
        /// per-polygon <c>no_clutter</c> flag — flagged red, clear green — and force whatever
        /// clutter is built blue, so "which ground is flagged" and "where the decorations are" can
        /// never be confused. See <see cref="SceneBuilder.DebugClutterFlag"/>.</summary>
        public bool DebugClutterFlag { get; init; }

        /// <summary><c>--clutter-templates=</c>: build these clutter templates instead of the
        /// chapter's own registered list, so one district at a time can be A/B'd against the
        /// original. The per-polygon <c>no_clutter</c> gate still applies. Null → the chapter's
        /// own list. <see cref="NoClutter"/> wins over this.</summary>
        public IReadOnlyList<string>? ClutterTemplates { get; init; }

        /// <summary>The caller's <see cref="TextureArchive"/> outlives this build, so the runtime
        /// keeps its emitter factory and every <c>PUFFER_STATE</c> reached at RUNTIME — a
        /// destructible's death trails and sustained fire, the ON_CALL ambient dust and smoke —
        /// can still bake its atlas. True in every game session (the archive belongs to the
        /// session, freed on return-to-menu); false only where it is genuinely a <c>using</c> local
        /// of the build, i.e. the test harness.
        /// <para>⚠ Default false is the SAFE answer, not the common one. Left false by a caller
        /// that does own its archive, every runtime-reached puffer in the world silently builds
        /// nothing and the log stays clean — the miss is counted as
        /// <c>PufferState(after build)</c> into a census printed at the end of the bootstrap, which
        /// is before the first death can happen.</para></summary>
        public bool TexturesOutliveBuild { get; init; }

        /// <summary>The factory <see cref="AnimRuntime"/> builds <c>PUFFER_STATE</c> emitters
        /// through. Null (the default) means the real <see cref="Anim.PufferEmitterFactory"/> over
        /// this build's <see cref="TextureArchive"/> and <see cref="EffectsParent"/>, subject to
        /// <see cref="TexturesOutliveBuild"/> exactly as before; a caller supplies its own — the
        /// test harness's <c>CountingEmitterFactory</c> — to observe emitter lifetime with no GPU,
        /// and a caller-supplied factory is never auto-retired (it holds no archive reference for
        /// <see cref="TexturesOutliveBuild"/> to be about). A post-build swap would miss the
        /// bootstrap, where most <c>PUFFER_STATE</c>s fire, so this is read once, here, not assigned
        /// after <see cref="Build"/> returns.</summary>
        public Anim.IEmitterFactory? EmitterFactory { get; init; }

        /// <summary>The caller's <see cref="SoundArchive"/> outlives this build, so
        /// <c>WorldSounds.Loader</c> stays live for names the prewarm did not reach. Separate from
        /// <see cref="TexturesOutliveBuild"/> because the lifetimes are separate: a game session
        /// scopes the sound archive to the build (the prewarm is what makes that survivable) while
        /// its texture archive lasts the session. Only the lab keeps both.</summary>
        public bool SoundsOutliveBuild { get; init; }

        /// <summary>Whether the bootstrap runs the ambient-playback passes (ON_STARTUP defs +
        /// startanims). True — the default — in every game/viewer/flight session; the animation
        /// lab sets false for its quiet stage and runs them on demand through
        /// <see cref="AnimRuntime.StartAmbient"/>.</summary>
        public bool AutoStart { get; init; } = true;

        /// <summary>Whether a CALL_ANIMATION relocates its callee's effect-template root onto the
        /// call site (<see cref="Anim.TemplateStage{TNode}.Places"/>). False — the default — in
        /// every game/viewer/flight session, where the ambient world boot must stay byte-identical;
        /// the animation lab sets true so the templates it stages in front of the camera play at the
        /// call site instead of at their gamez origin. Read once, at construction: the flag is
        /// sealed onto the runtime's template stage, not writable
        /// afterwards, which makes a post-<c>Bind</c> write unexpressible.</summary>
        public bool PlacesCalledTemplates { get; init; }

        /// <summary>Pins the runtime's RNG for a reproducible run (see
        /// <see cref="AnimRuntime.Seed"/>). Null — the default — leaves it unseeded: the game.</summary>
        public int? RuntimeSeed { get; init; }

        /// <summary>The <c>--node=</c> stage: build ONLY this gamez subtree instead of the whole
        /// world. Null — the default — is the full chapter build. The caller resolves the name
        /// (<see cref="WorldBuilder.MatchNodes"/>) so a miss can report its candidates and quit
        /// before anything is built.</summary>
        public GameZNode? NodeSubtree { get; init; }
    }
}
