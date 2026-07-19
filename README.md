# ⚔️ Erenshor SwingSync

> **Your hits land when your blade does.** Removes the classic-MMO "server said you hit it half a second before your character swung" disconnect from Erenshor melee combat.

![Version](https://img.shields.io/badge/version-1.1.1-blue)
![BepInEx](https://img.shields.io/badge/BepInEx-5.4.23%2B-green)
![Game](https://img.shields.io/badge/Erenshor-Steam-orange)
![License](https://img.shields.io/badge/license-MIT-lightgrey)

📦 **Get it:** [Nexus Mods](https://www.nexusmods.com/erenshor/mods/14) · [Thunderstore](https://thunderstore.io/c/erenshor/p/Blackhorse311/ErenshorSwingSync/) · [GitHub Releases](https://github.com/Blackhorse311/ErenshorSwingSync/releases)

---

## ⏱️ 30 Seconds to Understand

Erenshor faithfully recreates 1999-era MMO combat, including the part where the "server" resolves your hit instantly while your character is still winding up the swing:

```
VANILLA:
  [attack round fires]
   ├─ damage number pops ──────────► you see "You hit Rat for 12!"
   ├─ combat log updates
   └─ swing animation STARTS ... ~0.5s later the blade finally connects (with nothing)

SWINGSYNC:
  [attack round fires]
   └─ swing animation STARTS
        ... windup ...
        └─ blade connects ─────────► damage number, log, sounds, flinch, all land HERE
```

Same damage, same DPS, same attack speed. Only the *feedback timing* moves.

---

## ✨ Key Features

| Feature | Description |
|---|---|
| 🗡️ **Player swing sync** | Your melee damage (numbers, combat log, sounds, target flinch, screen shake) lands when the swing animation connects |
| 👹 **NPC & SimPlayer sync** | Mobs and simulated players get the same treatment, their hits on you sync with their animations |
| ⛏️ **Mining included** | Mining swings go through the same combat path and sync too |
| ⚖️ **Zero balance changes** | Attack round cadence and DPS are byte-identical to vanilla, for you *and* for mobs |
| 🏹 **Ranged untouched** | Bows and wands already have projectile travel time, so they are left alone |
| 🎛️ **Fully configurable** | Separate live-tunable delays for player and NPCs, plus master toggles to go back to 1999 anytime |
| 🛡️ **Fail-safe** | If a game update changes the code this mod hooks, it logs one clear error and leaves the game 100% vanilla |

---

## 🚀 Quick Start

1. Install [BepInEx 5.4.23+](https://github.com/BepInEx/BepInEx/releases) (64-bit) into your Erenshor folder
2. Download the mod from [Nexus Mods](https://www.nexusmods.com/erenshor/mods/14), [Thunderstore](https://thunderstore.io/c/erenshor/p/Blackhorse311/ErenshorSwingSync/) (mod-manager friendly), or [Releases](https://github.com/Blackhorse311/ErenshorSwingSync/releases)
3. Extract into your Erenshor folder so it looks like this:

```
Erenshor/
├── BepInEx/
│   ├── core/
│   └── plugins/
│       └── ErenshorSwingSync.dll   ← the mod
├── Erenshor_Data/
├── Erenshor.exe
└── winhttp.dll                     ← from BepInEx
```

4. Launch the game. That's it. Swing at something and enjoy living in the present day.

---

## 🎛️ Configuration Reference

Config file is generated on first launch at `Erenshor/BepInEx/config/com.blackhorse311.erenshor.swingsync.cfg`. All values apply live, no restart needed.

| Section | Setting | Default | Range | Description |
|---|---|---|---|---|
| `[General]` | `Enabled` | `true` | | Master toggle for player swing sync |
| `[General]` | `SwingDelaySeconds` | `0.45` | `0.0 - 1.5` | Seconds between your swing starting and damage landing |
| `[NPCs]` | `Enabled` | `true` | | Master toggle for NPC/SimPlayer swing sync |
| `[NPCs]` | `SwingDelaySeconds` | `0.45` | `0.0 - 1.5` | Seconds between an NPC swing starting and their damage landing |

💡 If impacts feel early or late for your weapon's animation, nudge `SwingDelaySeconds` by 0.05 until it feels right. [BepInEx.ConfigurationManager](https://github.com/BepInEx/BepInEx.ConfigurationManager) (press F1 in game) makes this painless.

---

## 🧩 Compatibility

| | Status |
|---|---|
| Erenshor Steam build (July 2026) | ✅ Built and tested against |
| BepInEx 5.4.23.x | ✅ |
| Unity 2021.3 (Mono) | ✅ |
| Other Erenshor mods | ✅ Designed for coexistence: deferred hits still run other mods' combat patches |
| Game updates | ⚠️ If the hooked methods change, the mod disables itself cleanly and logs an error, game stays vanilla |

---

## 🔧 Troubleshooting

| Symptom | Fix |
|---|---|
| Mod doesn't load | Check `BepInEx/LogOutput.log` for `Erenshor SwingSync ... loaded`. No log file at all means BepInEx isn't installed correctly |
| Damage still lands before the swing | Confirm `[General] Enabled = true` in the config, and check the log for errors |
| Timing feels slightly off | Tune `SwingDelaySeconds` live (see Configuration Reference) |
| Errors mentioning "Could not resolve" | The game updated and moved the code this mod hooks. Check here for a mod update and open an issue with your log |
| Want vanilla back temporarily | Set both `Enabled` values to `false`, or just remove the DLL |

---

## 🛠️ Building from Source

Requirements: .NET SDK 8+, Erenshor installed, BepInEx 5.4.23+ installed into the game folder.

```powershell
git clone https://github.com/Blackhorse311/ErenshorSwingSync.git
cd ErenshorSwingSync
dotnet build -c Release -p:GameDir="C:\Path\To\Steam\steamapps\common\Erenshor"
```

The build references `Assembly-CSharp.dll` and BepInEx assemblies from your game folder and copies the built DLL into `BepInEx/plugins` automatically (skipped gracefully if the folder doesn't exist).

---

## 🔒 Security & Compliance

- **No game files are modified.** All changes are runtime Harmony patches; removing the DLL restores 100% vanilla behavior
- **No network calls, no telemetry, no data collection.** The mod reads its config file and patches two game methods, nothing else
- **No redistributed game code.** This repository contains only original mod source
- **AI collaboration disclosure**: This mod was developed by Blackhorse311 in collaboration with Claude (Anthropic). All code was human-reviewed and play-tested before release

---

## 🙏 Credits

- **Burgee Media** for Erenshor, the single-player MMO we all wished for (go [wishlist and buy it](https://store.steampowered.com/app/2382520/Erenshor/))
- **BepInEx team** for the plugin framework
- **Blackhorse311** - author

### Community Contributors

*Your name here! PRs and well-described issues welcome.*

---

## 📜 Changelog

### 1.1.1
- Fixed NPCs attacking many times per second during the sync window (the "grass spider machine gun"). Their attack-round timer is now set the moment their swing starts, exactly like vanilla

### 1.1.0
- NPCs and SimPlayers now sync their swings too, their hits on you land with their animations
- Their attack cadence is backdated after each deferred hit, so mob DPS is unchanged from vanilla
- Separate `[NPCs]` config section with its own toggle and delay

### 1.0.1
- Fixed every swing failing with an NRE when the game destroyed the mod's coroutine host mid-session (impacts now run on the game's own combat components)

### 1.0.0
- Initial release: player melee and mining swings sync with their animations

---

## 💬 Support

- 🐛 [Report a bug](https://github.com/Blackhorse311/ErenshorSwingSync/issues/new?labels=bug)
- 💡 [Request a feature](https://github.com/Blackhorse311/ErenshorSwingSync/issues/new?labels=enhancement)

Include your `BepInEx/LogOutput.log` with bug reports and I'll take a look!
