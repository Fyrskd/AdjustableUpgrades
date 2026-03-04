using HarmonyLib;
using Photon.Pun;
using System;
using System.Collections.Generic;
using System.IO;
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

        // File path for persisting caps
        private static string capsFilePath;

        /// <summary>
        /// Set the file path for saving caps. Called from Plugin.Awake.
        /// </summary>
        public static void SetSavePath(string configDir)
        {
            capsFilePath = Path.Combine(configDir, "AdjustableUpgrades_caps.txt");
        }

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

                // Load previously saved caps
                Dictionary<string, int> savedCaps = LoadCapsFromFile();

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

                    // Current in-game level
                    int currentLevel = kvp.Value.ContainsKey(localSteamID) ? kvp.Value[localSteamID] : 0;

                    // Cap = max(saved cap, current level)
                    // This prevents cap loss when level was lowered before exit
                    int savedCap = savedCaps.ContainsKey(kvp.Key) ? savedCaps[kvp.Key] : 0;
                    upgradeCaps[kvp.Key] = Math.Max(savedCap, currentLevel);
                }

                // Also restore caps for keys that exist in save but not in current stats
                // (edge case: modded upgrades that were uninstalled)
                // Skip this to avoid phantom keys

                Plugin.Log.LogInfo($"UpgradeCapManager initialized: {VanillaKeys.Count} vanilla, {ModdedKeys.Count} modded. Caps loaded (saved caps applied).");

                // Restore levels to cap values (undo any lowered levels from last session)
                RestoreLevelsToCaps(localSteamID);

                // Save the merged caps
                SaveCapsToFile();
            }
        }

        /// <summary>
        /// After loading, restore all upgrade levels back to their cap values.
        /// This reverses any level reductions from the previous session.
        /// </summary>
        private static void RestoreLevelsToCaps(string localSteamID)
        {
            if (PunManager.instance == null) return;
            PhotonView punView = PunManager.instance.GetComponent<PhotonView>();
            if (punView == null) return;

            foreach (var kvp in upgradeCaps)
            {
                string key = kvp.Key;
                int cap = kvp.Value;

                // Get current level
                int currentLevel = 0;
                if (StatsManager.instance.dictionaryOfDictionaries.TryGetValue(key, out var dict))
                {
                    currentLevel = dict.ContainsKey(localSteamID) ? dict[localSteamID] : 0;
                }

                if (currentLevel < cap)
                {
                    // Restore to cap
                    bool isVanilla = VanillaKeys.Contains(key);
                    if (isVanilla)
                    {
                        string commandName = key.Substring("playerUpgrade".Length);
                        int delta = cap - currentLevel;
                        punView.RPC("TesterUpgradeCommandRPC", RpcTarget.All, localSteamID, commandName, delta);
                    }
                    else
                    {
                        punView.RPC("UpdateStatRPC", RpcTarget.All, key, localSteamID, cap);
                    }
                    Plugin.Log.LogInfo($"Restored {key}: {currentLevel} -> {cap}");
                }
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

                SaveCapsToFile();
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

        // ---- Persistence ----

        private static void SaveCapsToFile()
        {
            if (string.IsNullOrEmpty(capsFilePath)) return;

            try
            {
                List<string> lines = new List<string>();
                foreach (var kvp in upgradeCaps)
                {
                    lines.Add($"{kvp.Key}={kvp.Value}");
                }
                File.WriteAllLines(capsFilePath, lines);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"Failed to save caps: {ex.Message}");
            }
        }

        private static Dictionary<string, int> LoadCapsFromFile()
        {
            Dictionary<string, int> saved = new Dictionary<string, int>();
            if (string.IsNullOrEmpty(capsFilePath)) return saved;
            if (!File.Exists(capsFilePath)) return saved;

            try
            {
                string[] lines = File.ReadAllLines(capsFilePath);
                foreach (string line in lines)
                {
                    string trimmed = line.Trim();
                    if (string.IsNullOrEmpty(trimmed)) continue;

                    int eqIdx = trimmed.IndexOf('=');
                    if (eqIdx <= 0) continue;

                    string key = trimmed.Substring(0, eqIdx);
                    string valStr = trimmed.Substring(eqIdx + 1);
                    if (int.TryParse(valStr, out int val))
                    {
                        saved[key] = val;
                    }
                }
                Plugin.Log.LogInfo($"Loaded {saved.Count} saved caps from file.");
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"Failed to load caps: {ex.Message}");
            }

            return saved;
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
