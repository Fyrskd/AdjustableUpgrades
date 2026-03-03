using AdjustableUpgrades.Core;
using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace AdjustableUpgrades.Patches
{
    [HarmonyPatch(typeof(StatsManager), "Start")]
    public class StatsManagerInitPatch
    {
        [HarmonyPostfix]
        public static void Postfix(StatsManager __instance)
        {
            UpgradeCapManager.InitCapsFromCurrentStats();
        }
    }
}
