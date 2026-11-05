using System.Linq;
using CSVM.Flight.Hud;
using CSVM.UI.Menu;
using CSVM.Utils;
using Godot;
using Xunit;

namespace CSVM.Tests;

/// <summary>The HUD layout, engine-free: the setting's words, the stored layout's spelling, and every
/// element's rectangle on each pane shape the HUD meets. Original is the original's own formulas and
/// shifts nothing. Sides keeps every element inside its pane, apart, clear of the centre and the
/// bottom. A custom placement keeps its edge margin across shapes. The live controls and the editor
/// are the engine suites' (<c>hud-layout-placement</c>, <c>hud-layout-editor</c>).</summary>
public class HudLayoutTests
{
    // Every pane shape the HUD meets, as pane and window. A 1440p window holds a single 16:9 view, a 2P
    // stack and a 4P grid. A 21:9 view and a 2P side-by-side split on a 32:9 window are the wide ones.
    public static readonly TheoryData<string, float, float, float, float> Shapes = new()
    {
        { "single 16:9", 2560f, 1440f, 2560f, 1440f },
        { "ultrawide 21:9", 3440f, 1440f, 3440f, 1440f },
        { "2P stacked", 2560f, 719f, 2560f, 1440f },
        { "2P side by side", 2559f, 1440f, 5120f, 1440f },
        { "4P grid", 1279f, 719f, 2560f, 1440f },
        { "super-ultrawide 32:9", 5120f, 1440f, 5120f, 1440f },
        { "4P grid on 32:9", 2559f, 719f, 5120f, 1440f },
    };

    [Fact]
    public void TheWordsSpellTheThreeLayoutsWithOriginalTheDefault()
    {
        Assert.Equal(new[] { "original", "sides", "custom" }, HudLayoutSetting.Words);
        Assert.Equal(new[] { "Original", "Sides", "Custom" }, HudLayoutSetting.Labels());
        Assert.Equal(HudLayoutSetting.Words[0], HudLayoutSetting.Default);
        Assert.Equal(HudLayoutKind.Original, HudLayoutSetting.KindOf(null));
        Assert.Equal(HudLayoutKind.Original, HudLayoutSetting.KindOf("left"));
        foreach (var kind in System.Enum.GetValues<HudLayoutKind>())
        {
            Assert.Equal(kind, HudLayoutSetting.KindOf(HudLayoutSetting.WordOf(kind)));
        }

        Assert.Equal("HUD Layout: Custom", HudLayoutSetting.Line(HudLayoutKind.Custom));
        Assert.Equal("original", HudLayoutSetting.Resolve(null).Word);
        Assert.Equal("sides", HudLayoutSetting.Resolve("sides").Word);
    }

    [Fact]
    public void ALayoutRoundTripsThroughItsSpellingInAnyClauseOrder()
    {
        foreach (var layout in new[] { HudLayoutPlacement.Sides, HudLayoutPlacement.OriginalLayout, Custom() })
        {
            Assert.True(HudLayout.TryParse(layout.Format(), out var back));
            Assert.True(back.PaneRelative);
            Assert.Equal(layout.InPane(), back);
        }

        string reversed = string.Join(";", Custom().Format().Split(';').Reverse());
        Assert.True(HudLayout.TryParse(reversed, out var shuffled));
        Assert.Equal(Custom(), shuffled);
        Assert.Equal(
            "leftDials:start,centre,24,0;rightDials:end,centre,-24,0;compassTape:end,start,-24,35;throttlePlate:end,centre,-219,0",
            HudLayoutPlacement.Sides.Format());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("leftDials:start,centre,24,0")]
    [InlineData("leftDials:start,centre,24,0;rightDials:end,centre,-24,0;compassTape:end,start,-24,35;leftDials:start,start,0,0")]
    [InlineData("leftDials:middle,centre,24,0;rightDials:end,centre,-24,0;compassTape:end,start,-24,35;throttlePlate:end,centre,-219,0")]
    [InlineData("leftDials:start,centre,NaN,0;rightDials:end,centre,-24,0;compassTape:end,start,-24,35;throttlePlate:end,centre,-219,0")]
    [InlineData("leftDials:start,centre,9000,0;rightDials:end,centre,-24,0;compassTape:end,start,-24,35;throttlePlate:end,centre,-219,0")]
    [InlineData("leftDials:start,centre,24;rightDials:end,centre,-24,0;compassTape:end,start,-24,35;throttlePlate:end,centre,-219,0")]
    [InlineData("radar:start,centre,24,0;rightDials:end,centre,-24,0;compassTape:end,start,-24,35;throttlePlate:end,centre,-219,0")]
    public void AnythingButTheSpellingReadsAsNoLayout(string? text)
    {
        Assert.False(HudLayout.TryParse(text, out _));
        Assert.False(HudLayout.IsValid(text));
    }

