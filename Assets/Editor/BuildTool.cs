#if UNITY_EDITOR
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Multiverse.EditorTools
{
    /// <summary>
    /// Build Windows Standalone x64 dari scene yang terdaftar. Output: Builds/Windows/ (di-ignore Git).
    /// </summary>
    public static class BuildTool
    {
        private const string OutputPath = "Builds/Windows/MultiverseArcade.exe";

        [MenuItem("Tools/Multiverse/Build Windows (Integrator)", false, 50)]
        public static void BuildWindowsMenu()
        {
            bool ok = Build();
            EditorUtility.DisplayDialog("Multiverse Build",
                ok ? "Build berhasil:\n" + Path.GetFullPath(OutputPath) : "Build gagal. Lihat Console.", "OK");
        }

        /// <summary>CLI: -executeMethod Multiverse.EditorTools.BuildTool.BuildWindowsBatch</summary>
        public static void BuildWindowsBatch()
        {
            EditorApplication.Exit(Build() ? 0 : 1);
        }

        private static bool Build()
        {
            if (!SceneAutoRegister.RegisterAllScenesQuiet())
                return false;

            var options = new BuildPlayerOptions
            {
                scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
                locationPathName = OutputPath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            };

            BuildSummary summary = BuildPipeline.BuildPlayer(options).summary;
            Debug.Log($"[Multiverse] Build {summary.result}: {summary.totalSize / (1024 * 1024)} MB, " +
                      $"{summary.totalErrors} error, {summary.totalWarnings} warning, {summary.totalTime}");
            return summary.result == BuildResult.Succeeded;
        }
    }
}
#endif
