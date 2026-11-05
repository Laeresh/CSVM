using System.IO;
using CSVM.UI.Menu;
using CSVM.Utils;
using Xunit;

namespace CSVM.Tests;

/// <summary>The Keep Camera Zoom option and the zoom it remembers, engine-free. A fresh file reads
/// on and seeds the near end, and a toggle persists. A kept zoom round-trips, written once per
/// value. Off writes no zoom and seeds the near end over a saved one. A zoom off the axis reads as
/// never set. Over no store, a <c>--det</c> run, it reads on, seeds the near end and writes nothing.
/// </summary>
public class CameraZoomMemoryTests
{
    [Fact]
    public void AFreshFileReadsOnAndSeedsTheNearEnd()
    {
        var store = new OptionsStore(TestData.TempDir());
        var memory = new CameraZoomMemory(store);
        Assert.True(memory.Keep);
        Assert.Equal(CameraZoomMemory.NearEnd, memory.Seed());
        Assert.True(new OptionsChoices().KeepCameraZoomOn);
    }

    [Fact]
    public void TheToggleRoundTripsThroughTheFile()
    {
        var store = new OptionsStore(TestData.TempDir());
        store.Save(new OptionsDef { KeepCameraZoom = false });
        Assert.False(store.Load().KeepCameraZoom);
        Assert.False(new CameraZoomMemory(store).Keep);
        var choices = new OptionsChoices();
        choices.Load(store.Load());
        Assert.False(choices.KeepCameraZoomOn);
        Assert.False(choices.ToExit().KeepCameraZoom);

        store.Save(new OptionsDef { KeepCameraZoom = true });
        Assert.True(store.Load().KeepCameraZoom);
        Assert.True(new CameraZoomMemory(store).Keep);
    }

    [Fact]
    public void AKeptZoomIsSavedOncePerValueAndSeedsTheNextInit()
    {
        var store = new OptionsStore(TestData.TempDir());
        var memory = new CameraZoomMemory(store);
        Assert.True(memory.Save(0.625f));
        Assert.False(memory.Save(0.625f));
        Assert.Equal(0.625f, store.Load().CameraZoom);
        Assert.Equal(0.625f, new CameraZoomMemory(store).Seed());
    }

    [Fact]
    public void OffWritesNoZoomAndSeedsTheNearEnd()
    {
        var dir = TestData.TempDir();
        var store = new OptionsStore(dir);
        store.Save(new OptionsDef { KeepCameraZoom = false });
        var memory = new CameraZoomMemory(store);
        Assert.False(memory.Save(0.75f));
        Assert.Null(store.Load().CameraZoom);
        Assert.Contains("\"cameraZoom\": null", File.ReadAllText(Path.Combine(dir, OptionsStore.FileName)), System.StringComparison.Ordinal);

        store.Save(new OptionsDef { KeepCameraZoom = false, CameraZoom = 0.75f });
        Assert.Equal(CameraZoomMemory.NearEnd, new CameraZoomMemory(store).Seed());
    }

    [Fact]
    public void OffThenOnWritesTheFlyingZoomBack()
    {
        var store = new OptionsStore(TestData.TempDir());
        var memory = new CameraZoomMemory(store);
        Assert.True(memory.Save(0.5f));

        var options = store.Load();
        CameraZoomMemory.Stage(options, false);
        store.Save(options);
        memory.Reload(store);
        Assert.Null(store.Load().CameraZoom);
        Assert.False(memory.Save(0.5f));

        options = store.Load();
        CameraZoomMemory.Stage(options, true);
        store.Save(options);
        memory.Reload(store);
        Assert.True(memory.Save(0.5f));
        Assert.Equal(0.5f, store.Load().CameraZoom);
        Assert.True(store.Load().KeepCameraZoom);
    }

    [Fact]
    public void ASettledNearEndOverANeverSetZoomWritesNothing()
    {
        var dir = TestData.TempDir();
        var memory = new CameraZoomMemory(new OptionsStore(dir));
        Assert.False(memory.Save(CameraZoomMemory.NearEnd));
        Assert.False(File.Exists(Path.Combine(dir, OptionsStore.FileName)));
    }

    [Theory]
    [InlineData("1.5")]
    [InlineData("-0.25")]
    [InlineData("\"0.5\"")]
    public void AZoomOffTheAxisReadsNeverSet(string value)
    {
        var read = OptionsStore.Deserialize("{\"version\": 1, \"cameraZoom\": " + value + "}");
        Assert.NotNull(read);
        Assert.Null(read!.CameraZoom);
    }

    [Fact]
    public void ADeterministicRunReadsNothingAndWritesNothing()
    {
        var memory = new CameraZoomMemory(null);
        Assert.True(memory.Keep);
        Assert.Equal(CameraZoomMemory.NearEnd, memory.Seed());
        Assert.False(memory.Save(0.5f));
    }
}