    [Fact]
    public void EachKindResolvesToItsPlacement()
    {
        Assert.Null(HudLayoutPlacement.Resolve(HudLayoutKind.Original, Custom()));
        Assert.Same(HudLayoutPlacement.Sides, HudLayoutPlacement.Resolve(HudLayoutKind.Sides, Custom()));
        Assert.Same(HudLayoutPlacement.OriginalLayout, HudLayoutPlacement.Resolve(HudLayoutKind.Custom, null));
        Assert.Equal(Custom(), HudLayoutPlacement.Resolve(HudLayoutKind.Custom, Custom()));
    }

    // Original draws by the controls' own formulas. The rectangle is theirs and the shift is exactly
    // zero, which keeps every pixel of a default HUD where it was.
    [Theory]
    [MemberData(nameof(Shapes))]
    public void OriginalShiftsNothingOnAnyPane(string shape, float paneW, float paneH, float windowW, float windowH)
    {
        var (box, pane, scale) = Frame(paneW, paneH, windowW, windowH);
        foreach (var element in HudLayout.Elements)
        {
            Assert.Equal(Vector2.Zero, HudLayoutPlacement.Shift(element, null, box, pane, scale));
            Assert.Equal(HudLayoutPlacement.OriginalRect(element, box, pane, scale),
                HudLayoutPlacement.Place(element, null, box, pane, scale));
        }

        var (left, right) = GaugeCluster.ColumnAnchors(box, scale);
        var leftDials = HudLayoutPlacement.OriginalRect(HudElement.LeftDials, box, pane, scale);
        var rightDials = HudLayoutPlacement.OriginalRect(HudElement.RightDials, box, pane, scale);
        Assert.Equal(left + (GaugeCluster.LeftColumnMargin * scale), leftDials.Position.X, 3);
        Assert.Equal(right - (GaugeCluster.RightColumnMargin * scale), rightDials.End.X, 3);
        Assert.Equal(box.End.Y - ((HudMetrics.ReferenceHeight - 1384f) * scale), leftDials.End.Y, 3);
        _ = shape;
    }

    // The editor's first draft matches the original wherever the original keeps its 16:9 margins.
    [Theory]
    [InlineData(2560f, 1440f, 2560f, 1440f)]
    [InlineData(3440f, 1440f, 3440f, 1440f)]
    [InlineData(2560f, 719f, 2560f, 1440f)]
    [InlineData(1279f, 719f, 2560f, 1440f)]
    public void TheOriginalSpeltAsALayoutPlacesAsTheOriginalDoes(float paneW, float paneH, float windowW, float windowH)
    {
        var (box, pane, scale) = Frame(paneW, paneH, windowW, windowH);
        foreach (var element in HudLayout.Elements)
        {
            var original = HudLayoutPlacement.OriginalRect(element, box, pane, scale);
            var spelt = HudLayoutPlacement.Place(element, HudLayoutPlacement.OriginalLayout, box, pane, scale);
            Assert.Equal(original.Position.X, spelt.Position.X, 2);
            Assert.Equal(original.Position.Y, spelt.Position.Y, 2);
            Assert.Equal(original.Size, spelt.Size);
        }
    }

    // Sides on every pane: each element whole inside the pane and apart from the rest. The columns
    // stand at the reading box's sides, centred on its height, and the bottom centre stays clear.
    [Theory]
    [MemberData(nameof(Shapes))]
    public void SidesKeepsEveryElementInsideItsPaneApartAndOffTheCentre(string shape, float paneW, float paneH, float windowW, float windowH)
    {
        var (box, pane, scale) = Frame(paneW, paneH, windowW, windowH);
        var rects = HudLayout.Elements.ToDictionary(e => e, e => HudLayoutPlacement.Place(e, HudLayoutPlacement.Sides, box, pane, scale));
        var whole = new Rect2(Vector2.Zero, pane);
        foreach (var (element, rect) in rects)
        {
            Assert.True(whole.Encloses(rect), $"{shape}: {element} {rect} leaves the pane {pane}");
            foreach (var (other, otherRect) in rects)
            {
                Assert.True(other == element || !rect.Intersects(otherRect), $"{shape}: {element} overlaps {other}");
            }
        }

        float margin = HudLayoutPlacement.SidesMargin * scale;
        Assert.Equal(box.Position.X + margin, rects[HudElement.LeftDials].Position.X, 2);
        Assert.Equal(box.End.X - margin, rects[HudElement.RightDials].End.X, 2);
        Assert.Equal(box.GetCenter().Y, rects[HudElement.LeftDials].GetCenter().Y, 2);
        Assert.Equal(box.GetCenter().Y, rects[HudElement.RightDials].GetCenter().Y, 2);
        Assert.True(rects[HudElement.CompassTape].End.Y <= rects[HudElement.RightDials].Position.Y);
        var bottomCentre = new Rect2(box.Position.X + (box.Size.X / 3f), box.GetCenter().Y, box.Size.X / 3f, box.Size.Y / 2f);
        Assert.DoesNotContain(rects.Values, r => r.Intersects(bottomCentre));
        Assert.True(HudLayoutPlacement.Shift(HudElement.LeftDials, HudLayoutPlacement.Sides, box, pane, scale) != Vector2.Zero);
    }

