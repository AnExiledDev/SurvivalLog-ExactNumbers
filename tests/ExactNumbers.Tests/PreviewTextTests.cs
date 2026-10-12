using ExactNumbers.Core;
using Xunit;

namespace ExactNumbers.Tests;

public class PreviewTextTests
{
    [Fact]
    public void Chips_come_out_in_the_order_the_page_draws_them()
    {
        var parts = PreviewText.Parse("Satiety:-12;Stamina:-20;MaxMorale:+5");

        Assert.Equal(new[]
        {
            new PreviewPart(false, Stat.Satiety, -12),
            new PreviewPart(false, Stat.Stamina, -20),
            new PreviewPart(true, Stat.Morale, 5),
        }, parts);
    }

    [Fact]
    public void Parts_the_page_skips_are_skipped_so_chip_positions_still_line_up()
    {
        // Zero amounts, stats the page has no chip for, junk and empty pieces draw nothing on the page.
        var parts = PreviewText.Parse(" ;Satiety:0;Health:-5;Stamina -3;Morale:+4 ;;Vitality:-2;");

        Assert.Equal(new[]
        {
            new PreviewPart(false, Stat.Morale, 4),
            new PreviewPart(false, Stat.Vitality, -2),
        }, parts);
    }

    [Fact]
    public void Only_plain_digits_count_as_a_number_as_on_the_page()
    {
        // U+0663 is an Arabic-Indic digit: the page's regex does not take it, so neither may this.
        var parts = PreviewText.Parse("Satiety:-\u0663;Stamina:-3");

        Assert.Equal(new[] { new PreviewPart(false, Stat.Stamina, -3) }, parts);
    }

    [Fact]
    public void An_amount_too_big_to_read_leaves_the_whole_tooltip_as_the_game_drew_it()
    {
        Assert.Null(PreviewText.Parse("Satiety:-99999999999;Stamina:-3"));
    }

    [Fact]
    public void An_empty_preview_has_no_chips()
    {
        Assert.Empty(PreviewText.Parse("")!);
        Assert.Empty(PreviewText.Parse(null)!);
    }

    [Theory]
    [InlineData(-1, "\u2212")]
    [InlineData(-10, "\u2212")]
    [InlineData(-11, "\u2212\u2212")]
    [InlineData(30, "++")]
    [InlineData(31, "+++")]
    [InlineData(-200, "\u2212\u2212\u2212")]
    public void Vanilla_marks_match_the_page_size_tiers(int amount, string marks)
    {
        Assert.Equal(marks, PreviewText.VanillaText(new PreviewPart(false, Stat.Satiety, amount)));
    }

    [Fact]
    public void A_cap_chip_shows_its_signed_number_on_the_page()
    {
        Assert.Equal("+5", PreviewText.VanillaText(new PreviewPart(true, Stat.Morale, 5)));
        Assert.Equal("\u221240", PreviewText.VanillaText(new PreviewPart(true, Stat.Vitality, -40)));
    }
}
