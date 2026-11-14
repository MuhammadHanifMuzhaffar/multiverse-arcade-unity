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
        [SerializeField] private string hubSceneName = "MainMenu";

        public void ReturnToMainMenu()
        {
            // 1. Normalisasi Time.timeScale (mengantisipasi game yang sedang pause/game over)
            Time.timeScale = 1f;

            // 2. Reset visibilitas kursor mouse
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            // 3. Matikan seluruh audio yang sedang memutar di scene aktif agar tidak bocor
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
