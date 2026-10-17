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
        "~ = the game's listed estimate, not the exact amount",
        "~ 表示游戏列出的预估值，并非确切数值");

    public static readonly Phrase FillToNote = new(
        "; this tops the stat up to a limit, so what it adds depends on where the stat starts and no single number is exact",
        "；此动作会把该属性补到一定数值，实际增加多少取决于开始时的数值，没有一个固定的确切数字");

    public static readonly Phrase ListedBeforeModifiers = new("; listed {0} before your modifiers", "；计入角色倍率前，游戏列出的是 {0}");

    public static readonly Phrase MeasuredOverOneUse = new("measured over 1 use", "根据 1 次使用实测");

    public static readonly Phrase MeasuredOverUses = new("measured over {0} uses", "根据 {0} 次使用实测");

    public static readonly Phrase PerHourNote = new(", per in-game hour", "，按游戏内每小时计算");

    public static readonly Phrase TotalPerUseNote = new(", total per use", "，为每次使用的总量");

    public static readonly Phrase BeforeModifiers = new("; {0} before your modifiers", "；计入角色倍率前为 {0}");

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
