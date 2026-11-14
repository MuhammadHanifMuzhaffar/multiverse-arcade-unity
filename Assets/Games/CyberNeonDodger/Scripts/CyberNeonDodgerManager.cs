using UnityEngine;
using UnityEngine.UI;

namespace Multiverse.Games.CyberNeonDodger
{
    /// <summary>
    /// Game Manager untuk Cyber Neon Dodger (PIC: Novendra / Venator).
    /// Mengelola kecepatan terowongan neon, penambahan skor jarak, rintangan, dan status Game Over.
    /// </summary>
    public class CyberNeonDodgerManager : MonoBehaviour
    {
        [Header("Pengaturan Kecepatan & Jarak")]
        [SerializeField] private float baseSpeed = 15f;
        [SerializeField] private float maxSpeed = 40f;
        [SerializeField] private float accelerationRate = 0.5f;

        [Header("State Permainan")]
        private float currentSpeed;
        private float scoreDistance;
        private bool isGameOver = false;

        [Header("UI Referensi (Opsional)")]
        [SerializeField] private Text scoreText;
        [SerializeField] private GameObject gameOverPanel;

        public float CurrentSpeed => currentSpeed;
        public bool IsGameOver => isGameOver;

        private void Start()
        {
            currentSpeed = baseSpeed;
            scoreDistance = 0f;
            isGameOver = false;

            if (gameOverPanel != null)
                gameOverPanel.SetActive(false);
        }

        private void Update()
        {
            if (isGameOver) return;

            // Akselerasi bertahap seiring jarak tempuh
            currentSpeed = Mathf.Min(currentSpeed + (accelerationRate * Time.deltaTime), maxSpeed);
            scoreDistance += currentSpeed * Time.deltaTime;

            if (scoreText != null)
            {
                scoreText.text = $"Distance: {Mathf.FloorToInt(scoreDistance)}m";
            }
        }

        public void TriggerGameOver()
        {
            if (isGameOver) return;

            isGameOver = true;
            Debug.Log($"[CyberNeonDodger] Game Over! Jarak akhir: {Mathf.FloorToInt(scoreDistance)}m");

            if (gameOverPanel != null)
                gameOverPanel.SetActive(true);
        }

        public void RestartGame()
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene().name
            );
        }
    }
}
