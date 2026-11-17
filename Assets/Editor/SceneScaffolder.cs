#if UNITY_EDITOR
using System;
using System.IO;
using Multiverse.Games.CozyFruitCatcher;
using Multiverse.Games.CyberNeonDodger;
using Multiverse.Games.DeepSeaExplorer;
using Multiverse.Games.DragonHunter;
using Multiverse.Games.SkyGlider;
using Multiverse.Hub;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Multiverse.EditorTools
{
    /// <summary>
    /// Membuat scene KERANGKA (kamera + 1 AudioListener, Canvas 1920x1080 match 0.5,
    /// EventSystem, tombol Back to Hub / tombol portal Hub, game manager) untuk 6 scene resmi.
    /// TIDAK PERNAH menimpa scene yang sudah ada, sehingga aman dijalankan ulang.
    /// Warna di sini hanya placeholder datar (tanpa gradient, tanpa warna default Unity);
    /// PIC bebas menggantinya dengan palet gamenya sendiri.
    /// </summary>
    public static class SceneScaffolder
    {
        private struct Theme
        {
            public Color Bg, Surface, SurfaceHover, Text, Accent;
            public bool Orthographic;

            public Theme(string bg, string surface, string hover, string text, string accent, bool ortho)
            {
                Bg = Hex(bg); Surface = Hex(surface); SurfaceHover = Hex(hover);
                Text = Hex(text); Accent = Hex(accent); Orthographic = ortho;
            }
        }

        private static readonly Theme HubTheme = new Theme("#14161B", "#1F232B", "#2A303A", "#ECE8E1", "#E0A93B", true);

        private static Theme GameTheme(string folder)
        {
            switch (folder)
            {
                case "CyberNeonDodger": return new Theme("#0A0C11", "#141922", "#1D2430", "#E6F4F2", "#19E3D1", false);
                case "DragonHunter":    return new Theme("#17110F", "#241915", "#33231D", "#F0E6DF", "#D9552B", false);
                case "CozyFruitCatcher":return new Theme("#F7EFE4", "#EADCCB", "#E0CDB7", "#3B2F2A", "#E07B72", true);
                case "DeepSeaExplorer": return new Theme("#07202B", "#0E3342", "#14465A", "#DCEEEA", "#3FB6A8", false);
                case "SkyGlider":       return new Theme("#E4ECEF", "#CFDCE2", "#BFD0D8", "#1F2F3A", "#2F6E9E", true);
                default:                return HubTheme;
            }
        }

        [MenuItem("Tools/Multiverse/Create Missing Skeleton Scenes", false, 20)]
        public static void CreateMissingScenes()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            int created = Run();
            EditorUtility.DisplayDialog("Multiverse Scaffolder",
                created == 0 ? "Semua scene resmi sudah ada. Tidak ada yang diubah."
                             : created + " scene kerangka dibuat. Scene yang sudah ada tidak disentuh.", "OK");
        }

        /// <summary>CLI: -executeMethod Multiverse.EditorTools.SceneScaffolder.CreateMissingScenesBatch</summary>
        public static void CreateMissingScenesBatch()
        {
            try
            {
                int created = Run();
                Debug.Log($"[Multiverse] Scaffolder selesai. Scene baru: {created}");
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogError("[Multiverse] Scaffolder gagal: " + e);
                EditorApplication.Exit(1);
            }
        }

        private static int Run()
        {
            int created = 0;

            if (CreateIfMissing(SceneCatalog.HubScenePath, BuildHub)) created++;
            foreach (SceneCatalog.GameEntry game in SceneCatalog.Games)
            {
                SceneCatalog.GameEntry g = game;
                if (CreateIfMissing(g.ScenePath, scene => BuildGame(g))) created++;
            }

            AssetDatabase.Refresh();
            if (SceneAutoRegister.RegisterAllScenesQuiet())
                Debug.Log("[Multiverse] Build Settings diperbarui (MainMenu = index 0).");
            return created;
        }

        private static bool CreateIfMissing(string path, Action<string> build)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) != null)
            {
                Debug.Log("[Multiverse] Lewati (sudah ada): " + path);
                return false;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            build(path);
            EditorSceneManager.SaveScene(scene, path);
            Debug.Log("[Multiverse] Dibuat: " + path);
            return true;
        }

        // ---------------------------------------------------------------- Hub
        private static void BuildHub(string path)
        {
            Theme t = HubTheme;
            CreateCamera(t);
            CreateEventSystem();
            RectTransform canvas = CreateCanvas();

            Text title = CreateText(canvas, "Title", "MULTIVERSE ARCADE", 72, t.Text, TextAnchor.MiddleCenter);
            Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                  new Vector2(0, -70), new Vector2(1200, 100));
            Image bar = CreateImage(canvas, "AccentBar", t.Accent);
            Place(bar.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                  new Vector2(0, -185), new Vector2(240, 6));

            var hubGo = new GameObject("HubManager");
            var hub = hubGo.AddComponent<HubManager>();

            UnityAction[] actions =
            {
                hub.LoadCyberNeonDodger, hub.LoadDragonHunter, hub.LoadCozyFruitCatcher,
                hub.LoadDeepSeaExplorer, hub.LoadSkyGlider
            };

            float y = 80f;
            for (int i = 0; i < SceneCatalog.Games.Length; i++)
            {
                Button b = CreateButton(canvas, "Portal_" + SceneCatalog.Games[i].Folder,
                                        SceneCatalog.Games[i].DisplayName, t, 40);
                Place((RectTransform)b.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                      new Vector2(0.5f, 0.5f), new Vector2(0, y - i * 112f), new Vector2(720, 92));
                UnityEventTools.AddPersistentListener(b.onClick, actions[i]);
            }

            Button exit = CreateButton(canvas, "Exit", "Exit", t, 34);
            Place((RectTransform)exit.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                  new Vector2(0.5f, 0.5f), new Vector2(0, y - 5 * 112f - 24f), new Vector2(360, 76));
            UnityEventTools.AddPersistentListener(exit.onClick, hub.QuitApplication);
        }

        // --------------------------------------------------------------- Game
        private static void BuildGame(SceneCatalog.GameEntry game)
        {
            Theme t = GameTheme(game.Folder);
            CreateCamera(t);
            CreateEventSystem();
            RectTransform canvas = CreateCanvas();

            Text title = CreateText(canvas, "Title", game.DisplayName + " (scene kerangka)", 44, t.Text, TextAnchor.MiddleLeft);
            Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                  new Vector2(48, -36), new Vector2(1100, 70));
            Image bar = CreateImage(canvas, "AccentBar", t.Accent);
            Place(bar.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                  new Vector2(48, -110), new Vector2(160, 6));

            Button back = CreateButton(canvas, "BackToHub", "Back to Hub", t, 32);
            Place((RectTransform)back.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
                  new Vector2(-48, -36), new Vector2(300, 72));
            var ret = back.gameObject.AddComponent<ReturnToHub>();
            UnityEventTools.AddPersistentListener(back.onClick, ret.ReturnToMainMenu);

            var mgr = new GameObject("GameManager");
            mgr.AddComponent(ManagerType(game.Folder));
        }

        private static Type ManagerType(string folder)
        {
            switch (folder)
            {
                case "CyberNeonDodger": return typeof(CyberNeonDodgerManager);
                case "DragonHunter": return typeof(DragonHunterManager);
                case "CozyFruitCatcher": return typeof(FruitCatcherManager);
                case "DeepSeaExplorer": return typeof(DeepSeaExplorerManager);
                case "SkyGlider": return typeof(SkyGliderManager);
                default: throw new ArgumentException("Game tidak dikenal: " + folder);
            }
        }

        // ------------------------------------------------------------ Helpers
        private static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out Color c);
            return c;
        }

        private static void CreateCamera(Theme t)
        {
            var go = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = go.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor; // bukan skybox default Unity
            cam.backgroundColor = t.Bg;
            cam.orthographic = t.Orthographic;
            cam.orthographicSize = 5f;
            go.transform.position = new Vector3(0f, 0f, -10f);
            go.AddComponent<AudioListener>();
        }

        private static void CreateEventSystem()
        {
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<StandaloneInputModule>();
        }

        private static RectTransform CreateCanvas()
        {
            var go = new GameObject("Canvas");
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            go.AddComponent<GraphicRaycaster>();
            return (RectTransform)go.transform;
        }

        private static void Place(RectTransform rt, Vector2 aMin, Vector2 aMax, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = aMin;
            rt.anchorMax = aMax;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        private static Image CreateImage(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.sprite = null; // kotak datar: tanpa sprite bawaan Unity
            img.color = color;
            return img;
        }

        private static Text CreateText(Transform parent, string name, string content, int size, Color color, TextAnchor align)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var text = go.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = content;
            text.fontSize = size;
            text.color = color;
            text.alignment = align;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }

        private static Button CreateButton(Transform parent, string name, string label, Theme t, int fontSize)
        {
            Image img = CreateImage(parent, name, t.Surface);
            var button = img.gameObject.AddComponent<Button>();
            button.targetGraphic = img;

            var cb = button.colors;
            cb.normalColor = t.Surface;
            cb.highlightedColor = t.SurfaceHover;
            cb.selectedColor = t.Surface;
            cb.pressedColor = t.Accent;
            cb.disabledColor = new Color(t.Surface.r, t.Surface.g, t.Surface.b, 0.4f);
            cb.colorMultiplier = 1f;
            cb.fadeDuration = 0.08f;
            button.colors = cb;

            Text text = CreateText(img.transform, "Label", label, fontSize, t.Text, TextAnchor.MiddleCenter);
            var rt = text.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return button;
        }
    }
}
#endif
