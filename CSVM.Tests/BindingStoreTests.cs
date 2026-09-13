using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CSVM.Bindings;
using Godot;
using Xunit;

namespace CSVM.Tests;

/// <summary>The persistence contract: a profile round-trips through the file whole, an edit and an
/// unbind survive it, a file from a bumped version keeps the rows this build can read, and an
/// unknown action, an unreadable token or a hat row costs that action its saved bindings and
/// nothing else.</summary>
public class BindingStoreTests
{
    private static readonly DeviceId Pad = DeviceId.Joypad("030000004c050000c405000000010000");

    [Fact]
    public void RoundTrip_PreservesEveryBinding()
    {
        var profile = BindingProfile.Defaults(Pad, readsKeyboard: true);
        var loaded = BindingStore.Deserialize(BindingStore.Serialize(1, profile), Pad, readsKeyboard: true);

        AssertSameMaps(profile, loaded);
    }

    /// <summary>An edited map round-trips as edited: the rebind, the action that lost the control,
    /// and an action the player unbound outright all come back the way they were saved.</summary>
    [Fact]
    public void RoundTrip_PreservesARebindAndAnUnbind()
    {
        var profile = BindingProfile.Defaults(Pad, readsKeyboard: true);
        var map = profile.Map(InputContext.Flight);
        var space = new Binding(DeviceId.Keyboard, BindingControl.Key((int)Key.Space));
        Assert.Equal(new[] { InputAction.FireGuns }, map.Assign(InputAction.FireRockets, space));
        map.Clear(InputAction.Nitro);

        var loaded = BindingStore.Deserialize(BindingStore.Serialize(1, profile), Pad, readsKeyboard: true);
        var reloaded = loaded.Map(InputContext.Flight);

        Assert.Contains(space, reloaded.Bindings(InputAction.FireRockets));
        Assert.DoesNotContain(space, reloaded.Bindings(InputAction.FireGuns));
        Assert.Empty(reloaded.Bindings(InputAction.Nitro));
        AssertSameMaps(profile, loaded);
    }

    /// <summary>The snap-look diagonals are one key on two actions, so the file has to carry a
    /// control twice inside one context and read it back that way.</summary>
    [Fact]
    public void RoundTrip_KeepsAControlThatDrivesTwoActions()
    {
        var profile = BindingProfile.Defaults(Pad, readsKeyboard: true);
        var loaded = BindingStore.Deserialize(BindingStore.Serialize(1, profile), Pad, readsKeyboard: true)
            .Map(InputContext.Flight);
        var kp7 = new Binding(DeviceId.Keyboard, BindingControl.Key((int)Key.Kp7));

        Assert.Contains(kp7, loaded.Bindings(InputAction.LookUp));
        Assert.Contains(kp7, loaded.Bindings(InputAction.LookLeft));
    }

    /// <summary>A mouse binding round-trips through the file like any other control: the token names
    /// the device and the button in words, and reads back to the same binding.</summary>
    [Fact]
    public void RoundTrip_PreservesAMouseBinding()
    {
        var profile = BindingProfile.Defaults(Pad, readsKeyboard: true);
        var json = BindingStore.Serialize(1, profile);

        Assert.Contains("mouse/mouse:Right", json, StringComparison.Ordinal);

        var loaded = BindingStore.Deserialize(json, Pad, readsKeyboard: true).Map(InputContext.Flight);
        Assert.Equal(
            new[] { new Binding(DeviceId.Mouse, BindingControl.Mouse((int)MouseButton.Right)) },
            loaded.Bindings(InputAction.FreeLook));
    }

    /// <summary>A hand-written file from a bumped version: the version says how the tokens are
    /// encoded, so a reader that understands the tokens it is given keeps them and every action the
    /// file does not name stays at its shipped default.</summary>
    [Fact]
    public void Load_FileFromABumpedVersion_KeepsTheRowsItCanRead()
    {
        var json = """
        {
          "version": 99,
          "player": 1,
          "contexts": {
            "flight": { "FireGuns": ["keyboard/key:Z", "pad:*/button:X"] },
            "somethingNew": { "Whatever": ["keyboard/key:A"] }
          }
        }
        """;
        var loaded = BindingStore.Deserialize(json, Pad, readsKeyboard: true).Map(InputContext.Flight);

        Assert.Equal(
            new[]
            {
                new Binding(DeviceId.Keyboard, BindingControl.Key((int)Key.Z)),
                new Binding(Pad, BindingControl.Button((int)JoyButton.X)),
            },
            loaded.Bindings(InputAction.FireGuns));
        Assert.Equal(
            DefaultBindings.MapFor(InputContext.Flight, Pad).Bindings(InputAction.FireRockets),
            loaded.Bindings(InputAction.FireRockets));
    }

