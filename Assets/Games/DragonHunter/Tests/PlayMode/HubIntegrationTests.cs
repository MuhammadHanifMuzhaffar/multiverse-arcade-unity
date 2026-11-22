using System.Collections;
using Multiverse.Hub;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Multiverse.Games.DragonHunter.Tests
{
    /// <summary>
    /// Sprint 3: integrasi Dragon Hunter dengan Hub asli lewat jalur yang dipakai pemain
    /// (portal HubManager, tombol Back to Hub dengan ReturnToHub) dan pemeriksaan kebocoran state global.
    /// </summary>
    public class HubIntegrationTests
    {
        private const string GameScene = "DragonHunter_Main";
        private int frameRateBefore;
        private bool hadBest;
        private float savedBest;

        [SetUp]
        public void Remember()
        {
            frameRateBefore = Application.targetFrameRate;
            hadBest = PlayerPrefs.HasKey(DragonHunterManager.BestTimeKey);
            savedBest = PlayerPrefs.GetFloat(DragonHunterManager.BestTimeKey, 0f);
        }

        [TearDown]
        public void Restore()
        {
            Time.timeScale = 1f;
            AudioListener.pause = false;
            AudioListener.volume = 1f;
            if (hadBest) PlayerPrefs.SetFloat(DragonHunterManager.BestTimeKey, savedBest);
            else PlayerPrefs.DeleteKey(DragonHunterManager.BestTimeKey);
        }

        private static IEnumerator WaitForScene(string name, float timeout = 10f)
        {
            float end = Time.realtimeSinceStartup + timeout;
            while (SceneManager.GetActiveScene().name != name && Time.realtimeSinceStartup < end) yield return null;
            Assert.AreEqual(name, SceneManager.GetActiveScene().name, "Scene tidak termuat: " + name);
            yield return null; // Awake/Start
            yield return null;
        }

        private static IEnumerator EnterDragonHunterFromHub()
        {
            SceneManager.LoadScene(SceneCatalog.HubSceneName);
            yield return WaitForScene(SceneCatalog.HubSceneName);

            var hub = Object.FindObjectOfType<HubManager>();
            Assert.IsNotNull(hub, "HubManager di MainMenu");
            hub.LoadDragonHunter();
            yield return WaitForScene(GameScene);

            Object.FindObjectOfType<HunterInput>().enabled = false;
        }

        private static ReturnToHub BackButtonIn(GameObject panel)
        {
            var back = panel.GetComponentInChildren<ReturnToHub>(true);
            Assert.IsNotNull(back, panel.name + " harus punya tombol Back to Hub");
            return back;
        }

        private void AssertCleanHubState()
        {
            Assert.AreEqual(SceneCatalog.HubSceneName, SceneManager.GetActiveScene().name);
            Assert.AreEqual(1f, Time.timeScale, "timeScale harus kembali 1");
            Assert.IsFalse(AudioListener.pause, "AudioListener.pause bocor ke Hub");
            Assert.AreEqual(1f, AudioListener.volume, 0.0001f);
            Assert.IsFalse(DragonHunterManager.CursorLockRequested, "Kursor harus dilepas saat meninggalkan Dragon Hunter");
            Assert.AreEqual(CursorLockMode.None, Cursor.lockState, "Kursor harus terlepas di Hub");
            Assert.AreEqual(frameRateBefore, Application.targetFrameRate, "targetFrameRate dipulihkan");
            Assert.AreEqual(1, Object.FindObjectsOfType<AudioListener>().Length, "Tepat 1 AudioListener di Hub");

            Assert.IsNull(Object.FindObjectOfType<DragonHunterManager>(), "Objek Dragon Hunter tertinggal (DontDestroyOnLoad?)");
            Assert.IsNull(Object.FindObjectOfType<DragonHunterAudio>());
            Assert.IsNull(Object.FindObjectOfType<HunterArrow>());
            foreach (AudioSource s in Object.FindObjectsOfType<AudioSource>())
                Assert.IsFalse(s.isPlaying, "Audio masih berbunyi di Hub: " + s.name);
        }

        [UnityTest]
        public IEnumerator Hub_ToDragonHunter_AndBack_LeavesNoState()
        {
            yield return EnterDragonHunterFromHub();
            var manager = Object.FindObjectOfType<DragonHunterManager>();
            Assert.AreEqual(DragonHunterManager.BattlePhase.Fighting, manager.Phase);
            Assert.AreEqual(60, Application.targetFrameRate);
            Assert.IsTrue(Object.FindObjectOfType<DragonHunterAudio>().MusicNormal.isPlaying);

            // Tembakkan panah lalu langsung keluar: panah yang masih terbang tidak boleh terbawa ke Hub.
            Object.FindObjectOfType<HunterCombat>().TryShootArrow();
            manager.Pause();
            BackButtonIn(manager.PausePanel).ReturnToMainMenu();
            yield return WaitForScene(SceneCatalog.HubSceneName);

            AssertCleanHubState();
        }

        [UnityTest]
        public IEnumerator BackToHub_FromPause_RestoresTimeAndAudio()
        {
            yield return EnterDragonHunterFromHub();
            var manager = Object.FindObjectOfType<DragonHunterManager>();
            manager.Pause();
            Assert.AreEqual(0f, Time.timeScale);
            Assert.IsTrue(AudioListener.pause);

            // Klik tombol sungguhan (persistent listener hasil scene builder), bukan memanggil method langsung.
            Button button = BackButtonIn(manager.PausePanel).GetComponent<Button>();
            button.onClick.Invoke();
            yield return WaitForScene(SceneCatalog.HubSceneName);

            AssertCleanHubState();
        }

        [UnityTest]
        public IEnumerator BackToHub_FromVictoryPanel()
        {
            yield return EnterDragonHunterFromHub();
            var manager = Object.FindObjectOfType<DragonHunterManager>();
            Object.FindObjectOfType<DragonBoss>().ReceiveHit(99999f, Vector3.zero, true);
            yield return new WaitForSecondsRealtime(2f);
            Assert.IsTrue(manager.VictoryPanel.activeSelf);

            BackButtonIn(manager.VictoryPanel).GetComponent<Button>().onClick.Invoke();
            yield return WaitForScene(SceneCatalog.HubSceneName);
            AssertCleanHubState();
        }

        [UnityTest]
        public IEnumerator BackToHub_FromDefeatPanel()
        {
            yield return EnterDragonHunterFromHub();
            var manager = Object.FindObjectOfType<DragonHunterManager>();
            var hunter = Object.FindObjectOfType<HunterController>();
            while (!hunter.IsDead)
            {
                hunter.IFrames.Clear();
                hunter.ReceiveDamage(60f, Vector3.zero);
            }
            yield return new WaitForSecondsRealtime(2f);
            Assert.IsTrue(manager.DefeatPanel.activeSelf);

            BackButtonIn(manager.DefeatPanel).GetComponent<Button>().onClick.Invoke();
            yield return WaitForScene(SceneCatalog.HubSceneName);
            AssertCleanHubState();
        }

        [UnityTest]
        public IEnumerator Retry_ReloadsFreshQuest()
        {
            yield return EnterDragonHunterFromHub();
            var manager = Object.FindObjectOfType<DragonHunterManager>();
            Object.FindObjectOfType<DragonBoss>().ReceiveHit(300f, Vector3.zero, false);
            manager.Pause();
            manager.Retry();
            yield return WaitForScene(GameScene);

            var fresh = Object.FindObjectOfType<DragonHunterManager>();
            Assert.AreNotSame(manager, fresh);
            Assert.AreEqual(DragonHunterManager.BattlePhase.Fighting, fresh.Phase);
            Assert.AreEqual(1f, Time.timeScale);
            Assert.IsFalse(AudioListener.pause);
            yield return null;
            Assert.IsTrue(DragonHunterManager.CursorLockRequested, "Manager lama tidak boleh melepas kursor quest baru");
            Assert.AreEqual(60, Application.targetFrameRate);
            DragonBoss dragon = Object.FindObjectOfType<DragonBoss>();
            Assert.AreEqual(dragon.Health.Max, dragon.Health.Current);
        }

        [UnityTest]
        public IEnumerator RepeatedRoundTrips_DoNotLeakMeshesOrMaterials()
        {
            int meshesAfterFirst = 0, materialsAfterFirst = 0;
            for (int i = 0; i < 3; i++)
            {
                yield return EnterDragonHunterFromHub();
                // Picu telegraf dan panah agar mesh runtime ikut dibuat.
                var hunter = Object.FindObjectOfType<HunterController>();
                var cc = hunter.GetComponent<CharacterController>();
                cc.enabled = false;
                hunter.transform.position = new Vector3(0f, 0.05f, 2f);
                cc.enabled = true;
                Object.FindObjectOfType<HunterCombat>().TryShootArrow();
                yield return new WaitForSeconds(3f);

                Object.FindObjectOfType<DragonHunterManager>().Pause();
                BackButtonIn(Object.FindObjectOfType<DragonHunterManager>().PausePanel).ReturnToMainMenu();
                yield return WaitForScene(SceneCatalog.HubSceneName);
                yield return Resources.UnloadUnusedAssets();

                int meshes = Resources.FindObjectsOfTypeAll<Mesh>().Length;
                int materials = Resources.FindObjectsOfTypeAll<Material>().Length;
                if (i == 0) { meshesAfterFirst = meshes; materialsAfterFirst = materials; }
                else
                {
                    Assert.LessOrEqual(meshes, meshesAfterFirst + 2, $"Mesh bertambah tiap putaran ({meshesAfterFirst} -> {meshes})");
                    Assert.LessOrEqual(materials, materialsAfterFirst + 2, $"Material bertambah tiap putaran ({materialsAfterFirst} -> {materials})");
                }
            }
        }

        [UnityTest]
        public IEnumerator AllFiveGames_ReachableFromHub_AndReturn()
        {
            // Dragon Hunter tidak boleh merusak navigasi game lain: muat tiap game lewat Hub lalu kembali.
            foreach (SceneCatalog.GameEntry game in SceneCatalog.Games)
            {
                SceneManager.LoadScene(SceneCatalog.HubSceneName);
                yield return WaitForScene(SceneCatalog.HubSceneName);
                Object.FindObjectOfType<HubManager>().LoadSceneByName(game.SceneName);
                yield return WaitForScene(game.SceneName);

                var back = Object.FindObjectOfType<ReturnToHub>(true);
                Assert.IsNotNull(back, game.SceneName + " punya ReturnToHub");
                back.ReturnToMainMenu();
                yield return WaitForScene(SceneCatalog.HubSceneName);
                Assert.AreEqual(1f, Time.timeScale, game.SceneName);
                Assert.IsFalse(AudioListener.pause, game.SceneName);
            }
        }
    }
}
