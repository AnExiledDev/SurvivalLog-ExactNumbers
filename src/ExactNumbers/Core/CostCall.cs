using System;

namespace ExactNumbers.Core;

/// <summary>
/// The change event AttributeComponent.CostAttrValue raises (BTEffectManager.TriggerAttrChange): Requested is the
/// amount after the character's ratio, Before and After the stat on either side of it.
/// </summary>
public readonly record struct StatChange(float Requested, float Before, float After)
{
    /// <summary>What the stat really moved by; less than Requested when it stopped at zero or at its maximum.</summary>
    public float Applied => After - Before;
}

/// <summary>
/// One call of AttributeComponent.CostAttrValue on the player's character, as the hooks saw it. Value is the amount the
/// game asked for, before the character's ratio. Stat is null for an attribute the tooltips draw no chip for. Change is
/// null when the call raised no change event. Max is the stat's maximum, 0 when unknown. ActionId and FuncId are the
/// character's current action and the furniture function it matched, 0 when there is none. GameSeconds is the in-game
/// clock, -1 when unknown.
/// </summary>
public sealed record CostCall(
    string Attr,
    Stat? Stat,
    float Value,
    long Target,
    string ArgsType,
    bool IsAction,
    bool IsDependency,
    string Source,
    bool IsFurniture,
    int ConfigId,
    StatChange? Change,
    float Max,
    int ActionId,
    int FuncId,
    int GameSeconds)
{
    /// <summary>Where the game's fill-to rule (AE_AddCostAttr_Float.c:58-64) aims: the stat before the call plus the asked amount.</summary>
    public float? Reach => Change is { } change ? change.Before + Value : null;

    public bool IsAtMax => Change is { } change && Max > 0 && change.After >= Max - TickRule.Tolerance;
}

/// <summary>Why a cost call did or did not count toward a measurement. The trace log prints the name.</summary>
public enum TickVerdict
{
    Kept,
    NotChipStat,
    NotAction,
    NotFurniture,
    Dependency,
    NoChange,
    Zero,
    Clamped,
    NoUse,
    NoFunc,
}

/// <summary>
/// Which cost calls measure a furniture function. Only an action's own effects count (natural recovery, buffs and items
/// do not), only those the game sourced to a piece of furniture (its config id is then the furniture's,
/// EffectBtArgsExtension.DetermineSourceType), and not the follow-on changes one stat makes to another. A call the stat
/// could not take in full (stopped at zero or at its maximum) says nothing about the action's amount, so it spoils
/// that stat's measurement for the use instead of counting.
/// </summary>
public static class TickRule
{
    public const float Tolerance = 0.001f;

    public static TickVerdict Judge(CostCall call)
    {
        if (call.Stat is null) return TickVerdict.NotChipStat;
        if (!call.IsAction) return TickVerdict.NotAction;
        if (!call.IsFurniture || call.ConfigId <= 0) return TickVerdict.NotFurniture;
        if (call.IsDependency) return TickVerdict.Dependency;
        if (call.Change is not { } change) return TickVerdict.NoChange;
        if (call.Value == 0) return TickVerdict.Zero;

        return IsClamped(change) ? TickVerdict.Clamped : TickVerdict.Kept;
    }

    public static bool IsClamped(StatChange change) => Math.Abs(change.Applied - change.Requested) > Tolerance;
}
