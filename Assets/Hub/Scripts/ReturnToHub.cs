using UnityEngine;
using UnityEngine.SceneManagement;

namespace Multiverse.Hub
{
    /// <summary>
    /// Komponen universal 'Kembali ke Hub' yang dipasang pada tombol UI di setiap game.
    /// Membersihkan audio aktif, mengembalikan time scale, mereset kursor, dan kembali ke MainMenu.
    /// </summary>
    public class ReturnToHub : MonoBehaviour
    {
        [Header("Target Hub Scene")]
        [SerializeField] private string hubSceneName = SceneCatalog.HubSceneName;

        private bool isLeaving;

        public void ReturnToMainMenu()
        {
            if (isLeaving) return;

            // Cek lebih dulu: jangan membisukan game jika Hub tidak bisa dimuat.
            if (!Application.CanStreamedLevelBeLoaded(hubSceneName))
            {
                Debug.LogError($"[ReturnToHub] Scene '{hubSceneName}' tidak ada di Build Settings. " +
                               "Jalankan Tools > Multiverse > Register All Scenes to Build Settings.");
                return;
            }

            isLeaving = true;

            // 1. Normalisasi Time.timeScale (mengantisipasi game yang sedang pause/game over)
            Time.timeScale = 1f;

            // 2. Reset visibilitas kursor mouse
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            // 3. Reset state audio GLOBAL. AudioListener.pause/volume bersifat statis dan
            //    bertahan lintas scene; game yang mem-pause audio akan membuat Hub bisu.
            AudioListener.pause = false;
            AudioListener.volume = 1f;

            // 4. Hentikan semua AudioSource yang masih memutar (termasuk yang DontDestroyOnLoad)
            AudioSource[] activeAudioSources = FindObjectsOfType<AudioSource>();
            foreach (AudioSource source in activeAudioSources)
            {
                if (source != null && source.isPlaying)
                {
                    source.Stop();
                }
            }

            Debug.Log($"[ReturnToHub] Membersihkan audio & beralih kembali ke '{hubSceneName}'...");
            SceneManager.LoadScene(hubSceneName);
        }
    }
}
