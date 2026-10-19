using System;
using System.IO;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Unity.IL2CPP;
using ExactNumbers.Adapters;
using ExactNumbers.Core;
using HarmonyLib;

namespace ExactNumbers;

[BepInPlugin(Guid, "Exact Numbers", Version)]
[BepInDependency(OptionalLive.ToolkitGuid, BepInDependency.DependencyFlags.SoftDependency)]
public sealed class Plugin : BasePlugin
{
    private const string Guid = "com.dev.survivallog.exactnumbers";
    private const string Version = "0.3.0";

    public override void Load()
    {
        if (!OptionalLive.IsToolkitInstalled) Log.LogInfo(OptionalLive.MissingNote);

        var isEnabled = Config.Bind("General", "Enabled", true,
            "On (default) shows numbers in place of the + and - marks in the tooltips of object action menus and the crafting table. Off leaves every tooltip as the game draws it. Takes effect at the next game start.\n" +
            "开启时（默认），物件交互菜单和工作台的悬停提示里的 + 和 - 符号会换成具体数字。关闭时，所有提示都保持游戏原样。下次启动游戏时生效。").Value;
        var applyModifiers = Config.Bind("General", "ApplyModifiers", true,
            new ConfigDescription("On (default) scales the numbers by your character's own modifiers from traits, buffs and conditions, the same way the game scales every satiety, mood, energy and life change. Off shows the numbers before that scaling.\n" +
            "开启时（默认），数字会计入角色自身特质、增益和状态带来的倍率，换算方式与游戏换算每次饱腹、心态、精力、生命变化时相同。关闭时显示换算前的数字。", null, "ModManager.ShowWhen=Enabled"));
        var measure = Config.Bind("General", "Measure", true,
            new ConfigDescription("On (default) notes what each action on an object really did to your character, and shows that number once you have finished the action, instead of the game's estimate marked with ~. Off shows only the game's estimates and does not read or save measurements. Takes effect at the next game start.\n" +
            "开启时（默认），模组会记下每个物件动作实际给角色带来的变化，你完成一次该动作后就显示这个实测数字，代替游戏标有 ~ 的预估值。关闭时只显示游戏的预估值，也不读取或保存实测记录。下次启动游戏时生效。", null, "ModManager.ShowWhen=Enabled")).Value;
        var traceLog = Config.Bind("General", "TraceLog", true,
            new ConfigDescription("On (default), while Measure is also on, the mod writes every stat change of your character and how it counted it to BepInEx/exact-numbers.log for bug reports, keeping the file under about 10 MB plus one older copy. Off writes only the mod's usual lines to that file. Takes effect at the next game start.\n" +
            "开启时（默认），且 Measure 也开启时，模组会把角色的每次属性变化以及模组如何统计它写入 BepInEx/exact-numbers.log，方便反馈问题，文件保持在约 10 MB 以内，另留一份旧文件。关闭时，该文件只记录模组的常规信息。下次启动游戏时生效。", null, "ModManager.ShowWhen=Enabled;Measure", "ModManager.Advanced")).Value;
        var log = new ExactLog(Log, Path.Combine(Paths.BepInExRootPath, "exact-numbers.log"));

        log.Write($"===== Exact Numbers {Version} started, enabled {isEnabled}, apply modifiers {applyModifiers.Value}, measure {measure}, trace log {traceLog}");

        if (!isEnabled) return;

        var harmony = new Harmony(Guid);

        // Without the patch the page's requests never arrive, so a session would only re-post the install check forever.
        if (!PageMessages.Apply(harmony, log.Write)) return;

        var measuring = measure ? StartMeasuring(harmony, log, traceLog) : null;

        var session = new ExactSession(new WebPage(log.Write), new GameModifiers(log.Write), measuring, GameLanguage.IsChinese, log.Write);

        OptionalLive.Watch(applyModifiers, isApplying =>
        {
            session.UseModifiers(isApplying);
            log.Write($"apply modifiers {isApplying}");
        });

        ExactTicker.Session = session;
        ExactTicker.Log = log.Write;
        AddComponent<ExactTicker>();
    }

    /// <returns>Null when the stat hooks could not be applied; the chips then show the game's estimates.</returns>
    private static Measuring? StartMeasuring(Harmony harmony, ExactLog log, bool traceLog)
    {
        var buffered = traceLog ? new BufferedTrace(log) : null;
        Action<string>? trace = buffered == null ? null : buffered.Write;
        Action flushTrace = buffered == null ? () => { } : buffered.Flush;

        // Measurements are kept per game version: a patch can change what a function does.
        var version = UnityEngine.Application.version ?? "unknown";
        var file = new MeasuredFile(Path.Combine(Paths.BepInExRootPath, "exact-numbers-measured.json"), version, log.Write);
        var measurer = new Measurer(file.Load(), trace);
        var recipes = new RecipeSites();

        if (!MeasureHooks.Apply(harmony, measurer, recipes, log.Write, trace))
        {
            flushTrace();

            return null;
        }

        return new Measuring(measurer, file, recipes, trace ?? (_ => { }), flushTrace);
    }
}
