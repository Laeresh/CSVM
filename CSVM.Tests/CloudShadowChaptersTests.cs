using CSVM.Effects;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The per-chapter cloud shadow file. An entry overrides only the fields it holds, and a missing
/// chapter takes the constants. A bad field is reported, and the lab's Save keeps every other key.
/// </summary>
public class CloudShadowChaptersTests
{
    [Fact]
    public void An_entry_overrides_its_fields_and_other_chapters_take_the_constants()
    {
        var file = CloudShadowChapters.Parse("""{ "_about": "prose", "C2": { "darkness": 0.9, "size": 500 } }""");

        Assert.Empty(file.Warnings);
        var c2 = file.For("c2");
        Assert.Equal(0.9f, c2.Darkness);
        Assert.Equal(500f, c2.PatternSize);
        Assert.Equal(CloudShadowPattern.Coverage, c2.Coverage);
        Assert.Equal(CloudShadowPattern.Tune.Default, file.For("C1"));
    }

    [Fact]
    public void A_bad_field_is_reported_and_an_out_of_range_value_held()
    {
        var file = CloudShadowChapters.Parse("""{ "C1": { "darkness": "dark", "fog": 1, "coverage": 4 } }""");

        Assert.Equal(3, file.Warnings.Count);
        Assert.Equal(CloudShadowPattern.Darkness, file.For("C1").Darkness);
        Assert.Equal(1f, file.For("C1").Coverage);
    }

    [Fact]
    public void Save_replaces_one_chapter_in_place_and_reads_back()
    {
        const string existing = """{ "_about": "prose", "C1": { "darkness": 0.5 }, "C3": { "size": 700 } }""";
        var tune = CloudShadowPattern.Tune.Default with { Darkness = 0.95f, Ambient = 0.9f };

        string text = CloudShadowChapters.WithEntry(existing, "c1", tune);
        var file = CloudShadowChapters.Parse(text);

        Assert.Empty(file.Warnings);
        Assert.Equal(tune, file.For("C1"));
        Assert.Equal(700f, file.For("C3").PatternSize);
        Assert.True(text.IndexOf("\"_about\"", System.StringComparison.Ordinal) < text.IndexOf("\"C1\"", System.StringComparison.Ordinal));
        Assert.True(text.IndexOf("\"C1\"", System.StringComparison.Ordinal) < text.IndexOf("\"C3\"", System.StringComparison.Ordinal));
    }

    [Fact]
    public void A_saved_speed_keeps_the_missions_heading_and_an_unset_one_its_speed()
    {
        var tune = CloudShadowChapters.Parse("""{ "C2": { "speed": 25 } }""").For("C2");
        var mission = new Godot.Vector2(3.2f, 2.4f);

        var drift = tune.Drift(mission);
        Assert.Equal(25f, drift.Length(), 3);
        Assert.Equal(0.8f, drift.X / drift.Length(), 3);
        Assert.Equal(mission.X, CloudShadowPattern.Tune.Default.Drift(mission).X, 4);
        Assert.Equal(mission.Y, CloudShadowPattern.Tune.Default.Drift(mission).Y, 4);
    }

    [Fact]
    public void A_saved_speed_of_zero_holds_the_pattern_still_after_a_save()
    {
        var still = CloudShadowPattern.Tune.Default with { DriftSpeed = 0f, Heading = 90f };

        var read = CloudShadowChapters.Parse(CloudShadowChapters.WithEntry(null, "C1", still)).For("C1");
        Assert.Equal(still, read);
        Assert.Equal(Godot.Vector2.Zero, read.Drift(new Godot.Vector2(3.2f, 2.4f)));
    }

    [Fact]
    public void Every_field_is_written_read_and_described_through_the_one_table()
    {
        var tune = new CloudShadowPattern.Tune(0.7f, 330f, 0.26f, 0.07f, 0.87f, 9.5f, 291f, 0.45f, 0.8f, 0.25f);

        Assert.Equal(tune, CloudShadowChapters.Parse(CloudShadowChapters.WithEntry(null, "C2", tune)).For("C2"));
        Assert.Equal("darkness=0.70 ambient=0.87 size=330 coverage=0.26 softness=0.070 speed=9.5 heading=291 head_taper=0.45 deck_sun=0.80 deck_ambient=0.25", tune.Describe());
        Assert.Contains("speed=mission heading=mission", CloudShadowPattern.Tune.Default.Describe());
        // The file holds the size to the lab's slider.
        Assert.Equal(3000f, (CloudShadowPattern.Tune.Default with { PatternSize = 5000f }).Clamped().PatternSize);
    }

    [Fact]
    public void The_volumetric_fields_default_to_the_prototype_look_and_read_back()
    {
        var file = CloudShadowChapters.Parse("""{ "C1": { "head_taper": 0.5 } }""");

        Assert.Empty(file.Warnings);
        Assert.Equal((0.5f, 0.97f, 0.6f), (file.For("C1").HeadTaper, file.For("C1").DeckSunTaken, file.For("C1").DeckAmbientTaken));
        Assert.Equal((0.3f, 0.97f, 0.6f), (file.For("C2").HeadTaper, file.For("C2").DeckSunTaken, file.For("C2").DeckAmbientTaken));
        var saved = CloudShadowChapters.Parse(CloudShadowChapters.WithEntry(null, "C1", file.For("C1") with { DeckSunTaken = 0.5f, DeckAmbientTaken = 0.1f }));
        Assert.Equal((0.5f, 0.5f, 0.1f), (saved.For("C1").HeadTaper, saved.For("C1").DeckSunTaken, saved.For("C1").DeckAmbientTaken));
        Assert.Equal(0.8f, (CloudShadowPattern.Tune.Default with { HeadTaper = 2f }).Clamped().HeadTaper);
    }

    [Fact]
    public void A_saved_heading_turns_the_drift_and_survives_a_save()
    {
        var tune = CloudShadowChapters.Parse("""{ "C1": { "heading": 270 } }""").For("C1");

        var drift = tune.Drift(new Godot.Vector2(3.2f, 2.4f));
        Assert.Equal(4f, drift.Length(), 3);
        Assert.Equal(-4f, drift.Y, 3);
        string text = CloudShadowChapters.WithEntry(null, "C1", tune with { Heading = 400f });
        Assert.Equal(40f, CloudShadowChapters.Parse(text).For("C1").Heading);
        Assert.DoesNotContain("heading", CloudShadowChapters.WithEntry(null, "C2", CloudShadowPattern.Tune.Default));
    }

    [Fact]
    public void Save_refuses_a_file_it_cannot_read()
    {
        Assert.Throws<System.IO.InvalidDataException>(() =>
            CloudShadowChapters.WithEntry("[1, 2]", "C1", CloudShadowPattern.Tune.Default));
    }
}
