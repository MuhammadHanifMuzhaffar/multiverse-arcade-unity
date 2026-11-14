#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Multiverse.Editor
{
    /// <summary>
    /// Automasi pendaftaran scene ke Build Settings untuk proyek Arcade Multiverse.
    /// Memastikan scene Hub selalu berada pada Index 0 dan setiap scene game terdaftar secara deterministik.
    /// </summary>
    public static class SceneAutoRegister
    {
        private const string HubScenePath = "Assets/Hub/Scenes/MainMenu.unity";
        private const string GamesRootPath = "Assets/Games";

        [MenuItem("Tools/Multiverse/Register All Scenes to Build Settings", false, 10)]
        public static void RegisterAllScenes()
        {
            List<EditorBuildSettingsScene> buildScenes = new List<EditorBuildSettingsScene>();

            // 1. Scene Hub selalu menjadi Index 0
            if (File.Exists(HubScenePath))
            {
                buildScenes.Add(new EditorBuildSettingsScene(HubScenePath, true));
                Debug.Log($"[Multiverse] Registered Index 0 (Hub): {HubScenePath}");
            }
            else
            {
                Debug.LogWarning($"[Multiverse] Hub scene belum ditemukan di '{HubScenePath}'. Harap buat/simpan MainMenu.unity.");
            }

            // 2. Deteksi otomatis seluruh scene game di dalam Assets/Games/
            if (Directory.Exists(GamesRootPath))
            {
                string[] discoveredScenes = Directory.GetFiles(GamesRootPath, "*.unity", SearchOption.AllDirectories);
                Array.Sort(discoveredScenes, StringComparer.OrdinalIgnoreCase);

                foreach (string scenePath in discoveredScenes)
                {
                    string normalizedPath = scenePath.Replace('\\', '/');
                    if (normalizedPath != HubScenePath)
                    {
                        buildScenes.Add(new EditorBuildSettingsScene(normalizedPath, true));
                        Debug.Log($"[Multiverse] Registered Game Scene (Index {buildScenes.Count - 1}): {normalizedPath}");
                    }
                }
            }
            else
            {
                Debug.LogWarning($"[Multiverse] Folder games belum ditemukan di '{GamesRootPath}'.");
            }

            // 3. Terapkan ke EditorBuildSettings
            EditorBuildSettings.scenes = buildScenes.ToArray();
            Debug.Log($"[Multiverse] Sukses mendaftarkan {buildScenes.Count} scene ke Build Settings.");

            EditorUtility.DisplayDialog(
                "Multiverse Scene Auto-Register",
                $"Berhasil mendaftarkan {buildScenes.Count} scene ke Build Settings.\n\nScene Index 0: MainMenu (Hub).",
                "OK"
            );
        }
    }
}
#endif
