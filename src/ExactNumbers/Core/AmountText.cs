using System;
using System.Globalization;

namespace ExactNumbers.Core;

/// <summary>How a number is written in a chip: signed like the page's own marks, whole numbers bare, others to one decimal.</summary>
public static class AmountText
{
    private const char MinusSign = '\u2212';

    public static string Format(double amount)
    {
        var rounded = Math.Round(amount, 1, MidpointRounding.AwayFromZero);

        if (rounded == 0) return "0";

        var size = Math.Abs(rounded).ToString("0.#", CultureInfo.InvariantCulture);

        return (rounded > 0 ? '+' : MinusSign) + size;
    }
}
