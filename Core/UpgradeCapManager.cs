using HarmonyLib;
using Photon.Pun;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace AdjustableUpgrades.Core
{
    public static class UpgradeCapManager
    {
        // key -> cap value (max level this player has achieved)
        private static readonly Dictionary<string, int> upgradeCaps = new Dictionary<string, int>();

        // Tracks which keys are vanilla vs modded
        public static readonly HashSet<string> VanillaKeys = new HashSet<string>();
        public static readonly HashSet<string> ModdedKeys = new HashSet<string>();

        private static readonly object capLock = new object();
        private static readonly System.Random random = new System.Random();

        /// <summary>
        /// Initialize caps from current StatsManager data. Called on StatsManager.Start.
        /// </summary>
        public static void InitCapsFromCurrentStats()
        {
            lock (capLock)
            {
                upgradeCaps.Clear();
                VanillaKeys.Clear();
                ModdedKeys.Clear();

                if (StatsManager.instance == null) return;

                // Determine vanilla fields
                HashSet<string> vanillaFields = typeof(StatsManager)
                    .GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic)
                    .Select(f => f.Name)
                    .ToHashSet();

                string localSteamID = GetLocalSteamID();
                if (string.IsNullOrEmpty(localSteamID)) return;

                foreach (KeyValuePair<string, Dictionary<string, int>> kvp in StatsManager.instance.dictionaryOfDictionaries)
                {
                    if (!kvp.Key.StartsWith("playerUpgrade")) continue;

                    // Classify as vanilla or modded
                    if (vanillaFields.Contains(kvp.Key))
                        VanillaKeys.Add(kvp.Key);
                    else
                        ModdedKeys.Add(kvp.Key);

                    // Set cap to current level
                    int currentLevel = kvp.Value.ContainsKey(localSteamID) ? kvp.Value[localSteamID] : 0;
                    upgradeCaps[kvp.Key] = currentLevel;
                }

                Plugin.Log.LogInfo($"UpgradeCapManager initialized: {VanillaKeys.Count} vanilla, {ModdedKeys.Count} modded upgrade keys. Caps set from current levels.");
            }
        }

        /// <summary>
        /// Increase the cap for a specific upgrade key. Called after a purchase.
        /// </summary>
        public static void IncreaseCap(string key, int amount = 1)
        {
            lock (capLock)
            {
                if (!upgradeCaps.ContainsKey(key))
                    upgradeCaps[key] = 0;

                upgradeCaps[key] += amount;
                Plugin.Log.LogInfo($"Cap increased: {key} -> {upgradeCaps[key]}");
            }
        }

        /// <summary>
        /// Get the cap for a specific upgrade.
        /// </summary>
        public static int GetCap(string key)
        {
            lock (capLock)
            {
                return upgradeCaps.ContainsKey(key) ? upgradeCaps[key] : 0;
            }
        }

        /// <summary>
        /// Get the current actual level from StatsManager.
        /// </summary>
        public static int GetCurrentLevel(string key)
        {
            if (StatsManager.instance == null) return 0;
            string steamID = GetLocalSteamID();
            if (string.IsNullOrEmpty(steamID)) return 0;

            if (StatsManager.instance.dictionaryOfDictionaries.TryGetValue(key, out var dict))
            {
                return dict.ContainsKey(steamID) ? dict[steamID] : 0;
            }
            return 0;
        }

        /// <summary>
        /// Set the actual level for a specific upgrade (clamped to [0, cap]).
        /// Uses RPC to sync with all players.
        /// </summary>
        public static void SetLevel(string key, int targetLevel)
        {
            int cap = GetCap(key);
            int clampedLevel = Math.Max(0, Math.Min(targetLevel, cap));
            int currentLevel = GetCurrentLevel(key);

            if (clampedLevel == currentLevel) return;

            string steamID = GetLocalSteamID();
            if (string.IsNullOrEmpty(steamID)) return;

            if (PunManager.instance == null) return;
            PhotonView punView = PunManager.instance.GetComponent<PhotonView>();
            if (punView == null)
            {
                Plugin.Log.LogWarning("SetLevel: PunManager PhotonView not found.");
                return;
            }

            bool isVanilla = VanillaKeys.Contains(key);

            if (isVanilla)
            {
                string commandName = key.Substring("playerUpgrade".Length);
                int delta = clampedLevel - currentLevel;
                punView.RPC("TesterUpgradeCommandRPC", RpcTarget.All, steamID, commandName, delta);
                Plugin.Log.LogInfo($"SetLevel (vanilla): {key} {currentLevel} -> {clampedLevel} (delta: {delta})");
            }
            else
            {
                punView.RPC("UpdateStatRPC", RpcTarget.All, key, steamID, clampedLevel);
                Plugin.Log.LogInfo($"SetLevel (modded): {key} {currentLevel} -> {clampedLevel}");
            }
        }

        /// <summary>
        /// Randomize all upgrade levels within their caps.
        /// </summary>
        public static void RandomizeAll()
        {
            lock (capLock)
            {
                foreach (string key in upgradeCaps.Keys.ToList())
                {
                    int cap = upgradeCaps[key];
                    if (cap <= 0) continue;

                    int randomLevel = random.Next(0, cap + 1);
                    SetLevel(key, randomLevel);
                }
            }
            Plugin.Log.LogInfo("All upgrade levels randomized.");
        }

        /// <summary>
        /// Set all upgrades to their max cap.
        /// </summary>
        public static void MaxAll()
        {
            lock (capLock)
            {
                foreach (var kvp in upgradeCaps)
                {
                    SetLevel(kvp.Key, kvp.Value);
                }
            }
            Plugin.Log.LogInfo("All upgrade levels set to max.");
        }

        /// <summary>
        /// Set all upgrades to 0.
        /// </summary>
        public static void ClearAll()
        {
            lock (capLock)
            {
                foreach (string key in upgradeCaps.Keys.ToList())
                {
                    SetLevel(key, 0);
                }
            }
            Plugin.Log.LogInfo("All upgrade levels cleared to 0.");
        }

        /// <summary>
        /// Get all upgrade keys and their caps for UI display.
        /// </summary>
        public static Dictionary<string, int> GetAllCaps()
        {
            lock (capLock)
            {
                return new Dictionary<string, int>(upgradeCaps);
            }
        }

        /// <summary>
        /// Get a display-friendly name for an upgrade key.
        /// e.g. "playerUpgradeSpeed" -> "Speed"
        /// </summary>
        public static string GetDisplayName(string key)
        {
            if (key.StartsWith("playerUpgrade"))
                return key.Substring("playerUpgrade".Length);
            if (key.StartsWith("player"))
                return key.Substring("player".Length);
            return key;
        }

        private static string GetLocalSteamID()
        {
            if (SemiFunc.PlayerGetAll() == null) return null;

            foreach (PlayerAvatar player in SemiFunc.PlayerGetAll())
            {
                if (player == null) continue;
                if (player.photonView != null && player.photonView.IsMine)
                {
                    return SemiFunc.PlayerGetSteamID(player);
                }
            }

            // Fallback for singleplayer
            PlayerAvatar localPlayer = SemiFunc.PlayerGetAll().FirstOrDefault();
            return localPlayer != null ? SemiFunc.PlayerGetSteamID(localPlayer) : null;
        }
    }
}
