#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Multiverse.Editor
{
    /// <summary>
    /// Registers only the six explicitly approved scenes, in a stable order.
    /// Does not modify Build Settings if any required scene is missing.
    /// </summary>
    public static class SceneAutoRegister
    {
        private const string HubScenePath = "Assets/Hub/Scenes/MainMenu.unity";
        private static readonly string[] RequiredGameScenes =
        {
            "Assets/Games/CyberNeonDodger/Scenes/CyberNeonDodger_Main.unity",
            "Assets/Games/DragonHunter/Scenes/DragonHunter_Main.unity",
            "Assets/Games/CozyFruitCatcher/Scenes/CozyFruitCatcher_Main.unity",
            "Assets/Games/DeepSeaExplorer/Scenes/DeepSeaExplorer_Main.unity",
            "Assets/Games/SkyGlider/Scenes/SkyGlider_Main.unity"
        };

        [MenuItem("Tools/Multiverse/Register All Scenes to Build Settings", false, 10)]
        public static void RegisterAllScenes()
        {
            List<string> missing = new List<string>();
            List<string> scenePaths = new List<string> { HubScenePath };
            scenePaths.AddRange(RequiredGameScenes);

            foreach (string scenePath in scenePaths)
            {
                if (!File.Exists(scenePath))
                    missing.Add(scenePath);
            }

            if (missing.Count > 0)
            {
                string message = "Build Settings tidak diubah karena scene wajib belum tersedia:\n\n" +
                                 string.Join("\n", missing);
                Debug.LogError("[Multiverse] " + message);
                EditorUtility.DisplayDialog("Multiverse Scene Registration dibatalkan", message, "OK");
                return;
            }

            EditorBuildSettingsScene[] buildScenes = new EditorBuildSettingsScene[scenePaths.Count];
            for (int i = 0; i < scenePaths.Count; i++)
            {
                buildScenes[i] = new EditorBuildSettingsScene(scenePaths[i], true);
            }

            EditorBuildSettings.scenes = buildScenes;
            AssetDatabase.SaveAssets();
            Debug.Log("[Multiverse] Enam scene wajib terdaftar. MainMenu berada di index 0.");
            EditorUtility.DisplayDialog(
                "Multiverse Scene Registration berhasil",
                "Enam scene terdaftar; MainMenu berada di index 0.",
                "OK");
        }
    }
}
#endif
