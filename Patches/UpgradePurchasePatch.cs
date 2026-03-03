using AdjustableUpgrades.Core;
using HarmonyLib;
using System.Collections.Generic;

namespace AdjustableUpgrades.Patches
{
    [HarmonyPatch(typeof(ItemUpgrade), "PlayerUpgrade")]
    public class UpgradePurchasePatch
    {
        // Store pre-upgrade stats to detect which upgrade changed
        public struct UpgradeSnapshot
        {
            public string SteamID;
            public Dictionary<string, int> PreLevels;
        }

        [HarmonyPrefix]
        public static void Prefix(ItemUpgrade __instance, out UpgradeSnapshot __state)
        {
            __state = default;

            ItemToggle toggle = AccessTools.Field(typeof(ItemUpgrade), "itemToggle").GetValue(__instance) as ItemToggle;
            if (toggle == null || !toggle.toggleState) return;

            int targetViewID = (int)AccessTools.Field(typeof(ItemToggle), "playerTogglePhotonID").GetValue(toggle);
            PlayerAvatar avatar = SemiFunc.PlayerAvatarGetFromPhotonID(targetViewID);
            if (avatar == null) return;

            string steamID = (string)AccessTools.Field(typeof(PlayerAvatar), "steamID").GetValue(avatar);

            // Only track caps for the local player
            bool isLocal = avatar.photonView != null && avatar.photonView.IsMine;
            if (!isLocal) return;

            Dictionary<string, int> preLevels = new Dictionary<string, int>();
            if (StatsManager.instance != null)
            {
                foreach (var kvp in StatsManager.instance.dictionaryOfDictionaries)
                {
                    if (!kvp.Key.StartsWith("playerUpgrade")) continue;
                    int val = kvp.Value.ContainsKey(steamID) ? kvp.Value[steamID] : 0;
                    preLevels[kvp.Key] = val;
                }
            }

            __state = new UpgradeSnapshot
            {
                SteamID = steamID,
                PreLevels = preLevels
            };
        }

        [HarmonyPostfix]
        public static void Postfix(ItemUpgrade __instance, UpgradeSnapshot __state)
        {
            if (string.IsNullOrEmpty(__state.SteamID) || __state.PreLevels == null) return;
            if (StatsManager.instance == null) return;

            // Detect which upgrades changed and increase their caps
            foreach (var kvp in StatsManager.instance.dictionaryOfDictionaries)
            {
                if (!kvp.Key.StartsWith("playerUpgrade")) continue;

                int currentVal = kvp.Value.ContainsKey(__state.SteamID) ? kvp.Value[__state.SteamID] : 0;
                int preVal = __state.PreLevels.ContainsKey(kvp.Key) ? __state.PreLevels[kvp.Key] : 0;

                if (currentVal > preVal)
                {
                    int diff = currentVal - preVal;
                    UpgradeCapManager.IncreaseCap(kvp.Key, diff);
                    Plugin.Log.LogInfo($"Upgrade purchased: {kvp.Key} cap increased by {diff}");
                }
            }
        }
    }
}
