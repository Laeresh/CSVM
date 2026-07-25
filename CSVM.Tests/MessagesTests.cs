using System.IO;
using CSVM.Mech3;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The <c>MSG_*</c> string table and its placeholder grammar (<c>docs/formats/missions.md</c>).
/// Input is <c>fixtures/messages.json</c>.
/// </summary>
public class MessagesTests
{
    private static Messages Load() => Messages.Load(TestData.Fixture("messages.json"));

    [Fact]
    public void EntriesAreIndexedByKeyCaseInsensitively()
    {
        var messages = Load();
        Assert.Equal(4, messages.Count);
        Assert.Equal("Probe target", messages.Get("MSG_PROBE_PLAIN"));
        Assert.Equal("Probe target", messages.Get("msg_probe_plain"));
    }

    [Fact]
    public void AnUnknownKeyResolvesToItselfSoTheGapIsVisible()
    {
        Assert.Equal("MSG_NOT_IN_TABLE", Load().Get("MSG_NOT_IN_TABLE"));
        Assert.Equal("", Load().Get(null));
        Assert.Equal("", Load().Get(""));
    }

    [Fact]
    public void AMissingTableIsEmptyRatherThanAThrow()
    {
        // Display strings are cosmetic; a mission must still load without them.
        var messages = Messages.Load(Path.Combine(TestData.TempDir(), "absent.json"));
        Assert.Equal(0, messages.Count);
        Assert.Equal("MSG_ANYTHING", messages.Get("MSG_ANYTHING"));
    }

    [Fact]
    public void PositionalPlaceholdersTakeTheirArgumentsInOrder()
    {
        Assert.Equal("Vickers 240 rounds", Load().Format("MSG_PROBE_AMMO", "Vickers", "240"));
    }

    [Fact]
    public void ATypeSpecAfterAPlaceholderIsConsumed()
    {
        // "%2!d!" — the argument arrives already formatted, so the !d! marker must not print.
        Assert.Equal("a b", Messages.Fill("%1!s! %2!d!", "a", "b"));
    }

    [Fact]
    public void AMissingArgumentRendersEmptyRatherThanLeavingThePlaceholder()
    {
        Assert.Equal("[a][]", Load().Format("MSG_PROBE_GAP", "a"));
    }

    [Fact]
    public void DoubledPercentIsALiteralPercent()
    {
        Assert.Equal("100% throttle", Load().Format("MSG_PROBE_PERCENT"));
    }

    [Fact]
    public void TextWithNoPlaceholdersIsUnchangedAndAnEmptyTemplateStaysEmpty()
    {
        Assert.Equal("plain text 5%", Messages.Fill("plain text 5%"));
        Assert.Equal("", Messages.Fill(""));
    }
}
