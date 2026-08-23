using CSVM.Flight;
using Xunit;

namespace CSVM.Tests;

/// <summary>The selected-view rules (A1): which mode the cycle key lands on, which modes are
/// first person, and what a held numpad key does to the selection. Headless because
/// <see cref="PilotView"/> owns no camera — <c>CameraController</c> holds the state and defers
/// every decision here.</summary>
public sealed class PilotViewTests
{
    [Fact]
    public void Only_the_two_first_person_modes_answer_the_condition()
    {
        Assert.False(PilotView.IsFirstPerson(PilotViewMode.Chase));
        Assert.True(PilotView.IsFirstPerson(PilotViewMode.Cockpit));
        Assert.True(PilotView.IsFirstPerson(PilotViewMode.Nose));
    }

    // The original's cycle key cycles the 6/7 PAIR; a request it does not recognise falls back to
    // cockpit (FUN_004414a0), which is what entering from the chase view reads as.
    [Fact]
    public void The_cycle_key_swaps_cockpit_and_nose_and_enters_from_chase()
    {
        Assert.Equal(PilotViewMode.Cockpit, PilotView.Cycle(PilotViewMode.Chase));
        Assert.Equal(PilotViewMode.Nose, PilotView.Cycle(PilotViewMode.Cockpit));
        Assert.Equal(PilotViewMode.Cockpit, PilotView.Cycle(PilotViewMode.Nose));
    }

    // Cycling never reaches the chase view again: leaving the pair is the other key's job, so a
    // pilot who cycles forever stays in first person.
    [Fact]
    public void Cycling_never_lands_back_on_chase()
    {
        var mode = PilotViewMode.Chase;
        for (int i = 0; i < 6; i++)
        {
            mode = PilotView.Cycle(mode);
            Assert.NotEqual(PilotViewMode.Chase, mode);
        }
    }

    // The held-key override: a numpad view wins for as long as it is down and the selection is
    // untouched, so releasing it returns to the very same mode.
    [Fact]
    public void A_held_view_key_overrides_the_selection_without_changing_it()
    {
        Assert.Equal(PilotViewMode.Chase, PilotView.Effective(PilotViewMode.Chase, heldViewActive: true));
        foreach (var selected in new[] { PilotViewMode.Chase, PilotViewMode.Cockpit, PilotViewMode.Nose })
        {
            Assert.Equal(selected, PilotView.Effective(selected, heldViewActive: false));
        }
    }

    // The numpad is the head-look snap cluster in first person, which is what the original binds
    // it to, so a held view key is not an override there and the mode stands.
    [Fact]
    public void A_held_view_key_is_not_an_override_in_a_first_person_mode()
    {
        Assert.False(PilotView.HoldsFixedViews(PilotViewMode.Cockpit));
        Assert.False(PilotView.HoldsFixedViews(PilotViewMode.Nose));
        Assert.True(PilotView.HoldsFixedViews(PilotViewMode.Chase));
        Assert.Equal(PilotViewMode.Cockpit, PilotView.Effective(PilotViewMode.Cockpit, heldViewActive: true));
        Assert.Equal(PilotViewMode.Nose, PilotView.Effective(PilotViewMode.Nose, heldViewActive: true));
    }

    // The look-behind is its own override and keeps working in every mode: it is numpad 0, outside
    // the snap cluster, and it frames the aircraft from ahead rather than turning the pilot's head.
    [Fact]
    public void The_look_behind_still_overrides_every_mode_including_first_person()
    {
        foreach (var selected in new[] { PilotViewMode.Chase, PilotViewMode.Cockpit, PilotViewMode.Nose })
        {
            Assert.Equal(PilotViewMode.Chase,
                PilotView.Effective(selected, heldViewActive: false, backActive: true));
        }
    }

    [Fact]
    public void The_view_flag_spells_the_three_modes_and_nothing_else()
    {
        Assert.Equal(PilotViewMode.Chase, PilotView.Parse("chase"));
        Assert.Equal(PilotViewMode.Cockpit, PilotView.Parse("Cockpit"));
        Assert.Equal(PilotViewMode.Nose, PilotView.Parse("NOSE"));
        Assert.Null(PilotView.Parse("back"));
        Assert.Null(PilotView.Parse("6"));
        Assert.Null(PilotView.Parse("cockpit1"));
    }

    // The log breadcrumb a scripted capture reads back is the same word the flag took.
    [Fact]
    public void Every_mode_names_itself_the_way_the_flag_spells_it()
    {
        foreach (var mode in new[] { PilotViewMode.Chase, PilotViewMode.Cockpit, PilotViewMode.Nose })
            Assert.Equal(mode, PilotView.Parse(PilotView.Name(mode)));
    }
}
