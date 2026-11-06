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
    public void A_saved_speed_keeps_the_missions_heading_and_zero_keeps_its_speed()
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
