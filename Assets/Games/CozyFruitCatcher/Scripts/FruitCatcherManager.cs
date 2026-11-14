using UnityEngine;
using UnityEngine.UI;

namespace Multiverse.Games.CozyFruitCatcher
{
    /// <summary>
    /// Game Manager untuk Cozy Fruit Catcher (PIC: Azizah).
    /// Mengelola penangkapan buah, sistem combo multiplier, batas kesalahan (nyawa), dan skor.
    /// </summary>
    public class FruitCatcherManager : MonoBehaviour
    {
        [Header("Statistik Permainan")]
        [SerializeField] private int maxLives = 3;
        [SerializeField] private int currentLives;
        [SerializeField] private int score = 0;
        [SerializeField] private int comboStreak = 0;
        [SerializeField] private float comboMultiplier = 1.0f;

        [Header("UI Referensi")]
        [SerializeField] private Text scoreText;
        [SerializeField] private Text comboText;
        [SerializeField] private Text livesText;
        [SerializeField] private GameObject gameOverPanel;

        private bool isGameOver = false;
        public bool IsGameOver => isGameOver;

        private void Start()
        {
            currentLives = maxLives;
            score = 0;
            comboStreak = 0;
            comboMultiplier = 1.0f;
            isGameOver = false;

            UpdateUI();
            if (gameOverPanel != null) gameOverPanel.SetActive(false);
        }

        public void CatchFruit(int basePoints)
        {
            if (isGameOver) return;

            comboStreak++;
            comboMultiplier = 1.0f + (comboStreak / 5) * 0.2f; // Naik 20% tiap kelipatan 5 combo
            int earnedPoints = Mathf.RoundToInt(basePoints * comboMultiplier);
            score += earnedPoints;

            Debug.Log($"[CozyFruitCatcher] Buah tertangkap! +{earnedPoints} (Combo x{comboMultiplier:F1})");
            UpdateUI();
        }

        public void RegisterMiss()
        {
            if (isGameOver) return;

            comboStreak = 0;
            comboMultiplier = 1.0f;
            currentLives--;

            Debug.Log($"[CozyFruitCatcher] Buah terlewat! Sisa nyawa: {currentLives}");
            UpdateUI();

            if (currentLives <= 0)
            {
                TriggerGameOver();
            }
        }

        public void CatchBomb()
        {
            if (isGameOver) return;

            currentLives = 0;
            UpdateUI();
            TriggerGameOver();
        }

        private void UpdateUI()
        {
            if (scoreText != null) scoreText.text = $"Score: {score}";
            if (comboText != null) comboText.text = comboStreak > 1 ? $"Combo x{comboMultiplier:F1}!" : "";
            if (livesText != null) livesText.text = $"Lives: {currentLives}/{maxLives}";
        }

        private void TriggerGameOver()
        {
            isGameOver = true;
            Debug.Log($"[CozyFruitCatcher] Game Over! Skor akhir: {score}");
            if (gameOverPanel != null) gameOverPanel.SetActive(true);
        }
    }
}
