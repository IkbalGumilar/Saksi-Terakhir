# Inventory sederhana

Permintaan pengguna: gunakan slot UI yang sudah dibuat, mulai dengan grid 3×3, kolom dan baris masing-masing 1–8, maksimum 8×8. Upgrade tas nantinya mengubah kapasitas tanpa membuang item. Satu item menempati satu slot. Dokumen masuk Inventory Dock yang tidak dibatasi kapasitas dan tersusun vertikal.

Langkah posisi antar-slot adalah 100 unit Canvas di kedua halaman, bukan tambahan ruang kosong 100 di luar ukuran slot. Grid besar dapat digulir, agar slot tidak diperkecil. Dock hanya menggulir vertikal. Slot dan baris diduplikasi dari contoh milik pengguna; informasi sebelah kiri mengikuti pilihan.

Tab (aksi Journal yang sudah tersedia) membuka/menutup inventory, Esc menutup. Panah kanan dari inventory membuka dock; panah kiri mengembalikan grid. Pemain tidak dapat bergerak, menggerakkan kamera atau berinteraksi dengan dunia selama panel terbuka. Inventory tidak dibuka di tengah dialog atau gerakan cerita yang terkunci.

Data inventory hanya hidup selama sesi. Belum ada autosave, quick use, stacking, pickup, toko, penggunaan item, aset item baru atau penghubung kunci mobil cerita. Itu pekerjaan berikutnya.

Editor sedang terbuka dan koneksi Pipeline belum tersedia. Integrasi awal memakai bootstrap runtime yang mengenali panel scene ini. Tidak mengedit YAML scene. Menu Editor opsional memasang komponen dan referensi dengan Undo tanpa menyimpan otomatis. Tidak membuat cabang/commit baru.
