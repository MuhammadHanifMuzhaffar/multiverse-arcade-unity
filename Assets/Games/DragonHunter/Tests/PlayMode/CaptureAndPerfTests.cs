using System;
using System.Collections;
using System.IO;
using System.Text;
using NUnit.Framework;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Multiverse.Games.DragonHunter.Tests
{
    /// <summary>
    /// Alat bantu Sprint 3 (opsional, dilewati pada test run biasa):
    /// - DH_CAPTURE_DIR=&lt;folder&gt; : render momen-momen kunci (HUD ikut) ke PNG untuk ditinjau.
    /// - DH_PERF_FILE=&lt;file&gt;   : ukur waktu main thread, alokasi GC per frame, dan memori saat bertarung.
    /// Jalankan Unity batchmode TANPA -nographics agar GPU merender.
    /// </summary>
    public class CaptureAndPerfTests
    {
        private DragonHunterManager manager;
        private HunterController hunter;
        private HunterCombat combat;
        private DragonBoss dragon;
        private Camera cam;

        private IEnumerator Load()
        {
            AsyncOperation op = SceneManager.LoadSceneAsync("DragonHunter_Main", LoadSceneMode.Single);
            while (!op.isDone) yield return null;
            yield return null;
            manager = UnityEngine.Object.FindObjectOfType<DragonHunterManager>();
            hunter = UnityEngine.Object.FindObjectOfType<HunterController>();
            combat = UnityEngine.Object.FindObjectOfType<HunterCombat>();
            dragon = UnityEngine.Object.FindObjectOfType<DragonBoss>();
            cam = Camera.main;
            UnityEngine.Object.FindObjectOfType<HunterInput>().enabled = false;
        }

        [TearDown]
        public void Restore()
        {
            Time.timeScale = 1f;
            AudioListener.pause = false;
            PlayerPrefs.DeleteKey(DragonHunterManager.BestTimeKey);
        }

        private void PlaceHunter(Vector3 pos)
        {
            var cc = hunter.GetComponent<CharacterController>();
            cc.enabled = false;
            hunter.transform.position = pos;
            hunter.FaceTowards(dragon.transform.position);
            cc.enabled = true;
        }

        private DragonAttackDefinition Attack(string id)
        {
            foreach (DragonAttackDefinition a in dragon.Attacks) if (a.id == id) return a;
            throw new ArgumentException(id);
        }

        private IEnumerator ForceAttack(string id)
        {
            dragon.AutoAttack = false;
            float end = Time.time + 6f;
            while (!dragon.Brain.CanStartAttack && Time.time < end) yield return null;
            Assert.IsTrue(dragon.Brain.BeginAttack(Attack(id)), "Gagal memaksa jurus " + id);
        }

        // ------------------------------------------------------------------ Capture
        private static string CaptureDir => Environment.GetEnvironmentVariable("DH_CAPTURE_DIR");

        private void Capture(string name, int width, int height)
        {
            Canvas canvas = null;
            foreach (Canvas c in UnityEngine.Object.FindObjectsOfType<Canvas>())
                if (c.isRootCanvas) canvas = c;

            RenderMode oldMode = canvas.renderMode;
            var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            try
            {
                // UI overlay tidak masuk ke render kamera; pindahkan sementara ke Screen Space - Camera.
                cam.targetTexture = rt;
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = cam;
                canvas.planeDistance = 0.5f;
                Canvas.ForceUpdateCanvases();
                cam.Render();

                RenderTexture.active = rt;
                var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                tex.Apply();
                Directory.CreateDirectory(CaptureDir);
                File.WriteAllBytes(Path.Combine(CaptureDir, name + ".png"), tex.EncodeToPNG());
                UnityEngine.Object.Destroy(tex);
            }
            finally
            {
                RenderTexture.active = null;
                cam.targetTexture = null;
                canvas.renderMode = oldMode;
                rt.Release();
                UnityEngine.Object.Destroy(rt);
            }
        }

        [UnityTest]
        public IEnumerator CaptureKeyMoments()
        {
            if (string.IsNullOrEmpty(CaptureDir)) Assert.Ignore("Set DH_CAPTURE_DIR untuk menangkap screenshot.");
            yield return Load();

            yield return new WaitForSeconds(0.5f);
            Capture("01_start_1920x1080", 1920, 1080);

            // Telegraf + semburan api dari samping agar naga dan area terlihat.
            PlaceHunter(new Vector3(-7f, 0.05f, -3f));
            yield return ForceAttack("fire");
            yield return new WaitForSeconds(0.8f);
            Capture("02_fire_telegraph", 1920, 1080);
            yield return new WaitForSeconds(0.9f);
            Capture("03_fire_breath_active", 1920, 1080);

            // Combo pedang dekat naga yang sedang rentan.
            yield return new WaitForSeconds(1.2f);
            PlaceHunter(new Vector3(0f, 0.05f, 2.2f));
            combat.TryLightAttack();
            yield return new WaitForSeconds(0.14f);
            Capture("04_sword_hit_vulnerable", 1920, 1080);

            // Enraged lalu Inferno Nova.
            dragon.ReceiveHit(dragon.Health.Max * 0.55f, dragon.AimPoint, false);
            yield return new WaitForSeconds(0.6f);
            Capture("05_enraged_roar", 1920, 1080);
            PlaceHunter(new Vector3(-8f, 0.05f, -2f));
            yield return ForceAttack("nova");
            yield return new WaitForSeconds(0.9f);
            Capture("06_nova_telegraph", 1920, 1080);
            yield return new WaitForSeconds(0.32f);
            Capture("07_nova_shockwave", 1920, 1080);

            // Panel di resolusi laptop kecil dan rasio 5:4.
            manager.Pause();
            yield return null;
            Capture("08_pause_1366x768", 1366, 768);
            manager.Resume();

            dragon.ReceiveHit(99999f, dragon.AimPoint, true);
            yield return new WaitForSecondsRealtime(2.2f);
            Capture("09_victory_1280x1024", 1280, 1024);
            Capture("10_victory_1366x768", 1366, 768);
        }

        // --------------------------------------------------------------------- Perf
        [UnityTest]
        public IEnumerator MeasureCombatPerformance()
        {
            string file = Environment.GetEnvironmentVariable("DH_PERF_FILE");
            if (string.IsNullOrEmpty(file)) Assert.Ignore("Set DH_PERF_FILE untuk mengukur performa.");
            // Baseline: scene Hub (tanpa Dragon Hunter) untuk memisahkan alokasi milik editor/test runner.
            SceneManager.LoadScene("MainMenu");
            yield return null;
            yield return null;
            Application.targetFrameRate = -1;
            long baselineGc = 0;
            int baselineFrames = 0;
            using (var baseGc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame"))
            {
                float baseEnd = Time.realtimeSinceStartup + 3f;
                while (Time.realtimeSinceStartup < baseEnd)
                {
                    yield return null;
                    baselineFrames++;
                    baselineGc += System.Math.Max(0L, baseGc.LastValue);
                }
            }

            yield return Load();
            Application.targetFrameRate = -1; // ukur biaya sebenarnya, bukan batas 60 FPS

            using var mainThread = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread", 15);
            using var gcAlloc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
            using var usedMemory = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "Total Used Memory");

            PlaceHunter(new Vector3(0f, 0.05f, 1.5f));
            yield return new WaitForSeconds(0.5f); // lewati frame pemanasan

            int frames = 0;
            double sumMs = 0, maxMs = 0;
            long gcFrames = 0, gcBytes = 0, maxGc = 0;
            float end = Time.realtimeSinceStartup + 10f;
            float nextAction = 0f;
            int action = 0;
            while (Time.realtimeSinceStartup < end)
            {
                // Simulasi pemain aktif: combo, panah, dodge bergantian; naga menyerang balik.
                if (Time.time >= nextAction && !hunter.IsDead)
                {
                    switch (action++ % 4)
                    {
                        case 0: combat.TryLightAttack(); break;
                        case 1: combat.TryLightAttack(); break;
                        case 2: combat.TryShootArrow(); break;
                        case 3: hunter.TryDodge(); break;
                    }
                    nextAction = Time.time + 0.35f;
                }
                if (hunter.Health.Fraction < 0.3f) { hunter.IFrames.Clear(); hunter.Health.Heal(200f); }

                yield return null;
                frames++;
                double ms = Time.unscaledDeltaTime * 1000.0;
                sumMs += ms;
                if (ms > maxMs) maxMs = ms;
                long gc = gcAlloc.LastValue;
                if (gc > 0) { gcFrames++; gcBytes += gc; if (gc > maxGc) maxGc = gc; }
            }

            var sb = new StringBuilder();
            sb.AppendLine($"baseline_hub_avg_gc_bytes_per_frame={(double)baselineGc / System.Math.Max(1, baselineFrames):0.0}");
            sb.AppendLine($"frames={frames}");
            sb.AppendLine($"avg_frame_ms={sumMs / frames:0.00}");
            sb.AppendLine($"avg_fps={1000.0 / (sumMs / frames):0.0}");
            sb.AppendLine($"max_frame_ms={maxMs:0.00}");
            sb.AppendLine($"main_thread_last_ms={mainThread.LastValue / 1e6:0.00}");
            sb.AppendLine($"frames_with_gc_alloc={gcFrames} ({100.0 * gcFrames / frames:0.0}%)");
            sb.AppendLine($"avg_gc_bytes_per_frame={(double)gcBytes / frames:0.0}");
            sb.AppendLine($"max_gc_bytes_in_frame={maxGc}");
            sb.AppendLine($"total_used_memory_mb={usedMemory.LastValue / (1024.0 * 1024.0):0.0}");
            sb.AppendLine($"dragon_hp_left={dragon.Health.Current:0}");
            File.WriteAllText(file, sb.ToString());
            Debug.Log("[DragonHunter] Perf:\n" + sb);
        }
    }
}
