namespace ExactNumbers.Core;

/// <summary>What the game knows about one ratio stat (for example the satiety cost ratio) on the player's character.</summary>
public readonly record struct RatioStat(int BaseValue, int StrengtheningValue, int Min, int Max, bool IsSingle);

/// <summary>
/// The game's own scaling of a stat change. AttributeComponent.CostAttrValue multiplies every change to satiety, mood,
/// energy and life by (1 + ratio), where ratio is the matching ratio stat (base plus strengthening, held between its
/// min and max) divided by GameKey.Attr_ScalingRatio, or 0 for a ratio stat that is not a plain value. The game clears
/// the stat's condition part before each change, so it is left out here too.
/// </summary>
public static class CostRatio
{
    /// <returns>Null when the scaling constant would make the game's own result meaningless, so the base is shown instead.</returns>
    public static float? Of(RatioStat stat, float scalingRatio)
    {
        if (!stat.IsSingle) return 0f;

        var value = unchecked(stat.BaseValue + stat.StrengtheningValue);

        // Same order as the game: under the minimum takes the minimum, otherwise over the maximum takes the maximum.
        if (value < stat.Min) value = stat.Min;
        else if (value > stat.Max) value = stat.Max;

        var ratio = value / scalingRatio;

        return float.IsFinite(ratio) ? ratio : null;
    }

    /// <summary>The change the game applies for a preview amount: (ratio + 1) * amount, in the game's float arithmetic.</summary>
    public static float Apply(float ratio, int amount) => (ratio + 1f) * amount;
}
