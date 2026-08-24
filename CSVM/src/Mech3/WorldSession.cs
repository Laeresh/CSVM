using System;
using System.Collections.Generic;
using CSVM.Utils;
using Godot;

namespace CSVM.Mech3;

/// <summary>Builds one chapter world and binds its animation program to it: the world+anim half of
/// <see cref="CSVM.Session.GameSession"/>'s session build, extracted so <c>--anim-lab</c>
/// builds the same world+runtime a normal session does without duplicating it.
/// Load → <see cref="WorldBuilder"/> → clutter → mission setup → <see cref="AnimProgram"/> →
/// <see cref="AnimRuntime"/> wiring → <c>Bind</c> → sound-prewarm. Stops before the per-view steps
/// (horizon, weather, edge-extender, unplaced-entity watch), which stay in the caller and drive off
/// <see cref="Builder"/>. Does NOT add <see cref="Root"/> to the scene tree; the caller owns
/// that. Decode: docs/architecture.md, this file's entry.</summary>
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
    /// are the caller's <c>using</c> locals — see the disposal-lifetime contract on the class.
    /// ⚠ Keep each phase's <see cref="StartupProfile.Record"/> call next to its step; moving one
    /// without the other makes a dropped phase read as a growing <c>rest</c>, not as missing.</summary>
    public static WorldSession Build(Options o, GameZ gamez, TextureArchive textures,
        SoundArchive? sounds, Dictionary<string, SoundDef>? soundDefs,
        IReadOnlyDictionary<string, SoundGroup>? soundGroups = null)
    {
        var s = new WorldSession();

        // Loaded before the build: scroll rates feed the material cache key
        // (MissionSetup.ScrollByModel); the entity half applies later, in the animation bootstrap.
        long mark = StartupProfile.Mark();
        MissionSetup? missionSetup = null;
        if (o.NodeSubtree != null)
        {
            // Skipped deliberately: the one verb MissionSetup reliably resolves switches the
            // subject off (C1/IA1 hides hk_zep); a node stage shows the subtree in its gamez
            // base state instead.
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
        // The area-selected toggles are resolved to gamez node indices here, while the gamez is in
        // hand: Apply runs inside the animation bootstrap, which sees only the built tree.
        missionSetup?.BindPartitions(gamez);
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

        // Clutter: forest trees, river bushes, and C2/C5's 3D city blocks (Clutter.cs). Sprites are
        // never solid (billboards have no side to hit); 3D decorations are, in flight — both user
        // decisions; see docs/org/clutter.md for the Spruce Goose misread this settled.
        mark = StartupProfile.Mark();
        ClutterBuilder? clutterBuilder = null;
        Node3D? clutterRoot = null;
        // --node= has no clutter terrain; --clutter-templates= A/Bs one district (no_clutter still
        // gates per polygon); --no-clutter wins over both as the stronger statement.
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

        // --debug-clutterflag: force clutter blue and print the census. Blue is the one colour
        // the world shader cannot express itself — a decoration's own polygons are unflagged,
        // so without it a flagged city block misreads as clutter-eligible ground.
        if (o.DebugClutterFlag)
        {
            int painted = clutterRoot != null ? TintClutterBlue(clutterRoot) : 0;
            GD.Print("debug: --debug-clutterflag view — world polygons "
                     + $"no_clutter={builder.Scene.FlaggedPolygonCount} (red), "
                     + $"clear={builder.Scene.ClearPolygonCount} (green), over the models built for "
                     + $"{o.Chapter}; clutter blue ({painted} multimesh"
                     + (painted == 1 ? ")" : "es)"));
        }

        // Base states first, then ON_STARTUP + startanims, which now play rather than being posed
        // at end state (hangar doors swing, the C1 train drives its loop). The program merges the
        // compiled cam_anim/mis_anim archives with the three zrdr scopes.
        mark = StartupProfile.Mark();
        var chapterZrdrPath = SessionPaths.ChapterZrdr(o.DataRoot, o.Chapter);
        var (chapterAnimPath, missionAnimPath) =
            AnimProgram.ArchivePaths(o.DataRoot, o.Chapter, o.Mission);
        var animProgram = AnimProgram.Load(o.ZrdrPath, chapterZrdrPath, o.MissionZrdrPath,
            chapterAnimPath, missionAnimPath);
        StartupProfile.Record("anim", mark);
        s.Program = animProgram;
        // Puffer factory retirement: see Options.TexturesOutliveBuild.
        var lights = new WorldLights();
        s.Lights = lights;
        // Sealed template stage: the ambient world pools/stages nothing hidden — its templates ARE
        // the world's own nodes. See Options.PlacesCalledTemplates for the one exception.
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
            // PLAYER_RANGE conditions measure from the nearest player (PlayerPositions);
            // PlayerPosition is only the fallback for a runtime with no PlayerPositions wired.
            // Both resolved per call because no camera exists yet here.
            PlayerPosition = o.PlayerPosition,
            PlayerPositions = o.PlayerPositions,
            LightViewerPositions = o.LightViewerPositions,
            FirstPersonView = o.FirstPersonView,
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
            worldSounds.SetListeners(o.ListenerPositions
                                     ?? (() => new[] { o.PlayerPosition() }));
        }
        // A CALL_ANIMATION callee on a library-root gamez node (docs/formats/gamez.md) builds
        // LAZILY on first call, pooled per anchor and sized by EffectPools (docs/architecture.md).
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
        // Only the REAL factory this method built is tied to `textures`'s scope; a caller-supplied
        // one holds no archive reference, so it needs no retirement.
        if (o.EmitterFactory == null && !o.TexturesOutliveBuild)
        {
            animRuntime.Emitters.RetireFactory();
        }
        // Same rule as the puffer factory: the zip handle dies with the caller's scope. Most
        // SOUND_NODE events are first reached at RUNTIME (an OnCall def, a delayed CallSequence),
        // so decode everything the program can ask for now, before the archive closes.
        if (animRuntime.Sounds is { } builtSounds)
        {
            mark = StartupProfile.Mark();
            int prewarmed = builtSounds.Prewarm(animProgram.SoundNodeNames());
            // The one-shot SOUND streams too (destruction/damage audio) — first reached at runtime
            // from a death or damage sequence, always after this scope closes. Prewarm expands a
            // SOUND_GROUPS name to its members.
            prewarmed += builtSounds.Prewarm(animProgram.OneShotSoundNames());
            // The mission's combat-voice clips (CombatVoice.SessionPrewarmNames): every line an AI
            // pilot could speak is first reached at runtime, so it must decode now or stay silent.
            if (o.VoiceClipNames is { Count: > 0 } voiceNames)
            {
                int voiced = builtSounds.Prewarm(voiceNames);
                prewarmed += voiced;
                GD.Print($"anim: prewarmed {voiced} combat-voice stream(s) "
                         + $"of {voiceNames.Count} roster clip def(s)");
            }
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

    // Stamps SceneBuilder.ClutterColor onto every clutter draw under
    // `clutter` as a full-strength SceneBuilder.TintParam, and
    // returns how many it painted. Per instance rather than per material because both clutter
    // paths are MultiMeshes sharing the placed world's materials — the sprite cards' own shader
    // and, for the 3D decorations, literally the world's — so a material-level colour would
    // repaint the ground with them.
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

        /// <summary>The PLAYER_RANGE fallback for a runtime with no <see cref="PlayerPositions"/>
        /// wired (a real session always wires both). Resolved per call because no
        /// camera exists yet at build time; player 1's camera is the honest single-camera answer
        /// in every mode (chase cam, free camera, or the orbit eye).</summary>
        public required Func<Vector3> PlayerPosition { get; init; }

        /// <summary>Every 3D audio listener's position — one per pane, since every pane camera is
        /// listener-enabled (UI.SplitScreen). Read only by the debug sound log's distance column;
        /// the engine reads the listeners themselves. Null → <see cref="PlayerPosition"/> alone.</summary>
        public Func<IReadOnlyList<Vector3>>? ListenerPositions { get; init; }

        /// <summary>Every player's position, for the EXECUTION_BY_RANGE proximity gate and every
        /// PLAYER_RANGE condition — the aircraft themselves in flight, not the
        /// chase cameras (a chase camera trails ~25 m behind, which is most of the spiderweb's
        /// 50 m radius). Null → both fall back to <see cref="PlayerPosition"/>.</summary>
        public Func<IReadOnlyList<Vector3>>? PlayerPositions { get; init; }

        /// <summary>Every pane's camera, for budgeting the world's <c>LIGHT_STATE</c> spill
        /// against the nearest one — the draw-rule seam (`ViewerSet.Positions`),
        /// not <see cref="PlayerPositions"/>. Null → the runtime falls back to
        /// <see cref="PlayerPosition"/> alone.</summary>
        public Func<IReadOnlyList<Vector3>>? LightViewerPositions { get; init; }

        /// <summary>Whether any human pilot is in one of the two first-person views, for the
        /// <c>PLAYER_1ST_PERSON</c> condition. Null → false, the pre-cockpit answer.</summary>
        public Func<bool>? FirstPersonView { get; init; }

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

        /// <summary>The caller's <see cref="TextureArchive"/> outlives this build, so a
        /// runtime-reached <c>PUFFER_STATE</c> (death trails, ON_CALL dust/smoke) can still bake
        /// its atlas. True in a game session (archive freed on return-to-menu); false only where the
        /// archive is genuinely a <c>using</c> local of the build (the test harness).
        /// ⚠ Default false is the SAFE choice, not the common one — left false by a caller that
        /// owns its archive, a runtime-reached puffer silently builds nothing.</summary>
        public bool TexturesOutliveBuild { get; init; }

        /// <summary>The factory <see cref="AnimRuntime"/> builds <c>PUFFER_STATE</c> emitters
        /// through. Null (default) is the real <see cref="Anim.PufferEmitterFactory"/> over this
        /// build's archive, subject to <see cref="TexturesOutliveBuild"/>; a caller-supplied one
        /// (the test harness's <c>CountingEmitterFactory</c>) holds no archive reference and is
        /// never auto-retired. ⚠ Read once, here — a post-<see cref="Build"/> swap would miss the
        /// bootstrap, where most <c>PUFFER_STATE</c>s fire.</summary>
        public Anim.IEmitterFactory? EmitterFactory { get; init; }

        /// <summary>The mission's combat-voice clip defs (<see cref="CombatVoice.SessionPrewarmNames"/>),
        /// prewarmed with the animation program's own sound names so a pilot's line still decodes
        /// after the archive closes. Null or empty (the default) prewarms no voice: a viewer or
        /// lab session hosts no talking pilots. The caller computes the set because which pilots
        /// talk is session policy, not world-build mechanics.</summary>
        public IReadOnlyCollection<string>? VoiceClipNames { get; init; }

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
        /// call site (<see cref="Anim.TemplateStage{TNode}.Places"/>). False (default) in every
        /// game/viewer/flight session, where the ambient boot must stay byte-identical; the
        /// animation lab sets true so staged templates play at the call site. ⚠ Read once, at
        /// construction — sealed onto the runtime's template stage, not writable
        /// afterwards.</summary>
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
