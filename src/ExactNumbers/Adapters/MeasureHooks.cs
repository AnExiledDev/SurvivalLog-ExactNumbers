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
/// BTEffectManager.c:481), whose ActionManager.RemoveAction (called at ActionComponent.c:1366) gives the end-of-use
/// reward through ActionManager.TryExecuteFurnitureFuncReward unless the use was broken off (ActionManager.c:6678). The
/// reward's stat changes look like no action's (RewardSetManager.c:993), so that call is hooked to mark them. An
/// effect that fills a stat up to a limit (AE_AddCostAttr_Float.Run) is hooked to mark its change as fill-to. Crafting
/// recipes get their function from Reducer_Web_ToolTable.FillFurnitureBrief (Reducer_Web_ToolTable.c:4198). Only
/// reads; every hook checks the character first so other agents cost one compare, and a stat change that cannot count
/// is dropped before anything is looked up, unless the trace is on. Any failure is logged once and the hooks go
/// dormant: a stat change must never break because of this mod. Static because Harmony patches are.
/// </summary>
internal static class MeasureHooks
{
    /// <summary>Held around the measurer and the recipe sites by the hooks and by the session.</summary>
    public static readonly object Gate = new();

    /// <summary>Nested cost calls are a handful deep; frames past this are left over from a call that threw, and are dropped.</summary>
    private const int MaxDepth = 16;

    private static readonly List<Frame> Frames = new();

    /// <summary>The crafting table's furniture function choices; only touched from the game's thread, in the brief hooks.</summary>
    private static readonly BriefChoices Briefs = new();
    private static Measurer? _measurer;
    private static RecipeSites? _recipes;
    private static Action<string> _log = _ => { };
    private static Action<string> _trace = _ => { };
    private static bool _isTracing;
    private static long _heroId = -1;
    private static int _isDormant;
    private static int _isBriefDormant;
    private static int _isRewardOpen;

    /// <summary>The stat an AE_AddCostAttr_Float.Run with a limit is about to change; null outside one.</summary>
    [ThreadStatic] private static AttrName? _fillingAttr;

    /// <summary>A furniture brief filled inside GetOrBuildRecipeStatic, waiting for the recipe key it returns; null outside one.</summary>
    [ThreadStatic] private static (Config_Item? Config, string Preview)? _pendingBrief;

    /// <summary>One cost call in progress: the change event it raised, once it has.</summary>
    private sealed class Frame
    {
        public Frame(AttrName attr, bool isFillTo)
        {
            Attr = attr;
            IsFillTo = isFillTo;
        }

        public AttrName Attr { get; }

        public bool IsFillTo { get; }

        public StatChange? Change { get; set; }
    }

    /// <summary>The action ActionComponent.RemoveActionCall is removing, and the button press it belongs to.</summary>
    private sealed class Removing
    {
        public Removing(int actionId, long press)
        {
            ActionId = actionId;
            Press = press;
        }

        public int ActionId { get; }

        public long Press { get; }
    }

    /// <summary>The player's character, set once a frame by the session; -1 while there is none.</summary>
    public static void SetHero(long id) => Interlocked.Exchange(ref _heroId, id);

