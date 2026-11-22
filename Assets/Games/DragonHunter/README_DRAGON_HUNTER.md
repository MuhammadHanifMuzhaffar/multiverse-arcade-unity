# Dragon Hunter — Dokumen Desain & Teknis (Sprint 1–4)

PIC: Muhammad Hanif (Velmiree) · Scene: `Scenes/DragonHunter_Main.unity` · Assembly: `Multiverse.Games.DragonHunter`

Boss fight orang ketiga melawan naga **Emberscale Wyvern** di arena bundar. Terinspirasi alur boss fight (baca telegraf, dodge, hukum jendela rentan), memakai mekanik dan visual orisinal.

## 1. Kontrol (Input Manager lama)

| Tombol | Aksi |
| :-- | :-- |
| WASD | Bergerak relatif kamera |
| Shift (tahan) | Sprint (menguras stamina) |
| Space | Dodge roll: i-frame 0,32 detik, 25 stamina, bisa membatalkan recovery serangan |
| Klik kiri | Combo pedang 3 tahap (16 / 20 / 30 dmg), input di-buffer |
| Klik kanan | Serangan berat 55 dmg, 20 stamina, windup panjang |
| E | Panah 10 dmg (jarak jauh, cooldown 0,6 detik, 8 stamina), bidik otomatis bila naga di depan |
| R | Potion +55 HP (3x, 1 detik, gerak melambat, batal bila terkena) |
| Tab / klik tengah | Lock-on kamera ke naga |
| Esc | Pause (Resume / Restart Quest / Back to Hub) |

## 2. Pemenuhan PRD

| Syarat PRD | Implementasi |
| :-- | :-- |
| Serangan jarak dekat/jauh | `HunterCombat`: combo pedang + serangan berat + panah (`HunterArrow`) |
| Dodge roll ber-i-frame & stamina | `HunterController.TryDodge` + `InvulnerabilityWindow` + `StaminaPool` |
| State naga Idle / Telegraph / Attacking / Vulnerable / Enraged | `DragonStateMachine` (logika murni) + `DragonBoss` (gerak, pemilihan jurus, hit) |
| Menang jika HP naga 0, kalah jika HP Hunter 0 | `DragonHunterManager` → panel QUEST CLEAR / QUEST FAILED |

### Jurus naga (`DragonBoss.CreateDefaultAttacks`)

| Jurus | Area | Windup | Damage | Catatan |
| :-- | :-- | :-- | :-- | :-- |
| Savage Bite | Kerucut 70°, 4,6 m | 0,75 detik | 22 | Menerjang maju 1,6 m |
| Tail Sweep | Lingkaran 6,2 m | 1,0 detik | 26 | Dipilih saat Hunter di samping/belakang |
| Fire Breath | Kerucut 36°, 13 m | 1,2 detik | 11/tick | Diikuti **Vulnerable** 2,2 detik |
| Wyvern Charge | Terjangan 12 m | 0,9 detik | 30 | Untuk Hunter yang menjaga jarak |
| Inferno Nova | Lingkaran 9 m | 1,5 detik | 38 | **Hanya saat Enraged**, diikuti Vulnerable 2,5 detik |

- **Vulnerable**: damage ke naga x1,5. Terjadi setelah jurus berat atau **stagger** (akumulasi 200 damage, serangan berat dihitung x1,4).
- **Enraged** (HP ≤ 50%, sekali): raungan 1,8 detik, lalu semua fase 25% lebih cepat, gerak +30%, damage +15%, dan Inferno Nova terbuka.
- **Telegraf**: area serangan tampil di lantai (oranye) dengan isian yang membesar sampai saat serangan, lalu merah saat aktif.
- Hurtbox: kepala x1,3 (weak point), tubuh/kaki x1,0, ekor x0,8.

## 3. Arsitektur