    [Fact]
    public void Load_UnknownActionName_LeavesEveryOtherActionAlone()
    {
        var json = Row("flight", "\"Teleport\": [\"keyboard/key:Z\"], \"Nitro\": [\"keyboard/key:Z\"]");
        var loaded = BindingStore.Deserialize(json, Pad, readsKeyboard: true).Map(InputContext.Flight);

        Assert.Equal(
            new[] { new Binding(DeviceId.Keyboard, BindingControl.Key((int)Key.Z)) },
            loaded.Bindings(InputAction.Nitro));
    }

    /// <summary>A hat row can only be a hand edit: nothing in the build authors one, and on this
    /// backend it would alias a d-pad button binding. The action keeps its default instead.
    /// </summary>
    [Fact]
    public void Load_HatToken_LeavesTheActionAtItsDefault()
    {
        var json = Row("flight", "\"SelectGunGroup\": [\"pad:*/hat:0:Right\"]");
        var loaded = BindingStore.Deserialize(json, Pad, readsKeyboard: true).Map(InputContext.Flight);

        Assert.Equal(
            DefaultBindings.MapFor(InputContext.Flight, Pad).Bindings(InputAction.SelectGunGroup),
            loaded.Bindings(InputAction.SelectGunGroup));
    }

    /// <summary>One unreadable token costs the whole row, not part of it: half a keymap the player
    /// never chose is worse than the default they know.</summary>
    [Fact]
    public void Load_UnreadableToken_LeavesTheWholeRowAtItsDefault()
    {
        var json = Row("flight", "\"Nitro\": [\"keyboard/key:Z\", \"keyboard/key:Nonsense\"]");
        var loaded = BindingStore.Deserialize(json, Pad, readsKeyboard: true).Map(InputContext.Flight);

        Assert.Equal(
            DefaultBindings.MapFor(InputContext.Flight, Pad).Bindings(InputAction.Nitro),
            loaded.Bindings(InputAction.Nitro));
    }

    [Fact]
    public void Load_EmptyRow_IsADeliberateUnbind()
    {
        var loaded = BindingStore.Deserialize(Row("flight", "\"Nitro\": []"), Pad, readsKeyboard: true);

        Assert.Empty(loaded.Map(InputContext.Flight).Bindings(InputAction.Nitro));
    }

    [Fact]
    public void Load_MalformedJson_ReadsAsTheShippedDefaults()
    {
        var loaded = BindingStore.Deserialize("{ not json", Pad, readsKeyboard: true);

        AssertSameMaps(BindingProfile.Defaults(Pad, true), loaded);
    }

    /// <summary>A file saved on one machine and read where the pad is a different one: a row saved
    /// on the placeholder identity follows the seat, while a row naming a real pad keeps naming it.
    /// </summary>
    [Fact]
    public void Load_PlaceholderPadRow_FollowsTheSeatsPad()
    {
        var other = DeviceId.Joypad("some-other-pad");
        var json = Row("flight", "\"Nitro\": [\"pad:*/button:Y\", \"pad:some-other-pad/button:A\"]");
        var loaded = BindingStore.Deserialize(json, Pad, readsKeyboard: true).Map(InputContext.Flight);

        Assert.Equal(
            new[]
            {
                new Binding(Pad, BindingControl.Button((int)JoyButton.Y)),
                new Binding(other, BindingControl.Button((int)JoyButton.A)),
            },
            loaded.Bindings(InputAction.Nitro));
    }

    [Fact]
    public void Load_MissingFile_ReadsAsTheShippedDefaults()
    {
        var store = new BindingStore(TestData.TempDir());

        AssertSameMaps(BindingProfile.Defaults(Pad, true), store.Load(1, Pad, readsKeyboard: true));
    }

    /// <summary>Through the file itself, and per player: player two's save does not disturb player
    /// one's, which is the whole reason the file is named for the seat.</summary>
    [Fact]
    public void SaveAndLoad_KeepThePlayersApart()
    {
        var dir = TestData.TempDir();
        var store = new BindingStore(dir);
        var second = BindingProfile.Defaults(Pad, readsKeyboard: false);
        second.Map(InputContext.Flight).Clear(InputAction.Nitro);
        store.Save(1, BindingProfile.Defaults(Pad, readsKeyboard: true));
        store.Save(2, second);

        Assert.True(File.Exists(Path.Combine(dir, "bindings_p2.json")));
        Assert.NotEmpty(store.Load(1, Pad, true).Map(InputContext.Flight).Bindings(InputAction.Nitro));
        Assert.Empty(store.Load(2, Pad, false).Map(InputContext.Flight).Bindings(InputAction.Nitro));
    }

