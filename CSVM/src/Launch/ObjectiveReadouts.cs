using System.Collections.Generic;
using System.IO;
using System.Linq;
using CSVM.Extraction;
using CSVM.Flight.Camera;
using CSVM.Flight.Hud;
using CSVM.Flight.Modes;
using CSVM.Flight.Weapons;
using CSVM.Mech3;
using CSVM.Session.Campaign;
using CSVM.Session.Objectives;
using CSVM.Session.Roster;
using CSVM.Session.World;
using CSVM.Spec;
using CSVM.UI.Overlays;
using CSVM.UI.Screens;
using CSVM.Utils;

namespace CSVM.Launch;

/// <summary>A flown mission's objective readouts, one step of the session build. A campaign gets
/// each pane's objectives readout and mission-end fade, the clock's expiry notices and the
/// objective sites on the player's target cycles. A mode without a director takes the same site
/// feed off the mission's own targets.zrd (<see cref="BindsMissionTargetTable"/>). It runs once
/// the objectives graph is armed and builds nothing it ticks itself.
/// Module entry: docs/architecture/Launch.md on src/Launch/ObjectiveReadouts.cs.</summary>
internal static class ObjectiveReadouts
{
    /// <summary>Whether a session binds the mission's own targets.zrd, read with nothing editing
    /// it while the session runs. The director owns that channel when one exists, so this asks
    /// for the director and not for the launch. A <c>--campaign=</c> launch whose profile did not
    /// load flies without one and still gets the table its mission ships. ⚠ Keep the stunt term.
    /// A stunt run binds the same channel per pane. Public so the mode-target-table suite pins
    /// the rule the build runs.</summary>
    public static bool BindsMissionTargetTable(
        SessionSpec spec, bool hasDirector, bool stunting, bool hasWorld, int rigs) =>
        !hasDirector && spec.WorldMode && !spec.EmptyStage && !stunting && hasWorld && rigs > 0;

    /// <summary>Builds the readouts and binds the site feed onto the roster's target cycles.
    /// </summary>
    public static void Build(BuildState state, Inputs inputs)
    {
        var spec = inputs.Spec;
        var rigs = inputs.Rigs;
        var chapterZrdr = SessionPaths.ChapterZrdr(state.DataRoot, spec.Chapter);
        if (inputs.Campaign is { } campaign && inputs.CampaignStrings is { } objectiveStrings)
        {
            // One readout and one fade per rig, under that rig's own HudParent, so every pane
            // draws its own copy as every other per-rig HUD does. The completion
            // mark's art is loaded once and shared across them, as the HUD font is.
            var objectiveMark = ObjectivesHud.LoadMark(
                Path.Combine(state.DataRoot, "extracted", "rimage"));
            foreach (var rig in rigs)
            {
                // ⚠ Not beside the Original pause sheet: that screen's own parchment already
                // carries the objectives, and two readouts over one pause is one too many.
                if (!inputs.SheetCarriesObjectives)
                {
                    rig.HudParent.AddChild(ObjectivesHud.Build(
                        campaign, objectiveStrings, inputs.PauseState, objectiveMark));
                }

                rig.HudParent.AddChild(MissionEndFade.Build(campaign));
            }

            // The mission clock running out posts its two notices into the same stack a kill line
            // lands in, every pane's. Every seat is flying the mission that just expired.
            if (campaign.Graph is { } timerGraph)
            {
                timerGraph.TimerExpired += () =>
                {
                    foreach (var rig in rigs)
                    {
                        if (rig.Controller?.MessageStack is { } stack)
                        {
                            HudMessages.PostTimeExpired(stack, objectiveStrings);
                        }
                    }
                };
            }
            var sites = new ObjectiveSites(campaign, objectiveStrings,
                MissionTargets.Load(state.MissionZrdrPath, chapterZrdr), state.WorldRuntime);
            inputs.Roster.SetTargetObjectives(into => sites.Collect(into));
            // Verification breadcrumb: how many sites the mission starts with, split by the flag
            // that picks their cycle. A zero here and a populated objectives readout means the
            // target table, not the graph, is the problem.
            var offered = new List<AimCandidate>();
            sites.Collect(offered);
            int flagged = offered.Count(c => c.Source is ObjectiveSite { Objective: true });
            Log.Info("core", $"campaign: {flagged} objective site(s) and {offered.Count - flagged} other-target site(s) on the player's target cycles");
        }
        else if (BindsMissionTargetTable(spec, inputs.Campaign != null, inputs.Stunting,
                                         state.WorldRuntime != null, rigs.Count))
        {
            // Instant Action, the multiplayer modes, and a campaign launch flying without a
            // director take the same site feed with no graph behind it. The table's own objective
            // and other_target keys are the whole answer (see BindsMissionTargetTable).
            var sites = new ObjectiveSites(Messages.Load(state.MessagesPath),
                MissionTargets.Load(state.MissionZrdrPath, chapterZrdr), state.WorldRuntime);
            // A team mode labels its flags and hulls by side over the table's own lines.
            var dogfight = inputs.Dogfight;
            sites.Sides = key => dogfight?.SideOf(key);
            inputs.Roster.SetTargetObjectives(into => sites.Collect(into));
            var modeSites = new List<AimCandidate>();
            sites.Collect(modeSites);
            int modeFlagged = modeSites.Count(c => c.Source is ObjectiveSite { Objective: true });
            Log.Info("core", $"{spec.Chapter}/{spec.Mission}: {modeFlagged} objective site(s) and {modeSites.Count - modeFlagged} other-target site(s) from the mission's own targets.zrd");
        }
        else if (spec.MissionType == DogfightMissionType.CaptureTheFlag && inputs.StageArena is { } arena)
        {
            // The empty stage's arena ships no targets.zrd and has no world index. It flags its own
            // flag markers and finds them among its own nodes. The flag runtime labels them.
            var sites = new ObjectiveSites(Messages.Load(state.MessagesPath),
                MissionTargets.Objectives(EmptyStage.ArenaTargets), key => EmptyStage.ArenaNode(arena, key));
            var dogfight = inputs.Dogfight;
            sites.Sides = key => dogfight?.SideOf(key);
            inputs.Roster.SetTargetObjectives(into => sites.Collect(into));
            Log.Info("core", $"stage empty: {EmptyStage.ArenaTargets.Count} flag marker site(s) from the match arena");
        }
    }

    /// <summary>What the readouts are built over, each an output of an earlier build step.
    /// </summary>
    internal sealed class Inputs
    {
        public SessionSpec Spec = null!;
        public IReadOnlyList<PlayerRig> Rigs = null!;
        public FlightRoster Roster = null!;
        public CampaignDirector? Campaign;
        // The string table the campaign's readouts are worded from, loaded before the graph was
        // armed; null outside a campaign.
        public Messages? CampaignStrings;
        public PauseState PauseState = null!;
        // Whether the Original pause sheet carries the objectives itself.
        public bool SheetCarriesObjectives;
        // A stunt run binds the targets channel per pane instead.
        public bool Stunting;
        public VersusDirector? Dogfight;
        // The empty stage's match arena, null on a chapter.
        public Godot.Node3D? StageArena;
    }
}
