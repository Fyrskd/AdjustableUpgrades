# AdjustableUpgrades

A [REPO](https://store.steampowered.com/app/3241660/REPO/) mod that lets you **adjust your upgrade levels** within your purchased cap.

## How It Works

- When you **purchase an upgrade**, both your actual level and your level cap increase.
- Press **F2** to open an in-game UI where you can freely adjust each upgrade's level between 0 and your cap.
- Every player who wants to use this feature needs to install this mod.

## Features

- **Per-upgrade adjustment** — Use +/- buttons or sliders to set each upgrade to any level within your cap
- **Quick actions** — MAX ALL, CLEAR ALL, RANDOM buttons for batch adjustment
- **Vanilla & Modded** — Supports both vanilla and modded upgrades
- **Multiplayer sync** — Level changes are synced to all players via Photon RPC
- **Configurable hotkey** — Default F2, changeable in BepInEx config

## Installation

### Via Thunderstore Mod Manager
1. Import the mod zip via the mod manager's import feature

### Manual
1. Install [BepInEx 5](https://thunderstore.io/c/repo/p/BepInEx/BepInExPack/)
2. Place `AdjustableUpgrades.dll` in `BepInEx/plugins/`
3. Launch the game and press **F2** to open the UI

## Building from Source

Requirements:
- .NET SDK (6.0+)
- REPO game installed (for game DLL references)
- BepInEx installed (for BepInEx/Harmony DLL references)

Update the DLL reference paths in `AdjustableUpgrades.csproj` to match your system, then:

```bash
dotnet build AdjustableUpgrades.csproj -c Release
```

Output: `bin/Release/net48/AdjustableUpgrades.dll`

## Configuration

Config file auto-generated at `BepInEx/config/Hazuki.REPO.AdjustableUpgrades.cfg`:

| Setting | Default | Description |
|---|---|---|
| ToggleUIKey | F2 | Key to toggle the UI |
| EnableRandomizeButton | true | Show the Randomize button |

## License

MIT
