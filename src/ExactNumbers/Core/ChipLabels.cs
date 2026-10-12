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

    /// <summary>Added to the estimate's hover text when measuring found the function fills a stat up to a limit.</summary>
    public const string FillToNote = "; this tops the stat up to a limit, so what it adds depends on where the stat starts and no single number is exact";

    public const string RangeJoin = " to ";
    public const string PerHourSuffix = "/h";

    /// <param name="site">The furniture function the chips belong to, when known.</param>
    /// <param name="measured">What was measured; a chip with a usable measurement shows it instead of the estimate.</param>
    /// <returns>One label per chip the page draws for this preview, in page order; null when the preview cannot be matched.</returns>
    public static IReadOnlyList<ChipLabel>? For(string? raw, Modifiers modifiers, MeasureSite? site = null, MeasureStore? measured = null)
    {
        var parts = PreviewText.Parse(raw);

        return parts?.Select(part => Label(part, modifiers, RecordFor(part, site, measured))).ToList();
    }

    public static string KindName(ChipKind kind) => kind switch
    {
        ChipKind.Gain => "gain",
        ChipKind.Cost => "cost",
        ChipKind.Cap => "cap",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "unknown chip kind"),
    };

    /// <summary>The text that says, in the hover, how many uses a measured number comes from.</summary>
    public static string MeasuredTitle(MeasureRecord record)
    {
        var uses = record.Count == 1 ? "measured over 1 use" : "measured over " + record.Count + " uses";

        return uses + (record.Kind == MeasureKind.PerHour ? ", per in-game hour" : ", total per use");
    }

    private static MeasureRecord? RecordFor(PreviewPart part, MeasureSite? site, MeasureStore? measured)
    {
        if (part.IsCap || site is not { IsKnown: true } known || measured == null) return null;

        return measured.Find(new MeasureKey(known, part.Stat));
    }

    private static ChipLabel Label(PreviewPart part, Modifiers modifiers, MeasureRecord? record)
    {
        var vanilla = PreviewText.VanillaText(part);

        if (part.IsCap) return new ChipLabel(ChipKind.Cap, vanilla, null, null);

        var kind = part.IsGain ? ChipKind.Gain : ChipKind.Cost;
        var ratio = modifiers.RatioFor(part.Stat, part.IsGain);

        if (record != null && IsShowable(record, part)) return Measured(kind, vanilla, record, ratio);

        var estimate = Estimate(kind, vanilla, part, ratio);

        return record is { IsFillTo: true } ? estimate with { Title = estimate.Title + FillToNote } : estimate;
    }

    /// <summary>
    /// A fill-to function has no single amount, so it keeps the estimate. So does a measurement whose direction disagrees
    /// with the chip's: the page drew that chip as a gain or a cost, and the number in it must not say the opposite.
    /// </summary>
    private static bool IsShowable(MeasureRecord record, PreviewPart part) =>
        !record.IsFillTo && Math.Sign(record.Low) == Math.Sign(part.Amount) && Math.Sign(record.High) == Math.Sign(part.Amount);

    private static ChipLabel Estimate(ChipKind kind, string vanilla, PreviewPart part, float? ratio)
    {
        var listed = AmountText.Format(part.Amount);

        if (ratio is not { } known) return new ChipLabel(kind, vanilla, EstimateMark + listed, EstimateTitle);

        var scaled = AmountText.Format(CostRatio.Apply(known, part.Amount));
        var title = scaled == listed ? EstimateTitle : EstimateTitle + "; listed " + listed + " before your modifiers";

        return new ChipLabel(kind, vanilla, EstimateMark + scaled, title);
    }

    /// <summary>
    /// A measured number is exact, so it carries no estimate mark. It is stored before the character's ratio and scaled by
    /// the ratio the character has now, the way the game scales every change it makes.
    /// </summary>
    private static ChipLabel Measured(ChipKind kind, string vanilla, MeasureRecord record, float? ratio)
    {
        var suffix = record.Kind == MeasureKind.PerHour ? PerHourSuffix : "";
        var unscaled = Amounts(record, 0f) + suffix;
        var shown = ratio is { } known ? Amounts(record, known) + suffix : unscaled;
        var title = MeasuredTitle(record) + (shown == unscaled ? "" : "; " + unscaled + " before your modifiers");

        return new ChipLabel(kind, vanilla, shown, title);
    }

    /// <summary>One number for a fixed amount; "smaller to larger" for an amount that varied between uses.</summary>
    private static string Amounts(MeasureRecord record, float ratio)
    {
        if (!record.Varies) return AmountText.Format(CostRatio.Apply(ratio, (record.Low + record.High) / 2));

        var low = CostRatio.Apply(ratio, record.Low);
        var high = CostRatio.Apply(ratio, record.High);
        var (near, far) = Math.Abs(low) <= Math.Abs(high) ? (low, high) : (high, low);

        return AmountText.Format(near) + RangeJoin + AmountText.Format(far);
    }
}
