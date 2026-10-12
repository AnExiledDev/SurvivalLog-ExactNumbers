using System;
using System.Collections.Generic;
using System.Linq;

namespace ExactNumbers.Core;

/// <summary>The page's chip classes: a stat going up, a stat going down, or a change to a stat's maximum.</summary>
public enum ChipKind
{
    Gain,
    Cost,
    Cap,
}

/// <summary>
/// What one chip should say. Vanilla is what the page drew there, so the page script can check it is looking at the
/// chip this label was made for. Shown is the replacement, or null to leave the chip as it is (caps already show their
/// number). Title is the chip's hover text, or null for none.
/// </summary>
public sealed record ChipLabel(ChipKind Kind, string Vanilla, string? Shown, string? Title);

public static class ChipLabels
{
    /// <summary>
    /// Put in front of every number made from the game's preview line. The preview is designer text, not what the action
    /// applies, so a number made from it is an estimate and is never shown as if it were exact.
    /// </summary>
    public const string EstimateMark = "~";

    public const string EstimateTitle = "~ = the game's listed estimate, not the exact amount";

    /// <returns>One label per chip the page draws for this preview, in page order; null when the preview cannot be matched.</returns>
    public static IReadOnlyList<ChipLabel>? For(string? raw, Modifiers modifiers)
    {
        var parts = PreviewText.Parse(raw);

        return parts?.Select(part => Label(part, modifiers)).ToList();
    }

    public static string KindName(ChipKind kind) => kind switch
    {
        ChipKind.Gain => "gain",
        ChipKind.Cost => "cost",
        ChipKind.Cap => "cap",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "unknown chip kind"),
    };

    private static ChipLabel Label(PreviewPart part, Modifiers modifiers)
    {
        var vanilla = PreviewText.VanillaText(part);

        if (part.IsCap) return new ChipLabel(ChipKind.Cap, vanilla, null, null);

        var kind = part.IsGain ? ChipKind.Gain : ChipKind.Cost;
        var listed = AmountText.Format(part.Amount);
        var ratio = modifiers.RatioFor(part.Stat, part.IsGain);

        if (ratio is not { } known) return new ChipLabel(kind, vanilla, EstimateMark + listed, EstimateTitle);

        var scaled = AmountText.Format(CostRatio.Apply(known, part.Amount));
        var title = scaled == listed ? EstimateTitle : EstimateTitle + "; listed " + listed + " before your modifiers";

        return new ChipLabel(kind, vanilla, EstimateMark + scaled, title);
    }
}