    /// <summary>The file is text a player can read and correct, which is the property the original's
    /// 2400 unversioned raw bytes do not have.</summary>
    [Fact]
    public void Serialize_NamesActionsAndControlsInWords()
    {
        var json = BindingStore.Serialize(1, BindingProfile.Defaults(Pad, readsKeyboard: true));

        Assert.Contains("\"version\": 2", json, StringComparison.Ordinal);
        Assert.Contains("\"FireGuns\"", json, StringComparison.Ordinal);
        Assert.Contains("keyboard/key:Space", json, StringComparison.Ordinal);
        Assert.Contains("keyboard/key:Shift+S", json, StringComparison.Ordinal);
        Assert.Contains("axis:TriggerRight+@0", json, StringComparison.Ordinal);
    }

    /// <summary>A modified key round-trips through its own token: the prefix is part of the control,
    /// so a file written before it existed and one written after it are both read as written.
    /// </summary>
    [Fact]
    public void RoundTrip_PreservesAModifiedKey()
    {
        var profile = BindingProfile.Defaults(Pad, readsKeyboard: true);
        var loaded = BindingStore.Deserialize(BindingStore.Serialize(1, profile), Pad, readsKeyboard: true)
            .Map(InputContext.Flight);

        Assert.Equal(
            new[] { new Binding(DeviceId.Keyboard, BindingControl.Key((int)Key.E, KeyModifiers.Shift)) },
            loaded.Bindings(InputAction.TargetPreviousEnemy));
    }

    /// <summary>A version 1 file, which has no modifier prefix on any token, still loads whole: the
    /// reader checks no version and a bare key token means a bare key in either version. A player who
    /// last played before the modifiers existed keeps the keymap they chose.</summary>
    [Fact]
    public void Load_AVersionOneFile_KeepsEveryRowItNames()
    {
        var json = """
        {
          "version": 1,
          "player": 1,
          "contexts": {
            "flight": {
              "FireGuns": ["keyboard/key:Z"],
              "TargetPreviousEnemy": ["keyboard/key:Key5"],
              "ToggleSpyglass": ["keyboard/key:F2"]
            }
          }
        }
        """;
        var loaded = BindingStore.Deserialize(json, Pad, readsKeyboard: true).Map(InputContext.Flight);

        Assert.Equal(
            new[] { new Binding(DeviceId.Keyboard, BindingControl.Key((int)Key.Z)) },
            loaded.Bindings(InputAction.FireGuns));
        Assert.Equal(
            new[] { new Binding(DeviceId.Keyboard, BindingControl.Key((int)Key.Key5)) },
            loaded.Bindings(InputAction.TargetPreviousEnemy));
        Assert.Equal(
            new[] { new Binding(DeviceId.Keyboard, BindingControl.Key((int)Key.F2)) },
            loaded.Bindings(InputAction.ToggleSpyglass));
    }

    /// <summary>An unreadable modifier name costs the row it stands on and nothing else, on the same
    /// rule as any other unreadable token.</summary>
    [Fact]
    public void Load_UnknownModifierName_LeavesTheWholeRowAtItsDefault()
    {
        var json = Row("flight", "\"ToggleSpyglass\": [\"keyboard/key:Hyper+S\"]");
        var loaded = BindingStore.Deserialize(json, Pad, readsKeyboard: true).Map(InputContext.Flight);

        Assert.Equal(
            DefaultBindings.MapFor(InputContext.Flight, Pad).Bindings(InputAction.ToggleSpyglass),
            loaded.Bindings(InputAction.ToggleSpyglass));
    }

    /// <summary>A saved keymap is written atomically, so a run killed mid-write leaves the previous
    /// save rather than a half-written one.</summary>
    [Fact]
    public void Load_LeftoverTempFile_IsIgnored()
    {
        var dir = TestData.TempDir();
        var store = new BindingStore(dir);
        store.Save(1, BindingProfile.Defaults(Pad, readsKeyboard: true));
        File.WriteAllText(Path.Combine(dir, "bindings_p1.json.tmp"), "{ half-writ", new UTF8Encoding(false));

        AssertSameMaps(BindingProfile.Defaults(Pad, true), store.Load(1, Pad, true));
    }

    private static string Row(string context, string body) =>
        "{\"version\": 1, \"player\": 1, \"contexts\": {\"" + context + "\": {" + body + "}}}";

    private static void AssertSameMaps(BindingProfile expected, BindingProfile actual)
    {
        foreach (var context in Enum.GetValues<InputContext>())
        {
            foreach (var action in DefaultBindings.ActionsIn(context))
            {
                Assert.Equal(
                    expected.Map(context).Bindings(action).ToArray(),
                    actual.Map(context).Bindings(action).ToArray());
            }
        }
    }
}
