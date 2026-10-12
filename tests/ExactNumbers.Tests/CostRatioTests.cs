using ExactNumbers.Core;
using Xunit;

namespace ExactNumbers.Tests;

public class CostRatioTests
{
    [Fact]
    public void Ratio_is_base_plus_strengthening_over_the_scaling_ratio()
    {
        var ratio = CostRatio.Of(new RatioStat(BaseValue: 150, StrengtheningValue: 50, Min: -1000, Max: 1000, IsSingle: true), 1000f);

        Assert.Equal<float?>(0.2f, ratio);
    }

    [Fact]
    public void A_value_under_the_minimum_counts_as_the_minimum()
    {
        var ratio = CostRatio.Of(new RatioStat(-900, -300, Min: -500, Max: 1000, IsSingle: true), 1000f);

        Assert.Equal<float?>(-0.5f, ratio);
    }

    [Fact]
    public void A_value_over_the_maximum_counts_as_the_maximum()
    {
        var ratio = CostRatio.Of(new RatioStat(800, 400, Min: -500, Max: 1000, IsSingle: true), 1000f);

        Assert.Equal<float?>(1f, ratio);
    }

    [Fact]
    public void A_ratio_stat_that_is_not_a_plain_value_changes_nothing()
    {
        var ratio = CostRatio.Of(new RatioStat(500, 0, Min: -1000, Max: 1000, IsSingle: false), 1000f);

        Assert.Equal<float?>(0f, ratio);
    }

    [Fact]
    public void A_zero_scaling_ratio_gives_no_ratio_so_the_base_is_shown()
    {
        Assert.Null(CostRatio.Of(new RatioStat(500, 0, Min: -1000, Max: 1000, IsSingle: true), 0f));
    }

    [Fact]
    public void The_change_is_the_amount_times_one_plus_the_ratio()
    {
        Assert.Equal(-15f, CostRatio.Apply(0.25f, -12));
        Assert.Equal(-6f, CostRatio.Apply(-0.5f, -12));
    }
}
