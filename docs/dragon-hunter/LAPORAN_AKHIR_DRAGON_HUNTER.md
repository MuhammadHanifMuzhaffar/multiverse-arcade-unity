# Laporan Akhir — Dragon Hunter

**Mata kuliah:** Pemrograman Multimedia Interaktif · **Proyek:** Multiverse Arcade Unity (5-in-1 Game Hub)
**PIC:** Muhammad Hanif Muzhaffar (Velmiree) · **Engine:** Unity 2022.3.62f3 LTS · **Platform:** Windows x86_64
**Tanggal:** 22 November 2026 · **Status:** Sprint 1–4 selesai untuk Dragon Hunter

![Awal quest](images/01_quest_start.jpg)

---

## 1. Ringkasan

Dragon Hunter adalah game *boss fight* orang ketiga. Pemain (Hunter) berhadapan dengan naga **Emberscale Wyvern** di arena bundar. Inti permainannya adalah membaca telegraf serangan naga, menghindar dengan *dodge roll* ber-i-frame, lalu menghukum naga saat ia rentan. Game ini dibangun sebagai salah satu dari lima game di Multiverse Arcade dan dibuka dari Hub (MainMenu).

| Aspek | Hasil |
| :-- | :-- |
| Kode runtime | 3.206 baris C# (20 skrip, 1 assembly `Multiverse.Games.DragonHunter`) |
| Kode editor + test | 2.927 baris C# (scene builder, generator mesh, 77 test otomatis) |
| Aset | 8 mesh, 28 material, 23 audio, 1 tekstur, 1 font — semuanya unik untuk Dragon Hunter |
| Test otomatis | 53 EditMode + 24 PlayMode (lulus di Editor **dan** di player build Windows) |
| Validator proyek | 0 error |
| Build | Windows x64, 68 MB (zip 26 MB) |

## 2. Pemenuhan PRD

| Syarat PRD (bagian 3.C) | Implementasi | Bukti |
| :-- | :-- | :-- |
| Pemain berhadapan langsung dengan Boss Naga di arena | Arena bundar berdinding, naga 5 jurus | Gambar 1–7 |
| Serangan jarak dekat/jauh | Combo pedang 3 tahap (LMB), serangan berat (RMB), panah bidik otomatis (E) | `HunterCombat`, test `SwordCombo_DamagesDragon`, `Arrow_DamagesDragonFromRange` |
| Dodge roll ber-i-frame yang memakai stamina | Roll 0,42 detik, i-frame 0,32 detik, 25 stamina, bisa membatalkan recovery | `HunterController`, test `DodgeRoll_GivesIFrames_AndCostsStamina`, `OverlappingGrants_ExtendInsteadOfCutting` |
| AI naga: Idle, Telegraphed Attack Windup, Active Attack, Vulnerable Stun, Enraged | `DragonStateMachine` (logika murni) + `DragonBoss` | 7 test `DragonStateMachineTests`, `Dragon_TelegraphsThenDamagesIdleHunter`, `Enrage_AtHalfHealth` |
| Menang jika HP naga 0, kalah jika HP Hunter habis | Panel QUEST CLEAR / QUEST FAILED + rekor waktu | `Victory_ShowsPanel_AndSavesPrefixedRecord`, `Defeat_StopsDragon_AndShowsPanel` |
| Aset khas: naga 3D, semburan api, pedang/panah, raungan | Mesh low-poly buatan sendiri, partikel api, SFX raungan & musik sintesis | Bagian 5, gambar 2–3 |

Standar teknis PRD (bagian 4):