    // A custom placement measures from the pane's own edges. One saved against the right edge stands
    // at a wide pane's real edge, past the reading box Sides keeps to. One saved off a big
    // screen stands whole inside a small split pane.
    [Theory]
    [InlineData(2560f, 1440f, 2560f, 1440f)]
    [InlineData(3440f, 1440f, 3440f, 1440f)]
    [InlineData(5120f, 1440f, 5120f, 1440f)]
    [InlineData(2559f, 1440f, 5120f, 1440f)]
    public void ACustomPlacementHoldsThePanesEdgeAndStaysOnAPane(float paneW, float paneH, float windowW, float windowH)
    {
        var layout = Custom().With(HudElement.RightDials, new HudPlacement(HudAnchor.End, HudAnchor.Start, -40f, 200f));
        var (box, pane, scale) = Frame(paneW, paneH, windowW, windowH);
        var rect = HudLayoutPlacement.Place(HudElement.RightDials, layout, box, pane, scale);
        Assert.Equal(pane.X - (40f * scale), rect.End.X, 2);
        Assert.Equal(200f * scale, rect.Position.Y, 2);
        var sides = HudLayoutPlacement.Place(HudElement.RightDials, HudLayoutPlacement.Sides, box, pane, scale);
        Assert.Equal(box.End.X - (HudLayoutPlacement.SidesMargin * scale), sides.End.X, 2);
        if (pane.X > box.Size.X + 1f)
        {
            Assert.True(rect.End.X > box.End.X, $"a custom column on {pane} stands past the reading box {box}");
        }

        var far = Custom().With(HudElement.LeftDials, new HudPlacement(HudAnchor.Start, HudAnchor.End, 0f, 900f));
        var (sb, sp, ss) = Frame(1279f, 719f, 2560f, 1440f);
        var kept = HudLayoutPlacement.Place(HudElement.LeftDials, far, sb, sp, ss);
        Assert.True(new Rect2(Vector2.Zero, sp).Encloses(kept));
        Assert.Equal(sp.Y, kept.End.Y, 2);
    }

    // The editor's first draft out of Sides or the original, re-measured from the pane's edges,
    // leaves every element where it stood on that pane.
    [Theory]
    [MemberData(nameof(Shapes))]
    public void ReMeasuringFromThePaneMovesNothingOnThatPane(string shape, float paneW, float paneH, float windowW, float windowH)
    {
        var (box, pane, scale) = Frame(paneW, paneH, windowW, windowH);
        foreach (var layout in new[] { HudLayoutPlacement.Sides, HudLayoutPlacement.OriginalLayout })
        {
            var moved = HudLayoutPlacement.ToPane(layout, box, pane, scale);
            Assert.True(moved.PaneRelative);
            foreach (var element in HudLayout.Elements)
            {
                var before = HudLayoutPlacement.Place(element, layout, box, pane, scale);
                var after = HudLayoutPlacement.Place(element, moved, box, pane, scale);
                Assert.True((after.Position - before.Position).Length() <= scale, $"{shape}: {element} moved {before.Position} to {after.Position}");
            }
        }
    }

    // The editor's drop: the placement read off a rectangle puts the element back on it. Each axis
    // anchors to the third of the reading box its centre stands in.
    [Theory]
    [MemberData(nameof(Shapes))]
    public void ADropReadsBackAsThePlacementThatPutsItThere(string shape, float paneW, float paneH, float windowW, float windowH)
    {
        var (box, pane, scale) = Frame(paneW, paneH, windowW, windowH);
        var size = HudLayoutPlacement.OriginalRect(HudElement.ThrottlePlate, box, pane, scale).Size;
        foreach (var corner in new[] { new Vector2(40f, 60f), new Vector2(0.45f, 0.4f), new Vector2(0.9f, 0.7f) })
        {
            var at = corner.X < 1f ? (pane - size) * corner : corner * scale;
            var rect = HudLayoutPlacement.Inside(new Rect2(at, size), pane);
            var placement = HudLayoutPlacement.PlacementFor(rect, box, scale);
            var back = HudLayoutPlacement.Place(HudElement.ThrottlePlate,
                HudLayoutPlacement.OriginalLayout.With(HudElement.ThrottlePlate, placement), box, pane, scale);
            Assert.True((back.Position - rect.Position).Length() <= scale, $"{shape}: {rect.Position} came back at {back.Position}");
        }

        var right = HudLayoutPlacement.PlacementFor(new Rect2(new Vector2(pane.X - size.X - 10f, 10f), size), box, scale);
        Assert.Equal(HudAnchor.End, right.X);
        Assert.Equal(HudAnchor.Start, right.Y);
        var middle = HudLayoutPlacement.PlacementFor(new Rect2(box.GetCenter() - (size / 2f), size), box, scale);
        Assert.Equal(new HudPlacement(HudAnchor.Centre, HudAnchor.Centre, 0f, 0f), middle);
    }

