using System.Text.RegularExpressions;
using ExactNumbers.Core;
using Xunit;

namespace ExactNumbers.Tests;

/// <summary>Every word the mod puts on screen exists in both of the game's languages and follows the game's choice.</summary>
public class TextsTests
{
    public static TheoryData<int> Indexes()
    {
        var data = new TheoryData<int>();

        for (var i = 0; i < Texts.All.Count; i++) data.Add(i);

        return data;
    }

    [Fact]
    public void Every_phrase_on_the_page_is_in_the_table()
    {
        Assert.Equal(10, Texts.All.Count);
        Assert.Equal(Texts.All.Count, Texts.All.Distinct().Count());
    }

    [Theory]
    [MemberData(nameof(Indexes))]
    public void Every_phrase_has_english_and_chinese(int index)
    {
        var phrase = Texts.All[index];

        Assert.False(string.IsNullOrWhiteSpace(phrase.En));
        Assert.False(string.IsNullOrWhiteSpace(phrase.Zh));
        Assert.NotEqual(phrase.En, phrase.Zh);
        Assert.Matches("^[ -~]+$", phrase.En);
        Assert.Matches(@"\p{IsCJKUnifiedIdeographs}", phrase.Zh);
    }

    [Theory]
    [MemberData(nameof(Indexes))]
    public void A_chinese_game_gets_the_chinese_phrase_and_any_other_the_english(int index)
    {
        var phrase = Texts.All[index];

        Assert.Equal(phrase.Zh, phrase.Pick(true));
        Assert.Equal(phrase.En, phrase.Pick(false));
    }

    /// <summary>A translation that drops the {0} would show the words without the number they are about.</summary>
    [Theory]
    [MemberData(nameof(Indexes))]
    public void Both_languages_carry_the_same_number_slots(int index)
    {
        var phrase = Texts.All[index];

        Assert.Equal(Slots(phrase.En), Slots(phrase.Zh));
    }

    [Fact]
    public void A_number_slot_is_filled_in_either_language()
    {
        Assert.Equal("measured over 3 uses", Texts.MeasuredOverUses.Pick(false, "3"));
        Assert.Equal("根据 3 次使用实测", Texts.MeasuredOverUses.Pick(true, "3"));
    }

    private static string[] Slots(string text) => Regex.Matches(text, @"\{\d+\}").Select(match => match.Value).OrderBy(slot => slot).ToArray();
}