| Standar | Status |
| :-- | :-- |
| Canvas Scaler 1920x1080, match 0,5; UI tidak terpotong di 1366x768 | Terpenuhi; diuji di 6 resolusi (`UiLayoutTests`) |
| 1 kamera + 1 AudioListener per scene | Terpenuhi (validator + test) |
| `ReturnToHub` menghentikan audio dan mereset `Time.timeScale` | Terpenuhi; diuji dari pause, panel menang, panel kalah (`HubIntegrationTests`) |
| Isolasi aset 100% per game | Terpenuhi; seluruh aset di `Assets/Games/DragonHunter/`, validator ISO/DUP 0 error |
| `PlayerPrefs` ber-prefix game | `DragonHunter.BestClearTime` |

## 3. Desain Permainan

### 3.1 Kontrol

| Tombol | Aksi |
| :-- | :-- |
| WASD | Bergerak relatif kamera |
| Shift | Sprint (menguras stamina) |
| Space | Dodge roll |
| Klik kiri / kanan | Combo pedang / serangan berat |
| E | Panah |
| R | Potion (+55 HP, 3x) |
| Tab / klik tengah | Lock-on kamera |
| Esc | Pause (Resume, Restart Quest, Back to Hub) |

### 3.2 Jurus naga

| Jurus | Area | Telegraf | Damage | Catatan |
| :-- | :-- | :-- | :-- | :-- |
| Savage Bite | Kerucut 70°, 4,6 m | 0,75 detik | 22 | Menerjang 1,6 m |
| Tail Sweep | Lingkaran 6,2 m | 1,0 detik | 26 | Saat Hunter di samping/belakang |
| Fire Breath | Kerucut 36°, 13 m | 1,2 detik | 11 per tick | Diikuti Vulnerable 2,2 detik |
| Wyvern Charge | Terjangan 12 m | 0,9 detik | 30 | Menghukum Hunter yang menjaga jarak |
| Inferno Nova | Lingkaran 9 m | 1,5 detik | 38 | Hanya saat Enraged, diikuti Vulnerable 2,5 detik |

- **Vulnerable:** damage ke naga x1,5. Terjadi setelah jurus berat atau *stagger* (akumulasi 200 damage).
- **Enraged** (HP ≤ 50%, sekali): raungan, tempo semua fase 25% lebih cepat, gerak +30%, damage +15%, Inferno Nova terbuka, musik berpindah ke lapis yang lebih intens.
- **Weak point:** kepala x1,3, tubuh/kaki x1,0, ekor x0,8.

| | |
| :-: | :-: |
| ![Telegraf Fire Breath](images/02_fire_breath_telegraph.jpg) | ![Fire Breath aktif](images/03_fire_breath_active.jpg) |
| Gambar 2. Telegraf area Fire Breath (isian membesar hingga serangan) | Gambar 3. Fire Breath aktif |
| ![Naga rentan](images/04_sword_hit_vulnerable.jpg) | ![Enraged](images/05_enraged.jpg) |
| Gambar 4. Serangan pedang saat naga Vulnerable | Gambar 5. Transisi Enraged |
| ![Telegraf Inferno Nova](images/06_inferno_nova_telegraph.jpg) | ![Gelombang kejut Nova](images/07_inferno_nova_shockwave.jpg) |
| Gambar 6. Telegraf Inferno Nova | Gambar 7. Gelombang kejut Inferno Nova |

![Quest Clear 1366x768](images/09_quest_clear_1366x768.jpg)
*Gambar 8. Panel QUEST CLEAR pada resolusi laptop 1366x768.*

## 4. Arsitektur Teknis

```
Assets/Games/DragonHunter/
  Scripts/
    DragonHunterManager.cs   alur pertarungan, pause, hasil, rekor, pemulihan state global
    Core/                    logika murni tanpa MonoBehaviour (diuji EditMode)
      HealthPool, StaminaPool, InvulnerabilityWindow, AttackGeometry,
      DragonAttackDefinition, DragonStateMachine
    Hunter/                  HunterController, HunterCombat, HunterArrow, HunterInput
    Dragon/                  DragonBoss, DragonHurtbox, AttackTelegraph
    Presentation/            ThirdPersonCamera, DragonHunterHUD, UIBar, HitFlash,
                             ProceduralMeshes, DragonHunterAudio, DragonHunterVfx
  Editor/                    DragonHunterSceneBuilder, DragonHunterMeshLibrary
  Tools~/generate_audio.py   generator SFX & musik (diabaikan Unity)
  Tests/EditMode, Tests/PlayMode
```

