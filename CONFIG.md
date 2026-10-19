# Exact Numbers settings

Settings live in `BepInEx\config\com.dev.survivallog.exactnumbers.cfg`, created the first time the game starts with the mod. Change them in game on the Mod Manager's Mods screen, or edit the file while the game is closed. ApplyModifiers changed on the Mods screen takes effect at once; Enabled, Measure and TraceLog take effect at the next game start. Delete the file to get the defaults back. Without Exiled's Toolkit installed, changes made on the Mods screen take effect at the next game start instead.

## General

| Setting | Default | What it does |
| --- | --- | --- |
| `Enabled` | `true` | On shows numbers in place of the + and - marks in the tooltips of object action menus and the crafting table. Off leaves every tooltip as the game draws it. |
| `ApplyModifiers` | `true` | On scales the numbers by your character's own modifiers from traits, buffs and conditions, the same way the game scales every satiety, mood, energy and life change. Where that changed a number, the chip's hover text also gives the number before scaling. Off shows the numbers before that scaling. |
| `Measure` | `true` | On notes what each action on an object really did to your character, and shows that number (without `~`) once you have finished the action. Until then, and for actions that top a stat up to a level, the game's estimate is shown with `~`. Measurements are saved in `BepInEx\exact-numbers-measured.json`. Off shows only the game's estimates and does not read or save measurements. |
| `TraceLog` | `true` | On, while `Measure` is also on, the mod writes every stat change of your character, every measured use, and what it read for each menu button to `BepInEx\exact-numbers.log`. This helps with bug reports: it lets a wrong number be traced back. The file stays under about 10 MB: past that it is moved to `exact-numbers.log.1` and started again. Off writes only the mod's usual lines to that file. |

There are no hotkeys.
