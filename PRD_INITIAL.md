# PRODUCT REQUIREMENT DOCUMENT (PRD)
## Proyek: Multiverse Arcade Unity (5-in-1 Game Hub)

* **Versi Dokumen:** 1.0.0 (Initial Baseline)
* **Tanggal Rilis:** November 2026
* **Target Engine:** Unity 2022.3.62f3 (LTS)
* **Target Platform:** Windows Standalone PC (x86_64, Target 60 FPS, RAM Budget < 4 GB)
* **Status Kerja:** BUILD (Active Scaffolding)

---

## 1. Latar Belakang & Batasan Mutlak Dosen

1. **Format Tugas Individu dalam Tim:** Setiap anggota (5 orang) merancang dan membangun 1 game yang berbeda secara mandiri. Seluruh game tersebut diintegrasikan ke dalam 1 kesatuan aplikasi yang dipusatkan pada sebuah Hub Menu utama.
2. **Aturan 100% Isolasi Aset:** Tidak boleh ada aset bersama (model, tekstur, audio, font, prefab) yang dibagi antar-game. Setiap anggota menggunakan aset yang 100% unik dan tersimpan eksklusif di dalam direktori game masing-masing.
3. **Penyatuan Terpadu:** Pengguna dapat berpindah dari Menu Hub ke game mana pun, dan kembali ke Menu Hub setelah bermain tanpa merusak audio atau performa game lain.

---

## 2. Struktur Tim & Pembagian Tugas (Role Scoping)

| Anggota Tim | Peran Utama | Judul Game | Genre / Mekanik | Folder Eksklusif |
| :--- | :--- | :--- | :--- | :--- |
| **Novendra (Venator)** | Lead Integrator / Dev | **Cyber Neon Dodger** | 3D Endless Dodger (Neon Grid, Glow Shaders, Obstacle Runtuh) | `Assets/Games/CyberNeonDodger/` |
| **Muhammad Hanif (Velmiree)** | Sys Integrator / Dev | **Dragon Hunter** | Action Combat Boss Fight (Inspirasi Monster Hunter: Attack, Dodge Roll, Boss HP & State Machine) | `Assets/Games/DragonHunter/` |
| **Azizah** | Game Developer | **Cozy Fruit Catcher** | 2D Physics Casual (Keranjang geser, tangkap buah, combo multiplier & nyawa) | `Assets/Games/CozyFruitCatcher/` |
| **Kaishan** | Game Developer | **Deep Sea Explorer** | 2.5D Submarine Survival (Navigasi bawah laut, konsumsi oksigen & baterai, obstacle karang) | `Assets/Games/DeepSeaExplorer/` |
| **Zidnii** | Game Developer | **Sky Glider** | Vertical Platformer (Melompat vertikal platform melayang, gravitasi dinamis, rekor ketinggian) | `Assets/Games/SkyGlider/` |
| **System / Bersama** | Central Architecture | **Arcade Hub (MainMenu)** | Interactive Arcade Room / Selection Hub untuk 5 portal game | `Assets/Hub/` |

---

## 3. Spesifikasi Fungsional Tiap Game

### A. Central Arcade Hub (`Assets/Hub/Scenes/MainMenu.unity`)
* **Tujuan:** Menu awal ketika aplikasi pertama kali dibuka (Index 0 di Build Settings).
* **Fitur Utama:**
  * 5 Tombol/Portal interaktif yang mengarah ke masing-masing scene game.
  * Tombol *Exit Application* untuk keluar dari game.
  * Background musik ambient santai (Arcade Lounge).

### B. Game 1: Cyber Neon Dodger (Novendra / Venator)
* **Scene:** `CyberNeonDodger_Main.unity`
* **Mekanik Inti:** Pemain mengendalikan objek pesawat neon bergerak ke kiri/kanan di jalur berkecepatan tinggi. Kecepatan meningkat secara bertahap seiring jarak tempuh. Tabrakan dengan balok rintangan memicu Game Over.
* **Aset Khas:** Wireframe neon shaders, grid bercahaya warna cyan & magenta, synthwave SFX.

### C. Game 2: Dragon Hunter (Muhammad Hanif / Velmiree)
* **Scene:** `DragonHunter_Main.unity`
* **Mekanik Inti:** Pemain berhadapan langsung dengan Boss Naga dalam arena pertarungan.
  * **Sistem Serangan:** Hunter dapat melancarkan serangan jarak dekat/jauh.
  * **Sistem Bertahan (Dodge Roll):** Menghindar dengan roll yang memiliki *invulnerability frame* (i-frame) dan menghabiskan stamina.
  * **AI Boss Naga:** Memiliki state dinamis (*Idle*, *Telegraphed Attack Windup*, *Active Attack*, *Vulnerable Stun*, dan *Enraged Mode*).
  * **Kondisi Menang/Kalah:** Menang jika HP Naga mencapai 0; Kalah jika HP Hunter habis.
