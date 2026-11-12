using System.Collections.Generic;
using System.Globalization;

namespace ExactNumbers.Core;

/// <summary>One piece of text a player sees, in both languages the game offers. A {0} in it is filled with a number.</summary>
public sealed record Phrase(string En, string Zh)
{
    public string Pick(bool isChinese) => isChinese ? Zh : En;

    public string Pick(bool isChinese, string value) => string.Format(CultureInfo.InvariantCulture, Pick(isChinese), value);
}

/// <summary>
/// Every text the mod puts on screen: what joins and ends the numbers in a chip, and the chip's hover text. The pieces
/// of a hover text are joined in this order, so each one carries its own leading punctuation. Log lines stay English,
/// so a bug report reads the same whatever the player's language.
/// </summary>
public static class Texts
{
    public static readonly Phrase RangeJoin = new(" to ", " 至 ");

    public static readonly Phrase PerHourSuffix = new("/h", "/小时");

    public static readonly Phrase EstimateTitle = new(
        "~ means this is the game's estimate.",
        "~ 表示这是游戏的预估值。");

    public static readonly Phrase FillToNote = new(
        " This action tops the stat up to a set level, so how much it adds depends on where the stat starts.",
        "此动作会把该属性补到一定数值，实际增加多少取决于开始时的数值。");

    public static readonly Phrase ListedBeforeModifiers = new(" Before your modifiers, the game lists {0}.", "计入角色倍率前，游戏列出的是 {0}。");

    public static readonly Phrase MeasuredOverOneUse = new("Measured over 1 use", "根据 1 次使用实测");

    public static readonly Phrase MeasuredOverUses = new("Measured over {0} uses", "根据 {0} 次使用实测");

    public static readonly Phrase PerHourNote = new(", shown per in-game hour.", "，按游戏内每小时计算。");

    public static readonly Phrase TotalPerUseNote = new(", shown as the total for one use.", "，为每次使用的总量。");

    public static readonly Phrase BeforeModifiers = new(" Before your modifiers, it is {0}.", "计入角色倍率前为 {0}。");

    /// <summary>Declared last: static fields are set in order, so every phrase above is made by the time this list is.</summary>
    public static readonly IReadOnlyList<Phrase> All = new[]
    {
        RangeJoin,
        PerHourSuffix,
        EstimateTitle,
        FillToNote,
        ListedBeforeModifiers,
        MeasuredOverOneUse,
        MeasuredOverUses,
        PerHourNote,
        TotalPerUseNote,
        BeforeModifiers,
    };
}