```
Scripts/
  DragonHunterManager.cs      alur pertarungan, pause, hasil, rekor, reset state global
  Core/                       logika murni (diuji EditMode)
    HealthPool, StaminaPool, InvulnerabilityWindow, AttackGeometry,
    DragonAttackDefinition, DragonStateMachine
  Hunter/  HunterController, HunterCombat, HunterArrow, HunterInput
  Dragon/  DragonBoss, DragonHurtbox, AttackTelegraph
  Presentation/  ThirdPersonCamera, DragonHunterHUD, UIBar, HitFlash, ProceduralMeshes,
                 DragonHunterAudio, DragonHunterVfx
Editor/   DragonHunterSceneBuilder, DragonHunterMeshLibrary (asmdef Editor sendiri)
Tools~/   generate_audio.py (diabaikan Unity)
Tests/    EditMode (53 test) · PlayMode (24 test + 2 alat capture/perf opsional)
Materials/ 28 material · Models/ 8 mesh · Audio/ 23 clip · Fonts/ Cinzel (OFL)
```

- Input dipisah dari logika (`HunterInput` → API publik), sehingga PlayMode test bisa mengendalikan Hunter tanpa keyboard.
- Rekor waktu tercepat: `PlayerPrefs["DragonHunter.BestClearTime"]` (ber-prefix sesuai konvensi README).
- `Time.timeScale`, `AudioListener.pause`, kursor, dan `targetFrameRate` dipulihkan saat scene ditinggalkan.
- Tanpa paket tambahan (kamera ditulis sendiri, bukan Cinemachine). Satu-satunya paket baru di proyek: `com.unity.test-framework` (khusus editor).

## 4. Menyusun ulang scene

Scene dibangun dari kode agar mudah direproduksi dan aman dari konflik merge:

- Editor: `Tools > Multiverse > Dragon Hunter > Rebuild Scene`
- CLI: `Unity.exe -batchmode -nographics -projectPath <path> -executeMethod Multiverse.Games.DragonHunter.EditorTools.DragonHunterSceneBuilder.RebuildBatch`

Perubahan manual di scene akan **tertimpa** saat rebuild. Untuk tuning angka, ubah default di skrip atau di builder.

## 5. Pengujian

- Editor: `Window > General > Test Runner` → EditMode / PlayMode.
- CLI: `Unity.exe -batchmode -nographics -projectPath <path> -runTests -testPlatform EditMode` (atau `PlayMode`) `-testResults <file.xml>`

## 6. Sprint 2: Visual & Audio

Semua aset dibuat sendiri di proyek ini, kecuali satu font OFL. Detail sumber dan lisensi ada di `README_ASSET_MANIFEST.md`.

| Area | Isi |
| :-- | :-- |
| Model | 8 mesh low-poly faceted (`Models/`) dari `Editor/DragonHunterMeshLibrary.cs`. Naga: tubuh faceted, tanduk menyapu ke belakang, taring, duri punggung/ekor, selaput sayap bergerigi, cakar. Hunter: zirah, helm bertanduk, jubah, bilah pedang belah ketupat, tabung panah |
| VFX | Percikan dan bara saat kena (`DragonHunterVfx`), debu saat sapuan ekor/terjangan/Hunter terkena, gelombang kejut Inferno Nova, jejak ayunan pedang (TrailRenderer), jejak panah, bara melayang di arena, api dengan tekstur partikel lembut |
| SFX | 21 suara disintesis (`Tools~/generate_audio.py`): ayunan, hit ringan/berat, panah, dodge, Hunter terluka, potion, geraman telegraf, raungan Enraged, erangan naga, gigitan, sapuan ekor, semburan api (loop), hentakan terjangan, ledakan nova, kematian naga, fanfare menang/kalah, klik UI |
| Musik | Dua loop 29 detik (132 BPM, D minor: taiko, ostinato string, brass). Lapis normal dan Enraged diputar **sinkron** lalu di-crossfade daya-konstan selama 2,5 detik saat naga murka. Musik memudar saat menang/kalah |
| Font | Cinzel (SIL OFL 1.1) untuk seluruh UI Dragon Hunter |

