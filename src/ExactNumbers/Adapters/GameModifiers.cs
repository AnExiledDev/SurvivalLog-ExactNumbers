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

    private readonly Action<string> _log;
    private bool _hasReportedFailure;

    public GameModifiers(Action<string> log)
    {
        _log = log;
    }

    /// <returns>Modifiers.None when there is no character yet (main menu, loading) or the read failed.</returns>
    public Modifiers Read()
    {
        try
        {
            return TryRead() ?? Modifiers.None;
        }
        catch (Exception ex)
        {
            if (!_hasReportedFailure) _log("could not read the character's modifiers, showing base numbers (reported once): " + ex);

            _hasReportedFailure = true;

            return Modifiers.None;
        }
    }

    private static Modifiers? TryRead()
    {
        // AgentManager.GetLeadingRole (decomp GameCore.HotUpdate.Battle.Logic.AgentManager.c:4408), reached the way
        // sl-time-control does, and AgentTools.GetAgentComponent (AgentTools.c:53).
        var role = GameSingleton.Existing<BattleLogicWorld>()?._AgentManager?.GetLeadingRole();

        if (role == null) return null;

        var attributes = AgentTools.GetAgentComponent<AttributeComponent>(role)?.AttrDict;
        var gainRatios = AttributeComponent.AddCostValueDic;
        var costRatios = AttributeComponent.DelCostValueDic;

        if (attributes == null || gainRatios == null || costRatios == null) return null;

        var scaling = GameKey.Attr_ScalingRatio;
        var ratios = new Dictionary<RatioKey, float>();

        foreach (var (stat, name) in Stats)
        {
            // CostAttrValue picks DelCostValueDic for a decrease and AddCostValueDic otherwise (AttributeComponent.c:591-607).
            AddRatio(ratios, new RatioKey(stat, true), gainRatios, attributes, name, scaling);
            AddRatio(ratios, new RatioKey(stat, false), costRatios, attributes, name, scaling);
        }

        return new Modifiers(ratios);
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
