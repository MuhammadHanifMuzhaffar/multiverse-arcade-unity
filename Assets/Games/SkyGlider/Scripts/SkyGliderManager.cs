using UnityEngine;
using UnityEngine.UI;

namespace Multiverse.Games.SkyGlider
{
    /// <summary>
    /// Game Manager untuk Sky Glider (PIC: Zidnii).
    /// Mengelola pendakian vertikal, rekor ketinggian maksimal, platform melayang, dan batas jatuh.
    /// </summary>
    public class SkyGliderManager : MonoBehaviour
    {
        [Header("Statistik Ketinggian")]
        [SerializeField] private float highestAltitude = 0f;
        [SerializeField] private float currentAltitude = 0f;
        [SerializeField] private Transform gliderTransform;

        [Header("Batas Bawah (Fall Boundary)")]
        [SerializeField] private float maxFallDistanceAllowed = 12f;

        [Header("UI Referensi")]
        [SerializeField] private Text altitudeText;
        [SerializeField] private Text recordText;
        [SerializeField] private GameObject fallGameOverPanel;

        private bool isGameOver = false;
        public bool IsGameOver => isGameOver;

        private void Start()
        {
            highestAltitude = 0f;
            currentAltitude = 0f;
            isGameOver = false;

            UpdateUI();
            if (fallGameOverPanel != null) fallGameOverPanel.SetActive(false);
        }

        private void Update()
        {
            if (isGameOver) return;

            if (gliderTransform != null)
            {
                currentAltitude = gliderTransform.position.y;

                if (currentAltitude > highestAltitude)
                {
                    highestAltitude = currentAltitude;
                }

                // Cek apakah pemain terjatuh terlalu jauh di bawah rekor ketinggian
                if (highestAltitude - currentAltitude > maxFallDistanceAllowed)
                {
                    TriggerGameOver();
                }
            }

            UpdateUI();
        }

        public void TriggerBoostPad(float boostVelocity)
        {
            Debug.Log($"[SkyGlider] Updraft Boost aktif! Kecepatan: +{boostVelocity}");
        }

        private void UpdateUI()
        {
            if (altitudeText != null) altitudeText.text = $"Alt: {Mathf.Max(0, Mathf.FloorToInt(currentAltitude))}m";
            if (recordText != null) recordText.text = $"Best: {Mathf.FloorToInt(highestAltitude)}m";
        }

        public void TriggerGameOver()
        {
            if (isGameOver) return;

            isGameOver = true;
            Debug.Log($"[SkyGlider] Glider Terjatuh! Rekor ketinggian: {Mathf.FloorToInt(highestAltitude)}m");
            if (fallGameOverPanel != null) fallGameOverPanel.SetActive(true);
        }
    }
}
