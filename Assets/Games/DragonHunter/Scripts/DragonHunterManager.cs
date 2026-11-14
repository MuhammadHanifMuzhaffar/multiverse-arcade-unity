using UnityEngine;
using UnityEngine.UI;

namespace Multiverse.Games.DragonHunter
{
    /// <summary>
    /// Game Manager untuk Dragon Hunter (PIC: Muhammad Hanif / Velmiree).
    /// Terinspirasi Monster Hunter World: Boss Fight dengan telegraf serangan naga,
    /// sistem stamina, dodge roll (i-frame), dan manajemen fase pertarungan.
    /// </summary>
    public class DragonHunterManager : MonoBehaviour
    {
        public enum DragonState
        {
            Idle,
            TelegraphAttack, // Indikator serangan naga (windup)
            Attacking,       // Serangan aktif (semburan api/sapuan ekor)
            Vulnerable,      // Jendela waktu naga lelah/rentan diserang
            Enraged          // Mode murka (kecepatan meningkat)
        }

        [Header("Statistik Hunter (Pemain)")]
        [SerializeField] private float maxHunterHP = 150f;
        [SerializeField] private float currentHunterHP;
        [SerializeField] private float maxStamina = 100f;
        [SerializeField] private float currentStamina;
        [SerializeField] private float staminaRegenRate = 20f;
        [SerializeField] private float dodgeStaminaCost = 25f;

        [Header("Statistik Boss Naga")]
        [SerializeField] private float maxDragonHP = 1000f;
        [SerializeField] private float currentDragonHP;
        [SerializeField] private DragonState currentDragonState = DragonState.Idle;

        [Header("UI Status")]
        [SerializeField] private Slider hunterHPSlider;
        [SerializeField] private Slider staminaSlider;
        [SerializeField] private Slider dragonHPSlider;
        [SerializeField] private Text bossStatusText;
        [SerializeField] private GameObject victoryPanel;
        [SerializeField] private GameObject defeatPanel;

        private bool isInvulnerable = false; // Efek i-frame saat dodge roll
        private bool isBattleOver = false;

        public bool IsInvulnerable => isInvulnerable;
        public bool IsBattleOver => isBattleOver;

        private void Start()
        {
            currentHunterHP = maxHunterHP;
            currentStamina = maxStamina;
            currentDragonHP = maxDragonHP;
            currentDragonState = DragonState.Idle;
            isBattleOver = false;

            UpdateUI();
            if (victoryPanel != null) victoryPanel.SetActive(false);
            if (defeatPanel != null) defeatPanel.SetActive(false);
        }

        private void Update()
        {
            if (isBattleOver) return;

            // Regenerasi stamina alami
            if (currentStamina < maxStamina)
            {
                currentStamina = Mathf.Min(currentStamina + (staminaRegenRate * Time.deltaTime), maxStamina);
                if (staminaSlider != null) staminaSlider.value = currentStamina / maxStamina;
            }
        }

        /// <summary>
        /// Aksi serang Hunter ke Boss Naga
        /// </summary>
        public void HunterAttack(float damage)
        {
            if (isBattleOver) return;

            // Jika naga dalam status Vulnerable, berikan critical multiplier 1.5x
            float finalDamage = (currentDragonState == DragonState.Vulnerable) ? damage * 1.5f : damage;
            currentDragonHP = Mathf.Max(0f, currentDragonHP - finalDamage);

            Debug.Log($"[DragonHunter] Hunter hit Dragon! Damage: {finalDamage}. Sisa HP Naga: {currentDragonHP}");
            UpdateUI();

            if (currentDragonHP <= 0f)
            {
                TriggerVictory();
            }
        }

        /// <summary>
        /// Aksi Dodge Roll Hunter (menghindari serangan dengan biaya stamina)
        /// </summary>
        public bool AttemptDodgeRoll(float iframeDuration = 0.6f)
        {
            if (isBattleOver || currentStamina < dodgeStaminaCost) return false;

            currentStamina -= dodgeStaminaCost;
            StartCoroutine(DodgeIFrameRoutine(iframeDuration));
            return true;
        }

        private System.Collections.IEnumerator DodgeIFrameRoutine(float duration)
        {
            isInvulnerable = true;
            yield return new WaitForSeconds(duration);
            isInvulnerable = false;
        }

        /// <summary>
        /// Naga memberi damage ke Hunter
        /// </summary>
        public void DamageHunter(float damage)
        {
            if (isBattleOver || isInvulnerable) return;

            currentHunterHP = Mathf.Max(0f, currentHunterHP - damage);
            Debug.Log($"[DragonHunter] Hunter terkena serangan! Sisa HP: {currentHunterHP}");
            UpdateUI();

            if (currentHunterHP <= 0f)
            {
                TriggerDefeat();
            }
        }

        public void SetDragonState(DragonState newState)
        {
            currentDragonState = newState;
            if (bossStatusText != null)
            {
                bossStatusText.text = $"Dragon: {currentDragonState}";
            }
        }

        private void UpdateUI()
        {
            if (hunterHPSlider != null) hunterHPSlider.value = currentHunterHP / maxHunterHP;
            if (dragonHPSlider != null) dragonHPSlider.value = currentDragonHP / maxDragonHP;
            if (staminaSlider != null) staminaSlider.value = currentStamina / maxStamina;
        }

        private void TriggerVictory()
        {
            isBattleOver = true;
            Debug.Log("[DragonHunter] TARGET ELIMINATED! Quest Complete (Victory).");
            if (victoryPanel != null) victoryPanel.SetActive(true);
        }

        private void TriggerDefeat()
        {
            isBattleOver = true;
            Debug.Log("[DragonHunter] HUNTER FAINTED! Quest Failed.");
            if (defeatPanel != null) defeatPanel.SetActive(true);
        }
    }
}