Keputusan desain utama:

1. **Logika dipisah dari MonoBehaviour.** Aturan HP, stamina, i-frame, geometri serangan, dan timing state naga berada di kelas C# murni, sehingga bisa diuji secara deterministik.
2. **Input dipisah dari aksi.** `HunterInput` membaca Input Manager lalu memanggil API publik (`TryDodge`, `TryLightAttack`, dst.). Test PlayMode mengendalikan Hunter lewat API yang sama tanpa keyboard.
3. **Gameplay → event → presentasi.** Audio, VFX, dan HUD hanya mendengarkan event (`Damaged`, `AttackActivated`, `PhaseChanged`, dst.), sehingga logika permainan tidak bergantung pada aset.
4. **Scene dibangun dari kode** (`Tools > Multiverse > Dragon Hunter > Rebuild Scene`). Scene bisa direproduksi kapan saja dan aman dari konflik merge file `.unity`.
5. **Tanpa paket tambahan.** Kamera orbit, lock-on, dan tabrakan kamera ditulis sendiri (bukan Cinemachine). Satu-satunya paket baru di proyek adalah Unity Test Framework (khusus editor).
6. **Ramah memori.** Teks HUD hanya diperbarui saat nilainya berubah, efek partikel memakai `Emit()` pada sistem yang sudah ada, dan query fisika memakai varian `NonAlloc`.

## 5. Aset dan Lisensi

Seluruh aset berada di folder Dragon Hunter dan tercatat di `Assets/Games/DragonHunter/README_ASSET_MANIFEST.md`.

| Jenis | Jumlah | Sumber | Lisensi |
| :-- | :-- | :-- | :-- |
| Mesh low-poly (naga, Hunter, pedang, jubah, gelombang kejut) | 8 | Dibangkitkan kode `DragonHunterMeshLibrary.cs` | Karya sendiri |
| Material | 28 | Dibuat oleh scene builder (palet ember/arang) | Karya sendiri |
| SFX | 21 | Disintesis dari nol (`generate_audio.py`: osilator, noise, filter) | Karya sendiri |
| Musik battle (lapis normal & Enraged, 29 detik, 132 BPM) | 2 | Dikomposisi dan disintesis (`generate_audio.py`) | Karya sendiri |
| Tekstur partikel 64x64 | 1 | Dibangkitkan kode | Karya sendiri |
| Font Cinzel | 1 | github.com/google/fonts (`ofl/cinzel`) | SIL Open Font License 1.1 (disertakan) |

## 6. Pengujian

### 6.1 Ringkasan test otomatis

| Suite | Jumlah | Isi |
| :-- | :-- | :-- |
| EditMode — aturan inti | 20 | HP, stamina, i-frame, geometri serangan, definisi jurus, state machine naga |
| EditMode — aset | 21 | Mesh menghadap keluar & < 10.000 tris, audio `.ogg`, panjang dua lapis musik identik, lisensi font, batas tekstur |
| EditMode — layout UI | 12 | Semua UI di dalam layar dan blok HUD tidak bertumpuk pada 1920x1080, 1366x768, 1280x720, 1440x900, 1280x1024, 2560x1080 |
| PlayMode — gameplay | 10 | Telegraf → serangan, combo, panah, dodge, potion, Enraged, menang, kalah, pause |
| PlayMode — audio/VFX/kamera | 7 | Clip terpasang, crossfade musik, trail + percikan, font, kamera tidak menembus naga, warna tombol |
| PlayMode — integrasi Hub | 7 | Hub → game → Hub dari pause/menang/kalah, tanpa kebocoran state; Retry; tanpa kebocoran mesh/material; kelima game dapat dibuka |
| **Total** | **77** | Lulus di Unity Editor dan di **player build Windows** (`-testPlatform StandaloneWindows64`) |