    /// <param name="trace">Where trace lines go; null when the trace is off.</param>
    /// <returns>False when the stat hooks could not all be applied; nothing is measured then.</returns>
    public static bool Apply(Harmony harmony, Measurer measurer, RecipeSites recipes, Action<string> log, Action<string>? trace)
    {
        _measurer = measurer;
        _recipes = recipes;
        _log = log;
        _trace = trace ?? (_ => { });
        _isTracing = trace != null;

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
            Patch(harmony, typeof(ActionManager), nameof(ActionManager.TryExecuteFurnitureFuncReward), null, prefix: nameof(BeforeReward), postfix: nameof(AfterReward));
            Patch(harmony, typeof(AE_AddCostAttr_Float), nameof(AE_AddCostAttr_Float.Run), null, prefix: nameof(BeforeFill), postfix: nameof(AfterFill));
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

        // Builds that fill the brief on a blank row name it in GetOrBuildRecipeStatic; older builds have no such method.
        if (AccessTools.Method(typeof(Reducer_Web_ToolTable), "GetOrBuildRecipeStatic") != null)
        {
            try
            {
                Patch(harmony, typeof(Reducer_Web_ToolTable), "GetOrBuildRecipeStatic", null, prefix: nameof(BeforeStatic), postfix: nameof(AfterStatic));
            }
            catch (Exception ex)
            {
                log("could not hook the crafting table's recipe list, furniture recipes keep the game's estimates: " + ex.Message);
            }
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

            var isFillTo = _fillingAttr == attrName;

            // Only the fill effect's own change: a change it causes to another stat is not filled to anything.
            if (isFillTo) _fillingAttr = null;

            var frame = new Frame(attrName, isFillTo);

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

            var mayCount = MayCount(argsType, isDependency, sourceType);

            // Most changes of the character are its stats running down on their own; without a trace they end here.
            if (!mayCount && !_isTracing) return;

            // Looked up only for a change that can count; a traced one that cannot shows max 0, action 0 and time -1.
            var action = mayCount ? CurrentAction() : null;
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
                mayCount ? MaxOf(__instance, attrName) : 0f,
                action?.ActionId ?? 0,
                action?.MatchedFuncId ?? 0,
                mayCount ? GameSeconds() : -1,
                __state.IsFillTo);

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

            // AgentAction.ParentId is the press: ActionManager.Update starts only queued actions of the current one
            // (ActionManager.c:10033).
            var use = new UseStart(config.ID, targetId, action?.MatchedFuncId ?? 0, config.During < 0, GameSeconds(), action?.ParentId ?? 0);

            // A reward window never spans the start of an action; one left open by a reward that threw is shut here.
            Interlocked.Exchange(ref _isRewardOpen, 0);

            lock (Gate) _measurer.Play(use);
        }
        catch (Exception ex)
        {
            Fail("at the start of an action", ex);
        }
    }

    /// <summary>The action the component is removing and its press, read before RemoveAction runs.</summary>
    private static void BeforeRemove(ActionComponent __instance, out Removing? __state)
    {
        __state = null;

        if (IsDormant) return;

        try
        {
            if (IsHero(__instance.ownerInstanceId)) __state = new Removing(__instance.ActionConfig?.ID ?? -1, CurrentAction()?.ParentId ?? 0);
        }
        catch (Exception ex)
        {
            Fail("at the end of an action", ex);
        }
    }

    /// <summary>
    /// A postfix, so the end-of-use reward RemoveAction gives (BeforeReward) is already in the use, and the queue shows
    /// whether the press goes on with another action.
    /// </summary>
    private static void AfterRemove(long btArgsId, bool forceBroken, Removing? __state)
    {
        if (__state == null || IsDormant || _measurer == null) return;

        try
        {
            var pressGoesOn = !forceBroken && PressGoesOn(__state.Press);

            _trace($"remove action={__state.ActionId} args={btArgsId} broken={forceBroken} press={__state.Press} press goes on={pressGoesOn}");

            lock (Gate) _measurer.End(__state.ActionId, forceBroken, GameSeconds(), pressGoesOn);
        }
        catch (Exception ex)
        {
            Fail("at the end of an action", ex);
        }
    }

    /// <summary>
    /// ActionManager.TryExecuteFurnitureFuncReward (ActionManager.c:8207), which RemoveAction calls only for a use that
    /// was not broken off (ActionManager.c:6678). For an action with a matched function it gives that function's reward
    /// set (ActionManager.c:8270, 8296), and RewardSetManager.ExecuteAttrReward hands each stat reward to CostAttrValue
    /// with no args type, sourced to a dynamic event, config id 0 (RewardSetManager.c:993): nothing in the call says
    /// which furniture it came from, so the window carries it. Without a matched function the game picks the reward by
    /// the furniture's functions (ActionManager.c:8326-8344), which this does not repeat; those rewards are not counted.
    /// </summary>
    private static void BeforeReward(AgentAction action, long agentId, out bool __state)
    {
        __state = false;

        if (IsDormant || _measurer == null) return;

        try
        {
            if (action == null || !IsHero(agentId)) return;

            var site = new MeasureSite(FurnitureOf(action.TargetId), action.MatchedFuncId);

            lock (Gate) _measurer.OpenReward(action.ActionId, site);

            Interlocked.Exchange(ref _isRewardOpen, 1);
            __state = true;
        }
        catch (Exception ex)
        {
            Fail("before an end-of-use reward", ex);
        }
    }

    private static void AfterReward(bool __state)
    {
        if (!__state) return;

        Interlocked.Exchange(ref _isRewardOpen, 0);

        if (IsDormant || _measurer == null) return;

        try
        {
            lock (Gate) _measurer.CloseReward();
        }
        catch (Exception ex)
        {
            Fail("after an end-of-use reward", ex);
        }
    }

    /// <summary>
    /// AE_AddCostAttr_Float.Run (AE_AddCostAttr_Float.c:4). With a MaxValue (the constructor's -1 means none,
    /// AE_AddCostAttr_Float.c:111) it fills the stat up to that limit (AE_AddCostAttr_Float.c:52-59), asking only for
    /// what is left below it (AE_AddCostAttr_Float.c:71-79), then calls CostAttrValue on its target
    /// (AE_AddCostAttr_Float.c:91). What such a change adds depends on where the stat started, so it is marked.
    /// </summary>
    private static void BeforeFill(AE_AddCostAttr_Float __instance)
    {
        if (IsDormant) return;

        try
        {
            _fillingAttr = __instance.MaxValue != -1 ? __instance.NameAttr : null;
        }
        catch (Exception ex)
        {
            Fail("before a fill effect", ex);
        }
    }

    /// <summary>Run returns early when the stat is already at the limit (AE_AddCostAttr_Float.c:76-77); nothing is left marked.</summary>
    private static void AfterFill() => _fillingAttr = null;

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

            // GetOrBuildRecipeStatic fills a blank row and only names the recipe once the brief is back, so the key is
            // taken from what it returns.
            if (string.IsNullOrEmpty(recipeKey)) _pendingBrief = (productConfig, preview);
            else NoteBrief(recipeKey, productConfig, preview);
        }
        catch (Exception ex)
        {
            StopBriefs(ex);
        }
    }

    private static void BeforeStatic() => _pendingBrief = null;

    private static void AfterStatic(RecipeStaticInfo __result)
    {
        if (Volatile.Read(ref _isBriefDormant) != 0 || _recipes == null || _pendingBrief is not { } brief) return;

        _pendingBrief = null;

        try
        {
            if (!string.IsNullOrEmpty(__result?.RecipeKey)) NoteBrief(__result.RecipeKey, brief.Config, brief.Preview);
        }
        catch (Exception ex)
        {
            StopBriefs(ex);
        }
    }

    private static void StopBriefs(Exception ex)
    {
        if (Interlocked.Exchange(ref _isBriefDormant, 1) == 0) _log("crafting previews stopped after an error, crafting chips keep the game's estimates (reported once): " + ex);
    }

    private static void NoteBrief(string recipeKey, Config_Item? productConfig, string preview)
    {
        var furnitureId = productConfig?.TargetFurnitureID ?? 0;

        // FillFurnitureBrief returns at once for an item that is not furniture (Reducer_Web_ToolTable.c:4308).
        if (furnitureId <= 0 || _recipes == null) return;

        var choice = ChoiceFor(furnitureId);
        var chosen = choice.Func;
        var isMapped = chosen is { } func && preview.Length > 0 && func.Preview == preview;

        lock (Gate)
        {
            if (isMapped && chosen is { } mapped) _recipes.Note(recipeKey, new RecipeSite(new MeasureSite(furnitureId, mapped.Id), preview));
            else _recipes.Forget(recipeKey);
        }

        if (!_isTracing) return;

        var outcome = isMapped ? "mapped" : chosen == null ? "none" : "mismatch, game shows " + preview;

        _trace($"recipe key={recipeKey} item={productConfig?.ID ?? 0} furniture={furnitureId} slot={choice.SlotType} func={chosen?.Id ?? 0} preview={chosen?.Preview ?? ""} -> {outcome}");
    }

    /// <summary>Read once per furniture and kept while the game's furniture table stays the same object.</summary>
    private static BriefChoice ChoiceFor(int furnitureId)
    {
        var configs = GameSingleton.Existing<ConfigManager>();
        var furnitures = configs?._Config_Furniture_Dict;

        if (configs == null || furnitures == null) return default;

        if (Briefs.TryGet(furnitures.Pointer.ToInt64(), furnitureId, out var known)) return known;

        // The tables are read directly: Get_Config_Furniture and Get_Config_FurnitureFunc write an error to the game's
        // log for every id they do not have.
        var choice = furnitures.TryGetValue(furnitureId, out var furniture) && furniture != null
            ? new BriefChoice(true, furniture.SlotType, BriefFunc.Choose(furniture.SlotType, FuncsOf(configs, furniture)))
            : default;

        Briefs.Put(furnitureId, choice);

        return choice;
    }

    /// <summary>ConfigManager._Config_FurnitureFunc_Dict (read by Get_Config_FurnitureFunc, ConfigManager.c:12696) for each of the furniture's functions, in order.</summary>
    private static List<FurnitureFunc?> FuncsOf(ConfigManager configs, Config_Furniture furniture)
    {
        var funcs = new List<FurnitureFunc?>();
        var ids = furniture.FurnitureFunc;
        var table = configs._Config_FurnitureFunc_Dict;

        if (ids == null || table == null) return funcs;

        for (var i = 0; i < ids.Count; i++)
        {
            funcs.Add(table.TryGetValue(ids[i], out var func) && func != null ? new FurnitureFunc(func.ID, func.RecommendWeight, func.PreviewAttrDelta) : null);
        }

        return funcs;
    }

    /// <summary>
    /// Whether a change can count toward a measurement (TickRule): an action's own effect sourced to furniture, or a
    /// reward-shaped change while an end-of-use reward is being given. Never a change one stat makes to another.
    /// </summary>
    private static bool MayCount(EffectArgsType argsType, bool isDependency, VitalityChangeSourceType sourceType)
    {
        if (isDependency) return false;
        if (argsType == EffectArgsType.Action && sourceType == VitalityChangeSourceType.Furniture) return true;

        return argsType == EffectArgsType.None && sourceType == VitalityChangeSourceType.DynamicEvent && Volatile.Read(ref _isRewardOpen) != 0;
    }

    /// <summary>ActionManager.GetActionSource (ActionManager.c:3116) and AgentActionSource.CurrentAction (AgentActionSource.c:15).</summary>
    private static AgentAction? CurrentAction() => HeroActions()?.CurrentAction;

    private static AgentActionSource? HeroActions() =>
        GameSingleton.Existing<BattleLogicWorld>()?._ActionManager?.GetActionSource(Interlocked.Read(ref _heroId));

    /// <summary>
    /// After RemoveAction: it cleared the current action, and kept the press because the next queued action belongs to
    /// it (ActionManager.c:6792); ActionManager.Update starts that one on a later frame (ActionManager.c:10033).
    /// </summary>
    private static bool PressGoesOn(long press)
    {
        if (press <= 0) return false;

        var actions = HeroActions();
        var queued = actions?.ActionList;

        if (actions == null || actions.CurrentAction != null || queued == null || queued.Count == 0) return false;

        return queued[0]?.ParentId == press;
    }

    /// <summary>
    /// The furniture config of the action's target, found the way TryExecuteFurnitureFuncReward finds it:
    /// AgentManager.GetAgent (AgentManager.c:3187, called at ActionManager.c:8326), a Furniture, its AgentConfigId
    /// (ActionManager.c:8344). 0 when the target is no furniture.
    /// </summary>
    private static int FurnitureOf(long targetId) =>
        GameSingleton.Existing<BattleLogicWorld>()?._AgentManager?.GetAgent(targetId)?.TryCast<Furniture>()?.AgentConfigId ?? 0;

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
