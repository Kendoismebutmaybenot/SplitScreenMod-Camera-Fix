# Split-Screen Mod

A fork of [etkramer/SplitScreenMod](https://github.com/etkramer/SplitScreenMod) that fixes stretched split-screen cameras, plus a few other fixes (see [Changes in this fork](#-changes-in-this-fork)).

<img width="910" height="512" alt="image2" src="https://github.com/user-attachments/assets/6c79721c-43c1-4f48-bd84-215023368ea3" />

## 📖 About
Adds support for 2-player split-screen to Batman: Arkham City, allowing you to play through the main story and challenge maps with multiple players.

Based on [BmSDK](https://bmsdk.dev), a C# modding framework for the game built by the same creators as the split-screen mod. Leave a star on both repos if you have fun with this!

## ⚠️ Warning

This is an early in-development version of the split-screen mod. Several things can break during the campaign, especially if P2 triggers certain actions instead of P1. If this happens, restart from checkpoint or use the ghost/teleport keybinds to get past it.

## 🚀 Getting started
Follow these steps to set up the mod for the first time:
1. Install [BmSDK](https://github.com/Team-BmSDK/BmSDK-AC#-getting-started)
2. Download the latest .zip from [here](https://github.com/Kendoismebutmaybenot/SplitScreenMod-fixes/archive/refs/heads/main.zip) and extract it into `%GameDir%\BmGame\Mods`
3. If you've done everything right, your `Mods` folder should now have a `SplitScreenMod-fixes-main` folder inside it
4. Done! Start the game as usual, then press "G" from the menu to add a new player

## 🎮 Controls
Like normal the first controller plugged in is used for P1 (auto-swaps with keyboard/mouse), so P2 needs a second controller.

At the main menu:
- G: Add a player
- O: Remove a player
- C / V / B: Cycle P2's / P3's / P4's character and suit (shows the pick on screen and saves it to `mod.toml`)

In game:
- T: Teleport all players to P1 (if you get stuck)
- Y: Teleport all players to P2 (if you get stuck)
- V: Toggle ghost (if you get stuck)
- `[` (left bracket): Swap who is P1 mid-game. Every controller moves up one screen (P2's to P1, P3's to P2... P1's to the last player), so the next person takes over P1's character and screen. With 2 players it simply swaps P1 and P2

## ⚙️ Options
Edit `mod.toml` in the mod folder, under `[options]`:

- `layout_horizontal`: split the screen top/bottom (`true`) or side by side (`false`)
- `letterboxing`: `false` (default) fills each half of the screen with a wider view, with no stretching. `true` adds black bars instead
- `letterbox_aspect`: shape of each view when letterboxing is on (`1.7778` = 16:9)
- `players`: character and suit for P2, P3 and P4, e.g. `["Playable_Batman", "Beyond"]`. Picking the same character as P1 with a different suit works too. You can also pick these from the main menu with C / V / B

## ✨ Changes in this fork
- **No more stretched split-screen.** With letterboxing off, each 2P view used to be a 16:9 image squeezed into half the screen. Now each player's camera uses the real shape of their half, with a wider field of view so it shows as much height as solo play
- **Letterboxing is off by default**, since the full-size views no longer stretch. Turn it on with `letterboxing = true` if you prefer black bars
- **New `letterbox_aspect` option** to set the shape of the letterboxed views
- **Same character, different suit.** Extra players can be the same character as P1 in another suit (e.g. P2 as Batman Beyond). Before, they always got P1's suit. This works whatever suit P1 picks (Animated, Earth One, Year One, etc.)
- **Remote control batarang at normal speed.** The mod turns off slow-mo in split-screen so it doesn't slow the other player, which made the batarang fly about 3x too fast. Now only the batarang is slowed to its intended speed
- **P2 can use the line launcher tightrope.** The wire only checked whether P1 was on it, so it vanished as soon as P2 flipped up onto their own line
- **P2 can crack codes with the cryptographic sequencer.** It used P1's sticks and only gave codes to P1, so P2 could open it but never crack anything
- **P2 no longer gets stuck after cutscenes.** The game only unlocked P1's controls when a cutscene ended. Now any player still locked is freed, and brought to P1 if the cutscene moved P1 far away
- **Gadget icons for other characters.** P2's gadget bar used P1's icon set, so e.g. Robin as P2 had red question marks instead of gadget icons. Now each player gets their own character's icons
- **Robin gets grapnel boost and dive bomb in co-op** whenever Batman (P1) has those upgrades. Robin has his own upgrade list without them, so he couldn't use either. Nightwing and Catwoman are left alone, since their grapple gets stuck with the boost
- **Swap who is P1 mid-game** with the `[` key, for 2 to 4 players. The game follows P1 for the story and cutscenes, so this lets anyone take the lead without restarting
- **Pick P2-P4's characters from the main menu** with C / V / B, instead of editing `mod.toml`. Cycles through every installed character and suit (including the story's battle-damaged Batman and Catwoman suits), shows the pick on screen and saves it
- **Solo play is unchanged.** All of these only apply with 2 or more players

## 🛠️ Troubleshooting

If you run into issues getting the mod to work (i.e. the game crashes on startup, "G" doesn't work), try these steps:
1. Make sure you followed the install instructions exactly (for this and BmSDK)
2. If you're on Steam, double-check that you installed the BmSDK compatability patch
3. If you're on Steam and still nothing happens when you press a key, try running BatmanAC.exe directly instead of starting through Steam
4. Try using DX11 or DX9 mode - depending on what works better for you. You can change this by enabling or disabling DX11 features in the Arkham City Launcher. If the launcher doesn't work for you, you can try the [Advanced Launcher](https://www.nexusmods.com/batmanarkhamcity/mods/406) instead
