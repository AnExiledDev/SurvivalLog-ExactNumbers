using System;
using System.Collections.Generic;
using System.Threading;
using ExactNumbers.Core;
using GameCore.HotUpdate;
using GameCore.HotUpdate.Battle.Logic;
using GameCore.HotUpdate.ReduxUI;
using HarmonyLib;

namespace ExactNumbers.Adapters;

/// <summary>
/// Watches the player's character use furniture, to measure what each function really does. Every stat change goes
/// through AttributeComponent.CostAttrValue (8-argument overload, AttributeComponent.c:512), which raises
/// BTEffectManager.TriggerAttrChange (BTEffectManager.c:1192, called at AttributeComponent.c:829-838) with the asked
/// amount after the character's ratio and the stat before and after. A use starts at ActionComponent.PlayAction
/// (ActionComponent.c:709; the game sets the character's current action before calling it, ActionManager.c:10049 and
/// 10143) and ends at ActionComponent.RemoveActionCall (ActionComponent.c:1201, called from BTEffectManager.DestroyBtArgs,
/// BTEffectManager.c:481), whose ActionManager.RemoveAction gives the end-of-use reward unless the use was broken off
/// (ActionManager.c:1366, 6678). Crafting recipes get their function from Reducer_Web_ToolTable.FillFurnitureBrief
/// (Reducer_Web_ToolTable.c:4198). Only reads; every hook checks the character first so other agents cost one compare.
/// Any failure is logged once and the hooks go dormant: a stat change must never break because of this mod. Static
/// because Harmony patches are.
/// </summary>
internal static class MeasureHooks
{
    /// <summary>Held around the measurer and the recipe sites by the hooks and by the session.</summary>
    public static readonly object Gate = new();

    /// <summary>Nested cost calls are a handful deep; frames past this are left over from a call that threw, and are dropped.</summary>
    private const int MaxDepth = 16;

    private static readonly List<Frame> Frames = new();
    private static Measurer? _measurer;
    private static RecipeSites? _recipes;
    private static Action<string> _log = _ => { };
    private static Action<string> _trace = _ => { };
    private static long _heroId = -1;
    private static int _isDormant;
    private static int _isBriefDormant;

    /// <summary>One cost call in progress: the change event it raised, once it has.</summary>
    private sealed class Frame
    {
        public Frame(AttrName attr)
        {
            Attr = attr;
        }

        public AttrName Attr { get; }

        public StatChange? Change { get; set; }
    }

    /// <summary>The player's character, set once a frame by the session; -1 while there is none.</summary>
    public static void SetHero(long id) => Interlocked.Exchange(ref _heroId, id);

    /// <returns>False when the stat hooks could not all be applied; nothing is measured then.</returns>
    public static bool Apply(Harmony harmony, Measurer measurer, RecipeSites recipes, Action<string> log, Action<string> trace)
    {
        _measurer = measurer;
        _recipes = recipes;
        _log = log;
        _trace = trace;

        try
        {
            Patch(harmony, typeof(AttributeComponent), nameof(AttributeComponent.CostAttrValue),
                new[] { typeof(AttrName), typeof(float), typeof(long), typeof(EffectArgsType), typeof(bool), typeof(bool), typeof(VitalityChangeSourceType), typeof(int) },
                prefix: nameof(BeforeCost), postfix: nameof(AfterCost));
            Patch(harmony, typeof(BTEffectManager), nameof(BTEffectManager.TriggerAttrChange),
                new[] { typeof(long), typeof(AttrName), typeof(float), typeof(float), typeof(float), typeof(long) },
                prefix: nameof(SeeChange));
            Patch(harmony, typeof(ActionComponent), nameof(ActionComponent.PlayAction), null, prefix: nameof(BeforePlay));
            Patch(harmony, typeof(ActionComponent), nameof(ActionComponent.RemoveActionCall), null, prefix: nameof(BeforeRemove), postfix: nameof(AfterRemove));
            log("measuring furniture functions");
        }
        catch (Exception ex)
        {
            Interlocked.Exchange(ref _isDormant, 1);
            log("could not hook stat changes, nothing is measured and the game's estimates are shown: " + ex.Message);

            return false;
        }

        try
        {
            Patch(harmony, typeof(Reducer_Web_ToolTable), nameof(Reducer_Web_ToolTable.FillFurnitureBrief), null, postfix: nameof(AfterBrief));
        }
        catch (Exception ex)
        {
            log("could not hook the crafting table's furniture previews, crafting chips keep the game's estimates: " + ex.Message);
        }

        return true;
    }