### 6.2 Bug yang ditemukan oleh pengujian dan sudah diperbaiki

| Sprint | Temuan | Perbaikan |
| :-- | :-- | :-- |
| 2 | Mesh kerucut dan frustum menghadap ke dalam, sehingga tanduk, cakar, ekor, dan kaki tak terlihat | Urutan segitiga dibalik; dijaga test winding |
| 3 | Boss bar bertumpuk dengan status Hunter di semua resolusi | Boss bar dipindah ke tengah bawah dengan latar gelap |
| 3 | Retry: manager lama bisa melepas kursor / mereset frame rate quest baru | Penghitung instance pada pemulihan state global |
| 3 | Tombol panel tampil nyaris hitam (tint dikalikan dua kali) | Image tombol putih; bug yang sama di Hub diperbaiki (PR #5) |
| 3 | Kamera menembus tubuh naga | Sphere cast, kamera ditarik mendekat lalu kembali halus |

### 6.3 Performa

Diukur di Unity Editor (batchmode dengan GPU NVIDIA GeForce RTX 4050 Laptop) selama 10 detik pertarungan aktif:

| Metrik | Hasil | Target PRD |
| :-- | :-- | :-- |
| Waktu main thread | ±0,21 ms/frame | 60 FPS (16,7 ms/frame) |
| Memori terpakai | ±109 MB | < 4 GB |
| Alokasi GC | ±80 B/frame di atas baseline editor | — |

Angka ini menunjukkan ruang performa yang sangat lega. Pengukuran di laptop spesifikasi rendah belum dilakukan.

## 7. Build Rilis

| Item | Nilai |
| :-- | :-- |
| Output | `Builds/Windows/MultiverseArcade.exe` (Windows x64, Mono) |
| Ukuran | 68 MB (zip 26,1 MB) |
| Paket | `Builds/MultiverseArcade_Windows_DragonHunterRC_20261122.zip` |
| SHA-256 | `e4436200aa6531f9773c28a82905328351b22dcafba3c7a5bbc9f72b7e20dc13` |
| Smoke test | Player berjalan 15 detik tanpa exception |
| Test assembly di build | Tidak ikut (dibatasi `UNITY_INCLUDE_TESTS`) |

Cara membangun ulang: buka proyek di Unity 2022.3.62f3 → `Tools > Multiverse > Build Windows (Integrator)`. Folder `Builds/` tidak masuk Git.

## 8. Riwayat Pengembangan

| Sprint | PR | Isi |
| :-- | :-- | :-- |
| 0 | #1 | Fondasi proyek: compile bersih, asmdef per game, scene kerangka, validator |
| 1 | #2 | Gameplay inti boss fight |
| 2 | #3 | Model low-poly, VFX, SFX, musik, font |
| 3 | #4, #5 | Integrasi Hub, layout multi-resolusi, kamera, perbaikan tombol Hub |
| 4 | (PR ini) | Verifikasi di player build, paket rilis, laporan akhir |

## 9. Keterbatasan dan Pengembangan Lanjutan

- Visual memakai gaya low-poly flat-shaded tanpa animasi rangka (skeletal); gerakan dibuat lewat transformasi bagian tubuh.
- Tanduk, duri, dan sayap naga tidak memiliki collider, sehingga sesekali dapat menghalangi kamera dari jarak sangat dekat.
- Keseimbangan tingkat kesulitan belum diuji dengan banyak pemain; seluruh angka dapat di-tuning di `DragonBoss.CreateDefaultAttacks` dan komponen Hunter.
- Kualitas audio dan rasa kontrol perlu dinilai melalui playtest manual.
- Build final 5-in-1 menunggu empat game anggota lain masuk ke `main`; setelah itu `HubIntegrationTests` perlu dijalankan ulang.
