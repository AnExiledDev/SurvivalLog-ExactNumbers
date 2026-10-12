# Exact Numbers

See the real numbers behind the + and - marks in action tooltips.

When you hover an action on an object (sleep, eat, reinforce a door), Survival Log shows what it does to satiety, mood, energy and life, but only as one to three + or - marks. Exact Numbers swaps each mark for the number, such as `-12` or `+4`. It does the same for the stat chips in the crafting table's tooltips. It only changes what the tooltip says. It never changes your character, the world or a save.

## Features

- **Numbers instead of marks**: every + or - chip in an object's action menu tooltip and in the crafting table's product tooltip shows its number.
- **Your character's modifiers**: the game scales every satiety, mood, energy and life change by your character's own ratios (from traits, conditions and the like). By default the numbers include that scaling, so they match what your character gets. Where the scaling changed a number, the chip also carries the listed one as hover text (`Base -12`), for the tooltips that stay open while you point at them. Turn `ApplyModifiers` off to see the listed numbers instead.
- **Safe fallback**: if anything does not look the way the mod expects, the tooltip stays exactly as the game drew it.
- **A log file**: what the mod set up, and your character's ratios whenever they change, are written to `BepInEx\exact-numbers.log`.

There are no hotkeys. Exact Numbers adds no data to your save and does not edit game files. You can remove it at any time.

## Where the numbers come from

Each action carries a short preview line in the game's data, such as `Satiety:-12;Stamina:-20`. The game turns those numbers into marks: one mark up to 10, two up to 30, three above that. Exact Numbers shows the number itself.

Please keep in mind:

- The preview is the game's own summary of the action. The game does not say whether it is for one use or for an hour of work, so take it as the game's estimate for the action, now with a number on it.
- The real change happens while the action runs, and some effects only kick in then (a condition you have, a random event). Those can make the real change differ from the preview.
- The modifier scaling is the one the game applies to every stat change of your character. It is read from your character while you play. On the main menu, or if it cannot be read, the listed numbers are shown.
- Max chips (a change to a stat's maximum) already show their number in the game and are left alone.
- With the Action Speed mod, actions take a different time. The numbers here are the game's preview and are not scaled by Action Speed.

## Known issues and limits

This is a new mod. If a number looks wrong, a tooltip shows marks where you expected a number, or something else looks off, please tell me (see [Feedback](#feedback-and-bug-reports)).

- The first time a tooltip with a new action appears, its marks can show for a split second before the numbers arrive.
- A number that is not whole is shown to one decimal.
- The mod's own text, in the log and in the hover text, is in English.

## Requirements

- Survival Log 1.1.18317. Built for this version; a game update can break it until the mod is updated.
- The [BepInEx Pack for Survival Log](https://www.nexusmods.com/survivallog/mods/12), the BepInEx 6 (IL2CPP) build for the game, version `6.0.0-be.788`.

## Install

1. Install the [BepInEx Pack for Survival Log](https://www.nexusmods.com/survivallog/mods/12) (if no other mods were installed before, start the game once so BepInEx finishes setup, then quit).
2. Download `ExactNumbers.dll` from the [latest release](https://github.com/AnExiledDev/SurvivalLog-ExactNumbers/releases/latest) and put it in the `BepInEx\plugins` folder inside the game folder (the folder with the game .exe). Full path example: `C:\Program Files (x86)\Steam\steamapps\common\Survival Log\BepInEx\plugins\ExactNumbers.dll`

## Uninstall

Delete `ExactNumbers.dll` from the `BepInEx\plugins` folder. For a clean slate, also delete `BepInEx\config\com.dev.survivallog.exactnumbers.cfg` and `BepInEx\exact-numbers.log`.

## Configuration

Settings live in `BepInEx\config\com.dev.survivallog.exactnumbers.cfg`, created the first time the game starts with the mod. Edit it while the game is closed; changes apply at the next game start. See [CONFIG.md](CONFIG.md) for every setting.

## Troubleshooting

Built against BepInEx `6.0.0-be.788`.

Check `BepInEx\LogOutput.log` for a line starting `===== Exact Numbers 0.1.0 started`. It says whether the mod is on and whether modifiers are applied. After it you should see `listening for page script messages`, then `page script ready` once the game's UI is up, and `modifiers now:` with your character's ratios once a world loads.

The same lines are in `BepInEx\exact-numbers.log`, which survives restarts.

If the tooltips still show marks, look in the log for a line starting `could not` or `page said:`. Those name what the mod could not reach.

## Feedback and bug reports

Found a bug, have an idea, or something feels off? Come say so on the Core Directive Discord: https://discord.gg/trTsyxUFqg . You can also leave a comment on the Nexus page or [open an issue](https://github.com/AnExiledDev/SurvivalLog-ExactNumbers/issues). When reporting a bug, attach `BepInEx\LogOutput.log` and `BepInEx\exact-numbers.log`, and say which object and action showed the wrong number.

## Build

This is a BepInEx 6 IL2CPP plugin. It compiles against BepInEx and the game's IL2CPP interop assemblies, so a game install with BepInEx set up and started once is required. Those assemblies come from the game and are not part of this repo. The .NET 8 SDK is required.

```
dotnet build src/ExactNumbers -c Release -p:GameDir="C:\path\to\Survival Log"
```

`GameDir` is the game folder. The default in the project file is a local path you will almost certainly need to override. The output DLL is at `src\ExactNumbers\bin\Release\ExactNumbers.dll`.

The unit tests cover the pure rules (reading the preview line the way the game's pages do, the game's modifier formula, how numbers are written and when they are sent again) and need nothing from the game:

```
dotnet test tests/ExactNumbers.Tests
```

## License

MIT. Copyright (c) 2026 AnExiledDev. See [LICENSE](LICENSE).
