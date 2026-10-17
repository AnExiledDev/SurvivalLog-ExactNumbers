# Exact Numbers

See the numbers behind the + and - marks in action tooltips.

When you hover an action on an object (sleep, eat, reinforce a door), Survival Log shows what it does to satiety, mood, energy and life, but only as one to three + or - marks. Exact Numbers swaps each mark for a number. At first that is the number the game lists for the action, such as `~-12`. Once you have used the action, it is the amount your character really got, such as `-10`. It does the same for the stat chips in the crafting table's tooltips. It only changes what the tooltip says. It never changes your character, the world or a save.

## Features

- **Numbers instead of marks**: every + or - chip in an object's action menu tooltip and in the crafting table's product tooltip shows a number.
- **Measured numbers**: when you finish using an object, the mod notes what the action really did to your character and shows that from then on, without the `~`. See [Measured and estimated](#measured-and-estimated).
- **Your character's modifiers**: the game scales every satiety, mood, energy and life change by your character's own ratios (from traits, conditions and the like). By default the numbers include that scaling. Where the scaling changed a number, the chip's hover text also gives the number before it, for the tooltips that stay open while you point at them. Turn `ApplyModifiers` off to see the numbers unscaled.
- **Safe fallback**: if anything does not look the way the mod expects, the tooltip stays exactly as the game drew it, or keeps the game's estimate.
- **English and Simplified Chinese**: the words the mod adds (such as `to` in a range, `/h`, and the hover text) follow the game's language setting. Switch the language in game and the tooltips follow within about a second.
- **A log file**: what the mod set up, your character's ratios whenever they change, and (by default) every stat change of your character are written to `BepInEx\exact-numbers.log`.

There are no hotkeys. Exact Numbers adds no data to your save and does not edit game files. You can remove it at any time.

## Measured and estimated

A number with `~` in front is the game's estimate. A number without it was measured.

**The estimate.** Each action carries a short preview line in the game's data, such as `Satiety:-12;Stamina:-20`. The game turns those numbers into marks: one mark up to 10, two up to 30, three above that. Exact Numbers shows the number itself, with `~` in front. The preview is a summary written for the tooltip, not what the game applies. What your character actually gets comes from the action's effects while it runs, and those can differ: some costs tick over time, some amounts are random, some gains fill a stat up to a set amount, every stat stops at zero and at its maximum, and food types, difficulty, conditions and buffs can change the amount.

**The measurement.** While you play, the mod watches the stat changes the game makes to your character for the action you are doing on an object. When the action ends, it keeps what that use added up to, and the chip for that action on that kind of object shows it:

- `-10`: the same amount every time you have used it.
- `+12 to +16`: the amount changed between uses. It shows the range once two different amounts have been seen.
- `+4/h`: for an action that runs until you stop it, the change per in-game hour.
- `~+20`: not measured yet, so still the game's estimate.

Point at a chip for its hover text. It says `measured over 3 uses, total per use` (or `per in-game hour`) for a measured number, and that the `~` is the game's estimate otherwise.

Some things are left out on purpose, so a number is never shown that the mod cannot stand behind:

- Only the action's own changes count. Natural changes, such as recovery over time, are not counted.
- A use you break off before it finishes is not counted.
- A use where the stat hit zero or its maximum is not counted for that stat, since the stat could not take the full amount.
- Some actions top a stat up to a level instead of adding a fixed amount, so their amount depends on where you started. When the mod sees that, it keeps the `~` estimate and says why in the hover text, rather than show a number that would be wrong next time.
- Each action is measured on its own object. Two objects can list the same preview and do different things, so one's numbers never go on the other's chips.
- On the crafting table, a furniture product's chips use what that furniture's main action measured, the one the game shows the preview of.

Measurements are kept before your character's modifiers and scaled by the modifiers you have now when they are shown, so a trait or condition that changes later still shows correctly.

**It is a bit finicky.** The numbers learn as you play: an action shows the estimate until you have finished it once, and a range can widen as you use it more. Long actions that end on their own show a total per use, not per hour: what the whole use did. If you want the estimates only, turn `Measure` off.

The measurements are saved in `BepInEx\exact-numbers-measured.json` and loaded the next time you start the game. They are kept per game version, because a game update can change what an action does. Delete the file to start measuring from scratch. If the file is damaged, the mod renames it to `exact-numbers-measured.json.bad`, says so in the log, and starts fresh. If it cannot rename it, nothing is saved until you restart the game, so the old file is never overwritten.

Please also keep in mind:

- On the main menu, or if your character's modifiers cannot be read, the numbers are shown unscaled.
- Max chips (a change to a stat's maximum) already show their number in the game and are left alone.
- With the Action Speed mod, actions take a different time. The `~` estimates are not scaled by Action Speed. Measured numbers are what your character really got.

## Known issues and limits

This is a new mod. If a number looks wrong, a tooltip shows marks where you expected a number, or something else looks off, please tell me (see [Feedback](#feedback-and-bug-reports)).

- The first time a tooltip with a new action appears, its marks can show for a split second before the numbers arrive. A menu opened in the first couple of seconds after the game's UI loads can keep its marks.
- The first time the crafting table opens in a session, its products can show the `~` estimates even where a measurement exists.
- A number that is not whole is shown to one decimal.
- The log is in English in every game language, so bug reports read the same.

## Requirements

- Survival Log 1.1.18902. Built for this version; a game update can break it until the mod is updated.
- The [BepInEx Pack for Survival Log](https://www.nexusmods.com/survivallog/mods/12), the BepInEx 6 (IL2CPP) build for the game, version `6.0.0-be.788`.

## Install

1. Install the [BepInEx Pack for Survival Log](https://www.nexusmods.com/survivallog/mods/12) (if no other mods were installed before, start the game once so BepInEx finishes setup, then quit).
2. Download `ExactNumbers.dll` from the [latest release](https://github.com/AnExiledDev/SurvivalLog-ExactNumbers/releases/latest) and put it in the `BepInEx\plugins` folder inside the game folder (the folder with the game .exe). Full path example: `C:\Program Files (x86)\Steam\steamapps\common\Survival Log\BepInEx\plugins\ExactNumbers.dll`

## Uninstall

Delete `ExactNumbers.dll` from the `BepInEx\plugins` folder. For a clean slate, also delete `BepInEx\config\com.dev.survivallog.exactnumbers.cfg`, `BepInEx\exact-numbers-measured.json`, `BepInEx\exact-numbers.log` and `BepInEx\exact-numbers.log.1`.

## Configuration

Settings live in `BepInEx\config\com.dev.survivallog.exactnumbers.cfg`, created the first time the game starts with the mod. Edit it while the game is closed; changes apply at the next game start. See [CONFIG.md](CONFIG.md) for every setting.

## Troubleshooting

Built against BepInEx `6.0.0-be.788`.

Check `BepInEx\LogOutput.log` for a line starting `===== Exact Numbers`, followed by the mod's version and `started`. It says whether the mod is on, whether modifiers are applied, and whether measuring and the trace log are on. After it you should see `measurements loaded for game version` (or `no measurements file yet` on the first start) and `measuring furniture functions`, then `listening for page script messages`, then `page script ready` once the game's UI is up, and `modifiers now:` with your character's ratios once a world loads.

The same lines are in `BepInEx\exact-numbers.log`, which survives restarts. With `TraceLog` on (the default), that file also gets a line for every stat change of your character, every use the mod measured, and what it read for each menu button; those lines are written in a batch every few seconds. It is kept under about 10 MB: past that, it is moved to `exact-numbers.log.1` and started again.

If the tooltips still show marks, look in the log for a line starting `could not` or `page said:`. If numbers stay `~` after you have used an action, look for `measuring stopped`. Those name what the mod could not reach.

## Feedback and bug reports

Found a bug, have an idea, or something feels off? Come say so on the Core Directive Discord: https://discord.gg/trTsyxUFqg . You can also leave a comment on the Nexus page or [open an issue](https://github.com/AnExiledDev/SurvivalLog-ExactNumbers/issues). When reporting a bug, attach `BepInEx\LogOutput.log` and `BepInEx\exact-numbers.log`, and say which object and action showed the wrong number.

## Build

This is a BepInEx 6 IL2CPP plugin. It compiles against BepInEx and the game's IL2CPP interop assemblies, so a game install with BepInEx set up and started once is required. Those assemblies come from the game and are not part of this repo. The .NET 8 SDK is required.

```
dotnet build src/ExactNumbers -c Release -p:GameDir="C:\path\to\Survival Log"
```

`GameDir` is the game folder. The default in the project file is a local path you will almost certainly need to override. The output DLL is at `src\ExactNumbers\bin\Release\ExactNumbers.dll`.

The unit tests cover the pure rules (reading the game's messages to its pages and the preview line the way those pages do, the game's modifier formula, how numbers are written and marked in English and Chinese, and when they are sent again, which stat changes count as a measurement, how measurements are combined, saved and read back, and the trace log's lines and size cap), plus the page script, run in a JavaScript engine against a stand-in for the game's UI. They need nothing from the game:

```
dotnet test tests/ExactNumbers.Tests
```

## License

MIT. Copyright (c) 2026 AnExiledDev. See [LICENSE](LICENSE).
