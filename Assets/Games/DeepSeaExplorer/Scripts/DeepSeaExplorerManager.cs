using UnityEngine;
using UnityEngine.UI;

namespace Multiverse.Games.DeepSeaExplorer
{
    /// <summary>
    /// Game Manager untuk Deep Sea Explorer (PIC: Kaishan).
    /// Mengelola indikator kedalaman bawah laut, konsumsi oksigen & baterai sonar, serta bahaya tekanan laut.
    /// </summary>
    public class DeepSeaExplorerManager : MonoBehaviour
    {
        [Header("Statistik Kapal Selam")]
        [SerializeField] private float maxOxygen = 100f;
        [SerializeField] private float currentOxygen;
        [SerializeField] private float oxygenDepletionRate = 2.5f;

        [SerializeField] private float maxBattery = 100f;
        [SerializeField] private float currentBattery;
        [SerializeField] private float batteryDepletionRate = 1.5f;

        [SerializeField] private float depthMeters = 0f;
        [SerializeField] private float descentSpeed = 3f;

        [Header("UI Referensi")]
        [SerializeField] private Slider oxygenSlider;
        [SerializeField] private Slider batterySlider;
        [SerializeField] private Text depthText;
        [SerializeField] private GameObject emergencyPanel;

        private bool isMissionFailed = false;
        public bool IsMissionFailed => isMissionFailed;

        private void Start()
        {
            currentOxygen = maxOxygen;
            currentBattery = maxBattery;
            depthMeters = 0f;
            isMissionFailed = false;

            UpdateUI();
            if (emergencyPanel != null) emergencyPanel.SetActive(false);
        }

        private void Update()
        {
            if (isMissionFailed) return;

            // Penurunan bertahap oksigen dan baterai
            currentOxygen = Mathf.Max(0f, currentOxygen - (oxygenDepletionRate * Time.deltaTime));
            currentBattery = Mathf.Max(0f, currentBattery - (batteryDepletionRate * Time.deltaTime));

            // Pertambahan kedalaman kapal selam
            depthMeters += descentSpeed * Time.deltaTime;

            UpdateUI();

            if (currentOxygen <= 0f)
            {
                TriggerMissionFailed("Oksigen habis di kedalaman laut.");
            }
        }

        public void CollectOxygenTank(float amount)
        {
            if (isMissionFailed) return;
            currentOxygen = Mathf.Min(maxOxygen, currentOxygen + amount);
            Debug.Log($"[DeepSeaExplorer] Oksigen dipulihkan +{amount}!");
            UpdateUI();
        }

        public void CollectBatteryPack(float amount)
        {
            if (isMissionFailed) return;
            currentBattery = Mathf.Min(maxBattery, currentBattery + amount);
            Debug.Log($"[DeepSeaExplorer] Baterai sonar terisi +{amount}!");
            UpdateUI();
        }

        public void HitObstacle(float damage)
        {
            if (isMissionFailed) return;
            currentOxygen = Mathf.Max(0f, currentOxygen - damage);
            Debug.Log($"[DeepSeaExplorer] Lambung kapal retak! Kerusakan: {damage}");
            UpdateUI();

            if (currentOxygen <= 0f)
            {
                TriggerMissionFailed("Lambung kapal hancur terbentur karang.");
            }
        }

        private void UpdateUI()
        {
            if (oxygenSlider != null) oxygenSlider.value = currentOxygen / maxOxygen;
            if (batterySlider != null) batterySlider.value = currentBattery / maxBattery;
            if (depthText != null) depthText.text = $"Depth: {Mathf.FloorToInt(depthMeters)}m";
        }

        private void TriggerMissionFailed(string reason)
        {
            isMissionFailed = true;
            Debug.Log($"[DeepSeaExplorer] Mission Failed! Alasan: {reason}");
            if (emergencyPanel != null) emergencyPanel.SetActive(true);
        }
    }
}
