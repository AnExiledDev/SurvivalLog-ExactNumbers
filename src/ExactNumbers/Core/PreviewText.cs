using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace ExactNumbers.Core;

/// <summary>The four stats the game's tooltips draw a chip for.</summary>
public enum Stat
{
    Satiety,
    Morale,
    Stamina,
    Vitality,
}

/// <summary>One chip the page draws: a stat change, or a change to that stat's maximum (a cap).</summary>
public sealed record PreviewPart(bool IsCap, Stat Stat, int Amount)
{
    public bool IsGain => Amount > 0;
}

/// <summary>
/// Reads the game's preview text, such as "Satiety:-12;Stamina:-20", exactly the way the game's pages do
/// (buildAttrChips in SelectMenu.html and ToolTable.html), so chip number i here is chip number i on the page.
/// </summary>
public static class PreviewText
{
    private const char MinusSign = '\u2212';
    private const int OneMarkUpTo = 10;
    private const int TwoMarksUpTo = 30;

    // [0-9] rather than \d: the page's regex has no unicode flag, so only ASCII digits count there.
    private static readonly Regex Part = new(@"^(Max)?(Satiety|Morale|Stamina|Vitality):([-+]?[0-9]+)\z", RegexOptions.CultureInvariant);

    /// <summary>
    /// The chips in page order. Parts the page skips (unknown stats, zero amounts, junk) are skipped here too. Null when
    /// an amount is too big to read: the page would still draw it, so the chips could no longer be matched one to one.
    /// </summary>
    public static IReadOnlyList<PreviewPart>? Parse(string? raw)
    {
        var parts = new List<PreviewPart>();

        if (string.IsNullOrEmpty(raw)) return parts;

        foreach (var piece in raw.Split(';'))
        {
            var match = Part.Match(piece.Trim());

            if (!match.Success) continue;

            if (!int.TryParse(match.Groups[3].Value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var amount)) return null;

            if (amount == 0) continue;

            parts.Add(new PreviewPart(match.Groups[1].Success, StatNamed(match.Groups[2].Value), amount));
        }

        return parts;
    }

    /// <summary>
    /// The text the page itself puts in the chip: the number for a cap, otherwise one to three + or minus marks by size.
    /// The page script only rewrites a chip that still shows this, so a page the game reshaped is left alone.
    /// </summary>
    public static string VanillaText(PreviewPart part)
    {
        var sign = part.IsGain ? '+' : MinusSign;
        var size = Math.Abs((long)part.Amount);

        if (part.IsCap) return sign + size.ToString(CultureInfo.InvariantCulture);

        var marks = size <= OneMarkUpTo ? 1 : size <= TwoMarksUpTo ? 2 : 3;

        return new string(sign, marks);
    }

    private static Stat StatNamed(string name) => name switch
    {
        "Satiety" => Stat.Satiety,
        "Morale" => Stat.Morale,
        "Stamina" => Stat.Stamina,
        "Vitality" => Stat.Vitality,
        _ => throw new ArgumentOutOfRangeException(nameof(name), name, "the pattern only matches the four stats"),
    };
}