* **Aset Khas:** Model 3D monster naga, efek semburan api, pedang/panah hunter, audio raungan monster epik.

### D. Game 3: Cozy Fruit Catcher (Azizah)
* **Scene:** `CozyFruitCatcher_Main.unity`
* **Mekanik Inti:** Pemain menggeser keranjang di bagian bawah layar untuk menangkap berbagai buah yang berjatuhan dari atas.
  * Setiap buah memberikan poin dasar yang dikalikan dengan streak combo.
  * Menjatuhkan buah mengurangi 1 nyawa (maksimal 3 nyawa).
  * Menangkap bom langsung memicu Game Over.
* **Aset Khas:** Sprite 2D warna pastel, karakter keranjang imut, SFX pop ceria dan musik lofi santai.

### E. Game 4: Deep Sea Explorer (Kaishan)
* **Scene:** `DeepSeaExplorer_Main.unity`
* **Mekanik Inti:** Pemain mengarahkan kapal selam menjelajah ke kedalaman laut.
  * Pengukur kedalaman bertambah seiring penyelaman.
  * Indikator oksigen dan baterai lampu sonar terus berkurang dan harus diisi ulang dengan mengambil tangki/baterai di jalan.
  * Menghindari ranjau laut dan terumbu karang tajam.
* **Aset Khas:** Model 2.5D kapal selam, efek gelembung air (particle system), efek pencahayaan gelap bawah air (vignette).

### F. Game 5: Sky Glider (Zidnii)
* **Scene:** `SkyGlider_Main.unity`
* **Mekanik Inti:** Pemain melompat ke atas melewati platform awan/batu melayang.
  * Terdapat booster updraft yang melontarkan pemain lebih tinggi.
  * Layar mengikuti posisi tertinggi pemain; jika pemain terjatuh melampaui batas bawah kamera, game berakhir.
  * Perekaman rekor ketinggian tertinggi (High Score).
* **Aset Khas:** Gaya visual langit sci-fi minimalis, platform melayang, efek partikel angin dan musik ambient ringan.

---

## 4. Standarisasi Teknis & Mitigasi Kesalahan (Technical Quality Gates)

1. **Standarisasi UI Multi-Resolusi:**
   * Setiap `Canvas` menggunakan `Canvas Scaler`: *Scale With Screen Size*, referensi *1920x1080*, match *0.5*.
   * Mengeliminasi bug UI terpotong pada laptop layar kecil (1366x768).
2. **Zero-Audio Leak & Single AudioListener:**
   * Setiap scene hanya memuat 1 kamera aktif dengan 1 `AudioListener`.
   * Skrip `ReturnToHub.cs` mematikan semua suara yang sedang berjalan dan mereset `Time.timeScale = 1f` sebelum berpindah ke `MainMenu.unity`.
3. **Automasi Pendaftaran Scene (`SceneAutoRegister.cs`):**
   * Pendaftaran scene ke `Build Settings` dilakukan secara terprogram melalui menu Unity `Tools > Multiverse > Register All Scenes to Build Settings`. Scene `MainMenu` selalu di posisi 0, diikuti 5 scene game lainnya.
4. **Git Sandbox Monorepo:**
   * Struktur folder `Assets/Games/<GameName>/` memastikan isolasi GUID `.meta` 100%, sehingga merge pull request dari 5 anggota tidak akan pernah mengalami konflik.

---

## 5. Milestone & Jadwal Implementasi

- [x] **Sprint 0: Architecture & Scaffolding (Selesai)** — Perumusan PRD, struktur folder sandbox, standarisasi C# scripts, gitignore, dan automasi build settings.
- [ ] **Sprint 1: Individual Core Gameplay** — Masing-masing anggota menyelesaikan mekanik dasar (pergerakan pemain & game loop inti) di scene lokal.
- [ ] **Sprint 2: Visual & Audio Polishing** — Pemasangan aset visual unik dan SFX di masing-masing game.
- [ ] **Sprint 3: Hub Integration & Testing** — Penggabungan seluruh branch ke `main`, pengujian transisi antar-scene, verifikasi UI di berbagai resolusi.
- [ ] **Sprint 4: Final Release** — Ekspor build standalone `.exe` dan dokumentasi akhir untuk penilaian dosen.
