using BepInEx.Configuration;
using UnityEngine;

namespace AdjustableUpgrades.Configuration
{
    internal static class ModConfig
    {
        public static ConfigEntry<KeyCode> ToggleUIKey;
        public static ConfigEntry<bool> EnableRandomizeButton;

        public static void Init(ConfigFile config)
        {
            ToggleUIKey = config.Bind(
                "UI Settings",
                "ToggleUIKey",
                KeyCode.F2,
                "Key to toggle the upgrade adjustment UI"
            );

            EnableRandomizeButton = config.Bind(
                "UI Settings",
                "EnableRandomizeButton",
                true,
                "Show the 'Randomize All' button in the UI"
            );
        }
    }
}
