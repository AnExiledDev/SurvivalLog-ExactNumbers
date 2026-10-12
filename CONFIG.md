# Exact Numbers settings

Settings live in `BepInEx\config\com.dev.survivallog.exactnumbers.cfg`, created the first time the game starts with the mod. Edit the file while the game is closed; changes apply at the next game start. Delete the file to get the defaults back.

## General

| Setting | Default | What it does |
| --- | --- | --- |
| `Enabled` | `true` | Show numbers instead of + and - marks in the stat chips of action and crafting tooltips. Off leaves every tooltip as the game draws it. |
| `ApplyModifiers` | `true` | Scale the numbers by your character's own satiety, mood, energy and life ratios, the way the game scales every change to those stats. Where that changed a number, the chip's hover text also gives the number before it. Off shows the numbers unscaled. |
| `Measure` | `true` | Note what each action on an object really does to your character, and show that number (without `~`) once you have used the action. Until then, and for actions that top a stat up to a level, the game's estimate is shown with `~`. Measurements are saved in `BepInEx\exact-numbers-measured.json`. Off shows the game's estimates only, exactly as version 0.1.0 did, and reads or writes no measurements file. |
| `TraceLog` | `true` | While `Measure` is on, write every stat change of your character, every measured use, and what the mod read for each menu button to `BepInEx\exact-numbers.log`. This is for bug reports: it is what lets a wrong number be traced back. The file is kept under about 10 MB; past that it is moved to `exact-numbers.log.1` and started again. Off keeps only the mod's normal lines in that file. |

There are no hotkeys.
