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

/// <summary>What the configs say about one furniture for the crafting table: whether it was found, its slot type, and the function Choose picked.</summary>
public readonly record struct BriefChoice(bool IsFound, int SlotType, FurnitureFunc? Func);

/// <summary>
/// The crafting table asks for every furniture recipe's brief each time it lists them, and the answer only depends on
/// the config tables, so it is read once per furniture. Kept per table: a table the game built again (another native
/// object) drops everything kept. Nothing kept depends on the game's language: ids, weights, slot types and previews
/// such as "Satiety:+30".
/// </summary>
public sealed class BriefChoices
{
    /// <summary>Far above the furniture in the game; past it a choice is still made, only not kept.</summary>
    public const int MaxKept = 4096;

    private readonly Dictionary<int, BriefChoice> _choices = new();
    private long _table;

    /// <param name="table">The identity of the config table read from, such as its native pointer.</param>
    public bool TryGet(long table, int furnitureId, out BriefChoice choice)
    {
        if (table != _table)
        {
            _choices.Clear();
            _table = table;
        }

        return _choices.TryGetValue(furnitureId, out choice);
    }

    /// <summary>Keeps a choice read from the table last passed to TryGet.</summary>
    public void Put(int furnitureId, BriefChoice choice)
    {
        if (_choices.Count < MaxKept) _choices[furnitureId] = choice;
    }
}