Catatan teknis:
- Audio 3D (spatial blend 0,6) di posisi naga, audio Hunter 2D. Tetap 1 AudioListener (di kamera). Klik UI memakai `ignoreListenerPause` agar terdengar saat pause.
- Import audio diatur builder: SFX *Decompress On Load*, musik *Compressed In Memory* (agar dua lapis mulai bersamaan).
- Efek partikel memakai `Emit()` pada sistem yang sudah ada (tanpa Instantiate per hit).
- Test baru: `Sprint2AssetTests` (mesh menghadap keluar, < 10.000 tris, audio .ogg, panjang dua lapis musik identik, lisensi font) dan `DragonHunterFeedbackTests` (semua clip terpasang, crossfade Enraged, trail + percikan saat pedang kena, font Cinzel di seluruh UI, musik memudar saat menang).

## 7. Sprint 3: Integrasi Hub & Pengujian

| Area | Hasil |
| :-- | :-- |
| Hub → Dragon Hunter → Hub | `HubIntegrationTests`: masuk lewat portal `HubManager`, keluar lewat tombol Back to Hub asli (dari pause, panel menang, panel kalah). Setelah kembali: `timeScale` 1, `AudioListener.pause` false, volume 1, kursor lepas, `targetFrameRate` dipulihkan, 1 AudioListener, tidak ada objek/audio/panah Dragon Hunter yang tertinggal |
| Retry | Quest baru bersih; manager lama tidak lagi melepas kursor/`targetFrameRate` milik quest baru (bug urutan `OnDestroy` diperbaiki dengan penghitung instance) |
| Kebocoran memori | Tiga putaran Hub ↔ Dragon Hunter: jumlah Mesh/Material tidak bertambah |
| Lima game | Semua scene game bisa dimuat dari Hub dan kembali tanpa state rusak |
| UI multi-resolusi | `UiLayoutTests` di 1920x1080, 1366x768, 1280x720, 1440x900, 1280x1024, 2560x1080: semua elemen di dalam layar, blok HUD tidak bertumpuk |
| Visual | Screenshot GPU ditinjau (`CaptureAndPerfTests`, aktif dengan `DH_CAPTURE_DIR`) |
| Performa (editor, RTX 4050) | Main thread ±0,2 ms/frame saat bertarung, memori terpakai ±110 MB (target PRD: 60 FPS, < 4 GB). Alokasi GC ±80 B/frame di atas baseline editor (diukur dengan `DH_PERF_FILE`) |

Perbaikan dari temuan Sprint 3:
- Boss bar dipindah ke **tengah bawah** dengan latar gelap (sebelumnya bertumpuk dengan status Hunter di semua resolusi). Panduan kontrol dipindah ke bawah status Hunter.
- Tombol panel nyaris hitam: Image tombol kini putih, warna diatur ColorBlock (tint dikalikan dua kali).
- Kamera tidak lagi menembus tubuh naga/dinding (sphere cast, ditarik mendekat lalu kembali halus).
- Titik bidik di tengah layar dihapus (panah sudah membidik otomatis).
- `DragonBoss.AutoAttack` dan `ThirdPersonCamera.SetOrbit` untuk test/capture yang deterministik.

Cara memakai alat opsional (Unity batchmode **tanpa** `-nographics`):

```
DH_CAPTURE_DIR=<folder> Unity.exe -batchmode -projectPath <path> -runTests -testPlatform PlayMode -testFilter CaptureKeyMoments
DH_PERF_FILE=<file.txt> Unity.exe -batchmode -projectPath <path> -runTests -testPlatform PlayMode -testFilter MeasureCombatPerformance
```

## 8. Sprint 4: Rilis

- Seluruh 24 PlayMode test lulus di **player build Windows** (`-runTests -testPlatform StandaloneWindows64`), bukan hanya di Editor.
- Build rilis bersih + smoke test + paket zip ber-checksum (detail di laporan akhir).
- Laporan akhir untuk penilaian: [`docs/dragon-hunter/LAPORAN_AKHIR_DRAGON_HUNTER.md`](../../../docs/dragon-hunter/LAPORAN_AKHIR_DRAGON_HUNTER.md) (dengan screenshot di `docs/dragon-hunter/images/`).

Sisa untuk tim: build final 5-in-1 setelah keempat game lain masuk ke `main`, lalu jalankan ulang `HubIntegrationTests`.
