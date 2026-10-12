using System.IO;
using BepInEx;
using BepInEx.Unity.IL2CPP;
using ExactNumbers.Adapters;
using HarmonyLib;

namespace ExactNumbers;

[BepInPlugin(Guid, "Exact Numbers", Version)]
public sealed class Plugin : BasePlugin
{
    private const string Guid = "com.dev.survivallog.exactnumbers";
    private const string Version = "0.1.0";

    public override void Load()
    {
        var isEnabled = Config.Bind("General", "Enabled", true, "Show the game's listed numbers, marked ~ as estimates, instead of + and - marks in the stat chips of action and crafting tooltips.").Value;
        var applyModifiers = Config.Bind("General", "ApplyModifiers", true, "Scale the numbers by your character's own stat ratios, the way the game scales every satiety, mood, energy and life change. Off shows the game's listed numbers unchanged.").Value;
        var log = new ExactLog(Log, Path.Combine(Paths.BepInExRootPath, "exact-numbers.log"));

        log.Write($"===== Exact Numbers {Version} started, enabled {isEnabled}, apply modifiers {applyModifiers}");

        if (!isEnabled) return;

        var harmony = new Harmony(Guid);

        // Without the patch the page's requests never arrive, so a session would only re-post the install check forever.
        if (!PageMessages.Apply(harmony, log.Write)) return;

        ExactTicker.Session = new ExactSession(new WebPage(log.Write), applyModifiers ? new GameModifiers(log.Write) : null, log.Write);
        ExactTicker.Log = log.Write;
        AddComponent<ExactTicker>();
    }
}
