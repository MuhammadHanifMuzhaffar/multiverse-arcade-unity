# Laporan Audit Fondasi: Multiverse Arcade Unity

Tanggal: 2026-11-17 | Objek: `main` @ `8d2ae21` | Engine uji: Unity 2022.3.62f3 (batchmode, Windows 11)

## 1. Ringkasan

Fondasi berupa scaffold (PRD, README, 5 manager game, Hub, `SceneAutoRegister`). Arsitektur dan aturannya masuk akal, tetapi **scaffold di GitHub tidak bisa dikompilasi pada clone bersih** dan belum bisa dipakai 5 orang secara paralel. Semua temuan di bawah diverifikasi dengan menjalankan Unity sungguhan, bukan hanya membaca kode.

Status setelah perbaikan (belum di-commit): compile bersih, 6 scene kerangka terdaftar (MainMenu = index 0), validator 0 error, build Windows sukses (67 MB, 0 error), `.exe` berjalan tanpa exception pada uji headless.

## 2. Temuan dan status

| ID | Tingkat | Temuan | Status |
| :-- | :-- | :-- | :-- |
| C1 | Kritis | `Packages/manifest.json` hanya berisi `com.unity.ugui`; modul built-in (Audio, Physics, Animation, dst.) tidak aktif. `ReturnToHub` gagal compile: `CS1069 AudioSource`. Project tidak bisa dibuka tanpa error di laptop mana pun. (Terbukti di log baseline.) | Diperbaiki: 18 modul built-in didaftarkan |
| C2 | Kritis | Tidak ada `.meta` dan `ProjectSettings/` hanya berisi `ProjectVersion.txt`. Jika tiap anggota membuka project, Unity membuat GUID berbeda per laptop, lalu terjadi konflik `.meta` dan referensi scene putus. | Diperbaiki: dibangkitkan sekali (45 `.meta`, ProjectSettings, `packages-lock.json`). **Wajib di-commit satu kali oleh Integrator** |
| C3 | Kritis | Tidak ada satu pun scene; `SceneAutoRegister` tidak pernah bisa jalan (mode strict). | Diperbaiki: 6 scene kerangka dibuat otomatis, tidak menimpa scene yang sudah ada |
| H1 | Tinggi | Tidak ada asmdef: semua skrip masuk satu `Assembly-CSharp`. Satu error compile dari satu anggota memblokir keempat anggota lain, dan tidak ada penegakan isolasi antar-game. | Diperbaiki: asmdef per game + Hub + Editor |
| H2 | Tinggi | `ReturnToHub` tidak mereset `AudioListener.pause/volume` (state statis lintas scene). Game yang mem-pause audio membuat Hub bisu. | Diperbaiki |
| H3 | Tinggi | Akun GitHub aktif di laptop ini (`Velmiree`) hanya punya izin baca ke repo milik `MuhammadHanifMuzhaffar`. Branch `main` tidak diproteksi. | **Manual** (bagian 4) |
| H4 | Tinggi | README kontradiktif: langkah 2 menyuruh `git add .`, langkah 3 melarangnya. | Diperbaiki |
| H5 | Sedang | `.gitignore`: pola tidak ter-anchor (folder game bernama `Build`/`Library` akan ter-ignore diam-diam), `*.unitypackage`/`*.apk` tidak ada. `.gitattributes`: tanpa normalisasi line ending, tanpa merge driver YAML Unity, `jpg/jpeg/exr/hdr/flac/mov/webm` tidak masuk LFS, aturan `-text` tidak konsisten. | Diperbaiki |
| M1 | Sedang | `DragonHunterManager`: i-frame berbasis coroutine + flag bool. Dodge kedua dalam 0,6 detik dipotong oleh coroutine pertama (invulnerability berakhir terlalu cepat). | Diperbaiki (waktu akhir, bukan flag) |
| M2 | Sedang | `SkyGliderManager`: rekor awal selalu 0, bukan posisi glider. Spawn di y negatif memberi rekor salah di frame pertama. | Diperbaiki |
| M3 | Sedang | `HubManager`/`ReturnToHub`: scene tidak ada di Build Settings memunculkan exception, dan klik ganda memuat dua kali. Nama scene diduplikasi di 3 tempat. | Diperbaiki: guard + `SceneCatalog` sebagai sumber tunggal |
| M4 | Rendah | Namespace `Multiverse.Editor` membayangi `UnityEditor.Editor` bagi kode `Multiverse.*`. | Diperbaiki: `Multiverse.EditorTools` |

### Tidak diubah (keputusan desain milik PIC)
- DeepSeaExplorer: baterai habis tidak berdampak apa pun; kedalaman naik otomatis tanpa input pemain; pesan "lambung" memakai variabel oksigen.
- SkyGlider: `TriggerBoostPad` hanya mencetak log; rekor belum disimpan.
- CozyFruitCatcher: teks combo tampil "x1.0" pada streak 2-4.
- Update teks per frame membuat string baru tiap frame (garbage) di beberapa manager.
- Dragon Hunter: belum ada AI yang mengganti `DragonState`.

## 3. Yang ditambahkan (otomasi)

