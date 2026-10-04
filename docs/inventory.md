# Inventory

Saat scene kantor dimainkan, sistem mengenali `Canvas/Save Area/Inventory Panel` dan memakai Slot 1 yang sudah dibuat sebagai template. Tidak perlu mengubah scene untuk mencoba.

- **Tab:** buka/tutup inventory (binding Journal).
- **Esc** atau **Kembali:** tutup.
- **Panah kanan:** Inventory Dock; **panah kiri:** kembali ke item.
- **Scroll mouse/drag:** gulir grid besar atau daftar dokumen.
- Klik slot berisi entri untuk menampilkan nama, jenis, deskripsi dan ikon di panel informasi.

Awal 3×3. Kolom dan baris masing-masing 1–8, sehingga grid persegi panjang juga didukung. Langkah posisi slot 100 unit Canvas. Dokumen tidak mengurangi kapasitas item dan tidak dibatasi jumlahnya. Inventory dimulai kosong; pickup, penggunaan item dan penghubung quest belum ditambahkan.

## Mengatur kapasitas di Editor

Di luar Play Mode, pilih **Saksi Terakhir > Inventory > Hubungkan UI Inventory**. Komponen **Inventory Panel Controller** muncul pada player. Atur **Columns** dan **Rows**, lalu simpan scene sendiri. Perintah ini mendukung Undo dan tidak menyimpan scene otomatis.

Saat Play, komponen juga muncul otomatis pada player. Mengubah Columns/Rows saat Play berlaku pada sesi itu saja. Resize yang lebih kecil daripada jumlah item ditolak supaya tidak membuang isi.

## Untuk tahap item berikutnya

Gunakan `InventoryPanelController.TryAdd(InventoryEntry)` untuk memasukkan entri. Jenis `Item` menuju grid; `Document` menuju dock. Hasil `false` menandakan entri gagal ditambahkan, termasuk saat grid penuh. Setiap entri item memakai satu slot, tanpa stacking.

Upgrade tas nantinya memanggil `TrySetGridSize(4, 4)` atau dimensi lain yang sah. Koleksi dan urutannya tetap dipertahankan. Data hanya hidup selama sesi, tanpa autosave atau file simpan.

## Verifikasi

Tes data mencakup batas dimensi, grid penuh, dokumen tanpa batas, upgrade yang mempertahankan isi, penolakan penyusutan, hapus dan reset. Pemeriksaan kompilasi memakai referensi Unity asli. Pengujian visual/Play Mode belum dilakukan karena koneksi ke Editor yang sedang terbuka belum tersedia.
