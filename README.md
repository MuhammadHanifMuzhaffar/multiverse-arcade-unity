# Multiverse Arcade Unity (Proyek Kolaborasi 5 Game dalam 1 Hub)

> Proyek Terintegrasi Unity 2022.3.62f3 LTS (5 Game Berbeda, 1 Hub Arcade, 100% Aset Terisolasi).

---

## 1. Pembagian Penugasan Resmi (PIC & Judul Game)

| No | Anggota (PIC) | Judul Game | Folder Kerja Eksklusif | Scene Utama | Konsep Mekanik & Visual |
| :--- | :--- | :--- | :--- | :--- | :--- |
| 1 | **Novendra (Venator)** | **Cyber Neon Dodger** | `Assets/Games/CyberNeonDodger/` | `CyberNeonDodger_Main.unity` | 3D Endless Dodger (Neon Grid, Glow Shaders, Obstacle Runtuh) |
| 2 | **Muhammad Hanif (Velmiree)** | **Dragon Hunter** | `Assets/Games/DragonHunter/` | `DragonHunter_Main.unity` | Boss Fight Combat (Inspirasi Monster Hunter: Attack, Dodge Roll i-frame, Boss HP & State) |
| 3 | **Azizah** | **Cozy Fruit Catcher** | `Assets/Games/CozyFruitCatcher/` | `CozyFruitCatcher_Main.unity` | 2D Physics Casual (Keranjang, ragam buah jatuh, sistem combo & nyawa) |
| 4 | **Kaishan** | **Deep Sea Explorer** | `Assets/Games/DeepSeaExplorer/` | `DeepSeaExplorer_Main.unity` | 2.5D Submarine Survival (Navigasi kedalaman laut, konsumsi oksigen & baterai) |
| 5 | **Zidnii** | **Sky Glider** | `Assets/Games/SkyGlider/` | `SkyGlider_Main.unity` | Vertical Platformer (Melompat vertikal, gravitasi dinamis, rekor ketinggian) |
| - | **Integrator / Shared** | **Arcade Hub (MainMenu)** | `Assets/Hub/` | `MainMenu.unity` | Menu Portal Utama untuk memilih 5 game |

---

## 2. SOP 1 Halaman untuk Anggota Tim (Alur Kerja Harian)

### A. Persiapan Awal di Laptop
1. Pastikan terpasang **Unity 2022.3.62f3** (wajib versi yang persis sama).
2. Clone repositori ke laptop masing-masing:
   ```bash
   git clone <REPO_URL>
   cd multiverse-arcade-unity
   ```
3. Buat branch kerja sendiri sesuai nama:
   ```bash
   git checkout -b game/<nama-anda>
   # Contoh: git checkout -b game/velmiree
   ```

### B. Aturan Pengerjaan & Komitmen
- **HANYA sentuh folder game milik sendiri** (`Assets/Games/<FolderAnda>/`). Dilarang keras mengedit atau memindahkan file di folder game anggota lain atau folder `Assets/Hub/`.
- Seluruh aset baru (model, sprite, audio, script) wajib disimpan di dalam folder game masing-masing.

### C. Alur Simpan & Setor (Push ke GitHub)
1. Simpan scene di Unity (`Ctrl + S`).
2. Jalankan perintah Git berikut:
   ```bash
   git add .
   git commit -m "feat: implementasi mekanik baru"
   git push origin game/<nama-anda>
   ```
3. Buat Pull Request (PR) ke branch `main` di GitHub untuk di-review oleh Integrator.

---

## 3. Empat Standarisasi Teknis Mutlak (Anti-Error)

### 1. Canvas UI Multi-Resolusi (1920x1080)
- Setiap UI Canvas wajib memiliki komponen **Canvas Scaler**:
  - `UI Scale Mode`: **Scale With Screen Size**
  - `Reference Resolution`: **X = 1920, Y = 1080**
  - `Screen Match Mode`: **Match Width Or Height**
  - `Match`: **0.5**
- Pasang anchor UI pada pojok/sisi yang relevan agar teks dan tombol tidak terpotong pada laptop beresolusi 1366x768.

### 2. Isolasi Audio & Kamera
- Setiap scene hanya boleh memiliki **1 Main Camera dengan 1 AudioListener**.
- Pada tombol *Back to Hub*, pasang script `Multiverse.Hub.ReturnToHub` yang secara otomatis:
  - Mematikan semua audio aktif agar tidak bocor ke MainMenu.
  - Mengembalikan `Time.timeScale` ke `1f` (jika game sedang pause/game over).
  - Mereset kursor mouse ke status terlihat.

### 3. Automasi Registrasi Scene ke Build Settings
- Di Unity Editor, klik menu atas: **`Tools > Multiverse > Register All Scenes to Build Settings`**.
- Tool ini otomatis:
  1. Menetapkan `Assets/Hub/Scenes/MainMenu.unity` sebagai **Index 0**.
  2. Mendaftarkan seluruh 5 scene game ke index berikutnya secara otomatis tanpa perlu drag-and-drop manual.

### 4. Batasan Ukuran Aset (Aman RAM 8 GB & Koneksi Cepat)
- Resolusi tekstur maksimal `1024x1024` (atau `2048x2048` untuk background luas).
- Format audio gunakan kompresi `.mp3` atau `.ogg`.
- Model 3D menggunakan gaya Low-Poly (< 10.000 triangles per model) agar laptop tidak lag saat playmode.

---

## 4. Alur Integrasi & Final Build
1. Integrator (Venator / Velmiree) memverifikasi semua PR telah ter-merge ke `main`.
2. Buka project di Unity Editor pada laptop Integrator.
3. Jalankan menu `Tools > Multiverse > Register All Scenes to Build Settings`.
4. Buka `File > Build Settings`, pastikan Platform **Windows / Standalone**, lalu klik **Build**.
5. File `.exe` yang dihasilkan siap dimainkan berisi 1 Hub Menu dan 5 Game utuh.
