using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Multiverse.Games.DragonHunter.Tests
{
    /// <summary>Uji Sprint 2: audio, VFX, font, dan mesh terpasang dan bereaksi terhadap gameplay.</summary>
    public class DragonHunterFeedbackTests
    {
        private DragonHunterManager manager;
        private HunterController hunter;
        private HunterCombat combat;
        private DragonBoss dragon;
        private DragonHunterAudio audio;
        private DragonHunterVfx vfx;
        private bool hadBest;
        private float savedBest;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            hadBest = PlayerPrefs.HasKey(DragonHunterManager.BestTimeKey);
            savedBest = PlayerPrefs.GetFloat(DragonHunterManager.BestTimeKey, 0f);
            AsyncOperation op = SceneManager.LoadSceneAsync("DragonHunter_Main", LoadSceneMode.Single);
            while (!op.isDone) yield return null;
            yield return null;

            manager = Object.FindObjectOfType<DragonHunterManager>();
            hunter = Object.FindObjectOfType<HunterController>();
            combat = Object.FindObjectOfType<HunterCombat>();
            dragon = Object.FindObjectOfType<DragonBoss>();
            audio = Object.FindObjectOfType<DragonHunterAudio>();
            vfx = Object.FindObjectOfType<DragonHunterVfx>();
            Object.FindObjectOfType<HunterInput>().enabled = false;
        }

        [TearDown]
        public void Restore()
        {
            Time.timeScale = 1f;
            AudioListener.pause = false;
            if (hadBest) PlayerPrefs.SetFloat(DragonHunterManager.BestTimeKey, savedBest);
            else PlayerPrefs.DeleteKey(DragonHunterManager.BestTimeKey);
        }

        [Test]
        public void Audio_AllClipsAssigned_AndMusicLayersPlaying()
        {
            Assert.IsNotNull(audio);
            foreach (var field in typeof(DragonHunterAudio.ClipSet).GetFields())
                Assert.IsNotNull(field.GetValue(audio.Clips), "Clip kosong: " + field.Name);

            Assert.IsTrue(audio.MusicNormal.isPlaying, "Musik battle diputar.");
            Assert.IsTrue(audio.MusicEnraged.isPlaying, "Lapis Enraged diputar sinkron (volume 0).");
            Assert.Greater(audio.MusicNormal.volume, 0f);
            Assert.AreEqual(0f, audio.MusicEnraged.volume, 0.001f);
            Assert.AreEqual(1, Object.FindObjectsOfType<AudioListener>().Length, "Tetap 1 AudioListener.");
        }

        [UnityTest]
        public IEnumerator Enrage_CrossfadesToEnragedMusic()
        {
            dragon.ReceiveHit(dragon.Health.Max * 0.55f, dragon.AimPoint, false);
            yield return new WaitForSeconds(3f);
            Assert.Greater(audio.MusicBlend, 0.95f);
            Assert.Greater(audio.MusicEnraged.volume, audio.MusicNormal.volume);
        }

        [UnityTest]
        public IEnumerator SwordHit_EmitsSparks_AndTrail()
        {
            dragon.Halt();
            var cc = hunter.GetComponent<CharacterController>();
            cc.enabled = false;
            hunter.transform.position = new Vector3(0f, 0.05f, 2f);
            hunter.FaceTowards(dragon.transform.position);
            cc.enabled = true;
            yield return null;

            var trail = hunter.GetComponentInChildren<TrailRenderer>();
            Assert.IsNotNull(trail, "Sword trail");
            Assert.IsFalse(trail.emitting);

            int before = vfx.SparkBursts;
            Assert.IsTrue(combat.TryLightAttack());
            yield return new WaitForSeconds(0.13f);
            Assert.IsTrue(trail.emitting, "Trail menyala saat fase aktif.");
            yield return new WaitForSeconds(0.3f);
            Assert.Greater(vfx.SparkBursts, before, "Percikan muncul saat pedang kena.");
            Assert.IsFalse(trail.emitting, "Trail mati setelah fase aktif.");
        }

        [Test]
        public void Ui_UsesBundledFont_AndCharactersUseGeneratedMeshes()
        {
            Text[] texts = Object.FindObjectsOfType<Text>(true);
            Assert.Greater(texts.Length, 5);
            foreach (Text t in texts) Assert.AreEqual("Cinzel-Variable", t.font.name, t.name);

            int custom = 0;
            foreach (MeshFilter mf in dragon.GetComponentsInChildren<MeshFilter>(true))
                if (mf.sharedMesh != null && mf.sharedMesh.name.StartsWith("DH_Mesh_")) custom++;
            Assert.Greater(custom, 30, "Naga tersusun dari mesh low-poly milik Dragon Hunter.");
        }

        [UnityTest]
        public IEnumerator Victory_FadesMusic()
        {
            dragon.ReceiveHit(99999f, dragon.AimPoint, true);
            yield return new WaitForSeconds(2f);
            Assert.AreEqual(DragonHunterManager.BattlePhase.Victory, manager.Phase);
            Assert.Less(audio.MusicNormal.volume, 0.01f);
        }
    }
}
