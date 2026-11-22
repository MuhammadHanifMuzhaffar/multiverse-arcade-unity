using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Multiverse.Games.DragonHunter.Tests
{
    /// <summary>
    /// Sprint 3: memastikan seluruh elemen UI Dragon Hunter tetap berada di dalam layar dan
    /// tidak saling bertumpuk pada berbagai resolusi laptop (PRD: hindari UI terpotong di 1366x768).
    /// Layout dihitung dari anchor/offset + Canvas Scaler (Match 0.5), sama seperti Unity menghitungnya.
    /// </summary>
    public class UiLayoutTests
    {
        private const string ScenePath = "Assets/Games/DragonHunter/Scenes/DragonHunter_Main.unity";

        private static readonly Vector2Int[] Resolutions =
        {
            new Vector2Int(1920, 1080), new Vector2Int(1366, 768), new Vector2Int(1280, 720),
            new Vector2Int(1440, 900), new Vector2Int(1280, 1024), new Vector2Int(2560, 1080),
        };

        private Canvas canvas;
        private CanvasScaler scaler;

        [OneTimeSetUp]
        public void OpenScene()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            foreach (Canvas c in Object.FindObjectsOfType<Canvas>(true))
                if (c.isRootCanvas) canvas = c;
            Assert.IsNotNull(canvas);
            scaler = canvas.GetComponent<CanvasScaler>();
        }

        [OneTimeTearDown]
        public void CloseScene() => EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        /// <summary>Ukuran canvas (unit referensi) untuk resolusi layar tertentu, sesuai Canvas Scaler Match Width/Height.</summary>
        private Vector2 CanvasSize(Vector2Int screen)
        {
            Vector2 reference = scaler.referenceResolution;
            float logW = Mathf.Log(screen.x / reference.x, 2f);
            float logH = Mathf.Log(screen.y / reference.y, 2f);
            float scale = Mathf.Pow(2f, Mathf.Lerp(logW, logH, scaler.matchWidthOrHeight));
            return new Vector2(screen.x / scale, screen.y / scale);
        }

        private static Rect Resolve(RectTransform rt, Rect parent)
        {
            Vector2 min = parent.min + Vector2.Scale(rt.anchorMin, parent.size) + rt.offsetMin;
            Vector2 max = parent.min + Vector2.Scale(rt.anchorMax, parent.size) + rt.offsetMax;
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        private static Rect ResolveInCanvas(RectTransform target, Rect canvasRect)
        {
            var chain = new List<RectTransform>();
            for (Transform t = target; t != null && t.GetComponent<Canvas>() == null; t = t.parent)
                chain.Add((RectTransform)t);
            Rect r = canvasRect;
            for (int i = chain.Count - 1; i >= 0; i--) r = Resolve(chain[i], r);
            return r;
        }

        /// <summary>Area teks yang benar-benar tergambar (teks overflow bisa melebihi kotaknya).</summary>
        private static Rect DrawnTextRect(Text text, Rect box)
        {
            float w = Mathf.Max(box.width, text.preferredWidth);
            float h = Mathf.Max(box.height, text.preferredHeight);
            float x;
            switch (text.alignment)
            {
                case TextAnchor.UpperLeft: case TextAnchor.MiddleLeft: case TextAnchor.LowerLeft: x = box.xMin; break;
                case TextAnchor.UpperRight: case TextAnchor.MiddleRight: case TextAnchor.LowerRight: x = box.xMax - w; break;
                default: x = box.center.x - w * 0.5f; break;
            }
            float y;
            switch (text.alignment)
            {
                case TextAnchor.LowerLeft: case TextAnchor.LowerCenter: case TextAnchor.LowerRight: y = box.yMin; break;
                case TextAnchor.UpperLeft: case TextAnchor.UpperCenter: case TextAnchor.UpperRight: y = box.yMax - h; break;
                default: y = box.center.y - h * 0.5f; break;
            }
            return new Rect(x, y, w, h);
        }

        [Test]
        public void AllUiInsideScreen([ValueSource(nameof(Resolutions))] Vector2Int resolution)
        {
            Vector2 size = CanvasSize(resolution);
            var screen = new Rect(0f, 0f, size.x, size.y);
            var problems = new List<string>();

            foreach (Graphic g in canvas.GetComponentsInChildren<Graphic>(true))
            {
                Rect r = ResolveInCanvas(g.rectTransform, screen);
                if (g is Text text && text.text.Length > 0) r = DrawnTextRect(text, r);
                // Teks pesan kosong saat diam; ukur dengan pesan terpanjang yang dipakai HUD.
                if (g is Text msg && msg.name == "Message")
                {
                    string old = msg.text;
                    msg.text = "Dragon staggered! Strike now!";
                    r = DrawnTextRect(msg, ResolveInCanvas(g.rectTransform, screen));
                    msg.text = old;
                }
                const float tolerance = 1f;
                if (r.xMin < -tolerance || r.yMin < -tolerance || r.xMax > screen.width + tolerance || r.yMax > screen.height + tolerance)
                    problems.Add($"{g.name} ({r.xMin:0},{r.yMin:0})-({r.xMax:0},{r.yMax:0}) di luar {screen.width:0}x{screen.height:0}");
            }

            Assert.IsEmpty(problems, $"Resolusi {resolution.x}x{resolution.y}:\n" + string.Join("\n", problems));
        }

        [Test]
        public void HudBlocksDoNotOverlap([ValueSource(nameof(Resolutions))] Vector2Int resolution)
        {
            Vector2 size = CanvasSize(resolution);
            var screen = new Rect(0f, 0f, size.x, size.y);

            // Kelompok HUD utama; elemen dalam satu kelompok boleh berdekatan, antar-kelompok tidak boleh bertumpuk.
            var groups = new Dictionary<string, string[]>
            {
                { "Hunter", new[] { "HunterLabel", "HunterHP", "HunterStamina", "Potions" } },
                { "Dragon", new[] { "DragonName", "DragonHP", "DragonState" } },
                { "Timer", new[] { "Timer", "LockOn" } },
                { "Hint", new[] { "ControlsHint" } },
            };

            var bounds = new Dictionary<string, Rect>();
            foreach (KeyValuePair<string, string[]> group in groups)
            {
                Rect? acc = null;
                foreach (string name in group.Value)
                {
                    Transform t = FindDeep(canvas.transform, name);
                    Assert.IsNotNull(t, name);
                    Rect r = ResolveInCanvas((RectTransform)t, screen);
                    var text = t.GetComponent<Text>();
                    if (text != null)
                    {
                        string old = text.text;
                        if (name == "DragonState") text.text = "[ENRAGED] Winding up: Wyvern Charge"; // label terpanjang
                        r = DrawnTextRect(text, r);
                        text.text = old;
                    }
                    acc = acc.HasValue ? Rect.MinMaxRect(Mathf.Min(acc.Value.xMin, r.xMin), Mathf.Min(acc.Value.yMin, r.yMin),
                                                         Mathf.Max(acc.Value.xMax, r.xMax), Mathf.Max(acc.Value.yMax, r.yMax)) : r;
                }
                bounds[group.Key] = acc.Value;
            }

            var keys = new List<string>(bounds.Keys);
            for (int i = 0; i < keys.Count; i++)
                for (int j = i + 1; j < keys.Count; j++)
                    Assert.IsFalse(bounds[keys[i]].Overlaps(bounds[keys[j]]),
                        $"{resolution.x}x{resolution.y}: blok {keys[i]} {bounds[keys[i]]} bertumpuk dengan {keys[j]} {bounds[keys[j]]}");
        }

        private static Transform FindDeep(Transform root, string name)
        {
            if (root.name == name) return root;
            foreach (Transform child in root)
            {
                Transform found = FindDeep(child, name);
                if (found != null) return found;
            }
            return null;
        }
    }
}