    private static void Patch(Harmony harmony, Type type, string name, Type[]? arguments, string? prefix = null, string? postfix = null)
    {
        var target = (arguments == null ? AccessTools.Method(type, name) : AccessTools.Method(type, name, arguments))
                     ?? throw new MissingMethodException(type.Name + "." + name);

        harmony.Patch(
            target,
            prefix: prefix == null ? null : new HarmonyMethod(typeof(MeasureHooks), prefix),
            postfix: postfix == null ? null : new HarmonyMethod(typeof(MeasureHooks), postfix));
    }

    private static bool IsDormant => Volatile.Read(ref _isDormant) != 0;

    private static bool IsHero(long id) => id == Interlocked.Read(ref _heroId);

    private static void BeforeCost(AttributeComponent __instance, AttrName attrName, out Frame? __state)
    {
        __state = null;

        if (IsDormant) return;

        try
        {
            if (!IsHero(__instance.ownerInstanceId)) return;

            var frame = new Frame(attrName);

            lock (Gate)
            {
                if (Frames.Count >= MaxDepth) Frames.Clear();

                Frames.Add(frame);
            }

            __state = frame;
        }
        catch (Exception ex)
        {
            Fail("before a stat change", ex);
        }
    }

    private static void AfterCost(
        AttributeComponent __instance,
        AttrName attrName,
        float value,
        long targetInstanceId,
        EffectArgsType argsType,
        bool isDependency,
        VitalityChangeSourceType sourceType,
        int configId,
        Frame? __state)
    {
        if (__state == null) return;

        try
        {
            lock (Gate) Frames.Remove(__state);

            if (IsDormant || _measurer == null) return;

            var action = CurrentAction();
            var call = new CostCall(
                attrName.ToString(),
                GameModifiers.StatOf(attrName),
                value,
                targetInstanceId,
                argsType.ToString(),
                argsType == EffectArgsType.Action,
                isDependency,
                sourceType.ToString(),
                sourceType == VitalityChangeSourceType.Furniture,
                configId,
                __state.Change,
                MaxOf(__instance, attrName),
                action?.ActionId ?? 0,
                action?.MatchedFuncId ?? 0,
                GameSeconds());

            lock (Gate) _measurer.Cost(call);
        }
        catch (Exception ex)
        {
            Fail("after a stat change", ex);
        }
    }

    /// <summary>Fills in the innermost open cost call for the same stat: CostAttrValue raises this once, after clamping.</summary>
    private static void SeeChange(long ownerId, AttrName attrName, float addValue, float curValue, float resultValue)
    {
        if (IsDormant || !IsHero(ownerId)) return;

        try
        {
            lock (Gate)
            {
                for (var i = Frames.Count - 1; i >= 0; i--)
                {
                    if (Frames[i].Attr != attrName || Frames[i].Change != null) continue;

                    Frames[i].Change = new StatChange(addValue, curValue, resultValue);

                    break;
                }
            }
        }
        catch (Exception ex)
        {
            Fail("reading a stat change", ex);
        }
    }

    /// <summary>
    /// A prefix, so the use is open before the action's first effects: PlayAction adds them itself (BTEffectManager.AddBtArgs,
    /// ActionComponent.c:1050). PlayAction scales During by positive factors only (ActionComponent.c:1042-1045), so a
    /// negative During in the config is an action that runs until stopped.
    /// </summary>
    private static void BeforePlay(ActionComponent __instance, long targetId, Config_Action config)
    {
        if (IsDormant || _measurer == null) return;

        try
        {
            if (!IsHero(__instance.ownerInstanceId) || config == null) return;

            var action = CurrentAction();

            _trace($"play config={config.ID} during={config.During} current action={action?.ActionId ?? 0} matched func={action?.MatchedFuncId ?? 0} target={targetId}");

            var use = new UseStart(config.ID, targetId, action?.MatchedFuncId ?? 0, config.During < 0, GameSeconds());

            lock (Gate) _measurer.Play(use);
        }
        catch (Exception ex)
        {
            Fail("at the start of an action", ex);
        }
    }

    /// <summary>The action the component is removing, read before RemoveAction runs.</summary>
    private static void BeforeRemove(ActionComponent __instance, out int __state)
    {
        __state = 0;

        if (IsDormant) return;

        try
        {
            if (IsHero(__instance.ownerInstanceId)) __state = __instance.ActionConfig?.ID ?? -1;
        }
        catch (Exception ex)
        {
            Fail("at the end of an action", ex);
        }
    }

