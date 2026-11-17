using UnityEngine;
using UnityEngine.SceneManagement;

namespace Multiverse.Hub
{
    /// <summary>
    /// Pengelola navigasi utama di Scene Hub Arcade.
    /// Mengontrol pemindahan scene ke 5 game anggota secara presisi dan aman.
    /// Nama scene default diambil dari <see cref="SceneCatalog"/>.
    /// </summary>
    public class HubManager : MonoBehaviour
    {
        [Header("Konfigurasi Nama Scene Resmi")]
        [SerializeField] private string sceneCyberNeonDodger = SceneCatalog.Games[0].SceneName;
        [SerializeField] private string sceneDragonHunter = SceneCatalog.Games[1].SceneName;
        [SerializeField] private string sceneCozyFruitCatcher = SceneCatalog.Games[2].SceneName;
        [SerializeField] private string sceneDeepSeaExplorer = SceneCatalog.Games[3].SceneName;
        [SerializeField] private string sceneSkyGlider = SceneCatalog.Games[4].SceneName;

        private bool isLoading;

        private void Awake()
        {
            // Pulihkan state global yang mungkin ditinggalkan game sebelumnya.
            Time.timeScale = 1f;
            AudioListener.pause = false;
            AudioListener.volume = 1f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        public void LoadCyberNeonDodger() => LoadSceneByName(sceneCyberNeonDodger);
        public void LoadDragonHunter() => LoadSceneByName(sceneDragonHunter);
        public void LoadCozyFruitCatcher() => LoadSceneByName(sceneCozyFruitCatcher);
        public void LoadDeepSeaExplorer() => LoadSceneByName(sceneDeepSeaExplorer);
        public void LoadSkyGlider() => LoadSceneByName(sceneSkyGlider);

        public void LoadSceneByName(string targetSceneName)
        {
            if (isLoading) return; // cegah klik ganda memicu load dua kali

            if (string.IsNullOrEmpty(targetSceneName))
            {
                Debug.LogError("[HubManager] Nama scene target tidak boleh kosong.");
                return;
            }

            if (!Application.CanStreamedLevelBeLoaded(targetSceneName))
            {
                Debug.LogError($"[HubManager] Scene '{targetSceneName}' tidak ada di Build Settings. " +
                               "Jalankan Tools > Multiverse > Register All Scenes to Build Settings.");
                return;
            }

            isLoading = true;
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
