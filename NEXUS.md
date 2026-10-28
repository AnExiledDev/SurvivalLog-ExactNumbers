Name: Exact Numbers
Summary: See real numbers in action tooltips where the game shows + and - marks: -10 satiety instead of two minus signs. Once you've used an action, it shows what your character actually got, scaled by your own modifiers, with the game's estimate (marked ~) until then. Only the tooltip changes, nothing in your game or your save.

Description (BBCode):
[center][img]https://sldb.core-directive.com/media/save/1-save-safe.png[/img][/center]

When you hover an action on an object, Survival Log shows what it does to satiety, mood, energy and life, but only as one to three + or - marks. Exact Numbers swaps each mark for a number. At first that is the game's own estimate, marked with ~. Once you have used the action, it is the amount your character really got. It only changes what the tooltip says; it never changes your character, the world or a save.

[size=5]Main features[/size]
[list]
[*][b]Numbers instead of marks[/b]: every + or - chip in an object's action menu tooltip and in the crafting table's product tooltip shows a number.[/*]
[*][b]Measured numbers[/b]: when you finish using an object, the mod notes what the action really did and shows that from then on, without the ~.[/*]
[*][b]Your character's modifiers[/b]: the game scales every satiety, mood, energy and life change by your character's own ratios. By default the numbers include that scaling. Where it changed a number, the chip's hover text also gives the number before it. You can turn this off in the config.[/*]
[*][b]Safe fallback[/b]: if anything does not look the way the mod expects, the tooltip stays exactly as the game drew it, or keeps the game's estimate.[/*]
[/list]
No hotkeys. Exact Numbers adds no data to your save and does not edit game files. You can remove it at any time.

Works in English and Chinese: the hover text and the words in the chips follow the game's language.

[size=5]Measured and estimated[/size]
A number with ~ in front is the game's estimate. A number without it was measured.

The estimate comes from a short preview line in the game's data, such as Satiety:-12;Stamina:-20, which the game turns into marks. It is a summary written for the tooltip, not what the game applies: some costs tick over time, some amounts are random, some gains fill a stat up to a set amount, every stat stops at zero and at its maximum, and food types, difficulty, conditions and buffs can change the amount.

The measurement is what the action really did to your character, noted each time a use ends:
[list]
[*][b]-10[/b]: the same amount every time.[/*]
[*][b]+12 to +16[/b]: the amount changed between uses.[/*]
[*][b]+4/h[/b]: for an action that runs until you stop it, the change per in-game hour.[/*]
[*][b]~+20[/b]: not measured yet.[/*]
[/list]
The chip's hover text says which it is: "measured over 3 uses" or the game's estimate. Natural changes, uses you break off, and uses where the stat hit zero or its maximum are not counted. Actions that top a stat up to a level keep the ~ estimate, since no single number would be right. Each action is measured on its own object, and measurements are scaled by the modifiers your character has now.

The numbers learn as you play: an action shows the estimate until you have finished it once. Long actions that end on their own show a total per use, not per hour. Measurements are saved in BepInEx\exact-numbers-measured.json, per game version. Delete it to start over, or turn Measure off in the config for estimates only.

Max chips already show their number and are left alone. With the Action Speed mod, the ~ estimates are not scaled to the changed action time; measured numbers are what your character really got.

[size=5]Known issues[/size]
[list]
[*]The first time a tooltip with a new action appears, its marks can show for a split second before the numbers arrive. A menu opened in the first couple of seconds after the game's UI loads can keep its marks.[/*]
[*]The first time the crafting table opens in a session, its products can show the ~ estimates even where a measurement exists.[/*]
[*]A number that is not whole is shown to one decimal.[/*]
[/list]

[center][img]https://sldb.core-directive.com/img/mods-banner.jpg[/img]
[size=4][url=https://www.nexusmods.com/survivallog/mods/73]Get Mod Manager[/url] to set up all your mods in game  |  [url=https://sldb.core-directive.com/en/]Open the Survival Log wiki[/url] for items, recipes and maps[/size][/center]

[size=5]Requirements[/size]
[list]
[*]Survival Log 1.1.19611. A game update can break it until the mod is updated.[/*]
[*][url=https://www.nexusmods.com/survivallog/mods/12]BepInEx for Survival Log[/url] (BepInEx 6.0.0-be.788, IL2CPP). Install it first.[/*]
[*]Optional: [url=https://www.nexusmods.com/survivallog/mods/61]Exiled's Toolkit[/url] 0.4.0 or newer. With it, settings changed on the Mod Manager's Mods screen take effect without restarting the game. Without it, they take effect at the next game start.[/*]
[/list]

[size=5]Installation[/size]
[list=1]
[*]Install [url=https://www.nexusmods.com/survivallog/mods/12]BepInEx[/url] [i](if no other mods were installed before, start the game once so BepInEx finishes setup, then quit)[/i].[/*]
[*]Optional, to change settings without a restart: install Exiled's Toolkit the same way. Download its zip and extract it into the game folder.[/*]
[*]Download the zip from the Files tab.[/*]
[*]Extract it into the game folder (the folder with [font=Courier New]SurvivalLog.exe[/font]). The zip holds a BepInEx folder, so the DLL lands in the right place. When asked, merge the folders. The result should be:
[code]C:\Program Files (x86)\Steam\steamapps\common\Survival Log\BepInEx\plugins\ExactNumbers.dll[/code][/*]
[/list]

[size=5]Uninstall[/size]
Delete [font=Courier New]ExactNumbers.dll[/font] from the [font=Courier New]BepInEx\plugins[/font] folder. The measurements are in [font=Courier New]BepInEx\exact-numbers-measured.json[/font] if you want them gone too.

[size=5]Configuration[/size]
To turn the mod, the modifier scaling, measuring or the detailed log off, edit [font=Courier New]BepInEx\config\com.dev.survivallog.exactnumbers.cfg[/font] while the game is closed, or do it in game on the Mod Manager's Mods screen (see [url=https://github.com/AnExiledDev/SurvivalLog-ExactNumbers/blob/main/CONFIG.md]CONFIG.md[/url] for every setting). ApplyModifiers changed there takes effect at once; Enabled, Measure and TraceLog take effect at the next game start. Without Exiled's Toolkit installed, changes made on the Mods screen take effect at the next game start instead.

[size=5]Feedback[/size]
Found a bug or have an idea? Come say hi on the [url=https://discord.gg/trTsyxUFqg]Core Directive Discord[/url], or post in this page's Posts tab. For bugs, please include [font=Courier New]BepInEx\LogOutput.log[/font] and [font=Courier New]BepInEx\exact-numbers.log[/font]. The second one records every stat change of your character (kept under about 10 MB), which is what lets a wrong number be traced back.
To reach me directly, message [b]exiled.dev[/b] on Discord; you can find me in the [url=https://discord.gg/MZGg4gQAUe]official Survival Log Discord[/url]. I'm also on X as [url=https://x.com/AnExiledDev]@AnExiledDev[/url].

[size=5]Source and license[/size]
Open source under MIT: [url=https://github.com/AnExiledDev/SurvivalLog-ExactNumbers]github.com/AnExiledDev/SurvivalLog-ExactNumbers[/url]

[line]
[size=5]简体中文[/size]

把鼠标悬停在物件的动作上时，Survival Log 会显示这个动作对饱腹、心态、精力和生命的影响，但只用一到三个 + 或 - 符号表示。Exact Numbers 会把每个符号换成具体数字。一开始显示的是游戏自己的预估值，前面带 ~。你用过一次这个动作后，显示的就是角色实际得到的数值。它只改变提示里写的内容，不会改变你的角色、世界或存档。

[size=5]主要功能[/size]
[list]
[*][b]数字代替符号[/b]：物件交互菜单的提示和工作台产物提示里的每个 + 或 - 标签都会显示数字。[/*]
[*][b]实测数字[/b]：每次用完一个物件，模组都会记下这个动作实际带来的变化，之后就显示这个数字，不再带 ~。[/*]
[*][b]计入角色倍率[/b]：游戏会按角色自身的倍率换算每次饱腹、心态、精力和生命的变化。数字默认已计入这个倍率。倍率改变了数字时，标签的悬停提示里还会写出换算前的数字。可以在配置中关闭。[/*]
[*][b]安全回退[/b]：只要有任何地方和模组预想的不一样，提示就保持游戏原样，或继续显示游戏的预估值。[/*]
[/list]
没有快捷键。Exact Numbers 不会往存档里添加任何内容，也不会修改游戏文件，随时可以移除。

支持英文和中文：悬停提示和标签里的文字会跟随游戏的语言设置。

[size=5]实测值与预估值[/size]
前面带 ~ 的数字是游戏的预估值，不带 ~ 的是实测值。

预估值来自游戏数据里的一行简短预览，例如 Satiety:-12;Stamina:-20，游戏会把它换成符号。这行预览是为提示写的概要，并不是游戏实际施加的数值：有些消耗会随时间逐步扣除，有些数值是随机的，有些增益会把属性补到一定数值，每项属性都不会低于零或超过上限，食物种类、难度、状态和增益也会改变实际数值。

实测值是这个动作实际给角色带来的变化，每次使用结束时记录：
[list]
[*][b]-10[/b]：每次都是同样的数值。[/*]
[*][b]+12 至 +16[/b]：每次使用的数值不同。[/*]
[*][b]+4/小时[/b]：一直进行到你主动停止的动作，显示游戏内每小时的变化。[/*]
[*][b]~+20[/b]：还没有实测过。[/*]
[/list]
标签的悬停提示会说明是哪一种：「根据 3 次使用实测」，或者是游戏的预估值。自然变化、中途打断的使用，以及属性到达零或上限的那次使用都不计入。会把属性补到一定数值的动作继续显示 ~ 预估值，因为没有哪个固定数字是对的。每个动作只在它自己的物件上实测，实测值会按角色当前的倍率换算。

数字会随着游戏进程逐步学到：一个动作在你完整做完一次之前都显示预估值。会自己结束的长动作显示的是每次使用的总量，而不是每小时的数值。实测记录按游戏版本保存在 BepInEx\exact-numbers-measured.json 中。删除这个文件即可重新开始实测；如果只想看预估值，可以在配置中关闭 Measure。

上限标签本来就显示数字，模组不会改动它们。和 Action Speed 模组一起使用时，~ 预估值不会按改变后的动作时长换算；实测值就是角色实际得到的数值。

[size=5]已知问题[/size]
[list]
[*]带有新动作的提示第一次出现时，符号可能会在数字到达前闪现一瞬间。游戏界面加载后最初一两秒内打开的菜单可能会一直显示符号。[/*]
[*]每次启动游戏后第一次打开工作台时，即使已有实测记录，产物也可能显示 ~ 预估值。[/*]
[*]不是整数的数字会保留一位小数。[/*]
[/list]

[url=https://www.nexusmods.com/survivallog/mods/73]获取 Mod Manager[/url]，在游戏里设置所有模组。[url=https://sldb.core-directive.com/zh/]打开 Survival Log 维基[/url]，物品、配方、地图一查就有。

[size=5]前置要求[/size]
[list]
[*]Survival Log 1.1.19611。游戏更新后模组可能暂时失效，直到模组更新为止。[/*]
[*][url=https://www.nexusmods.com/survivallog/mods/12]BepInEx for Survival Log[/url]（BepInEx 6.0.0-be.788，IL2CPP），请先安装。[/*]
[*]可选：[url=https://www.nexusmods.com/survivallog/mods/61]Exiled's Toolkit[/url] 0.4.0 或更新版本。装了它，在 Mod Manager 的「模组」界面改动的设置无需重启游戏即可生效；没装的话，改动在下次启动游戏时生效。[/*]
[/list]

[size=5]安装[/size]
[list=1]
[*]安装 [url=https://www.nexusmods.com/survivallog/mods/12]BepInEx[/url] [i]（如果之前没有装过其他模组，先启动一次游戏，让 BepInEx 完成初始化，然后退出）[/i]。[/*]
[*]可选（改动设置无需重启游戏）：用同样的方式安装 Exiled's Toolkit，下载它的压缩包并解压到游戏目录。[/*]
[*]在「文件」页下载压缩包。[/*]
[*]把压缩包解压到游戏目录（[font=Courier New]SurvivalLog.exe[/font] 所在的文件夹）。压缩包里带有 BepInEx 文件夹，DLL 会自动放到正确位置。如果提示合并文件夹，选择合并。解压后应为：
[code]C:\Program Files (x86)\Steam\steamapps\common\Survival Log\BepInEx\plugins\ExactNumbers.dll[/code][/*]
[/list]

[size=5]卸载[/size]
从 [font=Courier New]BepInEx\plugins[/font] 文件夹中删除 [font=Courier New]ExactNumbers.dll[/font]。如果也想清除实测记录，它们保存在 [font=Courier New]BepInEx\exact-numbers-measured.json[/font]。

[size=5]配置[/size]
如需关闭模组、倍率换算、实测或详细日志，可以在游戏内 Mod Manager 的「模组」界面修改，也可以在游戏关闭时编辑 [font=Courier New]BepInEx\config\com.dev.survivallog.exactnumbers.cfg[/font]。配置文件中每项设置都有中文说明，完整说明见 [url=https://github.com/AnExiledDev/SurvivalLog-ExactNumbers/blob/main/CONFIG.md]CONFIG.md[/url]（英文）。在「模组」界面修改的 ApplyModifiers 立即生效；Enabled、Measure 和 TraceLog 在下次启动游戏时生效。没装 Exiled's Toolkit 的话，在「模组」界面的改动改为在下次启动游戏时生效。

[size=5]反馈[/size]
发现 bug 或有好点子？欢迎来 [url=https://discord.gg/trTsyxUFqg]Core Directive Discord[/url] 打个招呼，或在本页的「Posts」标签里发帖。报告 bug 时，请附上 [font=Courier New]BepInEx\LogOutput.log[/font] 和 [font=Courier New]BepInEx\exact-numbers.log[/font]。后者记录了角色的每次属性变化（保持在约 10 MB 以内），有了它才能追查出错误数字的来源。
想直接联系我，可以在 Discord 上给 [b]exiled.dev[/b] 发消息，我在 [url=https://discord.gg/MZGg4gQAUe]Survival Log 官方 Discord[/url] 里。也可以在 X 上找我：[url=https://x.com/AnExiledDev]@AnExiledDev[/url]。

[size=5]源代码与许可[/size]
基于 MIT 许可开源：[url=https://github.com/AnExiledDev/SurvivalLog-ExactNumbers]github.com/AnExiledDev/SurvivalLog-ExactNumbers[/url]
