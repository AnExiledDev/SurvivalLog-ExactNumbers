using ExactNumbers.Core;
using Xunit;

namespace ExactNumbers.Tests;

public class AmountTextTests
{
    [Fact]
    public void Whole_numbers_are_written_without_a_decimal()
    {
        Assert.Equal("+12", AmountText.Format(12));
        Assert.Equal("\u221220", AmountText.Format(-20));
    }

    [Fact]
    public void Fractions_round_to_one_decimal_away_from_zero()
    {
        Assert.Equal("+2.3", AmountText.Format(2.25));
        Assert.Equal("\u22122.3", AmountText.Format(-2.25));
        Assert.Equal("\u221213", AmountText.Format(-12.96));
    }

    [Fact]
    public void A_change_too_small_to_show_is_written_as_zero_without_a_sign()
    {
        Assert.Equal("0", AmountText.Format(0.04));
        Assert.Equal("0", AmountText.Format(-0.04));
    }

    [Fact]
    public void Decreases_use_the_same_minus_sign_as_the_page()
    {
        Assert.StartsWith("\u2212", AmountText.Format(-1));
    }
}
