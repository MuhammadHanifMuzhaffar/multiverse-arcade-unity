# Dragon Hunter — Dokumen Desain & Teknis (Sprint 1)

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
  Presentation/  ThirdPersonCamera, DragonHunterHUD, UIBar, HitFlash, ProceduralMeshes
Editor/   DragonHunterSceneBuilder (asmdef Editor sendiri)
Tests/    EditMode (20 test) · PlayMode (10 test di scene asli)
Materials/ 21 material milik Dragon Hunter (palet ember/arang)
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

## 6. Sprint 2 (berikutnya)

Model 3D naga & Hunter, SFX (raungan, api, pedang), musik battle, font eksklusif, efek hit. Setiap aset wajib dicatat di `README_ASSET_MANIFEST.md`.