    /// <summary>A postfix, so the end-of-use reward RemoveAction hands out is part of the use.</summary>
    private static void AfterRemove(long btArgsId, bool forceBroken, int __state)
    {
        if (__state == 0 || IsDormant || _measurer == null) return;

        try
        {
            _trace($"remove action={__state} args={btArgsId} broken={forceBroken}");

            lock (Gate) _measurer.End(__state, forceBroken, GameSeconds());
        }
        catch (Exception ex)
        {
            Fail("at the end of an action", ex);
        }
    }

    /// <summary>
    /// Notes which furniture function a furniture recipe's product chips come from, picked the way the game picks it
    /// (BriefFunc), and only when the game's own pick shows the same preview.
    /// </summary>
    private static void AfterBrief(Config_Item productConfig, Data_Web_ToolTable_Recipe recipeData)
    {
        if (Volatile.Read(ref _isBriefDormant) != 0 || _recipes == null) return;

        try
        {
            var recipeKey = recipeData?.RecipeKey?.Value;
            var preview = recipeData?.ProductAttrDelta?.Value ?? "";
            var furnitureId = productConfig?.TargetFurnitureID ?? 0;

            // FillFurnitureBrief returns at once for an item that is not furniture (Reducer_Web_ToolTable.c:4308).
            if (string.IsNullOrEmpty(recipeKey) || furnitureId <= 0) return;

            var configs = GameSingleton.Existing<ConfigManager>();
            var furniture = configs?.Get_Config_Furniture(furnitureId);
            var chosen = furniture == null || configs == null ? null : BriefFunc.Choose(furniture.SlotType, FuncsOf(configs, furniture));
            var isMapped = chosen is { } func && preview.Length > 0 && func.Preview == preview;

            lock (Gate)
            {
                if (isMapped && chosen is { } mapped) _recipes.Note(recipeKey, new RecipeSite(new MeasureSite(furnitureId, mapped.Id), preview));
                else _recipes.Forget(recipeKey);
            }

            var outcome = isMapped ? "mapped" : chosen == null ? "none" : "mismatch, game shows " + preview;

            _trace($"recipe key={recipeKey} item={productConfig?.ID ?? 0} furniture={furnitureId} slot={furniture?.SlotType ?? 0} func={chosen?.Id ?? 0} preview={chosen?.Preview ?? ""} -> {outcome}");
        }
        catch (Exception ex)
        {
            if (Interlocked.Exchange(ref _isBriefDormant, 1) == 0) _log("crafting previews stopped after an error, crafting chips keep the game's estimates (reported once): " + ex);
        }
    }

    /// <summary>ConfigManager.Get_Config_FurnitureFunc (ConfigManager.c:12696) for each of the furniture's functions, in order.</summary>
    private static List<FurnitureFunc?> FuncsOf(ConfigManager configs, Config_Furniture furniture)
    {
        var funcs = new List<FurnitureFunc?>();
        var ids = furniture.FurnitureFunc;

        if (ids == null) return funcs;

        for (var i = 0; i < ids.Count; i++)
        {
            var func = configs.Get_Config_FurnitureFunc(ids[i]);

            funcs.Add(func == null ? null : new FurnitureFunc(func.ID, func.RecommendWeight, func.PreviewAttrDelta));
        }

        return funcs;
    }

    /// <summary>ActionManager.GetActionSource (ActionManager.c:3116) and AgentActionSource.CurrentAction (AgentActionSource.c:15).</summary>
    private static AgentAction? CurrentAction()
    {
        var actions = GameSingleton.Existing<BattleLogicWorld>()?._ActionManager;

        return actions?.GetActionSource(Interlocked.Read(ref _heroId))?.CurrentAction;
    }

    /// <summary>GameTimeManager.GetTotalSeconds (GameTimeManager.c:735), the in-game clock; -1 when there is none.</summary>
    private static int GameSeconds() => GameSingleton.Existing<BattleLogicWorld>()?._GameTimeManager?.GetTotalSeconds() ?? -1;

    /// <summary>
    /// The stat's maximum the way CostAttrValue clamps to it: AttributeComponent.MaxAttrValueDic names the maximum's
    /// attribute and GetTotalValue_Int (AttributeComponent.c:1533) reads it. 0 when the stat has none.
    /// </summary>
    private static float MaxOf(AttributeComponent attributes, AttrName attrName)
    {
        var maxNames = AttributeComponent.MaxAttrValueDic;

        return maxNames != null && maxNames.TryGetValue(attrName, out var maxName) ? attributes.GetTotalValue_Int(maxName) : 0f;
    }

    private static void Fail(string where, Exception ex)
    {
        if (Interlocked.Exchange(ref _isDormant, 1) != 0) return;

        lock (Gate) Frames.Clear();

        _log($"measuring stopped after an error {where}; the game's estimates are shown from now on (reported once): {ex}");
    }
}
