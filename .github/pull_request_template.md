## Ringkasan
<!-- Apa yang berubah dan di game/modul mana. -->

## Checklist PIC (wajib sebelum minta review)
- [ ] Branch bernama `game/<nama>` dan perubahan HANYA di `Assets/Games/<FolderSaya>/` (beserta `.meta`-nya).
- [ ] Setiap file baru di Unity disertai file `.meta` yang ikut di-commit.
- [ ] `Tools > Multiverse > Validate Project` dijalankan: 0 error.
- [ ] `README_ASSET_MANIFEST.md` game saya diperbarui (sumber + lisensi tiap aset).
- [ ] Tidak ada aset yang sama (byte-identik atau hanya diganti nama) dengan game lain.
- [ ] Scene game: 1 Main Camera + 1 AudioListener, Canvas Scaler 1920x1080 match 0.5, tombol Back to Hub memakai `ReturnToHub`.
- [ ] Console Unity bersih dari error merah setelah Play dari `MainMenu` -> game -> kembali.

## Untuk Integrator
- [ ] Diff tidak menyentuh `Assets/Hub/`, `ProjectSettings/`, `Packages/` (kecuali PR Integrator).
- [ ] Clone bersih + import tanpa error.
