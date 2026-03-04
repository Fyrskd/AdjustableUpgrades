using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using AdjustableUpgrades.Configuration;
using AdjustableUpgrades.Core;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace AdjustableUpgrades
{
    [BepInPlugin(MOD_GUID, MOD_NAME, MOD_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        private const string MOD_GUID = "Hazuki.REPO.AdjustableUpgrades";
        private const string MOD_NAME = "Adjustable Upgrades";
        private const string MOD_VERSION = "1.0.3";

        private readonly Harmony harmony = new Harmony(MOD_GUID);

        internal static ManualLogSource Log;
        internal static Plugin Instance;

        // UI state
        private bool isUIVisible = false;
        private Rect windowRect = new Rect(20, 20, 420, 500);
        private Vector2 scrollPosition = Vector2.zero;
        private GUIStyle headerStyle;
        private GUIStyle labelStyle;
        private GUIStyle buttonStyle;
        private GUIStyle boxStyle;
        private GUIStyle windowStyle;
        private bool stylesInitialized = false;

        // Cursor state backup
        private CursorLockMode previousLockState;
        private bool previousCursorVisible;

        private const int WINDOW_ID = 98712;

        void Awake()
        {
            if (Instance != null) return;
            Instance = this;

            Log = Logger;

            // Hide from scene hierarchy (like GodMode does)
            ((Component)this).gameObject.transform.parent = null;
            ((UnityEngine.Object)((Component)this).gameObject).hideFlags = (HideFlags)61;

            UpgradeCapManager.SetSavePath(Paths.ConfigPath);
            ModConfig.Init(Config);
            harmony.PatchAll();

            Log.LogInfo($"{MOD_NAME} v{MOD_VERSION} loaded!");
            Log.LogInfo($"Press '{ModConfig.ToggleUIKey.Value}' to toggle upgrade adjustment UI.");
        }

        void Update()
        {
            if (Input.GetKeyDown(ModConfig.ToggleUIKey.Value))
            {
                isUIVisible = !isUIVisible;
                Log.LogInfo($"Upgrade Adjust UI toggled: {isUIVisible}");

                if (isUIVisible)
                {
                    previousLockState = Cursor.lockState;
                    previousCursorVisible = Cursor.visible;
                    Cursor.lockState = CursorLockMode.None;
                    Cursor.visible = true;

                    if (UpgradeCapManager.GetAllCaps().Count == 0)
                    {
                        Log.LogInfo("Caps empty, reinitializing...");
                        UpgradeCapManager.InitCapsFromCurrentStats();
                    }
                }
                else
                {
                    Cursor.lockState = previousLockState;
                    Cursor.visible = previousCursorVisible;
                }
            }
        }

        void OnGUI()
        {
            if (!isUIVisible) return;

            if (Cursor.lockState != CursorLockMode.None)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }

            InitStyles();
            windowRect = GUILayout.Window(WINDOW_ID, windowRect, DrawWindow, "Adjustable Upgrades", windowStyle);
        }

        void OnDestroy()
        {
            harmony.UnpatchSelf();
            if (isUIVisible)
            {
                Cursor.lockState = previousLockState;
                Cursor.visible = previousCursorVisible;
            }
        }

        private void InitStyles()
        {
            if (stylesInitialized) return;

            windowStyle = new GUIStyle(GUI.skin.window);
            windowStyle.normal.textColor = new Color(0.9f, 0.85f, 0.6f);
            windowStyle.fontSize = 16;
            windowStyle.fontStyle = FontStyle.Bold;

            headerStyle = new GUIStyle(GUI.skin.label);
            headerStyle.fontSize = 14;
            headerStyle.fontStyle = FontStyle.Bold;
            headerStyle.normal.textColor = new Color(1f, 0.9f, 0.5f);
            headerStyle.alignment = TextAnchor.MiddleCenter;

            labelStyle = new GUIStyle(GUI.skin.label);
            labelStyle.fontSize = 13;
            labelStyle.normal.textColor = Color.white;

            buttonStyle = new GUIStyle(GUI.skin.button);
            buttonStyle.fontSize = 13;
            buttonStyle.fontStyle = FontStyle.Bold;

            boxStyle = new GUIStyle(GUI.skin.box);
            boxStyle.normal.textColor = Color.white;
            boxStyle.fontSize = 12;

            stylesInitialized = true;
        }

        private void DrawWindow(int windowID)
        {
            Dictionary<string, int> caps = UpgradeCapManager.GetAllCaps();

            if (caps.Count == 0)
            {
                GUILayout.Space(10);
                GUILayout.Label("No upgrades detected yet.", headerStyle);
                GUILayout.Label("Enter a level first, then try again.", labelStyle);
                GUILayout.Space(5);
                if (GUILayout.Button("Refresh", buttonStyle, GUILayout.Height(30)))
                {
                    UpgradeCapManager.InitCapsFromCurrentStats();
                }
                GUILayout.Space(10);
                GUI.DragWindow();
                return;
            }

            // Action buttons
            GUILayout.Space(5);
            GUILayout.BeginHorizontal();
            {
                if (GUILayout.Button("MAX ALL", buttonStyle, GUILayout.Height(30)))
                    UpgradeCapManager.MaxAll();
                if (GUILayout.Button("CLEAR ALL", buttonStyle, GUILayout.Height(30)))
                    UpgradeCapManager.ClearAll();
                if (ModConfig.EnableRandomizeButton.Value)
                {
                    if (GUILayout.Button("RANDOM", buttonStyle, GUILayout.Height(30)))
                        UpgradeCapManager.RandomizeAll();
                }
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(5);

            // Upgrade lists
            List<string> vanillaKeys = caps.Keys.Where(k => UpgradeCapManager.VanillaKeys.Contains(k)).OrderBy(k => k).ToList();
            List<string> moddedKeys = caps.Keys.Where(k => UpgradeCapManager.ModdedKeys.Contains(k)).OrderBy(k => k).ToList();

            scrollPosition = GUILayout.BeginScrollView(scrollPosition);
            {
                if (vanillaKeys.Count > 0)
                {
                    GUILayout.Label("--- Vanilla Upgrades ---", headerStyle);
                    GUILayout.Space(3);
                    foreach (string key in vanillaKeys)
                        DrawUpgradeRow(key, caps[key]);
                }

                if (moddedKeys.Count > 0)
                {
                    GUILayout.Space(8);
                    GUILayout.Label("--- Modded Upgrades ---", headerStyle);
                    GUILayout.Space(3);
                    foreach (string key in moddedKeys)
                        DrawUpgradeRow(key, caps[key]);
                }

                List<string> unknownKeys = caps.Keys
                    .Where(k => !UpgradeCapManager.VanillaKeys.Contains(k) && !UpgradeCapManager.ModdedKeys.Contains(k))
                    .OrderBy(k => k).ToList();
                if (unknownKeys.Count > 0)
                {
                    GUILayout.Space(8);
                    GUILayout.Label("--- Other Upgrades ---", headerStyle);
                    GUILayout.Space(3);
                    foreach (string key in unknownKeys)
                        DrawUpgradeRow(key, caps[key]);
                }
            }
            GUILayout.EndScrollView();

            GUILayout.Space(5);
            GUILayout.Label("Press " + ModConfig.ToggleUIKey.Value.ToString() + " to close", labelStyle);
            GUI.DragWindow();
        }

        private void DrawUpgradeRow(string key, int cap)
        {
            int currentLevel = UpgradeCapManager.GetCurrentLevel(key);
            string displayName = UpgradeCapManager.GetDisplayName(key);

            GUILayout.BeginHorizontal(boxStyle);
            {
                GUILayout.Label($"{displayName}", labelStyle, GUILayout.Width(150));
                GUILayout.Label($"Lv.{currentLevel} / {cap}", labelStyle, GUILayout.Width(70));

                GUI.enabled = currentLevel > 0;
                if (GUILayout.Button("-", buttonStyle, GUILayout.Width(30), GUILayout.Height(25)))
                    UpgradeCapManager.SetLevel(key, currentLevel - 1);
                GUI.enabled = true;

                float sliderVal = GUILayout.HorizontalSlider(currentLevel, 0, cap, GUILayout.Width(80));
                int sliderLevel = Mathf.RoundToInt(sliderVal);
                if (sliderLevel != currentLevel)
                    UpgradeCapManager.SetLevel(key, sliderLevel);

                GUI.enabled = currentLevel < cap;
                if (GUILayout.Button("+", buttonStyle, GUILayout.Width(30), GUILayout.Height(25)))
                    UpgradeCapManager.SetLevel(key, currentLevel + 1);
                GUI.enabled = true;
            }
            GUILayout.EndHorizontal();
        }
    }
}
