#if UNITY_EDITOR
using System.Collections.Generic;
using Multiverse.Hub;
using UnityEditor;
using UnityEngine;

namespace Multiverse.EditorTools
{
    /// <summary>
    /// Mendaftarkan scene resmi ke Build Settings dengan urutan stabil (MainMenu = index 0).
    /// Mode STRICT: tidak mengubah apa pun jika ada scene wajib yang hilang.
    /// Mode PARTIAL: untuk masa development, mendaftarkan scene yang sudah ada saja.
    /// </summary>
    public static class SceneAutoRegister
    {
        [MenuItem("Tools/Multiverse/Register All Scenes to Build Settings", false, 10)]
        public static void RegisterAllScenes()
        {
            Register(strict: true, interactive: true);
        }

        [MenuItem("Tools/Multiverse/Register Available Scenes (Partial, Dev)", false, 11)]
        public static void RegisterAvailableScenes()
        {
            Register(strict: false, interactive: true);
        }

        /// <summary>
        /// CLI: Unity.exe -batchmode -quit -projectPath ... -executeMethod
        /// Multiverse.EditorTools.SceneAutoRegister.RegisterAllScenesBatch
        /// </summary>
        public static void RegisterAllScenesBatch()
        {
            bool ok = Register(strict: true, interactive: false);
            EditorApplication.Exit(ok ? 0 : 1);
        }

        /// <summary>Registrasi strict tanpa dialog (dipakai SceneScaffolder & validator).</summary>
        public static bool RegisterAllScenesQuiet()
        {
            return Register(strict: true, interactive: false);
        }

        private static bool SceneExists(string path)
        {
            return AssetDatabase.LoadAssetAtPath<SceneAsset>(path) != null;
        }

        private static bool Register(bool strict, bool interactive)
        {
            var missing = new List<string>();
            var scenePaths = new List<string>();

            if (SceneExists(SceneCatalog.HubScenePath)) scenePaths.Add(SceneCatalog.HubScenePath);
            else missing.Add(SceneCatalog.HubScenePath);

            foreach (SceneCatalog.GameEntry game in SceneCatalog.Games)
            {
                if (SceneExists(game.ScenePath)) scenePaths.Add(game.ScenePath);
                else missing.Add(game.ScenePath);
            }

            bool hubMissing = missing.Contains(SceneCatalog.HubScenePath);
            if (hubMissing || (strict && missing.Count > 0))
            {
                string message = "Build Settings tidak diubah karena scene wajib belum tersedia:\n\n" +
                                 string.Join("\n", missing);
                Debug.LogError("[Multiverse] " + message);
                if (interactive)
                    EditorUtility.DisplayDialog("Multiverse Scene Registration dibatalkan", message, "OK");
                return false;
            }

            var buildScenes = new EditorBuildSettingsScene[scenePaths.Count];
            for (int i = 0; i < scenePaths.Count; i++)
                buildScenes[i] = new EditorBuildSettingsScene(scenePaths[i], true);

            EditorBuildSettings.scenes = buildScenes;
            AssetDatabase.SaveAssets();

            string summary = $"{scenePaths.Count} scene terdaftar; MainMenu berada di index 0.";
            if (missing.Count > 0)
                summary += "\n\nBelum tersedia (dilewati):\n" + string.Join("\n", missing);

            Debug.Log("[Multiverse] " + summary);
            if (interactive)
                EditorUtility.DisplayDialog("Multiverse Scene Registration berhasil", summary, "OK");
            return true;
        }
    }
}
#endif
