using UnityEngine;
using UnityEngine.SceneManagement;

namespace Multiverse.Hub
{
    /// <summary>
    /// Pengelola navigasi utama di Scene Hub Arcade.
    /// Mengontrol pemindahan scene ke 5 game anggota secara presisi dan aman.
    /// </summary>
    public class HubManager : MonoBehaviour
    {
        [Header("Konfigurasi Nama Scene Resmi")]
        [SerializeField] private string sceneCyberNeonDodger = "CyberNeonDodger_Main";
        [SerializeField] private string sceneDragonHunter = "DragonHunter_Main";
        [SerializeField] private string sceneCozyFruitCatcher = "CozyFruitCatcher_Main";
        [SerializeField] private string sceneDeepSeaExplorer = "DeepSeaExplorer_Main";
        [SerializeField] private string sceneSkyGlider = "SkyGlider_Main";

        private void Awake()
        {
            // Pastikan TimeScale normal saat masuk ke Hub
            Time.timeScale = 1f;
        }

        public void LoadCyberNeonDodger()
        {
            LoadSceneByName(sceneCyberNeonDodger);
        }

        public void LoadDragonHunter()
        {
            LoadSceneByName(sceneDragonHunter);
        }

        public void LoadCozyFruitCatcher()
        {
            LoadSceneByName(sceneCozyFruitCatcher);
        }

        public void LoadDeepSeaExplorer()
        {
            LoadSceneByName(sceneDeepSeaExplorer);
        }

        public void LoadSkyGlider()
        {
            LoadSceneByName(sceneSkyGlider);
        }

        public void LoadSceneByName(string targetSceneName)
        {
            if (string.IsNullOrEmpty(targetSceneName))
            {
                Debug.LogError("[HubManager] Nama scene target tidak boleh kosong.");
                return;
            }

            Debug.Log($"[HubManager] Berpindah ke scene: {targetSceneName}");
            SceneManager.LoadScene(targetSceneName);
        }

        public void QuitApplication()
        {
            Debug.Log("[HubManager] Keluar dari aplikasi Multiverse.");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
