#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Multiverse.EditorTools
{
    /// <summary>
    /// Menerapkan pengaturan global yang disepakati. Dijalankan sekali oleh Integrator
    /// (hasilnya tersimpan di ProjectSettings/ dan ikut di-commit).
    /// </summary>
    public static class ProjectSetup
    {
        [MenuItem("Tools/Multiverse/Apply Project Settings (Integrator)", false, 40)]
        public static void Apply()
        {
            PlayerSettings.companyName = "Multiverse Team";
            PlayerSettings.productName = "Multiverse Arcade";

            PlayerSettings.defaultScreenWidth = 1920;
            PlayerSettings.defaultScreenHeight = 1080;
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            PlayerSettings.resizableWindow = true;

            // Linear: pencahayaan 3D (Dragon Hunter, Cyber Neon Dodger) benar secara fisik.
            // Harus ditetapkan SEBELUM ada aset berwarna, karena mengubahnya belakangan
            // menggeser seluruh tampilan warna semua game.
            PlayerSettings.colorSpace = ColorSpace.Linear;

            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Standalone, ScriptingImplementation.Mono2x);

            AssetDatabase.SaveAssets();
            Debug.Log("[Multiverse] Project settings diterapkan (Linear, 1920x1080 FullScreenWindow, Mono).");
        }

        /// <summary>CLI: -executeMethod Multiverse.EditorTools.ProjectSetup.ApplyBatch</summary>
        public static void ApplyBatch()
        {
            Apply();
            EditorApplication.Exit(0);
        }
    }
}
#endif