| Alat | Fungsi |
| :-- | :-- |
| `Tools/Multiverse/Validate Project` (+ CLI `ValidateBatch`) | Cek build settings, asmdef, 1 kamera + 1 AudioListener, Canvas Scaler 1920x1080/0,5, EventSystem, ReturnToHub, Missing Script, **isolasi dependensi aset antar-game**, **duplikat konten lintas game (termasuk hanya ganti nama)**, batas tekstur/model/audio, manifest aset |
| `Create Missing Skeleton Scenes` | Scene kerangka datar, kamera solid color (bukan skybox default), tombol Back to Hub, portal Hub ter-wire |
| `Register ... (Partial, Dev)` | Pendaftaran scene yang sudah ada saja |
| `Apply Project Settings` | Nama produk, 1920x1080, Linear color space, Mono |
| `Build Windows (Integrator)` | Build `Builds/Windows/MultiverseArcade.exe` |
| `.github/pull_request_template.md` | Checklist PIC dan Integrator |

Uji negatif validator (salinan terpisah, 5 pelanggaran sengaja): ISO-001, DUP-001, SCN-002, SCN-004, SCN-008 semuanya tertangkap; ISO-003 dan MAN-002 juga muncul sesuai harapan.

## 4. Langkah manual yang tersisa

1. **Akses push.** Salah satu: (a) minta pemilik repo menambahkan `Velmiree` sebagai Collaborator (Settings > Collaborators), atau (b) push dari akun `MuhammadHanifMuzhaffar`.
2. **Commit satu kali oleh Integrator** (semua perubahan di atas, termasuk `.meta`, `ProjectSettings/`, `Packages/packages-lock.json`), push, lalu minta empat anggota lain `git pull` sebelum mulai bekerja.
3. **Proteksi `main`** (Settings > Branches): wajib PR, minimal 1 approval, larang push langsung.
4. **CODEOWNERS** (`.github/CODEOWNERS`): petakan `/Assets/Games/<Folder>/` ke username GitHub masing-masing PIC dan `/Assets/Hub/`, `/ProjectSettings/`, `/Packages/` ke Integrator. Butuh username GitHub kelima anggota.
5. **Tiap laptop**: Unity 2022.3.62f3, `git lfs install`, setup merge driver (README bagian 3b), cek `git lfs ls-files` setelah commit binary pertama.
6. **Uji klik manual di Unity** (belum bisa diotomasi di sini): Play dari `MainMenu`, klik tiap portal, lalu Back to Hub. Cek Console bersih.
7. Uji **clone bersih** setelah push: clone ke folder baru, buka di Unity, validator 0 error.
8. Kuota **Git LFS GitHub gratis** (storage dan bandwidth bulanan terbatas): lima orang yang clone/pull berulang cepat menghabiskannya. Pantau di Settings > Billing, atau batasi ukuran aset.
9. Lisensi Unity: di batchmode log tercatat `No ULF license found` pada update lisensi (build tetap berhasil). Pastikan Unity Hub tetap login di laptop Integrator.

## 5. Rekomendasi pengembangan

Semua rekomendasi visual mengikuti aturan proyek: tanpa warna/skybox/sprite UI bawaan Unity, tanpa gradient generik; palet datar per game.

1. **Putuskan render pipeline sekarang.** Mengganti belakangan membuat semua material merah muda. Rekomendasi: tetap Built-in RP. Paling kecil risikonya untuk 5 anggota yang memakai aset berbeda-beda; bloom Cyber Neon Dodger lewat Post Processing v2 yang profilnya disimpan di folder game itu sendiri. URP baru layak jika semua PIC setuju memakai shader URP.
2. **Input Manager lama** sebagai standar (sudah default). Jangan campur dengan Input System.
3. **Jangan import TextMeshPro Essentials**: itu membuat `Assets/TextMesh Pro/` bersama, melanggar isolasi. Jika TMP dipakai, tiap game membuat font asset sendiri di foldernya.
4. **Palet datar per game** (placeholder sudah ada di scene kerangka, silakan diganti): Cyber Neon Dodger cyan/magenta pekat di atas hitam kebiruan, Dragon Hunter ember oranye di atas arang hangat, Cozy Fruit Catcher krem + persik, Deep Sea Explorer biru-hijau tua + teal, Sky Glider abu kebiruan terang + biru baja. Tiap game satu font sendiri, satu bahasa bentuk (sudut tajam untuk Cyber/Dragon, bulat untuk Cozy).
5. **Satu `GameBootstrap` kecil per game** untuk `Application.targetFrameRate = 60`, reset `Time.timeScale`, dan prefix `PlayerPrefs` agar rekor tidak saling menimpa.
6. **CI ringan tanpa Unity** (GitHub Actions): tolak PR jika branch `game/<nama>` mengubah file di luar `Assets/Games/<Folder>/` (+ `.meta`-nya), dan jika ada file Assets tanpa `.meta`. Belum ditulis karena tidak bisa diuji lokal tanpa push.
7. **Test Framework** (EditMode): uji logika murni (combo Fruit Catcher, i-frame Dragon Hunter, jatuh Sky Glider) setelah logika dipisah dari UI.
8. **Fade transisi antar-scene** di Hub dan `ReturnToHub` (overlay satu warna datar) agar perpindahan tidak terasa patah.
