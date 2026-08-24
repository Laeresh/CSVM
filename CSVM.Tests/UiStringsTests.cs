using CSVM.Mech3;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The langui string table's reading rules: the two merged tables are not one namespace, and the
/// placeholders are Win32 `FormatMessage` specifiers rather than printf ones
/// (docs/formats/strings.md).
/// </summary>
public class UiStringsTests
{
    /// <summary>Ids are not unique across the file's two tables (9-35 exist in both), so anything
    /// but a langui row is dropped rather than allowed to answer a menu lookup.</summary>
    [Fact]
    public void OnlyLanguiRowsAreKept()
    {
        var strings = UiStrings.Parse(
            "[{\"id\":20,\"text\":\"the other table\",\"dll\":\"language\"}," +
            "{\"id\":20,\"text\":\"the menu one\",\"dll\":\"langui\"}]");
        Assert.Equal("the menu one", strings.Text(20));
    }

    [Fact]
    public void AMissingIdFallsBack() =>
        Assert.Equal("fallback", UiStrings.Empty.Text(1004, "fallback"));

    /// <summary>A `[FONTID]` tag the extractor could not lift off a multi-line row is a renderer
    /// directive, not text.</summary>
    [Fact]
    public void ALeadingFontTagIsStripped()
    {
        var strings = UiStrings.Parse("[{\"id\":1030,\"text\":\"[CSB11I]WEIGHT CAPACITY:\",\"dll\":\"langui\"}]");
        Assert.Equal("WEIGHT CAPACITY:", strings.Text(1030));
    }

    [Theory]
    [InlineData("%1!d! units", "{0} units")]
    [InlineData("Left Wing: %1!d!", "Left Wing: {0}")]
    [InlineData("%1!s! %2!s!", "{0} {1}")]
    [InlineData("%1!02d!:%2!02d!", "{0:00}:{1:00}")]
    [InlineData("%1!d!%%", "{0}%")]
    [InlineData("nothing to do", "nothing to do")]
    [InlineData("a {brace}", "a {{brace}}")]
    public void FormatMessageSpecifiersBecomeCompositeFormat(string original, string expected) =>
        Assert.Equal(expected, UiStrings.ToCompositeFormat(original));

    /// <summary>The specifiers are positional: `%2!s!` takes the second argument whatever order
    /// the string uses them in.</summary>
    [Fact]
    public void FormattingIsPositional()
    {
        var strings = UiStrings.Parse("[{\"id\":7,\"text\":\"%2!s! then %1!s!\",\"dll\":\"langui\"}]");
        Assert.Equal("second then first", strings.Format(7, "first", "second"));
    }

    /// <summary>The twin-gun prefix is a parenthesised count (A2's correction to the decode), not
    /// a literal "2x".</summary>
    [Fact]
    public void TheTwinPrefixIsAParenthesisedCount()
    {
        var strings = UiStrings.Parse("[{\"id\":506,\"text\":\"(%1!d!) \",\"dll\":\"langui\"}]");
        Assert.Equal("(2) ", strings.Format(506, 2));
    }
}
