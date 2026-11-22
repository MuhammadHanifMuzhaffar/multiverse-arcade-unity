using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Multiverse.Games.DragonHunter
{
    /// <summary>
    /// Game Manager untuk Dragon Hunter (PIC: Muhammad Hanif / Velmiree).
    /// Mengatur alur pertarungan: fase bertarung/pause/menang/kalah, timer quest,
    /// rekor waktu tercepat, serta pemulihan state global (timeScale, audio, kursor)
    /// agar tidak bocor ke Hub atau game lain.
    /// </summary>
    public class DragonHunterManager : MonoBehaviour
    {
        public enum BattlePhase { Fighting, Paused, Victory, Defeat }

        /// <summary>Key PlayerPrefs wajib ber-prefix nama game (konvensi README).</summary>
        public const string BestTimeKey = "DragonHunter.BestClearTime";

        [Header("Referensi Gameplay")]
        [SerializeField] private HunterController hunter;
        [SerializeField] private HunterCombat combat;
        [SerializeField] private DragonBoss dragon;
        [SerializeField] private ThirdPersonCamera cameraRig;
        [SerializeField] private DragonHunterHUD hud;

        [Header("Panel UI")]
        [SerializeField] private GameObject pausePanel;
        [SerializeField] private GameObject victoryPanel;
        [SerializeField] private GameObject defeatPanel;
        [SerializeField] private Text victoryDetailText;
        [SerializeField] private Text defeatDetailText;

        [Header("Pengaturan")]
        [SerializeField] private KeyCode pauseKey = KeyCode.Escape;
        [SerializeField] private float resultPanelDelay = 1.6f;
        [SerializeField] private bool lockCursorDuringFight = true;

        private float resultShowTime = -1f;

        // Saat Retry, OnDestroy manager lama bisa terpanggil setelah Awake/Start manager baru.
        // Penghitung instance memastikan state global hanya dipulihkan saat manager terakhir hilang.
        private static int liveInstances;
        private static int frameRateBeforeGame;
        private bool newRecord;

        private BattlePhase phase = BattlePhase.Fighting;

        public BattlePhase Phase
        {
            get => phase;
            private set
            {
                if (phase == value) return;
                phase = value;
                PhaseChanged?.Invoke(value);
            }
        }

        /// <summary>Dipicu setiap fase pertarungan berubah (audio menang/kalah, dsb.).</summary>
        public event System.Action<BattlePhase> PhaseChanged;
        public float ElapsedTime { get; private set; }
        public bool IsBattleOver => Phase == BattlePhase.Victory || Phase == BattlePhase.Defeat;
        public bool IsPaused => Phase == BattlePhase.Paused;
        public bool AcceptsGameplayInput => Phase == BattlePhase.Fighting;

        public HunterController Hunter => hunter;
        public HunterCombat Combat => combat;
        public DragonBoss Dragon => dragon;
        public GameObject PausePanel => pausePanel;
        public GameObject VictoryPanel => victoryPanel;
        public GameObject DefeatPanel => defeatPanel;

        /// <summary>Rekor waktu tercepat dalam detik; 0 bila belum pernah menang.</summary>
        public static float BestClearTime => PlayerPrefs.GetFloat(BestTimeKey, 0f);

        private void Awake()
        {
            if (liveInstances++ == 0) frameRateBeforeGame = Application.targetFrameRate;
            Application.targetFrameRate = 60;

            // Mulai dari state global yang bersih, apa pun yang ditinggalkan scene sebelumnya.
            Time.timeScale = 1f;
            AudioListener.pause = false;
        }

        private void Start()
        {
            if (hunter != null) hunter.Died += HandleHunterDied;
            if (hunter != null) hunter.Damaged += HandleHunterDamaged;
            if (dragon != null) dragon.Died += HandleDragonDied;
            if (dragon != null) dragon.EnrageStarted += HandleEnrage;
            if (combat != null) combat.HitLanded += HandleHitLanded;

            if (hud != null) hud.Bind(this, hunter, dragon, cameraRig);

            SetPanel(pausePanel, false);
            SetPanel(victoryPanel, false);
            SetPanel(defeatPanel, false);

            Phase = BattlePhase.Fighting;
            ApplyCursor(locked: lockCursorDuringFight);
        }

        private void OnDestroy()
        {
            if (hunter != null) { hunter.Died -= HandleHunterDied; hunter.Damaged -= HandleHunterDamaged; }
            if (dragon != null) { dragon.Died -= HandleDragonDied; dragon.EnrageStarted -= HandleEnrage; }
            if (combat != null) combat.HitLanded -= HandleHitLanded;

            // Pengaman terakhir saat scene ditinggalkan (ReturnToHub juga mereset ini).
            if (--liveInstances > 0) return; // scene Dragon Hunter baru (Retry) sudah mengambil alih
            liveInstances = 0;
            Time.timeScale = 1f;
            AudioListener.pause = false;
            Application.targetFrameRate = frameRateBeforeGame;
            ApplyCursor(locked: false);
        }

        private void Update()
        {
            if (Input.GetKeyDown(pauseKey) && (Phase == BattlePhase.Fighting || Phase == BattlePhase.Paused))
                TogglePause();

            if (Phase == BattlePhase.Fighting)
                ElapsedTime += Time.deltaTime;

            if (resultShowTime > 0f && Time.unscaledTime >= resultShowTime)
            {
                resultShowTime = -1f;
                ShowResultPanel();
            }
        }

        // ------------------------------------------------------------------ Pause
        public void TogglePause()
        {
            if (Phase == BattlePhase.Paused) Resume();
            else Pause();
        }

        public void Pause()
        {
            if (Phase != BattlePhase.Fighting) return;
            Phase = BattlePhase.Paused;
            Time.timeScale = 0f;
            AudioListener.pause = true;
            if (hunter != null) hunter.ControlsEnabled = false;
            SetPanel(pausePanel, true);
            ApplyCursor(locked: false);
        }

        public void Resume()
        {
            if (Phase != BattlePhase.Paused) return;
            Phase = BattlePhase.Fighting;
            Time.timeScale = 1f;
            AudioListener.pause = false;
            if (hunter != null) hunter.ControlsEnabled = true;
            SetPanel(pausePanel, false);
            ApplyCursor(locked: lockCursorDuringFight);
        }

        /// <summary>Muat ulang quest (tombol Retry).</summary>
        public void Retry()
        {
            Time.timeScale = 1f;
            AudioListener.pause = false;
            SceneManager.LoadScene(gameObject.scene.name);
        }

        // ------------------------------------------------------------------ Hasil
        private void HandleDragonDied()
        {
            if (IsBattleOver) return;
            Phase = BattlePhase.Victory;
            if (hunter != null) hunter.ControlsEnabled = false;
            if (combat != null) combat.CancelAttack();
            if (cameraRig != null) cameraRig.Shake(0.4f, 1.0f);

            float best = BestClearTime;
            newRecord = best <= 0f || ElapsedTime < best;
            if (newRecord)
            {
                PlayerPrefs.SetFloat(BestTimeKey, ElapsedTime);
                PlayerPrefs.Save();
            }

            if (hud != null) hud.ShowMessage("QUEST CLEAR!", resultPanelDelay);
            resultShowTime = Time.unscaledTime + resultPanelDelay;
        }

        private void HandleHunterDied()
        {
            if (IsBattleOver) return;
            Phase = BattlePhase.Defeat;
            if (hunter != null) hunter.ControlsEnabled = false;
            if (dragon != null) dragon.Halt();
            if (hud != null) hud.ShowMessage("You fainted...", resultPanelDelay);
            resultShowTime = Time.unscaledTime + resultPanelDelay;
        }

        private void ShowResultPanel()
        {
            ApplyCursor(locked: false);

            if (Phase == BattlePhase.Victory)
            {
                if (victoryDetailText != null)
                {
                    string record = newRecord ? "NEW RECORD!" : "Best: " + DragonHunterHUD.FormatTime(BestClearTime);
                    victoryDetailText.text = "Clear time  " + DragonHunterHUD.FormatTime(ElapsedTime) + "\n" + record;
                }
                SetPanel(victoryPanel, true);
            }
            else if (Phase == BattlePhase.Defeat)
            {
                if (defeatDetailText != null)
                {
                    float hpLeft = dragon != null ? dragon.Health.Fraction * 100f : 0f;
                    defeatDetailText.text = "Dragon HP left  " + Mathf.CeilToInt(hpLeft) + "%\nTime  " + DragonHunterHUD.FormatTime(ElapsedTime);
                }
                SetPanel(defeatPanel, true);
            }
        }

        // ------------------------------------------------------------------ Feedback
        private void HandleHunterDamaged(float damage)
        {
            if (cameraRig != null) cameraRig.Shake(Mathf.Clamp(damage / 80f, 0.12f, 0.45f), 0.3f);
        }

        private void HandleHitLanded(float damage, Vector3 point, bool heavy)
        {
            if (heavy && cameraRig != null) cameraRig.Shake(0.15f, 0.15f);
        }

        private void HandleEnrage()
        {
            if (cameraRig != null) cameraRig.Shake(0.3f, 1.4f);
        }

        // ------------------------------------------------------------------ Helper
        private static void SetPanel(GameObject panel, bool visible)
        {
            if (panel != null) panel.SetActive(visible);
        }

        /// <summary>Permintaan kunci kursor terakhir (bisa diuji di batchmode yang tidak punya jendela/kursor).</summary>
        public static bool CursorLockRequested { get; private set; }

        private static void ApplyCursor(bool locked)
        {
            CursorLockRequested = locked;
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }
    }
}
