#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Multiverse.Hub;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Multiverse.EditorTools
{
    /// <summary>
    /// Menegakkan aturan di README/PRD secara otomatis:
    /// scene terdaftar, 1 kamera + 1 AudioListener, Canvas Scaler 1920x1080 match 0.5,
    /// tombol Back to Hub, isolasi aset antar-game, duplikat aset (termasuk yang cuma diganti nama),
    /// batas ukuran tekstur/model, format audio, asmdef per game, dan manifest aset.
    /// </summary>
    public static class ProjectValidator
    {
        private enum Level { Error, Warning }

        private sealed class Finding
        {
            public Level Level;
            public string Code;
            public string Message;
            public override string ToString() => $"[{(Level == Level.Error ? "ERROR" : "WARN ")}] {Code}: {Message}";
        }

        private static readonly string[] ContentExclude = { ".meta", ".cs", ".asmdef", ".md", ".unity", ".txt", ".json" };
        private static readonly string[] AudioPreferred = { ".mp3", ".ogg" };
        private static readonly string[] AudioDiscouraged = { ".wav", ".aif", ".aiff", ".flac" };

        private const int TextureHardMax = 2048;
        private const int TextureSoftMax = 1024;
        private const int ModelTriangleLimit = 10000;

        [MenuItem("Tools/Multiverse/Validate Project", false, 30)]
        public static void ValidateFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            List<Finding> findings = Run();
            int errors = findings.Count(f => f.Level == Level.Error);
            int warnings = findings.Count - errors;

            string head = errors == 0
                ? $"LULUS: 0 error, {warnings} peringatan."
                : $"GAGAL: {errors} error, {warnings} peringatan.";
            string body = string.Join("\n", findings.Take(12).Select(f => f.ToString()));
            if (findings.Count > 12) body += $"\n... dan {findings.Count - 12} lainnya (lihat Console).";
            EditorUtility.DisplayDialog("Multiverse Validator", head + "\n\n" + body, "OK");
        }

        /// <summary>
        /// CLI: Unity.exe -batchmode -quit -projectPath ... -executeMethod
        /// Multiverse.EditorTools.ProjectValidator.ValidateBatch
        /// Exit code 0 = tanpa error, 1 = ada error. Laporan: Logs/Multiverse_Validation.txt
        /// </summary>
        public static void ValidateBatch()
        {
            List<Finding> findings = Run();
            int errors = findings.Count(f => f.Level == Level.Error);

            var sb = new StringBuilder();
            sb.AppendLine($"Multiverse validation {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            foreach (Finding f in findings) sb.AppendLine(f.ToString());
            sb.AppendLine($"RESULT: {errors} error, {findings.Count - errors} warning");

            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/Multiverse_Validation.txt", sb.ToString());
            Debug.Log(sb.ToString());
            EditorApplication.Exit(errors > 0 ? 1 : 0);
        }

        // ----------------------------------------------------------------- Run
        private static List<Finding> Run()
        {
            var f = new List<Finding>();

            CheckBuildSettings(f);
            CheckAssemblies(f);
            CheckIsolation(f);
            CheckDuplicates(f);
            CheckImportLimits(f);
            CheckManifests(f);
            CheckScenes(f);

            foreach (Finding x in f)
            {
                if (x.Level == Level.Error) Debug.LogError("[Multiverse] " + x);
                else Debug.LogWarning("[Multiverse] " + x);
            }

            int errors = f.Count(x => x.Level == Level.Error);
            Debug.Log($"[Multiverse] Validasi selesai: {errors} error, {f.Count - errors} peringatan.");
            return f;
        }

        private static void Add(List<Finding> list, Level level, string code, string message)
        {
            list.Add(new Finding { Level = level, Code = code, Message = message });
        }

        // ------------------------------------------------------ Build Settings
        private static void CheckBuildSettings(List<Finding> f)
        {
            EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
            var expected = new List<string> { SceneCatalog.HubScenePath };
            expected.AddRange(SceneCatalog.Games.Select(g => g.ScenePath));

            if (scenes.Length == 0 || scenes[0].path != SceneCatalog.HubScenePath)
                Add(f, Level.Error, "BUILD-001", "MainMenu harus berada di Build Settings index 0.");

            foreach (string path in expected)
            {
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null)
                {
                    Add(f, Level.Error, "BUILD-002", "Scene belum ada: " + path);
                    continue;
                }
                EditorBuildSettingsScene entry = scenes.FirstOrDefault(s => s.path == path);
                if (entry == null) Add(f, Level.Error, "BUILD-003", "Scene belum terdaftar di Build Settings: " + path);
                else if (!entry.enabled) Add(f, Level.Error, "BUILD-004", "Scene dinonaktifkan di Build Settings: " + path);
            }

            foreach (EditorBuildSettingsScene s in scenes)
                if (!expected.Contains(s.path))
                    Add(f, Level.Warning, "BUILD-005", "Scene non-resmi di Build Settings: " + s.path);
        }

        // ---------------------------------------------------------- Assemblies
        private static void CheckAssemblies(List<Finding> f)
        {
            foreach (SceneCatalog.GameEntry g in SceneCatalog.Games)
            {
                if (!AssetDatabase.IsValidFolder(g.FolderPath)) { Add(f, Level.Error, "ASM-001", "Folder game hilang: " + g.FolderPath); continue; }
                string[] asm = AssetDatabase.FindAssets("t:AssemblyDefinitionAsset", new[] { g.FolderPath });
                if (asm.Length == 0)
                    Add(f, Level.Warning, "ASM-002", $"{g.Folder} tidak punya .asmdef; error compile game ini akan memblokir 4 anggota lain.");
            }
        }

        // ----------------------------------------------------------- Isolation
        private static IEnumerable<string> ContentAssets(string folder)
        {
            if (!AssetDatabase.IsValidFolder(folder)) yield break;
            foreach (string guid in AssetDatabase.FindAssets("", new[] { folder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetDatabase.IsValidFolder(path)) continue;
                string ext = Path.GetExtension(path).ToLowerInvariant();
                if (ContentExclude.Contains(ext)) continue;
                yield return path;
            }
        }

        private static void CheckIsolation(List<Finding> f)
        {
            foreach (SceneCatalog.GameEntry g in SceneCatalog.Games)
            {
                string own = g.FolderPath + "/";
                var paths = ContentAssets(g.FolderPath).ToList();
                // Scene juga diperiksa dependensinya.
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(g.ScenePath) != null) paths.Add(g.ScenePath);

                foreach (string path in paths)
                {
                    foreach (string dep in AssetDatabase.GetDependencies(path, true))
                    {
                        if (!dep.StartsWith("Assets/") || dep.StartsWith(own)) continue;
                        if (dep.StartsWith("Assets/Hub/Scripts/")) continue; // kode infrastruktur bersama (diizinkan)
                        Add(f, Level.Error, "ISO-001",
                            $"{path} bergantung pada '{dep}' di luar folder {g.Folder}. Aset wajib terisolasi per game.");
                    }
                }
            }

            foreach (string path in ContentAssets("Assets/Hub").Concat(new[] { SceneCatalog.HubScenePath })
                         .Where(p => AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(p) != null))
            {
                foreach (string dep in AssetDatabase.GetDependencies(path, true))
                {
                    if (dep.StartsWith("Assets/Games/"))
                        Add(f, Level.Error, "ISO-002", $"Hub ({path}) memakai aset milik game: {dep}");
                }
            }

            // Folder liar di Assets/ yang bukan milik siapa pun.
            string[] allowedRoots = { "Assets/Games", "Assets/Hub", "Assets/Editor" };
            foreach (string sub in AssetDatabase.GetSubFolders("Assets"))
                if (!allowedRoots.Contains(sub))
                    Add(f, Level.Warning, "ISO-003", $"Folder tidak bertuan di Assets/: {sub} (aset bersama melanggar aturan isolasi).");
        }

        // ---------------------------------------------------------- Duplicates
        private static void CheckDuplicates(List<Finding> f)
        {
            var byHash = new Dictionary<string, List<string>>();
            foreach (string root in new[] { "Assets/Games", "Assets/Hub" })
            {
                foreach (string path in ContentAssets(root))
                {
                    var info = new FileInfo(path);
                    if (!info.Exists || info.Length == 0) continue;
                    string hash;
                    using (var sha = SHA256.Create())
                    using (FileStream stream = File.OpenRead(path))
                        hash = BitConverter.ToString(sha.ComputeHash(stream));
                    if (!byHash.TryGetValue(hash, out List<string> list)) byHash[hash] = list = new List<string>();
                    list.Add(path);
                }
            }

            foreach (List<string> group in byHash.Values.Where(l => l.Count > 1))
            {
                var owners = group.Select(OwnerOf).Distinct().ToList();
                if (owners.Count > 1)
                    Add(f, Level.Error, "DUP-001",
                        "Konten identik dipakai lintas game (menyalin/ganti nama tetap melanggar): " + string.Join("  <->  ", group));
            }
        }

        private static string OwnerOf(string path)
        {
            string[] parts = path.Split('/');
            return parts.Length >= 3 && parts[1] == "Games" ? parts[2] : parts.Length >= 2 ? parts[1] : "";
        }

        // -------------------------------------------------------- Import limits
        private static void CheckImportLimits(List<Finding> f)
        {
            string[] roots = SceneCatalog.Games.Select(g => g.FolderPath).Concat(new[] { "Assets/Hub" })
                .Where(AssetDatabase.IsValidFolder).ToArray();
            if (roots.Length == 0) return;

            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", roots))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var imp = AssetImporter.GetAtPath(path) as TextureImporter;
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (imp == null || tex == null) continue;

                int max = Mathf.Max(tex.width, tex.height);
                if (max > TextureHardMax || imp.maxTextureSize > TextureHardMax)
                    Add(f, Level.Error, "TEX-001", $"{path}: {tex.width}x{tex.height} (maxTextureSize {imp.maxTextureSize}) melebihi {TextureHardMax}px.");
                else if (max > TextureSoftMax)
                    Add(f, Level.Warning, "TEX-002", $"{path}: {tex.width}x{tex.height} > {TextureSoftMax}px (hanya boleh untuk background luas).");
            }

            foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", roots))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string ext = Path.GetExtension(path).ToLowerInvariant();
                if (AudioDiscouraged.Contains(ext))
                    Add(f, Level.Warning, "AUD-001", $"{path}: gunakan {string.Join("/", AudioPreferred)} (README), bukan {ext}.");
            }

            foreach (string guid in AssetDatabase.FindAssets("t:Model", roots))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                long tris = 0;
                foreach (UnityEngine.Object o in AssetDatabase.LoadAllAssetsAtPath(path))
                {
                    if (o is Mesh mesh)
                        for (int i = 0; i < mesh.subMeshCount; i++)
                            if (mesh.GetTopology(i) == MeshTopology.Triangles)
                                tris += mesh.GetIndexCount(i) / 3;
                }
                if (tris >= ModelTriangleLimit)
                    Add(f, Level.Error, "MDL-001", $"{path}: {tris} triangles (batas < {ModelTriangleLimit}).");
            }
        }

        // ------------------------------------------------------------ Manifests
        private static void CheckManifests(List<Finding> f)
        {
            foreach (SceneCatalog.GameEntry g in SceneCatalog.Games)
            {
                if (!AssetDatabase.IsValidFolder(g.FolderPath)) continue;
                string manifest = g.FolderPath + "/README_ASSET_MANIFEST.md";
                if (!File.Exists(manifest)) { Add(f, Level.Error, "MAN-001", "Manifest aset hilang: " + manifest); continue; }

                int rows = File.ReadAllLines(manifest)
                    .Count(l => l.TrimStart().StartsWith("|") && !l.Contains("---") && !l.Contains("| Asset "));
                int contentAssets = ContentAssets(g.FolderPath).Count(p => !p.EndsWith(".asmdef"));
                if (contentAssets > 0 && rows == 0)
                    Add(f, Level.Warning, "MAN-002", $"{g.Folder}: ada {contentAssets} aset konten tetapi manifest sumber/lisensi masih kosong.");
            }
        }

        // --------------------------------------------------------------- Scenes
        private static void CheckScenes(List<Finding> f)
        {
            var paths = new List<string> { SceneCatalog.HubScenePath };
            paths.AddRange(SceneCatalog.Games.Select(g => g.ScenePath));
            paths = paths.Where(p => AssetDatabase.LoadAssetAtPath<SceneAsset>(p) != null).ToList();
            if (paths.Count == 0) return;

            SceneSetup[] previous = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                foreach (string path in paths)
                {
                    EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                    CheckOneScene(f, path, isHub: path == SceneCatalog.HubScenePath);
                }
            }
            finally
            {
                if (previous.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(previous);
                else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
        }

        private static void CheckOneScene(List<Finding> f, string path, bool isHub)
        {
            string name = Path.GetFileNameWithoutExtension(path);

            int mainCams = GameObject.FindGameObjectsWithTag("MainCamera").Count(go => go.GetComponent<Camera>() != null);
            if (mainCams != 1) Add(f, Level.Error, "SCN-001", $"{name}: harus tepat 1 Main Camera, ditemukan {mainCams}.");

            int listeners = UnityEngine.Object.FindObjectsOfType<AudioListener>().Count(a => a.enabled);
            if (listeners != 1) Add(f, Level.Error, "SCN-002", $"{name}: harus tepat 1 AudioListener aktif, ditemukan {listeners}.");

            Canvas[] canvases = UnityEngine.Object.FindObjectsOfType<Canvas>(true).Where(c => c.isRootCanvas).ToArray();
            foreach (Canvas c in canvases)
            {
                if (c.renderMode == RenderMode.WorldSpace) continue;
                var scaler = c.GetComponent<CanvasScaler>();
                if (scaler == null) { Add(f, Level.Error, "SCN-003", $"{name}: Canvas '{c.name}' tidak punya Canvas Scaler."); continue; }

                bool ok = scaler.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize
                          && scaler.referenceResolution == new Vector2(1920f, 1080f)
                          && scaler.screenMatchMode == CanvasScaler.ScreenMatchMode.MatchWidthOrHeight
                          && Mathf.Approximately(scaler.matchWidthOrHeight, 0.5f);
                if (!ok)
                    Add(f, Level.Error, "SCN-004", $"{name}: Canvas '{c.name}' wajib Scale With Screen Size, 1920x1080, Match Width Or Height, 0.5.");
            }

            bool hasUi = canvases.Length > 0 || UnityEngine.Object.FindObjectsOfType<Selectable>(true).Length > 0;
            int eventSystems = UnityEngine.Object.FindObjectsOfType<EventSystem>(true).Length;
            if (hasUi && eventSystems == 0) Add(f, Level.Error, "SCN-005", $"{name}: ada UI tetapi tidak ada EventSystem (tombol tidak bisa diklik).");
            if (eventSystems > 1) Add(f, Level.Error, "SCN-006", $"{name}: {eventSystems} EventSystem (harus 1).");

            if (isHub)
            {
                if (UnityEngine.Object.FindObjectOfType<HubManager>(true) == null)
                    Add(f, Level.Error, "SCN-007", $"{name}: HubManager tidak ada di scene.");
            }
            else if (UnityEngine.Object.FindObjectOfType<ReturnToHub>(true) == null)
            {
                Add(f, Level.Error, "SCN-008", $"{name}: tidak ada komponen ReturnToHub (tombol Back to Hub).");
            }

            foreach (Button b in UnityEngine.Object.FindObjectsOfType<Button>(true))
            {
                if (b.transition == Selectable.Transition.ColorTint && b.targetGraphic is Image img && img.color != Color.white)
                    Add(f, Level.Warning, "SCN-010",
                        $"{name}: tombol '{b.name}' memakai ColorTint dengan warna Image bukan putih; warna tampil dikalikan " +
                        "(lebih gelap dari ColorBlock). Set Image.color = putih dan atur warna di ColorBlock.");
            }

            int missing = 0;
            foreach (GameObject root in UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene().GetRootGameObjects())
                foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                    missing += GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject);
            if (missing > 0)
                Add(f, Level.Error, "SCN-009", $"{name}: {missing} komponen 'Missing Script' (biasanya .meta/asmdef hilang atau salah merge).");
        }
    }
}
#endif
