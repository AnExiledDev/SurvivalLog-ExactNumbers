using System;
using System.Collections.Generic;
using ExactNumbers.Core;
using GameCore.HotUpdate;
using GameCore.HotUpdate.Battle.Logic;

namespace ExactNumbers.Adapters;

/// <summary>
/// Reads the ratios the game applies to every satiety, mood, energy and life change of the player's character
/// (AttributeComponent.CostAttrValue). It only reads. Anything missing leaves that ratio out, so its chips show the base.
/// </summary>
internal sealed class GameModifiers
{
    private static readonly (Stat Stat, AttrName Name)[] Stats =
    {
        (Stat.Satiety, AttrName.Satiety),
        (Stat.Morale, AttrName.Morale),
        (Stat.Stamina, AttrName.Stamina),
        (Stat.Vitality, AttrName.Vitality),
    };

    /// <returns>The stat a tooltip chip shows for this attribute, or null for one no chip shows.</returns>
    public static Stat? StatOf(AttrName name)
    {
        foreach (var (stat, statName) in Stats)
        {
            if (statName == name) return stat;
        }

        return null;
    }

    private readonly Action<string> _log;

    // Read into every second and kept, so a read that finds the same ratios allocates nothing.
    private readonly Dictionary<RatioKey, float> _read = new();
    private bool _hasReportedFailure;

    public GameModifiers(Action<string> log)
    {
        _log = log;
    }

    /// <returns>
    /// current itself when the ratios read are exactly its own; Modifiers.None when there is no character yet (main
    /// menu, loading) or the read failed.
    /// </returns>
    public Modifiers Read(Modifiers current)
    {
        try
        {
            _read.Clear();

            if (!TryRead(_read)) return Modifiers.None;

            return current.Holds(_read) ? current : new Modifiers(_read);
        }
        catch (Exception ex)
        {
            if (!_hasReportedFailure) _log("could not read the character's modifiers, showing base numbers (reported once): " + ex);

            _hasReportedFailure = true;

            return Modifiers.None;
        }
    }

    /// <returns>False when there is no character to read.</returns>
    private static bool TryRead(Dictionary<RatioKey, float> ratios)
    {
        // AgentManager.GetLeadingRole (decomp GameCore.HotUpdate.Battle.Logic.AgentManager.c:4408), reached the way
        // sl-time-control does, and AgentTools.GetAgentComponent (AgentTools.c:53).
        var role = GameSingleton.Existing<BattleLogicWorld>()?._AgentManager?.GetLeadingRole();

        if (role == null) return false;

        var attributes = AgentTools.GetAgentComponent<AttributeComponent>(role)?.AttrDict;
        var gainRatios = AttributeComponent.AddCostValueDic;
        var costRatios = AttributeComponent.DelCostValueDic;

        if (attributes == null || gainRatios == null || costRatios == null) return false;

        var scaling = GameKey.Attr_ScalingRatio;

        foreach (var (stat, name) in Stats)
        {
            // CostAttrValue picks DelCostValueDic for a decrease and AddCostValueDic otherwise (AttributeComponent.c:591-607).
            AddRatio(ratios, new RatioKey(stat, true), gainRatios, attributes, name, scaling);
            AddRatio(ratios, new RatioKey(stat, false), costRatios, attributes, name, scaling);
        }

        return true;
    }

    private static void AddRatio(
        Dictionary<RatioKey, float> ratios,
        RatioKey key,
        Il2CppSystem.Collections.Generic.Dictionary<AttrName, AttrName> ratioNames,
        Il2CppSystem.Collections.Generic.Dictionary<AttrName, Attr> attributes,
        AttrName name,
        float scaling)
    {
        if (!ratioNames.TryGetValue(name, out var ratioName)) return;

        if (!attributes.TryGetValue(ratioName, out var attr) || attr == null) return;

        // The ratio formula is AttributeComponent.c:664-683, mirrored in CostRatio.
        var ratio = CostRatio.Of(new RatioStat(attr.BaseValue, attr.StrengtheningValue, attr.Min, attr.Max, attr.isSingle), scaling);

        if (ratio is { } known) ratios[key] = known;
    }
}