    [Fact]
    public void TheWordAndTheCustomPlacementRoundTripThroughTheFileAndTheOptionsRow()
    {
        var store = new OptionsStore(TestData.TempDir());
        Assert.Null(store.Load().HudLayout);
        Assert.Null(store.Load().HudLayoutCustom);
        Assert.Null(new OptionsChoices().HudLayout);

        store.Save(new OptionsDef { HudLayout = "sides", HudLayoutCustom = Custom().Format() });
        Assert.Equal("sides", store.Load().HudLayout);
        Assert.Equal(Custom().Format(), store.Load().HudLayoutCustom);
        var choices = new OptionsChoices();
        choices.Load(store.Load());
        Assert.Equal("sides", choices.ToExit().HudLayout);

        var read = OptionsStore.Deserialize(OptionsStore.Serialize(new OptionsDef
        {
            HudLayout = "diagonal", HudLayoutCustom = "leftDials:start", ThrottlePlate = true,
        }));
        Assert.Null(read!.HudLayout);
        Assert.Null(read.HudLayoutCustom);
        Assert.True(read.ThrottlePlate);
    }

    // One fact, since it is the only one here to move the process-wide layout, and it puts it back.
    [Fact]
    public void ApplyAndTheEditorsSaveSetTheRunningLayoutAndOnlyAStoreIsWritten()
    {
        var (kind, custom) = (HudLayoutSetting.Current, HudLayoutSetting.Custom);
        try
        {
            HudLayoutSetting.Apply("sides", "garbage");
            Assert.Equal(HudLayoutKind.Sides, HudLayoutSetting.Current);
            Assert.Null(HudLayoutSetting.Custom);
            Assert.Same(HudLayoutPlacement.Sides, Running());

            HudLayoutSetting.Apply(null, Custom().Format());
            Assert.Equal(HudLayoutKind.Original, HudLayoutSetting.Current);
            Assert.Equal(Custom(), HudLayoutSetting.Custom);
            Assert.Null(Running());

            var store = new OptionsStore(TestData.TempDir());
            store.Save(new OptionsDef { HudMode = "minimal" });
            HudLayoutSetting.SaveCustom(Custom(), store);
            Assert.Equal(HudLayoutKind.Custom, HudLayoutSetting.Current);
            Assert.Equal("custom", store.Load().HudLayout);
            Assert.Equal(Custom().Format(), store.Load().HudLayoutCustom);
            Assert.Equal("minimal", store.Load().HudMode);
            Assert.Equal(Custom(), Running());

            HudLayoutSetting.SaveCustom(HudLayoutPlacement.Sides, null);
            Assert.Equal(HudLayoutPlacement.Sides.InPane(), HudLayoutSetting.Custom);
            Assert.Equal(Custom().Format(), store.Load().HudLayoutCustom);

            // An options accept under --det leaves the run's own pick, here the editor's last save.
            Assert.False(HudLayoutSetting.ApplyAfterOptions(det: true));
            Assert.Equal(HudLayoutKind.Custom, HudLayoutSetting.Current);
            Assert.Equal(HudLayoutPlacement.Sides.InPane(), HudLayoutSetting.Custom);
        }
        finally
        {
            (HudLayoutSetting.Current, HudLayoutSetting.Custom) = (kind, custom);
        }
    }

    private static HudLayout? Running() => HudLayoutPlacement.Resolve(HudLayoutSetting.Current, HudLayoutSetting.Custom);

    private static HudLayout Custom() => HudLayoutPlacement.Sides
        .With(HudElement.CompassTape, new HudPlacement(HudAnchor.Centre, HudAnchor.End, 12.5f, -60f))
        .With(HudElement.ThrottlePlate, new HudPlacement(HudAnchor.Start, HudAnchor.Start, 300f, 120f))
        .InPane();

    private static (Rect2 Box, Vector2 Pane, float Scale) Frame(float paneW, float paneH, float windowW, float windowH)
    {
        var pane = new Vector2(paneW, paneH);
        var window = new Vector2(windowW, windowH);
        return (HudMetrics.ReadingBox(pane, window), pane, HudLayoutPlacement.PaneScale(pane, window));
    }
}
