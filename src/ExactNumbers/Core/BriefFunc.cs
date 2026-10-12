using System;
using System.Collections.Generic;

namespace ExactNumbers.Core;

/// <summary>One of a furniture's functions as the crafting table reads it: its id, its RecommendWeight and its preview text.</summary>
public readonly record struct FurnitureFunc(int Id, int RecommendWeight, string? Preview);

/// <summary>
/// Which function's preview the crafting table shows for a furniture product, the same way the game picks it
/// (Reducer_Web_ToolTable.FillFurnitureBrief, Reducer_Web_ToolTable.c:4601-4640): none for slot types 8, 9 and 10; else,
/// in list order, the function whose preview holds a gain (":+") with the strictly highest RecommendWeight, so the
/// first wins a tie. A missing function is skipped. It is not simply the first function with a preview.
/// </summary>
public static class BriefFunc
{
    private static readonly HashSet<int> NoBriefSlots = new() { 8, 9, 10 };

    /// <param name="funcs">The furniture's FurnitureFunc list in order; null for an id the configs do not have.</param>
    public static FurnitureFunc? Choose(int slotType, IEnumerable<FurnitureFunc?> funcs)
    {
        if (NoBriefSlots.Contains(slotType)) return null;

        FurnitureFunc? best = null;

        foreach (var candidate in funcs)
        {
            if (candidate is not { } func || string.IsNullOrEmpty(func.Preview)) continue;
            if (func.Preview.IndexOf(":+", StringComparison.Ordinal) < 0) continue;

            if (best is not { } current || func.RecommendWeight > current.RecommendWeight) best = func;
        }

        return best;
    }
}
