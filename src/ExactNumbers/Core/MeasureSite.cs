using System;
using System.Globalization;

namespace ExactNumbers.Core;

/// <summary>
/// One function of one kind of furniture: what a measurement belongs to. Furniture is the furniture's config id and
/// Func its function's config id (the button the player picked). Both are positive when known.
/// </summary>
public readonly record struct MeasureSite(int Furniture, int Func)
{
    public bool IsKnown => Furniture > 0 && Func > 0;

    public override string ToString() => Furniture.ToString(CultureInfo.InvariantCulture) + "/" + Func.ToString(CultureInfo.InvariantCulture);
}

/// <summary>What one stat did when one furniture function was used.</summary>
public readonly record struct MeasureKey(MeasureSite Site, Stat Stat);

/// <summary>
/// What the page script asks numbers for. A chip whose furniture function is known is asked for as
/// "@furniture/func/preview", so its measured numbers can be told apart from another function's with the same preview;
/// any other chip is asked for by its plain preview, as in 0.1.0. The game's previews start with a stat name, never "@".
/// </summary>
public static class LabelKey
{
    private const char Mark = '@';
    private const char Separator = '/';

    public static string For(MeasureSite? site, string preview)
    {
        if (preview.Length == 0 || site is not { IsKnown: true } known) return preview;

        return Mark + known.ToString() + Separator + preview;
    }

    /// <returns>The site and the preview; no site for a plain preview or anything not written by For.</returns>
    public static (MeasureSite? Site, string Preview) Parse(string key)
    {
        var site = SiteOf(key, out var previewAt);

        return site == null ? (null, key) : (site, key.Substring(previewAt));
    }

    /// <summary>The site alone, as Parse reads it, without copying out the preview.</summary>
    public static MeasureSite? SiteOf(string key) => SiteOf(key, out _);

    private static MeasureSite? SiteOf(string key, out int previewAt)
    {
        previewAt = 0;

        if (key.Length == 0 || key[0] != Mark) return null;

        var first = key.IndexOf(Separator, 1);
        var second = first < 0 ? -1 : key.IndexOf(Separator, first + 1);

        if (second < 0) return null;

        if (!PositiveInt(key.AsSpan(1, first - 1), out var furniture) || !PositiveInt(key.AsSpan(first + 1, second - first - 1), out var func)) return null;

        previewAt = second + 1;

        return new MeasureSite(furniture, func);
    }

    private static bool PositiveInt(ReadOnlySpan<char> text, out int value) =>
        int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value) && value > 0;
}
