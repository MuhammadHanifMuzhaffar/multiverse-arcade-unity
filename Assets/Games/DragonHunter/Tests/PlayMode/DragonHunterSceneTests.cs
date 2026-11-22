using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Multiverse.Games.DragonHunter.Tests
{
    /// <summary>
    /// Uji integrasi di scene asli. Input pemain dimatikan; aksi dipanggil lewat API publik
    /// sehingga bisa jalan di batchmode tanpa keyboard/mouse.
    /// </summary>
    public class DragonHunterSceneTests
    {
        private const string SceneName = "DragonHunter_Main";

        private DragonHunterManager manager;
        private HunterController hunter;
        private HunterCombat combat;
        private DragonBoss dragon;
        private bool hadBestTime;
        private float savedBestTime;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            hadBestTime = PlayerPrefs.HasKey(DragonHunterManager.BestTimeKey);
            savedBestTime = PlayerPrefs.GetFloat(DragonHunterManager.BestTimeKey, 0f);

            AsyncOperation op = SceneManager.LoadSceneAsync(SceneName, LoadSceneMode.Single);
            Assert.IsNotNull(op, "Scene harus terdaftar di Build Settings.");
            while (!op.isDone) yield return null;
            yield return null; // tunggu Start()

            manager = Object.FindObjectOfType<DragonHunterManager>();
            hunter = Object.FindObjectOfType<HunterController>();
            combat = Object.FindObjectOfType<HunterCombat>();
            dragon = Object.FindObjectOfType<DragonBoss>();

            var input = Object.FindObjectOfType<HunterInput>();
            if (input != null) input.enabled = false;
        }

        [TearDown]
        public void RestoreGlobals()
        {
            Time.timeScale = 1f;
            AudioListener.pause = false;
            if (hadBestTime) PlayerPrefs.SetFloat(DragonHunterManager.BestTimeKey, savedBestTime);
            else PlayerPrefs.DeleteKey(DragonHunterManager.BestTimeKey);
        }

        private void PlaceHunter(Vector3 position)
        {
            var cc = hunter.GetComponent<CharacterController>();
            cc.enabled = false;
            hunter.transform.position = position;
            hunter.FaceTowards(dragon.transform.position);
            cc.enabled = true;
        }

        [Test]
        public void Scene_IsFullyWired()
        {
            Assert.IsNotNull(manager, "GameManager");
            Assert.IsNotNull(hunter, "Hunter");
            Assert.IsNotNull(combat, "HunterCombat");
            Assert.IsNotNull(dragon, "Dragon");
            Assert.IsNotNull(Object.FindObjectOfType<AttackTelegraph>(), "Telegraph");
            Assert.IsNotNull(Object.FindObjectOfType<ThirdPersonCamera>(), "Camera rig");
            Assert.IsNotNull(Object.FindObjectOfType<DragonHunterHUD>(), "HUD");
            Assert.AreEqual(1, Object.FindObjectsOfType<AudioListener>().Length, "Tepat 1 AudioListener.");
            Assert.AreEqual(5, dragon.Attacks.Count);
            Assert.GreaterOrEqual(Object.FindObjectsOfType<DragonHurtbox>().Length, 8, "Hurtbox kepala, tubuh, ekor, kaki.");
            Assert.IsNotNull(manager.PausePanel);
            Assert.IsFalse(manager.VictoryPanel.activeSelf);
            Assert.AreEqual(DragonHunterManager.BattlePhase.Fighting, manager.Phase);
        }

        [UnityTest]
        public IEnumerator Dragon_TelegraphsThenDamagesIdleHunter()
        {
            var visited = new HashSet<DragonState>();
            dragon.StateChanged += s => visited.Add(s);
            PlaceHunter(new Vector3(0f, 0.05f, 2f));

            float startHp = hunter.Health.Current;
            float deadline = Time.time + 10f;
            while (hunter.Health.Current >= startHp && Time.time < deadline) yield return null;

            Assert.Less(hunter.Health.Current, startHp, "Naga harus menyerang Hunter yang diam.");
            Assert.IsTrue(visited.Contains(DragonState.TelegraphAttack), "Serangan wajib didahului telegraf.");
            Assert.IsTrue(visited.Contains(DragonState.Attacking));
        }

        [UnityTest]
        public IEnumerator SwordCombo_DamagesDragon()
        {
            dragon.Halt();
            PlaceHunter(new Vector3(0f, 0.05f, 2f));
            yield return null;

            float startHp = dragon.Health.Current;
            Assert.IsTrue(combat.TryLightAttack());
            yield return new WaitForSeconds(0.3f);
            float afterFirst = dragon.Health.Current;
            Assert.Less(afterFirst, startHp, "Ayunan pertama harus mengenai naga.");

            // Buffer dua input combo berikutnya.
            combat.TryLightAttack();
            yield return new WaitForSeconds(0.35f);
            combat.TryLightAttack();
            yield return new WaitForSeconds(0.6f);
            Assert.Less(dragon.Health.Current, afterFirst, "Tahap combo berikutnya juga mengenai.");
        }

        [UnityTest]
        public IEnumerator Arrow_DamagesDragonFromRange()
        {
            dragon.Halt();
            PlaceHunter(new Vector3(0f, 0.05f, -8f));
            yield return null;

            float startHp = dragon.Health.Current;
            Assert.IsTrue(combat.TryShootArrow());
            yield return new WaitForSeconds(1.0f);
            Assert.Less(dragon.Health.Current, startHp);
        }

        [UnityTest]
        public IEnumerator DodgeRoll_GivesIFrames_AndCostsStamina()
        {
            dragon.Halt();
            float stamina = hunter.Stamina.Current;
            Assert.IsTrue(hunter.TryDodge());
            Assert.AreEqual(stamina - hunter.DodgeStaminaCost, hunter.Stamina.Current, 0.01f);
            Assert.IsFalse(hunter.ReceiveDamage(50f, dragon.transform.position), "Damage dihindari saat i-frame.");
            Assert.AreEqual(hunter.Health.Max, hunter.Health.Current);
            yield return new WaitForSeconds(0.6f);
            Assert.IsTrue(hunter.ReceiveDamage(10f, dragon.transform.position));
        }

        [UnityTest]
        public IEnumerator Potion_HealsAfterDrinkTime()
        {
            dragon.Halt();
            hunter.ReceiveDamage(80f, dragon.transform.position);
            yield return new WaitForSeconds(0.7f); // lewati stun + i-frame pasca-hit
            float hp = hunter.Health.Current;
            Assert.IsTrue(hunter.TryDrinkPotion());
            Assert.AreEqual(2, hunter.PotionsLeft);
            yield return new WaitForSeconds(1.2f);
            Assert.Greater(hunter.Health.Current, hp);
        }

        [UnityTest]
        public IEnumerator Enrage_AtHalfHealth()
        {
            dragon.ReceiveHit(dragon.Health.Max * 0.55f, dragon.AimPoint, false);
            yield return null;
            Assert.IsTrue(dragon.IsEnraged);
            Assert.AreEqual(DragonState.Enraged, dragon.State);
        }

        [UnityTest]
        public IEnumerator Victory_ShowsPanel_AndSavesPrefixedRecord()
        {
            PlayerPrefs.DeleteKey(DragonHunterManager.BestTimeKey);
            dragon.ReceiveHit(99999f, dragon.AimPoint, true);
            Assert.AreEqual(DragonHunterManager.BattlePhase.Victory, manager.Phase);
            Assert.IsFalse(hunter.ControlsEnabled);
            Assert.Greater(PlayerPrefs.GetFloat(DragonHunterManager.BestTimeKey, 0f), 0f);

            yield return new WaitForSecondsRealtime(2f);
            Assert.IsTrue(manager.VictoryPanel.activeSelf);
            Assert.IsFalse(manager.DefeatPanel.activeSelf);
        }

        [UnityTest]
        public IEnumerator Defeat_StopsDragon_AndShowsPanel()
        {
            while (!hunter.IsDead)
            {
                hunter.IFrames.Clear();
                hunter.ReceiveDamage(60f, dragon.transform.position);
            }
            Assert.AreEqual(DragonHunterManager.BattlePhase.Defeat, manager.Phase);
            Assert.IsTrue(dragon.Brain.IsHalted);

            yield return new WaitForSecondsRealtime(2f);
            Assert.IsTrue(manager.DefeatPanel.activeSelf);
        }

        [UnityTest]
        public IEnumerator PauseAndResume_RestoreTimeAndAudio()
        {
            manager.Pause();
            Assert.AreEqual(0f, Time.timeScale);
            Assert.IsTrue(AudioListener.pause);
            Assert.IsTrue(manager.PausePanel.activeSelf);
            Assert.IsFalse(manager.AcceptsGameplayInput);

            yield return null;
            manager.Resume();
            Assert.AreEqual(1f, Time.timeScale);
            Assert.IsFalse(AudioListener.pause);
            Assert.IsFalse(manager.PausePanel.activeSelf);
        }
    }
}
