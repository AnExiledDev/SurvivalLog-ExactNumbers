using System;
using System.IO;
using BepInEx;
using BepInEx.Unity.IL2CPP;
using ExactNumbers.Adapters;
using ExactNumbers.Core;
using HarmonyLib;

namespace ExactNumbers;

[BepInPlugin(Guid, "Exact Numbers", Version)]
public sealed class Plugin : BasePlugin
{
    private const string Guid = "com.dev.survivallog.exactnumbers";
    private const string Version = "0.2.0";

    public override void Load()
    {
        var isEnabled = Config.Bind("General", "Enabled", true, "Show numbers instead of + and - marks in the stat chips of action and crafting tooltips.").Value;
        var applyModifiers = Config.Bind("General", "ApplyModifiers", true, "Scale the numbers by your character's own stat ratios, the way the game scales every satiety, mood, energy and life change. Off shows the numbers unscaled.").Value;
        var measure = Config.Bind("General", "Measure", true, "Watch what each piece of furniture really does to your character's stats and show that once it is known, instead of the game's estimate (marked ~). Off shows the game's estimates only, as version 0.1.0 did.").Value;
        var traceLog = Config.Bind("General", "TraceLog", true, "While Measure is on, write every stat change of your character and what the mod made of it to BepInEx/exact-numbers.log, for bug reports. The file is kept under about 10 MB, with one older copy.").Value;
        var log = new ExactLog(Log, Path.Combine(Paths.BepInExRootPath, "exact-numbers.log"));

        log.Write($"===== Exact Numbers {Version} started, enabled {isEnabled}, apply modifiers {applyModifiers}, measure {measure}, trace log {traceLog}");

        if (!isEnabled) return;

        var harmony = new Harmony(Guid);

        // Without the patch the page's requests never arrive, so a session would only re-post the install check forever.
        if (!PageMessages.Apply(harmony, log.Write)) return;

        var measuring = measure ? StartMeasuring(harmony, log, traceLog) : null;

        ExactTicker.Session = new ExactSession(new WebPage(log.Write), applyModifiers ? new GameModifiers(log.Write) : null, measuring, log.Write);
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
