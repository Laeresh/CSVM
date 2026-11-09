using System.Collections.Generic;
using System.Linq;
using System.Text;
using CSVM.Extraction;
using CSVM.Mech3;
using CSVM.Net;
using CSVM.UI.Menu;
using CSVM.UI.Menu.Original;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// One name per chapter in Free Flight, Instant Action and Multiplayer (<see cref="MenuChapters.Name"/>).
/// An English install or a missing string table takes the English table. A translated install takes
/// the game's own word, one row per chapter, so every mode shows the same word.
/// </summary>
public class MenuChapterNamesTests
{
    /// <summary>The German disc's string resources' language ID.</summary>
    internal const int German = 0x407;

    private static readonly Dictionary<string, string> English = new()
    {
        ["C1"] = "Airfield",
        ["C1B"] = "The Ocean",
        ["C1C"] = "Above the Clouds",
        ["C2"] = "Hollywood",
        ["C2B"] = "The Clouds",
        ["C3"] = "Hawaii",
        ["C4"] = "Sky Haven",
        ["C5"] = "Manhattan",
    };

    // An invented translation laid out as the shipped table is. The Multiplayer rows run from 10558
    // in the lobby's order, one carrying the empty tag's ]. Instant Action's clouds row is 3651.
    private static readonly Dictionary<string, string> Translated = new()
    {
        ["C1C"] = "Ueber den Wolken",
        ["C3"] = "Hawaii-Inseln",
        ["C2"] = "Hollywood-Studio",
        ["C5"] = "Manhattan",
        ["C1"] = "NW Flugfeld",
        ["C1B"] = "NW Leuchtturm",
        ["C4"] = "Sky Haven (Rockies)",
        ["C2B"] = "den Wolken",
    };

    [Fact]
    public void WithNoTableOrAnEnglishOneEveryChapterTakesItsEnglishName()
    {
        var english = Table(0x409);
        foreach (var chapter in MenuChapters.All)
        {
            Assert.Equal(English[chapter.Code], MenuChapters.Name(chapter.Code));
            Assert.Equal(English[chapter.Code], MenuChapters.Name(chapter.Code, UiStrings.Empty));
            Assert.Equal(English[chapter.Code], MenuChapters.Name(chapter.Code, english));
        }

        Assert.False(english.IsTranslation);
        Assert.False(UiStrings.Empty.IsTranslation);
    }

    [Fact]
    public void ATranslatedTableGivesEachChapterTheGamesOwnWord()
    {
        var strings = Table(German);

        Assert.True(strings.IsTranslation);
        Assert.Equal(German, strings.Language);
        foreach (var chapter in MenuChapters.All)
        {
            Assert.Equal(Translated[chapter.Code], MenuChapters.Name(chapter.Code, strings));
        }

        // ABLE-TO-FAIL CONTROL: a translated table missing a chapter's row falls back to English.
        var partial = Table(German, skip: 3651);
        Assert.Equal("The Clouds", MenuChapters.Name("C2B", partial));
        Assert.Equal("Ueber den Wolken", MenuChapters.Name("C1C", partial));
    }

    /// <summary>The Multiplayer half of the one-word rule. The lobby's Environment box and the games
    /// list name a row by the chapter it flies, the same word <see cref="TranslatedName"/> gives.
    /// Free Flight's map box and Instant Action's Environment box are held to that word through
    /// their screens, in <c>OriginalShellTests</c> and <c>OriginalInstantActionTests</c>.</summary>
    [Fact]
    public void TheLobbyBoxAndTheGamesListNameEachRowByItsChapter()
    {
        foreach (var (strings, words) in new[] { ((UiStrings?)null, English), (Table(German), Translated) })
        {
            for (int row = 0; row < DogfightLobby.EnvironmentCount; row++)
            {
                string word = words[DogfightLobby.ChapterOf(row)];
                Assert.Equal(word, DogfightLobby.EnvironmentName(row, strings));
                var advert = new SessionAdvertMessage(NetSessionKind.Dogfight, (byte)row, 2, "Lucy");
                Assert.Equal(word, CoopDoorText.Environment(advert, _ => "x", _ => true, strings));
            }
        }
    }

    [Fact]
    public void EveryModeKeepsItsChaptersAndTheirOrder()
    {
        Assert.Equal(new[] { "C1", "C1B", "C1C", "C2", "C2B", "C3", "C4", "C5" }, OriginalRosters.Chapters.Select(c => c.Code));
        Assert.Equal(new[] { "C1", "C2B", "C3", "C5", "C1B", "C4", "C2" }, InstantActionFeature.Environments.Select(e => e.Code));
        Assert.Equal(
            new[] { "C1C", "C3", "C2", "C5", "C1", "C1B", "C4" },
            Enumerable.Range(0, DogfightLobby.EnvironmentCount).Select(DogfightLobby.ChapterOf));
    }

    [Fact]
    public void TheStringTableReportsItsLanguage()
    {
        byte[] pe = ExtractionFixtures.StringTablePe(1, new[] { "a" }, German);

        PeStringTable.Read(pe, out int language);

        Assert.Equal(German, language);
    }

    /// <summary>The invented translation's word for a chapter.</summary>
    internal static string TranslatedName(string code) => Translated[code];

    /// <summary>The table as the runtime reads it, through the JSON the extraction writes.</summary>
    internal static UiStrings Table(int language, int skip = -1) => UiStrings.Parse(Encoding.UTF8.GetString(Json(language, skip)));

    /// <summary>The table's <c>ui_strings.json</c> under <paramref name="language"/>, less the row
    /// <paramref name="skip"/>. Each row carries a face as the shipped rows do; the third also
    /// leads with the empty tag's ].</summary>
    internal static byte[] Json(int language, int skip = -1)
    {
        var rows = new List<UiStringRow>();
        for (int row = 0; row < DogfightLobby.EnvironmentCount; row++)
        {
            string word = Translated[DogfightLobby.ChapterOf(row)];
            rows.Add(new UiStringRow(10558 + row, null, "TREB9", row == 2 ? "]" + word : word, "langui", language));
        }

        rows.Add(new UiStringRow(3651, null, "TREB9", Translated["C2B"], "langui", language));
        rows.RemoveAll(r => r.Id == skip);
        return UiStringTable.ToJson(rows);
    }
}
