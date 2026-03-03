# AdjustableUpgrades

A REPO mod that lets you **adjust your upgrade levels** within your purchased cap.

## How It Works

- When you **purchase an upgrade**, both your actual level and your level cap increase by 1.
- Press **F2** to open an in-game UI where you can freely adjust each upgrade's level between 0 and your cap.
- Every player who wants to use this feature needs to install this mod.

## Features

- **Per-upgrade adjustment**: Use +/- buttons or sliders to set each upgrade to any level within your cap
- **Quick actions**: MAX ALL, CLEAR ALL, RANDOM buttons for batch adjustment
- **Vanilla & Modded**: Supports both vanilla and modded upgrades
- **Multiplayer sync**: Level changes are synced to all players via Photon RPC
- **Configurable hotkey**: Default F2, changeable in BepInEx config

## Installation

1. Install [BepInEx](https://thunderstore.io/c/repo/p/BepInEx/BepInExPack/) if not already installed
2. Place `AdjustableUpgrades.dll` in `BepInEx/plugins/`
3. Launch the game and press **F2** to open the upgrade adjustment UI

## Configuration

Config file is generated at `BepInEx/config/Hazuki.REPO.AdjustableUpgrades.cfg`:

| Setting | Default | Description |
|---|---|---|
| ToggleUIKey | F2 | Key to toggle the UI |
| EnableRandomizeButton | true | Show the Randomize button |
